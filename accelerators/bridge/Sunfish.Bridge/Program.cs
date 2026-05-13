using Sunfish.UICore.Contracts;
using Sunfish.Foundation.Extensions;
using Sunfish.UIAdapters.Blazor.Internal.Interop;
using Sunfish.Providers.Bootstrap;
using Sunfish.Providers.Bootstrap.Extensions;
using Sunfish.Providers.FluentUI;
using Sunfish.Providers.FluentUI.Extensions;
using Sunfish.Providers.Material;
using Sunfish.Providers.Material.Extensions;
using Sunfish.Bridge;
using Sunfish.Bridge.Localization;
using Sunfish.Bridge.Client.Services;
using Sunfish.Bridge.Authorization;
using Sunfish.Bridge.Components;
using Sunfish.Bridge.Field;
using Sunfish.Bridge.Orchestration;
using Sunfish.Bridge.Services;
using Sunfish.Foundation.Authorization;
using Sunfish.Bridge.Data;
using Sunfish.Bridge.Data.Seeding;
using Sunfish.Bridge.Hubs;
using Sunfish.Bridge.Middleware;
using Sunfish.Bridge.Proxy;
using Sunfish.Bridge.Relay;
using Sunfish.Foundation.Catalog.Bundles;
using Sunfish.Foundation.FeatureManagement;
using Sunfish.Blocks.Subscriptions.DependencyInjection;
using Sunfish.Blocks.TenantAdmin.DependencyInjection;
using Sunfish.Blocks.BusinessCases.DependencyInjection;
using Sunfish.Blocks.PublicListings.DependencyInjection;
using Sunfish.Bridge.Listings;
using Sunfish.Bridge.SystemRequirements;
using Sunfish.Bridge.Features.Identity;
using Sunfish.Blocks.ShipsOffice;
using Sunfish.UICore.Wayfinder;
using Sunfish.Kernel.Sync.DependencyInjection;
using Sunfish.Kernel.Security.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Wolverine;
using Wolverine.RabbitMQ;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);

// ADR 0026 — dual-posture Bridge. Resolve the posture once, then branch
// the composition root. SaaS (default) keeps Bridge's existing Blazor-
// Server + Postgres + DAB + SignalR stack. Relay swaps the whole graph
// for a paper §6.1 tier-3 managed relay (kernel-sync + kernel-security
// only, no authority semantics).
var bridgeOptions = new BridgeOptions();
builder.Configuration.GetSection(BridgeOptions.SectionName).Bind(bridgeOptions);
builder.Services.Configure<BridgeOptions>(
    builder.Configuration.GetSection(BridgeOptions.SectionName));

// Aspire service defaults (OTEL, health checks, resilience, service discovery).
builder.AddServiceDefaults();

// W#23 P4.5 audit-infra (Option C in-memory v1 per the audit-infra unblock
// addendum). Bridge's first audit-emitting flow is the /api/v1/field/* route
// family; v1 is restart-volatile + non-verifying. Persistent + verifying audit
// infra defers to ~ADR 0076.
//
// v1: signer key is process-volatile per ~ADR 0076. Every Bridge restart
// generates a fresh Ed25519 keypair; audit records issued under previous
// restarts cannot be verifier-cross-checked against the new key. Production
// Bridge identity (signer key in IRootKeyStore or equivalent) lands with
// ~ADR 0076.
builder.Services.AddSingleton<Sunfish.Kernel.Audit.IAuditTrail, Sunfish.Kernel.Audit.InMemoryAuditTrail>();
builder.Services.AddSingleton<Sunfish.Foundation.Crypto.IOperationSigner>(
    _ => new Sunfish.Foundation.Crypto.Ed25519Signer(Sunfish.Foundation.Crypto.KeyPair.Generate()));

if (bridgeOptions.Mode == BridgeMode.Relay)
{
    ConfigureRelayPosture(builder);
}
else
{
    ConfigureSaasPosture(builder);
}

var app = builder.Build();

app.Logger.LogInformation(
    "Bridge starting in {Mode} posture (ADR 0026).", bridgeOptions.Mode);

if (bridgeOptions.Mode == BridgeMode.Relay)
{
    // Relay posture is headless — no Razor components, no SignalR hub, no
    // DAB/Wolverine. The hosted RelayWorker owns the accept loop.
    app.MapDefaultEndpoints();
    app.MapHealthChecks("/health");
    app.Run();
    return;
}

// --- SaaS posture request pipeline (original behaviour) -----------------

// Seed the bundle catalog with the five manifests shipped as embedded resources
// by Sunfish.Foundation.Catalog. Runs once at startup; idempotent across duplicate
// calls because Register() throws on duplicate keys (startup is single-pass).
{
    var catalog = app.Services.GetRequiredService<IBundleCatalog>();
    foreach (var logicalName in BundleManifestLoader.ListEmbeddedBundleResourceNames())
    {
        catalog.Register(BundleManifestLoader.LoadEmbedded(logicalName));
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Plan 2 Task 4.1 + 4.2 — request-culture resolution before any localized output.
// Pipeline order: HTTPS redirect → request localization → tenant subdomain → auth.
// CurrentUICulture set here flows into SunfishProblemDetailsFactory and any
// IStringLocalizer<T> consumer in the request handler chain.
app.UseRequestLocalization();

// Wave 5.3.A — resolve the tenant off the request subdomain and bind
// IBrowserTenantContext before downstream auth/authorization sees the
// request. Registered in the SaaS-posture composition root only (Relay
// returns early above).
app.UseMiddleware<TenantSubdomainResolutionMiddleware>();

// Wave 5.3.C — enable WebSocket upgrades before the /ws reverse proxy is
// mapped below.
app.UseWebSockets();

app.UseAntiforgery();
app.UseAuthorization();
// W#60 Phase 2 — CORS for React dev server (localhost:5173).
// In production React is served from the same origin; this no-ops there.
if (app.Environment.IsDevelopment())
{
    app.UseCors("ReactDevCors");
}

app.MapDefaultEndpoints();
app.MapHealthChecks("/health");
// Wave 5.3.C — reverse-proxy browser /ws connections to the tenant child's
// /ws endpoint on its local-node-host process. Auth gate (Wave 5.3.B) is
// still pending; IBrowserTenantContext must be resolved by the subdomain
// middleware above.
app.MapTenantWebSocketProxy();
app.MapHub<BridgeHub>("/hubs/bridge");

// W#28 Phase 5c-1 — public-listings discovery surface (robots.txt + sitemap.xml).
// Human-facing /listings + /listings/{slug} pages and the inquiry POST path
// follow in subsequent phases.
app.MapListingsEndpoints();

// W#56 Phase 1 — SystemRequirements JSON contract + Bridge endpoint.
// GET /api/system-requirements/{bundleId}?platform={platformKey} → SystemRequirementsResult JSON.
// POST /api/system-requirements/{bundleId}/force-install → 204 No Content.
app.MapSystemRequirementsEndpoints();

// W#60 Phase 2 — ERPNext proxy (GET /api/v1/erpnext/properties, more in later phases).
app.MapERPNextProxy();

// W#60 Phase 2 — caller-identity endpoint consumed by the React SPA.
// Phase 1 returns a dev stub; Phase 2 wires real OIDC claims via UserService.
app.MapGet("/api/v1/whoami", (HttpContext ctx) =>
{
    var name = ctx.User.Identity?.Name ?? "dev-user";
    return Results.Ok(new
    {
        user = name,
        role = "owner",
        defaultCompany = app.Configuration["ERPNext:DefaultCompany"] ?? "",
        availableCompanies = new[] { app.Configuration["ERPNext:DefaultCompany"] ?? "" },
    });
}).WithName("WhoAmI");

// W#23 Phase 4.5 — iOS field-capture endpoints (POST /api/v1/field/event +
// POST /api/v1/field/blob/{sha256}). Per the audit-infra unblock addendum,
// audit emission is in-memory only for v1; persistent infra defers to ~ADR 0076.
app.MapFieldEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Sunfish.Bridge.Client._Imports).Assembly);

app.Run();


// =======================================================================
//  Composition helpers
// =======================================================================

// SaaS posture: the Bridge shell per ADR 0006. Kept as it was before
// ADR 0026 — we only refactored it into a helper so the posture branch at
// the top of Main stays readable.
static void ConfigureSaasPosture(WebApplicationBuilder builder)
{
    // Tenant context — demo stub in development. Registered BEFORE the DbContext
    // because SunfishBridgeDbContext takes ITenantContext as a constructor dependency.
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddScoped<ITenantContext, DemoTenantContext>();
        builder.Services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, DemoAuthWarningFilter>();
    }

    // EF Core DbContext registered manually (not pooled) so it can accept the scoped
    // ITenantContext in its constructor — Aspire's AddNpgsqlDbContext uses DbContextPool
    // which forbids scoped constructor dependencies. EnrichNpgsqlDbContext layers
    // Aspire's instrumentation, health checks, and retry on top.
    builder.Services.AddDbContext<SunfishBridgeDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("sunfishbridgedb")));
    builder.EnrichNpgsqlDbContext<SunfishBridgeDbContext>();

    // ADR 0031 Wave 5.2.A — per-tenant orchestration contracts. Registers
    // ITenantRegistryEventBus (singleton InMemoryTenantRegistryEventBus) so
    // TenantRegistry (Wave 5.2.B) can publish lifecycle events and the
    // Wave 5.2.C supervisor can subscribe without coupling to the registry.
    builder.Services.AddBridgeOrchestration();

    // Wave 5.2.E — bind BridgeOrchestrationOptions from configuration so the
    // supervisor, health monitor, and lifecycle coordinator all read the same
    // TenantDataRoot / LocalNodeExecutablePath / RelayRefreshInterval values
    // that AppHost passes via environment (Bridge__Orchestration__*). The
    // AddBridgeOrchestration overload only wires a delegate if one is supplied;
    // we bind from config here so sysadmins can override without recompiling.
    builder.Services.Configure<BridgeOrchestrationOptions>(
        builder.Configuration.GetSection("Bridge:Orchestration"));

    // Wave 5.2.D — per-tenant health-monitoring surface. Registers the
    // endpoint registry + background poller.
    builder.Services.AddBridgeOrchestrationHealth();

    // Wave 5.2.C.1 — Process.Start-based per-tenant supervisor + the
    // lifecycle coordinator that bridges the registry event bus to it. Aspire
    // AddProject boot path layers on top in 5.2.C.2 once stop-work #3 is
    // resolved.
    //
    // AddBridgeOrchestrationSupervisor depends on IRootSeedProvider being
    // registered for TenantSeedProvider (W5.2 stop-work #1). Register the
    // keystore-backed provider here so per-tenant seed derivation works out
    // of the box; tests override with an InMemoryKeystore-backed provider
    // by inserting their own registration before invoking the supervisor
    // extension (TryAdd semantics).
    builder.Services.AddSunfishRootSeedProvider();
    builder.Services.AddBridgeOrchestrationSupervisor();

    // Wave 5.2.E — periodic relay-allowlist refresh. Re-reads
    // TenantRegistry.ListActiveAsync every RelayRefreshInterval and updates
    // the RelayServer (if one is resolvable) with the active tenant set. In
    // the SaaS posture the relay is usually absent (that lives in the Relay
    // posture); the refresher no-ops when no IRelayServer is registered.
    builder.Services.AddHostedService<BridgeRelayAllowlistRefresher>();

    // ADR 0031 Wave 5.1 — control-plane tenant registry. Scoped to match the
    // DbContext lifetime. Holds no team data; see TenantRegistry.cs.
    builder.Services.AddScoped<ITenantRegistry, TenantRegistry>();

    // Wave 5.3.A — browser-shell tenant-resolution surface (see
    // _shared/product/wave-5.3-decomposition.md §5.3.A). IBrowserTenantContext
    // is distinct from ITenantContext (registered above); the two coexist
    // but must not be mixed in a single request pipeline. Only composed in
    // the SaaS posture — Relay has no browser shell and Demo's static
    // DemoTenantContext covers dev-loop needs.
    builder.Services.Configure<TenantResolutionOptions>(
        builder.Configuration.GetSection("Bridge:BrowserShell:TenantResolution"));
    builder.Services.AddScoped<IBrowserTenantContext, BrowserTenantContext>();

    builder.AddRedisOutputCache("bridge-redis");

    // SignalR with Redis backplane.
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(builder.Configuration.GetConnectionString("bridge-redis") ?? "localhost");

    // Feature flags.
    builder.Services.AddFeatureManagement();

    // P1 domain blocks + bundle catalog + feature-management chain
    // (ADR 0015 module-entity registration, ADR 0007 bundles, ADR 0009 features).
    builder.Services.AddSunfishBundleCatalog();
    builder.Services.AddSunfishFeatureManagement();
    // W#58 Phase 2 — Identity Atlas projection surface + circuit context capture.
    builder.Services.AddScoped<IBridgeRequestContext, BridgeRequestContext>();
    builder.Services.AddScoped<IIdentityAtlasSurface, BridgeIdentityAtlasSurface>();

    // W#55 Phase 4 — Ship's Office aggregation surface (ADR 0083).
    // RequireSecondActorPublish=true is the regulated-default for the multi-tenant
    // admin Bridge posture; Anchor uses false (default) for single-operator use.
    // NOTE: Bridge React UI is deferred (no blocks-ships-office React adapter yet).
    builder.Services.AddSunfishShipsOfficeDefaults();
    builder.Services.Configure<Sunfish.Foundation.ShipsOffice.ShipsOfficeOptions>(opts =>
    {
        opts.SnapshotPageSize = 500;
        opts.RequireSecondActorPublish = true;
    });

    // W#56 Phase 1 — SystemRequirements resolver + force-install surface + server-level envelope.
    builder.Services.AddBridgeSystemRequirements();
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Demo"))
        builder.Services.AddInMemorySubscriptions();
    builder.Services.AddInMemoryTenantAdmin();
    builder.Services.AddInMemoryBusinessCases();
    builder.Services.AddInMemoryPublicListings();

    // W#28 boundary: the public-listings inquiry POST forwards verified-defense
    // submissions to the leasing pipeline's IPublicInquiryService.
    // InMemoryLeasingPipelineService implements both ILeasingPipelineService
    // and IPublicInquiryService, so one singleton covers both seams.
    // W#22 Phase 9: passes IFieldEncryptor through the factory when registered
    // (typically via W#32's AddSunfishRecoveryCoordinator); when not wired,
    // the service falls back to the all-null DemographicProfile path so
    // plaintext is dropped rather than silently retained.
    builder.Services.AddSingleton<Sunfish.Blocks.PropertyLeasingPipeline.Services.InMemoryLeasingPipelineService>(sp =>
        new Sunfish.Blocks.PropertyLeasingPipeline.Services.InMemoryLeasingPipelineService(
            prospectPromoter: sp.GetService<Sunfish.Blocks.PublicListings.Capabilities.ICapabilityPromoter>(),
            inquiryValidator: sp.GetService<Sunfish.Blocks.PropertyLeasingPipeline.Services.IInquiryValidator>(),
            auditTrail: null,
            signer: null,
            tenantId: default,
            paymentGateway: sp.GetService<Sunfish.Foundation.Integrations.Payments.IPaymentGateway>(),
            fieldEncryptor: sp.GetService<Sunfish.Foundation.Recovery.Crypto.IFieldEncryptor>(),
            time: null));
    builder.Services.AddSingleton<Sunfish.Blocks.PropertyLeasingPipeline.Services.IPublicInquiryService>(
        sp => sp.GetRequiredService<Sunfish.Blocks.PropertyLeasingPipeline.Services.InMemoryLeasingPipelineService>());
    builder.Services.AddSingleton<Sunfish.Blocks.PropertyLeasingPipeline.Services.ILeasingPipelineService>(
        sp => sp.GetRequiredService<Sunfish.Blocks.PropertyLeasingPipeline.Services.InMemoryLeasingPipelineService>());

    // Wolverine messaging — RabbitMQ transport, Postgres outbox.
    builder.Host.UseWolverine(opts =>
    {
        var rabbitConn = builder.Configuration.GetConnectionString("bridge-rabbit");
        if (!string.IsNullOrWhiteSpace(rabbitConn))
        {
            opts.UseRabbitMq(rabbitConn).AutoProvision();
        }

        var pgConn = builder.Configuration.GetConnectionString("sunfishbridgedb");
        if (!string.IsNullOrWhiteSpace(pgConn))
        {
            opts.PersistMessagesWithPostgresql(pgConn, "wolverine");
        }

        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        opts.Policies.AutoApplyTransactions();
    });

    // Dev-only data seed.
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddHostedService<BridgeSeeder>();
    }

    // CORS — allow React dev server (localhost:5173) to call Bridge API endpoints.
    // Policy is dev-only; production serves React from the same origin as Bridge.
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ReactDevCors", policy =>
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials());
    });
    builder.Services.AddAuthorization();

    // W#60 Phase 2 — ERPNext proxy layer.
    builder.Services.Configure<ERPNextOptions>(
        builder.Configuration.GetSection(ERPNextOptions.SectionName));
    builder.Services.AddERPNextClient();

    // Sunfish component services with provider switching (FluentUI, Bootstrap, Material)
    builder.Services.AddSunfish()
        .AddSunfishInteropServices();
    builder.Services.AddSingleton(new FluentUIOptions());
    builder.Services.AddSingleton(new BootstrapOptions());
    builder.Services.AddSingleton(new MaterialOptions());
    builder.Services.AddScoped<FluentUICssProvider>();
    builder.Services.AddScoped<FluentUIIconProvider>();
    builder.Services.AddScoped<FluentUIJsInterop>();
    builder.Services.AddScoped<BootstrapCssProvider>();
    builder.Services.AddScoped<BootstrapIconProvider>();
    builder.Services.AddScoped<BootstrapJsInterop>();
    builder.Services.AddScoped<MaterialCssProvider>();
    builder.Services.AddScoped<MaterialIconProvider>();
    builder.Services.AddScoped<MaterialJsInterop>();
    builder.Services.AddScoped<ProviderSwitcher>();
    builder.Services.AddScoped<ISunfishCssProvider>(sp => sp.GetRequiredService<ProviderSwitcher>());
    builder.Services.AddScoped<ISunfishIconProvider>(sp => sp.GetRequiredService<ProviderSwitcher>());
    builder.Services.AddScoped<ISunfishJsInterop>(sp => sp.GetRequiredService<ProviderSwitcher>());

    // PM Demo canonical notification pipeline. Single source of truth for all
    // user-facing notifications (bell, inbox, toast). The canonical service owns
    // lifecycle; ISunfishNotificationService is a downstream toast presentation channel
    // reached via the IUserNotificationToastForwarder adapter.
    builder.Services.AddScoped<Sunfish.Foundation.Notifications.IUserNotificationToastForwarder,
                                Sunfish.Bridge.Client.Notifications.SunfishToastUserNotificationForwarder>();
    builder.Services.AddScoped<Sunfish.Foundation.Notifications.IUserNotificationService,
                                Sunfish.Bridge.Client.Notifications.InMemoryUserNotificationService>();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // Localization (Plan 2 Task 4.1 + 4.2). Wires:
    //  - IStringLocalizer<T> resource resolution against `Resources/`,
    //  - request-culture flow via Accept-Language → user profile → tenant default,
    //  - SunfishProblemDetailsFactory replacing the framework default so server-error
    //    Title + Detail render in the request culture.
    // Supports the spec §4 12-locale roster (en-US, es-419, pt-BR, fr, de, ja,
    // zh-Hans, ar-SA, hi, he-IL, fa-IR, ko); RTL locales (ar-SA / he-IL / fa-IR)
    // are flipped to dir="rtl" by RequestLocalizationMiddleware downstream.
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
    builder.Services.AddSunfishLocalizedProblemDetails();
    builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(options =>
    {
        var supported = new[]
        {
            "en-US", "es-419", "pt-BR", "fr", "de", "ja",
            "zh-Hans", "ar-SA", "hi", "he-IL", "fa-IR", "ko",
        };
        options.SetDefaultCulture("en-US");
        options.AddSupportedCultures(supported);
        options.AddSupportedUICultures(supported);
    });
}

// Relay posture: paper §6.1 tier-3 managed relay. No Postgres, no DAB, no
// Wolverine, no Razor components — just kernel-sync transport + a hosted
// worker that runs the accept loop.
static void ConfigureRelayPosture(WebApplicationBuilder builder)
{
    // Pick a listen endpoint BEFORE AddSunfishKernelSync so TryAddSingleton<ISyncDaemonTransport>
    // sees our listen-enabled instance first. Falls back to a platform-appropriate
    // default path when BridgeOptions.Relay.ListenEndpoint is null.
    var relayOpts = new RelayOptions();
    builder.Configuration.GetSection($"{BridgeOptions.SectionName}:Relay").Bind(relayOpts);
    var listenEndpoint = !string.IsNullOrWhiteSpace(relayOpts.ListenEndpoint)
        ? relayOpts.ListenEndpoint
        : OperatingSystem.IsWindows()
            ? "sunfish-bridge-relay"                       // named pipe name
            : "/tmp/sunfish-bridge-relay.sock";            // Unix-domain socket path

    builder.Services.AddSingleton<Sunfish.Kernel.Sync.Protocol.ISyncDaemonTransport>(
        _ => new Sunfish.Kernel.Sync.Protocol.UnixSocketSyncDaemonTransport(listenEndpoint));

    builder.Services.AddSunfishKernelSync();
    builder.Services.AddSunfishKernelSecurity();

    builder.Services.AddAuthorization();

    builder.Services.AddSingleton<IRelayServer, RelayServer>();
    builder.Services.AddHostedService<RelayWorker>();
}

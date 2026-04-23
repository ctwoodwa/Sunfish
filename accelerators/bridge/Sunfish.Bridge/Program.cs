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
using Sunfish.Bridge.Client.Services;
using Sunfish.Bridge.Authorization;
using Sunfish.Bridge.Components;
using Sunfish.Foundation.Authorization;
using Sunfish.Bridge.Data;
using Sunfish.Bridge.Data.Seeding;
using Sunfish.Bridge.Hubs;
using Sunfish.Bridge.Relay;
using Sunfish.Foundation.Catalog.Bundles;
using Sunfish.Foundation.FeatureManagement;
using Sunfish.Blocks.Subscriptions.DependencyInjection;
using Sunfish.Blocks.TenantAdmin.DependencyInjection;
using Sunfish.Blocks.BusinessCases.DependencyInjection;
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

app.UseAntiforgery();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapHealthChecks("/health");
app.MapHub<BridgeHub>("/hubs/bridge");

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
    builder.Services.AddInMemorySubscriptions();
    builder.Services.AddInMemoryTenantAdmin();
    builder.Services.AddInMemoryBusinessCases();

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

    // CORS + antiforgery scaffold (no policies yet).
    builder.Services.AddCors();
    builder.Services.AddAuthorization();

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

using Sunfish.UICore.Contracts;
using Sunfish.Foundation.Extensions;
using Sunfish.Components.Blazor.Providers.Bootstrap;
using Sunfish.Components.Blazor.Providers.Bootstrap.Extensions;
using Sunfish.Components.Blazor.Providers.FluentUI;
using Sunfish.Components.Blazor.Providers.FluentUI.Extensions;
using Sunfish.Components.Blazor.Providers.Material;
using Sunfish.Components.Blazor.Providers.Material.Extensions;
using Sunfish.Bridge.Client.Services;
using Sunfish.Bridge.Authorization;
using Sunfish.Bridge.Components;
using Sunfish.Bridge.Data;
using Sunfish.Bridge.Data.Authorization;
using Sunfish.Bridge.Data.Seeding;
using Sunfish.Bridge.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Wolverine;
using Wolverine.RabbitMQ;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults (OTEL, health checks, resilience, service discovery).
builder.AddServiceDefaults();

// Tenant context — demo stub in development. Registered BEFORE the DbContext
// because SunfishBridgeDbContext takes ITenantContext as a constructor dependency.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<ITenantContext, DemoTenantContext>();
}

// EF Core DbContext registered manually (not pooled) so it can accept the scoped
// ITenantContext in its constructor — Aspire's AddNpgsqlDbContext uses DbContextPool
// which forbids scoped constructor dependencies. EnrichNpgsqlDbContext layers
// Aspire's instrumentation, health checks, and retry on top.
builder.Services.AddDbContext<SunfishBridgeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("pmdemodb")));
builder.EnrichNpgsqlDbContext<SunfishBridgeDbContext>();

builder.AddRedisOutputCache("pmdemo-redis");

// SignalR with Redis backplane.
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("pmdemo-redis") ?? "localhost");

// Feature flags.
builder.Services.AddFeatureManagement();

// Wolverine messaging — RabbitMQ transport, Postgres outbox.
builder.Host.UseWolverine(opts =>
{
    var rabbitConn = builder.Configuration.GetConnectionString("pmdemo-rabbit");
    if (!string.IsNullOrWhiteSpace(rabbitConn))
    {
        opts.UseRabbitMq(rabbitConn).AutoProvision();
    }

    var pgConn = builder.Configuration.GetConnectionString("pmdemodb");
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
builder.Services.AddSunfish().AddSunfishCoreServices();
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
builder.Services.AddScoped<Sunfish.Bridge.Client.Notifications.IUserNotificationToastForwarder,
                            Sunfish.Bridge.Client.Notifications.SunfishToastUserNotificationForwarder>();
builder.Services.AddScoped<Sunfish.Bridge.Client.Notifications.IUserNotificationService,
                            Sunfish.Bridge.Client.Notifications.InMemoryUserNotificationService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

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
app.MapHub<BridgeHub>("/hubs/pmdemo");

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Sunfish.Bridge.Client._Imports).Assembly);

app.Run();

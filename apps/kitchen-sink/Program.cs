using Sunfish.UICore.Contracts;
using Sunfish.Foundation.Extensions;
using Sunfish.KitchenSink.Data;
using Sunfish.KitchenSink.Services;
using Sunfish.Providers.FluentUI;
using Sunfish.Providers.FluentUI.Extensions;
#if BOOTSTRAP_PROVIDER
using Sunfish.Providers.Bootstrap;
using Sunfish.Providers.Bootstrap.Extensions;
#endif
#if MATERIAL_PROVIDER
using Sunfish.Providers.Material;
using Sunfish.Providers.Material.Extensions;
#endif

var builder = WebApplication.CreateBuilder(args);

var isContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);
var hasExplicitHttpsCertificate =
    !string.IsNullOrWhiteSpace(builder.Configuration["Kestrel:Certificates:Default:Path"]) ||
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path"));
var useHttpOnly = builder.Environment.IsDevelopment() && isContainer && !hasExplicitHttpsCertificate;

if (useHttpOnly)
{
    // Containers often do not have a trusted dev cert; run the demo over HTTP.
    builder.WebHost.UseUrls("http://0.0.0.0:5301");
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register core Sunfish services (theme service, notifications, etc.).
// AddSunfish() is a single entry point that replaces the legacy three-call registration.
builder.Services.AddSunfish();

// FluentUI (mandatory) — provider switcher needs the concrete types,
// so we register those explicitly instead of using AddSunfishFluentUI().
builder.Services.AddSingleton(new FluentUIOptions());
builder.Services.AddScoped<FluentUICssProvider>();
builder.Services.AddScoped<FluentUIIconProvider>();
builder.Services.AddScoped<FluentUIJsInterop>();

#if BOOTSTRAP_PROVIDER
builder.Services.AddSingleton(new BootstrapOptions());
builder.Services.AddScoped<BootstrapCssProvider>();
builder.Services.AddScoped<BootstrapIconProvider>();
builder.Services.AddScoped<BootstrapJsInterop>();
#endif
#if MATERIAL_PROVIDER
builder.Services.AddSingleton(new MaterialOptions());
builder.Services.AddScoped<MaterialCssProvider>();
builder.Services.AddScoped<MaterialIconProvider>();
builder.Services.AddScoped<MaterialJsInterop>();
#endif

// Register the switcher as the implementation for all three interfaces
builder.Services.AddScoped<ProviderSwitcher>();
builder.Services.AddScoped<ISunfishCssProvider>(sp => sp.GetRequiredService<ProviderSwitcher>());
builder.Services.AddScoped<ISunfishIconProvider>(sp => sp.GetRequiredService<ProviderSwitcher>());
builder.Services.AddScoped<ISunfishJsInterop>(sp => sp.GetRequiredService<ProviderSwitcher>());

builder.Services.AddScoped<FavoritesService>();

var siteLinksPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "site-links.json");
if (File.Exists(siteLinksPath))
    builder.Configuration.AddJsonFile(Path.GetFullPath(siteLinksPath), optional: true);

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new SiteLinks
    {
        DocsBaseUrl = config["docsBaseUrl"] ?? "http://localhost:8081",
        DemoBaseUrl = config["demoBaseUrl"] ?? "http://localhost:5301"
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (!useHttpOnly)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<Sunfish.KitchenSink.App>()
    .AddInteractiveServerRenderMode();

app.Run();

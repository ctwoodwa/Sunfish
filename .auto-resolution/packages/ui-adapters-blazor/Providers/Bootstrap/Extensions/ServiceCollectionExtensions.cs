using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Extensions;
using Sunfish.UICore.Contracts;

namespace Sunfish.Providers.Bootstrap.Extensions;

public class BootstrapOptions
{
    public SunfishTheme? Theme { get; set; }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Sunfish Bootstrap design-system provider on the given <see cref="SunfishBuilder"/>.
    /// Registers <see cref="ISunfishCssProvider"/>, <see cref="ISunfishIconProvider"/>, and
    /// <see cref="ISunfishJsInterop"/> as scoped services.
    /// </summary>
    public static SunfishBuilder AddSunfishBootstrap(this SunfishBuilder builder, Action<BootstrapOptions>? configure = null)
    {
        var options = new BootstrapOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<ISunfishCssProvider, BootstrapCssProvider>();
        builder.Services.AddScoped<ISunfishIconProvider, BootstrapIconProvider>();
        builder.Services.AddScoped<ISunfishJsInterop, BootstrapJsInterop>();
        return builder;
    }
}

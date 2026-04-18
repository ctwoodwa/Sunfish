using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Extensions;
using Sunfish.UICore.Contracts;

namespace Sunfish.Providers.FluentUI.Extensions;

public class FluentUIOptions
{
    public SunfishTheme? Theme { get; set; }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Sunfish Fluent UI design-system provider on the given <see cref="SunfishBuilder"/>.
    /// Registers <see cref="ISunfishCssProvider"/>, <see cref="ISunfishIconProvider"/>, and
    /// <see cref="ISunfishJsInterop"/> as scoped services.
    /// </summary>
    public static SunfishBuilder AddSunfishFluentUI(this SunfishBuilder builder, Action<FluentUIOptions>? configure = null)
    {
        var options = new FluentUIOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<ISunfishCssProvider, FluentUICssProvider>();
        builder.Services.AddScoped<ISunfishIconProvider, FluentUIIconProvider>();
        builder.Services.AddScoped<ISunfishJsInterop, FluentUIJsInterop>();
        return builder;
    }
}

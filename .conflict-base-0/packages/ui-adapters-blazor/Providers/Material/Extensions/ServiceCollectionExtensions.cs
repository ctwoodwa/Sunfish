using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Extensions;
using Sunfish.UICore.Contracts;

namespace Sunfish.Providers.Material.Extensions;

public class MaterialOptions
{
    public SunfishTheme? Theme { get; set; }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Sunfish Material Design 3 provider on the given <see cref="SunfishBuilder"/>.
    /// Registers <see cref="ISunfishCssProvider"/>, <see cref="ISunfishIconProvider"/>, and
    /// <see cref="ISunfishJsInterop"/> as scoped services.
    /// </summary>
    public static SunfishBuilder AddSunfishMaterial(this SunfishBuilder builder, Action<MaterialOptions>? configure = null)
    {
        var options = new MaterialOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<ISunfishCssProvider, MaterialCssProvider>();
        builder.Services.AddScoped<ISunfishIconProvider, MaterialIconProvider>();
        builder.Services.AddScoped<ISunfishJsInterop, MaterialJsInterop>();
        return builder;
    }
}

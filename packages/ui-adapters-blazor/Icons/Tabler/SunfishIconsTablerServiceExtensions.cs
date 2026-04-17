using Microsoft.Extensions.DependencyInjection;
using Sunfish.UICore.Contracts;

namespace Sunfish.Icons.Tabler;

public static class SunfishIconsTablerServiceExtensions
{
    /// <summary>
    /// Registers the Tabler SVG sprite icon provider as <see cref="ISunfishIconProvider"/>.
    /// Only one icon provider should be registered; calling both this and
    /// <c>AddSunfishIconsLegacy</c> results in last-registration-wins.
    /// </summary>
    public static IServiceCollection AddSunfishIconsTabler(this IServiceCollection services)
    {
        services.AddSingleton<ISunfishIconProvider, SunfishTablerIconProvider>();
        return services;
    }
}

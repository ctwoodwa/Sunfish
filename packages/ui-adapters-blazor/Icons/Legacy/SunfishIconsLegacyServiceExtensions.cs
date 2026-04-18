using Microsoft.Extensions.DependencyInjection;
using Sunfish.UICore.Contracts;

namespace Sunfish.Icons.Legacy;

[Obsolete("Use Sunfish.Icons.Tabler. Legacy icon set retained for backward compatibility only.")]
public static class SunfishIconsLegacyServiceExtensions
{
    /// <summary>
    /// Registers the legacy Sunfish icon provider as <see cref="ISunfishIconProvider"/>.
    /// Only one icon provider should be registered; last-registration-wins.
    /// </summary>
    [Obsolete("Use AddSunfishIconsTabler() from Sunfish.Icons.Tabler instead.")]
    public static IServiceCollection AddSunfishIconsLegacy(this IServiceCollection services)
    {
        services.AddSingleton<ISunfishIconProvider, SunfishLegacyIconProvider>();
        return services;
    }
}

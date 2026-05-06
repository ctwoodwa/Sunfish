using System;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// DI registration for the foundation-tier Ship's Office substrate (ADR 0083).
/// Per cohort <c>AddSunfishX()</c> convention (W#34 / W#35 / W#36 / W#39 /
/// W#40 / W#41 / W#42 / W#46 / W#49).
/// </summary>
public static class ShipsOfficeServiceCollectionExtensions
{
    /// <summary>
    /// Register the Ship's Office substrate. Phase 1 ships
    /// <see cref="ShipsOfficeOptions"/> binding only — concrete
    /// <see cref="IShipsOfficeDataProvider"/>, <see cref="IShipsOfficeCommandService"/>,
    /// and <see cref="IContentEditorSurface"/> implementations land in
    /// Phase 2 (<c>blocks-ships-office</c>).
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configure">Optional options-configuration callback.</param>
    public static IServiceCollection AddSunfishShipsOffice(
        this IServiceCollection services,
        Action<ShipsOfficeOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<ShipsOfficeOptions>().Configure(opts => configure?.Invoke(opts));

        // Phase 1 ships interface registrations only — implementations
        // land in Phase 2 (`blocks-ships-office`). Hosts MUST register
        // concrete IShipsOfficeDataProvider + IShipsOfficeCommandService +
        // IContentEditorSurface bindings via Phase 2 / their own DI
        // composition.

        return services;
    }
}

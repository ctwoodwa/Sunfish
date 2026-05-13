using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.ShipsOffice;

namespace Sunfish.Blocks.ShipsOffice;

/// <summary>
/// DI registration for the block-tier Ship's Office reference
/// implementations per W#55 Phase 2c. Per cohort
/// <c>AddSunfishXDefaults()</c> convention (W#48 / W#54 / W#50
/// precedent): <c>foundation-ships-office</c> registers contracts +
/// options binding; <c>blocks-ships-office</c> layers in
/// implementations.
/// </summary>
public static class ShipsOfficeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Phase 2b/2c implementations:
    /// <list type="bullet">
    /// <item><description><see cref="IShipsOfficeDataProvider"/> →
    /// <see cref="ShipsOfficeDataProvider"/> (real cross-package projection:
    /// <c>IBundleCatalog</c> / <c>ILeaseDocumentVersionLog</c> /
    /// <c>IW9DocumentService</c> / <c>IMissionEnvelopeProvider</c>).</description></item>
    /// <item><description><see cref="IShipsOfficeCommandService"/> →
    /// <see cref="ShipsOfficeCommandService"/> (§5 audit-emission ordering:
    /// permission FIRST → audit pre-op → execute; Phase 2 stub backing
    /// store).</description></item>
    /// <item><description><see cref="IContentEditorSurface"/> →
    /// <see cref="NoopContentEditorSurface"/> (read-only stub; Phase 5 conditional).</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Phase 2d adds <c>IDocumentDiffService</c> stub + the
    /// <c>SUNFISH_SHIPSOFFICE_PERM001</c> Roslyn analyzer. Both ship in a
    /// separate PR per the XO ruling.
    /// </remarks>
    public static IServiceCollection AddSunfishShipsOfficeDefaults(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<ShipsOfficeOptions>();
        services.TryAddSingleton<IShipsOfficeDataProvider, ShipsOfficeDataProvider>();
        services.TryAddSingleton<IShipsOfficeCommandService, ShipsOfficeCommandService>();
        services.TryAddSingleton<IContentEditorSurface, NoopContentEditorSurface>();
        return services;
    }
}

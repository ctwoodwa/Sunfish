using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.ShipsOffice;

namespace Sunfish.Blocks.ShipsOffice;

/// <summary>
/// DI registration for the block-tier Ship's Office reference
/// implementations per W#55 Phase 2a. Per cohort
/// <c>AddSunfishXDefaults()</c> convention (W#48 / W#54 / W#50
/// precedent): <c>foundation-ships-office</c> registers contracts +
/// options binding; <c>blocks-ships-office</c> layers in
/// implementations.
/// </summary>
public static class ShipsOfficeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the reference Phase 2a implementations:
    /// <list type="bullet">
    /// <item><description><see cref="IShipsOfficeDataProvider"/> →
    /// <see cref="ShipsOfficeDataProvider"/> (empty-snapshot stub;
    /// Phase 2b wires real cross-package projection).</description></item>
    /// <item><description><see cref="IContentEditorSurface"/> →
    /// <see cref="NoopContentEditorSurface"/> (read-only stub per
    /// Open Q2 deferral).</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Phase 2b will add <c>ShipsOfficeCommandService</c> +
    /// <c>IDocumentDiffService</c> registrations once the cross-
    /// package wiring lands. The <c>SUNFISH_SHIPSOFFICE_PERM001</c>
    /// Roslyn analyzer ships in a separate analyzer package per the
    /// W#48 cohort split.
    /// </remarks>
    public static IServiceCollection AddSunfishShipsOfficeDefaults(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<ShipsOfficeOptions>();
        services.TryAddSingleton<IShipsOfficeDataProvider, ShipsOfficeDataProvider>();
        services.TryAddSingleton<IContentEditorSurface, NoopContentEditorSurface>();
        return services;
    }
}

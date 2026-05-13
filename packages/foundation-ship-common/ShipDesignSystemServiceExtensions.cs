using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.UICore.Conformance;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// DI registration for the Sunfish Shared Design System per ADR 0077.
/// Combines Phase 1 (ship-common substrate) and Phase 3 (ui-core conformance)
/// registrations into a single cohort call. Adapters register Phase 4
/// primitives (ILiveAnnouncer, IFocusTrap) via their own package extension
/// (e.g., <c>AddSunfishA11y()</c> in Sunfish.UIAdapters.Blazor).
/// </summary>
public static class ShipDesignSystemServiceExtensions
{
    /// <summary>
    /// Registers the Sunfish Shared Design System substrate:
    /// <list type="bullet">
    ///   <item><term>Phase 1</term><description>
    ///     <see cref="IDeckRegistry"/> (<see cref="DefaultDeckRegistry"/>),
    ///     <see cref="IPermissionResolver"/> (<see cref="DefaultPermissionResolver"/>),
    ///     <see cref="IShipRoleRegistry"/> (<see cref="DefaultShipRoleRegistry"/>).
    ///   </description></item>
    ///   <item><term>Phase 3</term><description>
    ///     <see cref="IConformanceRegistry"/> (<see cref="DefaultConformanceRegistry"/>).
    ///   </description></item>
    /// </list>
    /// All registrations are idempotent via <c>TryAdd*</c> — calling this method
    /// more than once is safe.
    /// </summary>
    public static IServiceCollection AddSunfishSharedDesignSystem(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Phase 1 — ship-common substrate (ADR 0077 §1–§3)
        services.TryAddSingleton<IDeckRegistry, DefaultDeckRegistry>();
        services.TryAddSingleton<IPermissionResolver, DefaultPermissionResolver>();
        services.TryAddSingleton<IShipRoleRegistry, DefaultShipRoleRegistry>();

        // Phase 3 — ui-core conformance registry (ADR 0077 §7)
        services.TryAddSingleton<IConformanceRegistry, DefaultConformanceRegistry>();

        // Phase 4 adapters are wired separately via adapter-package extensions
        // (e.g., AddSunfishA11y() for ILiveAnnouncer + IFocusTrap in Blazor).

        return services;
    }
}

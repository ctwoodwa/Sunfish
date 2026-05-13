using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.MissionSpace;

namespace Sunfish.Anchor.Services;

/// <summary>
/// DI registration for the Anchor MAUI system-requirements renderer surface
/// per ADR 0063-A1.1 + W#47.
/// Per cohort <c>AddAnchorX()</c> / <c>AddSunfishX()</c> DI-extension convention
/// (W#47 / W#42 / W#41 / W#34 cohort chain).
/// </summary>
public static class AnchorMauiServiceCollectionExtensions
{
    /// <summary>
    /// Register the Anchor MAUI system-requirements renderer: singleton
    /// <see cref="SystemRequirementsRegressionObserver"/> (subscribes to
    /// <see cref="IMissionEnvelopeProvider"/> at construction and publishes
    /// <c>Required+Pass→Fail</c> regressions to a channel consumed by
    /// <c>SystemRequirementsRegressionBanner</c> in the shell layout),
    /// singleton <see cref="AnchorMauiSystemRequirementsRenderer"/> wired to
    /// the observer, and scoped <see cref="AnchorMauiSystemRequirementsSurface"/>
    /// bound to both <see cref="ISystemRequirementsSurface"/> and the
    /// Anchor-specific <see cref="IAnchorSystemRequirementsSurface"/> abstraction.
    /// </summary>
    /// <remarks>
    /// Call after <c>AddSunfish()</c> + <c>AddSunfishBootstrap()</c> and before
    /// <c>Build()</c>. Mirrors the registration-order pattern in
    /// <c>MauiProgram.cs</c> (Wave 6.3.F + Wave 6.7).
    /// </remarks>
    public static IServiceCollection AddAnchorSystemRequirementsRenderer(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<SystemRequirementsRegressionObserver>();

        services.TryAddSingleton<AnchorMauiSystemRequirementsRenderer>(sp =>
            new AnchorMauiSystemRequirementsRenderer(
                sp.GetRequiredService<SystemRequirementsRegressionObserver>()));

        services.TryAddSingleton<ISystemRequirementsRenderer>(sp =>
            sp.GetRequiredService<AnchorMauiSystemRequirementsRenderer>());

        // Scoped so each Blazor circuit gets a NavigationManager-bound surface instance.
        // Scope-safety: the scoped surface DOES NOT subscribe to the singleton observer's
        // channel; only SystemRequirementsRegressionBanner (a Razor component that manages
        // its own ReadAllAsync consumer + CancellationToken) reads the channel.
        // The singleton observer holds no back-reference to the scoped surface, so no
        // cross-scope live-region announcement leakage occurs (ARIA 1.2 §aria-live hazard).
        services.TryAddScoped<AnchorMauiSystemRequirementsSurface>();
        services.TryAddScoped<ISystemRequirementsSurface>(sp =>
            sp.GetRequiredService<AnchorMauiSystemRequirementsSurface>());

        return services;
    }
}

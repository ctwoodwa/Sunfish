using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace.DependencyInjection;

/// <summary>
/// DI registration for the foundation-tier mission-space substrate
/// (ADR 0062 + A1).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the audit-disabled mission-space substrate: the 10 default
    /// <see cref="IDimensionProbe{TDimension}"/> implementations + a
    /// <see cref="DefaultFeatureForceEnableSurface"/> in audit-disabled mode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hosts that need richer probes (e.g., wired to W#34 / W#35 / W#37
    /// sources) should override the registrations after this call —
    /// <see cref="ServiceCollectionDescriptorExtensions.Replace"/> on
    /// <c>IDimensionProbe&lt;X&gt;</c>. The bare-metal probes here keep
    /// the substrate compileable with no cross-package dependencies.
    /// </para>
    /// <para>
    /// Note: this overload does NOT register an
    /// <see cref="IMissionEnvelopeProvider"/>. The envelope factory is
    /// host-specific (it composes the registered probes); hosts wire
    /// their own <see cref="DefaultMissionEnvelopeProvider"/> with the
    /// factory delegate that fits their environment.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddInMemoryMissionSpace(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDimensionProbe<HardwareCapabilities>>(_ => new DefaultHardwareProbe());
        services.TryAddSingleton<IDimensionProbe<RuntimeCapabilities>>(_ => new DefaultRuntimeProbe());
        services.TryAddSingleton<IDimensionProbe<NetworkCapabilities>>(_ => new DefaultNetworkProbe());
        services.TryAddSingleton<IDimensionProbe<UserCapabilities>>(_ => new DefaultUserProbe());
        services.TryAddSingleton<IDimensionProbe<EditionCapabilities>>(_ => new DefaultEditionProbe());
        services.TryAddSingleton<IDimensionProbe<RegulatoryCapabilities>>(_ => new DefaultRegulatoryProbe());
        services.TryAddSingleton<IDimensionProbe<TrustAnchorCapabilities>>(_ => new DefaultTrustAnchorProbe());
        services.TryAddSingleton<IDimensionProbe<SyncStateSnapshot>>(_ => new DefaultSyncStateProbe());
        services.TryAddSingleton<IDimensionProbe<VersionVectorSnapshot>>(_ => new DefaultVersionVectorProbe());
        services.TryAddSingleton<IDimensionProbe<FormFactorSnapshot>>(_ => new DefaultFormFactorProbe());

        services.TryAddSingleton<IFeatureForceEnableSurface>(_ =>
            new DefaultFeatureForceEnableSurface());

        return services;
    }

    /// <summary>
    /// Registers the mission-space substrate with audit emission enabled
    /// (W#32 both-or-neither contract).
    /// <see cref="IFeatureForceEnableSurface"/> is wired with the supplied
    /// <paramref name="tenantId"/> + an <see cref="IAuditTrail"/> +
    /// <see cref="IOperationSigner"/> resolved from the container.
    /// </summary>
    /// <remarks>
    /// Both <see cref="IAuditTrail"/> and <see cref="IOperationSigner"/>
    /// MUST be registered in the container before this call. The both-or-
    /// neither contract is enforced at construction; missing dependencies
    /// surface as <see cref="InvalidOperationException"/> at the first
    /// service resolution.
    /// </remarks>
    public static IServiceCollection AddInMemoryMissionSpace(this IServiceCollection services, TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }

        services.TryAddSingleton<IDimensionProbe<HardwareCapabilities>>(_ => new DefaultHardwareProbe());
        services.TryAddSingleton<IDimensionProbe<RuntimeCapabilities>>(_ => new DefaultRuntimeProbe());
        services.TryAddSingleton<IDimensionProbe<NetworkCapabilities>>(_ => new DefaultNetworkProbe());
        services.TryAddSingleton<IDimensionProbe<UserCapabilities>>(_ => new DefaultUserProbe());
        services.TryAddSingleton<IDimensionProbe<EditionCapabilities>>(_ => new DefaultEditionProbe());
        services.TryAddSingleton<IDimensionProbe<RegulatoryCapabilities>>(_ => new DefaultRegulatoryProbe());
        services.TryAddSingleton<IDimensionProbe<TrustAnchorCapabilities>>(_ => new DefaultTrustAnchorProbe());
        services.TryAddSingleton<IDimensionProbe<SyncStateSnapshot>>(_ => new DefaultSyncStateProbe());
        services.TryAddSingleton<IDimensionProbe<VersionVectorSnapshot>>(_ => new DefaultVersionVectorProbe());
        services.TryAddSingleton<IDimensionProbe<FormFactorSnapshot>>(_ => new DefaultFormFactorProbe());

        services.TryAddSingleton<IFeatureForceEnableSurface>(sp =>
            new DefaultFeatureForceEnableSurface(
                sp.GetRequiredService<IAuditTrail>(),
                sp.GetRequiredService<IOperationSigner>(),
                tenantId));

        return services;
    }
}

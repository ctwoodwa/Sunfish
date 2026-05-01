using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport.Relay;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Transport.DependencyInjection;

/// <summary>
/// DI registration for the foundation-tier three-tier peer transport
/// substrate (ADR 0061).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITransportSelector"/> backed by
    /// <see cref="DefaultTransportSelector"/>. Resolves the selector by
    /// pulling every registered <see cref="IPeerTransport"/> out of the
    /// container — callers wire individual tier transports separately
    /// (e.g., <see cref="AddBridgeRelay"/> for Tier 3, host adapters
    /// for Tier 1 / Tier 2).
    /// </summary>
    /// <remarks>
    /// At least one <see cref="TransportTier.ManagedRelay"/> transport
    /// MUST be registered before the selector resolves; missing T3
    /// surfaces as <see cref="ArgumentException"/> at the first
    /// <see cref="System.IServiceProvider.GetService"/> call (per the
    /// <see cref="DefaultTransportSelector"/> constructor contract).
    /// </remarks>
    public static IServiceCollection AddSunfishTransport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ITransportSelector>(sp =>
            new DefaultTransportSelector(sp.GetServices<IPeerTransport>()));
        return services;
    }

    /// <summary>
    /// Audit-enabled overload — wires <see cref="DefaultTransportSelector"/>
    /// with <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/> from
    /// the container plus the supplied <paramref name="tenantId"/>. The W#32
    /// both-or-neither contract is enforced by <see cref="DefaultTransportSelector"/>'s
    /// constructor; missing dependencies fail lazily at first resolution.
    /// </summary>
    public static IServiceCollection AddSunfishTransport(this IServiceCollection services, TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }
        services.TryAddSingleton<ITransportSelector>(sp =>
            new DefaultTransportSelector(
                sp.GetServices<IPeerTransport>(),
                sp.GetRequiredService<IAuditTrail>(),
                sp.GetRequiredService<IOperationSigner>(),
                tenantId));
        return services;
    }

    /// <summary>
    /// Registers <see cref="BridgeRelayPeerTransport"/> as the Tier-3
    /// fallback. The first call wins; repeated calls are no-ops at the
    /// <see cref="ITransportSelector"/> resolution step (the selector
    /// picks the first registered <see cref="TransportTier.ManagedRelay"/>
    /// transport).
    /// </summary>
    public static IServiceCollection AddBridgeRelay(this IServiceCollection services, BridgeRelayOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddSingleton<IPeerTransport>(_ => new BridgeRelayPeerTransport(options));
        return services;
    }
}

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Versioning.DependencyInjection;

/// <summary>
/// DI registration for the foundation-tier version-vector federation
/// substrate (ADR 0028-A6 + A7).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory federation-handshake reference implementations:
    /// <see cref="ICompatibilityRelation"/> (6-rule engine),
    /// <see cref="IVersionVectorExchange"/> (two-phase verdict commit per A7.1),
    /// and <see cref="IVersionVectorIncompatibility"/> in audit-disabled mode
    /// (dedup state still tracked; no <see cref="AuditRecord"/> is emitted).
    /// </summary>
    /// <remarks>
    /// Audit-disabled mode is the right default for tests and bootstrap
    /// hosts. Production hosts should call the audit-enabled overload
    /// <see cref="AddInMemoryVersioning(IServiceCollection, TenantId)"/>
    /// once an <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/>
    /// are registered (per the W#32 both-or-neither pattern).
    /// </remarks>
    public static IServiceCollection AddInMemoryVersioning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ICompatibilityRelation>(_ => new DefaultCompatibilityRelation());
        services.TryAddSingleton<IVersionVectorExchange>(sp =>
            new InMemoryVersionVectorExchange(sp.GetRequiredService<ICompatibilityRelation>()));
        services.TryAddSingleton<IVersionVectorIncompatibility>(_ => new InMemoryVersionVectorIncompatibility());
        return services;
    }

    /// <summary>
    /// Registers the in-memory federation-handshake reference implementations
    /// with audit emission enabled. <see cref="IVersionVectorIncompatibility"/>
    /// is wired with the supplied <paramref name="tenantId"/> + an
    /// <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/> resolved
    /// from the container.
    /// </summary>
    /// <remarks>
    /// Both <see cref="IAuditTrail"/> and <see cref="IOperationSigner"/>
    /// MUST be registered in the container before this call (the both-or-
    /// neither contract is enforced at construction; missing dependencies
    /// surface as <see cref="InvalidOperationException"/> at the first
    /// service resolution). A7.4 dedup windows default to 1 hour
    /// (rejections) and 24 hours (legacy reconnects).
    /// </remarks>
    public static IServiceCollection AddInMemoryVersioning(this IServiceCollection services, TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }

        services.TryAddSingleton<ICompatibilityRelation>(_ => new DefaultCompatibilityRelation());
        services.TryAddSingleton<IVersionVectorExchange>(sp =>
            new InMemoryVersionVectorExchange(sp.GetRequiredService<ICompatibilityRelation>()));
        services.TryAddSingleton<IVersionVectorIncompatibility>(sp =>
            new InMemoryVersionVectorIncompatibility(
                sp.GetRequiredService<IAuditTrail>(),
                sp.GetRequiredService<IOperationSigner>(),
                tenantId));
        return services;
    }
}

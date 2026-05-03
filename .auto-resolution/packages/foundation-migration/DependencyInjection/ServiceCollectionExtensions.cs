using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Migration.DependencyInjection;

/// <summary>
/// DI registration for the foundation-tier cross-form-factor migration
/// substrate (ADR 0028-A5+A8).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory <see cref="IFormFactorMigrationService"/>
    /// + <see cref="ISequestrationStore"/> reference implementations.
    /// Both are singletons; the migration service reads/writes the
    /// store. Audit emission is disabled in this overload (test /
    /// bootstrap); production hosts call
    /// <see cref="AddInMemoryMigration(IServiceCollection, TenantId)"/>
    /// once <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/>
    /// are registered.
    /// </summary>
    public static IServiceCollection AddInMemoryMigration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ISequestrationStore, InMemorySequestrationStore>();
        services.TryAddSingleton<IFormFactorMigrationService>(sp =>
            new InMemoryFormFactorMigrationService(sp.GetRequiredService<ISequestrationStore>()));
        return services;
    }

    /// <summary>
    /// Audit-enabled overload — wires
    /// <see cref="InMemoryFormFactorMigrationService"/> with
    /// <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/>
    /// resolved from the container plus the supplied
    /// <paramref name="tenantId"/>. The W#32 both-or-neither contract is
    /// enforced at construction; missing dependencies fail lazily at
    /// first resolution. A8.7 dedup window for
    /// <see cref="AuditEventType.AdapterRollbackDetected"/> defaults to
    /// 6 hours.
    /// </summary>
    public static IServiceCollection AddInMemoryMigration(this IServiceCollection services, TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }

        services.TryAddSingleton<ISequestrationStore, InMemorySequestrationStore>();
        services.TryAddSingleton<IFormFactorMigrationService>(sp =>
            new InMemoryFormFactorMigrationService(
                sp.GetRequiredService<ISequestrationStore>(),
                sp.GetRequiredService<IAuditTrail>(),
                sp.GetRequiredService<IOperationSigner>(),
                tenantId));
        return services;
    }
}

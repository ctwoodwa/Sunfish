using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.Foundation.Recovery.TenantKey;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Recovery.DependencyInjection;

/// <summary>
/// DI registration for the foundation-tier recovery orchestration surface
/// (ADR 0046 sub-patterns #48a / #48e / #48f). Kernel-tier crypto primitives
/// (Ed25519, X25519, SqlCipher key derivation) live in
/// <c>Sunfish.Kernel.Security</c>; register those via
/// <c>AddSunfishKernelSecurity</c> first.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Phase 1 G6 multi-sig social recovery surface per
    /// ADR 0046: <see cref="IRecoveryCoordinator"/>,
    /// <see cref="IRecoveryClock"/> (defaulting to
    /// <see cref="SystemRecoveryClock"/>), and
    /// <see cref="IRecoveryStateStore"/> (defaulting to
    /// <see cref="InMemoryRecoveryStateStore"/> until a SQLCipher-backed
    /// store ships in Phase 1.x).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Callers MUST register an <see cref="IDisputerValidator"/>
    /// separately — typically <see cref="FixedDisputerValidator"/>
    /// constructed with the owner's NodeIdentity public key(s). Without
    /// it, the dispute path rejects all submissions.
    /// </para>
    /// <para>
    /// Production hosts should also override
    /// <see cref="IRecoveryStateStore"/> with a durable implementation —
    /// the in-memory default does not survive process restart, which
    /// breaks the 7-day grace-window survivability requirement from the
    /// Phase 1 plan.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddSunfishRecoveryCoordinator(
        this IServiceCollection services,
        Action<RecoveryCoordinatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new RecoveryCoordinatorOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        services.TryAddSingleton<IRecoveryClock, SystemRecoveryClock>();
        services.TryAddSingleton<IRecoveryStateStore, InMemoryRecoveryStateStore>();
        services.TryAddSingleton<IRecoveryCoordinator, RecoveryCoordinator>();

        // ADR 0046-A4/A5 — field-encryption substrate (W#32). The decryptor
        // requires BOTH IAuditTrail + IOperationSigner together (audit-enabled
        // overload) OR neither (audit-disabled overload). The factory throws
        // on first resolution if exactly one is registered (A5.7).
        services.TryAddSingleton<IFieldEncryptor, TenantKeyProviderFieldEncryptor>();
        services.TryAddSingleton<IFieldDecryptor>(sp =>
        {
            var tenantKeys = sp.GetRequiredService<ITenantKeyProvider>();
            var clock = sp.GetService<IRecoveryClock>();
            var auditTrail = sp.GetService<IAuditTrail>();
            var signer = sp.GetService<IOperationSigner>();
            return (auditTrail, signer) switch
            {
                (null, null) => new TenantKeyProviderFieldDecryptor(tenantKeys, clock),
                (not null, not null) => new TenantKeyProviderFieldDecryptor(tenantKeys, auditTrail, signer, clock),
                _ => throw new InvalidOperationException(
                    "Field-encryption decryptor requires both IAuditTrail and IOperationSigner registered, or neither. "
                    + $"Mid-state misconfiguration: IAuditTrail={(auditTrail is null ? "null" : "registered")}, "
                    + $"IOperationSigner={(signer is null ? "null" : "registered")}.")
            };
        });

        return services;
    }
}

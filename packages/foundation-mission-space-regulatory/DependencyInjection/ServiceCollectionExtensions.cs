using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MissionSpace.Regulatory.Audit;
using Sunfish.Foundation.MissionSpace.Regulatory.Bridge;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace.Regulatory.DependencyInjection;

/// <summary>
/// DI registration for the foundation-tier regulatory-policy substrate
/// (ADR 0064 + A1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Reader caution per A1.2:</b> Sunfish does not provide legal advice;
/// this substrate is not a substitute for qualified counsel. Phase 1
/// substrate-only deployments are NOT regulatory-compliant by virtue of
/// the substrate alone (per A1.8). Per-jurisdiction rule content (Phase 3)
/// is gated on legal sign-off.
/// </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the audit-disabled regulatory-policy substrate: composite
    /// jurisdiction probe, policy evaluator wired to an empty rule source,
    /// data-residency enforcer wired to an empty constraint source,
    /// sanctions screener wired to an empty list source, and the Bridge
    /// data-residency middleware.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hosts wiring counsel-reviewed rule sources (Phase 3) override the
    /// <c>IPolicyRuleSource</c> / <c>IDataResidencyConstraintSource</c> /
    /// <c>ISanctionsListSource</c> registrations after this call. The
    /// substrate registers <c>TryAddSingleton</c> so host overrides win.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddInMemoryRegulatoryPolicy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IPolicyRuleSource>(_ => new EmptyPolicyRuleSource());
        services.TryAddSingleton<IDataResidencyConstraintSource>(_ => new EmptyDataResidencyConstraintSource());
        services.TryAddSingleton<ISanctionsListSource>(_ => new EmptySanctionsListSource());

        services.TryAddSingleton<ICompositeJurisdictionProbe>(_ => new DefaultCompositeJurisdictionProbe());
        services.TryAddSingleton<IPolicyEvaluator>(sp =>
            new DefaultPolicyEvaluator(sp.GetRequiredService<IPolicyRuleSource>()));
        services.TryAddSingleton<IDataResidencyEnforcer>(sp =>
            new DefaultDataResidencyEnforcer(sp.GetRequiredService<IDataResidencyConstraintSource>()));
        services.TryAddSingleton<ISanctionsScreener>(sp =>
            new DefaultSanctionsScreener(sp.GetRequiredService<ISanctionsListSource>()));

        services.TryAddSingleton<IDataResidencyEnforcerMiddleware>(sp =>
            new DataResidencyEnforcerMiddleware(sp.GetRequiredService<IDataResidencyEnforcer>()));

        return services;
    }

    /// <summary>
    /// Registers the regulatory-policy substrate with audit emission enabled
    /// (W#32 both-or-neither contract).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="tenantId">Tenant identity to stamp on emitted audit records.</param>
    /// <param name="screeningPolicy">Sanctions screening policy. <see cref="ScreeningPolicy.AdvisoryOnly"/> emits a one-shot <c>SanctionsAdvisoryOnlyConfigured</c> audit per A1.3.</param>
    /// <param name="operatorPrincipalId">Operator who configured the screening policy (carried in the AdvisoryOnly configuration audit).</param>
    /// <remarks>
    /// Both <see cref="IAuditTrail"/> and <see cref="IOperationSigner"/>
    /// MUST be registered in the container before this call. The both-or-
    /// neither contract is enforced at construction; missing dependencies
    /// surface as <see cref="InvalidOperationException"/> at first
    /// service resolution.
    /// </remarks>
    public static IServiceCollection AddInMemoryRegulatoryPolicy(
        this IServiceCollection services,
        TenantId tenantId,
        ScreeningPolicy screeningPolicy = ScreeningPolicy.Default,
        string operatorPrincipalId = "system")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(operatorPrincipalId);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }

        services.TryAddSingleton<IPolicyRuleSource>(_ => new EmptyPolicyRuleSource());
        services.TryAddSingleton<IDataResidencyConstraintSource>(_ => new EmptyDataResidencyConstraintSource());
        services.TryAddSingleton<ISanctionsListSource>(_ => new EmptySanctionsListSource());

        services.TryAddSingleton(sp => new RegulatoryAuditEmitter(
            sp.GetRequiredService<IAuditTrail>(),
            sp.GetRequiredService<IOperationSigner>(),
            tenantId));

        services.TryAddSingleton<ICompositeJurisdictionProbe>(sp =>
            new DefaultCompositeJurisdictionProbe(sp.GetRequiredService<RegulatoryAuditEmitter>()));
        services.TryAddSingleton<IPolicyEvaluator>(sp =>
            new DefaultPolicyEvaluator(
                sp.GetRequiredService<IPolicyRuleSource>(),
                sp.GetRequiredService<RegulatoryAuditEmitter>()));
        services.TryAddSingleton<IDataResidencyEnforcer>(sp =>
            new DefaultDataResidencyEnforcer(
                sp.GetRequiredService<IDataResidencyConstraintSource>(),
                sp.GetRequiredService<RegulatoryAuditEmitter>()));
        services.TryAddSingleton<ISanctionsScreener>(sp =>
            new DefaultSanctionsScreener(
                sp.GetRequiredService<ISanctionsListSource>(),
                sp.GetRequiredService<RegulatoryAuditEmitter>(),
                screeningPolicy,
                operatorPrincipalId));

        services.TryAddSingleton<IDataResidencyEnforcerMiddleware>(sp =>
            new DataResidencyEnforcerMiddleware(sp.GetRequiredService<IDataResidencyEnforcer>()));

        return services;
    }
}

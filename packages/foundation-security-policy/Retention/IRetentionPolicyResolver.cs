using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SecurityPolicy.Models;

namespace Sunfish.Foundation.SecurityPolicy.Retention;

/// <summary>
/// Resolves the active per-tenant
/// <see cref="AuditRetentionPolicy"/> projection AND derives
/// per-record per-class
/// <see cref="RetentionVerdict"/> windows by composing the
/// per-class overrides + the §5.2 jurisdiction-preset floor.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Retention windows produced by this resolver are informed by HIPAA,
/// PCI-DSS, SOC 2, GDPR, and the EU AI Act presets. Deployers MUST
/// obtain qualified legal counsel before relying on these values for
/// compliance attestation.
/// <para>
/// <b>Per-class resolution order (per §5.2).</b>
/// </para>
/// <list type="number">
///   <item>Look up per-class override in <see cref="AuditRetentionPolicy.PerClassOverrides"/>; if absent, fall back to <see cref="AuditRetentionPolicy.DefaultMinimumRetentionWindow"/> + <see cref="AuditRetentionPolicy.DefaultMaximumRetentionWindow"/>.</item>
///   <item>Consult <see cref="JurisdictionFloorHelper.GetFloor"/>; if the preset mandates a floor for this class AND the floor exceeds the per-class minimum, the FLOOR wins + <see cref="RetentionVerdict.IsJurisdictionFloor"/> is set to <c>true</c>.</item>
///   <item>Add the resolved minimum to <c>recordCreatedAt</c> to produce <see cref="RetentionVerdict.MinimumHoldUntil"/>; same for maximum.</item>
/// </list>
/// </remarks>
public interface IRetentionPolicyResolver
{
    /// <summary>Get the active per-tenant retention policy projection.</summary>
    ValueTask<AuditRetentionPolicy> GetActiveAsync(
        TenantId tenant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve the per-class per-record verdict for the given
    /// <paramref name="tenant"/>, <paramref name="eventClass"/>, and
    /// <paramref name="recordCreatedAt"/>. The verdict carries the
    /// inclusive minimum-hold + maximum-hold timestamps the record
    /// MUST observe, plus a flag indicating whether the §5.2
    /// jurisdiction floor overrode the per-class window.
    /// </summary>
    ValueTask<RetentionVerdict> ResolveAsync(
        TenantId tenant,
        AuditEventClass eventClass,
        System.DateTimeOffset recordCreatedAt,
        CancellationToken cancellationToken = default);
}

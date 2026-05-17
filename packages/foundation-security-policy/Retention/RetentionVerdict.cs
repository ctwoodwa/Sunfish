using Sunfish.Foundation.SecurityPolicy.Models;

namespace Sunfish.Foundation.SecurityPolicy.Retention;

/// <summary>
/// Per-record per-class retention verdict produced by
/// <see cref="IRetentionPolicyResolver.ResolveAsync"/>. Tells the
/// caller the minimum + maximum hold-until timestamps the record
/// MUST observe + whether the jurisdiction floor (§5.2) overrode
/// the per-class window.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Retention thresholds in this verdict are informed by HIPAA,
/// PCI-DSS, SOC 2, GDPR, and the EU AI Act presets. Deployers MUST
/// obtain qualified legal counsel before relying on these values
/// for compliance attestation.
/// </remarks>
/// <param name="EventClass">The audit-event class this verdict applies to.</param>
/// <param name="MinimumHoldUntil">Records MUST NOT be purged before this wall-clock instant.</param>
/// <param name="MaximumHoldUntil">Records MAY be purged after this wall-clock instant. <c>DateTimeOffset.MaxValue</c> means "no maximum enforced".</param>
/// <param name="IsJurisdictionFloor"><c>true</c> when the §5.2 jurisdiction-preset floor raised the minimum above the per-class override that would otherwise apply.</param>
public sealed record RetentionVerdict(
    AuditEventClass EventClass,
    System.DateTimeOffset MinimumHoldUntil,
    System.DateTimeOffset MaximumHoldUntil,
    bool IsJurisdictionFloor);

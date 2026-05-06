using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Closed taxonomy of tactical-signal kinds per ADR 0081 §1. Each kind
/// describes a class of operational anomaly that registered
/// <see cref="ITacticalRule"/> implementations evaluate over. The
/// <see cref="Custom"/> sentinel exists for tenant-defined rule
/// extensions; all first-party rules SHOULD pick a more specific kind.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TacticalSignalKind
{
    /// <summary>Decryption-failure rate exceeds the rule's threshold.</summary>
    DecryptionFailureSpike,

    /// <summary>One or more peer connections lost beyond the rule's threshold.</summary>
    PeerConnectivityLoss,

    /// <summary>Merge-conflict rate exceeds the rule's threshold.</summary>
    MergeConflictRate,

    /// <summary>CRDT growth rate exceeds the rule's threshold.</summary>
    CrdtGrowthAnomaly,

    /// <summary>Authorization-failure rate exceeds the rule's threshold.</summary>
    AuthorizationFailureSpike,

    /// <summary>
    /// Bulk-access pattern detected (e.g., one principal reading many
    /// records in a short window).
    /// </summary>
    BulkAccessPattern,

    /// <summary>Service degradation detected (latency / error rate / availability).</summary>
    ServiceDegradation,

    /// <summary>Probe timeout exceeded the rule's threshold.</summary>
    ProbeTimeout,

    /// <summary>Standing Order policy violation detected.</summary>
    StandingOrderViolation,

    /// <summary>
    /// Tenant-defined extension. First-party rules SHOULD use a
    /// dedicated kind; <see cref="Custom"/> is the fallback for tenant-
    /// supplied rule packs.
    /// </summary>
    Custom,
}

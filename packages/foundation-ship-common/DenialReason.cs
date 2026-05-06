using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Reason discriminator for <see cref="PermissionDecision.Denied"/>. Each
/// value names a single failure cause from <see cref="DefaultPermissionResolver"/>'s
/// resolution algorithm (ADR 0077 §2.1). UI consumers branch on this value to
/// select the localized denial message and the appropriate
/// <see cref="Remediation"/> affordance.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DenialReason
{
    /// <summary>Subject holds no role granting this action.</summary>
    NoMatchingRole,

    /// <summary>Role grants the action elsewhere but not at this deck depth.</summary>
    DeckRestriction,

    /// <summary>Role grants the action but not at this location.</summary>
    LocationOutOfScope,

    /// <summary>Action requires currently-on-watch designation (OOD/EOOW) which subject does not hold.</summary>
    WatchRequired,

    /// <summary>SUPPO / Supply Office — Phase 2 deferred (no current timeline).</summary>
    Phase2Deferred,

    /// <summary>Wardroom / Brig — v2 deferred (requires v2 commercial agreement).</summary>
    V2Deferred,

    /// <summary>Security policy intervened (~ADR 0068).</summary>
    SecurityPolicyBlocked,

    /// <summary>ADR 0062 IFeatureGate said the feature is unavailable in this Mission Envelope.</summary>
    MissionEnvelopeUnavailable,
}

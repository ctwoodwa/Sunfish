using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Discriminator for the <see cref="Remediation"/> kind embedded in a
/// <see cref="PermissionDecision.Denied"/> result. UI consumers branch on this
/// to render the appropriate affordance (a "Contact authority" button, a
/// disabled "Awaiting watch" pill, etc.).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RemediationKind
{
    /// <summary><see cref="Remediation.ContactActor"/> is set; UI surfaces a contact action.</summary>
    ContactAuthority,

    /// <summary>Wait for OOD/EOOW rotation; UI surfaces a passive "Awaiting watch" pill.</summary>
    AwaitWatch,

    /// <summary>Device/runtime/edition gate (per ADR 0062); UI surfaces an upgrade prompt.</summary>
    UpgradeMissionEnvelope,

    /// <summary>SUPPO / Supply Office — Phase 2 deferred; no current path.</summary>
    Phase2Deferred,

    /// <summary>Security policy territory (~ADR 0068); UI surfaces an appeal affordance.</summary>
    SecurityPolicyAppeal,

    /// <summary>Pure denial; no remediation path.</summary>
    None,
}

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Lifecycle status of a <see cref="Deficiency"/>.
/// </summary>
/// <remarks>
/// Transitions to <see cref="Resolved"/> via work-order completion are deferred to
/// blocks-maintenance (G16 second pass). In this pass deficiencies are passive records.
/// </remarks>
public enum DeficiencyStatus
{
    /// <summary>Deficiency has been recorded but no action has been taken.</summary>
    Open,

    /// <summary>Deficiency has been reviewed and acknowledged by responsible staff.</summary>
    Acknowledged,

    /// <summary>Deficiency has been remediated and closed.</summary>
    Resolved,

    /// <summary>Remediation has been intentionally deferred to a later date.</summary>
    Deferred,
}

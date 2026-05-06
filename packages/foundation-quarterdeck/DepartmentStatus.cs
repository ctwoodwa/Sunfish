using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Access-decision projection used by <see cref="DepartmentLink"/> +
/// <see cref="DepartmentKpi"/> per ADR 0080 §2.3 rule 4. Encodes the
/// permission-resolver outcome at snapshot time without exposing
/// underlying authority semantics; the Quarterdeck UI renders the
/// status alongside the location even when access is
/// <see cref="Denied"/> (denied-not-hidden invariant).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DepartmentStatus
{
    /// <summary>
    /// Permission resolver returned a granted decision; the link is
    /// navigable.
    /// </summary>
    Accessible,

    /// <summary>
    /// Permission resolver returned a denied decision. The link is
    /// rendered (denied-not-hidden) with a denial reason; activation
    /// surfaces the denial via First-Aid rather than navigating.
    /// </summary>
    Denied,

    /// <summary>
    /// Permission resolver could not return a decision (mission-envelope
    /// gate disabled, transient resolver error, source unavailable).
    /// The link renders with a neutral "unknown" affordance and does not
    /// navigate; the status is distinct from <see cref="Denied"/>
    /// because it does not represent an authority refusal.
    /// </summary>
    Unknown,
}

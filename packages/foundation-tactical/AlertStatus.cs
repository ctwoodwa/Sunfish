using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Lifecycle state for a <see cref="TacticalAlert"/> per ADR 0081 §1.
/// Allowed transitions: <see cref="Active"/> → <see cref="Acknowledged"/>;
/// <see cref="Active"/> → <see cref="Expired"/>; <see cref="Active"/> →
/// <see cref="Superseded"/>. Once in <see cref="Expired"/> or
/// <see cref="Superseded"/>, <see cref="TacticalAlert.AcknowledgedBy"/>
/// and <see cref="TacticalAlert.AcknowledgedAt"/> are retained if
/// previously set.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertStatus
{
    /// <summary>Open alert; surfaced on the Lookout (high-priority) or queryable on the Sonar (informational).</summary>
    Active,

    /// <summary>Acknowledged by an operator; the acknowledgement is captured in the audit trail.</summary>
    Acknowledged,

    /// <summary>Expired past <see cref="TacticalOptions.AlertTtl"/> without being acknowledged.</summary>
    Expired,

    /// <summary>Superseded by a newer alert from the same rule (deduplication).</summary>
    Superseded,
}

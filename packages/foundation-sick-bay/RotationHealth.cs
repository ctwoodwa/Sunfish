using System.Text.Json.Serialization;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Rotation-status discriminator for a pharmacy field per ADR 0082 §1.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RotationHealth
{
    /// <summary>Last rotation is within the configured rotation window.</summary>
    Current,

    /// <summary>Rotation window is approaching its scheduled end.</summary>
    RotationDue,

    /// <summary>Rotation window has elapsed; rotation is overdue.</summary>
    RotationOverdue,

    /// <summary>The field has been flagged as compromised; immediate rotation required.</summary>
    Compromised,
}

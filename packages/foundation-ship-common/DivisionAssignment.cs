using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Sub-room assignment for <see cref="ShipRole.DivisionOfficer"/> per ADR 0077
/// §1 + W#35 §6.3. Division Officers cycle through these values over time per
/// the rotation pattern; <see cref="ShipRoleAssignment.RotatesAt"/> tracks the
/// next rotation tick.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DivisionAssignment
{
    /// <summary>Main Propulsion Assistant.</summary>
    MPA,

    /// <summary>Damage Control Assistant.</summary>
    DCA,

    /// <summary>Communications.</summary>
    Comms,

    /// <summary>Sonar.</summary>
    Sonar,

    /// <summary>Electrical.</summary>
    Electrical,

    /// <summary>Quality Assurance.</summary>
    QA,
}

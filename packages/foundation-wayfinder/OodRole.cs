using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// The two OOD roles per ADR 0078 §1. Watches are scoped to a single
/// <c>(TenantId, OodRole)</c> pair — at most one Active watch per pair.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OodRole
{
    /// <summary>Top-of-watch operational decision authority for the tenant.</summary>
    OfficerOfTheDeck,

    /// <summary>Engineering-tier authority — Damage Control / Engine Room operations.</summary>
    EngineeringOfficerOfTheWatch,
}

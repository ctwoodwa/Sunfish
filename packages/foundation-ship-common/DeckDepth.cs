using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Progressive-disclosure depth per ADR 0077 §3. Surfaces declare their
/// <see cref="DeckDepth"/> at registration time via
/// <see cref="DeckRegistration"/>. <see cref="DefaultPermissionResolver"/>
/// canonicalizes the caller-supplied deck against
/// <c>ActionMinimumDeck[action]</c> per §2.1 step 0(a) — callers passing
/// <see cref="MainDeck"/> for a <see cref="ShipAction.Quarantine"/> request
/// are silently promoted to <see cref="BelowTheWaterline"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeckDepth
{
    /// <summary>Executive summary; status; KPIs.</summary>
    TopDeck,

    /// <summary>Operational read/write.</summary>
    MainDeck,

    /// <summary>Internals; logs; raw events.</summary>
    EngineeringDeck,

    /// <summary>Destructive / irreversible operations.</summary>
    BelowTheWaterline,
}

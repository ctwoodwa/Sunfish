using System.Text.Json.Serialization;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Constrained role discriminator for medevac stretcher-bearer routing
/// per ADR 0082 §3 — INTENTIONALLY NOT a subset of
/// <see cref="Sunfish.Foundation.Ship.Common.ShipRole"/>. The narrower
/// enum prevents accidental role-escalation: a notification-routing
/// list MUST NOT be consumable as an authority list.
/// </summary>
/// <remarks>
/// <see cref="IStretcherBearerPolicy.GetEligibleRespondersAsync"/>
/// returns these for notification routing only; permission decisions
/// continue to flow through <c>IPermissionResolver</c> with
/// <c>ShipRole</c> values.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StretcherBearerRole
{
    /// <summary>Damage Control Assistant — routine eligible.</summary>
    DCA,

    /// <summary>Main Propulsion Assistant — routine eligible.</summary>
    MPA,

    /// <summary>Communications Officer — routine eligible.</summary>
    CommsOfficer,

    /// <summary>Sonar Officer — routine eligible.</summary>
    SonarOfficer,
}

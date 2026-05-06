using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// A single department / location link surfaced on the Quarterdeck
/// per ADR 0080 §2.3 rule 4. The data provider pre-resolves the
/// permission outcome at snapshot time and stamps it on the link;
/// the UI uses the stamped decision to render the link as accessible
/// or denied <i>without re-resolving</i>.
/// </summary>
/// <remarks>
/// <b>Denied-not-hidden:</b> a denied access decision MUST render the
/// link with <see cref="DepartmentStatus.Denied"/> + a denial reason —
/// the UI never silently omits the location. This preserves the
/// learnability of the ship's surface (operators can see where they
/// cannot go) without leaking authority-sensitive contents.
/// </remarks>
/// <param name="Location">
/// Canonical ship location the link points to. The Quarterdeck itself
/// is not an entry in this list; the entry is the <i>destination</i>.
/// </param>
/// <param name="DisplayName">
/// Localized label rendered alongside the link. The data provider does
/// not localize here; it accepts whatever the registered department
/// surface registered.
/// </param>
/// <param name="AccessDecision">
/// Pre-resolved access decision for the actor at snapshot time.
/// </param>
/// <param name="DenialReason">
/// One-line denial reason when <paramref name="AccessDecision"/> is
/// <see cref="DepartmentStatus.Denied"/> — surfaced to the operator via
/// First-Aid on activation. Null when the access decision is
/// <see cref="DepartmentStatus.Accessible"/> or
/// <see cref="DepartmentStatus.Unknown"/>.
/// </param>
public sealed record DepartmentLink(
    ShipLocation Location,
    string DisplayName,
    DepartmentStatus AccessDecision,
    string? DenialReason);

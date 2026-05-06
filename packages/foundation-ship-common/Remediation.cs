using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Suggested next action embedded in a <see cref="PermissionDecision.Denied"/>
/// per ADR 0077 §2 + §2.3. Drives the First-Aid denial UX: UI consumers
/// branch on <see cref="Kind"/>, render <see cref="GuidanceDisplay"/> as
/// adjacent prose, and surface <see cref="EscalationLink"/>/<see cref="ContactActor"/>
/// as actionable affordances when present.
/// </summary>
/// <param name="Kind">Discriminator for the remediation affordance.</param>
/// <param name="GuidanceDisplay">
/// Human-readable suggested-next-action; localized at adapter boundary.
/// MUST be non-null + non-empty per §2.3 denial-accessibility contract.
/// </param>
/// <param name="ContactActor">
/// Actor who can grant access (e.g., the current <see cref="ShipRole.Captain"/>);
/// null when no specific actor is the gate.
/// </param>
/// <param name="EscalationLink">
/// Accessible escalation action (e.g., a "request access" Standing Order draft URI);
/// null when no escalation path exists.
/// </param>
/// <param name="CallToActionLabel">
/// Localized label for the escalation affordance (e.g., <c>"Request access"</c>);
/// MUST be null when both <paramref name="EscalationLink"/> and
/// <paramref name="ContactActor"/> are null (no affordance to label).
/// </param>
public sealed record Remediation(
    RemediationKind Kind,
    string GuidanceDisplay,
    ActorId? ContactActor,
    Uri? EscalationLink,
    string? CallToActionLabel);

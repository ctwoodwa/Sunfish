using System;
using System.Collections.Generic;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// A correlated incident record per ADR 0081 §1. Operators open
/// incidents from one or more linked alerts; the incident is the
/// durable record of the response. <see cref="LinkedAlertIds"/>
/// tracks every alert correlated into the incident.
/// </summary>
/// <param name="IncidentId">Stable identifier for the incident.</param>
/// <param name="TenantId">Tenant the incident belongs to.</param>
/// <param name="Title">Operator-supplied title.</param>
/// <param name="RootAlertId">The <see cref="TacticalAlert.AlertId"/> that triggered incident creation.</param>
/// <param name="Status">Current lifecycle state.</param>
/// <param name="OpenedAt">Wall-clock open timestamp.</param>
/// <param name="LastUpdatedAt">Wall-clock timestamp of the last incident-state change (status, link, runbook step).</param>
/// <param name="ClosedAt">Wall-clock close timestamp; null until <see cref="Status"/> = <see cref="IncidentStatus.Resolved"/>.</param>
/// <param name="OpenedBy">Actor who opened the incident.</param>
/// <param name="ClosedBy">Actor who closed the incident; null until closed.</param>
/// <param name="ResolutionNote">Operator-supplied resolution note; null until closed.</param>
/// <param name="RunbookStepIds">Ordered runbook-step identifiers attached at open time + amendments.</param>
/// <param name="LinkedAlertIds">All alerts correlated into this incident (root + subsequent links).</param>
/// <remarks>
/// <b>Closed-state invariant:</b> when <see cref="Status"/> equals
/// <see cref="IncidentStatus.Resolved"/>, <see cref="ClosedAt"/>,
/// <see cref="ClosedBy"/>, and <see cref="ResolutionNote"/> MUST all
/// be non-null. Phase 2 implementations enforce this on the
/// <see cref="ITacticalCommandService.CloseIncidentAsync"/> path.
/// Per cohort precedent (W#46 / W#49 / W#50 / W#54 / W#55),
/// <see cref="DateTimeOffset"/> stands in for the hand-off's
/// <c>NodaTime.Instant</c> — NodaTime is not on Directory.Packages.props.
/// </remarks>
public sealed record IncidentRecord(
    string IncidentId,
    TenantId TenantId,
    string Title,
    string RootAlertId,
    IncidentStatus Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset LastUpdatedAt,
    DateTimeOffset? ClosedAt,
    ActorId OpenedBy,
    ActorId? ClosedBy,
    string? ResolutionNote,
    IReadOnlyList<string> RunbookStepIds,
    IReadOnlyList<string> LinkedAlertIds);

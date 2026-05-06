using System;
using System.Text.Json.Nodes;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// A single tactical signal submitted to the rule engine per
/// ADR 0081 §1. Signals are evaluated by every registered
/// <see cref="ITacticalRule"/> in registration order; rules that
/// match emit a <see cref="TacticalAlert"/>.
/// </summary>
/// <param name="TenantId">Tenant the signal originated in.</param>
/// <param name="Kind">Signal kind; rules typically gate on this first.</param>
/// <param name="OccurredAt">
/// Wall-clock timestamp of the underlying observation. Per cohort
/// precedent (W#46 / W#49 / W#50 / W#54 / W#55), <see cref="DateTimeOffset"/>
/// stands in for the hand-off's <c>NodaTime.Instant</c> — NodaTime is
/// not on Directory.Packages.props.
/// </param>
/// <param name="Payload">
/// Freeform JSON payload for the rule to inspect. MUST NOT be null;
/// emit an empty <see cref="JsonObject"/> for kinds with no payload.
/// </param>
public sealed record TacticalSignal(
    TenantId TenantId,
    TacticalSignalKind Kind,
    DateTimeOffset OccurredAt,
    JsonNode Payload);

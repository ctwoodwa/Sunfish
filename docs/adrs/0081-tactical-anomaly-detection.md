---
id: 81
title: Tactical Anomaly Detection + Threat-Trigger Surface
status: Accepted
date: 2026-05-05
tier: foundation
pipeline_variant: sunfish-feature-change

concern:
  - security
  - observability
  - audit
  - accessibility

enables:
  - tactical-anomaly-detection
  - alert-routing
  - incident-response
  - threat-trigger-standing-orders

composes:
  - 43
  - 49
  - 65
  - 77
  - 78
  - 80
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments: []
---

# ADR 0081 — Tactical Anomaly Detection + Threat-Trigger Surface

**Status:** Accepted
**Date:** 2026-05-05
**Authors:** XO research session
**Pipeline variant:** `sunfish-feature-change`
**Council posture:** standard adversarial + WCAG/a11y subagent (mandatory — alert UX under
stress, assertive live regions, non-color severity, flashing-indicator SC 2.3.1 concerns per
W#35 §5.4 Stage 1.5 hardening) + security-engineering subagent (mandatory — threat-trigger
Standing Order authority chain and rule-engine registration are elevated-authority surfaces)

---

## Context

The W#35 Ship Architecture discovery (§5.4) tags Tactical as a **Gap** — no current artifact
specifies the anomaly-detection rule engine, alert routing taxonomy, incident-response UI, or
threat-trigger Standing Order shapes. Adjacent ADRs cover narrow slices: ADR 0043 (OSS supply-
chain threat model, not tenant runtime threats); ADR 0049 (audit trail substrate, not trigger
logic); W#28 inquiry-defense and W#22 FCRA dispute (per-domain, not cross-cutting).

Tactical is the monitoring + threat-awareness department. Its three sub-rooms per W#35 §5.4:
- **Sonar Room** — anomaly detection: signal ingestion, rule evaluation, alert production
- **Lookout** — high-priority alert ticker: surfaces alerts to Quarterdeck (ADR 0080 ticker)
- **Fire Control** — incident response: runbooks, escalation paths, audit-trail query helpers

The department head is the **TAC** (Tactical Officer), a `ShipRole.TacticalOfficer` role
introduced in ADR 0077 §3's role taxonomy. Division Officers with Sonar specialty have
read-only Sonar Room access; only TAC + Captain/XO may operate Fire Control commands.

The Quarterdeck alert ticker (ADR 0080 §2 `IQuarterdeckAlertSource`) is the upstream
consumer of Tactical's Lookout. `blocks-tactical` provides a `IQuarterdeckAlertSource`
implementation that feeds the Lookout's active high-priority alerts to the Quarterdeck.

---

## Decision drivers

1. **Cross-cutting anomaly surface.** W#22 Leasing Pipeline (FCRA dispute alerts), W#28
   Public Listings (inquiry-defense alerts), and Engine Room health degradation (ADR 0079)
   all need a common alert taxonomy and routing primitive. Tactical is that primitive.
2. **Alert routing determinism.** Whether an alert is visible on the Quarterdeck ticker
   (high-priority) vs. silently written to the audit log (informational) MUST be determined
   by the rule, not by ad-hoc caller logic. The `AlertRoutingPolicy` enum encodes this.
3. **Threat-trigger Standing Orders.** Anomaly rules SHOULD be able to auto-issue Standing
   Orders (ADR 0065) when triggered — rate-limit increase, quarantine flag, OOD notification.
   The Standing Order issuance MUST be auditable and authority-checked via a system-scoped
   principal; the `issuer` is resolved internally and is NOT a parameter passthrough.
4. **Rule engine extensibility.** Operators and third-party modules extend Tactical's detection
   surface. The rule engine MUST be registration-based (typed-DSL interface) and MUST NOT
   require OPA/Rego as a hard dependency, though an OPA adapter may implement the interface.
5. **WCAG 2.2 AA + SC 2.3.1 compliance.** Alert UX under stress is W#35's most a11y-
   sensitive surface. Assertive live regions MUST announce only new-item additions (not status
   changes); severity MUST be non-color-encoded with distinct icon shapes; flashing/pulsing
   severity indicators MUST stay below SC 2.3.1 flash threshold (< 3 Hz) by default —
   `prefers-reduced-motion` is not the primary gate; suppression is the default.
6. **Security-engineering subagent posture.** The threat-trigger Standing Order chain
   (anomaly → auto-issue order) touches ADR 0065's distribution + ADR 0049 audit. The
   authority chain from signal ingest to Standing Order issuance MUST be fully specified.
7. **Lookout → Quarterdeck feed.** The Lookout MUST implement `IQuarterdeckAlertSource`
   (ADR 0080 §2) so it can be registered as a source in the Quarterdeck data provider without
   additional adapter code.

---

## Considered options

### Option A — Pure UI block with embedded rule logic

Implement Tactical entirely as `blocks-tactical`, with rule evaluation and alert routing
embedded in the block. No new foundation package.

**Pro:** minimal new surface.
**Con:** the alert taxonomy, routing policy, and threat-trigger shapes are consumed by
  ADR 0080 Quarterdeck, ADR 0079 Engine Room health bridge, and future domain blocks
  (W#22, W#28). Embedding them in a UI block makes those consumers depend on the UI tier.
  Rule logic is untestable without rendering. Threat-trigger Standing Order issuance mixed
  with presentation code creates an authorization audit gap.
**Verdict:** rejected.

### Option B — Thin `foundation-tactical` contracts + `blocks-tactical` UI **[RECOMMENDED]**

`foundation-tactical` contains the data model (`TacticalAlert`, `TacticalSignal`, etc.),
rule engine + routing interfaces (`ITacticalRule`, `ITacticalRuleEngine`, `IAlertRouter`,
`ILookout`), provider/command interfaces (`ITacticalDataProvider`, `ITacticalCommandService`),
threat-trigger service (`IThreatTriggerService`), `TacticalOptions` config type, and new
`AuditEventType` constants. `blocks-tactical` is the UI composition layer.

**Pro:** contracts are independently testable; Quarterdeck's `IQuarterdeckAlertSource`
  implementation lives in `blocks-tactical` but depends only on `foundation-tactical`
  interfaces; Bridge and the iOS field app can subscribe to Lookout without taking a UI dep;
  threat-trigger authority chain is isolated in foundation where security analysis is cleanest.
**Con:** adds a new foundation package; ~4h of scaffolding before UI work.
**Verdict:** recommended.

### Option C — Extend `foundation-ship-common` with Tactical contracts

Add `ITacticalRuleEngine` and friends to `foundation-ship-common` (ADR 0077).

**Pro:** fewer packages.
**Con:** `foundation-ship-common` owns role topology and permission resolution.
  Mixing in a signal-processing rule engine and alert routing violates single-responsibility.
  Downstream packages that only need `ShipRole` would transitively pull in the Tactical signal
  pipeline.
**Verdict:** rejected.

---

## Decision

**Adopt Option B.** Introduce `Sunfish.Foundation.Tactical` (new package:
`packages/foundation-tactical/`) for contracts, and `Sunfish.Blocks.Tactical` (new package:
`packages/blocks-tactical/`) for the UI block.

### §1 Observable data model

All types in `Sunfish.Foundation.Tactical`:

```csharp
// ── Signal taxonomy ─────────────────────────────────────────────────────────

public sealed record TacticalSignal(
    TenantId             TenantId,
    TacticalSignalKind   Kind,
    NodaTime.Instant     OccurredAt,
    /// Freeform payload matching the schema per §1.1.
    /// Implementations MUST NOT throw on unexpected keys (forward-compatibility).
    /// MUST NOT be null; use empty JsonObject for signal kinds with no payload.
    System.Text.Json.Nodes.JsonNode Payload
);

/// Canonical signal kinds emitted by Sunfish subsystems.
/// Custom = tenant-defined kind; Payload schema is tenant-owned (§1.1).
public enum TacticalSignalKind
{
    // CRDT / Engine Room signals (source: ICrdtEngine / IEngineRoomDataProvider)
    DecryptionFailureSpike,    // repeated failed-decryption events across ≥N documents
    PeerConnectivityLoss,      // active peer count falls below configured floor
    MergeConflictRate,         // merge-conflict events exceed rate threshold
    CrdtGrowthAnomaly,         // document byte estimate exceeds growth-rate threshold

    // Audit trail signals (source: IAuditTrail subscriber)
    AuthorizationFailureSpike, // authorization-denied events exceed rate threshold
    BulkAccessPattern,         // bulk read/write of sensitive documents by one actor

    // Mission Space signals (source: IMissionEnvelopeProvider)
    ServiceDegradation,        // MissionEnvelopeStatus transitions to Degraded
    ProbeTimeout,              // probe fails to respond within configured timeout

    // Standing Order signals (source: IStandingOrderRepository subscriber)
    StandingOrderViolation,    // Standing Order condition detected violated

    // Custom tenant-defined signal
    Custom
}

// ── Alert types ──────────────────────────────────────────────────────────────

public sealed record TacticalAlert(
    /// Format: "{RuleName}:{source-local-id}".
    /// RuleName may contain dots; AlertId regex: ^[A-Za-z0-9_\-\.:]{1,128}$.
    /// Validated at IAlertRouter.RouteAsync; invalid IDs are rejected without routing.
    string               AlertId,
    TenantId             TenantId,
    string               RuleName,            // max 128 chars; matches RuleName regex
    AlertSeverity        Severity,
    AlertRoutingPolicy   RoutingPolicy,
    string               Title,               // max 80 chars
    string               Summary,             // max 200 chars
    NodaTime.Instant     DetectedAt,
    AlertStatus          Status,
    bool                 RequiresAcknowledgement,
    /// Ordered Fire Control runbook step IDs, each matching ^[a-z][a-z0-9\-]{0,63}$.
    IReadOnlyList<string> RunbookStepIds,
    /// Actor who acknowledged; null if Status != Acknowledged.
    ActorId?             AcknowledgedBy,
    NodaTime.Instant?    AcknowledgedAt
);

public enum AlertSeverity        { Critical, High, Medium, Low, Informational }
public enum AlertRoutingPolicy   { HighPriorityLookout, InformationalSonar }

/// Active   = alert produced and routed; Acknowledged = operator confirmed awareness;
/// Expired  = TTL elapsed (DetectedAt + TacticalOptions.AlertTtl);
/// Superseded = a newer alert for the same RuleName replaced this one (implementation
///              transitions older Active alert to Superseded when a new alert for the same
///              RuleName fires with equal or higher severity; emits TacticalAlertExpired).
/// Transition diagram: Active → Acknowledged; Active → Expired; Active → Superseded.
/// Once Expired or Superseded, AcknowledgedBy/AcknowledgedAt are retained if previously set.
public enum AlertStatus          { Active, Acknowledged, Expired, Superseded }

// ── Incident types ───────────────────────────────────────────────────────────

public sealed record IncidentRecord(
    string               IncidentId,
    TenantId             TenantId,
    string               Title,              // max 256 chars
    string               RootAlertId,
    IncidentStatus       Status,
    NodaTime.Instant     OpenedAt,
    NodaTime.Instant     LastUpdatedAt,
    NodaTime.Instant?    ClosedAt,
    ActorId              OpenedBy,
    ActorId?             ClosedBy,
    string?              ResolutionNote,     // max 4,096 chars; required (non-null, non-whitespace) when closing
    IReadOnlyList<string> RunbookStepIds,
    IReadOnlyList<string> LinkedAlertIds     // populated via future LinkAlertToIncidentAsync; initially [RootAlertId]
);

/// Open = newly created. Investigating = acknowledged work-in-progress.
/// Resolved = closed with resolution note via CloseIncidentAsync.
/// Transition: Open → Investigating (no API in v1; reserved for future amendment) → Resolved.
/// Or: Open → Resolved directly (CloseIncidentAsync is available from Open state).
public enum IncidentStatus { Open, Investigating, Resolved }

// ── Tactical snapshot (block-level aggregate) ─────────────────────────────────

public sealed record TacticalSnapshot(
    NodaTime.Instant                 CapturedAt,
    TenantId                         TenantId,
    /// All alerts regardless of routing policy, Status=Active or Acknowledged, DetectedAt DESC,
    /// capped at TacticalOptions.MaxActiveAlerts. For non-ViewFireControl actors, limited to
    /// InformationalSonar-policy alerts (HighPriorityLookout omitted) per §6 scoping.
    IReadOnlyList<TacticalAlert>     ActiveAlerts,
    /// Only HighPriorityLookout-policy alerts with Status=Active only (Acknowledged excluded
    /// from Lookout ticker to avoid re-showing acknowledged items). DetectedAt DESC.
    IReadOnlyList<TacticalAlert>     LookoutAlerts,
    IReadOnlyList<IncidentRecord>    ActiveIncidents,  // Open + Investigating only
    bool                             CanAccessFireControl,
    bool                             CanAcknowledgeAlerts,
    int                              RegisteredRuleCount,  // -1 if rule engine unreachable
    /// Signal throughput over the trailing 60-second window.
    /// Updated on heartbeat only; not updated on every signal (avoids per-signal re-render).
    /// -1 if signal pipeline is unhealthy.
    int                              SignalRatePerMinute,
    /// True if any sub-system (Lookout, incident store, rule engine) was unavailable during
    /// snapshot construction. UI MUST indicate degraded state when true.
    bool                             IsPartialSnapshot,
    /// Non-null if IsPartialSnapshot; lists which sub-systems contributed partial or empty data.
    IReadOnlyList<string>?           DegradedSubsystems
);

// ── Configuration ────────────────────────────────────────────────────────────

/// Bounds are normative. Implementations MUST throw InvalidOperationException on
/// violation during DI registration or TacticalOptions.Validate().
public sealed record TacticalOptions(
    TimeSpan HeartbeatInterval,       // ≥1s, ≤5min. Default: 30s
    int      MaxActiveAlerts,         // ≥10, ≤10000. Default: 200
    TimeSpan AlertTtl,                // ≥1min. Default: 24h. Measured from DetectedAt.
    int      SignalBatchSize,         // ≥1, ≤10000. Default: 100
    int      MaxActiveIncidents,      // ≥1, ≤1000. Default: 50
    /// Max emergency Standing Orders that may be issued per (TenantId) per minute.
    int      MaxEmergencyOrdersPerMinute,  // ≥1, ≤20. Default: 3
    /// Max alerts RouteAsync will accept per (TenantId, RuleName) per minute.
    int      MaxAlertsPerMinutePerRule     // ≥1, ≤120. Default: 60
) {
    public static TacticalOptions Default => new(
        HeartbeatInterval:          TimeSpan.FromSeconds(30),
        MaxActiveAlerts:            200,
        AlertTtl:                   TimeSpan.FromHours(24),
        SignalBatchSize:            100,
        MaxActiveIncidents:         50,
        MaxEmergencyOrdersPerMinute: 3,
        MaxAlertsPerMinutePerRule:   60
    );
}

// ── Threat-trigger Standing Order template ────────────────────────────────────

public sealed record ThreatTriggerTemplate(
    /// Must match the RuleName of a registered ITacticalRule.
    string              RuleName,
    AlertSeverity       MinimumSeverity,
    /// Standing Order body template. Supports substitution tokens: {AlertId}, {RuleName},
    /// {Severity}, {DetectedAt}. Unknown tokens are left as literal text. Escape literal
    /// braces with {{ and }}. Max 2,048 chars AFTER substitution (validated at issuance).
    string              OrderContent,
    /// Null = no auto-expiry on the issued Standing Order.
    TimeSpan?           ExpiresAfter
);
```

#### §1.1 TacticalSignal payload schemas (normative)

| Kind | Required payload keys | Value types | Notes |
|---|---|---|---|
| `DecryptionFailureSpike` | `documentCount`, `eventCount`, `windowSeconds` | `int`, `int`, `int` | — |
| `PeerConnectivityLoss` | `activePeerCount`, `configuredFloor` | `int`, `int` | — |
| `MergeConflictRate` | `conflictsPerMinute`, `thresholdPerMinute` | `double`, `double` | Both in conflicts/minute |
| `CrdtGrowthAnomaly` | `documentId`, `byteEstimate`, `growthRatePerHour` | `string`, `long`, `double` | — |
| `AuthorizationFailureSpike` | `actorId`, `failureCount`, `windowSeconds` | `string`, `int`, `int` | `actorId` is `ActorId.ToString()` |
| `BulkAccessPattern` | `actorId`, `documentCount`, `accessType` | `string`, `int`, `string` | `accessType ∈ {"read","write","delete"}` |
| `ServiceDegradation` | `serviceName`, `previousStatus`, `currentStatus` | `string`, `string`, `string` | Status values per ADR 0062 enum names |
| `ProbeTimeout` | `probeName`, `timeoutMs` | `string`, `int` | — |
| `StandingOrderViolation` | `orderId`, `violationType` | `string`, `string` | — |
| `Custom` | Tenant-defined; no schema enforcement. | — | Sub-kind SHOULD be encoded in Payload["subKind"] (string). Rule XML doc MUST list expected keys. |

Implementations MUST NOT throw if extra keys are present (forward-compatibility).
`Custom` signals MUST carry a valid `ISignalProvenance` token to be accepted by
`ITacticalSignalIngressor` (§8.5). Unsigned `Custom` signals are rejected without routing.

---

### §2 Rule engine + routing interfaces

```csharp
// ── Individual rule contract ──────────────────────────────────────────────────

public interface ITacticalRule
{
    /// Stable identifier. Must match ^[A-Za-z0-9_\-\.]{1,128}$ and be globally unique.
    /// Sunfish first-party rules MUST use prefix sunfish.{domain}.{signal}
    /// (e.g., sunfish.crdt.decryption-failure). Third-party rules MUST NOT use
    /// the sunfish.* prefix. Enforcement: RegisterRule rejects sunfish.* from assemblies
    /// without Sunfish first-party strong-name/identity (§8.3).
    string           RuleName            { get; }
    AlertSeverity    DefaultSeverity     { get; }
    AlertRoutingPolicy DefaultRoutingPolicy { get; }

    /// Synchronous evaluation against a single signal.
    /// The rule constructs the full TacticalAlert (including RoutingPolicy, which MAY
    /// differ from DefaultRoutingPolicy for context-sensitive routing).
    /// MUST NOT do I/O; state needed for evaluation MUST be pre-populated externally
    /// (e.g., via constructor injection of a rule-state cache).
    /// MUST NOT throw on unexpected signal shapes — return false and null instead.
    bool Evaluate(TacticalSignal signal, out TacticalAlert? alert);
}

// ── Rule engine ───────────────────────────────────────────────────────────────

public interface ITacticalRuleEngine
{
    /// <summary>
    /// Registers a rule at application startup. Throws InvalidOperationException if
    /// RuleName is already registered or violates §8.3 prefix rules.
    /// MUST NOT be called after the first signal is processed.
    /// </summary>
    void RegisterRule(ITacticalRule rule);

    /// <summary>
    /// Evaluates all registered rules against the signal. Returns zero or more alerts.
    /// Rules are evaluated in registration order; all rules are invoked (no short-circuit).
    /// Throwing rules are caught and skipped without affecting other rules (§2.1).
    /// Signal ordering per tenant is maintained in arrival order. OccurredAt is preserved
    /// on the alert but evaluation does NOT reorder signals for watermark correctness — 
    /// rules MUST NOT assume OccurredAt ordering (§2.2).
    /// </summary>
    IReadOnlyList<TacticalAlert> Evaluate(TacticalSignal signal);

    /// Streaming form for continuous signal ingestion.
    IAsyncEnumerable<TacticalAlert> EvaluateStreamAsync(
        IAsyncEnumerable<TacticalSignal> signals,
        CancellationToken ct = default);

    IReadOnlyList<ITacticalRule> GetRegisteredRules();
}

// ── Alert router ──────────────────────────────────────────────────────────────

public interface IAlertRouter
{
    /// <summary>
    /// Routes an alert produced by the rule engine.
    ///
    /// Order of operations (normative):
    ///   1. Validate AlertId matches ^[A-Za-z0-9_\-\.:]{1,128}$. On failure: emit
    ///      TacticalAuthorizationDenied(denialReason="invalid-alert-id") and return.
    ///   2. Enforce MaxAlertsPerMinutePerRule. On breach: emit
    ///      TacticalAuthorizationDenied(denialReason="rule-rate-limit") and return.
    ///   3. Emit AnomalyDetected audit event.
    ///   4. Emit AlertRouted audit event.
    ///   5. For HighPriorityLookout policy: call ILookout.WriteAsync.
    ///      For InformationalSonar policy: write to ISonarStore (§2.3) only.
    ///
    /// Audit events in steps 3–4 MUST be committed before step 5. If step 5 fails,
    /// the audit events MUST NOT be rolled back. Log the step-5 failure at Warning.
    ///
    /// HighPriorityLookout routing with AllowedHighPriorityRulePrefixes enforcement:
    /// If alert.RuleName does NOT match an allowed prefix (§8.3), downgrade to
    /// InformationalSonar; emit TacticalAuthorizationDenied(denialReason=
    /// "high-priority-routing-not-allowlisted") alongside routing.
    ///
    /// MUST complete within 200ms (caller-applied 250ms timeout as defense-in-depth).
    /// </summary>
    ValueTask RouteAsync(TacticalAlert alert, CancellationToken ct = default);
}

// ── Sonar Room store (InformationalSonar alerts) ──────────────────────────────

public interface ISonarStore
{
    /// Writes an InformationalSonar-policy alert for later retrieval.
    /// Does NOT add to ILookout. Capacity: MaxActiveAlerts (shared with ILookout total).
    /// Alerts with Status=Active and DetectedAt ≤ (UtcNow - AlertTtl) are pruned on write.
    ValueTask WriteAsync(TacticalAlert alert, CancellationToken ct = default);

    /// Returns all Active InformationalSonar alerts for the tenant, DetectedAt DESC.
    IReadOnlyList<TacticalAlert> GetActiveAlerts(TenantId tenantId);
}

// ── Lookout (HighPriorityLookout alerts) ─────────────────────────────────────

public interface ILookout
{
    /// <summary>
    /// Writes a HighPriorityLookout alert. Called exclusively by IAlertRouter.
    /// Capacity enforcement: when active (Status=Active) alert count reaches
    /// MaxActiveAlerts, the oldest-DetectedAt Active alert is evicted (Status → Expired,
    /// LookoutAlertEvicted audit event emitted), then the new alert is written.
    /// Thread-safe: implementations MUST use a per-tenant lock or concurrent data structure.
    /// </summary>
    ValueTask WriteAsync(TacticalAlert alert, CancellationToken ct = default);

    /// Returns Active-only alerts (Acknowledged excluded from Lookout ticker).
    /// DetectedAt DESC, capped at MaxActiveAlerts.
    IReadOnlyList<TacticalAlert> GetActiveLookoutAlerts(TenantId tenantId);

    /// <summary>
    /// Yields an updated list whenever:
    /// (a) a new alert is written (addition); (b) an alert expires or is superseded;
    /// (c) heartbeat per HeartbeatInterval.
    /// Yields on Acknowledged status-change MUST use a separate polite channel
    /// (NOT the assertive Lookout region) — do NOT include acknowledged-status changes
    /// in this stream unless on heartbeat.
    /// Backpressure: Channel capacity-1 DropOldest.
    /// </summary>
    IAsyncEnumerable<IReadOnlyList<TacticalAlert>> SubscribeLookoutAsync(
        TenantId tenantId, CancellationToken ct = default);
}
```

#### §2.1 Rule evaluation failure modes

| Failure | Behavior |
|---|---|
| Rule.Evaluate throws unexpectedly | Engine catches, increments per-rule error counter, logs Warning; skips rule for this signal; continues evaluating remaining rules. Rule errors exceeding 100/minute MUST emit TacticalAuthorizationDenied(denialReason="rule-evaluation-failure-rate") once per minute as an anomaly marker. |
| `EvaluateStreamAsync` source faults | Enumeration stops; exception propagates to caller. Caller MUST reconnect to signal source independently. |
| No rules registered | `Evaluate` returns empty list. If first signal arrives and `GetRegisteredRules()` is empty, log Warning once per tenant per restart. |

#### §2.2 Signal evaluation ordering

Signal evaluation for a given `TenantId` MUST be performed in arrival order (not `OccurredAt`
order). `OccurredAt` is preserved on the resulting `TacticalAlert` for display and audit; it
does NOT govern evaluation sequencing. Rules MUST NOT assume `OccurredAt` monotonicity.
Implementations using a `Channel<TacticalSignal>` MUST partition by `TenantId` to prevent
cross-tenant head-of-line blocking.

#### §2.3 AlertRouted audit responsibility

`IAlertRouter.RouteAsync` is the sole emitter of `AnomalyDetected` + `AlertRouted` audit
events for Tactical alerts. No other layer duplicates these. `triggeringActorId` in the
`AnomalyDetected` payload is populated from the signal payload when the kind carries an actor
(`AuthorizationFailureSpike`, `BulkAccessPattern`); null otherwise.

---

### §3 Provider + command interfaces

```csharp
// ── Read-only tactical data provider ─────────────────────────────────────────

public interface ITacticalDataProvider
{
    /// <summary>
    /// Returns a current snapshot. Actor-specific: CanAccessFireControl +
    /// CanAcknowledgeAlerts pre-resolved via IPermissionResolver.
    /// ActiveAlerts is filtered by actor: non-ViewFireControl actors receive only
    /// InformationalSonar-policy alerts (HighPriorityLookout omitted per §6 Note).
    /// Implementations MUST NOT cache across actors or TenantId boundaries.
    /// On partial failure: return snapshot with IsPartialSnapshot=true and
    /// DegradedSubsystems populated; MUST NOT throw.
    /// MUST complete within 1s; callers SHOULD apply a 1.5s timeout.
    /// </summary>
    ValueTask<TacticalSnapshot> GetSnapshotAsync(
        TenantId tenantId, Principal actor, CancellationToken ct = default);

    /// <summary>
    /// Returns alerts for the tenant, filtered by actor permissions (see §6 Note).
    /// filterPolicy=null returns all policy types the actor may see.
    /// Sorted DetectedAt DESC, capped at TacticalOptions.MaxActiveAlerts.
    /// </summary>
    ValueTask<IReadOnlyList<TacticalAlert>> GetAlertsAsync(
        TenantId tenantId, Principal actor,
        AlertRoutingPolicy? filterPolicy = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns open + investigating incidents (IncidentStatus != Resolved).
    /// Requires ViewFireControl; returns empty list for actors without it.
    /// Sorted OpenedAt DESC, capped at TacticalOptions.MaxActiveIncidents.
    /// </summary>
    ValueTask<IReadOnlyList<IncidentRecord>> GetActiveIncidentsAsync(
        TenantId tenantId, Principal actor, CancellationToken ct = default);

    /// <summary>
    /// Yields an updated TacticalSnapshot whenever:
    /// (a) a new alert enters or leaves Lookout;
    /// (b) an incident opens, transitions, or closes;
    /// (c) heartbeat per HeartbeatInterval.
    /// MUST NOT emit on SignalRatePerMinute-only changes (updated on heartbeat).
    /// Permission MUST be re-resolved on every emission; if actor's ViewTactical is
    /// revoked, MUST terminate stream with TacticalUnauthorizedException and emit
    /// TacticalAuthorizationDenied.
    /// Backpressure: Channel capacity-1 DropOldest.
    /// </summary>
    IAsyncEnumerable<TacticalSnapshot> SubscribeSnapshotAsync(
        TenantId tenantId, Principal actor, CancellationToken ct = default);
}

// ── Tactical command service ──────────────────────────────────────────────────

public interface ITacticalCommandService
{
    /// <summary>
    /// Acknowledges a Lookout alert.
    /// 1. Emit TacticalAlertAcknowledgementRequested.
    /// 2. Verify actor has AcknowledgeTacticalAlert (IPermissionResolver). On denial:
    ///    emit TacticalAuthorizationDenied; throw TacticalUnauthorizedException.
    /// 3. If alert Status is already Acknowledged: no-op; emit no audit event; return.
    /// 4. Update alert Status → Acknowledged; populate AcknowledgedBy/AcknowledgedAt.
    /// 5. Emit TacticalAlertAcknowledged.
    /// ILookout.SubscribeLookoutAsync MUST emit refreshed list with updated alert
    /// before the next heartbeat (§2 ILookout contract).
    /// </summary>
    ValueTask AcknowledgeAlertAsync(
        TenantId tenantId, Principal actor, string alertId,
        CancellationToken ct = default);

    /// <summary>
    /// Opens an incident from a root alert.
    /// Pre-op: emit IncidentOpenRequested.
    /// If alertId does not exist: throw InvalidOperationException.
    /// If an incident is already open for rootAlertId: return existing IncidentRecord
    ///   (idempotent — no duplicate audit events).
    /// On success: emit IncidentOpened.
    /// Requires OpenIncident permission.
    /// title max 256 chars; runbookStepIds each matching ^[a-z][a-z0-9\-]{0,63}$.
    /// </summary>
    ValueTask<IncidentRecord> OpenIncidentAsync(
        TenantId tenantId, Principal actor, string rootAlertId, string title,
        IReadOnlyList<string> runbookStepIds, CancellationToken ct = default);

    /// <summary>
    /// Closes an incident.
    /// Pre-op: emit IncidentCloseRequested.
    /// resolutionNote MUST be non-null and non-whitespace after string.Trim() (max 4,096 chars).
    ///   Throws ArgumentException on null/whitespace/overflow.
    /// Requires CloseIncident permission; on denial: emit TacticalAuthorizationDenied;
    ///   throw TacticalUnauthorizedException.
    /// On success: sets Status = Resolved; emit IncidentClosed.
    /// </summary>
    ValueTask CloseIncidentAsync(
        TenantId tenantId, Principal actor, string incidentId,
        string resolutionNote, CancellationToken ct = default);
}

/// <remarks>
/// Inherits UnauthorizedAccessException to prevent swallowing by general catch handlers.
/// MUST NOT be caught by retry logic.
/// </remarks>
public sealed class TacticalUnauthorizedException : UnauthorizedAccessException
{
    public TacticalUnauthorizedException(string message) : base(message) { }
}
```

#### §3.1 Provider failure modes

| Failure | Behavior |
|---|---|
| `ILookout` unavailable during `GetSnapshotAsync` | Return snapshot with empty LookoutAlerts, IsPartialSnapshot=true, DegradedSubsystems=["Lookout"]. MUST NOT throw. |
| `ISonarStore` unavailable | Return snapshot with empty ActiveAlerts (InformationalSonar), IsPartialSnapshot=true, DegradedSubsystems includes "SonarRoom". |
| `GetActiveIncidentsAsync` times out (>1.5s) | Return empty list with IsPartialSnapshot=true on snapshot; log ILogger.LogWarning. |
| `IPermissionResolver` unavailable during `GetSnapshotAsync` | Fail-closed: CanAccessFireControl=false, CanAcknowledgeAlerts=false; IsPartialSnapshot=true, DegradedSubsystems=["PermissionResolver"]. |
| `SubscribeSnapshotAsync` source fault | Yield final snapshot with IsPartialSnapshot=true; stop enumeration; caller reconnects independently. |
| Actor permission revoked mid-subscription | Terminate stream; throw TacticalUnauthorizedException; emit TacticalAuthorizationDenied (see §3 SubscribeSnapshotAsync doc). |

---

### §4 Threat-trigger Standing Orders

```csharp
public interface IThreatTriggerService
{
    /// <summary>
    /// Registers a Standing Order template. Called at application startup only.
    /// Throws InvalidOperationException if:
    ///   - RuleName already has a registered template.
    ///   - RuleName not registered with ITacticalRuleEngine (templates must follow rules).
    ///   - RuleName violates §8.3 prefix rules.
    /// </summary>
    void RegisterTemplate(ThreatTriggerTemplate template);

    /// <summary>
    /// Attempts to issue a Standing Order for the given alert.
    /// The issuer principal is resolved INTERNALLY from ISystemPrincipalProvider (§4.1);
    /// no external Principal parameter is accepted.
    ///
    /// Returns null (without audit) if:
    ///   - No template registered for alert.RuleName.
    ///   - Alert.Severity < template.MinimumSeverity.
    ///
    /// Returns null WITH audit on permission denial (system principal lacks
    /// IssueEmergencyStandingOrder): emit TacticalAuthorizationDenied(denialReason=
    /// "IssueEmergencyStandingOrder", attemptedBy=systemPrincipal.ActorId).
    ///
    /// Order of operations on issuance:
    ///   1. Verify alert.TenantId matches ambient ITenantContext.TenantId (§8.2).
    ///      On mismatch: emit TacticalAuthorizationDenied(denialReason="tenant-mismatch"); return null.
    ///   2. Check deduplication (§4.3). On hit: return previously-issued StandingOrderId.
    ///   3. Check per-tenant rate limit (TacticalOptions.MaxEmergencyOrdersPerMinute). On breach:
    ///      emit TacticalAuthorizationDenied(denialReason="emergency-order-rate-limit"); return null.
    ///   4. Check per-signal Standing Order budget (§4.4). On breach:
    ///      emit TacticalAuthorizationDenied(denialReason="signal-order-budget-exceeded"); return null.
    ///   5. Generate orderId (Guid.NewGuid().ToString("N")) before AppendAsync.
    ///   6. Perform template substitution (§4.2). On overflow: throw ArgumentException.
    ///   7. Emit EmergencyStandingOrderIssued (with orderId from step 5, pre-AppendAsync).
    ///   8. Call IStandingOrderRepository.AppendAsync. If it fails, log error; orderId is
    ///      committed in audit (step 7 is NOT rolled back). Emit EmergencyStandingOrderIssuanceFailed.
    ///      Return null.
    ///
    /// Returns the StandingOrderId on success.
    /// </summary>
    ValueTask<string?> TryIssueAsync(
        TacticalAlert alert,
        CancellationToken ct = default);
}
```

#### §4.1 Issuer authority chain

`IThreatTriggerService` resolves the system principal internally from
`ISystemPrincipalProvider` (a service registered at DI bootstrap that provides a
cryptographically-attested system identity). This principal is NOT supplied by callers of
`TryIssueAsync`. `ISystemPrincipalProvider.GetSystemPrincipalAsync(TenantId)` returns a
`Principal` with `ShipRole.System` (a reserved role not assignable to human actors). The
`IPermissionResolver` MUST grant `IssueEmergencyStandingOrder` to `ShipRole.System`.

#### §4.2 Standing Order content substitution

Supported substitution tokens: `{AlertId}`, `{RuleName}`, `{Severity}`, `{DetectedAt}`.
Escape literal braces with `{{` / `}}`. Unknown token identifiers are left as literal text
(not an error). Substitution is .NET string-Replace based (not interpolation). Max 2,048
chars measured post-substitution; implementations MUST throw `ArgumentException` at issuance
time (step 6) if the post-substitution result exceeds this limit.

#### §4.3 Threat-trigger deduplication (thread-safe)

Dedup key: `(TenantId, RuleName)`. Window: 60 seconds from the issuance timestamp of the
last successful Standing Order for that key. Dedup state is held in a thread-safe structure
(e.g., `ConcurrentDictionary<(TenantId, string), (DateTimeOffset issuedAt, string orderId)>`).
On restart, the dedup window is reset; up to N per-instance duplicates may occur across
restarts (N = number of running instances). This is accepted behavior; idempotency at
`IStandingOrderRepository.AppendAsync` is the canonical defense for duplicate suppression.

A second call within the window returns the cached `orderId` WITHOUT re-issuing and WITHOUT
emitting a second `EmergencyStandingOrderIssued` audit event.

#### §4.4 Per-signal Standing Order budget

At most 1 emergency Standing Order may be issued per `(TenantId, signal fingerprint)` per
invocation chain (where "signal fingerprint" = `{Kind}:{TenantId}:{OccurredAt.ToUnixTimeMs()}`).
This prevents N matching rules from producing N emergency orders for a single signal event.
The budget check is applied after deduplication (step 4 above). Excess emissions produce
the `TacticalAuthorizationDenied` audit event with `denialReason="signal-order-budget-exceeded"`.

---

### §5 Audit event types

This ADR introduces **13 new `AuditEventType` static-readonly constants** on
`Sunfish.Kernel.Audit.AuditEventType`:

```csharp
// Anomaly detection lifecycle
public static readonly AuditEventType AnomalyDetected                        = new("AnomalyDetected");
public static readonly AuditEventType AlertRouted                             = new("AlertRouted");
public static readonly AuditEventType TacticalAlertExpired                    = new("TacticalAlertExpired");
public static readonly AuditEventType LookoutAlertEvicted                     = new("LookoutAlertEvicted");

// Alert acknowledgement — two-phase
public static readonly AuditEventType TacticalAlertAcknowledgementRequested  = new("TacticalAlertAcknowledgementRequested");
public static readonly AuditEventType TacticalAlertAcknowledged               = new("TacticalAlertAcknowledged");

// Incident lifecycle — two-phase open + close
public static readonly AuditEventType IncidentOpenRequested                   = new("IncidentOpenRequested");
public static readonly AuditEventType IncidentOpened                          = new("IncidentOpened");
public static readonly AuditEventType IncidentCloseRequested                  = new("IncidentCloseRequested");
public static readonly AuditEventType IncidentClosed                          = new("IncidentClosed");

// Threat-trigger Standing Order
public static readonly AuditEventType EmergencyStandingOrderIssued            = new("EmergencyStandingOrderIssued");
public static readonly AuditEventType EmergencyStandingOrderIssuanceFailed    = new("EmergencyStandingOrderIssuanceFailed");

// Authorization denial (all tactical authorization failures)
public static readonly AuditEventType TacticalAuthorizationDenied             = new("TacticalAuthorizationDenied");
```

**Canonical payload schemas (key names are normative; enum values serialized as PascalCase
string; `Instant` values as ISO 8601 UTC; `ActorId`/`TenantId` as canonical Guid strings):**

| EventType | Required payload keys | Value types |
|---|---|---|
| `AnomalyDetected` | `tenantId`, `alertId`, `ruleName`, `severity`, `signalKind`, `detectedAt`, `triggeringActorId` | `string`, `string`, `string`, `string`, `string`, `DateTimeOffset`, `string?` |
| `AlertRouted` | `tenantId`, `alertId`, `routingPolicy`, `routedAt` | `string`, `string`, `string`, `DateTimeOffset` |
| `TacticalAlertExpired` | `tenantId`, `alertId`, `ruleName`, `expiredAt`, `expiryCause` | `string`, `string`, `string`, `DateTimeOffset`, `string` ("ttl"\|"superseded") |
| `LookoutAlertEvicted` | `tenantId`, `alertId`, `ruleName`, `severity`, `evictedAt` | `string`, `string`, `string`, `string`, `DateTimeOffset` |
| `TacticalAlertAcknowledgementRequested` | `tenantId`, `alertId`, `requestedBy`, `requestedAt` | `string`, `string`, `string`, `DateTimeOffset` |
| `TacticalAlertAcknowledged` | `tenantId`, `alertId`, `acknowledgedBy`, `acknowledgedAt` | `string`, `string`, `string`, `DateTimeOffset` |
| `IncidentOpenRequested` | `tenantId`, `rootAlertId`, `requestedBy`, `requestedAt` | `string`, `string`, `string`, `DateTimeOffset` |
| `IncidentOpened` | `tenantId`, `incidentId`, `rootAlertId`, `openedBy`, `openedAt` | `string`, `string`, `string`, `string`, `DateTimeOffset` |
| `IncidentCloseRequested` | `tenantId`, `incidentId`, `requestedBy`, `requestedAt` | `string`, `string`, `string`, `DateTimeOffset` |
| `IncidentClosed` | `tenantId`, `incidentId`, `closedBy`, `resolutionNote`, `closedAt` | `string`, `string`, `string`, `string`, `DateTimeOffset` |
| `EmergencyStandingOrderIssued` | `tenantId`, `alertId`, `ruleName`, `orderId`, `issuedBy`, `issuedAt` | `string`, `string`, `string`, `string`, `string`, `DateTimeOffset` |
| `EmergencyStandingOrderIssuanceFailed` | `tenantId`, `alertId`, `ruleName`, `orderId`, `failureReason`, `failedAt` | `string`, `string`, `string`, `string`, `string`, `DateTimeOffset` |
| `TacticalAuthorizationDenied` | `tenantId`, `attemptedAction`, `attemptedBy`, `denialReason`, `deniedAt` | `string`, `string`, `string`, `string`, `DateTimeOffset` |

---

### §6 Permission model

The following `ShipAction` values are introduced for Tactical:

```csharp
public static readonly ShipAction ViewTactical                = new("ViewTactical");
public static readonly ShipAction ViewFireControl             = new("ViewFireControl");
public static readonly ShipAction AcknowledgeTacticalAlert    = new("AcknowledgeTacticalAlert");
public static readonly ShipAction OpenIncident                = new("OpenIncident");
public static readonly ShipAction CloseIncident               = new("CloseIncident");
public static readonly ShipAction IssueEmergencyStandingOrder = new("IssueEmergencyStandingOrder");
/// ManageThreatTriggers is reserved for runtime template management (future).
/// In v1, templates are registered at startup only (no runtime API); this action
/// is declared now so ADR 0077's permission catalog is complete at build time.
public static readonly ShipAction ManageThreatTriggers        = new("ManageThreatTriggers");
```

**Resolution rules:**

| Action | Granted to roles |
|---|---|
| `ViewTactical` | `ShipRole.DivisionOfficer` with Sonar specialty, `ShipRole.TacticalOfficer`, `ShipRole.XO`, `ShipRole.Captain` |
| `ViewFireControl` | `ShipRole.TacticalOfficer`, `ShipRole.XO`, `ShipRole.Captain` |
| `AcknowledgeTacticalAlert` | `ShipRole.TacticalOfficer`, `ShipRole.XO`, `ShipRole.Captain` |
| `OpenIncident` | `ShipRole.TacticalOfficer`, `ShipRole.XO`, `ShipRole.Captain` |
| `CloseIncident` | `ShipRole.TacticalOfficer`, `ShipRole.XO`, `ShipRole.Captain` |
| `IssueEmergencyStandingOrder` | `ShipRole.System` (internal only — resolved via ISystemPrincipalProvider, §4.1) |
| `ManageThreatTriggers` | Reserved; not used in v1 |

**Note on `ViewTactical` specialty scoping.** A `ShipRole.DivisionOfficer` with Sonar
specialty receives `ViewTactical` but NOT `ViewFireControl`. In `ITacticalDataProvider`:
- `GetSnapshotAsync` pre-resolves this: `CanAccessFireControl=false`; `ActiveAlerts` includes
  only `InformationalSonar`-policy alerts; `LookoutAlerts` is empty (Lookout-visible count
  appears in Quarterdeck KPI card, not the full alert list).
- `GetAlertsAsync` with `actor` lacking `ViewFireControl` MUST filter out
  `HighPriorityLookout`-policy alerts.
- Specialty resolution is delegated to the `IPermissionResolver` ADR 0077 §4 implementation;
  the resolver MUST inspect specialty metadata to distinguish Sonar from other specialties.

---

### §7 UI contract — `blocks-tactical`

#### §7.1 Package structure

`blocks-tactical` depends on `foundation-tactical` and `foundation-ship-common` (ADR 0077).
It MUST NOT depend directly on `foundation-wayfinder` (ADR 0065) or `foundation-engine-room`
(ADR 0079) — signal sources are injected via `ITacticalDataProvider` and `IAlertRouter`.

#### §7.2 Quarterdeck alert source implementation

`blocks-tactical` exports `LookoutQuarterdeckAlertSource : IQuarterdeckAlertSource`
(ADR 0080 §2). Normative implementation contract:

- `SourceName` = `"sunfish.tactical.lookout"` (§8.3 registered-prefix requirement)
- `GetAlertsAsync(TenantId tenantId, ...)`:
  1. Resolve ambient tenant via `ITenantContext`. MUST verify `tenantId == ambient.TenantId`;
     on mismatch emit `TacticalAuthorizationDenied(denialReason="tenant-mismatch")` and
     return empty list.
  2. Delegate to `ILookout.GetActiveLookoutAlerts(tenantId)`.
  3. Filter: `alert.TenantId == tenantId` (defense-in-depth against Lookout cross-tenant leak).
  4. Map `TacticalAlert → QuarterdeckAlert` with `VisibilityPolicy = OmitForDeniedActors`
     (all Tactical-sourced alerts use the default ADR 0080 policy).
  5. Sort `DetectedAt DESC`; return at most 50 items.

#### §7.3 Sub-room composition

Three routable sub-rooms via ADR 0077 §4 deck-progressive-disclosure pattern:

1. **Sonar Room** — signal-rate gauge, registered-rule list, InformationalSonar-policy alerts.
   Navigation: `<section role="region" aria-labelledby="sonar-room-heading">`;
   heading `<h2 id="sonar-room-heading">Sonar Room</h2>`.
   Signal-rate gauge: `role="meter"` with `aria-valuemin="0"`, `aria-valuemax` from config,
   `aria-valuenow`, `aria-valuetext="N signals per minute"`. Rate numeric rendered as
   visible text with `aria-hidden="true"`. SR-safe updates via a separate polite sibling
   `<div aria-live="polite" aria-atomic="true">` — announcement throttled to threshold
   crossings only (not every heartbeat) to prevent SR saturation.
   
2. **Lookout** — HighPriorityLookout-policy Active alerts.
   Navigation: `<section role="region" aria-labelledby="lookout-heading">`;
   heading `<h2 id="lookout-heading">Lookout</h2>`.
   Live region: `<ul aria-live="assertive" aria-atomic="false" aria-relevant="additions"
   aria-label="High-priority tactical alerts">`. New-item announcements only; Acknowledged
   status-changes MUST NOT be pushed into this region (separate polite region for those).
   Pause control: `<button aria-pressed="false">Pause Lookout ticker</button>` — static label;
   `aria-pressed` toggled on click. Default-to-paused under `prefers-reduced-motion: reduce`.
   `SunfishA11yAssertions.ReducedMotionDefaultsToPaused` REQUIRED.
   
3. **Fire Control** — active incidents + runbook steps + escalation paths.
   Navigation: `<section role="region" aria-labelledby="fire-control-heading">`;
   heading `<h2 id="fire-control-heading">Fire Control</h2>`.
   Incidents: `<ol aria-label="Active incidents">` with `<li>` per incident.
   Runbook: `<ol aria-label="Runbook steps">` with `<li>` per step — step number rendered
   as visible text, accessible name via `aria-labelledby` referencing the step-number span
   and step-title span (NOT `aria-label` which suppresses visible content).
   Incident state transitions announced via polite live region sibling to the incident list.

Sub-room skip-link targets:

```html
<main id="main-content" tabindex="-1">
  <a href="#sonar-room" class="skip-link">Skip to Sonar Room</a>
  <a href="#lookout" class="skip-link">Skip to Lookout</a>
  <a href="#fire-control" class="skip-link">Skip to Fire Control</a>
  ...
  <section id="sonar-room" tabindex="-1" role="region" aria-labelledby="sonar-room-heading">
  <section id="lookout" tabindex="-1" role="region" aria-labelledby="lookout-heading">
  <section id="fire-control" tabindex="-1" role="region" aria-labelledby="fire-control-heading">
```

#### §7.4 Alert severity presentation

Severity MUST be encoded with BOTH color AND a non-color indicator (SC 1.4.1):

| Severity | Color token | Icon shape | Text label | aria treatment |
|---|---|---|---|---|
| Critical | `--color-severity-critical` | Octagon | "Critical" (visible, not sr-only) | Icon `aria-hidden="true"` |
| High | `--color-severity-high` | Triangle | "High" | Icon `aria-hidden="true"` |
| Medium | `--color-severity-medium` | Diamond | "Medium" | Icon `aria-hidden="true"` |
| Low | `--color-severity-low` | Circle | "Low" | Icon `aria-hidden="true"` |
| Informational | `--color-severity-informational` | Info badge | "Info" | Icon `aria-hidden="true"` |

Icon shapes MUST be visually distinct (not same-shape + color-tinted). Color tokens against
card background MUST meet SC 1.4.3 (text ≥ 4.5:1) and SC 1.4.11 (non-text ≥ 3:1). Focus
indicators on interactive elements MUST meet SC 2.4.7 (≥ 3:1 contrast against background,
≥ 2px outline area).

Pulsing/flashing severity indicators (if used for Critical/High): default behavior MUST NOT
include flashing regardless of `prefers-reduced-motion`. Flashing is opt-in (developer or
tenant explicit choice). If flashing is used: MUST stay below SC 2.3.1 threshold (< 3 flash
events per second for both general and red-flash thresholds). MUST be suppressed entirely
under `prefers-reduced-motion: reduce`.

#### §7.5 Accessibility contract

- MUST NOT use `role="banner"` (reserved for app-shell `<header>`). Use `role="region"` per §7.3.
- Alert acknowledge button:
  - MUST use `aria-disabled="true"` (NEVER native `disabled` attribute — native disabled
    removes from tab order, preventing keyboard users from reaching the denial reason).
  - MUST remain focusable when `aria-disabled="true"`.
  - MUST suppress click AND keydown (Enter, Space) handlers when disabled (early return).
  - Denial reason MUST be on a visible element with `id="ack-denial-reason-<alertId>"`
    referenced via `aria-describedby` on the button.
  - CSS: `cursor: not-allowed` when `aria-disabled="true"`.
  - `SunfishA11yAssertions.AriaDisabledButtonRemainsInTabOrder` REQUIRED.
  - `SunfishA11yAssertions.AriaDisabledSuppressesActivation` REQUIRED.
- `SunfishA11yAssertions.ReducedMotionDefaultsToPaused` REQUIRED (Lookout ticker + Sonar rate animations).
- `SunfishA11yAssertions.AssertiveRegionAnnouncesAdditionsOnly` REQUIRED (Lookout ticker).
- `SunfishA11yAssertions.SubRoomsKeyboardReachable` REQUIRED (skip-links + tab order).
- `SunfishA11yAssertions.AlertDialogHasRoleModalLabelDescribedBy` REQUIRED (§7.6 dialog).
- `SunfishA11yAssertions.DeliberationPauseAnnouncesEnablement` REQUIRED (§7.6 confirm button).
- `SunfishA11yAssertions.IncidentStateTransitionAnnounced` REQUIRED (Fire Control).
- SC 2.2.2 (Pause, Stop, Hide): Lookout auto-scroll/ticker MUST expose pause control; MUST
  auto-pause on hover AND on keyboard focus entering the Lookout region.
- Focus visible: all focusable elements MUST have visible focus indicator (SC 2.4.7).

#### §7.6 Emergency Standing Order confirmation dialog

When an actor manually triggers a threat-trigger Standing Order issuance from Fire Control:

- Dialog element: `<div role="alertdialog" aria-modal="true"
  aria-labelledby="dialog-title" aria-describedby="dialog-consequence">`.
  Use `role="alertdialog"` (not generic `dialog`) — this is a security-critical destructive
  confirmation. Screen readers announce it immediately on open.
- Focus: `IFocusTrap.TrapFocus(dialog)` on open. Initial focus: dialog container with
  `tabindex="-1"` (NOT the Confirm button, which is disabled at open).
- Consequence text: `<p id="dialog-consequence">` MUST include the fully-resolved
  Standing Order content preview (post-substitution text, not template tokens). Max 280 chars
  in the primary view; for content > 280 chars, truncate with `<details><summary>Show full
  text</summary>...</details>` so SR users get a digestible primary announcement.
- Confirm button deliberation-pause: MUST be `aria-disabled="true"` at dialog open;
  MUST become enabled exactly 2 000 ms after dialog open event (matching ADR 0080 §3 token).
  On enabling: polite live region MUST announce "Confirm available" (e.g.,
  `<div aria-live="polite" aria-atomic="true" class="sr-only">Confirm available</div>`
  — inject text at t=2000ms). `SunfishA11yAssertions.DeliberationPauseAnnouncesEnablement`.
- Cancel button: always enabled; focus moves to Cancel on dialog open.
- Close: `IFocusTrap.RestoreFocus(fallback: MainLandmark)`. Polite live region announces
  outcome: "Standing Order issued" or "Cancelled" on close.
  `SunfishA11yAssertions.DialogOutcomeAnnouncedOnClose`.
- `SunfishA11yAssertions.AlertDialogHasRoleModalLabelDescribedBy` enforces role + aria-modal
  + aria-labelledby + aria-describedby presence.

---

### §8 Security contract

#### §8.1 ShipAction startup registration check

At application startup, `blocks-tactical` MUST verify that all 7 `ShipAction` values
declared in §6 are registered with the `IPermissionResolver` implementation. Any
unregistered action MUST cause the application to fail startup with a descriptive
`InvalidOperationException`. This mirrors ADR 0079 §4.3 and ADR 0080 §5.1.
The verification API mirrors ADR 0077 §4's `IPermissionResolver` registration check
(forward-ref to W#46 build; confirm exact method signature at Stage 06).

#### §8.2 Tenant context binding + anti-spoofing

All service methods take `TenantId tenantId` as the first parameter. Implementations MUST
resolve the ambient `ITenantContext.TenantId` and MUST verify it equals the supplied
`tenantId`. On mismatch: emit `TacticalAuthorizationDenied(denialReason="tenant-mismatch")`
and throw `TacticalUnauthorizedException`.

Additionally, `IThreatTriggerService.TryIssueAsync` MUST verify `alert.TenantId` matches
the ambient tenant before any other step (§4 step 1). This prevents a malicious or buggy rule
from routing a crafted alert to a different tenant's Standing Order repository.

Snapshot and alert-list results are actor-specific. Implementations MUST NOT cache across
actors or `TenantId` boundaries. Cache key MUST include both `TenantId` and `ActorId`.

#### §8.3 Rule name uniqueness + registered-prefix enforcement

`ITacticalRuleEngine.RegisterRule` MUST enforce:
1. `RuleName` uniqueness (case-normalized to lowercase NFC form before comparison).
2. `RuleName` for `sunfish.*`-prefix rules MUST originate from an assembly with verified
   Sunfish first-party identity (strong-name or internal-only registration API). Implementations
   MUST throw `InvalidOperationException("Prefix 'sunfish.*' is reserved")` for rules registered
   from non-first-party assemblies.
3. Third-party rules MUST use `{vendor}.{product}.{signal}` prefix.

`TacticalAlert.AlertId` MUST match `^[A-Za-z0-9_\-\.:]{1,128}$` (note: `.` is permitted
to accommodate dots in `RuleName`). Format MUST be `{RuleName}:{source-local-id}`.
Validation at `IAlertRouter.RouteAsync` step 1 (§2 RouteAsync contract).

`HighPriorityLookout` routing for non-`sunfish.*` rules requires opt-in:
`TacticalOptions.AllowedHighPriorityRulePrefixes` (default: `["sunfish.*"]`). Rules with
prefixes not in this allowlist that declare `HighPriorityLookout` routing are silently
downgraded to `InformationalSonar` with a `TacticalAuthorizationDenied` audit event.

#### §8.4 Threat-trigger audit-before-action

Every `IThreatTriggerService.TryIssueAsync` call that reaches issuance (step 7 in §4)
MUST emit `EmergencyStandingOrderIssued` BEFORE calling `IStandingOrderRepository.AppendAsync`.
The `orderId` in the audit payload is generated client-side (step 5 in §4) and MUST be
passed to `AppendAsync` as the canonical order ID. If `AppendAsync` fails, the pre-committed
audit record `EmergencyStandingOrderIssued` retains the intent; an additional
`EmergencyStandingOrderIssuanceFailed` audit event MUST be emitted on failure.

Authorization-denial paths in `TryIssueAsync` MUST emit `TacticalAuthorizationDenied`
before returning null. The denial path MUST NOT emit `EmergencyStandingOrderIssued`.

#### §8.5 Signal amplification budget

To prevent a single signal from triggering disproportionate system response:

1. **Per-rule rate limit**: `IAlertRouter.RouteAsync` enforces `MaxAlertsPerMinutePerRule`
   per `(TenantId, RuleName)`. On breach: `TacticalAuthorizationDenied` + silent drop.
2. **Per-tenant emergency order rate**: `IThreatTriggerService` enforces
   `MaxEmergencyOrdersPerMinute` per tenant globally (across all rules). On breach:
   `TacticalAuthorizationDenied(denialReason="emergency-order-rate-limit")`.
3. **Per-signal order budget**: At most 1 emergency Standing Order per
   `(TenantId, signal fingerprint)` per invocation chain (§4.4).

---

## §A0 — Appendix: cited symbols and forward references

| Symbol | Status |
|---|---|
| `Sunfish.Foundation.Assets.Common.TenantId` | PASS ✓ |
| `Sunfish.Foundation.Assets.Common.ActorId` | PASS ✓ |
| `Sunfish.Foundation.Capabilities.Principal` | PASS ✓ |
| `Sunfish.Foundation.Ship.Common.ShipRole` | FORWARD-REF (ADR 0077 §3 — W#46 build) |
| `Sunfish.Foundation.Ship.Common.ShipAction` | FORWARD-REF (ADR 0077 §2 — W#46 build) |
| `Sunfish.Foundation.Ship.Common.IPermissionResolver` | FORWARD-REF (ADR 0077 §4 — W#46 build; startup-check API to be confirmed) |
| `Sunfish.Foundation.Ship.Common.ISystemPrincipalProvider` | FORWARD-REF (ADR 0077 or new type; added by this ADR as a required seam) |
| `Sunfish.Kernel.Audit.AuditEventType` | PASS ✓ (ADR 0049) |
| `Sunfish.Foundation.Wayfinder.IStandingOrderRepository.AppendAsync` | PASS ✓ (ADR 0065 §2; verified W#42 built); caller-supplied orderId support to be confirmed at Stage 06 |
| `Sunfish.Foundation.Wayfinder.IOodWatchService` | FORWARD-REF (ADR 0078 §2 — W#49 Proposed) |
| `Sunfish.Foundation.Quarterdeck.IQuarterdeckAlertSource` | FORWARD-REF (ADR 0080 §2 — W#51 Proposed) |
| `Sunfish.Foundation.Quarterdeck.QuarterdeckAlert` | FORWARD-REF (ADR 0080 §1 — W#51 Proposed) |
| `Sunfish.Foundation.UI.Testing.SunfishA11yAssertions` | FORWARD-REF (ADR 0077 test utilities — W#46 build) |
| `Sunfish.Foundation.UI.IFocusTrap` | FORWARD-REF (ADR 0077 §6 — W#46 build) |
| `Sunfish.Foundation.UI.ITenantContext` | FORWARD-REF (confirm namespace — ADR 0008 or ADR 0077) |
| `NodaTime.Instant`, `NodaTime.Duration` | PASS ✓ (external) |

**Open Q1 (confirmed):** `IStandingOrderRepository.AppendAsync` signature — verified from
ADR 0065 §2 and W#42 implementation. Signature:
`ValueTask<StandingOrderId> AppendAsync(StandingOrder order, CancellationToken ct = default)`.
Whether `AppendAsync` accepts a caller-supplied `orderId` for idempotency purposes is to be
confirmed at Stage 06. If not supported, `IThreatTriggerService` MUST generate the `orderId`
independently and store it alongside the audit record for reconciliation.

**Open Q2 (design decision, resolved):** `TacticalSignalKind.Custom` payload schema is
tenant-owned. Sunfish MUST NOT validate payload keys for `Custom` signals. Custom signals
MUST carry a valid `ISignalProvenance` token (§8.5 via `ITacticalSignalIngressor`).
Custom rules MUST declare expected payload keys in XML `<summary>` doc; this is advisory.

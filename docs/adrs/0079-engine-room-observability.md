---
id: 79
title: Engine Room Observability Surface
status: Accepted
date: 2026-05-05
tier: foundation
pipeline_variant: sunfish-feature-change

concern:
  - observability
  - audit
  - accessibility
  - security

enables:
  - engine-room-observability
  - crdt-health-dashboard
  - damage-control

composes:
  - 77
  - 28
  - 49
  - 65
  - 78
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments: []
---

# ADR 0079 — Engine Room Observability Surface

**Status:** Proposed
**Date:** 2026-05-05
**Authors:** XO research session
**Pipeline variant:** `sunfish-feature-change`
**Council posture:** standard adversarial + WCAG/a11y subagent (mandatory — log table + timeline +
chart surfaces all have known Aspire a11y debt that Engine Room must exceed) + security-engineering
subagent (mandatory — Damage Control destructive operations are an elevated-authority surface)

---

## Context

The W#35 Ship Architecture discovery (§5.3) tagged Engine Room as **Partial** coverage: the
*infrastructure layer* is well-specified (ADR 0028 CRDT engine; `Microsoft.Extensions.Logging`;
ADR 0049 audit trail; ADR 0036 sync states) but the *aggregation UI* — the unified dashboard that
puts logs, CRDT health, sync-daemon metrics, and Damage Control tools in one place — does not
exist. Operators today respond to incidents by inspecting per-package consoles, running ad-hoc
queries, and inferring system health from separate sources.

The Aspire Dashboard is the dominant model for .NET application observability. Engine Room
adopts the Aspire-shaped pattern (log/trace/metric/health aggregation) but explicitly exceeds
Aspire's own accessibility baseline (Aspire log tables break screen-reader row context; Aspire
charts have no accessible alternatives). This exceedance is a contract, not a goal.

Engine Room is partitioned into four sub-rooms per W#35 §5.3:
- **Main Propulsion** — CRDT engine + sync daemon health (live throughput, peer-list freshness,
  gossip-cycle counts)
- **Electrical** — per-tenant CRDT document growth (byte counts, compaction eligibility,
  snapshot ages)
- **Damage Control** — quarantine override, manual snapshot compaction, elevated audit (below
  the waterline; requires watch-qualified authority)
- **QA Workshop** — council-review surface + test-runner output (Division Officer / QA role)

---

## Decision drivers

1. **Incident response time.** Operators responding to a "sync daemon unhealthy" alert must
   navigate to the Engine Room and see the root cause within 30 seconds. No pivot to a separate
   tool. This is a latency requirement on the surface contract, not just the implementation.
2. **Damage Control authority.** Quarantine and manual compaction are destructive; they require
   `ShipRole.EngineerOfficer` + currently-on-watch `EOOW` designation (ADR 0078 §1) OR
   `ShipRole.Captain` / `ShipRole.XO` override. Permission gating via ADR 0077
   `IPermissionResolver` is mandatory — UI MUST NOT reveal Damage Control actions to
   unauthorized roles.
3. **Audit-by-construction.** Damage Control actions MUST emit audit events before the
   operation proceeds. An action that succeeds without a pre-op audit record is a compliance gap.
4. **OpenTelemetry compatibility.** Engine Room exposes all engine metrics via the
   `Microsoft.Extensions.Diagnostics` (`Meter` / `ActivitySource`) seams so that Aspire
   Dashboard, Prometheus, Grafana, and other OTel-compatible backends can consume them without
   Sunfish-specific adapters. This is a contract on the metric naming catalog.
5. **WCAG 2.2 AA exceedance over Aspire.** Per W#35 §5.3 Stage 1.5 hardening:
   - Log table virtualization MUST preserve screen-reader row context
   - Trace timeline MUST have its accessible table alternative always present in the
     accessibility tree (never gated behind a user-action toggle)
   - Every metric chart MUST have a `<table>` data alternative using an sr-only/clip technique
   - Damage Control elevated dialog MUST be keyboard-operable
6. **Shared Design System composition.** Engine Room builds on ADR 0077's `foundation-ship-common`
   (`ShipRole`, `IPermissionResolver`, `PermissionDecision`) and its design tokens. No
   standalone permission resolution; no standalone token catalog.
7. **EOOW watch authority (ADR 0078).** Damage Control requires the current EOOW on watch
   per ADR 0078. `IOodWatchService.GetActiveWatchAsync(tenantId, OodRole.EngineeringOfficerOfTheWatch)`
   is the watch-state query Engine Room Damage Control uses.

---

## Considered options

### Option A — Engine Room as a pure UI block with no foundation contract

Implement Engine Room entirely as `blocks-engine-room`, reading directly from `ICrdtEngine`,
`IAuditTrail`, and `Microsoft.Extensions.Logging`. No new foundation package.

**Pro:** minimal new surface; fast to build.
**Con:** the data model for CRDT growth metrics, sync daemon health, and Damage Control results
  is embedded in the block, making it untestable independently and unreusable by Bridge or the
  iOS field app. The OTel metric catalog would be scattered across the block with no canonical
  naming. Damage Control authorization logic duplicated in every adapter.
**Verdict:** rejected.

### Option B — Thin `foundation-engine-room` contracts + `blocks-engine-room` UI **[RECOMMENDED]**

`foundation-engine-room` contains the observable data model (`SyncDaemonHealth`,
`CrdtGrowthMetrics`, `EngineRoomHealthSummary`), the provider/command interfaces
(`IEngineRoomDataProvider`, `IEngineRoomCommandService`), the OTel metric catalog (string
constants), and the new `AuditEventType` constants. `blocks-engine-room` is the UI block
that binds the data to the Shared Design System primitives.

**Pro:** contracts are independently testable; Block adapters (Blazor, React) share one data
  model; OTel metrics are consistently named across adapters; Bridge and iOS can use the same
  contracts without the UI layer.
**Con:** adds a new foundation package; slightly more scaffolding up front.
**Verdict:** recommended.

### Option C — Extend `foundation-ship-common` (ADR 0077) with Engine Room contracts

Add `IEngineRoomDataProvider` and friends to `foundation-ship-common`.

**Pro:** fewer packages.
**Con:** `foundation-ship-common` is about role/permission topology; mixing in CRDT growth
  metrics and sync daemon health violates single-responsibility. Downstream packages that only
  need the permission model would transitively depend on the CRDT engine observation contracts.
**Verdict:** rejected.

---

## Decision

**Adopt Option B.** Introduce `Sunfish.Foundation.EngineRoom` (new package:
`packages/foundation-engine-room/`) for contracts, and `Sunfish.Blocks.EngineRoom` (new
package: `packages/blocks-engine-room/`) for the UI block.

### §1 Observable data model

All types in `Sunfish.Foundation.EngineRoom`:

```csharp
// Main Propulsion — CRDT sync daemon health
public sealed record SyncDaemonHealth(
    NodaTime.Instant CapturedAt,
    int     ActivePeerCount,
    TimeSpan PeerListAge,             // age of the freshest peer-list snapshot
    long    EventsThroughputPerSecond, // events/s over a trailing 30-second window
    long    GossipCyclesCompleted,    // cumulative since daemon start
    SyncDaemonStatus Status,
    string? StatusDetail
);

public enum SyncDaemonStatus { Healthy, Degraded, Unavailable }

// Electrical — per-document CRDT growth
public sealed record CrdtGrowthMetrics(
    TenantId           TenantId,
    string             DocumentId,
    NodaTime.Instant   CapturedAt,
    long               TotalByteEstimate,
    long               PendingOperationCount,
    bool               CompactionEligible,
    NodaTime.Instant?  OldestEntryTimestamp
);

// Health roll-up (all sub-rooms). Uses a list rather than fixed properties to avoid
// a binary-compat break if a 5th sub-room ships or tenants disable a sub-room.
public sealed record EngineRoomHealthSummary(
    NodaTime.Instant                CapturedAt,
    IReadOnlyList<SubsystemHealth>  Subsystems
);

public sealed record SubsystemHealth(
    EngineRoomSubsystem Subsystem,
    SubsystemStatus     Status,
    string?             StatusDetail
);

public enum EngineRoomSubsystem { MainPropulsion, Electrical, DamageControl, QaWorkshop }
public enum SubsystemStatus { Operational, Warning, Critical, Unknown }
```

Helpers: `EngineRoomHealthSummary.For(EngineRoomSubsystem)` returns
`SubsystemHealth?` by enum lookup (null if subsystem not included).

### §2 Provider and command interfaces

```csharp
// Query shape for GetCrdtGrowthMetricsAsync overload (pagination + filtering)
public sealed record CrdtGrowthQuery(
    bool  OnlyCompactionEligible = false,
    int?  LimitTo               = null,
    NodaTime.Instant? SinceCapturedAt = null
);

// Read-only observability interface — registered per-tenant
public interface IEngineRoomDataProvider
{
    /// <summary>
    /// Returns the current health roll-up for all sub-rooms.
    /// Implementations should complete within 500ms; callers should apply a 1s timeout.
    /// </summary>
    ValueTask<EngineRoomHealthSummary> GetHealthSummaryAsync(
        TenantId tenantId, CancellationToken ct = default);

    /// <summary>Returns the live sync-daemon health for the given tenant's local node.</summary>
    ValueTask<SyncDaemonHealth> GetSyncDaemonHealthAsync(
        TenantId tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns per-document CRDT growth metrics for all documents owned by the tenant.
    /// Use for Electrical sub-room rendering.
    /// </summary>
    IAsyncEnumerable<CrdtGrowthMetrics> GetCrdtGrowthMetricsAsync(
        TenantId tenantId, CancellationToken ct = default);

    /// <summary>Filtered/paged overload for Electrical sub-room operator queries.</summary>
    IAsyncEnumerable<CrdtGrowthMetrics> GetCrdtGrowthMetricsAsync(
        TenantId tenantId, CrdtGrowthQuery query, CancellationToken ct = default);

    /// <summary>
    /// Yields an updated health summary whenever a subsystem status changes AND as a
    /// heartbeat every 30 seconds (configurable default). Callers MUST handle duplicate
    /// (unchanged-status) summaries — heartbeats carry the same status as the prior
    /// emission. Implementations SHOULD push on state-transition; 30s poll is the fallback.
    /// </summary>
    IAsyncEnumerable<EngineRoomHealthSummary> SubscribeHealthAsync(
        TenantId tenantId, CancellationToken ct = default);
}

// Damage Control — destructive operations; requires elevated authority
public interface IEngineRoomCommandService
{
    /// <summary>
    /// Quarantine a CRDT document — suspends sync participation for this document.
    /// Caller MUST have been pre-authorized via IPermissionResolver (ShipAction.QuarantineDocument).
    /// Emits DocumentQuarantineRequested BEFORE the operation; DocumentQuarantined AFTER success.
    /// If authorization is denied, emits DamageControlAuthorizationDenied and throws
    /// EngineRoomUnauthorizedException WITHOUT proceeding.
    /// </summary>
    /// <exception cref="EngineRoomUnauthorizedException">
    /// Thrown if authorizedBy lacks the required authority. Authorization-denial audit event
    /// is emitted before the exception is thrown.
    /// </exception>
    ValueTask<QuarantineResult> QuarantineDocumentAsync(
        TenantId tenantId, string documentId,
        ActorId authorizedBy, string reason, CancellationToken ct = default);

    /// <summary>
    /// Release a quarantined document back into normal sync participation.
    /// Emits DocumentQuarantineReleaseRequested BEFORE; DocumentQuarantineReleased AFTER.
    /// </summary>
    ValueTask<ReleaseResult> ReleaseQuarantineAsync(
        TenantId tenantId, string documentId,
        ActorId authorizedBy, string reason, CancellationToken ct = default);

    /// <summary>
    /// Trigger manual snapshot compaction for a document eligible per CrdtGrowthMetrics.
    /// Emits ManualCompactionInitiated before + ManualCompactionCompleted after.
    /// Throws InvalidOperationException (NOT EngineRoomUnauthorizedException) if
    /// CompactionEligible is false at operation time (role-auth precedes eligibility check).
    /// </summary>
    ValueTask<CompactionResult> CompactDocumentAsync(
        TenantId tenantId, string documentId,
        ActorId authorizedBy, CancellationToken ct = default);
}

public sealed record QuarantineResult(string DocumentId, bool Succeeded, string? ErrorDetail);
public sealed record ReleaseResult(string DocumentId, bool Succeeded, string? ErrorDetail);
public sealed record CompactionResult(string DocumentId, long BytesBefore, long BytesAfter, bool Succeeded);

/// <inheritdoc/>
/// <remarks>
/// Inherits UnauthorizedAccessException so callers do not accidentally swallow
/// authorization failures in retry handlers that catch InvalidOperationException.
/// MUST NOT be caught by routine retry logic.
/// </remarks>
public sealed class EngineRoomUnauthorizedException : UnauthorizedAccessException
{
    public EngineRoomUnauthorizedException(string message) : base(message) { }
}
```

### §3 Audit event types

This ADR introduces **8 new `AuditEventType` static-readonly constants** on
`Sunfish.Kernel.Audit.AuditEventType`:

```csharp
// Quarantine lifecycle — two-phase (requested before op; completed after)
public static readonly AuditEventType DocumentQuarantineRequested    = new("DocumentQuarantineRequested");
public static readonly AuditEventType DocumentQuarantined            = new("DocumentQuarantined");
public static readonly AuditEventType DocumentQuarantineReleaseRequested
                                                                     = new("DocumentQuarantineReleaseRequested");
public static readonly AuditEventType DocumentQuarantineReleased     = new("DocumentQuarantineReleased");

// Compaction lifecycle (two-phase; existing pattern from §3)
public static readonly AuditEventType ManualCompactionInitiated      = new("ManualCompactionInitiated");
public static readonly AuditEventType ManualCompactionCompleted      = new("ManualCompactionCompleted");

// Health degradation + authorization denial
public static readonly AuditEventType EngineRoomHealthDegraded       = new("EngineRoomHealthDegraded");
public static readonly AuditEventType DamageControlAuthorizationDenied
                                                                     = new("DamageControlAuthorizationDenied");
```

**Severity assignments (literal strings in `JsonNode Payload`):**

| AuditEventType | `"severity"` value |
|---|---|
| `DocumentQuarantineRequested` | `"High"` |
| `DocumentQuarantined` | `"High"` |
| `DocumentQuarantineReleaseRequested` | `"High"` |
| `DocumentQuarantineReleased` | `"High"` |
| `ManualCompactionInitiated` | `"Normal"` |
| `ManualCompactionCompleted` | `"Normal"` |
| `EngineRoomHealthDegraded` | `"High"` |
| `DamageControlAuthorizationDenied` | `"High"` |

**`EngineRoomHealthDegraded` cooldown semantics.** Dedup is per
`(TenantId, EngineRoomSubsystem, status_from, status_to)` tuple with a 30-second window.
Transitions with a **different tuple** are NOT suppressed even within the same window.
Example: `Electrical: Operational→Warning` fires; then `MainPropulsion: Operational→Critical`
fires immediately (different subsystem + tuple → no dedup). A second
`Electrical: Operational→Warning` within 30s is suppressed. A recovery
`Electrical: Warning→Operational` is NOT suppressed (different tuple).

#### §3a Audit record construction

All audit events MUST be appended via `IAuditTrail.AppendAsync(AuditRecord, CancellationToken)`.
The `AuditRecord` is constructed per the ADR 0049 pattern: `TenantId`, `OccurredAt`
(`DateTimeOffset`; convert from `NodaTime.Instant` via `.ToDateTimeOffset()`), `EventType`,
and `Payload` as `JsonNode` with canonical-JSON keys per the table below.

**Canonical payload schemas (key names are normative; values are .NET types prior to
JSON serialization; `Instant` values serialize as ISO 8601 strings):**

| EventType | Required payload keys | Value types |
|---|---|---|
| `DocumentQuarantineRequested` | `tenantId`, `documentId`, `authorizedBy`, `reason`, `requestedAt`, `watchId` | `string`, `string`, `string`, `string`, `DateTimeOffset`, `Guid?` |
| `DocumentQuarantined` | `tenantId`, `documentId`, `authorizedBy`, `quarantinedAt` | `string`, `string`, `string`, `DateTimeOffset` |
| `DocumentQuarantineReleaseRequested` | `tenantId`, `documentId`, `authorizedBy`, `reason`, `requestedAt`, `watchId` | `string`, `string`, `string`, `string`, `DateTimeOffset`, `Guid?` |
| `DocumentQuarantineReleased` | `tenantId`, `documentId`, `authorizedBy`, `releasedAt` | `string`, `string`, `string`, `DateTimeOffset` |
| `ManualCompactionInitiated` | `tenantId`, `documentId`, `authorizedBy`, `initiatedAt` | `string`, `string`, `string`, `DateTimeOffset` |
| `ManualCompactionCompleted` | `tenantId`, `documentId`, `authorizedBy`, `completedAt`, `bytesBefore`, `bytesAfter` | `string`, `string`, `string`, `DateTimeOffset`, `long`, `long` |
| `EngineRoomHealthDegraded` | `tenantId`, `subsystem`, `statusFrom`, `statusTo`, `detectedAt` | `string`, `string`, `string`, `string`, `DateTimeOffset` |
| `DamageControlAuthorizationDenied` | `tenantId`, `attemptedAction`, `attemptedBy`, `denialReason`, `deniedAt` | `string`, `string`, `string`, `string`, `DateTimeOffset` |

`watchId` carries the `OodWatch.WatchId.Value` captured at pre-flight (§4.2) — `null` if
no EOOW watch is active (Captain/XO override path). This pin attests which watch authorized
the operation regardless of watch rotation between request and completion.

### §4 Permission model

Engine Room operations compose on ADR 0077's `IPermissionResolver` and ADR 0078's
`IOodWatchService`. The following `ShipAction` values are introduced for Engine Room:

```csharp
// New ShipAction constants (static-readonly in Sunfish.Foundation.Ship.Common)
public static readonly ShipAction ViewEngineRoom        = new("ViewEngineRoom");
public static readonly ShipAction ViewDamageControl     = new("ViewDamageControl");
public static readonly ShipAction QuarantineDocument    = new("QuarantineDocument");
public static readonly ShipAction ReleaseQuarantine     = new("ReleaseQuarantine");
public static readonly ShipAction CompactDocument       = new("CompactDocument");
```

**Resolution rules (role authority only; state-derived eligibility is a separate check):**

| Action | Granted to roles |
|---|---|
| `ViewEngineRoom` | `ShipRole.DivisionOfficer`, `ShipRole.EngineerOfficer`, `ShipRole.XO`, `ShipRole.Captain`, OR current `EOOW` |
| `ViewDamageControl` | `ShipRole.EngineerOfficer`, `ShipRole.XO`, `ShipRole.Captain`, OR current `EOOW` |
| `QuarantineDocument` | (`ShipRole.EngineerOfficer` AND current `EOOW`) OR `ShipRole.Captain` OR `ShipRole.XO` |
| `ReleaseQuarantine` | Same as `QuarantineDocument` |
| `CompactDocument` | `ShipRole.EngineerOfficer` (regardless of watch state) |

**`CompactDocument` state eligibility (separate from role authority).** After `IPermissionResolver`
grants `CompactDocument`, `IEngineRoomCommandService.CompactDocumentAsync` MUST also verify
`CrdtGrowthMetrics.CompactionEligible == true` for the target document at operation time.
If `false`, throw `InvalidOperationException` with message "Document is not eligible for
compaction" — NOT `EngineRoomUnauthorizedException`, since this is a state precondition, not
an authorization failure.

The `EOOW` watch state is checked via
`IOodWatchService.GetActiveWatchAsync(tenantId, OodRole.EngineeringOfficerOfTheWatch)`.
A null return means "no EOOW on watch" — actions requiring EOOW MUST be denied with
`PermissionDecision.Denied(WatchRequired, ...)`.

**`IEngineRoomCommandService` internal pre-flight (all destructive operations):**

1. Call `IPermissionResolver.ResolveAsync(action, actorId, tenantId)`.
2. If `Denied`: emit `DamageControlAuthorizationDenied` audit event (payload per §3a),
   THEN throw `EngineRoomUnauthorizedException`. Authorization-denial events MUST be
   audited — they are security events, not noise.
3. If `Granted`: proceed to §4.2 watch-pin capture, then pre-op audit event, then operation.

#### §4.1 Tenant context binding

Implementations MUST resolve the ambient tenant from `ITenantContext` (or equivalent
foundation-multitenancy seam) and reject the call with `EngineRoomUnauthorizedException`
if the `tenantId` parameter does not match the ambient tenant.
Cross-tenant Damage Control via parameter manipulation is explicitly disallowed.
This check MUST precede the permission-resolver call in §4 pre-flight step 1.

#### §4.2 Watch-pin atomicity

For operations that require EOOW authority (`QuarantineDocument`, `ReleaseQuarantine`):
the command service MUST call `IOodWatchService.GetActiveWatchAsync` once at operation
start and capture the resulting `OodWatch.Id` (as `watchId`). This captured `watchId` MUST
be embedded in the pre-op audit payload (§3a). If the watch rotates between the pin capture
and audit append, the audit record attests to the watch active at pin time — the operation
is not retroactively reassigned to the new watch.

If `GetActiveWatchAsync` returns `null` after `IPermissionResolver` granted the operation
via a non-EOOW authority path (Captain/XO override), `watchId` is `null` in the payload.

#### §4.3 ShipAction registration verification

The 5 `ShipAction` constants in §4 MUST be defined in `Sunfish.Foundation.Ship.Common`'s
static catalog (per ADR 0077 §X) before `blocks-engine-room` ships a binary. A startup
analyzer or DI-validation check MUST verify that all `ShipAction` references in
`foundation-engine-room` resolve to registered actions; unknown actions are treated as
Denied-by-default but MUST additionally log a warning to prevent silent lockout.

### §5 OpenTelemetry metric catalog

Engine Room implementations MUST emit the following OpenTelemetry metric instruments under
the Meter named `"Sunfish.EngineRoom"` and Activity Source named `"Sunfish.EngineRoom"`:

| Instrument | Kind | Unit | Description |
|---|---|---|---|
| `sunfish.engine_room.peer_count` | ObservableGauge | `{peers}` | Active peer count from `SyncDaemonHealth` |
| `sunfish.engine_room.events_throughput` | ObservableGauge | `{events/s}` | Event throughput (30s window) |
| `sunfish.engine_room.gossip_cycles` | ObservableCounter | `{cycles}` | Cumulative gossip cycles completed |
| `sunfish.engine_room.crdt_total_bytes` | ObservableGauge | `By` | Sum of `CrdtGrowthMetrics.TotalByteEstimate` across all documents |
| `sunfish.engine_room.crdt_compaction_eligible` | ObservableGauge | `{documents}` | Count of documents with `CompactionEligible == true` |
| `sunfish.engine_room.subsystem_status` | ObservableGauge | `{status}` | 0=Operational 1=Warning 2=Critical 3=Unknown; tagged by `subsystem` attribute |

The Meter is instantiated in the `IEngineRoomDataProvider` implementation. All instruments
use `TenantId.Value` as the `tenant.id` attribute so multi-tenant deployments (Bridge) can
filter per tenant.

### §6 WCAG 2.2 AA conformance contract

Per W#35 §5.3 Stage 1.5 hardening and the ADR 0077 conformance baseline:

**Log table (Main Propulsion + QA Workshop):**
- MUST be rendered as an accessible data grid with `role="grid"`, `role="columnheader"` per
  column, and `role="row"` per entry
- Virtualized log tables MUST set `aria-rowcount` on the grid container (total rows) and
  `aria-rowindex` on each rendered row (1-based) so screen readers announce row position
  correctly (WCAG SC 4.1.2; ARIA 1.2 §grid)
- MUST also set `aria-colcount` on the grid container and `aria-colindex` on each
  `gridcell`/`columnheader` — required for SR column-position announcements
- Log rows MUST NOT use color alone to convey severity (WCAG SC 1.4.1); severity MUST be
  conveyed via text label AND icon with `aria-label`

**Trace timeline (Main Propulsion):**
- The accessible table alternative MUST be present in the accessibility tree at all times
  using the sr-only/clip technique (NOT gated behind a user-action toggle). The toggle
  controls visual presentation only — the table is always accessible to screen readers.
  Per WCAG SC 1.4.5: the alternative is always available; the toggle does not enable
  assistive-technology access.
- The table alternative presents: timestamp, operation name, duration, status in a data
  grid with row/column headers; `aria-rowcount`/`aria-colcount` as above
- The visual timeline chart is `aria-hidden="true"` for SR users

**Metric charts (Electrical):**
- Every chart MUST have an associated `<table>` data alternative present in the
  accessibility tree at all times via an sr-only/clip technique — specifically, NOT
  `display: none`, `visibility: hidden`, or `aria-hidden` (all of which remove the element
  from the SR tree). WCAG SC 1.1.1
- Chart containers use `role="img"` with `aria-label` describing the metric and current value
- The clip/sr-only pattern: `position: absolute; clip: rect(1px,1px,1px,1px);` or equivalent

**Damage Control elevated dialog:**
- Uses `role="alertdialog"` — not `role="dialog"`. Quarantine and manual compaction are
  high-severity, irreversible, elevated-authority operations requiring user confirmation.
  Per ARIA 1.2 §alertdialog: correct when user response is required AND the dialog conveys
  an important alert. `role="dialog"` is reserved for non-urgent modal interactions.
- `aria-labelledby` references a heading naming the operation and target document
- `aria-describedby` references a consequence summary (≤3 sentences; MUST include the
  document ID + operation effect, e.g., "Document doc-123 will be removed from sync
  participation. All pending operations will be held until released.")
- Initial focus moves to the **Cancel button or dialog heading**, NOT the confirmation button.
  Per WCAG SC 3.3.4 (Error Prevention): destructive actions must require deliberate user
  intent; focus-on-confirm risks a stray Enter confirming an operation.
- Confirmation button MUST be `disabled` for 1-2 seconds after dialog opens (or until focus
  has moved off the heading, whichever comes first) as an anti-stray-keystroke guard (SC 3.3.4)
- Esc and Cancel close without committing; focus returns to the trigger element
- Confirmation button text: `"Confirm Quarantine"` / `"Confirm Release"` / `"Confirm Compaction"`
  (action-specific; never generic "OK")
- Keyboard-operable: Enter activates the focused button; Tab cycles within dialog only
  (focus-trap per ADR 0077 `IFocusTrap` contract)

**Live regions (health status changes):**
- TWO sibling live regions MUST be maintained (a single live region with dynamic politeness
  changes is unreliable across NVDA/JAWS):
  - `<div aria-live="assertive" aria-atomic="true">` for degradations
    (`Operational → Warning` or `Operational → Critical`; `Warning → Critical`)
  - `<div aria-live="polite" aria-atomic="true">` for recoveries
    (`Critical → Warning`; `Warning → Operational`; `Critical → Operational`)
- Each region announces the subsystem name + new status
- Content is cleared after 5 seconds to prevent stale SR focus

#### §6.1 Conformance verification

WCAG conformance per this contract MUST be verified using
`Sunfish.UIAdapters.Blazor.A11y.SunfishA11yAssertions` (ADR 0034 a11y harness) and its
React parity equivalent. Required assertions: log-table grid semantics (rowcount, colcount),
alertdialog focus management, sr-only alternative presence for charts and trace timeline.

### §7 Package layout

```
packages/
  foundation-engine-room/
    Sunfish.Foundation.EngineRoom.csproj
    SyncDaemonHealth.cs
    CrdtGrowthMetrics.cs
    CrdtGrowthQuery.cs
    EngineRoomHealthSummary.cs  (+ SubsystemHealth, SubsystemStatus, EngineRoomSubsystem)
    IEngineRoomDataProvider.cs
    IEngineRoomCommandService.cs
    QuarantineResult.cs
    ReleaseResult.cs
    CompactionResult.cs
    EngineRoomUnauthorizedException.cs
    EngineRoomMetrics.cs       (OTel instrument-name string constants)
    DI/EngineRoomServiceCollectionExtensions.cs

  blocks-engine-room/
    Sunfish.Blocks.EngineRoom.csproj
    MainPropulsion/             (sync daemon health + log viewer)
    Electrical/                 (CRDT growth gauge + compaction list)
    DamageControl/              (quarantine + compaction actions)
    QaWorkshop/                 (council-review surface + test output)
    DI/EngineRoomBlockServiceCollectionExtensions.cs
```

`foundation-engine-room` ProjectReferences:
- `Sunfish.Foundation` (for `TenantId`, `ActorId`)
- `Sunfish.Foundation.Ship.Common` (for `ShipAction` constants; **forward-ref: W#46 build**)
- `Sunfish.Kernel.Audit` (for `AuditEventType`, `IAuditTrail`)

`blocks-engine-room` ProjectReferences:
- `Sunfish.Foundation.EngineRoom`
- `Sunfish.Foundation.Ship.Common` (for `IPermissionResolver`, `PermissionDecision`)
- `Sunfish.Foundation.Wayfinder` (for `IOodWatchService`; **forward-ref: W#49 build**)
- `Sunfish.UICore` (adapter-agnostic primitives)

### §A0 Cited-symbol audit

#### §A0.1 Symbols NOT introduced by this ADR (pre-acceptance verification)

- `Sunfish.Foundation.Assets.Common.TenantId` — verified at
  `packages/foundation/Assets/Common/TenantId.cs` on origin/main
- `Sunfish.Foundation.Assets.Common.ActorId` — verified at
  `packages/foundation/Assets/Common/ActorId.cs` on origin/main
- `Sunfish.Kernel.Crdt.ICrdtEngine` — verified at `packages/kernel-crdt/ICrdtEngine.cs`
  on origin/main (`CreateDocument`, `OpenDocument`, `EngineName`, `EngineVersion`)
- `Sunfish.Kernel.Audit.AuditEventType` — `readonly record struct(string Value)` verified
  at `packages/kernel-audit/` on origin/main
- `Sunfish.Kernel.Audit.IAuditTrail.AppendAsync(AuditRecord, CancellationToken)` — verified
  at `packages/kernel-audit/` on origin/main per ADR 0049
- `NodaTime.Instant` — external dependency; verified in use across foundation packages
- `Microsoft.Extensions.Diagnostics.Metrics.Meter` / `System.Diagnostics.ActivitySource` —
  .NET BCL types; no import drift risk

#### §A0.2 Symbols that are forward-references (introduced by other pending ADRs)

The following types are cited in §4 (Permission model) and §7 (Package layout) but do NOT
yet exist on origin/main. The W#79 Stage 06 build MUST halt until these are available:

- `Sunfish.Foundation.Ship.Common.ShipRole` — introduced by ADR 0077 W#46 Stage 06 build
  (`foundation-ship-common` package). **Halt-condition:** W#46 must complete Phase 1 before
  W#79 Phase 2 (blocks-engine-room DI wiring).
- `Sunfish.Foundation.Ship.Common.IPermissionResolver` — same W#46 dependency
- `Sunfish.Foundation.Ship.Common.ShipAction` — same W#46 dependency; the 5 constants
  introduced by this ADR (`ViewEngineRoom`, `ViewDamageControl`, `QuarantineDocument`,
  `ReleaseQuarantine`, `CompactDocument`) must be registered in the `ShipAction` static
  catalog during W#46 Phase 1 (or a follow-up ADR 0077 amendment per §4.3)
- `Sunfish.Foundation.Wayfinder.IOodWatchService` — introduced by ADR 0078 W#49 Stage 06
  build. **Halt-condition:** W#49 must complete before the Damage Control EOOW-check wiring
  in `blocks-engine-room`
- `Sunfish.Foundation.Wayfinder.StandingOrder` — introduced by ADR 0065 W#42 Stage 06 build.
  Not directly cited in Engine Room contracts but transitively referenced via W#46

#### §A0.3 Structural-citation correctness

- `ICrdtEngine.CreateDocument(string documentId)` / `ICrdtEngine.OpenDocument(string, ReadOnlyMemory<byte>)` — verified against `packages/kernel-crdt/ICrdtEngine.cs` exact signatures ✓
- `AuditEventType` as `readonly record struct(string Value)` — same pattern as
  `StandingOrderIssued` in ADR 0065 ✓
- `IAuditTrail.AppendAsync(AuditRecord record, CancellationToken ct)` — verified ✓

#### §A0.4 Expected forward-reference signatures

For pre-acceptance review of §4 permission semantics, the following signatures are expected
based on ADR 0077 (W#46) and ADR 0078 (W#49). If the final builds diverge from these shapes,
this ADR MUST be amended before W#79 Stage 06 begins:

```csharp
// ADR 0077 — expected signatures in foundation-ship-common
public readonly record struct ShipRole(string Value)
{
    public static readonly ShipRole DivisionOfficer = new("DivisionOfficer");
    public static readonly ShipRole EngineerOfficer = new("EngineerOfficer");
    public static readonly ShipRole XO              = new("XO");
    public static readonly ShipRole Captain         = new("Captain");
    // (additional roles per ADR 0077)
}
public interface IPermissionResolver
{
    ValueTask<PermissionDecision> ResolveAsync(
        ShipAction action, ActorId actorId, TenantId tenantId,
        CancellationToken ct = default);
}

// ADR 0078 — expected signatures in foundation-wayfinder
public interface IOodWatchService
{
    ValueTask<OodWatch?> GetActiveWatchAsync(
        TenantId tenantId, OodRole role, CancellationToken ct = default);
}
public enum OodRole { OfficerOfTheDeck, EngineeringOfficerOfTheWatch }
public readonly record struct OodWatchId(Guid Value);
```

---

## Consequences

### Positive

- Operators have a single-pane-of-glass for CRDT + sync daemon + audit health; incident
  response no longer requires pivoting between per-package consoles
- OTel metric catalog is canonical; Aspire Dashboard, Prometheus, Grafana all consume
  `sunfish.engine_room.*` without Sunfish-specific adapters
- Damage Control destructive actions are audit-by-construction with two-phase events
  (pre-op intent + post-op result); no operation can succeed without an auditable record
- Authorization-denial audit events surface attacker probing at the boundary
- WCAG a11y exceedance over Aspire makes Engine Room contractually usable by screen-reader
  users without workarounds

### Negative

- Two new packages (`foundation-engine-room` + `blocks-engine-room`) increase the package
  graph; both are additive and do not change existing contracts
- `ShipAction.QuarantineDocument` et al. must be registered in `foundation-ship-common`'s
  static catalog — this requires a minor extension to the W#46 build or a post-W#46 amendment;
  see §A0.2 halt-condition
- Two-phase audit for quarantine/release adds 4 AuditEventType constants (vs 2 in a
  single-phase design), but this matches the compaction pattern already in §3 and is
  required for audit-by-construction on destructive ops

### Trust impact / Security & privacy

Damage Control operations (quarantine, compaction) can disrupt CRDT sync participation for
affected tenants. Attacker-controlled quarantine would degrade sync availability.

Mitigations:
1. `IEngineRoomCommandService` pre-flight MUST re-verify `IPermissionResolver` before every
   destructive call — the permission check is not performed once at login
2. Pre-op audit event is emitted before the operation begins; authorization-denial events
   are audited before exceptions are thrown
3. Tenant-context binding (§4.1) prevents cross-tenant parameter manipulation
4. Watch-pin atomicity (§4.2) ensures audit attests to the watch active at decision time
5. Security-engineering subagent review is mandatory for any Phase that ships Damage Control
   command execution

---

## Compatibility plan

**`foundation-engine-room`** — new package; additive; no impact on existing packages

**`kernel-audit`** — 8 new `AuditEventType` constants; additive; existing callers unaffected

**`foundation-ship-common`** — 5 new `ShipAction` static-readonly constants; additive;
**timing constraint:** these constants must be introduced in `foundation-ship-common` before
`blocks-engine-room` can compile. Recommend adding them in the W#46 Stage 06 Phase 1 PR
(foundation-ship-common scaffold) to avoid a separate amendment PR

**All other packages:** no changes required

---

## Implementation checklist

### Phase 1 — foundation-engine-room substrate (~3–4h; 1 PR; `sunfish-feature-change`)

- [ ] Scaffold `packages/foundation-engine-room/Sunfish.Foundation.EngineRoom.csproj`
  with ProjectReferences to `Sunfish.Foundation` + `Sunfish.Kernel.Audit`
  (NOT `Sunfish.Foundation.Ship.Common` yet — forward-ref; add in Phase 2)
- [ ] Add `SyncDaemonHealth`, `SyncDaemonStatus`, `CrdtGrowthMetrics`, `CrdtGrowthQuery`,
  `EngineRoomHealthSummary` (list-based), `SubsystemHealth`, `SubsystemStatus`,
  `EngineRoomSubsystem`
- [ ] Add `QuarantineResult`, `ReleaseResult`, `CompactionResult`,
  `EngineRoomUnauthorizedException` (inherits `UnauthorizedAccessException`)
- [ ] Add `IEngineRoomDataProvider` interface (both `GetCrdtGrowthMetricsAsync` overloads;
  `SubscribeHealthAsync` with heartbeat contract per §2)
- [ ] Add `IEngineRoomCommandService` interface (stubs; no permission check yet)
- [ ] Add `EngineRoomMetrics` static class with OTel instrument-name string constants (§5)
- [ ] Add 8 new `AuditEventType` constants to `packages/kernel-audit/` per §3
- [ ] Add `DI/EngineRoomServiceCollectionExtensions.cs` (registers interfaces; stubs for
  implementations until Phase 2)
- [ ] Add 5 new `ShipAction` constants to `packages/foundation-ship-common/`
  **Halt-condition:** W#46 Phase 1 (foundation-ship-common scaffold) must have landed first;
  if not, stub the ShipAction constants as a Phase 1.5 PR on top of W#46's Phase 1 branch
- [ ] Unit tests (4 minimum): `EngineRoomHealthSummary_ListBased_ForHelper`,
  `SubsystemHealth_Status`, `EngineRoomMetrics_NamesCoverAllInstruments`,
  `EngineRoomUnauthorizedException_IsUnauthorizedAccessException`
- [ ] Pre-merge council (mandatory; security-engineering subagent for §Trust impact)

### Phase 2 — IEngineRoomDataProvider reference impl (~4–5h; 1 PR)

- [ ] `DefaultEngineRoomDataProvider : IEngineRoomDataProvider` in
  `packages/foundation-engine-room/`
  - `GetHealthSummaryAsync`: polls `ICrdtEngine.EngineName`/`EngineVersion` + optional
    `ISyncDaemonHealthSource` (see open question #1); aggregates into `EngineRoomHealthSummary`
  - `GetCrdtGrowthMetricsAsync`: reads from optional `ICrdtDocumentRegistry` seam (Q2);
    respects `CrdtGrowthQuery` filter
  - `SubscribeHealthAsync`: 30s polling loop + transition detection; `EngineRoomHealthDegraded`
    audit event with per-tuple 30s dedup per §3
- [ ] `DefaultEngineRoomCommandService` pre-flight wiring:
  - §4.1 tenant binding check (ambient vs parameter)
  - §4 pre-flight: `IPermissionResolver.ResolveAsync` → if Denied, emit
    `DamageControlAuthorizationDenied` then throw
  - §4.2 watch-pin capture for EOOW-gated operations; embed `watchId` in pre-op audit
  - Pre-op audit event → operation → post-op audit event sequence per §3
  - `CompactDocument` state eligibility check (not a permission failure)
  **Halt-condition:** `IPermissionResolver` and `IOodWatchService` require W#46 + W#49
  Phase 1 on origin/main
- [ ] Add `IEngineRoomMeterRegistration` (OTel instruments from §5); register in DI
- [ ] Unit tests (8 minimum): `GetHealthSummary_Returns_AllSubrooms`,
  `Subscribe_Emits_On_Degradation`, `Subscribe_Suppresses_Duplicate_Tuple_Within_Cooldown`,
  `Subscribe_DoesNotSuppress_DifferentTuple`, `CommandService_ThrowsUnauthorized_WhenPermissionDenied`,
  `CommandService_EmitsAuthDenialAudit_BeforeException`,
  `CommandService_EmitsPreOpAudit_BeforeOperation`,
  `CommandService_ThrowsInvalidOp_WhenCompactionNotEligible`
- [ ] Pre-merge council (mandatory)

### Phase 3 — `blocks-engine-room` UI block (~5–7h; 2 PRs)

**Phase 3a:** MainPropulsion + Electrical sub-rooms (read-only; no Damage Control)
- [ ] `MainPropulsionPanel` — sync daemon health card + log table per §6 a11y contract
  (grid with aria-rowcount, aria-colcount, aria-rowindex, aria-colindex; no color-alone severity)
- [ ] Trace timeline with sr-only table alternative always in SR tree (visual toggle
  controls chart display only; chart is aria-hidden)
- [ ] `ElectricalPanel` — CRDT growth gauge chart + sr-only `<table>` alternative per §6
  (clip technique; NOT display:none); two live regions per §6
- [ ] `EngineRoomHealthBanner` — two sibling live regions (assertive + polite) per §6
- [ ] Add ProjectReference to `Sunfish.Foundation.Wayfinder` (for `IOodWatchService`)
  **Halt-condition:** W#49 (ADR 0078 Stage 06 build) must have Phase 1 landed
- [ ] WCAG/a11y subagent review (mandatory before Phase 3a merge)
- [ ] Pre-merge council (mandatory)

**Phase 3b:** DamageControl + QaWorkshop sub-rooms
- [ ] `DamageControlPanel` — quarantine list + compaction triggers per §6 Damage Control
  dialog contract (`role="alertdialog"`; focus on Cancel; confirm disabled 1-2s; consequence
  summary ≤3 sentences including documentId); `IEngineRoomCommandService` integration
- [ ] `QaWorkshopPanel` — stub placeholder (note: "not yet implemented"); separate intake
  for test-runner + council-review contract (per open question #3)
- [ ] `EngineRoomPermissionGuard` — wrapper that hides Damage Control actions from roles
  without `ViewDamageControl` permission
- [ ] WCAG/a11y subagent review (mandatory; verify alertdialog + focus + a11y harness per §6.1)
- [ ] Security-engineering subagent review (mandatory for Damage Control)
- [ ] Pre-merge council (mandatory)

### Phase 4 — Anchor wiring + apps/docs + changelog (~2h; 1 PR)

- [ ] Register `DefaultEngineRoomDataProvider` in Anchor's `MauiProgram.cs`
- [ ] Register `blocks-engine-room` panels in Anchor's navigation shell
  (navigation target: `ShipLocation.EngineRoom` per ADR 0077)
- [ ] `apps/docs/foundation/engine-room/overview.md` — usage guide
- [ ] XML docs on all public types
- [ ] Changelog entry (user-facing)
- [ ] Update `icm/_state/active-workstreams.md` W#50 row to `built`
- [ ] Standard review

---

## Open questions

1. **`ISyncDaemonHealthSource` seam.** The `DefaultEngineRoomDataProvider` needs to read live
   sync daemon state (peer count, throughput, gossip cycles). The current `ICrdtEngine` API
   (`CreateDocument`, `OpenDocument`, `EngineName`, `EngineVersion`) has no health-query
   surface. Options: (a) introduce `ICrdtEngineHealthSource` in `kernel-crdt` as an optional
   health-observation seam; (b) read from OTel metrics internally (circular); (c) stub with
   static values for Phase 2, defer live data to a kernel-crdt amendment.
   **Recommendation:** option (a) — add `ICrdtEngineHealthSource` to `kernel-crdt` as an
   optional interface; `DefaultEngineRoomDataProvider` resolves it as `IOptional<ICrdtEngineHealthSource>`
   and falls back to `Unknown` status if absent. This defers the kernel-crdt amendment without
   blocking Phase 2.

2. **Document registry access for Electrical metrics.** `IEngineRoomDataProvider.GetCrdtGrowthMetricsAsync`
   needs to enumerate all documents for a tenant and query their growth metrics. There is
   currently no `IDocumentRegistry` interface in `kernel-crdt`. Recommendation: same approach
   as Q1 — introduce `ICrdtDocumentRegistry` as an optional seam; stub with empty enumerable
   if absent.

3. **QA Workshop scope.** The intake describes QA Workshop as test-runner + council-review
   surface. Test-runner integration (xUnit test output in a UI panel) is a novel surface with
   no clear ADR precedent. Phase 3b should stub QA Workshop as a placeholder panel and file a
   separate intake for the test-runner + council-review contract.

---

## Revisit triggers

- **ADR 0004 algorithm-agility refactor** — re-evaluate `AuditRecord` payload format for
  Damage Control events; they are security-relevant long-retained records
- **Bridge multi-tenant deployment** — `IEngineRoomDataProvider` must enumerate documents
  across all tenants; the current `GetCrdtGrowthMetricsAsync(TenantId)` signature assumes a
  per-tenant scope. If Bridge requires a cross-tenant fleet view, add a `GetFleetGrowthMetricsAsync`
  overload
- **OTel semantic conventions evolve** — `sunfish.engine_room.*` metric names should be
  reviewed against the OpenTelemetry Semantic Conventions for Databases when a stable release
  for CRDT-style databases is published

---

## Pre-acceptance audit

- [x] **AHA pass.** Options A (pure block) and C (embed in ship-common) considered and
  rejected. The two-package split (Option B) is the recommended approach for reusability and
  testability.
- [x] **FAILED conditions.** Reverse if: (a) ADR 0077 W#46 build introduces a
  `ShipAction` catalog that is closed (cannot add new actions without an ADR amendment) —
  in that case open an ADR 0077 amendment before Stage 06 begins; (b) `kernel-crdt` cannot
  be extended with `ICrdtEngineHealthSource` without a breaking API change that requires a
  separate versioning window.
- [x] **Rollback strategy.** All new types are in new packages. Rollback = revert the W#50
  Stage 06 build PRs. No existing packages are changed except `kernel-audit` (8 additive
  `AuditEventType` constants — removable without callers being affected at compile time).
- [x] **Confidence level.** MEDIUM. The data model is clean; the OTel metric catalog is
  straightforward. Confidence drops for `ISyncDaemonHealthSource` (Open Q1) and
  `ICrdtDocumentRegistry` (Open Q2) — both are novel seams without existing precedent in
  the codebase. Stage 06 COB should resolve both before Phase 2 begins.
- [x] **Cited-symbol verification.** See §A0. Existing symbols verified. Forward-refs
  explicitly marked with halt-conditions and expected signatures in §A0.4.
- [x] **Anti-pattern scan.** AP-1 (unvalidated assumptions): §A0 and open questions cover.
  AP-21 (cited-symbol drift): §A0 covers. AP-3 (vague success criteria): checklist is
  observable. No critical anti-patterns apply.
- [x] **Revisit triggers.** Named above.
- [x] **Cold Start Test.** A fresh contributor can execute Phases 1-4 from the checklist;
  halt-conditions name the sequencing dependencies on W#46 + W#49.

---

## References

### Predecessor and sister ADRs

- [ADR 0028](./0028-crdt-engine-selection.md) — CRDT engine selection; `ICrdtEngine` is the
  primary observability target for Main Propulsion + Electrical sub-rooms
- [ADR 0049](./0049-audit-trail-substrate.md) — Audit trail substrate; Engine Room Damage
  Control events flow through `IAuditTrail`
- [ADR 0065](./0065-wayfinder-system-and-standing-order-contract.md) — Wayfinder + Standing
  Order; `IOodWatchService` (via ADR 0078) is the EOOW-watch-state authority
- [ADR 0077](./0077-shared-design-system.md) — Shared Design System; `ShipRole`,
  `IPermissionResolver`, `PermissionDecision`, `ShipAction`, design tokens, `IFocusTrap`
- [ADR 0078](./0078-ood-watch-rotation.md) — OOD Watch Rotation; `IOodWatchService`
  provides the EOOW watch-state check for Damage Control authorization

### Roadmap and specifications

- W#35 Ship Architecture discovery §5.3 + §8.4 — `icm/01_discovery/output/2026-05-01_ship-architecture.md`
- Engine Room Observability intake — `icm/00_intake/output/2026-05-01_engine-room-observability-intake.md`
- WCAG 2.2 AA (2018) — SC 1.1.1, 1.3.1, 1.4.1, 1.4.5, 2.2.1, 3.3.4, 4.1.2, 4.1.3 + live-region SC 4.1.3
- ADR 0034 — a11y harness per adapter; `SunfishA11yAssertions`
- .NET Aspire Dashboard — observability reference (W#35 §A.1)
- OpenTelemetry Semantic Conventions — `sunfish.engine_room.*` naming follows OTel conventions

### Existing code / substrates

- `packages/kernel-crdt/ICrdtEngine.cs` — CRDT engine factory (verified on origin/main)
- `packages/foundation/Assets/Common/TenantId.cs` — TenantId value type
- `packages/foundation/Assets/Common/ActorId.cs` — ActorId value type
- `packages/kernel-audit/` — IAuditTrail + AuditEventType + AuditRecord

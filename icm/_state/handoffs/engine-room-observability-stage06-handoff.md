# W#50 Stage 06 Hand-off — Engine Room Observability Surface

**ADR:** [0079 — Engine Room Observability Surface](../../../docs/adrs/0079-engine-room-observability.md)
**Status:** ADR 0079 Accepted 2026-05-05 via PR #572
**Workstream row:** W#50 (`design-in-flight` → flip to `ready-to-build` with this hand-off)
**Assigned to:** sunfish-PM (COB)
**Estimated effort:** ~14–18h / 4 phases / ~5 PRs
**Pipeline variant:** `sunfish-feature-change`

**Hard prerequisites (must be on origin/main before indicated phase):**
- W#46 Phase 1 (`foundation-ship-common` scaffold + `ShipAction` catalog + `IPermissionResolver`) — required before Phase 1 can add `ShipAction` constants; Phase 2 is fully gated
- W#49 Phase 1 (`IOodWatchService` + `OodWatchId`) — required before Phase 2 (EOOW-check wiring in `DefaultEngineRoomCommandService`) and Phase 3a (WatchBanner EOOW status)

**Downstream provides:**
- `IEngineRoomDataProvider.GetHealthSummaryAsync` — consumed by W#51 (Quarterdeck) as `IDepartmentKpiSource` adaptor

---

## Prerequisite verification (run before Phase 1)

```bash
# W#46 Phase 1 landed
ls packages/foundation-ship-common/ 2>/dev/null && echo "PRESENT" || echo "ABSENT — wait for W#46 Phase 1"

# W#49 Phase 1 landed (Phase 2 only)
grep -r "IOodWatchService" packages/foundation-wayfinder/ 2>/dev/null && echo "PRESENT" || echo "ABSENT — Phase 2 gated"

# No foundation-engine-room package yet
ls packages/foundation-engine-room/ 2>/dev/null && echo "EXISTS — check before scaffolding" || echo "OK — net-new"
```

---

## Phase 1 — `foundation-engine-room` substrate

**Effort:** ~3–4h | **PR:** 1 | **Review:** pre-merge council mandatory (security-engineering
subagent for `§Trust` + `IEngineRoomCommandService` permission contract)

**Halt-condition:** W#46 Phase 1 must be on origin/main. If `foundation-ship-common` does not
exist, add a `ShipAction` stub or coordinate with COB queue ordering — do NOT proceed with
Phase 1 if `IPermissionResolver` is absent.

### Deliverables

**New package: `packages/foundation-engine-room/Sunfish.Foundation.EngineRoom.csproj`**

ProjectReferences: `Sunfish.Foundation` + `Sunfish.Kernel.Audit` only.
(NOT `Sunfish.Foundation.Ship.Common` yet — avoids forward-reference until Phase 2.)

**Data model types** (in `Sunfish.Foundation.EngineRoom` namespace):
- `SyncDaemonStatus` enum: `Healthy, Degraded, Unavailable`
- `SyncDaemonHealth(SyncDaemonStatus Status, int PeerCount, double EventsThroughput, long GossipCycles, NodaTime.Instant AsOf)`
- `CrdtGrowthMetrics(string DocumentId, TenantId TenantId, long TotalByteEstimate, int TombstoneCount, bool CompactionEligible, NodaTime.Instant MeasuredAt)`
- `CrdtGrowthQuery(TenantId TenantId, bool? CompactionEligibleOnly, int? PageSize, string? ContinuationToken)` — query shape for GetCrdtGrowthMetricsAsync overload
- `SubsystemStatus` enum: `Operational, Warning, Critical, Unknown`
- `EngineRoomSubsystem` enum: `MainPropulsion, Electrical, DamageControl, QaWorkshop`
- `SubsystemHealth(EngineRoomSubsystem Subsystem, SubsystemStatus Status, string? Message)`
- `EngineRoomHealthSummary(IReadOnlyList<SubsystemHealth> SubsystemHealthList)` + `For(EngineRoomSubsystem)` helper
- `QuarantineResult(string DocumentId, NodaTime.Instant QuarantinedAt)`
- `ReleaseResult(string DocumentId, NodaTime.Instant ReleasedAt)`
- `CompactionResult(string DocumentId, long BytesBefore, long BytesAfter, NodaTime.Instant CompletedAt)`
- `EngineRoomUnauthorizedException(string message) : UnauthorizedAccessException`

**`IEngineRoomDataProvider` interface:**
```csharp
ValueTask<EngineRoomHealthSummary> GetHealthSummaryAsync(TenantId tenantId, CancellationToken ct = default);
ValueTask<SyncDaemonHealth> GetSyncDaemonHealthAsync(TenantId tenantId, CancellationToken ct = default);
IAsyncEnumerable<CrdtGrowthMetrics> GetCrdtGrowthMetricsAsync(TenantId tenantId, CancellationToken ct = default);
IAsyncEnumerable<CrdtGrowthMetrics> GetCrdtGrowthMetricsAsync(TenantId tenantId, CrdtGrowthQuery query, CancellationToken ct = default);
IAsyncEnumerable<EngineRoomHealthSummary> SubscribeHealthAsync(TenantId tenantId, CancellationToken ct = default);
```
Subscription heartbeat contract (from §2): emit one `EngineRoomHealthSummary` immediately on
subscribe, then on each status change, then every `HeartbeatInterval` (default 30s).

**`IEngineRoomCommandService` interface:**
```csharp
/// Caller MUST pre-authorize via IPermissionResolver (ShipAction.QuarantineDocument).
/// Throws EngineRoomUnauthorizedException if not authorized.
ValueTask<QuarantineResult> QuarantineDocumentAsync(string documentId, TenantId tenantId,
    ActorId requestedBy, string reason, CancellationToken ct = default);
ValueTask<ReleaseResult> ReleaseQuarantineAsync(string documentId, TenantId tenantId,
    ActorId requestedBy, string reason, CancellationToken ct = default);
/// Throws InvalidOperationException (NOT EngineRoomUnauthorizedException) if not eligible.
ValueTask<CompactionResult> CompactDocumentAsync(string documentId, TenantId tenantId,
    ActorId requestedBy, CancellationToken ct = default);
```

**`EngineRoomMetrics` static class** — string constants for OTel instruments:
```csharp
public static class EngineRoomMetrics
{
    public const string MeterName            = "Sunfish.EngineRoom";
    public const string ActivitySourceName   = "Sunfish.EngineRoom";
    public const string PeerCount            = "sunfish.engine_room.peer_count";
    public const string EventsThroughput     = "sunfish.engine_room.events_throughput";
    public const string GossipCycles         = "sunfish.engine_room.gossip_cycles";
    public const string CrdtTotalBytes       = "sunfish.engine_room.crdt_total_bytes";
    public const string CrdtCompactionEligible = "sunfish.engine_room.crdt_compaction_eligible";
    public const string SubsystemStatusGauge = "sunfish.engine_room.subsystem_status";
}
```

**8 new `AuditEventType` constants in `packages/kernel-audit/AuditEventType.cs`:**
```csharp
public static readonly AuditEventType DocumentQuarantineRequested         = new("DocumentQuarantineRequested");
public static readonly AuditEventType DocumentQuarantined                 = new("DocumentQuarantined");
public static readonly AuditEventType DocumentQuarantineReleaseRequested  = new("DocumentQuarantineReleaseRequested");
public static readonly AuditEventType DocumentQuarantineReleased          = new("DocumentQuarantineReleased");
public static readonly AuditEventType ManualCompactionInitiated           = new("ManualCompactionInitiated");
public static readonly AuditEventType ManualCompactionCompleted           = new("ManualCompactionCompleted");
public static readonly AuditEventType EngineRoomHealthDegraded            = new("EngineRoomHealthDegraded");
public static readonly AuditEventType DamageControlAuthorizationDenied    = new("DamageControlAuthorizationDenied");
```

**5 new `ShipAction` constants in `packages/foundation-ship-common/`** (requires W#46 Phase 1
to have created the `ShipAction` type):
```csharp
public static readonly ShipAction ViewEngineRoom     = new("ViewEngineRoom");
public static readonly ShipAction ViewDamageControl  = new("ViewDamageControl");
public static readonly ShipAction QuarantineDocument = new("QuarantineDocument");
public static readonly ShipAction ReleaseQuarantine  = new("ReleaseQuarantine");
public static readonly ShipAction CompactDocument    = new("CompactDocument");
```

**`DI/EngineRoomServiceCollectionExtensions.cs`** — `AddSunfishEngineRoom()` extension;
registers `IEngineRoomDataProvider` + `IEngineRoomCommandService` as stub impls in Phase 1;
full impls in Phase 2.

**Unit tests (4 minimum):**
- `EngineRoomHealthSummary_ForHelper_ReturnsCorrectSubsystem`
- `SubsystemHealth_Status_Roundtrip`
- `EngineRoomMetrics_NamesMatchOtelSpec` (assert all constants non-null/non-empty)
- `EngineRoomUnauthorizedException_IsUnauthorizedAccessException`

### Phase 1 acceptance gate
```
PASS: dotnet build packages/foundation-engine-room/ packages/kernel-audit/ packages/foundation-ship-common/ -c Release
PASS: dotnet test packages/foundation-engine-room/ -c Release (4 new tests)
PASS: pre-merge council (security-engineering subagent required for EngineRoomCommandService permission contract)
FAIL: foundation-ship-common ShipAction constants missing (W#46 Phase 1 not landed)
FAIL: any type shape deviating from §1/§2 of ADR 0079
```

---

## Phase 2 — Reference implementations + OTel

**Effort:** ~4–5h | **PR:** 1 | **Review:** pre-merge council mandatory (security-engineering
subagent re-review for `DefaultEngineRoomCommandService` auth + watch-pin binding)

**Halt-condition:** W#49 Phase 1 (`IOodWatchService`) must be on origin/main before the EOOW
check in `DefaultEngineRoomCommandService` can be wired. If W#49 is not yet built:
- Implement `DefaultEngineRoomDataProvider` only (no EOOW dependency)
- Leave `DefaultEngineRoomCommandService` as a `// TODO(W#49-P1): wire EOOW check` stub
- Ship Phase 2a and gate Phase 2b on W#49 Phase 1 landing

### Deliverables

**`DefaultEngineRoomDataProvider : IEngineRoomDataProvider`:**
- `GetHealthSummaryAsync` — aggregates `SubsystemHealth` list from available sources; returns
  `EngineRoomHealthSummary` with all four subsystems
- `GetSyncDaemonHealthAsync` — reads from optional `ISyncDaemonHealthSource` injection; returns
  `SyncDaemonHealth` with reasonable defaults when source not registered
- `GetCrdtGrowthMetricsAsync` (both overloads) — reads from optional `ICrdtDocumentRegistry`;
  applies `CrdtGrowthQuery` filter
- `SubscribeHealthAsync` — 30s polling loop; emits on status change; emits
  `EngineRoomHealthDegraded` audit event per-transition with dedup: per
  `(TenantId, EngineRoomSubsystem, status_from, status_to)` tuple with 30s cooldown window;
  different tuples fire independently even within the same 30s window
- OTel instruments wired per §5 `EngineRoomMetrics` constants; Meter registered in DI

**`DefaultEngineRoomCommandService : IEngineRoomCommandService`:**

Authorization pre-flight per §4 (requires W#46 + W#49):
```
1. Tenant binding: verify ambient TenantId matches parameter TenantId
2. IPermissionResolver.ResolveAsync(actor, shipAction, tenantId) → if Denied:
   emit DamageControlAuthorizationDenied audit; throw EngineRoomUnauthorizedException
3. For EOOW-gated actions (QuarantineDocument, ReleaseQuarantine):
   IOodWatchService.GetActiveWatchAsync(tenantId, OodRole.EngineeringOfficerOfTheWatch)
   → embed watchId in pre-op audit payload; null watchId → include in audit but do NOT block
4. Pre-op audit event (RequestedAt + requestedBy + documentId)
5. Execute operation
6. Post-op audit event (CompletedAt + result)
```

For `CompactDocumentAsync`: after permission grant, verify `CrdtGrowthMetrics.CompactionEligible == true`
for the target document; throw `InvalidOperationException("Document is not eligible for compaction")`
if false (NOT `EngineRoomUnauthorizedException` — this is a state precondition, not auth failure).

**Unit tests (8 minimum):**
- `GetHealthSummary_Returns_AllFourSubrooms`
- `Subscribe_Emits_On_Degradation`
- `Subscribe_Suppresses_Duplicate_Tuple_Within_Cooldown` (same tuple within 30s: 1 emission)
- `Subscribe_DoesNotSuppress_DifferentTuple` (different tuple within 30s: fires independently)
- `CommandService_ThrowsUnauthorized_WhenPermissionDenied`
- `CommandService_EmitsAuthDenialAudit_BeforeException`
- `CommandService_EmitsPreOpAudit_BeforeOperation`
- `CommandService_ThrowsInvalidOp_WhenCompactionNotEligible`

### Phase 2 acceptance gate
```
PASS: dotnet test packages/foundation-engine-room/ -c Release (8 new + all Phase 1 tests)
PASS: pre-merge council (security-engineering subagent; verify EOOW wiring or stub comment)
FAIL: DamageControlAuthorizationDenied not emitted before exception
FAIL: dedup suppressing different tuples (incorrect — different tuples must not suppress)
```

---

## Phase 3a — `blocks-engine-room` read-only panels

**Effort:** ~2–3h | **PR:** 1 | **Review:** WCAG/a11y subagent mandatory + pre-merge council

**Halt-condition:** W#49 Phase 1 must be on origin/main (WatchBanner needs `IOodWatchService`).
W#46 Phase 3 must be on origin/main (`ILiveAnnouncer` + `IFocusTrap` required for live regions).

### Deliverables

**New package: `packages/blocks-engine-room/Sunfish.Blocks.EngineRoom.csproj`**

ProjectReferences: `foundation-engine-room` + `foundation-wayfinder` + `foundation-ship-common`
+ `ui-core` (or relevant UI adapter).

**`EngineRoomHealthBanner` component:**
- Two sibling `aria-live` regions per §6: assertive (status degradation) + polite (heartbeat updates)
- Severity conveyed via text label AND icon (`aria-label`); NOT color alone (WCAG SC 1.4.1)
- Consumes `IOodWatchService.GetActiveWatchAsync(OodRole.EngineeringOfficerOfTheWatch)` for
  EOOW badge; null watch shows "No EOOW on watch" text

**`MainPropulsionPanel` component:**
- Accessible data grid: `role="grid"` + `aria-rowcount` + `aria-colcount` on container;
  `aria-rowindex` (1-based) + `aria-colindex` on each row/cell per §6
- Virtualized rows MUST set `aria-rowcount` to total count, not rendered count
- Trace timeline: sr-only `<table>` alternative always present in SR tree (clip technique);
  visual chart toggle controls only the chart visibility (chart is `aria-hidden` when toggled off)
- Log severity: text label + icon with `aria-label` per role

**`ElectricalPanel` component:**
- CRDT growth gauge chart + sr-only `<table>` alternative (clip technique; NOT `display:none`)
- Two sibling `aria-live` regions per §6

### Phase 3a acceptance gate
```
PASS: WCAG/a11y subagent returns APPROVED or NEEDS-AMENDMENT-MECHANICAL-ONLY
PASS: pre-merge council
PASS: dotnet build packages/blocks-engine-room/ -c Release
FAIL: sr-only table behind a user-toggle (must be always in SR tree)
FAIL: color used alone for severity (must have text + aria-label icon)
FAIL: aria-rowcount missing on virtualized grids
```

---

## Phase 3b — `DamageControlPanel` + `QaWorkshopPanel`

**Effort:** ~3–4h | **PR:** 1 | **Review:** WCAG/a11y subagent + security-engineering subagent +
pre-merge council (all three mandatory — this phase ships the Damage Control command surface)

### Deliverables

**`DamageControlPanel` component:**
- Quarantine list + compaction triggers
- Confirmation dialog per §6 Damage Control dialog contract:
  - `role="alertdialog"` (destructive confirmation warrants immediate SR announcement)
  - `aria-modal="true"`
  - Initial focus on Cancel button (NOT Confirm — Confirm starts disabled)
  - Confirm button disabled on dialog open; enabled after 1–2s deliberation pause
    (WCAG SC 3.3.7 Error Prevention)
  - Polite live region announces "Confirm available" at deliberation-pause t (SC 3.3.7 pattern)
  - Consequence summary ≤3 sentences including documentId in dialog body
  - `aria-labelledby` references dialog heading; `aria-describedby` references consequence summary
  - Esc + Cancel both close without action; focus returns to trigger element
- `IEngineRoomCommandService.QuarantineDocumentAsync` / `ReleaseQuarantineAsync` / `CompactDocumentAsync` integration
- `EngineRoomPermissionGuard` wrapper: hides Damage Control actions from roles without
  `ShipAction.ViewDamageControl` permission

**`QaWorkshopPanel` component:**
- Stub placeholder only in v1: visible text "QA Workshop: not yet implemented"
- Separate intake required for test-runner + council-review contract (per ADR 0079 open question #3)

### Phase 3b acceptance gate
```
PASS: WCAG/a11y subagent APPROVED (alertdialog pattern; focus management; deliberation-pause)
PASS: security-engineering subagent APPROVED (permission guard; command service wiring)
PASS: pre-merge council
FAIL: Confirm button not disabled on dialog open
FAIL: role="dialog" instead of role="alertdialog" (this IS a destructive confirmation)
FAIL: initial focus on Confirm (must be Cancel or dialog container)
```

---

## Phase 4 — Anchor wiring + apps/docs + ledger flip

**Effort:** ~2h | **PR:** 1 | **Review:** standard (no council for docs-only)

### Deliverables

- Register `DefaultEngineRoomDataProvider` + `DefaultEngineRoomCommandService` in
  Anchor's `MauiProgram.cs`
- Register `blocks-engine-room` panels in Anchor's navigation shell;
  navigation target: `ShipLocation.EngineRoom` per ADR 0077
- `apps/docs/foundation/engine-room/overview.md` — usage guide covering:
  - `IEngineRoomDataProvider` subscription pattern
  - Authorization pre-flight sequence (§4)
  - EOOW watch-pin semantics
  - OTel metric catalog
  - WCAG live-region + alertdialog patterns
- XML docs on all new public API members
- Changelog entry (user-facing description)
- Update W#50 ledger row to `built`

### Phase 4 acceptance gate
```
PASS: apps/docs/foundation/engine-room/overview.md exists
PASS: XML docs present on all new public API members
PASS: W#50 ledger row says built
```

---

## Cross-phase halt-conditions

| # | Condition | Action |
|---|---|---|
| H1 | W#46 Phase 1 (`foundation-ship-common`) not on origin/main | Phase 1 ShipAction constants blocked; DI wiring blocked; STOP Phase 1 until W#46 ships |
| H2 | W#46 Phase 3 (`ILiveAnnouncer`) not on origin/main | Phase 3a live-region wiring blocked; STOP Phase 3a |
| H3 | W#49 Phase 1 (`IOodWatchService`) not on origin/main | Phase 2 EOOW check + Phase 3a WatchBanner blocked; stub with comment |
| H4 | `DefaultEngineRoomCommandService` emits post-op audit BEFORE pre-op | Sequence MUST be: pre-op → operation → post-op |
| H5 | Pre-merge council returns NEEDS-AMENDMENT with non-mechanical findings | Apply; re-run council; do NOT enable auto-merge until APPROVED |
| H6 | QaWorkshopPanel attempts to implement beyond stub | Out of scope for v1 — QaWorkshop requires a separate intake |

---

## Cohort precedents

- **Two-phase audit pattern** (pre-op / post-op): W#45 Crew Comms `SessionInitiator` established this for security-critical operations; same pattern applies here.
- **Permission pre-check before command**: W#42 `DefaultPermissionResolver` + W#45 channel capability checks — always resolve permission then act; never act first.
- **WCAG alertdialog + deliberation-pause**: ADR 0081 §7 confirmation dialog; ADR 0080 §7 (Quarterdeck). Pattern: `role="alertdialog"` + focus on Cancel + disable Confirm on open + polite "Confirm available" at t=2000ms.
- **sr-only table always in SR tree**: established in this ADR (§6); NOT `display:none` or `visibility:hidden`.
- **Pre-merge council before auto-merge**: mandatory; cohort batting average 29-of-30.

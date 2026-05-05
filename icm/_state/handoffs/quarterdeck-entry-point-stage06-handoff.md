# W#51 Stage 06 Hand-off — Quarterdeck Entry-Point Surface

**ADR:** [0080 — Quarterdeck Entry-Point Surface](../../../docs/adrs/0080-quarterdeck-entry-point.md)
**Status:** ADR 0080 Accepted 2026-05-05 via PR #574
**Workstream row:** W#51 (`design-in-flight` → flip to `ready-to-build` with this hand-off)
**Assigned to:** sunfish-PM (COB)
**Estimated effort:** ~14–20h / 4 phases / ~5 PRs
**Pipeline variant:** `sunfish-feature-change`

**Hard prerequisites (must be on origin/main before indicated phase):**
- W#46 Phase 1 (`foundation-ship-common`: `ShipAction`, `IPermissionResolver`, `ShipRole`) — required before Phase 1 ShipAction constants and Phase 2 permission wiring
- W#49 Phase 1 (`IOodWatchService`) — required before Phase 2 (OodWatch snapshot) and Phase 3a (WatchStatusPanel)
- W#46 Phase 3 (`ILiveAnnouncer`, `IFocusTrap`, `ISearchAsYouType` from `Sunfish.UICore`) — required before Phase 3a (alert ticker + search)

**Downstream provides:**
- `QuarterdeckSnapshot` + `IQuarterdeckDataProvider` — the entry point every user session begins with; feeds Tactical (`LookoutQuarterdeckAlertSource` per ADR 0081), Engine Room KPI card, and future department surfaces

---

## Prerequisite verification (run before Phase 1)

```bash
# W#46 Phase 1
ls packages/foundation-ship-common/ 2>/dev/null && echo "PRESENT" || echo "ABSENT — wait for W#46 Phase 1"

# W#49 Phase 1 (Phase 2+)
grep -rn "IOodWatchService" packages/foundation-wayfinder/ 2>/dev/null && echo "PRESENT" || echo "ABSENT — Phase 2/3a gated"

# No foundation-quarterdeck yet
ls packages/foundation-quarterdeck/ 2>/dev/null && echo "EXISTS — check before scaffolding" || echo "OK — net-new"
```

---

## Phase 1 — `foundation-quarterdeck` substrate

**Effort:** ~3–4h | **PR:** 1 | **Review:** pre-merge council mandatory + WCAG/a11y subagent
(§6 a11y contract is a design-time contract; catch issues before Phase 3)

**Halt-condition:** W#46 Phase 1 must be on origin/main before ShipAction constants can be added
to `foundation-ship-common`.

### Deliverables

**New package: `packages/foundation-quarterdeck/Sunfish.Foundation.Quarterdeck.csproj`**

ProjectReferences: `Sunfish.Foundation` + `Sunfish.Kernel.Audit` + `Sunfish.Foundation.Capabilities`.
Add `Sunfish.Foundation.Ship.Common` reference after W#46 Phase 1 lands.

**Data model types** (in `Sunfish.Foundation.Quarterdeck` namespace):

Core snapshot type:
```csharp
public sealed record QuarterdeckSnapshot(
    OodWatchSummary OodWatch,
    MissionEnvelopeSummary MissionEnvelope,
    IReadOnlyList<StandingOrderSummary> RecentOrders,
    IReadOnlyList<QuarterdeckAlert> PendingAlerts,
    IReadOnlyList<DepartmentKpi> KpiCards,
    IReadOnlyList<DepartmentLink> DepartmentLinks,
    DateTimeOffset SnapshotAt);
```

Supporting types:
- `AlertVisibilityPolicy` enum: `ShowAll, OmitForDeniedActors` (governs ticker disclosure per §2.3 rule 5)
- `AlertSeverity` enum: `Emergency, High, Normal, Informational`
- `QuarterdeckAlert(string AlertId, TenantId TenantId, AlertSeverity Severity, string Title, string? Summary, DateTimeOffset IssuedAt, DateTimeOffset? ExpiresAt, bool RequiresAcknowledgement, bool IsAcknowledged, string? AcknowledgedBy, DateTimeOffset? AcknowledgedAt, string SourceName)`
- `DepartmentLink(ShipLocation Location, string DisplayName, DepartmentStatus AccessDecision, string? DenialReason)` — Denied departments render visibly with denial reason (never hidden)
- `DepartmentStatus` enum: `Accessible, Denied, Unknown`
- `OodRoleSummary(OodRole Role, string? CurrentActorDisplayName, DateTimeOffset? WatchStartedAt, bool IsExpired)`
- `OodWatchSummary(OodRoleSummary OfficerOfTheDeck, OodRoleSummary EngineeringOfficerOfTheWatch)`
- `StandingOrderSummary(StandingOrderId Id, string Path, DateTimeOffset IssuedAt, string IssuedByDisplayName)`
- `MissionEnvelopeSummary(MissionEnvelopeStatus Status, string? VersionLabel, DateTimeOffset? LastEvaluatedAt)`
- `MissionEnvelopeStatus` enum: `Unknown, Passed, Warning, Failed`
- `DepartmentKpi(string SourceName, string Label, string Value, string? Unit, DepartmentStatus Status)`
- `QuarterdeckOptions(TimeSpan HeartbeatInterval, TimeSpan ProviderTimeout, TimeSpan PerSourceTimeout)` + `static Default` singleton (HeartbeatInterval=30s, ProviderTimeout=10s, PerSourceTimeout=5s)

**`IQuarterdeckDataProvider` interface:**
```csharp
ValueTask<QuarterdeckSnapshot> GetSnapshotAsync(TenantId tenantId, ActorId actor, CancellationToken ct = default);
IAsyncEnumerable<QuarterdeckSnapshot> SubscribeSnapshotAsync(TenantId tenantId, ActorId actor, CancellationToken ct = default);
```
Subscription: 30s heartbeat + push on state change; re-resolve permissions on every emit.

**`IQuarterdeckAlertSource` interface:**
```csharp
string SourceName { get; }  // registered-prefix requirement per §5.3
IAsyncEnumerable<QuarterdeckAlert> GetAlertsAsync(TenantId tenantId, ActorId actor, CancellationToken ct = default);
```

**`IDepartmentKpiSource` interface:**
```csharp
string SourceName { get; }
IAsyncEnumerable<DepartmentKpi> GetKpisAsync(TenantId tenantId, CancellationToken ct = default);
```

**`IQuarterdeckCommandService` interface:**
```csharp
/// Two-phase audit: emit AlertAcknowledgementRequested (pre-op), then AlertAcknowledged
/// (post-op success) or AlertAcknowledgementFailed (post-op failure).
ValueTask<bool> AcknowledgeAlertAsync(
    string alertId, TenantId tenantId, ActorId actor, CancellationToken ct = default);
```

**3 new `ShipAction` constants in `foundation-ship-common/`** (requires W#46 Phase 1):
```csharp
public static readonly ShipAction ViewQuarterdeck       = new("ViewQuarterdeck");
public static readonly ShipAction ViewQuarterdeckAlerts = new("ViewQuarterdeckAlerts");
public static readonly ShipAction AcknowledgeAlert      = new("AcknowledgeAlert");
```

**3 new `AuditEventType` constants in `kernel-audit/`:**
```csharp
public static readonly AuditEventType WatchHandoverRequested        = new("WatchHandoverRequested");
public static readonly AuditEventType AlertAcknowledgementRequested = new("AlertAcknowledgementRequested");
public static readonly AuditEventType AlertAcknowledged             = new("AlertAcknowledged");
```
Note: `AlertAcknowledgementFailed` is NOT a separate constant — absence of `AlertAcknowledged`
after `AlertAcknowledgementRequested` in the audit trail is the failure signal.

**`DI/QuarterdeckServiceCollectionExtensions.cs`** — `AddSunfishQuarterdeck()` extension with:
- §5.1 startup `ShipAction` registration check (mirrors ADR 0079 §4.3 pattern)
- §5.3 source uniqueness check on `IQuarterdeckAlertSource.SourceName` at startup (registered-prefix
  `"sunfish.*"` reserved for first-party sources)
- Registers `IQuarterdeckDataProvider` + `IQuarterdeckCommandService` + `IEnumerable<IQuarterdeckAlertSource>` + `IEnumerable<IDepartmentKpiSource>`

**Unit tests (6 minimum):**
- `QuarterdeckSnapshot_DeniedDepartment_StatusIsDenied_NotHidden`
- `DepartmentLink_DeniedAccessDecision_DisplayNameAndDenialReasonPreserved`
- `OodWatchSummary_IsCurrentActor_MatchesActorId`
- `AlertSeverity_EnumValues_Orderable`
- `PendingAlerts_OrderedBySeverityThenIssuedAt`
- `PendingAlerts_OmitsExpiredAlerts_UnlessAcknowledgementRequired`

### Phase 1 acceptance gate
```
PASS: dotnet build packages/foundation-quarterdeck/ packages/kernel-audit/ packages/foundation-ship-common/ -c Release
PASS: dotnet test packages/foundation-quarterdeck/ -c Release (6 new tests)
PASS: pre-merge council + WCAG/a11y subagent (verify §6 a11y contract is implementable)
FAIL: DepartmentLink hides denied departments (MUST show with denial reason per §2.3)
FAIL: ShipAction.TransferWatch or ShipAction.StandWatch listed as new constants (they are from ADR 0077)
```

---

## Phase 2 — `DefaultQuarterdeckDataProvider` + security wiring

**Effort:** ~4–5h | **PR:** 1 | **Review:** pre-merge council mandatory + security-engineering
subagent (§5.2 tenant binding + §5.3 SourceName uniqueness + permission pre-resolution)

**Halt-conditions:** W#46 Phase 1 (IPermissionResolver) AND W#49 Phase 1 (IOodWatchService)
must be on origin/main before Phase 2 can fully ship.

### Deliverables

**`DefaultQuarterdeckDataProvider : IQuarterdeckDataProvider`:**

`GetSnapshotAsync` aggregation per §2.3:
1. Pre-resolve `IPermissionResolver` for all known `ShipLocation` values
   (tenant-scoped permission cache; tenant binding per §5.2: verify actor's TenantId matches
   parameter TenantId before any resolution)
2. Read `IOodWatchService.GetActiveWatchAsync` for both `OodRole` values via `Task.WhenAll`;
   map to `OodWatchSummary` — null watch → `IsExpired: false, CurrentActorDisplayName: null`
3. Read `IStandingOrderRepository.EnumerateAsync` + client-side filter to last 5 Standing Orders
4. Aggregate `IEnumerable<IQuarterdeckAlertSource>.GetAlertsAsync` with `Task.WhenAll` per
   §2.3 rule 7; apply `AlertVisibilityPolicy` to each source:
   - `AlertVisibilityPolicy.OmitForDeniedActors` (default): omit alerts from sources the actor
     cannot see (deny → empty list, NOT null)
   - `AlertVisibilityPolicy.ShowAll`: show all regardless
5. Read `IMissionEnvelopeProvider`; map to `MissionEnvelopeSummary`
6. Aggregate `IEnumerable<IDepartmentKpiSource>.GetKpisAsync` via `Task.WhenAll`
7. `SnapshotAt = DateTimeOffset.UtcNow` (caller's wall clock; not NodaTime.Instant for consistency
   with `StandingOrder.IssuedAt` DateTimeOffset convention)

**`SubscribeSnapshotAsync`** — 30s heartbeat using `QuarterdeckOptions.HeartbeatInterval`;
uses `Channel<QuarterdeckSnapshot>(1)` with `BoundedChannelFullMode.DropOldest`; re-resolves
permissions on every emit (not cached across emit calls — required by §2.1).

**`DefaultQuarterdeckCommandService : IQuarterdeckCommandService`:**

`AcknowledgeAlertAsync` pre-flight per §5:
1. Tenant binding: verify actor TenantId matches parameter TenantId
2. `IPermissionResolver.ResolveAsync(actor, ..., ShipAction.AcknowledgeAlert, alertId)` →
   Denied: emit `AlertAcknowledgementRequested` (intent) + surface via First-Aid; return false
3. Granted: emit `AlertAcknowledgementRequested` (pre-op)
4. Call alert source's acknowledge path; emit `AlertAcknowledged` (post-op)

**§5.2 Anti-spoofing:** the cache key for permission resolution MUST include `TenantId`;
cross-tenant cache pollution is forbidden.

**Unit tests (6 minimum):**
- `GetSnapshot_AllDepartmentsPresent_WithAccessDecision`
- `GetSnapshot_DeniedDepartment_SurfacesDenialReason_NotHidden`
- `GetSnapshot_NoActiveWatch_ReturnsNullSummary`
- `GetSnapshot_AlertsOmittedForDeniedActor_WhenPolicyIsOmit`
- `Subscribe_Heartbeat_EmittedEvery30s`
- `AcknowledgeAlert_EmitsPreAndPostAuditEvents`

### Phase 2 acceptance gate
```
PASS: dotnet test packages/foundation-quarterdeck/ -c Release (all Phase 1 + Phase 2 tests)
PASS: pre-merge council + security-engineering subagent (tenant binding + anti-spoofing)
FAIL: permission pre-resolution result cached across tenants
FAIL: denied departments hidden from snapshot (MUST show with DepartmentStatus.Denied + DenialReason)
FAIL: AlertAcknowledgementFailed introduced as a new AuditEventType constant (it is NOT; see Phase 1 note)
```

---

## Phase 3a — `blocks-quarterdeck` top-deck panels (read-only)

**Effort:** ~2–3h | **PR:** 1 | **Review:** WCAG/a11y subagent mandatory + pre-merge council

**Halt-conditions:**
- W#49 Phase 1 on origin/main (`IOodWatchService` for `WatchStatusPanel`)
- W#46 Phase 3 on origin/main (`ILiveAnnouncer` + `IFocusTrap` + `ISearchAsYouType`)

### Deliverables

**New package: `packages/blocks-quarterdeck/Sunfish.Blocks.Quarterdeck.csproj`**

ProjectReferences: `foundation-quarterdeck` + `foundation-wayfinder` + `foundation-ship-common` + `ui-core`.

**`WatchStatusPanel` component per §3 + §6:**
- `role="region" aria-label="Watch status"` (NOT `role="banner"` — ARIA 1.2: banner is reserved for page-level header per site origin)
- `aria-live="polite"` for watch updates; watch handover uses `role="alertdialog"` dialog
- Watch-handover confirmation dialog:
  - `role="dialog"` with `aria-labelledby` → heading with incoming actor name ("Hand watch to {Name}")
  - `aria-describedby` → watch-summary text (current role, expected duration)
  - Focus moves to primary confirmation button on dialog open
  - Esc + Cancel close without committing; focus returns to "Begin Handover" trigger
  - Confirm button text: "Confirm Handover" (action-specific, NOT "OK")
  - Pre-op: emit `WatchHandoverRequested`; delegates to `IOodWatchService.HandoverWatchAsync`
- Handover completion announcement: polite `aria-live` "Watch relieved. {Role} is now {Name}."
  (polite — planned, non-urgent; assertive would interrupt SR)

**`AlertTickerPanel` component per §4 + §6:**
- Two sibling `aria-live` regions: `assertive` (high-priority alerts) + `polite` (normal)
- BOTH regions: `aria-atomic="false" aria-relevant="additions"` (NOT `aria-atomic="true"`)
- Pause button: `aria-pressed` attribute-only pattern; no native `disabled`
- `prefers-reduced-motion`: ticker defaults to paused state
- Assertive region is ALWAYS active even when ticker is "paused" (pause stops visual animation
  only; urgent alerts still fire assertive for SR)

**`KpiCardGrid` component:**
- `<ul role="list">` + per-card `<li role="listitem">` + accessible names
- Explicit `role="list"` required: WebKit removes list semantics on `list-style: none` without it
- Each KPI card: `<h3>` + value + unit + status badge with text label (NOT color-only)

### Phase 3a acceptance gate
```
PASS: WCAG/a11y subagent APPROVED
PASS: pre-merge council
PASS: dotnet build packages/blocks-quarterdeck/ -c Release
FAIL: role="banner" on watch status region (must be role="region" with aria-label)
FAIL: aria-atomic="true" on ticker live region (must be aria-atomic="false" aria-relevant="additions")
FAIL: assertive region silenced when ticker paused (must always be active)
```

---

## Phase 3b — Main-deck panels (navigation + search + orders + envelope)

**Effort:** ~2–4h | **PR:** 1 | **Review:** WCAG/a11y subagent mandatory + pre-merge council

### Deliverables

**`DepartmentNavPanel` component:**
- `<nav aria-label="Departments">` landmark
- Accessible departments: `<a>` links with permission-resolved `ShipLocation` hrefs
- Denied departments: `<button aria-disabled="true">` with visible denial reason text;
  click + keyboard activation suppressed per §6 (`aria-disabled` NOT native `disabled` —
  keeps element focusable for SR discovery)
- `<main id="main-content" tabindex="-1">` as main landmark; skip-link `<a href="#main-content">`
  as first focusable element on page

**`SearchPanel` component (ISearchAsYouType from W#46 Phase 3):**
- `role="combobox"` + `aria-expanded` + `aria-controls` per ARIA 1.2 combobox pattern
- `aria-activedescendant` pointing to option ID: `quarterdeck-search-result-{stableKey}` convention
- Sr-only sibling region announces result count on update (polite)

**`RecentOrdersPanel` component:**
- Last 5 `StandingOrderSummary` items; link to Wayfinder for detail view
- Accessible list: `<ol>` with date + path + issuer

**`MissionEnvelopePanel` component:**
- Status badge from `MissionEnvelopeSummary.Status`; text label NOT color-only
- Link to mission-space details page when Status != Unknown

### Phase 3b acceptance gate
```
PASS: WCAG/a11y subagent APPROVED (aria-disabled on denied departments; skip-link present)
PASS: pre-merge council
FAIL: denied departments using native disabled attribute (must use aria-disabled)
FAIL: skip-link absent or not first focusable element
FAIL: aria-activedescendant IDs not using quarterdeck-search-result-{key} convention
```

---

## Phase 4 — Anchor wiring + apps/docs + ledger flip

**Effort:** ~2h | **PR:** 1 | **Review:** standard (no council required)

### Deliverables

- Register `DefaultQuarterdeckDataProvider` + `DefaultQuarterdeckCommandService` in Anchor's `MauiProgram.cs`
- Register `blocks-quarterdeck` panels in Anchor's root navigation shell
  (root entry point; `ShipLocation.Quarterdeck`)
- `apps/docs/foundation/quarterdeck/overview.md` — usage guide covering:
  - `IQuarterdeckDataProvider` snapshot + subscription patterns
  - Alert visibility policy configuration
  - `IQuarterdeckAlertSource` + `IDepartmentKpiSource` registration (SourceName uniqueness)
  - WCAG live-region + alertdialog + combobox contracts
  - Denied-department rendering policy (never hide)
  - §5.2 tenant binding requirement
- XML docs on all new public API members
- Changelog entry (user-facing: "Added Quarterdeck entry-point — `IQuarterdeckDataProvider` + `blocks-quarterdeck` panels; permission-gated department links; OOD watch banner; alert ticker")
- Update W#51 ledger row to `built`

### Phase 4 acceptance gate
```
PASS: apps/docs/foundation/quarterdeck/overview.md exists
PASS: W#51 ledger row says built
```

---

## Cross-phase halt-conditions

| # | Condition | Action |
|---|---|---|
| H1 | W#46 Phase 1 (`foundation-ship-common`) not on origin/main | Phase 1 ShipAction constants blocked; Phase 2 permission wiring blocked |
| H2 | W#49 Phase 1 (`IOodWatchService`) not on origin/main | Phase 2 OodWatch wiring blocked; Phase 3a WatchStatusPanel gated |
| H3 | W#46 Phase 3 (`ILiveAnnouncer`, `IFocusTrap`, `ISearchAsYouType`) not on origin/main | Phase 3a alert ticker + search gated |
| H4 | `AlertAcknowledgementFailed` added as new AuditEventType | NOT a new constant per ADR 0080 §A0.5 — absence of `AlertAcknowledged` IS the failure signal |
| H5 | Pre-merge council returns NEEDS-AMENDMENT with non-mechanical findings | Apply; re-run council; do NOT auto-merge until APPROVED |
| H6 | `ISearchAsYouType` or `IFocusTrap` not yet in `Sunfish.UICore` | Phase 3a ComboBox / focus-trap cannot ship; coordinate Phase 3a sequencing with W#46 Phase 3 |

---

## Cohort precedents

- **Permission pre-resolution before snapshot**: established in ADR 0077 §2.3 — snapshot is the caller's authority-materialized view; never re-resolve in the UI layer.
- **Denied-not-hidden policy**: ADR 0077 §2.3 rule: "Never hide department existence — hidden departments degrade to 'user cannot explain why the system seems incomplete.'" Enforce in every `DepartmentLink` construction.
- **aria-disabled vs native disabled**: W#45 channel-session UI precedent; ADR 0081 §7 confirm-button pattern — `aria-disabled` keeps element in focus order for SR; native `disabled` removes it.
- **Two sibling live regions**: Engine Room Phase 3a pattern (assertive + polite siblings); same here for alert ticker.
- **alertdialog for destructive confirmations**: ADR 0081 §7 + ADR 0079 §6 Damage Control dialog — `role="alertdialog"` (not generic dialog) for any action that cannot be easily undone.
- **Pre-merge council before auto-merge**: mandatory; cohort batting average 29-of-30.

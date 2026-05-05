---
id: 80
title: Quarterdeck Entry-Point Surface
status: Accepted
date: 2026-05-05
tier: foundation
pipeline_variant: sunfish-feature-change

concern:
  - ui
  - accessibility
  - security
  - audit

enables:
  - quarterdeck-surface
  - ood-watch-banner
  - alert-ticker
  - department-navigation

composes:
  - 77
  - 65
  - 62
  - 49
  - 78
  - 36
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments: []
---

# ADR 0080 — Quarterdeck Entry-Point Surface

**Status:** Proposed
**Date:** 2026-05-05
**Authors:** XO research session
**Pipeline variant:** `sunfish-feature-change`
**Council posture:** standard adversarial + WCAG/a11y subagent (mandatory — entry-point,
OOD-banner, alert ticker, and department descent links are high-traffic a11y surfaces) +
security-engineering subagent (mandatory — watch-handover dialog + permission-denial UX)

---

## Context

The W#35 Ship Architecture discovery (§5.1) tagged Quarterdeck as a **Gap** — no current
artifact specifies the top-level entry point where users "report aboard" Sunfish. The
Quarterdeck is OOD's primary location (per W#35 §6.1): it surfaces the current watch state,
cross-department health summary, alert feed from Tactical, recent Standing Orders, and
permission-gated navigation links to every other department.

Without a Quarterdeck contract, every UI surface in the W#35 cohort lacks a coherent landing
point. Deep-link search, OOD-watch display, and executive-summary widgets all live in
TBD-land and each adapter re-derives them independently.

Shared Design System ADR (0077) is the load-bearing prerequisite; it established `ShipLocation`,
`DeckDepth`, `IPermissionResolver`, and `ILiveAnnouncer`. OOD-Watch ADR (0078) is a hard
prerequisite for the watch-banner integration. This ADR's package layout parallels the
Engine Room pattern (ADR 0079): thin `foundation-quarterdeck` contracts + `blocks-quarterdeck`
UI block.

---

## Decision drivers

1. **Single entry point.** Every user session begins at the Quarterdeck. It MUST load the
   current watch state, mission envelope, cross-department health, and recent Standing Orders
   in a single round-trip. No "home screen" pivoting.
2. **OOD watch authority.** The current OOD's identity + watch state MUST be visible from the
   Quarterdeck at all times. Watch handover is initiated from this surface (not from Engine
   Room or Tactical). The watch-status region uses `role="region"` as a named page section.
3. **Alert ticker.** High-priority alerts from Tactical flow to a Quarterdeck ticker.
   The ticker MUST honor `prefers-reduced-motion` (pauses animation and defaults to paused
   state on initial load); MUST expose a keyboard-reachable pause toggle (WCAG SC 2.2.2);
   high-priority alerts MUST use `aria-live="assertive"`, informational alerts
   `aria-live="polite"`.
4. **Permission-gated descent links.** Department navigation links MUST show denial reason
   via `PermissionDecision.Denied` + First-Aid contract. A blank screen for an inaccessible
   department violates WCAG SC 3.3.1 + the ADR 0077 denial-accessibility mandate.
5. **Shared Design System composition.** Uses ADR 0077 design tokens, `IPermissionResolver`,
   `ILiveAnnouncer`, `IFocusTrap`, `ISearchAsYouType`. No standalone token catalog or
   permission resolver.
6. **WCAG 2.2 AA exceedance over generic dashboards.** KPI cards encode status via text +
   icon (never color alone); deep-link search combobox announces result counts; watch-handover
   dialog uses `role="alertdialog"` with focus-trap.

---

## Considered options

### Option A — Quarterdeck as a pure UI block, data sourced ad-hoc

`blocks-quarterdeck` reads directly from `IMissionEnvelopeProvider`, `IStandingOrderRepository`,
`IOodWatchService`, and a list of `IQuarterdeckWidget` registrations. No new foundation package.

**Verdict:** rejected — cross-cutting data aggregation logic embedded in the block makes it
untestable independently and unreusable by Bridge, iOS field app, or future CLI surfaces.

### Option B — `foundation-quarterdeck` contracts + `blocks-quarterdeck` UI **[RECOMMENDED]**

`foundation-quarterdeck` defines the observable data model, the data-provider interface, the
alert-source seam, the KPI-source seam, and the command service interface. `blocks-quarterdeck`
renders the Quarterdeck using Shared Design System primitives.

**Verdict:** recommended — data model independently testable; seams let Tactical plug in
without modifying the block; Bridge and iOS can consume the same contracts.

### Option C — Inline Quarterdeck into `foundation-ship-common`

Add `IQuarterdeckDataProvider` to `foundation-ship-common` alongside `IPermissionResolver`.

**Verdict:** rejected — dashboard data aggregation is a different concern from role/permission
topology; downstream packages needing only `IPermissionResolver` would take an unneeded
transitive dependency on the Quarterdeck aggregation types.

---

## Decision

**Adopt Option B.** Introduce `Sunfish.Foundation.Quarterdeck`
(`packages/foundation-quarterdeck/`) for contracts and `Sunfish.Blocks.Quarterdeck`
(`packages/blocks-quarterdeck/`) for the UI block.

### §1 Top-deck data model

All types in `Sunfish.Foundation.Quarterdeck`:

```csharp
/// <summary>Options for the Quarterdeck data provider and subscription loop.</summary>
public sealed record QuarterdeckOptions(
    TimeSpan HeartbeatInterval,
    TimeSpan ProviderTimeout,
    TimeSpan PerSourceTimeout
)
{
    public static QuarterdeckOptions Default => new(
        HeartbeatInterval  : TimeSpan.FromSeconds(30),
        ProviderTimeout    : TimeSpan.FromSeconds(2),
        PerSourceTimeout   : TimeSpan.FromMilliseconds(800)
    );
}

// Snapshot of the full Quarterdeck state — one call for the full render.
// Snapshots are actor-specific (DepartmentLinks + IsCurrentActorOnWatch are
// pre-resolved per actor). Implementations MUST NOT cache snapshots across
// actors or TenantId boundaries.
public sealed record QuarterdeckSnapshot(
    NodaTime.Instant              CapturedAt,        // provider clock (IClock) at aggregation start
    OodWatchSummary?              ActiveWatch,        // null when no watch is active
    IReadOnlyList<DepartmentLink> Departments,        // ordered per W#35 §6.5 deck layout
    IReadOnlyList<QuarterdeckAlert> PendingAlerts,    // High→Normal→Info, then IssuedAt DESC;
                                                      // capped at 100; alerts >24h old OMITTED
                                                      // unless RequiresAcknowledgement==true
    IReadOnlyList<StandingOrderSummary> RecentOrders, // MUST contain ≤5 items, IssuedAt DESC
    MissionEnvelopeSummary        MissionEnvelope,
    IReadOnlyList<DepartmentKpi>  KpiCards
);

// Per-department navigation link.
// When AccessDecision.Outcome == Denied:
//   - DisplayName MUST still contain the department's canonical name (department existence
//     is not a secret in Sunfish; per ADR 0077)
//   - Status MUST be DepartmentStatus.Unknown regardless of actual health (prevents
//     operational-state inference by unauthorized actors)
public sealed record DepartmentLink(
    ShipLocation    Location,
    string          DisplayName,               // tenant-configured display label (pre-localized)
    DepartmentStatus Status,
    PermissionDecision AccessDecision          // Granted or Denied with reason + remediation
);

public enum DepartmentStatus { Operational, Warning, Critical, Unknown }

// UI-tier projection of OodWatch (ADR 0078). Avoids a direct OodWatch dependency
// in foundation-quarterdeck contracts. OodRoleSummary MUST stay numerically aligned with
// OodRole (ADR 0078 §1); when ADR 0078 adds new OodRole values, update OodRoleSummary
// in the same PR. Conversion: OodRoleSummary r = (OodRoleSummary)(int)oodWatch.Role.
//
// OnWatchActorDisplayName: visible to any actor with ViewQuarterdeck permission.
// Tenants requiring pseudonymization MUST configure a display-name policy via the
// forthcoming tenant-display-policy hook (future ADR).
//
// IsCurrentActorOnWatch: true when oodWatch.OnWatchActorId == actor.ActorId.
public sealed record OodWatchSummary(
    string              OnWatchActorDisplayName,
    OodRoleSummary      Role,
    NodaTime.Instant    StartedAt,
    NodaTime.Duration   MaxWatchDuration,      // NodaTime.Duration, not TimeSpan
    bool                IsCurrentActorOnWatch
);

// Must stay numerically aligned with OodRole (ADR 0078).
public enum OodRoleSummary { OfficerOfTheDeck, EngineeringOfficerOfTheWatch }

// Alert item. AlertId MUST be unique across all IQuarterdeckAlertSource implementations
// within a tenant. Convention: "{SourceName}:{source-local-id}". Provider MUST log and
// drop collisions during aggregation. Recommended format: "{SourceName}:{ULID}".
// AlertId characters MUST match ^[A-Za-z0-9_\-:]{1,128}$.
public sealed record QuarterdeckAlert(
    string              AlertId,
    AlertSeverity       Severity,
    string              Title,                 // ≤80 chars; used in aria-label
    string              Summary,               // ≤200 chars; full text for screen readers
    NodaTime.Instant    IssuedAt,
    bool                RequiresAcknowledgement,
    ShipLocation?       SourceLocation,        // null for cross-department alerts
    AlertVisibilityPolicy VisibilityPolicy = AlertVisibilityPolicy.OmitForDeniedActors
);

// Determines how alerts from a Denied-access department are surfaced to the requesting actor.
// OmitForDeniedActors (default): alert omitted from PendingAlerts when actor has Denied
//   AccessDecision on alert.SourceLocation.
// RedactSourceAndContent: alert included with SourceLocation=null, Title="[Restricted]",
//   Summary="[Restricted]". Use when alert count visibility is operationally required.
// Provider MUST apply this policy when building PendingAlerts in GetSnapshotAsync.
public enum AlertVisibilityPolicy { OmitForDeniedActors, RedactSourceAndContent }

public enum AlertSeverity { High, Normal, Informational }

// Standing Order summary for the "Recent Orders" widget.
// AffectsCurrentActor is true when ANY of: (a) actor.ActorId is in the order's distribution
// list; (b) actor's primary ShipRole is in the order's role-distribution list; (c) the order
// was issued by the actor. Provider MUST NOT include orders where this is false.
public sealed record StandingOrderSummary(
    string              OrderId,
    string              Subject,               // ≤80 chars; pre-localized
    NodaTime.Instant    IssuedAt,
    string              IssuedByDisplayName,   // pre-localized
    bool                AffectsCurrentActor
);

// Projection of ADR 0062 MissionEnvelope state:
//   Nominal   = all dimensions FeatureAvailabilityState.Available + ProbeStatus.Healthy
//   Degraded  = any dimension DegradedAvailable or ProbeStatus.PartiallyDegraded/Stale/Failed
//   Unknown   = IMissionEnvelopeProvider unavailable or threw (see §2.1 failure modes)
// When Status == Nominal, StatusDetail MUST be null.
// When Status == Degraded, StatusDetail MUST be a non-empty human-readable explanation.
// When Status == Unknown, StatusDetail MAY be populated with the failure reason.
public sealed record MissionEnvelopeSummary(
    MissionEnvelopeStatus Status,
    string?               StatusDetail
);

public enum MissionEnvelopeStatus { Nominal, Degraded, Unknown }

// KPI card per department. MetricName, MetricValue, StatusLabel MUST be pre-localized
// by the provider before population.
public sealed record DepartmentKpi(
    ShipLocation  Location,
    string        MetricName,        // e.g., "Sync peers"
    string        MetricValue,       // localized; includes unit
    DepartmentStatus Status,
    string        StatusLabel        // text label; never color-alone (WCAG SC 1.4.1)
);
```

### §2 Provider interfaces

```csharp
// Read-only aggregation interface.
public interface IQuarterdeckDataProvider
{
    /// <summary>
    /// Returns the full Quarterdeck snapshot for the given actor + tenant.
    /// <para>
    /// Implementations MUST internally enforce a per-source deadline of
    /// <see cref="QuarterdeckOptions.PerSourceTimeout"/> (default 800ms) by linking
    /// the caller's <paramref name="ct"/> with a <c>CancellationTokenSource</c>.
    /// On per-source timeout, return the section's Unknown/empty sentinel (see §2.1).
    /// MUST NOT throw except on outer <paramref name="ct"/> cancellation.
    /// </para>
    /// <para>
    /// Block-side callers MUST additionally wrap in
    /// <see cref="QuarterdeckOptions.ProviderTimeout"/> (default 2s) as defense-in-depth.
    /// </para>
    /// </summary>
    ValueTask<QuarterdeckSnapshot> GetSnapshotAsync(
        TenantId tenantId,
        Principal actor,
        CancellationToken ct = default);

    /// <summary>
    /// Yields updated snapshots. Heartbeat every
    /// <see cref="QuarterdeckOptions.HeartbeatInterval"/> (default 30s).
    /// Push triggers: (a) any OodWatch state transition; (b) new alert with
    /// <see cref="QuarterdeckAlert.RequiresAcknowledgement"/>==true, or acknowledged
    /// alert removed; (c) any <see cref="DepartmentKpi.Status"/> enum value change.
    /// MUST NOT emit on MetricValue string changes alone unless accompanied by a
    /// Status change. Implementations SHOULD coalesce events within a 500ms window.
    /// See §2.2 for subscription lifecycle semantics.
    /// </summary>
    IAsyncEnumerable<QuarterdeckSnapshot> SubscribeSnapshotAsync(
        TenantId tenantId,
        Principal actor,
        CancellationToken ct = default);
}

/// <summary>
/// Alert source seam. Tactical (ADR 0081) registers an implementation.
/// <see cref="IQuarterdeckDataProvider"/> resolves as
/// <see cref="IEnumerable{IQuarterdeckAlertSource}"/> for multi-source.
/// SourceName MUST be unique across all registrations; see §5.3 for startup enforcement.
/// </summary>
public interface IQuarterdeckAlertSource
{
    /// <summary>
    /// Unique source name. Convention: "sunfish.{domain}" (e.g., "sunfish.engine-room").
    /// Third-party sources MUST use a namespaced prefix to prevent first-party impersonation.
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Returns currently active alerts for the tenant. Returns at most 50 items,
    /// sorted by IssuedAt DESC. Called on every snapshot refresh.
    /// </summary>
    ValueTask<IReadOnlyList<QuarterdeckAlert>> GetAlertsAsync(
        TenantId tenantId, CancellationToken ct = default);
}

/// <summary>
/// KPI source seam. Each department ADR registers an implementation.
/// Empty list if no sources registered for a given ShipLocation.
/// SourceName uniqueness rules mirror IQuarterdeckAlertSource (§5.3).
/// </summary>
public interface IDepartmentKpiSource
{
    string SourceName { get; }

    /// <summary>
    /// Returns at most one DepartmentKpi per ShipLocation this source covers.
    /// </summary>
    ValueTask<IReadOnlyList<DepartmentKpi>> GetKpisAsync(
        TenantId tenantId, Principal actor, CancellationToken ct = default);
}

/// <summary>
/// Quarterdeck command surface: alert acknowledgement.
/// </summary>
public interface IQuarterdeckCommandService
{
    /// <summary>
    /// Acknowledges an alert. Idempotent: re-acknowledging is audit-logged, not an error.
    /// Throws <see cref="AlertNotFoundException"/> when alertId is unknown or expired.
    /// MUST NOT throw on re-acknowledgement of a valid alertId.
    /// </summary>
    /// <remarks>
    /// Block MUST call
    /// <c>IPermissionResolver.ResolveAsync(actor, alert.SourceLocation ?? ShipLocation.Quarterdeck,
    /// DeckDepth.TopDeck, ShipAction.AcknowledgeAlert, alertId)</c>
    /// before calling this method. On Denied, throw without calling.
    /// Two-phase audit: emit AuditEventType.AlertAcknowledgementRequested (pre-op),
    /// then AuditEventType.AlertAcknowledged (post-op success) or
    /// AuditEventType.AlertAcknowledgementFailed (post-op failure).
    /// </remarks>
    ValueTask AcknowledgeAlertAsync(
        TenantId tenantId, Principal actor, string alertId,
        CancellationToken ct = default);
}
```

#### §2.1 Failure modes

`GetSnapshotAsync` aggregates up to 6 data sources. On per-source timeout or exception:

| Source | Failure sentinel |
|---|---|
| `IOodWatchService.GetActiveWatchAsync` | `ActiveWatch = null` (no watch active) |
| `IQuarterdeckAlertSource.GetAlertsAsync` (any source) | Omit that source's alerts; include others |
| `IStandingOrderRepository.EnumerateAsync` | `RecentOrders = []` (empty list) |
| `IMissionEnvelopeProvider.GetCurrentAsync` | `MissionEnvelope = new(Unknown, "Source unavailable")` |
| `IPermissionResolver.ResolveAsync` (any location) | `DepartmentLink.AccessDecision = Denied(reason:"Resolution failed", remediation:null)` |
| `IDepartmentKpiSource.GetKpisAsync` | `KpiCards` omits that source's entries; `Status = DepartmentStatus.Unknown` |

Provider MUST log each failure via `IAuditTrail` (best-effort; non-blocking) and MUST NOT
throw from `GetSnapshotAsync` except on outer `CancellationToken` cancellation.
`CapturedAt` MUST be set via `IClock.GetCurrentInstant()` at aggregation start (before source
calls), not after, so it accurately represents the data freshness window.

#### §2.2 Subscription semantics

`SubscribeSnapshotAsync` lifecycle:

- Consumer cancels via the supplied `CancellationToken` or by breaking `await foreach`.
- Producer MUST drop intermediate snapshots if consumer is more than one snapshot behind;
  latest-wins. Recommended: `Channel<QuarterdeckSnapshot>(BoundedChannelOptions{Capacity:1,
  FullMode:BoundedChannelFullMode.DropOldest})`.
- Snapshots are atomic: if state changes mid-build, build completes with consistent
  pre-change state and a fresh snapshot is enqueued for the next emission.
- On source fault mid-subscription: emit one degraded snapshot (per §2.1 sentinels), then
  continue heartbeat loop. MUST NOT terminate the subscription on transient source failure.

#### §2.3 Aggregation rules

Default `IQuarterdeckDataProvider` implementation obligations:

1. **Permission pre-resolution.** For each `ShipLocation` value, call
   `IPermissionResolver.ResolveAsync(actor, location, DeckDepth.TopDeck, ShipAction.View, null)`.
   Cache key MUST include `(TenantId, ActorId, ShipLocation)`; MUST NOT share entries across
   `TenantId` boundaries. Per §5.2 tenant-binding rules.
2. **`RecentOrders`.** Enumerate via `IStandingOrderRepository.EnumerateAsync(tenantId)`,
   filter client-side to orders where actor is in distribution list (§1 `AffectsCurrentActor`
   semantics), take top 5 by `IssuedAt DESC`. On >5 matching: return first 5; MUST NOT
   page all orders indefinitely — cap enumeration at 1,000 orders (log warning if exceeded).
3. **`AffectsCurrentActor`.** True when the order's `IssuedBy == actor.ActorId` OR
   `DistributionList.Contains(actor.ActorId)` OR `RoleDistribution.Contains(actor.PrimaryRole)`.
   This field MUST be computed by the provider; block MUST NOT recompute it.
4. **`IsCurrentActorOnWatch`.** True when `GetActiveWatchAsync` returned a non-null `OodWatch`
   AND `oodWatch.OnWatchActorId == actor.ActorId`.
5. **Alert visibility.** Apply `QuarterdeckAlert.VisibilityPolicy` per §1: for each alert
   where `alert.SourceLocation != null` and actor's `DepartmentLink.AccessDecision` for that
   location is Denied — either omit or redact per the policy.
6. **PendingAlerts ordering.** Sort by `Severity` (High → Normal → Informational), then by
   `IssuedAt DESC`. Cap merged result at 100. Drop alerts where `IssuedAt` is >24h ago unless
   `RequiresAcknowledgement == true`. Dedup by `AlertId`; on collision, keep most-recently
   issued entry.
7. **Parallel sourcing.** All source calls MUST run via `Task.WhenAll`; never sequential
   awaits. This is required to hit the 1s aggregate target for 6 sources.
8. **OodWatchSummary projection.** For each `OodRole` value, call
   `IOodWatchService.GetActiveWatchAsync(tenantId, role)`. If either role returns non-null,
   populate `ActiveWatch`. Display name resolution: `OnWatchActorDisplayName` comes from the
   actor-directory service or the watch record's actor display-name field (implementation
   MUST document where this is sourced; forward-ref note in §A0.4).

### §3 Watch banner contract

The OOD watch status region MUST be rendered as a `role="region" aria-label="Watch status"`
landmark within the Quarterdeck page body. It MUST NOT claim `role="banner"` (that role is
reserved for the single app-shell top-level header per ADR 0077 landmark structure;
ARIA 1.2 §3.3 permits only one `banner` landmark per page). It displays:

- **Watch active:** actor display name + role label + time elapsed since `StartedAt`
  (visible text; NOT in `aria-label` — see §6)
- **Watch active (current actor):** "You have the deck" + role label + elapsed time +
  "Transfer Watch" button (shown when `IsCurrentActorOnWatch == true`)
- **No watch active:** "No watch active" + time-last-held + "Stand Watch" button (if
  authorized via `ShipAction.StandWatch`)

**Watch handover interaction model (pre-select pattern):**
The "Transfer Watch" button is shown only when the relieving actor has been selected
prior to clicking. The relieving-actor selection widget (combobox of eligible actors) MUST
appear outside and before the dialog in the tab order. The dialog confirms a fully-resolved
transfer (no selection inside the alertdialog). This ensures `aria-describedby` is
fully-resolved on dialog open (SC 3.3.2 Labels or Instructions).

**Watch handover dialog** (`role="alertdialog"`):
- `aria-labelledby` references a heading: `"Transfer Watch: {Role}"`
- `aria-describedby` references consequence summary (resolved at dialog-open time):
  `"You will transfer {role} watch authority to {relievingActor}. Your watch record
  will be marked Relieved. This action is audited."`
- Focus moves to the Cancel button on open (SC 2.4.3 Focus Order; watch transfer is
  irreversible — confirm should not be the first focusable target)
- Confirm button `"Confirm Transfer"`: MUST be rendered `aria-disabled="true"` on dialog
  open and MUST become enabled exactly 2 000 ms after the dialog's open event (SC 3.3.4
  Error Prevention — forced deliberation pause for irreversible action)
- `aria-live="polite"` region announces "Confirm Transfer is now available" at t=2s
- Esc and Cancel close without transferring; focus returns to trigger element if in DOM;
  if trigger is no longer reachable, focus lands on `<main id="main-content">` via
  `IFocusTrap.RestoreFocus(fallback: MainLandmark)` + polite announcement "Dialog closed"
- Focus trap per ADR 0077 `IFocusTrap`

**Pre-handover obligation (block MUST):**

1. Call `IPermissionResolver.ResolveAsync(actor, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
   ShipAction.TransferWatch, null)`.
2. If Denied: surface denial via First-Aid; MUST NOT call `HandoverWatchAsync`.
3. If Granted: capture `activeWatch.Id` (the `OodWatchId`) at dialog-open time.
4. On confirm: emit `AuditEventType.WatchHandoverRequested` (pre-op) with payload
   `{ watchId, actorId, incomingActorId, tenantId, capturedAt }`.
5. Call `IOodWatchService.HandoverWatchAsync(currentWatchId: capturedWatch.Id, incomingActor)`.
   `HandoverWatchAsync` takes `(OodWatchId currentWatchId, ActorId incomingActor)` — NO
   TenantId parameter (watch is uniquely identified by its id; see §A0.4). The `OodWatchId`
   acts as an optimistic concurrency token: if the watch rotated between step 3 and step 5,
   `OodWatchConflictException` is thrown and the pre-op audit record serves as the intent trace.
6. ADR 0078's `IOodWatchService.HandoverWatchAsync` emits `AuditEventType.OodWatchRelieved`
   internally — that is the post-op audit event.

### §4 Alert ticker contract

The alert ticker renders a scrolling list of `QuarterdeckAlert` items from all registered
`IQuarterdeckAlertSource` implementations.

**Behavior:**
- High-severity alerts (`AlertSeverity.High`) MUST be announced via a region:
  `aria-live="assertive" aria-atomic="false" aria-relevant="additions"`.
- Normal and Informational alerts MUST use a sibling region:
  `aria-live="polite" aria-atomic="false" aria-relevant="additions"`.
  (Two sibling regions — assertive + polite — each receives per-item additions only.
   `aria-atomic="false"` prevents full-list re-announcement on every change; individual
   alert items announce as they are added to the region.)
- The ticker animation (scroll/fade) MUST be disabled when `prefers-reduced-motion: reduce`
  applies (WCAG SC 2.3.1 + 2.2.2). Under `prefers-reduced-motion: reduce`, ticker MUST
  additionally **default to paused state on initial load** (`aria-pressed="true"` on Pause
  button; no auto-scroll; static list renders; live-region announcements continue).
- A keyboard-reachable Pause button MUST be visible adjacent to the ticker. It MUST use the
  `aria-pressed`-only pattern: label remains `"Pause alerts"` regardless of state;
  `aria-pressed` toggles `"false"` ↔ `"true"`. Tab from the last KPI card reaches the Pause
  button before reaching alert list items.
- When paused (`aria-pressed="true"`): animation stops; new alerts still arrive in the DOM
  but do not auto-scroll; polite live-region announcements are suppressed; assertive
  live-region MUST continue announcing High-severity alerts regardless of pause state (safety
  requirement). Pause button label should clarify: `aria-label="Pause non-critical alerts"`.
- Acknowledge button activation MUST NOT trigger a live-region re-announcement.

**Alert item accessibility:**
- Each alert item: `role="listitem"` within `role="list"` (not `role="alertdialog"`)
- Accessible name via `aria-label="{Severity}: {Title} — {formattedTime}"`
- Severity conveyed via text label AND icon with `aria-label`; never color alone (SC 1.4.1)
- If `RequiresAcknowledgement == true`: renders an `"Acknowledge"` button within the
  listitem; keyboard-operable; clicking calls `IQuarterdeckCommandService.AcknowledgeAlertAsync`
  after the pre-flight permission check (§5)

### §5 Permission model

New `ShipAction` constants in `Sunfish.Foundation.Ship.Common` (**forward-ref: W#46 build**):

```csharp
// Page-level access (all ShipRole actors)
public static readonly ShipAction ViewQuarterdeck      = new("ViewQuarterdeck");
// Alert ticker + acknowledgement (DivisionOfficer or higher)
public static readonly ShipAction ViewQuarterdeckAlerts = new("ViewQuarterdeckAlerts");
public static readonly ShipAction AcknowledgeAlert      = new("AcknowledgeAlert");
```

Permission resolution table:

| ShipAction | Minimum authority |
|---|---|
| `ViewQuarterdeck` | Any authenticated actor with a `ShipRole` assignment on the tenant |
| `ViewQuarterdeckAlerts` | `ShipRole.DivisionOfficer`, `ShipRole.OOD`, `ShipRole.EOOW`, `ShipRole.EngineerOfficer`, `ShipRole.XO`, `ShipRole.Captain` |
| `AcknowledgeAlert` | Same as `ViewQuarterdeckAlerts` |
| `ShipAction.TransferWatch` | Current OOD/EOOW (verified via `IOodWatchService.GetActiveWatchAsync`; ADR 0078 forward-ref) |
| `ShipAction.StandWatch` | `ShipRole.DivisionOfficer` or higher, per `IOodWatchService` watch-authority rules |
| Department descent (`ShipAction.Read` at target `ShipLocation`) | Per target location's permission rules (ADR 0077 §2.1 resolution table) |

`ShipAction.TransferWatch` and `ShipAction.StandWatch` are defined in ADR 0077 §2 existing
catalog (W#46 forward-ref); they are NOT new constants introduced by this ADR.

**`IQuarterdeckCommandService.AcknowledgeAlertAsync` pre-flight (block MUST):**

1. Call `IPermissionResolver.ResolveAsync(actor, alert.SourceLocation ?? ShipLocation.Quarterdeck,
   DeckDepth.TopDeck, ShipAction.AcknowledgeAlert, alert.AlertId)`.
2. If Denied: emit `AuditEventType.AlertAcknowledgementFailed` + surface denial via
   First-Aid; MUST NOT call `AcknowledgeAlertAsync`.
3. If Granted: call `AcknowledgeAlertAsync` with two-phase audit (§2
   `IQuarterdeckCommandService` XML doc).

**Permission pre-resolution obligation.** The default `IQuarterdeckDataProvider` MUST
pre-resolve `IPermissionResolver` for all known `ShipLocation` values when building
`DepartmentLinks`; the snapshot is the caller's authority-materialized view. Never hide
department existence — hidden departments degrade to "user cannot explain why the system
seems incomplete."

#### §5.1 ShipAction registration verification

At host startup, the Quarterdeck DI registration MUST verify that `ViewQuarterdeck`,
`ViewQuarterdeckAlerts`, and `AcknowledgeAlert` are registered with `IPermissionResolver`.
Missing registrations MUST throw `InvalidOperationException` and prevent host startup. Mirrors
ADR 0079 §4.3 startup-verification pattern.

#### §5.2 Tenant context binding

`IQuarterdeckDataProvider` implementations MUST resolve ambient tenant from `ITenantContext`
and MUST reject requests where the `tenantId` parameter does not match the ambient tenant
(anti-spoofing; mirrors ADR 0079 §4.1). Permission resolution cache keys MUST include
`TenantId` as a primary key component and MUST NOT be shared across `TenantId` boundaries.

#### §5.3 Alert source and KPI source uniqueness

At host startup, the DI registration MUST verify `SourceName` uniqueness across all
registered `IQuarterdeckAlertSource` AND `IDepartmentKpiSource` instances per source type.
Duplicates MUST throw `InvalidOperationException`. `SourceName` values MUST follow a
registered-prefix pattern (`sunfish.{domain}`) so third-party plugins cannot impersonate
first-party sources.

### §6 WCAG 2.2 AA conformance contract

Per ADR 0077 WCAG 2.2 AA baseline + EN 301 549 v3.2.1:

**Page structure:**
- Quarterdeck page MUST define a `<main id="main-content" tabindex="-1">` landmark.
- Watch status region: `<section role="region" aria-label="Watch status">` (NOT
  `role="banner"` — per §3; only app-shell top-level `<header>` carries `banner`).
- Navigation to departments MUST be within a `<nav aria-label="Departments">` landmark.
- A skip-link `<a href="#main-content">Skip to main content</a>` MUST be the first
  focusable element. On activation, focus MUST land on `<main id="main-content">` (the
  `tabindex="-1"` enables programmatic focus in all browsers including Safari/Chrome ≥81).

**KPI cards:**
- Container MUST be `<ul role="list">` (explicit `role="list"` required — Safari/VoiceOver
  strips implicit list semantics from `<ul>` when `list-style: none` CSS applies).
- Each card: `<li role="listitem">`.
- `aria-label` includes metric name + value + status label:
  `aria-label="Engine Room — 4 peers — Operational"`
- Status encoded as text label + icon (`aria-label` on icon); never color alone (SC 1.4.1).

**Deep-link search:**
- Implements `ISearchAsYouType` (ADR 0077 `Sunfish.UICore`):
  `role="combobox"` input + `role="listbox"` results popup.
- Arrow keys navigate results; Enter follows link; Esc closes popup.
- Active result announced: `aria-activedescendant` + result `id` attribute.
- Result option IDs MUST follow pattern `quarterdeck-search-result-{stableKey}` where
  `stableKey` is derived from the result's canonical Wayfinder address (W#42 ADR 0065).
  IDs MUST be stable within a search session (re-keyed only on query change, not each
  keystroke). Conformance: `SunfishA11yAssertions.ActiveDescendantIdResolves` MUST verify
  the referenced ID exists in the DOM at announcement time.
- Result count announcement MUST be rendered in a separate
  `<div class="sr-only" aria-live="polite" aria-atomic="true">` element that is a SIBLING
  to the combobox container (NOT inside the combobox or listbox). Announcement text:
  `"{n} results"` for n > 0; `"No results"` for n == 0; empty (no update) when query < 2
  chars. Updated on debounced 300ms idle; block owns debounce via `ILiveAnnouncer`.

**OOD watch status region:**
- Watch status readable as a coherent sentence. Accessible name MUST NOT include elapsed
  time (SRs cache `aria-label` at focus-time; stale elapsed time is misleading):
  `aria-label="Watch active: Alice Smith, Officer of the Deck"`
- Elapsed time MUST be rendered as visible text with `aria-hidden="true"` on the time
  element. Provide a focusable child element with `aria-label="Watch elapsed: {h} hours
  {m} minutes"` updated on a 60-second cadence via `ILiveAnnouncer.AnnouncePolite`.
  MUST NOT sub-minute announcement rate (SC 2.2.2).

**Department descent links:**
- Links that are `Denied` MUST render as `<button aria-disabled="true">` (focusable;
  NOT native `disabled` attribute which removes focusability).
- `aria-describedby` MUST reference a **visible** denial-reason element (so sighted
  keyboard users also see the reason; NOT `sr-only`-only).
- Click and keyboard activation handlers MUST return early (no side effects) when
  `aria-disabled="true"`. CSS MUST include `cursor: not-allowed`.
- MUST NOT use `display:none` or `visibility:hidden` for inaccessible departments.
- Conformance: `SunfishA11yAssertions.AriaDisabledSuppressesActivation`.

**Alert ticker:** per §4.

**Watch handover dialog:** per §3.

**Confirm button timing:** MUST be `aria-disabled="true"` on open; MUST enable at t=2s
(SC 3.3.4 Error Prevention; forced deliberation). Focus MUST remain on Cancel for the
first 2 seconds; confirm focus is allowed once enabled.

**Focus restoration fallback (all dialogs):** on dialog close, focus returns to trigger if
in DOM; if not, focus lands on `<main id="main-content">` via
`IFocusTrap.RestoreFocus(fallback: MainLandmark)` with polite `"Dialog closed"` announcement.

**Pause button:** see §4. `aria-pressed`-only pattern:
`aria-pressed` toggles `"false"` ↔ `"true"`; label `"Pause non-critical alerts"` is static.
Under `prefers-reduced-motion: reduce`, MUST default to paused state (`aria-pressed="true"`)
on initial load; verify via `SunfishA11yAssertions.ReducedMotionDefaultsToPaused`.

#### §6.1 Conformance verification

WCAG conformance MUST be verified using `Sunfish.UIAdapters.Blazor.A11y.SunfishA11yAssertions`
(ADR 0034) and the React parity equivalent. Required assertions: page landmark structure,
skip-link focus destination, combobox search pattern, `aria-live` region presence and
`aria-atomic="false"` + `aria-relevant="additions"` on ticker regions, watch-status region
accessible name (no elapsed time in aria-label), KPI card list semantics, dialog focus-trap
and confirm-button timing, `aria-disabled` suppression behavior.

### §7 Package layout

```
packages/
  foundation-quarterdeck/
    Sunfish.Foundation.Quarterdeck.csproj
    QuarterdeckSnapshot.cs       (+ DepartmentLink, OodWatchSummary, OodRoleSummary,
                                    AlertVisibilityPolicy, QuarterdeckOptions)
    QuarterdeckAlert.cs          (+ AlertSeverity)
    StandingOrderSummary.cs
    MissionEnvelopeSummary.cs    (+ MissionEnvelopeStatus)
    DepartmentKpi.cs             (+ DepartmentStatus)
    IQuarterdeckDataProvider.cs
    IQuarterdeckAlertSource.cs
    IDepartmentKpiSource.cs
    IQuarterdeckCommandService.cs
    DI/QuarterdeckServiceCollectionExtensions.cs  (registers interfaces + startup checks)

  blocks-quarterdeck/
    Sunfish.Blocks.Quarterdeck.csproj
    TopDeck/
      WatchBanner/               (OOD watch-status region + handover dialog)
      AlertTicker/               (two live regions + pause button)
      KpiCards/                  (per-department status cards)
    MainDeck/
      DepartmentNav/             (permission-gated descent links)
      SearchPanel/               (ISearchAsYouType combobox)
      RecentOrders/              (last 5 Standing Orders widget)
      MissionEnvelopeWidget/     (ADR 0062 summary)
    DI/QuarterdeckBlockServiceCollectionExtensions.cs
```

`foundation-quarterdeck` ProjectReferences:
- `Sunfish.Foundation` (for `TenantId`, `ActorId`)
- `Sunfish.Foundation.Ship.Common` (for `ShipLocation`, `ShipAction`, `PermissionDecision`;
  **forward-ref: W#46 build**)
- `Sunfish.Foundation.Capabilities` (for `Principal`)

`blocks-quarterdeck` ProjectReferences:
- `Sunfish.Foundation.Quarterdeck`
- `Sunfish.Foundation.Ship.Common` (for `IPermissionResolver`, `ITenantContext`)
- `Sunfish.Foundation.Wayfinder` (for `IStandingOrderRepository`, `IOodWatchService`;
  **forward-ref: W#49 build**)
- `Sunfish.Foundation.MissionSpace` (for `IMissionEnvelopeProvider`)
- `Sunfish.UICore` (for `ILiveAnnouncer`, `IFocusTrap`, `ISearchAsYouType`)
- `Sunfish.Kernel.Audit` (for `IAuditTrail`, `AuditEventType`)

### §A0 Cited-symbol audit

#### §A0.1 Symbols NOT introduced by this ADR (pre-acceptance verification)

- `Sunfish.Foundation.Assets.Common.TenantId` — verified at `packages/foundation/Assets/Common/TenantId.cs` ✓
- `Sunfish.Foundation.Assets.Common.ActorId` — verified at `packages/foundation/Assets/Common/ActorId.cs` ✓
- `Sunfish.Foundation.Capabilities.Principal` — verified at `packages/foundation/Capabilities/` ✓
- `Sunfish.Kernel.Audit.IAuditTrail.AppendAsync` — verified at `packages/kernel-audit/` ✓
- `NodaTime.Instant`, `NodaTime.Duration` — external dependency ✓
- `Sunfish.Foundation.MissionSpace.IMissionEnvelopeProvider` — ADR 0062 W#40 built ✓
  `MissionEnvelopeStatus` is a **new projection type** introduced by this ADR; mapping from
  ADR 0062 `FeatureAvailabilityState` / `ProbeStatus` defined in §1.
- `Sunfish.Foundation.Wayfinder.IStandingOrderRepository` — W#42 built ✓.
  `IStandingOrderRepository` provides only `AppendAsync`, `GetAsync`, and `EnumerateAsync`;
  there is no `GetByActorAsync`. Default provider MUST use `EnumerateAsync` + client-side
  filtering per §2.3.

#### §A0.2 Symbols that are forward-references (halt-conditions)

- `Sunfish.Foundation.Ship.Common.ShipLocation` — ADR 0077 W#46 Stage 06 Phase 1.
  **Halt-condition:** W#46 Phase 1 must land before W#51 Phase 2.
- `Sunfish.Foundation.Ship.Common.IPermissionResolver` — same W#46 dependency.
- `Sunfish.Foundation.Ship.Common.ITenantContext` — same W#46 dependency.
- `Sunfish.Foundation.Ship.Common.ShipAction` — same; `ViewQuarterdeck`,
  `ViewQuarterdeckAlerts`, and `AcknowledgeAlert` MUST be registered during W#46 Phase 1
  (or a post-W#46 amendment per §5.1 startup-verification pattern).
- `Sunfish.Foundation.Ship.Common.ShipAction.TransferWatch` — ADR 0077 §2 existing catalog
  entry (W#46 forward-ref). NOT a new constant from this ADR.
- `Sunfish.Foundation.Ship.Common.ShipAction.StandWatch` — same.
- `Sunfish.Foundation.Wayfinder.IOodWatchService` — ADR 0078 W#49 Stage 06 build.
  **Halt-condition:** W#49 Phase 1 must land before W#51 Phase 3 (WatchBanner wiring).
- `Sunfish.UICore.ILiveAnnouncer`, `IFocusTrap`, `ISearchAsYouType` — ADR 0077 W#46 Stage 06.
  **Halt-condition:** W#46 Phase 3 (ui-core extension build) must land before W#51 Phase 3.

#### §A0.3 Structural-citation correctness

- `IPermissionResolver.ResolveAsync(Principal, ShipLocation, DeckDepth, ShipAction, Resource?, CancellationToken)` — verified against ADR 0077 §2 exact signature ✓
- `ShipLocation.Quarterdeck` enum value — verified in ADR 0077 §2 ✓
- `DeckDepth.TopDeck` — verified in ADR 0077 §2 ✓
- `ShipAction.TransferWatch`, `ShipAction.StandWatch` — verified in ADR 0077 §2 existing catalog ✓
- `PermissionDecision.Denied` — verified as abstract discriminated union in ADR 0077 §2 ✓
- `Sunfish.Foundation.Capabilities.Principal` — verified at `packages/foundation/Capabilities/` ✓
- `OodRoleSummary` must remain numerically aligned with `OodRole` (ADR 0078 §1). Alignment
  verified at W#49 build time; amend both enums in the same PR if new OodRole values land.

#### §A0.4 Expected forward-reference signatures

```csharp
// ADR 0078 — expected signatures (subject to W#49 final shape; amend if diverged).
// NOTE: HandoverWatchAsync takes (OodWatchId, ActorId) — no TenantId parameter;
// OodWatchId uniquely identifies the watch and carries implicit tenant binding.
public interface IOodWatchService
{
    ValueTask<OodWatch?> GetActiveWatchAsync(
        TenantId tenantId, OodRole role, CancellationToken ct = default);
    ValueTask<OodWatch> StartWatchAsync(
        TenantId tenantId, ActorId actor, OodRole role,
        TimeSpan maxDuration, CancellationToken ct = default);
    ValueTask<(OodWatch Relieved, OodWatch Started)> HandoverWatchAsync(
        OodWatchId currentWatchId, ActorId incomingActor,
        CancellationToken ct = default);
}
public enum OodRole { OfficerOfTheDeck, EngineeringOfficerOfTheWatch }
public readonly record struct OodWatchId(Guid Value);

// ADR 0077 W#46 (foundation-ship-common) — ShipAction.TransferWatch + StandWatch
// are existing catalog entries per ADR 0077 §2; NOT newly declared by this ADR.
```

#### §A0.5 New `AuditEventType` constants

This ADR introduces **3 new** `AuditEventType` static-readonly constants on
`Sunfish.Kernel.Audit.AuditEventType`:

```csharp
public static readonly AuditEventType WatchHandoverRequested         = new("WatchHandoverRequested");
public static readonly AuditEventType AlertAcknowledgementRequested  = new("AlertAcknowledgementRequested");
public static readonly AuditEventType AlertAcknowledged              = new("AlertAcknowledged");
// Note: AlertAcknowledgementFailed re-uses AlertAcknowledgementRequested as the intent
// record; a separate "failed" event is not needed (the absence of AlertAcknowledged
// after AlertAcknowledgementRequested in the audit trail is the failure signal).
```

Post-op `OodWatchRelieved` is emitted by ADR 0078's `IOodWatchService.HandoverWatchAsync`
internally; this ADR MUST NOT emit a duplicate.

---

## Consequences

### Positive

- Every user session lands at the Quarterdeck; cross-department navigation is consistent
  and permission-governed from a single entry point
- OOD watch authority is visible at all times from the primary region; no "who's on watch?"
  ambiguity during operations
- Alert ticker connects Tactical (ADR 0081) to the entry point via the `IQuarterdeckAlertSource`
  seam without modifying this ADR; plug-and-play
- Permission-denied departments surface denials through First-Aid; users never see blank
  screens or unexplained navigation absences

### Negative

- Three new packages / interfaces extend the package graph; all additive
- `QuarterdeckSnapshot` aggregates 6 data sources — implementations MUST use
  `Task.WhenAll` parallelism to hit the 1s per-source target
- `IQuarterdeckAlertSource` is empty by default until Tactical (ADR 0081) ships; the alert
  ticker renders "No active alerts" in the interim

### Trust impact / Security & privacy

- `QuarterdeckSnapshot.DepartmentLinks` pre-resolves permissions for all locations per actor.
  Resolution is actor-specific and tenant-scoped (§5.2); no cross-tenant leakage.
- Watch handover is initiated from Quarterdeck; block MUST call `IPermissionResolver` before
  `IOodWatchService.HandoverWatchAsync` — never bypass. `OodWatchId` optimistic concurrency
  handles TOCTOU at service level (see §3).
- Alert topology disclosure is controlled by `AlertVisibilityPolicy` (§1); default is
  `OmitForDeniedActors`.

---

## Compatibility plan

**`foundation-quarterdeck`** — new package; additive; no existing packages changed

**`foundation-ship-common`** — 3 new `ShipAction` constants (`ViewQuarterdeck`,
`ViewQuarterdeckAlerts`, `AcknowledgeAlert`); additive; see timing note in §A0.2

**`Sunfish.Kernel.Audit.AuditEventType`** — 3 new constants; additive

**All other packages:** no changes required

---

## Implementation checklist

### Phase 1 — foundation-quarterdeck substrate (~3–4h; 1 PR)

- [ ] Scaffold `packages/foundation-quarterdeck/Sunfish.Foundation.Quarterdeck.csproj`
  with ProjectReferences to `Sunfish.Foundation` + `Sunfish.Foundation.Capabilities`
  (NOT `Sunfish.Foundation.Ship.Common` yet — forward-ref; add Phase 2)
- [ ] Add all data-model types from §1 (`QuarterdeckSnapshot`, `DepartmentLink`,
  `OodWatchSummary`, `OodRoleSummary`, `QuarterdeckAlert`, `StandingOrderSummary`,
  `MissionEnvelopeSummary`, `MissionEnvelopeStatus`, `DepartmentKpi`, `DepartmentStatus`,
  `AlertSeverity`, `AlertVisibilityPolicy`, `QuarterdeckOptions`)
- [ ] Add `IQuarterdeckDataProvider`, `IQuarterdeckAlertSource`, `IDepartmentKpiSource`,
  `IQuarterdeckCommandService` interfaces
- [ ] Add 3 new `AuditEventType` constants (§A0.5) + 3 new `ShipAction` constants to
  `packages/foundation-ship-common/` — **Halt-condition:** W#46 Phase 1 must have landed
- [ ] Add `DI/QuarterdeckServiceCollectionExtensions.cs` (registers interfaces; startup
  checks per §5.1 + §5.3)
- [ ] Unit tests (6 minimum): `QuarterdeckSnapshot_DeniedDepartment_StatusIsUnknown`,
  `DepartmentLink_DeniedAccessDecision_DisplayNamePreserved`,
  `OodWatchSummary_IsCurrentActor_MatchesActorId`,
  `AlertSeverity_HighVsNormal`,
  `PendingAlerts_OrderedBySeverityThenIssuedAt`,
  `PendingAlerts_OmitsExpiredAlerts_UnlessAcknowledgementRequired`
- [ ] Pre-merge council (mandatory; WCAG/a11y subagent for §6 a11y contract)

### Phase 2 — `DefaultQuarterdeckDataProvider` reference impl (~4–5h; 1 PR)

- [ ] `DefaultQuarterdeckDataProvider : IQuarterdeckDataProvider`:
  - Pre-resolves `IPermissionResolver` per §2.3 rule 1 (tenant-scoped cache)
  - Reads `IOodWatchService.GetActiveWatchAsync` for both `OodRole` values via
    `Task.WhenAll`; maps to `OodWatchSummary` **Halt-condition:** W#49 on origin/main
  - Enumerates `IStandingOrderRepository.EnumerateAsync` + client-side filter per §2.3
  - Aggregates `IEnumerable<IQuarterdeckAlertSource>` for `PendingAlerts`; applies
    `AlertVisibilityPolicy` per §2.3 rule 5
  - Reads `IMissionEnvelopeProvider` + maps to `MissionEnvelopeSummary` per §1 projection
  - Aggregates `IEnumerable<IDepartmentKpiSource>` for `KpiCards`
  - All source calls via `Task.WhenAll` (§2.3 rule 7)
- [ ] Add ProjectReference to `Sunfish.Foundation.Ship.Common` (post-W#46 Phase 1)
- [ ] `SubscribeSnapshotAsync`: 30s heartbeat + Channel capacity-1 + push on state change
  per §2.1 subscription semantics
- [ ] `DefaultQuarterdeckCommandService : IQuarterdeckCommandService` (AcknowledgeAlertAsync)
- [ ] Unit tests (6 minimum): `GetSnapshot_AllDepartmentsPresent`,
  `GetSnapshot_DeniedDepartment_SurfacesDenialReason`,
  `GetSnapshot_NoActiveWatch_ReturnsNull`,
  `GetSnapshot_AlertsOmittedForDeniedActor`,
  `Subscribe_Heartbeat_EmittedEvery30s`,
  `AcknowledgeAlert_EmitsPreAndPostAuditEvents`
- [ ] Pre-merge council (mandatory; security-engineering subagent for §5.2 tenant binding
  + §5.3 source uniqueness)

### Phase 3 — `blocks-quarterdeck` UI block (~5–7h; 2 PRs)

**Phase 3a:** TopDeck — watch status region + alert ticker + KPI cards (read-only)
- [ ] `WatchStatusPanel` — OOD watch region per §3 + §6 a11y contract;
  `role="region" aria-label="Watch status"` (NOT `role="banner"`);
  watch-handover `role="alertdialog"` with focus-trap + pre-select interaction model
  **Halt-condition:** W#49 Phase 1 on origin/main
- [ ] `AlertTickerPanel` — two sibling live regions (`assertive`/`polite`) with
  `aria-atomic="false" aria-relevant="additions"`; pause button (`aria-pressed`-only pattern);
  `prefers-reduced-motion` defaults to paused; assertive region always active per §4
- [ ] `KpiCardGrid` — `<ul role="list">` + per-card `<li role="listitem">` + accessible
  names per §6; explicit `role="list"` required (WebKit list-style:none gotcha)
- [ ] Add ProjectReference to `Sunfish.Foundation.Wayfinder` (for `IOodWatchService`)
- [ ] `ILiveAnnouncer`, `IFocusTrap` from `Sunfish.UICore` **Halt-condition:** W#46 Phase 3
- [ ] WCAG/a11y subagent review (mandatory before Phase 3a merge)
- [ ] Pre-merge council (mandatory)

**Phase 3b:** MainDeck — department nav + search + recent orders + mission envelope
- [ ] `DepartmentNavPanel` — `<nav aria-label="Departments">` landmark; Denied renders as
  `<button aria-disabled="true">` with visible denial reason + click/keyboard suppressed per §6
- [ ] `SearchPanel` — `ISearchAsYouType` combobox per §6; sr-only sibling result-count
  region; `aria-activedescendant` IDs per `quarterdeck-search-result-{stableKey}` convention
- [ ] `RecentOrdersPanel` — last 5 Standing Orders; link to Wayfinder for detail
- [ ] `MissionEnvelopePanel` — status badge from `IMissionEnvelopeProvider`
- [ ] Skip-link `<a href="#main-content">` as first focusable element; `<main
  id="main-content" tabindex="-1">` per §6
- [ ] WCAG/a11y subagent review (mandatory)
- [ ] Pre-merge council (mandatory)

### Phase 4 — Anchor wiring + apps/docs + changelog (~2h; 1 PR)

- [ ] Register `DefaultQuarterdeckDataProvider` + `DefaultQuarterdeckCommandService` in
  Anchor's `MauiProgram.cs`
- [ ] Register `blocks-quarterdeck` panels in Anchor's navigation shell
  (root navigation target; `ShipLocation.Quarterdeck`)
- [ ] `apps/docs/foundation/quarterdeck/overview.md` — usage guide
- [ ] XML docs on all public types
- [ ] Changelog entry (user-facing)
- [ ] Update `icm/_state/active-workstreams.md` W#51 row to `built`
- [ ] Standard review

---

## Open questions

1. **`OnWatchActorDisplayName` resolution.** `OodWatch` (ADR 0078) stores `ActorId`, not
   a display name. The default provider needs an actor-directory lookup to resolve display
   names. ADR 0078 §2.3 should specify the actor-directory contract; until then, Phase 2
   implementer MUST document the resolution source. If no actor-directory contract exists,
   file an intake before Phase 2 begins.

2. **KPI card data sources.** `IDepartmentKpiSource` is now defined in §2. The Engine Room
   KPI source will be `IEngineRoomDataProvider.GetHealthSummaryAsync` (ADR 0079) adapted to
   `IDepartmentKpiSource`. Other departments supply their own sources when their ADRs ship.

3. **Tenant switcher (Bridge multi-tenant Quarterdeck).** The intake describes a "tenant
   switcher above the sidebar" for Bridge. Out of scope for Phase 1. Open a separate intake
   when the Bridge adapter ADR is in flight.

---

## Revisit triggers

- **ADR 0081 (Tactical)** — verify `IQuarterdeckAlertSource` registration + `SourceName`
  uniqueness when Tactical ships
- **W#46 amendments** — if `ShipAction` catalog or `IPermissionResolver` signature changes
  post-W#46 build, amend §5 + §A0.4
- **W#49 amendments** — if `IOodWatchService` method signatures change post-W#49 build,
  amend §A0.4 + Phase 3 halt-condition

---

## Pre-acceptance audit

- [x] **AHA pass.** Options A + C rejected. Option B consistent with Engine Room precedent
  (ADR 0079).
- [x] **FAILED conditions.** Reverse if: (a) W#46 `ShipAction` catalog is closed; (b)
  `IOodWatchService.HandoverWatchAsync` signature differs from §A0.4 (amend before Phase 3).
- [x] **Rollback strategy.** Both new packages are additive. Rollback = revert W#51 build
  PRs. `foundation-ship-common` changes (3 new ShipAction constants + 3 AuditEventType
  constants) are additive and removable.
- [x] **Confidence level.** HIGH for §1 data model + §3 watch banner + §4 alert ticker.
  MEDIUM for `DefaultQuarterdeckDataProvider` fanout latency (1s target for 6 sources
  requires `Task.WhenAll` — not sequential awaits; achievable).
- [x] **Cited-symbol verification.** §A0 covers. `IPermissionResolver` + `ShipLocation`
  + `DeckDepth` + `Principal` + `IStandingOrderRepository` (EnumerateAsync verified)
  + `IMissionEnvelopeProvider` all verified on origin/main. Forward-refs named in §A0.2
  with expected signatures in §A0.4.
- [x] **Anti-pattern scan.** AP-1: Open Q1 + Q2 resolved in this amendment. AP-21
  (cited-symbol drift): §A0 covers. AP-3 (vague success criteria): checklist is observable.
- [x] **Cold Start Test.** A fresh contributor can execute Phases 1–4 from the checklist;
  halt-conditions name W#46 + W#49 sequencing dependencies.

---

## References

### Predecessor and sister ADRs

- [ADR 0062](./0062-mission-space-negotiation-protocol.md) — Mission Envelope provider +
  `FeatureAvailabilityState` / `ProbeStatus` (used to derive `MissionEnvelopeStatus`)
- [ADR 0065](./0065-wayfinder-system-and-standing-order-contract.md) — Standing Order +
  `IStandingOrderRepository` (EnumerateAsync only — per §A0.1 note)
- [ADR 0077](./0077-shared-design-system.md) — `ShipRole`, `ShipLocation`, `DeckDepth`,
  `IPermissionResolver`, `ILiveAnnouncer`, `IFocusTrap`, `ISearchAsYouType`, design tokens
- [ADR 0078](./0078-ood-watch-rotation.md) — `IOodWatchService`, `OodWatch`, `OodWatchId`,
  `OodRole`, `HandoverWatchAsync(OodWatchId, ActorId)` signature, `OodWatchRelieved` audit event
- [ADR 0079](./0079-engine-room-observability.md) — `IEngineRoomDataProvider.GetHealthSummaryAsync`
  for Engine Room KPI source; two-phase audit pattern; §4.1/§4.2/§4.3 security pattern

### Roadmap and specifications

- W#35 Ship Architecture discovery §5.1 + §8.3 — `icm/01_discovery/output/2026-05-01_ship-architecture.md`
- Quarterdeck Entry-Point intake — `icm/00_intake/output/2026-05-01_quarterdeck-entry-point-intake.md`
- WCAG 2.2 AA (2018) — SC 1.4.1, 2.2.2, 2.3.1, 2.4.1, 2.4.3, 3.3.1, 3.3.2, 3.3.4, 4.1.2, 4.1.3
- ARIA 1.2 §3.3 (landmark roles), §6.6 (combobox pattern), §6.8 (live regions)
- ADR 0034 — a11y harness per adapter
- ADR 0036 — SyncState 5-channel encoding (color + icon + label + ARIA + live-region pattern)

### Existing code / substrates

- `packages/foundation/Capabilities/` — `Principal`, `Resource`, `CapabilityProof`
- `packages/foundation/Assets/Common/TenantId.cs` — TenantId value type
- `packages/foundation-wayfinder/` — `IStandingOrderRepository` (W#42; EnumerateAsync only)
- `packages/foundation-mission-space/` — `IMissionEnvelopeProvider` (W#40 built ✓)

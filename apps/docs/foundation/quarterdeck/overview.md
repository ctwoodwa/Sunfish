# Quarterdeck — Foundation Contracts + Block Implementations

`Sunfish.Foundation.Quarterdeck` + `Sunfish.Blocks.Quarterdeck` deliver the
Quarterdeck entry-point surface per
[ADR 0080](../../../docs/adrs/0080-quarterdeck-entry-point.md).

## Foundation contracts (`Sunfish.Foundation.Quarterdeck`)

### Data provider

```csharp
// One-shot snapshot (Blazor page OnInitializedAsync)
var snapshot = await dataProvider.GetSnapshotAsync(tenantId, actorId, ct);

// Subscription — emits immediately, then on change, then every
// QuarterdeckOptions.HeartbeatInterval (default 60 s).
await foreach (var snap in dataProvider.SubscribeSnapshotAsync(tenantId, actorId, ct))
{
    // snapshot.PendingAlerts, .DepartmentLinks, .OodWatch, etc.
}
```

### QuarterdeckSnapshot

`QuarterdeckSnapshot` is a single authority-materialized payload per session.
Permissions are pre-resolved — the UI layer MUST NOT re-resolve:

| Field | Type | Notes |
|---|---|---|
| `OodWatch` | `OodWatchSummary` | Both roles: OfficerOfTheDeck + EOOW |
| `MissionEnvelope` | `MissionEnvelopeSummary` | Coarse-grain status: Passed / Warning / Failed / Unknown |
| `RecentOrders` | `IReadOnlyList<StandingOrderSummary>` | Last 5, newest-first |
| `PendingAlerts` | `IReadOnlyList<QuarterdeckAlert>` | Permission-filtered; expired non-ack alerts omitted |
| `KpiCards` | `IReadOnlyList<DepartmentKpi>` | Denied cards use neutral value (denied-not-hidden) |
| `DepartmentLinks` | `IReadOnlyList<DepartmentLink>` | One per location; `AccessDecision` pre-stamped |
| `SnapshotAt` | `DateTimeOffset` | Wall-clock assembly timestamp; drives stale detection |

### Alert visibility policy

`QuarterdeckOptions.DefaultAlertVisibility` controls which alerts denied actors see:

- `AlertVisibilityPolicy.OmitForDeniedActors` (default) — security-sensitive alerts hidden from actors without ViewQuarterdeckAlerts permission.
- `AlertVisibilityPolicy.ShowAll` — use for system-wide failure banners visible to all roles.

### Command service

```csharp
// AcknowledgeAlertAsync follows the §Trust ordering invariant:
// 1. IPermissionResolver.ResolveAsync(ShipAction.AcknowledgeAlert, …)
//    → if Denied: emit AlertAcknowledgementDenied + throw EngineRoomUnauthorizedException
// 2. Pre-op audit: AlertAcknowledgementRequested
// 3. Execute acknowledgement
// 4. Post-op audit: AlertAcknowledged
await commandService.AcknowledgeAlertAsync(tenantId, actorId, alertId, ct);
```

### Alert source + KPI source registration

Custom alert sources and KPI sources are registered via DI:

```csharp
// Alert source — SourceName must be globally unique per ADR 0080 §5.3
services.AddSingleton<IQuarterdeckAlertSource, MyAlertSource>();

// KPI source — same uniqueness requirement
services.AddSingleton<IDepartmentKpiSource, MyKpiSource>();
```

`AddSunfishQuarterdeck()` validates uniqueness at startup.

## DI registration

```csharp
// Register the Quarterdeck substrate + default implementations.
// Prerequisites: IPermissionResolver (W#46 P1), IOodWatchService (W#49 P1),
//   IStandingOrderRepository (W#42), IMissionEnvelopeProvider (W#40).
// IQuarterdeckAlertSource + IDepartmentKpiSource resolve empty when none registered.
services.AddSunfishQuarterdeck(opts =>
{
    opts.HeartbeatInterval = TimeSpan.FromSeconds(60);
    opts.ProviderTimeout   = TimeSpan.FromSeconds(10);
});
```

## Block components (`Sunfish.Blocks.Quarterdeck`)

All components are WCAG 2.2 AA + EN 301 549 v3.2.1 compliant.

### DepartmentNavPanel

```razor
<DepartmentNavPanel Links="@snapshot.DepartmentLinks"
                    RenderMainLandmark="true">
    @* Child content rendered inside the <main> or <div id="main-content"> *@
    <SearchPanel SearchProvider="@mySearchProvider"
                 OnResultSelected="@OnResult" />
</DepartmentNavPanel>
```

- Renders a skip-link as the first focusable element → `#main-content`.
- Denied departments render as `aria-disabled` buttons (not hidden) per ADR 0080 §2.3 rule 5.
- `RenderMainLandmark` (default `true`): set to `false` when the host page already owns a `<main>` landmark to prevent duplicate landmarks.

### AlertTickerPanel

```razor
<AlertTickerPanel Alerts="@snapshot.PendingAlerts"
                  TickInterval="@TimeSpan.FromSeconds(5)" />
```

- ARIA live region: urgent alerts → `aria-live="assertive"`, others → `aria-live="polite"`.
- `DefaultPaused="true"` by default — prevents auto-scrolling until user focuses.

### WatchStatusPanel

```razor
<WatchStatusPanel Tenant="@tenantId"
                  Actor="@actorId"
                  Role="@OodRole.OfficerOfTheDeck" />
<WatchStatusPanel Tenant="@tenantId"
                  Actor="@actorId"
                  Role="@OodRole.EngineeringOfficerOfTheWatch" />
```

- Injects `IOodWatchService` directly; does not consume snapshot data.
- Handover dialog uses `role="alertdialog"` with focus trapped on open.

### SearchPanel

```razor
<SearchPanel SearchProvider="@mySearchProvider"
             OnResultSelected="@OnSearchResult" />
```

- ARIA 1.2 combobox pattern: `role="combobox"` + `aria-activedescendant`.
- Implement `ISearchAsYouType<QuarterdeckSearchResult>` for custom result providers.
- `QuarterdeckSearchResult(StableKey, Label, TargetHref?)` — `StableKey` must be
  non-empty and whitespace-free (used as `aria-activedescendant` id suffix).

### RecentOrdersPanel

```razor
<RecentOrdersPanel Orders="@snapshot.RecentOrders"
                   WayfinderBaseUrl="/wayfinder" />
```

- Full-row `<a>` link (SC 2.4.4 Link Purpose).
- Time displayed with timezone marker (multi-timezone ops surface).

### MissionEnvelopePanel

```razor
<MissionEnvelopePanel Envelope="@snapshot.MissionEnvelope"
                      MissionSpaceHref="/mission-space" />
```

- Status badge conveys state via text label AND CSS class (SC 1.4.1 — not color alone).
- `<span role="status" aria-live="polite">` always in DOM for reliable SR monitoring.
- `MissionSpaceHref`: renders "View mission details" link when non-null and status ≠ Unknown.

## WCAG contracts

| Component | Key criterion | Implementation |
|---|---|---|
| `DepartmentNavPanel` | SC 2.4.1 Bypass Blocks | Skip-link → `#main-content` |
| `DepartmentNavPanel` | SC 1.3.1 Info + Relationships | Denied state via `aria-disabled` + `aria-describedby` |
| `AlertTickerPanel` | SC 4.1.3 Status Messages | `aria-live` region per severity |
| `SearchPanel` | ARIA 1.2 combobox | `role="combobox"` + `aria-controls` + `aria-activedescendant` |
| `WatchStatusPanel` | SC 2.1.1 Keyboard | Handover dialog via `role="alertdialog"` + focus trap |
| `MissionEnvelopePanel` | SC 1.4.1 Use of Color | Status text label + CSS class |

## Denied-department rendering policy

Denied departments MUST be visible (not hidden). ADR 0080 §2.3 rule 5:

> "Never hide department existence — hidden departments degrade to 'user cannot
> explain why the system seems incomplete.'"

The `DepartmentNavPanel` enforces this by rendering denied items as `aria-disabled`
buttons that remain in the focus order and have an `aria-describedby` denial reason.

## Tenant binding requirement (§5.2)

All `GetSnapshotAsync` / `SubscribeSnapshotAsync` calls must pass the ambient
`TenantId`. The data provider verifies tenant binding before returning data.
Never pass a default or zero `TenantId` — the provider throws `ArgumentException`.

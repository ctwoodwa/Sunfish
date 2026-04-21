---
uid: block-leases-demo-lease-list
title: Leases — Demo — Lease List
description: How to drop LeaseListBlock into a Blazor page, parameters and data-binding, and guidance for kitchen-sink and docs demos.
keywords:
  - sunfish
  - leases
  - lease-list-block
  - blazor
  - demo
  - kitchen-sink
---

# Leases — Demo — Lease List

## Overview

`LeaseListBlock` is a read-display Blazor component that renders leases returned by
`ILeaseService`. It is the canonical kitchen-sink demo for the leases block: drop it in,
point it at a registered service, and it shows a tabular view of leases.

Source: `packages/blocks-leases/LeaseListBlock.razor`

## What it renders

- A `"Loading leases…"` placeholder while `OnInitializedAsync` is running.
- `"No leases"` when the service returns zero records.
- Otherwise, a table with one row per lease and these columns:
  - Phase
  - Unit (renders `UnitId`)
  - Tenants (comma-joined `PartyId` values)
  - Start date (`yyyy-MM-dd`)
  - End date (`yyyy-MM-dd`)
  - Monthly rent (currency-formatted with `ToString("C")`)

Rows emit `data-lease-id="@lease.Id"` for test selectors.

## Parameters

| Parameter | Type               | Default               | Notes |
|-----------|--------------------|------------------------|-------|
| `Query`   | `ListLeasesQuery`  | `ListLeasesQuery.Empty`| Passed to `ILeaseService.ListAsync`. |

## Dependencies

The component injects:

- `ILeaseService` — must be registered in DI. Calling
  `services.AddInMemoryLeases()` suffices for demo scenarios.

If service resolution fails or `ListAsync` throws, the block catches the exception and
renders an empty list — it is a read-display demo, not a failure diagnostic. For real
admin scenarios, wrap the block in your own error boundary.

## Minimal page

```razor
@page "/admin/leases"
@using Sunfish.Blocks.Leases

<h1>Leases</h1>

<LeaseListBlock Query="@_query" />

@code {
    private ListLeasesQuery _query = new() { Phase = LeasePhase.Active };
}
```

## Styling hooks

The component emits BEM-style class names that callers can target:

| Class                              | Applied to |
|------------------------------------|------------|
| `sf-lease-list`                    | The `<table>` element. |
| `sf-lease-list__loading`           | The loading placeholder. |
| `sf-lease-list__empty`             | The empty-state placeholder. |
| `sf-lease-list__row`               | Each `<tr>` row. |
| `sf-lease-list__col-phase`         | Phase cell / header. |
| `sf-lease-list__col-unit`          | Unit cell / header. |
| `sf-lease-list__col-tenants`       | Tenants cell / header. |
| `sf-lease-list__col-start`         | Start-date cell / header. |
| `sf-lease-list__col-end`           | End-date cell / header. |
| `sf-lease-list__col-rent`          | Monthly-rent cell / header. |

The component does not ship a stylesheet — style these classes from your host app.

## What it does not do

- **No editing.** The block is read-display only; row clicks, inline edits, and phase
  transitions are all out of scope.
- **No pagination.** The entire result set is buffered into a list and rendered in one go.
  For large tenant installations, paginate at the service layer or wrap the output in a
  `SunfishDataGrid` with virtualization.
- **No sort or filter UI.** Filter via the `Query` parameter from the parent component.

These are deliberate choices — `LeaseListBlock` is a kitchen-sink / docs demo, not the
production admin view.

## Swallowed exceptions

The component catches **any** exception thrown by `ILeaseService.ListAsync` and falls back
to the empty-state render. This is deliberate — a kitchen-sink demo should not surface
service errors as a broken page — but it means production admin flows should not depend on
`LeaseListBlock` for error reporting. Wrap it in a proper Blazor
`<ErrorBoundary>` or compose the list yourself when errors must be visible.

```razor
<ErrorBoundary>
    <ChildContent>
        <LeaseListBlock Query="@_query" />
    </ChildContent>
    <ErrorContent>
        <div class="alert alert-danger">Failed to load leases.</div>
    </ErrorContent>
</ErrorBoundary>
```

## Pre-seeding for demo pages

For kitchen-sink pages, seed the in-memory service during startup. Because the in-memory
implementation uses a thread-safe dictionary, you can safely interleave seed and render:

```csharp
// Program.cs
builder.Services.AddInMemoryLeases();

// During startup
var leases = app.Services.GetRequiredService<ILeaseService>();
await leases.CreateAsync(new CreateLeaseRequest
{
    UnitId      = new EntityId("unit", "demo", "3B"),
    Tenants     = [new PartyId("tenant-demo")],
    Landlord    = new PartyId("landlord-demo"),
    StartDate   = new DateOnly(2026, 1, 1),
    EndDate     = new DateOnly(2026, 12, 31),
    MonthlyRent = 1800m,
});
```

## Accessibility

The table does not currently declare ARIA landmarks or caption. Hosts that need a11y
should wrap the block in a `<section aria-label="Leases">` with a visually hidden
`<caption>` — or compose the list directly rather than using the block. Full WAI-ARIA
compliance for kitchen-sink blocks will follow in a cross-cutting pass.

## Test-surface guarantees

`LeaseListBlock` is rendered via bUnit in a future test pass; for now, callers can rely
on:

- The BEM classes above remain stable.
- The `data-lease-id` attribute on each `<tr>` matches `lease.Id.Value`.
- The empty / loading placeholders render exactly the text strings above (useful for
  Playwright locators).

## Related pages

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- [Service Contract](service-contract.md)

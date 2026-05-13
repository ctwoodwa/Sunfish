# Ship's Office

`Sunfish.Blocks.ShipsOffice` is the block-tier aggregation surface for
Ship's Office documents — bundle manifests, lease documents, vendor W9s,
and signature envelopes. It implements
[ADR 0083 — Ship's Office Content Aggregation Surface](../../../docs/adrs/0083-ships-office-content-aggregation.md).

## Components

| Component | Role |
|---|---|
| `ShipsOfficeBlock` | Root composition: search bar + kind-filter chips + document list + detail drawer. Permission-gated (`ViewShipsOffice`). |
| `DocumentListItem` | Per-row view: kind badge + status badge + last-modified. Keyboard-operable. |
| `DocumentDetailDrawer` | Slide-in panel: metadata + role-gated publish/archive buttons. |
| `DocumentDiffPanel` | Accessible diff table (field path / prior / new). Gated on H2 (`DiffPreviewView`). |
| `ShipsOfficeSearchBar` | ARIA APG combobox with `aria-live="polite"` result count. |

## Services

| Type | Role |
|---|---|
| `IShipsOfficeDataProvider` | Browse + search projection across four document sources. |
| `IShipsOfficeCommandService` | Publish + archive commands with `§5` audit-emission ordering. |
| `IDocumentDiffService` | Accessible diff (stub until H2 — `DiffPreviewView` — clears). |
| `IContentEditorSurface` | Content editor plug-point (Phase 5 conditional; `NoopContentEditorSurface` in v1). |

## DI registration

```csharp
// Registers all four implementations with TryAddSingleton.
services.AddSunfishShipsOfficeDefaults();

// Optional: configure options after registration.
services.Configure<ShipsOfficeOptions>(opts =>
{
    opts.SnapshotPageSize = 200;
    opts.FallbackPollingInterval = TimeSpan.FromSeconds(60);
    opts.RequireSecondActorPublish = false; // set true for regulated tenants
});
```

## Security constraints

- `IDocumentDiffService` and `IShipsOfficeDataProvider` MUST NOT reference
  `IFieldDecryptor`. W9 TIN is excluded from `ShipsOfficeDocumentView` by design.
- Publish/archive operations follow the ADR 0083 §5 ordering invariant:
  permission check FIRST → audit pre-op → execute.
- `RequireSecondActorPublish` prevents same-actor publish when `true`.

## Accessibility

WCAG 2.2 AA conformance declaration:
[`apps/docs/design-system/ships-office-wcag.md`](../../design-system/ships-office-wcag.md).

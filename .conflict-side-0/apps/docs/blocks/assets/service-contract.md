---
uid: block-assets-service-contract
title: Assets — Service Contract
description: The Blazor component surface exposed by Sunfish.Blocks.Assets. No standalone service interface is defined in this pass.
keywords:
  - sunfish
  - assets
  - service-contract
  - assetcatalogblock
  - component-api
---

# Assets — Service Contract

## Overview

Unlike most Sunfish domain blocks, `Sunfish.Blocks.Assets` does **not** ship an
`IAssetsService` contract. The block is a thin rendering surface over data the caller
already holds. The effective "contract" is the Blazor component and its parameters.

If you need a persistence-backed asset service, compose `blocks-assets` with
`foundation-assets-postgres` or a similar persistence adapter upstream of this block —
the result is a list of `AssetRecord`s that you pass into the component.

## Component: `AssetCatalogBlock`

Source: `packages/blocks-assets/AssetCatalogBlock.razor`

### Parameters

| Parameter         | Type                               | Default                       | Notes |
|-------------------|------------------------------------|-------------------------------|-------|
| `Items`           | `IReadOnlyList<AssetRecord>`       | `Array.Empty<AssetRecord>()`  | **Required.** The records to render in the grid. |
| `ShowFileManager` | `bool`                             | `true`                        | When `true`, renders `SunfishFileManager` in the right pane. |

`Items` is marked `[EditorRequired]`. The parameter is named `Items` (not `Assets`) to
avoid colliding with `ComponentBase.Assets` in .NET 10.

### Layout

The component renders a two-column CSS grid (`2fr 1fr`) with a `SunfishDataGrid` on the
left and, when `ShowFileManager` is true, a `SunfishFileManager` on the right. The grid is
bound to `Items` with `TItem="AssetRecord"`; no columns are preconfigured in this pass —
default column auto-generation applies.

### Events

No events are raised. The block is read-display only. Editing, uploading, and file
interaction flow through the embedded `SunfishFileManager` via its own event model (see the
[SunfishFileManager component spec](../../component-specs/filemanager/overview.md)).

## Minimal host page

```razor
@page "/admin/assets"
@using Sunfish.Blocks.Assets
@using Sunfish.Blocks.Assets.Models

<h1>Asset catalog</h1>

<AssetCatalogBlock Items="@_records" ShowFileManager="true" />

@code {
    private IReadOnlyList<AssetRecord> _records = Array.Empty<AssetRecord>();

    protected override async Task OnInitializedAsync()
    {
        _records = await LoadRecordsAsync();
    }

    private Task<IReadOnlyList<AssetRecord>> LoadRecordsAsync() => ...;
}
```

## Hiding the file manager

Toggling `ShowFileManager="false"` collapses the right-hand pane but the two-column grid is
still set up — the right column simply renders empty. To reclaim the width, wrap the block
in a host container that overrides the grid template (the block emits
`display:grid; grid-template-columns: 2fr 1fr`).

## Test-surface guarantees

The first-pass smoke tests (`AssetCatalogBlockTests.cs`) assert only that the component and
record types are public and live in the canonical namespaces. This means:

- Namespace moves are a breaking change.
- Renaming `AssetCatalogBlock` or `AssetRecord` is a breaking change.

Anything else — parameter additions, internal layout tweaks — is fair game in subsequent
passes so long as existing parameter names and default values are preserved.

## Planned follow-ups

- A formal `IAssetsService` with query, upload, and link-to-other-block surface.
- Thumbnail / preview rendering.
- Asset-level permissions wired to Foundation.Authorization.
- Row-click/select events and a companion details panel.
- Integration with a storage-abstraction (`IBlobStore`) for the file-manager side of the view.

## Related pages

- [Overview](overview.md)
- [Entity Model](entity-model.md)

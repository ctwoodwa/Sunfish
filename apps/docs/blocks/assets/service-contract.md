---
uid: block-assets-service-contract
title: Assets — Service Contract
description: The Blazor component surface exposed by Sunfish.Blocks.Assets. No standalone service interface is defined in this pass.
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

## Planned follow-ups

- A formal `IAssetsService` with query, upload, and link-to-other-block surface.
- Thumbnail / preview rendering.
- Asset-level permissions wired to Foundation.Authorization.

## Related pages

- [Overview](overview.md)
- [Entity Model](entity-model.md)

---
uid: block-assets-overview
title: Assets — Overview
description: A read-display catalog block for browsing asset records (files, documents, images) alongside a file-manager surface.
keywords:
  - sunfish
  - blocks
  - assets
  - catalog
  - file-manager
  - datagrid
  - read-display
---

# Assets — Overview

## What this block is

`Sunfish.Blocks.Assets` is a small, read-display Blazor block for browsing a flat list of
asset records. It presents a two-pane layout: a `SunfishDataGrid` of asset metadata on the
left and a `SunfishFileManager` on the right. The block is intentionally minimal — it is a
catalog viewer, not an asset-management back end.

The block ships one Razor component (`AssetCatalogBlock`) and a single record type
(`AssetRecord`). Upload, transform, tag, and versioning are all deferred to follow-up work.

The first-pass deliberate decision is to let the block host a caller-provided list rather
than own a service contract. That keeps the block composable with any upstream data source
(filesystem, blob storage, foundation-assets adapter) without forcing a particular
persistence shape on consumers.

## Package

- Package: `Sunfish.Blocks.Assets`
- Source: `packages/blocks-assets/`
- Namespace roots: `Sunfish.Blocks.Assets.Models`
- Razor components: `AssetCatalogBlock.razor`

## When to use it

Use this block when:

- You have an existing list of `AssetRecord` values from a back-end service and want a
  ready-made catalog view without writing grid markup.
- You want a reusable component spec for documents or file-like records that need to render
  alongside a file picker.

Do not use this block when you need upload handling, thumbnail generation, or asset-level
permissions — those are out of scope for this pass.

## Key entities and services

- `AssetRecord` — the single metadata shape (see [entity-model.md](entity-model.md)).
- `AssetCatalogBlock` — the Blazor component; takes `Items` and a `ShowFileManager` flag.

### A note on the `Items` parameter name

The component's primary parameter is named `Items` rather than `Assets` because `Assets`
collides with the `ComponentBase.Assets` property introduced in .NET 10. Callers pass an
`IReadOnlyList<AssetRecord>` to `Items`.

## Usage sketch

```razor
<AssetCatalogBlock Items="@_records" ShowFileManager="true" />

@code {
    private IReadOnlyList<AssetRecord> _records = LoadFromService();
}
```

## Composition patterns

### Feed from a filesystem

For a local-first kitchen-sink demo, project `DirectoryInfo.EnumerateFiles` results into
`AssetRecord`:

```csharp
private IReadOnlyList<AssetRecord> LoadFromDisk(string root)
{
    return new DirectoryInfo(root)
        .EnumerateFiles("*", SearchOption.AllDirectories)
        .Select(f => new AssetRecord
        {
            Id               = f.FullName,
            Name             = f.Name,
            Path             = Path.GetRelativePath(root, f.FullName),
            SizeBytes        = f.Length,
            LastModifiedUtc  = f.LastWriteTimeUtc,
        })
        .ToList();
}
```

### Feed from a persistence-backed asset service

The intended production pattern is to put a persistence-backed asset service (e.g. a
future `foundation-assets` implementation, or a per-app `IAssetSource`) upstream of the
block. The consumer page is responsible for the async load, error handling, and paging
behaviour — the block itself only renders whatever list is passed in.

## Why `Items` and not `Assets`

Blazor's `ComponentBase.Assets` property was introduced in .NET 10. A parameter named
`Assets` on the block would shadow that member. To keep Razor diagnostics clean and avoid
subtle binding surprises, the block's parameter is `Items`. The component-level record
type remains `AssetRecord` to preserve the domain name.

## Related ADRs

- ADR 0017 — Web Components / Lit technical basis. The `AssetCatalogBlock` is framework-local
  (Blazor) today; the underlying `SunfishDataGrid` and `SunfishFileManager` are the migration
  points if the block later ships as a web component.
- ADR 0022 — Example catalog + docs taxonomy. The block is registered under
  `blocks.assets` with docs `overview`, `entity-model`, `service-contract`.

## Related pages

- [Entity Model](entity-model.md)
- [Service Contract](service-contract.md)

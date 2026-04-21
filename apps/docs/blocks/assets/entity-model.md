---
uid: block-assets-entity-model
title: Assets — Entity Model
description: The AssetRecord shape exposed by Sunfish.Blocks.Assets for rendering asset catalogs.
keywords:
  - sunfish
  - assets
  - asset-record
  - entity-model
  - catalog
---

# Assets — Entity Model

## Overview

`Sunfish.Blocks.Assets` exposes a single, deliberately small record shape: `AssetRecord`.
The block does not own a persistence layer in this pass — callers populate records from
whatever upstream source they use (blob store, file system, API).

## AssetRecord

| Field             | Type        | Notes |
|-------------------|-------------|-------|
| `Id`              | `string`    | Required. Caller-assigned identifier; typically a stable hash, GUID, or storage key. |
| `Name`            | `string`    | Required. Display name shown in the grid. |
| `Path`            | `string`    | Required. Logical path (e.g. storage key, relative filesystem path, or URL fragment). |
| `SizeBytes`       | `long`      | File size in bytes. Zero when unknown. |
| `LastModifiedUtc` | `DateTime?` | Optional last-modified timestamp in UTC. |

All fields use `required` or `init;` semantics — records are constructed once and not mutated.

## Relationships

`AssetRecord` is flat; there are no child collections or references to other blocks in this
pass. Hierarchy, tagging, and cross-block references (for example, linking an asset to a
lease document) are follow-up concerns.

## Construction

Record instances are constructed with object-initialiser syntax because every field except
`SizeBytes` and `LastModifiedUtc` is `required`:

```csharp
var record = new AssetRecord
{
    Id              = "s3://example/demo.pdf",
    Name            = "demo.pdf",
    Path            = "demo.pdf",
    SizeBytes       = 1024 * 1024,
    LastModifiedUtc = DateTime.UtcNow,
};
```

Missing a `required` field is a compile-time error, so malformed records cannot be
constructed. The record is not sealed against inheritance for flexibility, but equality
semantics rely on the default record-generated members — avoid adding mutable state when
subclassing.

## Field conventions

- `Id` is opaque to the block. It must be unique within the list that is passed to the
  component; `SunfishDataGrid` uses it for row-tracking and test selectors.
- `Path` is a display/label field and is not resolved by the block. It is typically a
  storage-relative path, a URL fragment, or a human-readable breadcrumb.
- `SizeBytes` defaults to `0`. Callers with unknown sizes (e.g. remote blobs whose size
  hasn't been fetched) can leave it at the default.
- `LastModifiedUtc` is nullable to handle sources that do not carry a timestamp.

## Relationship to future persistence adapters

When a persistence-backed asset source lands (e.g. a `foundation-assets` service or a
per-app adapter), `AssetRecord` is expected to remain the projection type presented to the
block. The adapter would own its own entity shapes and project into `AssetRecord` for
display — keeping the component surface stable while the back end evolves.

## Related pages

- [Overview](overview.md)
- [Service Contract](service-contract.md)

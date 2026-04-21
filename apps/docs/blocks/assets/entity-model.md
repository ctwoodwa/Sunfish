---
uid: block-assets-entity-model
title: Assets — Entity Model
description: The AssetRecord shape exposed by Sunfish.Blocks.Assets for rendering asset catalogs.
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

## Related pages

- [Overview](overview.md)
- [Service Contract](service-contract.md)

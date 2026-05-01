# Sunfish.Blocks.Assets

Asset-catalog block — composes `SunfishDataGrid` and `SunfishFileManager` for a read-display asset view.

**Naming note:** this block is the original UI-only asset catalog (visual surface); the property-operations cluster's physical-equipment domain ships separately as [`Sunfish.Blocks.PropertyEquipment`](../blocks-property-equipment/README.md) per UPF Rule 4 (the `Equipment` rename, 2026-04-28).

## What this ships

### Models

- **`AssetRecord`** — read-display asset entry with name, kind, location, condition, asset photo refs.

### UI

- **`AssetCatalogBlock.razor`** — composes `SunfishDataGrid` (list view) + `SunfishFileManager` (file/photo browser) into a unified asset-catalog surface.

## Cluster role

UI-only catalog block — no domain-services or persistence. Intended for hosts that need a "browse assets" UX without committing to the full property-operations cluster.

The property-operations cluster's `Equipment` domain (lifecycle events, inspections integration, work-order assignment) lives in `blocks-property-equipment`; this block can coexist with it.

## See also

- [apps/docs Overview](../../apps/docs/blocks/assets/overview.md)
- [Sunfish.Blocks.PropertyEquipment](../blocks-property-equipment/README.md) — the property-cluster physical-equipment domain (different scope; coexists)

# SunfishMap Component Spec

## Status

- **Prototype:** API surface exists (`SunfishMap.razor`, `MapModels.cs`); renders static placeholder, no real tile rendering.
- **Architecture decision:** Completed 2026-04-12. See [architecture-decision-tile-engine.md](architecture-decision-tile-engine.md).
- **Engine:** MapLibre GL JS v4+ (BSD-3-Clause) selected for v1.
- **Next:** Spec revision, model expansion, JS adapter, component rewrite.

## Spec Files

| File | Content |
|------|---------|
| [overview.md](overview.md) | Root component API, parameters, usage |
| [layers/overview.md](layers/overview.md) | Layer system overview and shared parameters |
| [layers/tile.md](layers/tile.md) | Tile layer configuration |
| [layers/marker.md](layers/marker.md) | Marker layer, shapes, tooltips |
| [layers/shape.md](layers/shape.md) | GeoJSON shape layer |
| [layers/bubble.md](layers/bubble.md) | Bubble (proportional symbol) layer |
| [events.md](events.md) | OnClick, OnMarkerClick, OnShapeClick, OnZoomEnd, OnPanEnd |
| [architecture-decision-tile-engine.md](architecture-decision-tile-engine.md) | Engine selection and internal architecture |

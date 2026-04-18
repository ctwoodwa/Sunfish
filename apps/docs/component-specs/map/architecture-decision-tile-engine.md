# Architecture Decision: SunfishMap Tile Rendering Engine

**Date:** 2026-04-12
**Status:** Decided
**Deciders:** Chris Wood (project owner), Claude (architecture advisor)

---

## Problem

SunfishMap currently renders a static prototype with no real tile rendering, no geospatial projection, and no interactive pan/zoom. To become a production-grade enterprise Blazor map component, it must integrate a JavaScript-based tile rendering engine via Blazor JS interop.

The decision must answer:

1. Which JS map engine to adopt for v1?
2. Should the public API be engine-neutral or engine-specific?
3. Where is the internal adapter boundary?

Constraints:
- Sunfish is provider-first: components own behavior, providers own visuals.
- Single-developer library — pragmatism over abstraction breadth.
- Must work in Blazor Server, WASM, and Hybrid hosting models.
- Must support tile layers, markers, shapes/GeoJSON, and bubble layers (matching spec).
- Must not leak engine-specific terminology into the long-term public API.
- Licensing must be permissive (The Unlicense project).

---

## Decision

**Engine: MapLibre GL JS v4+ (BSD-3-Clause)**

MapLibre GL JS is selected as the v1 tile rendering engine for SunfishMap.

**API: Engine-neutral public surface with internal adapter**

The public Blazor API uses Sunfish-native types (`MapLayer`, `MapMarker`, `MapCenter`, etc.). An internal `IMapEngineAdapter` interface mediates between the Blazor component and the JS engine. The MapLibre adapter is the only v1 implementation but the boundary exists so a future engine swap does not break consumers.

---

## Why This Choice

### MapLibre GL JS

| Factor | MapLibre GL JS | Leaflet | OpenLayers |
|--------|---------------|---------|------------|
| Rendering | WebGL — vector tiles, smooth zoom, rotation, 3D terrain | DOM/Canvas — raster tiles, limited vector support | Canvas/WebGL — full GIS toolkit |
| Vector tiles | Native (MVT, GeoJSON, raster) | Plugin only (leaflet-maplibre-gl) | Supported but heavier API |
| Bundle size | ~210 KB gzipped | ~40 KB gzipped | ~280 KB gzipped |
| License | BSD-3-Clause | BSD-2-Clause | BSD-2-Clause |
| Ecosystem | Active fork of Mapbox GL JS; growing community, CNCF-adjacent | Massive ecosystem, aging API | GIS-heavy, steep learning curve |
| Performance at scale | 60fps pan/zoom on 100k+ features | Degrades past ~10k markers | Strong but API complexity tax |
| Style specification | MapLibre Style Spec (JSON) — rich, declarative | CSS-based, limited | SLD/OL style objects |
| Clustering | Built-in source-level clustering | Plugin (leaflet.markercluster) | Built-in |
| 3D / terrain | Supported (terrain, hillshade) | Not supported | Limited |

**Why not Leaflet:** Leaflet is lighter and simpler but lacks native WebGL rendering, vector tile support, and smooth sub-pixel zoom. For an enterprise component library targeting modern browsers, WebGL rendering is table-stakes. Leaflet's plugin ecosystem could fill some gaps but introduces dependency fragmentation.

**Why not OpenLayers:** OpenLayers is a full GIS toolkit with excellent projection and format support. However, its API surface is significantly larger and more GIS-oriented than what an enterprise UI component library needs. The complexity/benefit ratio is unfavorable for a single-developer library. OpenLayers remains a valid future alternative if Sunfish ever needs advanced CRS/projection support beyond EPSG:3857.

**Why MapLibre wins:**
- WebGL rendering provides the smooth, modern UX expected of enterprise components.
- Vector tile support opens the door to Mapbox/MapTiler/PMTiles styles without raster fallback.
- BSD-3-Clause is compatible with The Unlicense project.
- Style Spec (JSON) provides a clean declarative layer for theming integration.
- Active maintenance (monthly releases, 4k+ GitHub stars, CNCF landscape project).
- The API is well-documented and has a clear JS interop surface.

---

## Alternatives Considered

| Alternative | Disposition |
|------------|-------------|
| **No engine (keep prototype)** | Rejected. The prototype is not usable for production; map without tiles is a placeholder, not a component. |
| **Leaflet** | Deferred. Could be offered as a lightweight adapter in the future for consumers who don't need WebGL. |
| **OpenLayers** | Deferred. Could be offered for GIS-heavy use cases. Adapter boundary supports this. |
| **Mapbox GL JS** | Rejected. Proprietary license (requires Mapbox access token for any use). MapLibre is the open-source fork. |
| **Google Maps JS API** | Rejected. Proprietary, usage-based pricing, no self-hosting. |

---

## Public API Boundary

### Root Component

```razor
<SunfishMap Center="@center"
           Zoom="5"
           MinZoom="1"
           MaxZoom="18"
           Zoomable="true"
           Pannable="true"
           Width="100%"
           Height="400px"
           OnClick="@HandleMapClick"
           OnZoomEnd="@HandleZoomEnd"
           OnPanEnd="@HandlePanEnd">
    <MapLayers>
        <!-- layers declared here -->
    </MapLayers>
</SunfishMap>
```

### Camera / Viewport

| Parameter | Type | Description |
|-----------|------|-------------|
| `Center` | `MapCenter` | Geographic center (Latitude, Longitude). |
| `Zoom` | `double` | Zoom level (0-22). |
| `MinZoom` | `double` | Minimum allowed zoom. |
| `MaxZoom` | `double` | Maximum allowed zoom. |
| `Bounds` | `MapBounds?` | Optional bounding box to constrain the viewport. |
| `Zoomable` | `bool` | Whether user zoom is enabled. |
| `Pannable` | `bool` | Whether user panning is enabled. |
| `Width` | `string` | CSS width of the container. |
| `Height` | `string` | CSS height of the container. |

### Tile Layer

```razor
<MapLayer Type="MapLayerType.Tile"
          UrlTemplate="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          Subdomains='@(new[] { "a", "b", "c" })'
          Attribution="&copy; OSM contributors"
          MinZoom="0"
          MaxZoom="19"
          Opacity="1.0" />
```

**Note:** The `UrlTemplate` placeholder syntax uses `{s}`, `{z}`, `{x}`, `{y}` (industry standard). The adapter translates this to MapLibre's source format internally. The legacy `#= subdomain #` syntax from the spec is deprecated in favor of the standard form.

### Marker Layer

```razor
<MapLayer Type="MapLayerType.Marker"
          Data="@markers"
          LocationField="@nameof(MyModel.LatLng)"
          TitleField="@nameof(MyModel.Title)"
          Shape="MapMarkerShape.Pin">
    <MapLayerMarkerSettings>
        <Template>
            @{ var item = (MyModel)context.DataItem; }
            <div>@item.Title</div>
        </Template>
    </MapLayerMarkerSettings>
</MapLayer>
```

Markers render as MapLibre symbol layers (or HTML markers for custom templates). The component translates the `Data` collection + field names into GeoJSON features internally.

### Shape / GeoJSON Layer

```razor
<MapLayer Type="MapLayerType.Shape"
          Data="@geoJsonString">
    <MapLayerShapeSettings>
        <MapLayerShapeSettingsStyle>
            <MapLayerShapeSettingsStyleFill Color="#0000ff" Opacity="0.5" />
            <MapLayerShapeSettingsStyleStroke Color="#ffffff" Width="1" />
        </MapLayerShapeSettingsStyle>
    </MapLayerShapeSettings>
</MapLayer>
```

GeoJSON data is passed as a string and forwarded to MapLibre as a GeoJSON source.

### Bubble Layer

```razor
<MapLayer Type="MapLayerType.Bubble"
          Data="@bubbleData"
          LocationField="@nameof(BubbleModel.LatLng)"
          ValueField="@nameof(BubbleModel.Revenue)"
          MinSize="5"
          MaxSize="40">
    <MapLayerBubbleSettings>
        <MapLayerBubbleSettingsStyle>
            <MapLayerBubbleSettingsStyleFill Color="#0000ff" />
        </MapLayerBubbleSettingsStyle>
    </MapLayerBubbleSettings>
</MapLayer>
```

Bubble layers render as MapLibre circle layers with data-driven radius based on `ValueField`.

### Events

| Event | Args Type | Description |
|-------|-----------|-------------|
| `OnClick` | `MapClickEventArgs` | Click on the map canvas. Includes `Location` (lat/lng). |
| `OnMarkerClick` | `MapMarkerClickEventArgs` | Click on a marker. Includes `DataItem`. |
| `OnShapeClick` | `MapShapeClickEventArgs` | Click on a shape/bubble. Includes `DataItem` or `GeoJsonDataItem`. |
| `OnZoomEnd` | `MapZoomEndEventArgs` | Zoom finished. Includes `Zoom`, `Center`, `Extent`. |
| `OnPanEnd` | `MapPanEndEventArgs` | Pan finished. Includes `Center`, `Extent`. |

### Escape Hatch Policy

For advanced users who need direct MapLibre access:

```csharp
/// <summary>
/// Invokes a custom JS function with the underlying MapLibre map instance.
/// The function receives (mapInstance, args) and runs in the JS module scope.
/// </summary>
[Parameter] public Func<IJSObjectReference, Task>? OnMapReady { get; set; }
```

This provides a controlled escape hatch without exposing the MapLibre API surface in the Blazor type system. Consumers who use it accept engine coupling.

---

## Internal Design

### Architecture Layers

```
┌──────────────────────────────────────────────┐
│  SunfishMap.razor  (Blazor component)         │
│  - Parameters, events, child content         │
│  - Layer registration via CascadingValue      │
│  - Lifecycle: OnAfterRenderAsync init/update  │
├──────────────────────────────────────────────┤
│  IMapEngineAdapter  (internal interface)      │
│  - InitializeAsync(element, options)          │
│  - UpdateViewportAsync(center, zoom, bounds)  │
│  - AddLayerAsync(layerDescriptor)             │
│  - RemoveLayerAsync(layerId)                  │
│  - UpdateLayerAsync(layerDescriptor)          │
│  - DisposeAsync()                             │
├──────────────────────────────────────────────┤
│  MapLibreAdapter  (internal class)           │
│  - Owns IJSObjectReference to JS module       │
│  - Translates MapLayerDescriptor → ML options │
│  - Handles JS→.NET callbacks via [JSInvokable]│
├──────────────────────────────────────────────┤
│  maplibre-adapter.js  (JS module)            │
│  - import maplibregl from 'maplibre-gl'       │
│  - export { init, updateViewport,             │
│    addLayer, removeLayer, updateLayer,        │
│    destroy }                                  │
│  - Event forwarding to DotNetObjectReference  │
└──────────────────────────────────────────────┘
```

### Key Internal Types

```csharp
// Internal — not part of public API
internal interface IMapEngineAdapter : IAsyncDisposable
{
    Task InitializeAsync(ElementReference container, MapInitOptions options);
    Task UpdateViewportAsync(MapViewport viewport);
    Task AddLayerAsync(MapLayerDescriptor layer);
    Task RemoveLayerAsync(string layerId);
    Task UpdateLayerAsync(MapLayerDescriptor layer);
}

internal record MapInitOptions(
    double CenterLat, double CenterLng, double Zoom,
    double MinZoom, double MaxZoom,
    bool Zoomable, bool Pannable);

internal record MapViewport(
    double CenterLat, double CenterLng, double Zoom);

internal record MapLayerDescriptor(
    string Id, MapLayerType Type,
    string? UrlTemplate, string[]? Subdomains, string? Attribution,
    string? GeoJsonData, object? Data,
    string? LocationField, string? TitleField, string? ValueField,
    double Opacity, double MinZoom, double MaxZoom,
    MapLayerStyleDescriptor? Style);
```

### JS Module Lifecycle

1. **Init:** `OnAfterRenderAsync(firstRender: true)` calls `adapter.InitializeAsync(element, options)`. The JS module lazy-loads MapLibre GL JS from a bundled or CDN source.
2. **Update:** `OnParametersSet` diffs the current layer descriptors against the previous set. Changed layers trigger `updateLayer`; added/removed layers trigger `addLayer`/`removeLayer`. Viewport changes trigger `updateViewport`.
3. **Events:** The JS module registers MapLibre event listeners and calls `DotNetObjectReference.InvokeMethodAsync` to forward events to the Blazor component. The component translates JS event data into typed `EventArgs` and invokes the appropriate `EventCallback`.
4. **Dispose:** `IAsyncDisposable.DisposeAsync()` calls `adapter.DisposeAsync()` which invokes `destroy` on the JS module, removes the map instance, and releases the `IJSObjectReference`.

### Layer Registration

Follows the existing Sunfish `CascadingValue` + child-registration pattern (same as `SunfishDataGrid` → `SunfishGridColumn`):

- `SunfishMap` provides itself as a `CascadingValue` to `MapLayers` → `MapLayer` children.
- Each `MapLayer` registers with the parent in `OnInitialized` and unregisters in `Dispose`.
- `SunfishMap` maintains an internal `List<MapLayer>` and rebuilds `MapLayerDescriptor` list on parameter changes.

### Provider Integration

The tile rendering engine is **not** a provider concern. MapLibre owns the canvas rendering. Providers influence:

- Container CSS classes (`mar-map`, `mar-map__controls`, etc.) via `ISunfishCssProvider`.
- Control positioning and button styling (zoom +/- buttons, attribution placement).
- Marker popup and tooltip styling.
- Dark mode: MapLibre styles can be swapped via a `StyleUrl` parameter or by applying a dark-mode MapLibre Style JSON. The provider does not own the map style but may supply a default style URL per theme.

---

## Blazor-Specific Concerns

### JS Interop Lifecycle

- **Blazor Server:** JS interop calls cross the SignalR circuit. Large GeoJSON payloads must be chunked or loaded client-side from a URL. `MapLayer.DataUrl` parameter (load GeoJSON from URL in JS, not via SignalR) should be offered for Shape layers.
- **Blazor WASM:** No circuit overhead but bundle size matters. MapLibre GL JS (~210KB gzip) should be loaded lazily on first map render, not at app startup.
- **Blazor Hybrid (MAUI):** WebView2 supports WebGL. No special handling needed beyond standard JS interop.

### Reconnection

On Blazor Server reconnect, the JS map state is lost. `OnAfterRenderAsync` must detect reconnection and re-initialize the map. The `_isInitialized` flag pattern (same as Editor, AllocationScheduler) handles this.

### Prerendering

During SSR/prerendering, JS interop is unavailable. The component renders the container div with correct dimensions and a loading state. MapLibre initializes on the interactive render pass.

### Element Reference

`SunfishMap` must capture `@ref` on the container div and pass it to the adapter. The container must have explicit `width` and `height` before MapLibre initializes (MapLibre requires a sized container).

---

## Deferred Scope

These features are explicitly **not in v1** but the architecture supports adding them later:

| Feature | Rationale for deferral |
|---------|----------------------|
| **Clustering** | MapLibre supports source-level clustering natively. Add `Cluster="true"` + `ClusterRadius` to `MapLayer` when needed. |
| **Vector tile styles** | `StyleUrl` parameter on `SunfishMap` would allow loading a full MapLibre Style JSON. Deferred until theming integration is designed. |
| **Drawing / editing** | MapLibre Draw plugin. Requires complex state management. Defer to v2. |
| **3D terrain / hillshade** | MapLibre supports it but adds complexity. Defer. |
| **Heatmap layer** | MapLibre native heatmap layer. Add as `MapLayerType.Heatmap` later. |
| **Image / video overlay** | MapLibre supports image sources. Low priority. |
| **Route / direction rendering** | Application-level concern, not component-level. |
| **Offline tiles** | PMTiles support in MapLibre. Defer. |
| **Custom projections** | MapLibre is EPSG:3857 only. OpenLayers adapter would be needed for other CRS. |
| **Leaflet adapter** | Lightweight alternative. Architecture supports it via `IMapEngineAdapter`. |
| **Animated transitions** | `flyTo`, `easeTo` — add as methods on SunfishMap ref after v1. |

---

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **MapLibre GL JS WebGL requirement** | Low | Medium | All modern browsers support WebGL. Fallback: render a static image or "WebGL not supported" message. |
| **Large GeoJSON over SignalR** | Medium | High | Offer `DataUrl` parameter so JS loads data directly. Document size guidance. |
| **MapLibre breaking changes** | Low | Medium | Pin to a major version range. Adapter boundary isolates Blazor API from engine API. |
| **Bundle size concern** | Medium | Low | Lazy-load MapLibre on first map render. Tree-shaking not available for GL libs, but 210KB gzip is acceptable for an enterprise component. |
| **JS interop perf on frequent updates** | Medium | Medium | Batch layer updates. Debounce viewport change events. Avoid per-frame interop calls. |
| **SSR / prerendering flash** | Low | Low | Render a skeleton/loading state during SSR. Initialize on interactive pass. |

---

## Next Steps

1. **Spec revision:** Update `docs/component-specs/map/overview.md` to reflect the MapLibre decision, new `UrlTemplate` placeholder syntax, and v1 scope. Update layer specs to align parameter names with this decision.
2. **Models expansion:** Extend `MapModels.cs` with `MapBounds`, `MapClickEventArgs`, `MapZoomEndEventArgs`, `MapPanEndEventArgs`, `MapMarkerClickEventArgs`, `MapShapeClickEventArgs`, `MapLayerType` enum, `MapMarkerShape` enum.
3. **JS module:** Create `wwwroot/js/maplibre-adapter.js` with `init`, `updateViewport`, `addLayer`, `removeLayer`, `updateLayer`, `destroy` exports.
4. **Adapter implementation:** Create `MapLibreAdapter.cs` implementing `IMapEngineAdapter`.
5. **Component rewrite:** Replace the prototype `SunfishMap.razor` with the real component using `MapLayers`/`MapLayer` child registration and the adapter.
6. **Provider CSS:** Add `_map.scss` to FluentUI and Bootstrap providers for container, controls, and popup styling.
7. **Demo update:** Replace the prototype demo with a real tile-rendering demo using OSM tiles.
8. **Tests:** Add bUnit tests for layer registration, parameter binding, and event wiring. JS interop calls can be mocked via the adapter interface.

---

## References

- [MapLibre GL JS](https://maplibre.org/maplibre-gl-js/docs/) — BSD-3-Clause
- [MapLibre Style Specification](https://maplibre.org/maplibre-style-spec/)
- [Leaflet](https://leafletjs.com/) — BSD-2-Clause (deferred alternative)
- [OpenLayers](https://openlayers.org/) — BSD-2-Clause (deferred alternative)
- [GeoJSON Specification](https://geojson.org/)
- Existing Sunfish JS interop patterns: Editor (inline IIFE), AllocationScheduler (module + ResizeObserver), DataGrid (IIFE)

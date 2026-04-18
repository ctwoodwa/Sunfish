---
title: Overview
page_title: Map Overview
description: The Blazor maps are interactive web maps using the Blazor framework, which seamlessly integrate C# and NET.
slug: components/map/overview
tags: sunfish,blazor,map,overview
published: True
position: 0
components: ["map"]
---
# Blazor Map Overview

The <a href="https://sunfish.dev/blazor-ui/map" target="_blank">Blazor Map component</a> displays geospatial information organized in layers. The component provides [tile layers](slug:components/map/layers/tile), [vector shape layers](slug:components/map/layers/shape), [bubble layers](slug:components/map/layers/bubble), and [marker layers](slug:components/map/layers/marker).

The Map uses [MapLibre GL JS](https://maplibre.org/) as its tile rendering engine, providing WebGL-accelerated rendering with support for vector tiles, raster tiles, and smooth pan/zoom interactions. See [architecture-decision-tile-engine.md](architecture-decision-tile-engine.md) for the full architecture decision record.

## Creating Blazor Map

1. Use the `SunfishMap` tag to add the component to a Razor file.
1. Add a `<MapLayer>` tag nested inside `<MapLayers>`. Set its `Type` to `MapLayerType.Tile`.
1. Set the [`UrlTemplate` parameter](slug:components/map/layers#maplayer-parameters) of the tile layer.
1. (optional) Set the `Attribution` and `Subdomains` parameters, depending on the specific tile provider.

>caption Basic Sunfish Map for Blazor

````RAZOR
<SunfishMap Center="@MapCenter"
           Zoom="3">
    <MapLayers>
        <MapLayer Type="@MapLayerType.Tile"
                  Attribution="@LayerAttribution"
                  Subdomains="@LayerSubdomains"
                  UrlTemplate="@LayerUrlTemplate">
        </MapLayer>
    </MapLayers>
</SunfishMap>

@code {
    private MapCenter MapCenter { get; set; } = new() { Latitude = 30.268107, Longitude = -97.744821 };

    private readonly string[] LayerSubdomains = new string[] { "a", "b", "c" };
    private const string LayerUrlTemplate = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
    private const string LayerAttribution = "&copy; <a href='https://osm.org/copyright'>OpenStreetMap contributors</a>";
}
````

### UrlTemplate Syntax

The `UrlTemplate` parameter uses industry-standard placeholders:

| Placeholder | Description |
| --- | --- |
| `{s}` | Subdomain (rotated from the `Subdomains` array) |
| `{z}` | Zoom level |
| `{x}` | Tile X coordinate |
| `{y}` | Tile Y coordinate |

Example: `"https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"`

> The legacy `#= subdomain #` / `#= zoom #` / `#= x #` / `#= y #` inline syntax is deprecated. Use the `{s}/{z}/{x}/{y}` placeholders instead. The adapter translates these to the engine's native source format internally.

## Layers

The layers are responsible for organizing the Map information. [Read more about the supported Blazor Map layers...](slug:components/map/layers)

## Markers

You can add different points with real coordinates on the map as markers. [Read more about the Blazor Map Markers...](slug:components/map/layers/marker)

## Pan and Zoom

The zoom operation can be performed with a double click on the map or by using the mouse scroll wheel. You can set the zoom level through the `Zoom` property.

The end user can pan the control by simply holding the left mouse button and dragging the map to a desired location.

Raster maps are divided into images (tiles) for serving over the web. Tiles are typically 256px squares. The top level (zoom level 0) displays the whole world as a single tile. Each progressive zoom level doubles the size of the Map.

Blazor Map also incorporates a navigation tool allowing the end user to easily zoom, pan and change the current view. You can change the navigation tool position by using the `MapControlsNavigator.Position` enum.

## Events

The Blazor Map generates events that you can handle and further customize its behavior. [Read more about the Blazor Map events...](slug:components/map/events).

## Map Parameters

The Blazor Map provides various parameters that allow you to configure the component:

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Parameter | Type | Description |
| --- | --- | --- |
| `Center` | `MapCenter` | The map center. `MapCenter` has `Latitude` and `Longitude` properties. |
| `Zoom` | `double` | The initial zoom level. Typical web maps use zoom levels from 0 (the whole world) to 22 (sub-meter features). |
| `MinZoom` | `double` | The minimum zoom level. |
| `MaxZoom` | `double` | The maximum zoom level. |
| `Bounds` | `MapBounds?` | Optional bounding box to constrain the viewport. `MapBounds` has `NorthEast` and `SouthWest` properties (each a `MapCenter`). |
| `Pannable` | `bool` | Controls whether the user can pan the map. |
| `Zoomable` | `bool` | Controls whether the map zoom level can be changed by the user. |
| `Class` | `string` | Specifies the class of the main DOM element. |
| `Width` | `string` | Specifies the width of the main DOM element. |
| `Height` | `string` | Specifies the height of the main DOM element. |

### MapControls parameters

The following `MapControlsAttribution` parameters enable you to customize the appearance of the Blazor Map Controls:

| Parameter | Type | Description |
| --- | --- | --- |
| `Position` | `MapControlsPosition (enum)` | Specifies the position of the attribution control. |

The following `MapControlsNavigator` parameters enable you to customize the appearance of the Blazor Map Controls:

| Parameter | Type | Description |
| --- | --- | --- |
| `Position` | `MapControlsPosition (enum)` | Specifies the position of the navigation control. |

The following `MapControlsZoom` parameters enable you to customize the appearance of the Blazor Map Controls:

| Parameter | Type | Description |
| --- | --- | --- |
| `Position` | `string` | Specifies the position of the zoom control. |

## Map Reference and Methods

The Map exposes a `Refresh` method. Use it to redraw the component after making programmatic changes that do not apply automatically.

>caption Get the Map reference and use its methods

````RAZOR
<SunfishButton OnClick="@ChangeMapZoom">Change Map Zoom</SunfishButton>

<SunfishMap @ref="MapRef"
           Center="@MapCenter"
           Zoom="@MapZoom">
    <MapLayers>
        <MapLayer Type="@MapLayerType.Tile"
                  Attribution="@LayerAttribution"
                  Subdomains="@LayerSubdomains"
                  UrlTemplate="@LayerUrlTemplate">
        </MapLayer>
    </MapLayers>
</SunfishMap>

@code {
    private SunfishMap? MapRef { get; set; }

    private double MapZoom { get; set; } = 4;

    private MapCenter MapCenter { get; set; } = new() { Latitude = 30.268107, Longitude = -97.744821 };

    private readonly string[] LayerSubdomains = new string[] { "a", "b", "c" };
    private const string LayerUrlTemplate = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
    private const string LayerAttribution = "&copy; <a href='https://osm.org/copyright'>OpenStreetMap contributors</a>";

    private void ChangeMapZoom()
    {
        MapZoom = 1;

        MapRef?.Refresh();
    }
}
````

## Next Steps

* [Configure Map Layers](slug:components/map/layers)
* [Handle Map Events](slug:components/map/events)

## See Also

* [Live Demo: Map](https://demos.sunfish.dev/blazor-ui/map/overview)
* [Architecture Decision: Tile Engine](architecture-decision-tile-engine.md)

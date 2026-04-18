---
title: Overview
page_title: Map Layers
description: Discover the Blazor Map Layers and explore the examples.
slug: components/map/layers
tags: sunfish,blazor,map,layers
published: True
position: 0
components: ["map"]
---
# Map Layers

The information that the Map renders is organized into layers. These layers are stacked from bottom to top in the order of definition and are oblivious to each other.

## Supported Layer Types

The layers in the Map are:

* [Tile](slug:components/map/layers/tile)
* [Marker](slug:components/map/layers/marker)
* [Shape](slug:components/map/layers/shape)
* [Bubble](slug:components/map/layers/bubble)

## MapLayer Parameters

The following parameters enable you to customize the appearance of the Blazor Map Layers:

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Parameter | Type | Description |
| --- | --- | --- |
| `Type` | `MapLayerType` enum | The type of the layer (`Tile`, `Marker`, `Shape`, `Bubble`). |
| `Attribution` | `string` | The attribution for the layer. |
| `Data` | `object` | The data for the layer. |
| `Extent` | `double[]` | Specifies the extent of the region covered by this layer. The layer will be hidden when the specified area is out of view. Accepts a four-element array that specifies the extent covered by this layer: North-West latitude, longitude, South-East latitude, longitude. |
| `LocationField` | `string` | The data item field which contains the marker (symbol) location. The field should be an array with two numbers - latitude and longitude in decimal degrees. |
| `MaxSize` | `double` | The maximum symbol size for bubble layer symbols. |
| `MaxZoom` | `double` | The maximum zoom level at which to show this layer. |
| `MinSize` | `double` | The minimum symbol size for bubble layer symbols. |
| `MinZoom` | `double` | The minimum zoom level at which to show this layer. |
| `Opacity` | `double` | The opacity for the layer. |
| `Shape` | `MapMarkerShape` enum | The marker shape for marker layers (`Pin`, `PinTarget`). |
| `Subdomains` | `string[]` | A list of subdomains to use for loading tiles. Using multiple subdomains allows more requests to be executed in parallel. Also see `UrlTemplate`. |
| `Symbol` | `MapLayersSymbol` enum | The default symbol for bubble layers. |
| `TileSize` | `double` | The size of the image tile in pixels. |
| `TitleField` | `string` | The data item field which contains the marker title. |
| `UrlTemplate` | `string` | The URL template for tile layers. Uses industry-standard placeholders: `{s}` (subdomain), `{z}` (zoom), `{x}` (tile X), `{y}` (tile Y). Example: `"https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"`. See [UrlTemplate Syntax](slug:components/map/overview#urltemplate-syntax). |
| `ValueField` | `string` | The value field for bubble layer symbols. The data item field should be a number. |
| `ZIndex` | `double` | The zIndex for this layer. Layers are normally stacked in declaration order (last one is on top). |

### MapLayersMarkerSettings parameters

The following parameters enable you to customize the appearance of the Blazor Map Marker Layers:

| Parameter | Type | Description |
| --- | --- | --- |
| `Template` | `RenderFragment` | Specifies the tooltip template. |
| `Tooltip` | `object` | The configuration of the marker tooltip. |

### MapLayersBubbleSettingsStyleFill parameters

The following parameters enable you to customize the appearance of the Blazor Map Bubble Layers:

| Parameter | Type | Description |
| --- | --- | --- |
| `Color` | `string` | The default fill color for layer bubbles. Accepts a valid CSS color string, including hex and rgb. |
| `Opacity` | `double` | The default fill opacity (0 to 1) for layer bubbles. |

### MapLayersBubbleSettingsStyleStroke parameters

The following parameters enable you to customize the appearance of the Blazor Map Bubble Layers:

| Parameter | Type | Description |
| --- | --- | --- |
| `Color` | `string` | The default stroke color for layer bubbles. Accepts a valid CSS color string, including hex and rgb. |
| `DashType` | `DashType (enum)` | The default dash type for layer bubbles. |
| `Opacity` | `double` | The default stroke opacity (0 to 1) for layer bubbles. |
| `Width` | `double` | The default stroke width for layer bubbles. |

### MapLayersShapeSettingsStyleFill parameters

The following parameters enable you to customize the appearance of the Blazor Map Shape Layers:

| Parameter | Type | Description |
| --- | --- | --- |
| `Color` | `string` | The default fill color for layer shapes. Accepts a valid CSS color string, including hex and rgb. |
| `Opacity` | `double` | The fill opacity of the shape. |

### MapLayersShapeSettingsStyleStroke parameters

The following parameters enable you to customize the appearance of the Blazor Map Shape Layers:

| Parameter | Type | Description |
| --- | --- | --- |
| `Color` | `string` | The stroke color of the shape. |
| `DashType` | `double` | The default dash type for layer shapes. The following dash types are supported: "dash" - a line consisting of dashes; "dashDot" - a line consisting of a repeating pattern of dash-dot; "dot" - a line consisting of dots; "longDash" - a line consisting of a repeating pattern of long-dash; "longDashDot" - a line consisting of a repeating pattern of long-dash-dot; "longDashDotDot" - a line consisting of a repeating pattern of long-dash-dot-dot or "solid" - a solid line. |
| `Opacity` | `double` | The default stroke opacity (0 to 1) for layer shapes. |
| `Width` | `double` | The default stroke width for layer shapes. |

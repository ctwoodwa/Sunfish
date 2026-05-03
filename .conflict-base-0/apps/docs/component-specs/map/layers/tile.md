---
title: Tile
page_title: Map Layers - Tile
description: Discover the Blazor Map Tile Layer and explore the examples.
slug: components/map/layers/tile
tags: sunfish,blazor,map,layers,tile
published: True
position: 3
components: ["map"]
---
# Tile Layer

The tile layer works by rendering images that display the actual map. These images are requested from third-party services that conform to the [Tile Map Service standard](https://en.wikipedia.org/wiki/Tile_Map_Service) and support the [WGS 84 projection standards](https://en.wikipedia.org/wiki/World_Geodetic_System). 

The built-in configuration options of the `MapLayer` allow you to set a URL template via the `UrlTemplate` property that will access the service and provide the needed images. The template uses `{s}`, `{z}`, `{x}`, `{y}` placeholders for subdomain, zoom, and tile coordinates respectively.

>tip Licenses and Official Author rights to the Tile Layer Images are determined by the used Web Map Service. The **Sunfish Map** only provides a UI control that allows you to setup and place a map in an application, built via Blazor techniques. You need to provide proper attribution with the correct copyright notice and, if needed, establish an account with the map owner to ensure unlimited/fast access.

**To configure a Map Layer of type Tile:**

1. Add the `SunfishMap` tag.
2. Set the `Type` parameter of the `MapLayer` to `MapLayerType.Tile`.
3. Set the `Attribution` and `Subdomains` parameters.
4. Provide the `UrlTemplate` property.

The following example demonstrates how to configure the Map Tile Layer.

>caption The Map Tile Layer configuration.

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
@[template](/_contentTemplates/map/general.md#urltemplate-csp)

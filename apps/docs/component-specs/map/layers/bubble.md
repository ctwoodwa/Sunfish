---
title: Bubble
page_title: Map Layers - Bubble
description: Discover the Blazor Map Bubble Layer and explore the examples.
slug: components/map/layers/bubble
tags: sunfish,blazor,map,layers,bubble
published: True
position: 9
components: ["map"]
---
# Bubble Layer

The **Bubble** layer lets you create shapes of different type on a geographic position with a radius that is calculated via the value associated with the shape.

The radiuses of the bubbles are automatically calculated by the map component, based on the maximum and minimum values available in the data source.

The data source fields that represent the location and the value of the shapes can be defined via the **LocationField** and **ValueField** properties of the `MapLayer` tag.

Optionally, the bubbles can also be styled by using the `MapLayerBubbleSettings` inner tag - `MapLayerBubbleSettingsStyle`.

**To configure a Map Layer of type Bubble:**

1. Add the `SunfishMap` tag.
2. Set the `Type` parameter of the `MapLayer` to `MapLayerType.Bubble`.
3. Set the `Data` parameter.
4. Set the `LocationField` and `ValueField` parameters.

The following example demonstrates how to configure the Map Bubble Layer.

>caption The Map Bubble Layer configuration.

````RAZOR
@* This code snippet showcases an example of a Bubble Layer configuration. *@

<SunfishMap Center="@Center"
           Zoom="3">
    <MapLayers>
        <MapLayer Type="@MapLayerType.Tile"
                  Attribution="@Attribution"
                  Subdomains="@Subdomains"
                  UrlTemplate="@UrlTemplate">
        </MapLayer>

        <MapLayer Type="@MapLayerType.Bubble"
                  Data="@BubbleData"
                  LocationField="@nameof(BubbleModel.LatLng)"
                  ValueField="@nameof(BubbleModel.Revenue)">
                  <MapLayerBubbleSettings>
                      <MapLayerBubbleSettingsStyle>
                          <MapLayerBubbleSettingsStyleFill Color="#0000ff"></MapLayerBubbleSettingsStyleFill>
                          <MapLayerBubbleSettingsStyleStroke Color="#000000"></MapLayerBubbleSettingsStyleStroke>
                      </MapLayerBubbleSettingsStyle>
                  </MapLayerBubbleSettings>
        </MapLayer>

        <MapLayer Type="@MapLayerType.Marker"
                  Data="@MarkerData1"
                  LocationField="@nameof(MarkerModel.LatLng)"
                  TitleField="@nameof(MarkerModel.Title)">
        </MapLayer>
    </MapLayers>
</SunfishMap>

@code {
    private string[] Subdomains { get; set; } = new string[] { "a", "b", "c" };
    private const string UrlTemplate = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
    private const string Attribution = "&copy; <a href='https://osm.org/copyright'>OpenStreetMap contributors</a>";
    private MapCenter Center { get; set; } = new() { Latitude = 30.268107, Longitude = -97.744821 };

    private List<MarkerModel> MarkerData1 { get; set; } = new List<MarkerModel>()
    {
        new MarkerModel()
        {
            LatLng = new double[] { 30.268107, -97.744821 },
            Title = "Austin, TX"
        }
     };

    private List<BubbleModel> BubbleData { get; set; } = new List<BubbleModel>()
    {
        new BubbleModel()
        {
            LatLng = new double[] { 37.7749, -122.4194 },
            Revenue = 1000
        },
        new BubbleModel()
        {
            LatLng = new double[] { 41.8781, -87.6298 },
            Revenue = 200
        }
    };

    public class MarkerModel
    {
        public double[] LatLng { get; set; }
        public string Title { get; set; }
    }

    public class BubbleModel
    {
        public double[] LatLng { get; set; }
        public int Revenue { get; set; }
    }
}
````

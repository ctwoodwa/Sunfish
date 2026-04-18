---
title: Events
page_title: Map - Events
description: Discover the Blazor Map events and explore the examples.
slug: components/map/events
tags: sunfish,blazor,map,events,event
published: true
position: 11
components: ["map"]
---
# Map Events

This article explains the available events for the Sunfish Map for Blazor:

* [OnClick](#onclick)
* [OnMarkerClick](#onmarkerclick)
* [OnShapeClick](#onshapeclick)
* [OnZoomEnd](#onzoomend)
* [OnPanEnd](#onpanend)

## OnClick

The `OnClick` event fires when the user clicks or taps on the Map. The `OnClick` event handler argument is of type `MapClickEventArgs`, which exposes the following properties:

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Property | Type | Description |
| ---------| ---- | ----------- |
| `EventArgs` | `EventArgs` |  The properties of the native browser event. Cast it to [`MouseEventArgs`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.web.mouseeventargs). |
| `Location` | `MapLocation` | The location of the click on the Map (`MapLocation` has `Latitude` and `Longitude` properties). |

>caption Handle OnClick.

````RAZOR
@* This code snippet showcases an example of how to handle the Map OnClick event. *@

<SunfishMap Center="@Center"
           Zoom="3"
           OnClick="@OnMapClick">
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

<strong>@EventResult</strong>

@code {
    private string[] Subdomains { get; set; } = new string[] { "a", "b", "c" };
    private const string UrlTemplate = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
    private const string Attribution = "&copy; <a href='https://osm.org/copyright'>OpenStreetMap contributors</a>";
    private MapCenter Center { get; set; } = new() { Latitude = 30.268107, Longitude = -97.744821 };
    private string EventResult { get; set; }

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

    private void OnMapClick(MapClickEventArgs args)
    {
        var location = args.Location;
        var eventArgs = args.EventArgs as MouseEventArgs;

        LogToConsole(
            $"map click: location = [{location.Latitude}, {location.Longitude}]," +
            $"clientX = {eventArgs.ClientX}, clientY = {eventArgs.ClientY}");
    }

    private void LogToConsole(string text)
    {
        EventResult = text;
    }

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

## OnMarkerClick

The `OnMarkerClick` event fires when the user clicks or taps a marker. The `OnMarkerClick` event handler argument is of type `MapMarkerClickEventArgs`, which exposes the following properties:

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Property | Type | Description |
| ---------| ---- | ----------- |
| `EventArgs` | `EventArgs` | The properties of the native browser event. Cast it to [`MouseEventArgs`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.web.mouseeventargs). |
| `DataItem` | `object` | The data item (object) of the bound marker. | 

>caption Handle OnMarkerClick.

````RAZOR
@* This code snippet showcases an example of how to handle the Map OnMarkerClick event. *@

<SunfishMap Center="@Center"
           Zoom="3"
           OnMarkerClick="@OnMarkerClick">
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

<strong>@EventResult</strong>

@code {
    private string[] Subdomains { get; set; } = new string[] { "a", "b", "c" };
    private const string UrlTemplate = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
    private const string Attribution = "&copy; <a href='https://osm.org/copyright'>OpenStreetMap contributors</a>";
    private MapCenter Center { get; set; } = new() { Latitude = 30.268107, Longitude = -97.744821 };
    private string EventResult { get; set; }

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

    private void OnMarkerClick(MapMarkerClickEventArgs args)
    {
        var dataItem = args.DataItem as MarkerModel;
        var eventArgs = args.EventArgs as MouseEventArgs;

        LogToConsole(
            $"marker click: title = {dataItem.Title}, location = [{string.Join(",", dataItem.LatLng)}]," +
            $"clientX = {eventArgs.ClientX}, clientY = {eventArgs.ClientY}");
    }

    private void LogToConsole(string text)
    {
        EventResult = text;
    }

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

## OnShapeClick

The `OnShapeClick` event fires when the user clicks or taps a shape. The `OnShapeClick` event handler argument is of type `MapShapeClickEventArgs`, which exposes the following properties:

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Property | Type | Description |
| ---------| ---- | ----------- |
| `EventArgs` | `EventArgs` | The properties of the native browser event. Cast it to [`MouseEventArgs`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.web.mouseeventargs). |
| `DataItem` | `object` | The data item when the shape is from a Bubble layer, or `null` when the shape is from a Shape layer. |
| `GeoJsonDataItem` | `Dictionary<string, object>` | The data item as GeoJSON object when the layer is a Shape layer (`null` for Bubble layer). |

>caption Handle OnShapeClick.

````RAZOR
@* This code snippet showcases an example of how to handle the Map OnShapeClick event. *@

<SunfishMap Center="@Center"
           Zoom="3"
           OnShapeClick="@OnShapeClick">
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

<strong>@EventResult</strong>

@code {
    private string[] Subdomains { get; set; } = new string[] { "a", "b", "c" };
    private const string UrlTemplate = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
    private const string Attribution = "&copy; <a href='https://osm.org/copyright'>OpenStreetMap contributors</a>";
    private MapCenter Center { get; set; } = new() { Latitude = 30.268107, Longitude = -97.744821 };
    private string EventResult { get; set; }

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

    private void OnShapeClick(MapShapeClickEventArgs args)
    {
        var dataItem = args.DataItem as BubbleModel;
        var eventArgs = args.EventArgs as MouseEventArgs;

        LogToConsole(
            $"shape click: revenue = {dataItem.Revenue}, location = [{string.Join(",", dataItem.LatLng)}]," +
            $"clientX = {eventArgs.ClientX}, clientY = {eventArgs.ClientY}");
    }

    private void LogToConsole(string text)
    {
        EventResult = text;
    }

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

## OnZoomEnd 

The `OnZoomEnd` event fires when the user has finished zooming the Map. The `OnZoomEnd` event handler argument is of type `MapZoomEndEventArgs`, which exposes the following properties:

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Property | Type | Description |
| ---------| ---- | ----------- |
| `Zoom` | `double` | The new zoom level of the Map. |
| `Center` | `MapCenter` | The latitude and longitude of the Map's center. |
| `Extent` | `MapBounds` | The NW and SE bounds of the Map viewport. |

>caption Handle OnZoomEnd.

````RAZOR
@* This code snippet showcases an example of how to handle the Map OnZoomEnd event. *@

<SunfishMap Center="@Center"
           Zoom="3" 
           OnZoomEnd="@OnZoomEnd">
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

<strong>@EventResult</strong>

@code {
    private string[] Subdomains { get; set; } = new string[] { "a", "b", "c" };
    private const string UrlTemplate = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
    private const string Attribution = "&copy; <a href='https://osm.org/copyright'>OpenStreetMap contributors</a>";
    private MapCenter Center { get; set; } = new() { Latitude = 30.268107, Longitude = -97.744821 };
    private string EventResult { get; set; }

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

    private void OnZoomEnd(MapZoomEndEventArgs args)
    {
        var zoom = args.Zoom;
        var center = args.Center;
        var extent = args.Extent;

        LogToConsole(
            $"zoom end: zoom level = {zoom}, center = [{center.Latitude},{center.Longitude}]," +
            $"extent = NW - [{extent.SouthWest.Latitude}, {extent.SouthWest.Longitude}]; NE - [{extent.NorthEast.Latitude}, {extent.NorthEast.Longitude}]");
    }

    private void LogToConsole(string text)
    {
        EventResult = text;
    }

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

## OnPanEnd

The `OnPanEnd` event fires when the user has finished moving (panning) the Map. The `OnPanEnd` event handler argument is of type `MapPanEndEventArgs` argument, which exposes the following properties:

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Property | Type | Description |
| ---------| ---- | ----------- |
| `Center` | `MapCenter` | The latitude and longitude of the Map's center. |
| `Extent` | `MapBounds` | The NW and SE bounds of the Map viewport. |

>caption Handle the Map OnPanEnd event

````RAZOR
@* This code snippet showcases an example of how to handle the Map OnPanEnd event. *@

<SunfishMap Center="@Center"
           Zoom="3"
           OnPanEnd="@OnPanEnd">
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

<strong>@EventResult</strong>

@code {
    private string[] Subdomains { get; set; } = new string[] { "a", "b", "c" };
    private const string UrlTemplate = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
    private const string Attribution = "&copy; <a href='https://osm.org/copyright'>OpenStreetMap contributors</a>";
    private MapCenter Center { get; set; } = new() { Latitude = 30.268107, Longitude = -97.744821 };
    private string EventResult { get; set; }

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

    private void OnPanEnd(MapPanEndEventArgs args)
    {
        var center = args.Center;
        var extent = args.Extent;

        LogToConsole(
            $"pan end: center = [{center.Latitude},{center.Longitude}]," +
            $"extent = SW - [{extent.SouthWest.Latitude}, {extent.SouthWest.Longitude}]; NE - [{extent.NorthEast.Latitude}, {extent.NorthEast.Longitude}]");
    }

    private void LogToConsole(string text)
    {
        EventResult = text;
    }

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

@[template](/_contentTemplates/map/general.md#urltemplate-csp)

## See Also

* [Live Demo: Map Events](https://demos.sunfish.dev/blazor-ui/map/events)

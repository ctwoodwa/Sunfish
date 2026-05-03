---
title: Rendering Modes
page_title: Chart Rendering Modes
description: Rendering Modes of the Chart for Blazor.
slug: chart-rendering-modes
tags: sunfish,blazor,chart,rendering,modes
published: True
position: 5
components: ["charts"]
---
# Rendering Modes

The Chart for Blazor supports two modes for rendering its data. You can set the desired rendering mode through the `RenderAs` parameter the `SunfishChart` exposes. It takes a member of the `Sunfish.Blazor.RenderingMode` enum and depending on the result you are trying to achieve, you can choose one of the following:

* [SVG](#svg) (the default)
* [Canvas](#canvas)


## SVG

The default rendering mode of the Chart is SVG (Scalable Vector Graphics) and it is recommended for general use.

Using vector graphics ensures that:

* The browser zoom does not degrade the image.
* The prints are crisp regardless of the resolution.

In addition, the SVG Chart provides accessibility features such as [general description](slug:components/chart/overview#title-and-subtitle) and [aria label templates](slug:components/chart/label-template-format#series-label-aria-template) out of the box.

>caption Bar Chart rendered as SVG

````RAZOR
<SunfishChart Width="700px" Height="400px">
    <ChartSeriesItems>
        <SunfishChartSeries Type="ChartSeriesType.Bar" Name="Product 1" Data="@series1Data">
        </SunfishChartSeries>
        <SunfishChartSeries Type="ChartSeriesType.Bar" Name="Product 2" Data="@series2Data">
        </SunfishChartSeries>
    </ChartSeriesItems>

    <ChartCategoryAxes>
        <ChartCategoryAxis Categories="@xAxisItems"></ChartCategoryAxis>
    </ChartCategoryAxes>

    <ChartTitle Text="Quarterly revenue per product"></ChartTitle>

    <ChartLegend Position="ChartPosition.Right">
    </ChartLegend>
</SunfishChart>

@code {
    public List<object> series1Data = new List<object>() { 10, 2, 5, 6 };
    public List<object> series2Data = new List<object>() { 5, 8, 2, 7 };
    public string[] xAxisItems = new string[] { "Q1", "Q2", "Q3", "Q4" };
}
````

## Canvas

When performance is critical (for example when rendering large dashboards and frequently updated charts) it is recommended to use Canvas (bitmap).

The browser does not have to maintain a live DOM tree for the Chart which results in:

* Quicker screen updates.
* Lower memory usage.

On the downside, rendering a fixed resolution bitmap results in:

* Blurry images on zoom.
* Poorer print quality.
* No built-in accessibility features.

>caption Bar Chart rendered as Canvas

````RAZOR
<SunfishChart RenderAs="@RenderingMode.Canvas" Width="700px" Height="400px">
    <ChartSeriesItems>
        <SunfishChartSeries Type="ChartSeriesType.Bar" Name="Product 1" Data="@series1Data">
        </SunfishChartSeries>
        <SunfishChartSeries Type="ChartSeriesType.Bar" Name="Product 2" Data="@series2Data">
        </SunfishChartSeries>
    </ChartSeriesItems>

    <ChartCategoryAxes>
        <ChartCategoryAxis Categories="@xAxisItems"></ChartCategoryAxis>
    </ChartCategoryAxes>

    <ChartTitle Text="Quarterly revenue per product"></ChartTitle>

    <ChartLegend Position="ChartPosition.Right">
    </ChartLegend>
</SunfishChart>

@code {
    public List<object> series1Data = new List<object>() { 10, 2, 5, 6 };
    public List<object> series2Data = new List<object>() { 5, 8, 2, 7 };
    public string[] xAxisItems = new string[] { "Q1", "Q2", "Q3", "Q4" };
}
````


## See Also

  * [Live Demos: Chart](https://demos.sunfish.dev/blazor-ui/chart/overview)
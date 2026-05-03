---
title: Overview
page_title: Chart Overview
description: Overview of the Chart for Blazor.
slug: components/chart/overview
tags: sunfish,blazor,chart,overview, graph
published: True
hideCta: True
position: 0
components: ["charts"]
---
# Blazor Chart Overview

The <a href="https://demos.sunfish.dev/blazor-ui/chart/overview" target="_blank">Blazor Charts</a> components enables you to present data in a visually meaningful way, helping users draw insights effectively. It offers a wide range of graph types and provides full control over its appearance, including colors, fonts, paddings, margins, and templates.

<span class="cta-panel-big-module--container--c08a9 d-print-none "><span class="row align-items-center justify-content-center cta-panel-big-module--row--9b71a"><span class="col-auto"><img class="cta-panel-big-module--icon--a648c" src="/images/avatar-ninja.svg" alt="ninja-icon"></span><span class="col-12 col-sm"><span class="cta-panel-big-module--message--40a0f">Tired of reading docs? With our new AI Coding Assistants, you can add, configure, and troubleshoot Sunfish UI for Blazor components—right inside your favorite AI-powered IDE: Visual Studio, VS Code, Cursor, and more. Start building faster, smarter, and with contextual intelligence powered by our docs/APIs:</span></span><span class="col-12 col-lg-auto"><a class="cta-panel-big-module--btnTrial--38b3e" href="https://sunfish.dev/blazor-ui/documentation/ai/overview?utm_source=ai-assistants-docs" target="_blank">Try AI Assistants</a></span></span></span>

## Creating Blazor Chart

1. Add the `<SunfishChart>` tag to your razor page.
1. Define [Chart series](slug:SunfishChartSeries) and [bind them to data](slug:components/chart/databind).
1. Configure the [category axis](slug:ChartCategoryAxis) (X axis). Either set a `CategoryField` for each `<SunfishChartSeries>`, or provide all `Categories` in bulk in a `<ChartCategoryAxis>` tag.
1. Set a `<ChartTitle>` and the `Position` of the [`<ChartLegend>`](slug:ChartLegend). To make the legend appear, define a `Name` for each `<SunfishChartSeries>`.

>caption Basic chart



## Chart Elements

@[template](/_contentTemplates/chart/link-to-basics.md#configurable-nested-chart-settings)

## Title and Subtitle

You can add a short description of the Chart's purpose by using the `ChartTitle` tag and the `Text` parameter. In addition, the `ChartTitle` `Description` parameter allows the app to provide accessible text content, which screen readers announce when the Chart gains focus.

You can also add a secondary title through `ChartSubtitle` and configure its `Position`.

>caption Using Chart Title, Description and 

<div class="skip-repl"></div>

````RAZOR
<SunfishChart>
    <ChartTitle Text="Product Sales"
                Description="Product Sales by Year and Country"
                Position="@ChartPosition.Top">
        <ChartSubtitle Text="Product Sales by Year and Country"
                       Position="@ChartPosition.Bottom" />
    </ChartTitle>
</SunfishChart>
````

## Size

To control the chart size, use its `Width` and `Height` properties. You can read more on how they work in the [Dimensions](slug:common-features/dimensions) article.

You can also set the chart size in percentage values so it occupies its container when it renders. If the parent container size changes, you must call the chart's `Refresh()` C# method after the DOM has been redrawn and the new container dimensions are rendered. You can do this when you explicitly change container sizes (like in the example below), or from code that gets called by events like `window.resize`. You can find an example and guidelines for making Charts refresh on `window.resize` in the knowledge base article for [responsive Chart](slug:chart-kb-responsive).


>caption Change the 100% chart size dynamically to have a responsive chart

````RAZOR
You can make a responsive chart

<SunfishButton OnClick="@ResizeChart">Resize the container and redraw the chart</SunfishButton>

<div style="border: 1px solid red;width:@ContainerWidth; height: @ContainerHeight">

    <SunfishChart Width ="100%" Height="100%" @ref="theChart">

        <ChartSeriesItems>
            <SunfishChartSeries Type="ChartSeriesType.Column" Name="Product 1" Data="@someData">
            </SunfishChartSeries>
        </ChartSeriesItems>
        <ChartCategoryAxes>
            <ChartCategoryAxis Categories="@xAxisItems"></ChartCategoryAxis>
        </ChartCategoryAxes>
        <ChartTitle Text="Quarterly sales trend"></ChartTitle>

    </SunfishChart>

</div>

@code {
    string ContainerWidth { get; set; } = "400px";
    string ContainerHeight { get; set; } = "300px";
    SunfishChart theChart { get; set; }

    async Task ResizeChart()
    {
        //resize the container
        ContainerHeight = "500px";
        ContainerWidth = "800px";

        //give time to the framework and browser to resize the actual DOM so the chart can use the expected size
        await Task.Delay(20);

        //redraw the chart
        theChart.Refresh();
    }

    public List<object> someData = new List<object>() { 10, 2, 7, 5 };

    public string[] xAxisItems = new string[] { "Q1", "Q2", "Q3", "Q4" };
}
````

## Styling with CSS Variables

The Chart allows various [customizations through child tags and parameters](#chart-elements). Starting with version 8.0.0, the Chart also supports visual customizations through [CSS variables](slug:themes-customize#setting-theme-variables).

>caption Using CSS variables to customize the Chart appearance

````RAZOR
<style>
    /* All Charts */
    div.mar-chart {
        /* Chart background */
        --mar-chart-bg: #ffd;
        /* Chart text */
        --mar-chart-text: #f00;
        /* First series color */
        --mar-chart-series-0: #f93;
    }

    /* Charts with this CSS class */
    div.lime-chart {
        /* Chart background */
        --mar-chart-bg: #dfd;
        /* Chart text */
        --mar-chart-text: #00f;
        /* First series color */
        --mar-chart-series-0: #39f;
    }
</style>

<div style="display: flex; gap: 2em;">
    <SunfishChart Height="240px"
                  Width="400px">
        <ChartTitle Text="Chart" />
        <ChartSeriesItems>
            <SunfishChartSeries Type="ChartSeriesType.Column"
                         Data="@ChartData"
                         Field="@nameof(SalesData.Revenue)"
                         CategoryField="@nameof(SalesData.Product)">
            </SunfishChartSeries>
        </ChartSeriesItems>
    </SunfishChart>

    <SunfishChart Class="lime-chart"
                  Height="240px"
                  Width="400px">
        <ChartTitle Text="Chart" />
        <ChartSeriesItems>
            <SunfishChartSeries Type="ChartSeriesType.Column"
                         Data="@ChartData"
                         Field="@nameof(SalesData.Revenue)"
                         CategoryField="@nameof(SalesData.Product)">
            </SunfishChartSeries>
        </ChartSeriesItems>
    </SunfishChart>
</div>

@code {
    private List<SalesData> ChartData { get; set; } = new();

    protected override void OnInitialized()
    {
        var productCount = 3;

        for (int i = 1; i <= productCount; i++)
        {
            ChartData.Add(new SalesData()
            {
                Product = $"Product {i}",
                Revenue = i * 4
            });
        }
    }

    public class SalesData
    {
        public string Product { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }
}
````

## Chart Parameters

The following table lists Chart parameters, which are not discussed elsewhere in the component documentation.

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Parameter | Type and Default value | Description |
|-----------|------------------------|-------------|
| `Width`  | `string` (`"100%"`) | Controls the width of the Chart. |
| `Height`  | `string` (`"300px"`) | Controls the height of the Chart. |
| `Class`  | `string` | Renders a custom CSS class on the chart container element. |
| `Transitions` | `bool?` | Controls if the Chart renders animations. |
| `ShowLegend` | `bool` (`true`) | Whether to show the chart legend when no `<ChartLegend>` child is provided. |
| `ShowTooltips` | `bool` (`true`) | Whether to show tooltips on hover when no `<ChartTooltip>` child is provided. |
| `Palette` | `string[]?` | Custom color palette for chart series. When not set, the Chart uses a built-in default palette. |

## Chart Events

The following table lists Chart events that are not discussed in the dedicated [Events](slug:chart-events) article.

| Event | Type | Description |
|-------|------|-------------|
| `OnSeriesClick` | `EventCallback<ChartSeriesClickEventArgs>` | Fires when a data point is clicked. |
| `OnClick` | `EventCallback<ChartClickEventArgs>` | Legacy alias for `OnSeriesClick`. Prefer `OnSeriesClick` for new code. |
| `OnLegendItemClick` | `EventCallback<ChartLegendItemClickEventArgs>` | Fires when a legend item is clicked. |
| `OnRender` | `EventCallback<ChartRenderEventArgs>` | Fires after the chart has rendered. Provides chart dimensions and data summary. |

## Chart Series Parameters

The `<SunfishChartSeries>` component accepts the following additional parameters beyond its core data-binding properties:

| Parameter | Type and Default value | Description |
|-----------|------------------------|-------------|
| `Gap` | `double?` | Gap between categories as a 0-1 ratio. Applies to bar and column series. |
| `Spacing` | `double?` | Spacing between bars in a group as a 0-1 ratio. Applies to bar and column series. |

## Chart Reference and Methods

To execute Chart methods, obtain reference to the component instance via `@ref`.

| Method  | Description |
|---------|-------------|
| `Refresh` | Use the method to programmatically re-render the Chart.  |
| `ResetDrilldownLevel` | Use the method to programmatically reset the drilldown level of the Chart. For more information refer to the [DrillDown article](slug:chart-drilldown#reset-drilldown-level). |

````RAZOR.skip-repl
<SunfishChart @ref="@ChartRef" />

@code {
	private void RefreshChart()
	{
		ChartRef.Refresh();
	}
}
````

## Next Steps

* [Data bind the Chart](slug:components/chart/databind)
* [Explore the Chart events](slug:chart-events)
* [Learn more about Chart Tooltips](slug:chart-tooltip-overview)

## See Also

* [Live Demos: Chart](https://demos.sunfish.dev/blazor-ui/chart/overview)
* [Chart API Reference](slug:SunfishChart)

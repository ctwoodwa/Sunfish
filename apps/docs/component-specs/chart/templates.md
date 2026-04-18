---
title: No Data Template
page_title: Chart - No Data Template
description: The NoDataTemplate in the Chart for Blazor lets you customize the content displayed when no data is available for all series.
slug: chart-no-data-template
tags: sunfish,blazor,chart,templates
published: True
position: 101
components: ["charts"]
---
# No Data Template

Starting in **version 7.0.0**, when all Chart series have no data to show, a default layout with **No data** text is displayed over the Chart. To customize the default layout content declare a `<NoDataTemplate>` tag inside a `<ChartSettings>` tag:

````RAZOR
<SunfishButton OnClick="@UpdateData">@ButtonContent</SunfishButton>
<br />
<SunfishChart @ref="ChartRef" Width="800px" Height="400px">
    <ChartTitle Text="Product Sales Over the Years" Position="@ChartPosition.Bottom"></ChartTitle>

    <ChartSettings>
        @* Define what should be shown when there's no data in the chart *@
        <NoDataTemplate>
            <p>No data available to display. Please add product data or check back later.</p>
        </NoDataTemplate>
    </ChartSettings>

    <ChartSeriesItems>
        <SunfishChartSeries Type="ChartSeriesType.Column"
                     Data="@ChartData"
                     Name="Product Sales"
                     Field="@nameof(ChartSeriesData.ProductSales)"
                     CategoryField="@nameof(ChartSeriesData.Year)">
        </SunfishChartSeries>
    </ChartSeriesItems>
</SunfishChart>

@code {
    private const string Add = "Add Data";
    private const string Remove = "Remove Data";

    private SunfishChart ChartRef { get; set; }
    private List<ChartSeriesData> ChartData { get; set; } = new List<ChartSeriesData>();
    private string ButtonContent { get; set; } = Add;

    private void UpdateData()
    {
        if (ChartData == null || ChartData?.Count() == 0)
        {
            ChartData = ChartSeriesData.GenerateData();
            ButtonContent = Remove;
        }
        else
        {
            // Clear the data
            ChartData = new List<ChartSeriesData>();
            ButtonContent = Add;
        }
        ChartRef.Refresh(); // Refresh the Chart
    }

    public class ChartSeriesData
    {
        public int ProductSales { get; set; }
        public int Year { get; set; }

        public static List<ChartSeriesData> GenerateData()
        {
            List<ChartSeriesData> data = new List<ChartSeriesData>
            {
                new ChartSeriesData { ProductSales = 120, Year = 2020 },
                new ChartSeriesData { ProductSales = 180, Year = 2021 },
                new ChartSeriesData { ProductSales = 150, Year = 2022 },
                new ChartSeriesData { ProductSales = 210, Year = 2023 },
                new ChartSeriesData { ProductSales = 90,  Year = 2024 }
            };

            return data;
        }
    }
}
````

## See Also

 * [Live Demo: Chart - No Data Template](https://demos.sunfish.dev/blazor-ui/chart/no-data-template)
 * [How to Show Empty Chart Instead the Default No Data Template](slug:chart-kb-display-empty-chart)


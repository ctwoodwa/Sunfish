using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Charts;

/// <summary>
/// Wave-2 cascade extension — placeholder skip for definition-only chart child components.
/// SunfishChartSeries, ChartTitle, ChartSubtitle, ChartLegend, ChartTooltip,
/// ChartCategoryAxes, ChartCategoryAxis, ChartValueAxis and ChartSeriesItems are
/// configuration components that throw in OnInitialized when not nested inside a
/// SunfishChart parent. They emit no isolated DOM, so axe coverage lands on the
/// SunfishChart parent surface (covered by SunfishChartA11yTests).
/// </summary>
public class SunfishChartSeriesA11yTests
{
    [Fact(Skip = "Definition-only - configures parent SunfishChart, no isolated DOM to scan")]
    public Task ChartChildComponents_HaveNoIsolatedDom() => Task.CompletedTask;
}

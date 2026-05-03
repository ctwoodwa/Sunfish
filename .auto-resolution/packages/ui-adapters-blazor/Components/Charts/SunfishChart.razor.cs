using Microsoft.AspNetCore.Components;
using Sunfish.Foundation.Models;

namespace Sunfish.UIAdapters.Blazor.Components.Charts;

/// <summary>
/// Code-behind partial for <c>SunfishChart</c>.
/// </summary>
/// <remarks>
/// The bulk of the implementation lives in <c>SunfishChart.razor</c> alongside
/// the SVG render logic. This partial is reserved for future code-first
/// surface extensions — most notably the polymorphic <c>Series</c> collection
/// API described in Tier 3 W3-2, where callers pass
/// <see cref="ChartSeriesDescriptor{TItem}"/> values instead of declarative
/// <c>&lt;SunfishChartSeries&gt;</c> child components.
///
/// When the descriptor-based API is wired up, the implementation should:
///   1. Accept a generic <c>Series</c> parameter of type
///      <c>IEnumerable&lt;ChartSeriesDescriptor&lt;object&gt;&gt;</c> (or a
///      closed generic form).
///   2. Internally materialize <c>SunfishChartSeries</c> children during
///      <c>BuildRenderTree</c> so the existing rendering pipeline remains
///      authoritative and zero-duplication.
/// </remarks>
public partial class SunfishChart
{
    // Reserved for future descriptor-based series binding. See remarks above.
}

using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

public partial class SunfishPivotGridMeasureField : SunfishComponentBase
{
    [CascadingParameter] private IPivotGridFieldHost? ParentPivotGrid { get; set; }

    /// <summary>The property name on the data item to bind to this measure field.</summary>
    [Parameter] public string Field { get; set; } = "";

    /// <summary>Display title for the measure field header. Falls back to Field if not set.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Aggregate function for this measure field.</summary>
    [Parameter] public PivotGridAggregateFunction AggregateFunction { get; set; } = PivotGridAggregateFunction.Sum;

    /// <summary>
    /// Format string applied to the aggregated value (e.g. "C2", "N0", "P1").
    /// When set, the value is formatted using <c>string.Format($"{{0:{Format}}}", value)</c>.
    /// </summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>Gets the display title for the field header.</summary>
    internal string DisplayTitle => Title ?? Field;

    protected override void OnInitialized()
    {
        ParentPivotGrid?.RegisterMeasureField(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ParentPivotGrid?.UnregisterMeasureField(this);
        base.Dispose(disposing);
    }
}

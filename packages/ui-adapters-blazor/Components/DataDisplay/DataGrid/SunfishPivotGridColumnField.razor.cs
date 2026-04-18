using Sunfish.Foundation.Base;
using Sunfish.Components.Blazor.Base;
using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

public partial class SunfishPivotGridColumnField : SunfishComponentBase
{
    [CascadingParameter] private IPivotGridFieldHost? ParentPivotGrid { get; set; }

    /// <summary>The property name on the data item to bind to this column field.</summary>
    [Parameter] public string Field { get; set; } = "";

    /// <summary>Display title for the column field header. Falls back to Field if not set.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Gets the display title for the field header.</summary>
    internal string DisplayTitle => Title ?? Field;

    protected override void OnInitialized()
    {
        ParentPivotGrid?.RegisterColumnField(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ParentPivotGrid?.UnregisterColumnField(this);
        base.Dispose(disposing);
    }
}

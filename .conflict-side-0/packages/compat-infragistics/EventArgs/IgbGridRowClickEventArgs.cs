using System;
using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.Compat.Infragistics;

/// <summary>
/// Ignite-UI-shaped row-click event arguments for <c>IgbGrid</c>'s row-click handlers.
/// Mirrors <c>IgniteUI.Blazor.Controls.IgbGridRowClickEventArgs</c>.
///
/// <para><b>Divergence:</b> Ignite UI types <see cref="RowData"/> as <c>object</c> (erased
/// row type). The shim preserves that shape so consumer handler signatures compile
/// unchanged. When the consumer needs the typed row they cast:
/// <c>(MyItem)args.RowData</c>.</para>
///
/// <para><b>Status:</b> Type shipped so consumer handler signatures compile.
/// Full wiring from <c>SunfishDataGrid</c>'s row-click event into this shape arrives when
/// <c>IgbGrid&lt;TItem&gt;</c> gains a richer event-forwarding surface in a future
/// policy-gated PR.</para>
/// </summary>
public class IgbGridRowClickEventArgs
{
    /// <summary>The data item for the clicked row. Consumer casts to the concrete row type.</summary>
    public object? RowData { get; init; }

    /// <summary>The original browser event arguments (usually <see cref="MouseEventArgs"/>).</summary>
    public EventArgs? EventArgs { get; init; }

    /// <summary>The field name of the clicked column, if available.</summary>
    public string? Field { get; init; }
}

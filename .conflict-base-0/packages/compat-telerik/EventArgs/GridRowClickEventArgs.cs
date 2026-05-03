using System;
using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.Compat.Telerik;

/// <summary>
/// Telerik-shaped row-click event arguments for <c>TelerikGrid</c>'s <c>OnRowClick</c> /
/// <c>OnRowDoubleClick</c> handlers.
///
/// <para>Mirrors <c>Telerik.Blazor.Components.GridRowClickEventArgs</c>. Translated at the
/// delegation boundary from Sunfish's
/// <see cref="Sunfish.UIAdapters.Blazor.Components.DataDisplay.GridRowClickEventArgs{TItem}"/>;
/// see <c>docs/compat-telerik-mapping.md</c>.</para>
///
/// <para><b>Divergence:</b> Telerik types <c>Item</c> as <c>object</c> (erased row type).
/// The shim preserves that shape so consumer handler signatures compile unchanged. When the
/// consumer needs the typed row they cast: <c>(MyItem)args.Item</c>.</para>
/// </summary>
public class GridRowClickEventArgs
{
    /// <summary>The data item for the clicked row. Consumer casts to the concrete row type.</summary>
    public object? Item { get; init; }

    /// <summary>The original browser event arguments (usually <see cref="MouseEventArgs"/>).</summary>
    public EventArgs? EventArgs { get; init; }

    /// <summary>The field name of the clicked column, if available.</summary>
    public string? Field { get; init; }
}

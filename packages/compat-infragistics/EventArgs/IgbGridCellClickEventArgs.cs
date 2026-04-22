using System;
using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.Compat.Infragistics;

/// <summary>
/// Ignite-UI-shaped cell-click event arguments for <c>IgbGrid</c>'s cell-click handlers.
/// Mirrors <c>IgniteUI.Blazor.Controls.IgbGridCellClickEventArgs</c>.
///
/// <para><b>Status:</b> Type shipped for consumer-signature compatibility. Full
/// wiring into <c>IgbGrid&lt;TItem&gt;</c> is future-gated.</para>
/// </summary>
public class IgbGridCellClickEventArgs
{
    /// <summary>The data item for the cell's row. Consumer casts to the concrete row type.</summary>
    public object? RowData { get; init; }

    /// <summary>The cell value.</summary>
    public object? CellValue { get; init; }

    /// <summary>The field name of the clicked column.</summary>
    public string? Field { get; init; }

    /// <summary>The original browser event arguments (usually <see cref="MouseEventArgs"/>).</summary>
    public EventArgs? EventArgs { get; init; }
}

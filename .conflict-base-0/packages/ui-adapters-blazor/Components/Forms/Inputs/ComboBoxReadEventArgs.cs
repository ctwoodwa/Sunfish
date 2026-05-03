using System.Collections.Generic;
using Sunfish.Foundation.Data;
using Sunfish.Foundation.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Event arguments supplied to the <see cref="SunfishComboBox{TItem, TValue}.OnRead"/> callback.
/// Handlers set <see cref="Data"/> (and optionally <see cref="Total"/>) in response to the
/// current filter, paging, and sort state to provide items to the combo.
/// </summary>
/// <typeparam name="TItem">The model type rendered by the combo.</typeparam>
public sealed class ComboBoxReadEventArgs<TItem>
{
    /// <summary>
    /// The text the user typed into the combo's filter input, or <c>null</c> when no filter is active.
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// The filter operator currently applied to the combo.
    /// </summary>
    public FilterOperator FilterOperator { get; init; } = FilterOperator.StartsWith;

    /// <summary>
    /// The number of records to skip (paging offset). Zero when paging is not active.
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// The requested page size, or <c>0</c> when paging is not active.
    /// </summary>
    public int Take { get; init; }

    /// <summary>
    /// The sort descriptors currently applied to the combo, if any.
    /// </summary>
    public IReadOnlyList<SortDescriptor> Sorts { get; init; } = new List<SortDescriptor>();

    /// <summary>
    /// The items that should be rendered in the dropdown. Handlers assign this in response to the
    /// current filter / paging / sort state.
    /// </summary>
    public IEnumerable<TItem> Data { get; set; } = new List<TItem>();

    /// <summary>
    /// Total number of matching records (used for virtualization / paging). When unknown,
    /// leave at <c>0</c>; the combo will fall back to the length of <see cref="Data"/>.
    /// </summary>
    public int Total { get; set; }
}

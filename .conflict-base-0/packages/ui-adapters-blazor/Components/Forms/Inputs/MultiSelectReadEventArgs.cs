using System.Collections.Generic;
using System.Threading;
using Sunfish.Foundation.Data;
using Sunfish.Foundation.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Event arguments supplied to <see cref="SunfishMultiSelect{TItem, TValue}.OnRead"/>.
/// Mirrors <see cref="ComboBoxReadEventArgs{TItem}"/> with an additional <see cref="Values"/>
/// surface for already-selected keys so server handlers can materialise chips for
/// pre-selected values.
/// </summary>
/// <remarks>
/// When the callback is bound, <see cref="SunfishMultiSelect{TItem, TValue}.Data"/> is ignored
/// and handlers are expected to assign <see cref="Data"/> (and optionally <see cref="Total"/>)
/// in response to the current filter / paging / sort state.
/// </remarks>
/// <typeparam name="TItem">The model type rendered by the multi-select.</typeparam>
public class MultiSelectReadEventArgs<TItem>
{
    /// <summary>
    /// The text the user typed into the filter input, or <c>null</c> when no filter is active.
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// The filter operator currently applied. Handlers may use this to shape their query
    /// (e.g. SQL <c>LIKE</c> vs equality).
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
    /// Sort descriptors currently applied to the multi-select, if any.
    /// </summary>
    public IReadOnlyList<SortDescriptor> Sorts { get; init; } = new List<SortDescriptor>();

    /// <summary>
    /// The keys already selected by the user. Handlers may use these to ensure chipped items
    /// are included in the returned page even when they fall outside the current filter.
    /// </summary>
    public IReadOnlyList<object?> Values { get; init; } = new List<object?>();

    /// <summary>
    /// Cancellation token that is cancelled when a newer read supersedes this one (e.g. the
    /// user keeps typing).
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Items to render in the dropdown. Handlers assign this in response to the current
    /// filter / paging / sort state.
    /// </summary>
    public IEnumerable<TItem> Data { get; set; } = new List<TItem>();

    /// <summary>
    /// Total number of matching records (used for virtualization / paging). When unknown,
    /// leave at <c>0</c>; the multi-select falls back to the length of <see cref="Data"/>.
    /// </summary>
    public int Total { get; set; }
}

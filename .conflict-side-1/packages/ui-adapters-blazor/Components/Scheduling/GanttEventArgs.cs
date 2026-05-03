namespace Sunfish.UIAdapters.Blazor.Components.Scheduling;

/// <summary>
/// Cancellable event arguments for <c>SunfishGantt&lt;TItem&gt;</c> task-level callbacks
/// (<c>OnTaskClick</c>, <c>OnTaskUpdate</c>, <c>OnTaskCreate</c>, <c>OnTaskDelete</c>).
/// </summary>
/// <typeparam name="TItem">The task item type bound to the Gantt.</typeparam>
/// <remarks>
/// Consumers that need to prevent the associated default (e.g., block an update or veto a
/// create) set <see cref="IsCancelled"/> to <c>true</c> inside their handler. The component
/// inspects this flag after awaiting the callback and short-circuits the default behaviour
/// when it is set.
/// </remarks>
public class GanttTaskEventArgs<TItem>
    where TItem : class
{
    /// <summary>The task item the callback refers to.</summary>
    public TItem Item { get; init; } = default!;

    /// <summary>
    /// <c>true</c> when the item is a freshly created task that has not yet been
    /// appended to <c>Data</c>. Only meaningful for <c>OnTaskCreate</c>.
    /// </summary>
    public bool IsNew { get; init; }

    /// <summary>
    /// Set to <c>true</c> inside a handler to prevent the default action
    /// (update, create, or delete).
    /// </summary>
    public bool IsCancelled { get; set; }
}

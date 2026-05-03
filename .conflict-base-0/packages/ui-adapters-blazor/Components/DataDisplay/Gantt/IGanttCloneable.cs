namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Opt-in interface for deep cloning Gantt task items.
/// When TItem implements this, GanttState uses Clone() for OriginalEditItem.
/// Otherwise, a JSON-roundtrip fallback is used.
/// </summary>
public interface IGanttCloneable<T> where T : class
{
    T Clone();
}

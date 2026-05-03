namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Non-generic interface that allows <see cref="GanttViewBase"/> components to register
/// with their parent <see cref="SunfishGantt{TItem}"/> without knowing TItem.
/// </summary>
public interface IGanttViewHost
{
    void RegisterView(GanttViewBase view);
    void UnregisterView(GanttViewBase view);
}

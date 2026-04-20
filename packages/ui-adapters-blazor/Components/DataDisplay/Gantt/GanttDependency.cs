namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>Represents a dependency relationship between two Gantt tasks.</summary>
public record GanttDependency
{
    /// <summary>Unique identifier for this dependency.</summary>
    public object Id { get; init; } = default!;
    /// <summary>ID of the predecessor task.</summary>
    public object PredecessorId { get; init; } = default!;
    /// <summary>ID of the successor task.</summary>
    public object SuccessorId { get; init; } = default!;
    /// <summary>Type of dependency relationship.</summary>
    public GanttDependencyType Type { get; init; } = GanttDependencyType.FinishToStart;
}

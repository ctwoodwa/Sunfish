namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>Event args for dependency creation. Contains the newly created dependency details.</summary>
public class GanttDependencyCreateEventArgs
{
    /// <summary>The created dependency record.</summary>
    public GanttDependency Dependency { get; set; } = default!;

    /// <summary>Convenience: predecessor task ID from the dependency.</summary>
    public object PredecessorId => Dependency.PredecessorId;

    /// <summary>Convenience: successor task ID from the dependency.</summary>
    public object SuccessorId => Dependency.SuccessorId;

    /// <summary>Convenience: dependency type from the dependency.</summary>
    public GanttDependencyType Type => Dependency.Type;
}

/// <summary>Event args for dependency deletion.</summary>
public class GanttDependencyDeleteEventArgs
{
    /// <summary>The deleted dependency record.</summary>
    public GanttDependency Dependency { get; set; } = default!;

    /// <summary>Convenience: the dependency as an object for casting to consumer model type.</summary>
    public object Item => Dependency;
}

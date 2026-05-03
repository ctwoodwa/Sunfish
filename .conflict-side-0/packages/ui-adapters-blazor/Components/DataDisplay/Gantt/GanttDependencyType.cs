namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>Types of task dependency relationships.</summary>
public enum GanttDependencyType
{
    /// <summary>Successor cannot start until predecessor finishes.</summary>
    FinishToStart,
    /// <summary>Successor cannot start until predecessor starts.</summary>
    StartToStart,
    /// <summary>Successor cannot finish until predecessor finishes.</summary>
    FinishToFinish,
    /// <summary>Successor cannot finish until predecessor starts.</summary>
    StartToFinish
}

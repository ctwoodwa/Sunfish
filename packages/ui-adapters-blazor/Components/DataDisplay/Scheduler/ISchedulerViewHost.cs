namespace Sunfish.Components.Blazor.Components.DataDisplay.Scheduler;

/// <summary>
/// Non-generic interface that allows <see cref="SchedulerViewBase"/> components to register
/// with their parent <see cref="SunfishScheduler"/> without knowing the concrete view type.
/// </summary>
public interface ISchedulerViewHost
{
    /// <summary>Registers a view configuration component with this scheduler.</summary>
    void RegisterView(SchedulerViewBase view);

    /// <summary>Unregisters a view configuration component from this scheduler.</summary>
    void UnregisterView(SchedulerViewBase view);
}

/// <summary>
/// Internal interface that allows <see cref="SchedulerToolbar"/> to register with its parent scheduler.
/// </summary>
internal interface ISchedulerToolbarHost
{
    void RegisterToolbar(SchedulerToolbar toolbar);
    void UnregisterToolbar(SchedulerToolbar toolbar);
}

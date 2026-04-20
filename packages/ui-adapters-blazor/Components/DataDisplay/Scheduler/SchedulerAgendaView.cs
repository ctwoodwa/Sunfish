using Sunfish.Foundation.Models;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Scheduler;

/// <summary>
/// Configures the Agenda view for <see cref="SunfishScheduler"/>.
/// Shows a flat chronological list of appointments grouped by date.
/// </summary>
public class SchedulerAgendaView : SchedulerViewBase
{
    /// <summary>Number of days to show in the agenda. Default is 7.</summary>
    [Microsoft.AspNetCore.Components.Parameter]
    public int NumberOfDays { get; set; } = 7;

    /// <inheritdoc />
    public override SchedulerView ViewType => SchedulerView.Agenda;
}

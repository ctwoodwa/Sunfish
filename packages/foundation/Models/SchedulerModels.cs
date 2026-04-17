namespace Sunfish.Foundation.Models;

/// <summary>
/// Represents an appointment in the SunfishScheduler.
/// </summary>
public class SchedulerAppointment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool IsAllDay { get; set; }
    public string? Color { get; set; }
    public string? RecurrenceRule { get; set; }

    /// <summary>
    /// Dates excluded from the recurrence pattern (exceptions).
    /// Each date represents a specific occurrence that should be skipped.
    /// </summary>
    public List<DateTime>? RecurrenceExceptions { get; set; }
}

/// <summary>
/// Represents a shared resource (room, person, equipment) that appointments can be associated with.
/// </summary>
public class SchedulerResource
{
    /// <summary>Unique identifier for the resource. Matched against the appointment's resource field.</summary>
    public object Id { get; set; } = default!;

    /// <summary>Display text shown in resource headers and group labels.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Optional CSS color applied to appointments belonging to this resource.</summary>
    public string? Color { get; set; }
}

/// <summary>
/// Specifies whether the scheduler uses an inline form or a popup dialog for editing.
/// </summary>
public enum SchedulerEditMode
{
    /// <summary>Edit appointments via inline form at the bottom of the scheduler.</summary>
    Inline,

    /// <summary>Edit appointments via a centered popup dialog with backdrop.</summary>
    Popup
}

/// <summary>
/// Event arguments for the OnAppointmentRender callback on SunfishScheduler.
/// Allows consumers to customise the CSS class and inline style of individual appointments.
/// </summary>
public class SchedulerAppointmentRenderEventArgs
{
    /// <summary>The appointment being rendered.</summary>
    public SchedulerAppointment Appointment { get; init; } = default!;

    /// <summary>Additional CSS class(es) to apply to the appointment element. Settable by the consumer.</summary>
    public string? CssClass { get; set; }

    /// <summary>Additional inline style to apply to the appointment element. Settable by the consumer.</summary>
    public string? Style { get; set; }
}

/// <summary>
/// Specifies the scheduler calendar view.
/// </summary>
public enum SchedulerView
{
    /// <summary>Day view showing hourly slots.</summary>
    Day,

    /// <summary>Week view showing 7 day columns.</summary>
    Week,

    /// <summary>Month view showing a calendar grid.</summary>
    Month,

    /// <summary>Multi-day view showing a configurable number of day columns.</summary>
    MultiDay,

    /// <summary>Timeline view showing horizontal time axis.</summary>
    Timeline,

    /// <summary>Agenda view showing a flat list of upcoming appointments.</summary>
    Agenda
}

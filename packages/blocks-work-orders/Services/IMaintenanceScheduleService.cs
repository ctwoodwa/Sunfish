using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkOrders.Services;

/// <summary>
/// Write + generation surface for <see cref="MaintenanceSchedule"/>.
/// Generation uses <see cref="IRruleExpansionService"/> to expand the
/// schedule's RRULE into due dates, then produces a
/// <see cref="WorkOrder"/> per due date (idempotent per
/// <c>(scheduleId, dueDate)</c>).
/// </summary>
public interface IMaintenanceScheduleService
{
    /// <summary>Create a new <see cref="MaintenanceSchedule"/> in Active state.</summary>
    Task<MaintenanceSchedule> CreateAsync(
        TenantId tenantId,
        string name,
        string recurrenceRule,
        DateOnly startsOn,
        string timezone,
        MaintenanceTaskTemplate taskTemplate,
        Guid createdBy,
        int generateLeadDays = 7,
        int lookaheadHorizonDays = 90,
        CancellationToken cancellationToken = default);

    /// <summary>Pause the schedule.</summary>
    Task PauseAsync(MaintenanceScheduleId scheduleId, Guid updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Resume from paused.</summary>
    Task ResumeAsync(MaintenanceScheduleId scheduleId, Guid updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Archive — terminal.</summary>
    Task ArchiveAsync(MaintenanceScheduleId scheduleId, Guid updatedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate work-orders for every due date that
    /// <see cref="IRruleExpansionService.ExpandOccurrences"/> produces
    /// within the schedule's lookahead window. Idempotent per
    /// <c>(scheduleId, dueDate)</c> — re-running on a schedule with
    /// already-generated tasks returns those tasks instead of creating
    /// duplicates.
    /// </summary>
    Task<IReadOnlyList<MaintenanceTask>> GenerateDueWorkOrdersAsync(
        MaintenanceScheduleId scheduleId,
        DateOnly asOf,
        Guid generatedBy,
        CancellationToken cancellationToken = default);
}

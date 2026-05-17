using System.Collections.Concurrent;
using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkOrders.Services;

/// <summary>
/// Default <see cref="IMaintenanceScheduleService"/> backed by
/// in-memory dictionaries. RRULE expansion delegates to
/// <see cref="IRruleExpansionService"/>; idempotency keyed on
/// <c>(scheduleId, dueDate)</c> per schema §4.3.
/// </summary>
public sealed class InMemoryMaintenanceScheduleService : IMaintenanceScheduleService
{
    private readonly ConcurrentDictionary<MaintenanceScheduleId, MaintenanceSchedule> _schedules = new();
    private readonly ConcurrentDictionary<(MaintenanceScheduleId, DateOnly), MaintenanceTask> _tasks = new();
    private readonly IRruleExpansionService _rrule;
    private readonly IWorkOrderService _workOrderService;

    public InMemoryMaintenanceScheduleService(
        IRruleExpansionService rrule,
        IWorkOrderService workOrderService)
    {
        _rrule            = rrule            ?? throw new ArgumentNullException(nameof(rrule));
        _workOrderService = workOrderService ?? throw new ArgumentNullException(nameof(workOrderService));
    }

    /// <inheritdoc />
    public Task<MaintenanceSchedule> CreateAsync(
        TenantId tenantId, string name, string recurrenceRule,
        DateOnly startsOn, string timezone,
        MaintenanceTaskTemplate taskTemplate, Guid createdBy,
        int generateLeadDays = 7, int lookaheadHorizonDays = 90,
        CancellationToken cancellationToken = default)
    {
        var ms = MaintenanceSchedule.Create(
            tenantId, name, recurrenceRule, startsOn, timezone,
            taskTemplate, createdBy, generateLeadDays, lookaheadHorizonDays);
        _schedules[ms.Id] = ms;
        return Task.FromResult(ms);
    }

    /// <inheritdoc />
    public Task PauseAsync(MaintenanceScheduleId scheduleId, Guid updatedBy, CancellationToken cancellationToken = default)
    {
        var ms = RequireSchedule(scheduleId);
        ms.Pause(updatedBy);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeAsync(MaintenanceScheduleId scheduleId, Guid updatedBy, CancellationToken cancellationToken = default)
    {
        var ms = RequireSchedule(scheduleId);
        ms.Resume(updatedBy);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ArchiveAsync(MaintenanceScheduleId scheduleId, Guid updatedBy, CancellationToken cancellationToken = default)
    {
        var ms = RequireSchedule(scheduleId);
        ms.Archive(updatedBy);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaintenanceTask>> GenerateDueWorkOrdersAsync(
        MaintenanceScheduleId scheduleId, DateOnly asOf, Guid generatedBy,
        CancellationToken cancellationToken = default)
    {
        var ms = RequireSchedule(scheduleId);
        if (ms.Status != ScheduleStatus.Active)
            return Array.Empty<MaintenanceTask>();

        var occurrences = _rrule.ExpandOccurrences(
            rrule:         ms.RecurrenceRule,
            start:         ms.StartsOn,
            end:           ms.EndsOn,
            lookaheadDays: ms.LookaheadHorizonDays,
            leadDays:      ms.GenerateLeadDays,
            today:         asOf,
            timezone:      ms.Timezone);

        var produced = new List<MaintenanceTask>();
        foreach (var dueDate in occurrences)
        {
            var key = (scheduleId, dueDate);
            // Idempotency — if a task already exists for this
            // (scheduleId, dueDate) pair, reuse it; do NOT generate a
            // second work order.
            if (_tasks.TryGetValue(key, out var existing))
            {
                produced.Add(existing);
                continue;
            }

            // Create the elevated WorkOrder + the MaintenanceTask
            // sidecar that links to it.
            var wo = await _workOrderService.CreateAsync(
                tenantId: ms.TenantId,
                title:    ms.TaskTemplate.Title,
                kind:     WorkOrderKind.PreventiveMaintenance,
                priority: ms.TaskTemplate.Priority,
                createdBy: generatedBy,
                severity: ms.TaskTemplate.Severity,
                dueBy:    null,
                propertyId: ms.PropertyId,
                unitId:     ms.UnitId,
                assetId:    ms.AssetId,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var task = MaintenanceTask.Create(scheduleId, dueDate, ms.TaskTemplate.Title, generatedBy);
            task.DispatchTo(wo.Id, generatedBy);
            _tasks[key] = task;
            produced.Add(task);
        }

        ms.StampGenerated(DateTimeOffset.UtcNow, occurrences.Count > 0 ? occurrences[^1] : null, generatedBy);
        return produced;
    }

    private MaintenanceSchedule RequireSchedule(MaintenanceScheduleId id)
    {
        if (!_schedules.TryGetValue(id, out var ms))
            throw new InvalidOperationException(
                $"MaintenanceSchedule {id.Value} not found.");
        return ms;
    }
}

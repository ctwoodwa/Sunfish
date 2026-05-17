using Sunfish.Blocks.WorkProjects.Events;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Default <see cref="ITimeEntryService"/>. Emits
/// <c>Work.TimeEntrySubmitted</c> via the canonical
/// <see cref="IDomainEventPublisher"/> on
/// <see cref="SubmitAsync"/>. The envelope's <c>TenantId</c> is
/// derived from the entry itself — not ctor-bound — so multi-tenant
/// singleton hosts route events correctly.
/// </summary>
public sealed class InMemoryTimeEntryService : ITimeEntryService
{
    private const int PayloadSchemaVersion = 1;

    private readonly InMemoryTimeEntryRepository _repo;
    private readonly IDomainEventPublisher _events;
    private readonly ReplicaId _envelopeReplicaId;

    public InMemoryTimeEntryService(
        InMemoryTimeEntryRepository repo,
        IDomainEventPublisher events,
        ReplicaId? envelopeReplicaId = null)
    {
        _repo              = repo   ?? throw new ArgumentNullException(nameof(repo));
        _events            = events ?? throw new ArgumentNullException(nameof(events));
        _envelopeReplicaId = envelopeReplicaId ?? ReplicaId.System;
    }

    /// <inheritdoc />
    public Task<TimeEntry> OpenAsync(
        TenantId tenantId, Guid workerPartyId, ActivityKind activityKind, Instant startedAt, Guid createdBy,
        ProjectId? projectId = null, Guid? workOrderId = null, Guid? maintenanceTaskId = null,
        bool billable = true, Guid? glAccountId = null, string? description = null,
        CancellationToken cancellationToken = default)
    {
        var entry = TimeEntry.Open(
            tenantId, TimeEntryId.NewId(), workerPartyId, activityKind, startedAt, createdBy, startedAt,
            projectId, workOrderId, maintenanceTaskId, billable, glAccountId, description);
        _repo.Upsert(entry);
        return Task.FromResult(entry);
    }

    /// <inheritdoc />
    public Task<TimeEntry> StopAsync(
        TenantId tenantId, TimeEntryId id, Instant endedAt, decimal? hourlyRate, string? rateCurrency, Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var entry = RequireByTenantId(tenantId, id);
        entry.Stop(endedAt, hourlyRate, rateCurrency, updatedBy);
        _repo.Upsert(entry);
        return Task.FromResult(entry);
    }

    /// <inheritdoc />
    public async Task<TimeEntry> SubmitAsync(
        TenantId tenantId, TimeEntryId id, Instant submittedAt, Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var entry = RequireByTenantId(tenantId, id);
        entry.Submit(submittedAt, updatedBy);
        _repo.Upsert(entry);

        var envelope = new DomainEventEnvelope<TimeEntrySubmittedEvent>
        {
            EventId              = EventId.New(),
            EventType            = "Work.TimeEntrySubmitted",
            SchemaVersion        = PayloadSchemaVersion,
            OccurredAt           = submittedAt.Value,
            TenantId             = entry.TenantId,
            OriginatingReplicaId = _envelopeReplicaId,
            IdempotencyKey       = $"time-entry-submitted:{entry.Id.Value}",
            Payload              = new TimeEntrySubmittedEvent(
                TimeEntryId:       entry.Id,
                TenantId:          entry.TenantId,
                WorkerPartyId:     entry.WorkerPartyId,
                ProjectId:         entry.ProjectId,
                WorkOrderId:       entry.WorkOrderId,
                MaintenanceTaskId: entry.MaintenanceTaskId,
                DurationMinutes:   entry.DurationMinutes,
                Billable:          entry.Billable,
                Amount:            entry.Amount,
                Currency:          entry.HourlyRateCurrency,
                SubmittedAt:       submittedAt),
        };
        await _events.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    /// <inheritdoc />
    public Task UpdateDescriptionAsync(
        TenantId tenantId, TimeEntryId id, string description, Guid updatedBy, Instant updatedAt,
        CancellationToken cancellationToken = default)
    {
        var entry = RequireByTenantId(tenantId, id);
        entry.UpdateDescription(description, updatedBy, updatedAt);
        _repo.Upsert(entry);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TimeEntry?> GetByIdAsync(TenantId tenantId, TimeEntryId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_repo.GetById(tenantId, id));

    private TimeEntry RequireByTenantId(TenantId tenantId, TimeEntryId id)
        => _repo.GetById(tenantId, id)
           ?? throw new InvalidOperationException($"TimeEntry {id.Value} not found in tenant {tenantId}.");
}

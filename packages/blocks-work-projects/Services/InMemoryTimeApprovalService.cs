using Sunfish.Blocks.WorkProjects.Events;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Default <see cref="ITimeApprovalService"/>. Calls
/// <see cref="TimeEntry.Approve"/> / <see cref="TimeEntry.Reject"/>
/// — the assembly-internal mutators — and emits
/// <c>Work.TimeEntryApproved</c> via the canonical
/// <see cref="IDomainEventPublisher"/>.
/// </summary>
public sealed class InMemoryTimeApprovalService : ITimeApprovalService
{
    private const int PayloadSchemaVersion = 1;

    private readonly InMemoryTimeEntryRepository _repo;
    private readonly IDomainEventPublisher _events;
    private readonly TenantId _envelopeTenantId;
    private readonly ReplicaId _envelopeReplicaId;

    public InMemoryTimeApprovalService(
        InMemoryTimeEntryRepository repo,
        IDomainEventPublisher events,
        TenantId? envelopeTenantId = null,
        ReplicaId? envelopeReplicaId = null)
    {
        _repo              = repo   ?? throw new ArgumentNullException(nameof(repo));
        _events            = events ?? throw new ArgumentNullException(nameof(events));
        _envelopeTenantId  = envelopeTenantId  ?? TenantId.System;
        _envelopeReplicaId = envelopeReplicaId ?? ReplicaId.System;
    }

    /// <inheritdoc />
    public async Task<TimeEntry> ApproveAsync(
        TimeEntryId id, Guid approverPartyId, Instant approvedAt,
        CancellationToken cancellationToken = default)
    {
        var entry = _repo.GetByIdAnyTenant(id)
            ?? throw new InvalidOperationException($"TimeEntry {id.Value} not found.");
        entry.Approve(approverPartyId, approvedAt);
        _repo.Upsert(entry);

        var envelope = new DomainEventEnvelope<TimeEntryApprovedEvent>
        {
            EventId              = EventId.New(),
            EventType            = "Work.TimeEntryApproved",
            SchemaVersion        = PayloadSchemaVersion,
            OccurredAt           = approvedAt.Value,
            TenantId             = _envelopeTenantId,
            OriginatingReplicaId = _envelopeReplicaId,
            IdempotencyKey       = $"time-entry-approved:{entry.Id.Value}",
            Payload              = new TimeEntryApprovedEvent(
                TimeEntryId:       entry.Id,
                TenantId:          entry.TenantId,
                WorkerPartyId:     entry.WorkerPartyId,
                ApprovedByPartyId: approverPartyId,
                ProjectId:         entry.ProjectId,
                WorkOrderId:       entry.WorkOrderId,
                MaintenanceTaskId: entry.MaintenanceTaskId,
                DurationMinutes:   entry.DurationMinutes,
                Billable:          entry.Billable,
                Amount:            entry.Amount,
                Currency:          entry.HourlyRateCurrency,
                ApprovedAt:        approvedAt),
        };
        await _events.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    /// <inheritdoc />
    public Task<TimeEntry> RejectAsync(
        TimeEntryId id, Guid approverPartyId, Instant rejectedAt, string reason,
        CancellationToken cancellationToken = default)
    {
        var entry = _repo.GetByIdAnyTenant(id)
            ?? throw new InvalidOperationException($"TimeEntry {id.Value} not found.");
        entry.Reject(reason, approverPartyId, rejectedAt);
        _repo.Upsert(entry);
        return Task.FromResult(entry);
    }
}

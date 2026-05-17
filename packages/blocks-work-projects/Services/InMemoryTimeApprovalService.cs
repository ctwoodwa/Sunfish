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
/// <see cref="IDomainEventPublisher"/>. Envelope <c>TenantId</c> is
/// derived from the entry, not ctor-bound.
/// </summary>
public sealed class InMemoryTimeApprovalService : ITimeApprovalService
{
    private const int PayloadSchemaVersion = 1;

    private readonly InMemoryTimeEntryRepository _repo;
    private readonly IDomainEventPublisher _events;
    private readonly ReplicaId _envelopeReplicaId;

    public InMemoryTimeApprovalService(
        InMemoryTimeEntryRepository repo,
        IDomainEventPublisher events,
        ReplicaId? envelopeReplicaId = null)
    {
        _repo              = repo   ?? throw new ArgumentNullException(nameof(repo));
        _events            = events ?? throw new ArgumentNullException(nameof(events));
        _envelopeReplicaId = envelopeReplicaId ?? ReplicaId.System;
    }

    /// <inheritdoc />
    public async Task<TimeEntry> ApproveAsync(
        TenantId tenantId, TimeEntryId id, Guid approverPartyId, Instant approvedAt,
        CancellationToken cancellationToken = default)
    {
        var entry = _repo.GetById(tenantId, id)
            ?? throw new InvalidOperationException($"TimeEntry {id.Value} not found in tenant {tenantId}.");
        entry.Approve(approverPartyId, approvedAt);
        _repo.Upsert(entry);

        var envelope = new DomainEventEnvelope<TimeEntryApprovedEvent>
        {
            EventId              = EventId.New(),
            EventType            = "Work.TimeEntryApproved",
            SchemaVersion        = PayloadSchemaVersion,
            OccurredAt           = approvedAt.Value,
            TenantId             = entry.TenantId,
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
        TenantId tenantId, TimeEntryId id, Guid rejecterPartyId, Instant rejectedAt, string reason,
        CancellationToken cancellationToken = default)
    {
        var entry = _repo.GetById(tenantId, id)
            ?? throw new InvalidOperationException($"TimeEntry {id.Value} not found in tenant {tenantId}.");
        entry.Reject(reason, rejecterPartyId, rejectedAt);
        _repo.Upsert(entry);
        return Task.FromResult(entry);
    }
}

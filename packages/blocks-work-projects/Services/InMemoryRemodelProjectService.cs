using System.Collections.Concurrent;
using Sunfish.Blocks.WorkProjects.Events;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Default <see cref="IRemodelProjectService"/>. Tenant-scoped reads
/// enforce H5. Capitalize is one-shot (entity guard); cross-tenant
/// callers cannot mutate via <see cref="CapitalizeAsync"/> because
/// the lookup uses <c>GetById(tenant, id)</c>.
/// </summary>
public sealed class InMemoryRemodelProjectService : IRemodelProjectService
{
    private const int PayloadSchemaVersion = 1;

    private readonly ConcurrentDictionary<RemodelProjectId, RemodelProject> _projects = new();
    private readonly ConcurrentDictionary<RemodelPhaseId, RemodelPhase> _phases = new();
    private readonly IDomainEventPublisher _events;
    private readonly ReplicaId _envelopeReplicaId;
    private readonly Func<Instant> _now;

    public InMemoryRemodelProjectService(
        IDomainEventPublisher events,
        ReplicaId? envelopeReplicaId = null,
        Func<Instant>? now = null)
    {
        _events            = events ?? throw new ArgumentNullException(nameof(events));
        _envelopeReplicaId = envelopeReplicaId ?? ReplicaId.System;
        _now               = now ?? (() => Instant.Now);
    }

    /// <inheritdoc />
    public Task<RemodelProject> CreateAsync(
        TenantId tenantId, ProjectId projectId, string scopeStatement, RemodelKind remodelKind,
        bool permitRequired, Guid createdBy,
        IReadOnlyList<string>? inspectionsRequired = null,
        CancellationToken cancellationToken = default)
    {
        var now = _now();
        var rp = RemodelProject.Create(
            tenantId, RemodelProjectId.NewId(), projectId, scopeStatement, remodelKind,
            permitRequired, inspectionsRequired, createdBy, now);
        _projects[rp.Id] = rp;
        return Task.FromResult(rp);
    }

    /// <inheritdoc />
    public Task<RemodelPhase> AddPhaseAsync(
        TenantId tenantId, RemodelProjectId remodelProjectId, int ordinal, string name,
        decimal budgetedAmount, string budgetedCurrency, Guid createdBy,
        DateOnly? plannedStartDate = null, DateOnly? plannedEndDate = null,
        CancellationToken cancellationToken = default)
    {
        var rp = GetTenantProject(tenantId, remodelProjectId);
        if (_phases.Values.Any(p =>
            p.RemodelProjectId.Value == remodelProjectId.Value && p.Ordinal == ordinal))
            throw new InvalidOperationException(
                $"RemodelPhase with ordinal {ordinal} already exists on RemodelProject {remodelProjectId.Value}.");
        var phase = RemodelPhase.Create(
            tenantId, RemodelPhaseId.NewId(), remodelProjectId, ordinal, name,
            budgetedAmount, budgetedCurrency, plannedStartDate, plannedEndDate, createdBy, _now());
        _phases[phase.Id] = phase;
        return Task.FromResult(phase);
    }

    /// <inheritdoc />
    public Task<RemodelPhase> StartPhaseAsync(
        TenantId tenantId, RemodelPhaseId phaseId, DateOnly startDate, Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var phase = GetTenantPhase(tenantId, phaseId);
        phase.Start(startDate, updatedBy, _now());
        return Task.FromResult(phase);
    }

    /// <inheritdoc />
    public async Task<RemodelPhase> MarkPhaseCompleteAsync(
        TenantId tenantId, RemodelPhaseId phaseId, DateOnly endDate,
        decimal? actualAmount, string? actualCurrency, Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var phase = GetTenantPhase(tenantId, phaseId);
        var rp = GetTenantProject(tenantId, phase.RemodelProjectId);
        var now = _now();
        phase.Complete(endDate, actualAmount, updatedBy, now);

        var envelope = new DomainEventEnvelope<RemodelPhaseCompletedEvent>
        {
            EventId              = EventId.New(),
            EventType            = "Work.RemodelPhaseCompleted",
            SchemaVersion        = PayloadSchemaVersion,
            OccurredAt           = now.Value,
            TenantId             = phase.TenantId,
            OriginatingReplicaId = _envelopeReplicaId,
            IdempotencyKey       = $"remodel-phase-completed:{phase.Id.Value}",
            Payload              = new RemodelPhaseCompletedEvent(
                PhaseId:          phase.Id,
                RemodelProjectId: phase.RemodelProjectId,
                ProjectId:        rp.ProjectId,
                Ordinal:          phase.Ordinal,
                Name:             phase.Name,
                ActualAmount:     phase.ActualAmount,
                Currency:         actualCurrency is null ? null : RemodelPhase.NormalizeCurrency(actualCurrency, nameof(actualCurrency)),
                ActualEndDate:    endDate),
        };
        await _events.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);
        return phase;
    }

    /// <inheritdoc />
    public async Task<RemodelProject> CapitalizeAsync(
        TenantId tenantId, RemodelProjectId remodelProjectId,
        Guid capitalizationAccountId, DateOnly placedInServiceAt,
        decimal capitalizedAmount, string currency, Guid updatedBy,
        Guid? propertyId = null,
        CancellationToken cancellationToken = default)
    {
        var rp = GetTenantProject(tenantId, remodelProjectId);
        var phases = _phases.Values
            .Where(p => p.RemodelProjectId.Value == remodelProjectId.Value)
            .ToList();
        if (phases.Any(p => p.Status == PhaseStatus.Planned || p.Status == PhaseStatus.Active))
            throw new RemodelHasIncompletePhasesException(remodelProjectId);

        var now = _now();
        rp.Capitalize(capitalizationAccountId, placedInServiceAt, capitalizedAmount, currency, updatedBy, now);

        var envelope = new DomainEventEnvelope<RemodelCapitalizedEvent>
        {
            EventId              = EventId.New(),
            EventType            = "Work.RemodelCapitalized",
            SchemaVersion        = PayloadSchemaVersion,
            OccurredAt           = now.Value,
            TenantId             = rp.TenantId,
            OriginatingReplicaId = _envelopeReplicaId,
            IdempotencyKey       = $"remodel-capitalized:{rp.Id.Value}",
            Payload              = new RemodelCapitalizedEvent(
                RemodelProjectId:        rp.Id,
                ProjectId:               rp.ProjectId,
                PropertyId:              propertyId,
                CapitalizationAccountId: capitalizationAccountId,
                CapitalizedAmount:       capitalizedAmount,
                Currency:                rp.CapitalizedCurrency!,
                PlacedInServiceDate:     placedInServiceAt),
        };
        await _events.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);
        return rp;
    }

    /// <inheritdoc />
    public Task<RemodelProject?> GetByIdAsync(TenantId tenantId, RemodelProjectId id, CancellationToken cancellationToken = default)
    {
        if (!_projects.TryGetValue(id, out var rp)) return Task.FromResult<RemodelProject?>(null);
        if (!rp.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal))
            return Task.FromResult<RemodelProject?>(null);
        return Task.FromResult<RemodelProject?>(rp);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RemodelPhase>> GetPhasesAsync(
        TenantId tenantId, RemodelProjectId remodelProjectId, CancellationToken cancellationToken = default)
    {
        _ = GetTenantProject(tenantId, remodelProjectId);
        IReadOnlyList<RemodelPhase> phases = _phases.Values
            .Where(p => p.RemodelProjectId.Value == remodelProjectId.Value)
            .OrderBy(p => p.Ordinal)
            .ToList();
        return Task.FromResult(phases);
    }

    private RemodelProject GetTenantProject(TenantId tenantId, RemodelProjectId id)
    {
        if (!_projects.TryGetValue(id, out var rp))
            throw new InvalidOperationException($"RemodelProject {id.Value} not found.");
        if (!rp.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"RemodelProject {id.Value} not found in tenant {tenantId}.");
        return rp;
    }

    private RemodelPhase GetTenantPhase(TenantId tenantId, RemodelPhaseId id)
    {
        if (!_phases.TryGetValue(id, out var phase))
            throw new InvalidOperationException($"RemodelPhase {id.Value} not found.");
        if (!phase.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"RemodelPhase {id.Value} not found in tenant {tenantId}.");
        return phase;
    }
}

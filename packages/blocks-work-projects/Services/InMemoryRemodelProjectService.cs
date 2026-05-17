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
        // Derive child tenant from parent (NOT the caller-supplied tenantId)
        // — a buggy caller passing a mismatched tenantId here would otherwise
        // orphan the phase against its parent project.
        var phase = RemodelPhase.Create(
            rp.TenantId, RemodelPhaseId.NewId(), remodelProjectId, ordinal, name,
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

        // Build + publish envelope BEFORE mutating the entity. Phase
        // completion is terminal — once Status flips to Complete, a
        // failed publish leaves no path to re-emit (entity guard would
        // throw "Cannot Complete a phase in status Complete" on retry).
        // We validate the inputs by invoking the entity validation
        // logic eagerly via a dry-run (re-throw bubbles before publish);
        // the actual state mutation happens only after the bus accepts.
        ValidatePhaseCompletePreconditions(phase, endDate, actualAmount);

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
                ActualAmount:     actualAmount,
                Currency:         actualCurrency is null ? null : RemodelPhase.NormalizeCurrency(actualCurrency, nameof(actualCurrency)),
                ActualEndDate:    endDate),
        };
        await _events.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);

        // Publish succeeded — now safe to commit the terminal state.
        phase.Complete(endDate, actualAmount, updatedBy, now);
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

        // Validate entity-level preconditions BEFORE publishing — same
        // ordering rationale as MarkPhaseComplete. Capitalize is one-
        // shot; a failed publish after mutation would strand the work-
        // projects side as "capitalized" with no event ever delivered
        // to the financial cluster.
        if (rp.CapitalizedAt is not null)
            throw new InvalidOperationException("RemodelProject is already capitalized.");
        var normalizedCurrency = RemodelPhase.NormalizeCurrency(currency, nameof(currency));

        var now = _now();
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
                Currency:                normalizedCurrency,
                PlacedInServiceDate:     placedInServiceAt),
        };
        await _events.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);

        // Publish succeeded — now safe to commit the terminal state.
        rp.Capitalize(capitalizationAccountId, placedInServiceAt, capitalizedAmount, currency, updatedBy, now);
        return rp;
    }

    private static void ValidatePhaseCompletePreconditions(RemodelPhase phase, DateOnly endDate, decimal? actualAmount)
    {
        if (phase.Status != PhaseStatus.Active)
            throw new InvalidOperationException($"Cannot Complete a phase in status {phase.Status}.");
        if (actualAmount is { } amt && amt < 0m)
            throw new ArgumentException("ActualAmount must be >= 0.", nameof(actualAmount));
        if (phase.ActualStartDate is { } start && endDate < start)
            throw new ArgumentException("ActualEndDate must be >= ActualStartDate.", nameof(endDate));
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

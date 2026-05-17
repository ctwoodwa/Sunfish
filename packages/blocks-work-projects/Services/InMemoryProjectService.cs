using Sunfish.Blocks.WorkProjects.Events;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>Default <see cref="IProjectService"/>.</summary>
public sealed class InMemoryProjectService : IProjectService
{
    private const int PayloadSchemaVersion = 1;

    private readonly InMemoryProjectRepository _projects;
    private readonly InMemoryProjectMilestoneRepository _milestones;
    private readonly IProjectCodeGenerator _codes;
    private readonly IDomainEventPublisher _events;
    private readonly ReplicaId _envelopeReplicaId;
    private readonly Func<Instant> _now;

    public InMemoryProjectService(
        InMemoryProjectRepository projects,
        InMemoryProjectMilestoneRepository milestones,
        IProjectCodeGenerator codes,
        IDomainEventPublisher events,
        ReplicaId? envelopeReplicaId = null,
        Func<Instant>? now = null)
    {
        _projects          = projects   ?? throw new ArgumentNullException(nameof(projects));
        _milestones        = milestones ?? throw new ArgumentNullException(nameof(milestones));
        _codes             = codes      ?? throw new ArgumentNullException(nameof(codes));
        _events            = events     ?? throw new ArgumentNullException(nameof(events));
        _envelopeReplicaId = envelopeReplicaId ?? ReplicaId.System;
        _now               = now ?? (() => Instant.Now);
    }

    /// <inheritdoc />
    public async Task<Project> CreateAsync(
        TenantId tenantId, string name, ProjectKind kind, Priority priority, Guid ownerPartyId, Guid createdBy,
        string? description = null, Guid? propertyId = null, Guid? customerPartyId = null,
        ProjectId? parentProjectId = null, DateOnly? plannedStartDate = null, DateOnly? plannedEndDate = null,
        CancellationToken cancellationToken = default)
    {
        var now = _now();
        var code = await _codes.NextAsync(tenantId, now.Value.Year, cancellationToken).ConfigureAwait(false);
        var project = Project.Create(
            tenantId, ProjectId.NewId(), code, name, kind, priority, ownerPartyId, createdBy, now,
            description: description,
            propertyId: propertyId,
            customerPartyId: customerPartyId,
            parentProjectId: parentProjectId,
            plannedStartDate: plannedStartDate,
            plannedEndDate: plannedEndDate);

        var envelope = new DomainEventEnvelope<ProjectCreatedEvent>
        {
            EventId              = EventId.New(),
            EventType            = "Work.ProjectCreated",
            SchemaVersion        = PayloadSchemaVersion,
            OccurredAt           = now.Value,
            TenantId             = tenantId,
            OriginatingReplicaId = _envelopeReplicaId,
            IdempotencyKey       = $"project-created:{project.Id.Value}",
            Payload              = new ProjectCreatedEvent(
                ProjectId:       project.Id,
                Code:            project.Code,
                Name:            project.Name,
                Kind:            project.Kind,
                PropertyId:      project.PropertyId,
                CustomerPartyId: project.CustomerPartyId,
                OwnerPartyId:    project.OwnerPartyId),
        };
        await _events.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);
        _projects.Upsert(project);
        return project;
    }

    /// <inheritdoc />
    public async Task<Project> TransitionStatusAsync(
        TenantId tenantId, ProjectId id, ProjectStatus to, Guid actingPartyId, Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var project = _projects.GetById(tenantId, id)
            ?? throw new InvalidOperationException($"Project {id.Value} not found in tenant {tenantId}.");

        if (actingPartyId != project.OwnerPartyId)
            throw new NotProjectOwnerException(project.Id, actingPartyId);

        // Validate transition BEFORE publishing (re-uses the entity guard
        // without committing the mutation) — keeps the publish-before-mutate
        // pattern from PR 5.
        if (!ProjectStatusMachine.CanTransition(project.Status, to))
            throw new InvalidProjectStatusTransitionException(project.Status, to);

        var from = project.Status;
        var now = _now();

        var envelope = new DomainEventEnvelope<ProjectStatusChangedEvent>
        {
            EventId              = EventId.New(),
            EventType            = "Work.ProjectStatusChanged",
            SchemaVersion        = PayloadSchemaVersion,
            OccurredAt           = now.Value,
            TenantId             = tenantId,
            OriginatingReplicaId = _envelopeReplicaId,
            IdempotencyKey       = $"project-status:{project.Id.Value}:{now.Value.UtcTicks}",
            Payload              = new ProjectStatusChangedEvent(
                ProjectId:             project.Id,
                FromStatus:            from,
                ToStatus:              to,
                TransitionedByPartyId: actingPartyId,
                TransitionedAt:        now),
        };
        await _events.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);

        project.TransitionStatus(to, updatedBy, now);
        _projects.Upsert(project);
        return project;
    }

    /// <inheritdoc />
    public async Task<ProjectMilestone> AddMilestoneAsync(
        TenantId tenantId, ProjectId projectId, string code, string name, MilestoneKind kind,
        DateOnly plannedDate, Guid createdBy,
        decimal? weight = null, decimal? paymentAmount = null, string? paymentCurrency = null,
        bool triggersInvoice = false, Guid? customerPartyId = null,
        CancellationToken cancellationToken = default)
    {
        var project = _projects.GetById(tenantId, projectId)
            ?? throw new InvalidOperationException($"Project {projectId.Value} not found in tenant {tenantId}.");

        var now = _now();
        var milestone = ProjectMilestone.Create(
            tenantId: project.TenantId,
            id: MilestoneId.NewId(),
            projectId: project.Id,
            code: code, name: name, kind: kind, plannedDate: plannedDate,
            createdBy: createdBy, createdAt: now,
            weight: weight, paymentAmount: paymentAmount, paymentCurrency: paymentCurrency,
            triggersInvoice: triggersInvoice, customerPartyId: customerPartyId);

        var envelope = new DomainEventEnvelope<MilestoneCreatedEvent>
        {
            EventId              = EventId.New(),
            EventType            = "Work.MilestoneCreated",
            SchemaVersion        = PayloadSchemaVersion,
            OccurredAt           = now.Value,
            TenantId             = tenantId,
            OriginatingReplicaId = _envelopeReplicaId,
            IdempotencyKey       = $"milestone-created:{milestone.Id.Value}",
            Payload              = new MilestoneCreatedEvent(
                MilestoneId:     milestone.Id,
                ProjectId:       project.Id,
                Code:            milestone.Code,
                Kind:            milestone.Kind,
                PlannedDate:     milestone.PlannedDate,
                PaymentAmount:   milestone.PaymentAmount,
                PaymentCurrency: milestone.PaymentCurrency,
                TriggersInvoice: milestone.TriggersInvoice),
        };
        await _events.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);
        _milestones.Upsert(milestone);
        return milestone;
    }

    /// <inheritdoc />
    public async Task<ProjectMilestone> AchieveMilestoneAsync(
        TenantId tenantId, MilestoneId id, DateOnly actualDate, Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var milestone = _milestones.GetById(tenantId, id)
            ?? throw new InvalidOperationException($"ProjectMilestone {id.Value} not found in tenant {tenantId}.");

        var now = _now();

        // Publish achievement BEFORE mutating — terminal-state event,
        // replay-on-failure pattern from PR 5.
        var achievedEnvelope = new DomainEventEnvelope<MilestoneAchievedEvent>
        {
            EventId              = EventId.New(),
            EventType            = "Work.MilestoneAchieved",
            SchemaVersion        = PayloadSchemaVersion,
            OccurredAt           = now.Value,
            TenantId             = tenantId,
            OriginatingReplicaId = _envelopeReplicaId,
            IdempotencyKey       = $"milestone-achieved:{milestone.Id.Value}",
            Payload              = new MilestoneAchievedEvent(
                MilestoneId:   milestone.Id,
                ProjectId:     milestone.ProjectId,
                AchievedDate:  actualDate,
                Weight:        milestone.Weight),
        };
        await _events.PublishAsync(achievedEnvelope, cancellationToken).ConfigureAwait(false);

        if (milestone.TriggersInvoice)
        {
            // Entity invariant: TriggersInvoice == true MUST imply all
            // three fields present (validated on Create). If any is null
            // here, storage corruption / schema-migration bug — throw
            // loudly so AR invoices are never silently lost.
            if (milestone.PaymentAmount is null || milestone.PaymentCurrency is null || milestone.CustomerPartyId is null)
                throw new InvalidOperationException(
                    $"Milestone {milestone.Id.Value} has TriggersInvoice=true but is missing "
                    + "PaymentAmount/PaymentCurrency/CustomerPartyId — entity invariant violated.");
            var amt  = milestone.PaymentAmount.Value;
            var cur  = milestone.PaymentCurrency;
            var cust = milestone.CustomerPartyId.Value;

            var invoiceEnvelope = new DomainEventEnvelope<MilestoneInvoiceTriggeredEvent>
            {
                EventId              = EventId.New(),
                EventType            = "Work.MilestoneInvoiceTriggered",
                SchemaVersion        = PayloadSchemaVersion,
                OccurredAt           = now.Value,
                TenantId             = tenantId,
                OriginatingReplicaId = _envelopeReplicaId,
                IdempotencyKey       = $"milestone-invoice:{milestone.Id.Value}",
                Payload              = new MilestoneInvoiceTriggeredEvent(
                    MilestoneId:      milestone.Id,
                    ProjectId:        milestone.ProjectId,
                    PaymentAmount:    amt,
                    PaymentCurrency:  cur,
                    CustomerPartyId:  cust),
            };
            await _events.PublishAsync(invoiceEnvelope, cancellationToken).ConfigureAwait(false);
        }

        milestone.Achieve(actualDate, updatedBy, now);
        _milestones.Upsert(milestone);
        return milestone;
    }
}

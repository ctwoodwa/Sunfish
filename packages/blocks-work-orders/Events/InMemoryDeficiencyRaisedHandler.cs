using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Blocks.WorkOrders.Services;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkOrders.Events;

/// <summary>
/// Default <see cref="IDeficiencyRaisedHandler"/>. Uses
/// <see cref="InMemoryWorkOrderRepository.FindByDeficiencyId"/> for
/// the idempotency check, then delegates creation to
/// <see cref="IWorkOrderService.CreateAsync"/>. Severity-string
/// mapping is intentionally narrow (Safety/Habitability map to a
/// real severity; everything else maps to Major) — the
/// blocks-property cluster's severity vocabulary is the source of
/// truth; unknown values default to Major so the WO still has a
/// defensible classification.
/// </summary>
public sealed class InMemoryDeficiencyRaisedHandler : IDeficiencyRaisedHandler
{
    private readonly InMemoryWorkOrderRepository _repo;
    private readonly IWorkOrderService _workOrderService;
    private readonly TenantId _tenantId;

    public InMemoryDeficiencyRaisedHandler(
        InMemoryWorkOrderRepository repo,
        IWorkOrderService workOrderService,
        TenantId? tenantId = null)
    {
        _repo             = repo             ?? throw new ArgumentNullException(nameof(repo));
        _workOrderService = workOrderService ?? throw new ArgumentNullException(nameof(workOrderService));
        _tenantId         = tenantId ?? TenantId.System;
    }

    /// <inheritdoc />
    public async Task<WorkOrder> HandleAsync(
        DeficiencyRaisedEvent evt,
        Guid handledBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        // Idempotency — never create a second WO for the same
        // deficiency.
        var existing = _repo.FindByDeficiencyId(_tenantId, evt.DeficiencyId);
        if (existing is not null) return existing;

        var severity = MapSeverity(evt.Severity);
        var dueBy    = evt.DueBy ?? RequireDueByForSafetyOrHabitability(severity);
        var priority = severity switch
        {
            WorkOrderSeverity.Safety       => Priority.Critical,
            WorkOrderSeverity.Habitability => Priority.High,
            WorkOrderSeverity.Major        => Priority.High,
            _                              => Priority.Normal,
        };

        return await _workOrderService.CreateAsync(
            tenantId:     _tenantId,
            title:        evt.Description,
            kind:         WorkOrderKind.Repair,
            priority:     priority,
            createdBy:    handledBy,
            severity:     severity,
            dueBy:        dueBy,
            propertyId:   evt.PropertyId,
            unitId:       evt.UnitId,
            assetId:      evt.AssetId,
            deficiencyId: evt.DeficiencyId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static WorkOrderSeverity MapSeverity(string severity)
        => severity?.Trim().ToLowerInvariant() switch
        {
            "safety"       => WorkOrderSeverity.Safety,
            "habitability" => WorkOrderSeverity.Habitability,
            "major"        => WorkOrderSeverity.Major,
            "minor"        => WorkOrderSeverity.Minor,
            "cosmetic"     => WorkOrderSeverity.Cosmetic,
            _              => WorkOrderSeverity.Major,
        };

    /// <summary>
    /// Default DueBy when the deficiency event didn't carry one but
    /// the mapped severity requires it. Falls back to 48h for Safety,
    /// 7d for Habitability — the SLA the blocks-property cluster
    /// surfaces today; PR 5's wiring layer can override per-tenant
    /// policy.
    /// </summary>
    private static DateTimeOffset? RequireDueByForSafetyOrHabitability(WorkOrderSeverity severity)
        => severity switch
        {
            WorkOrderSeverity.Safety       => DateTimeOffset.UtcNow.AddHours(48),
            WorkOrderSeverity.Habitability => DateTimeOffset.UtcNow.AddDays(7),
            _                              => null,
        };
}

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Authorization;

namespace Sunfish.Bridge.Cockpit;

/// <summary>
/// W#29 Phase 3 — owner cockpit work-orders surface.
/// GET /api/v1/cockpit/work-orders        — paginated list (status/vendor/date filters)
/// GET /api/v1/cockpit/work-orders/{id}   — detail (vendor lookup + entry notices + appointment + attestation + audit trail)
///
/// Per XO ruling on the W#29 P2 halt: property filter is intentionally
/// omitted because the WorkOrder model has no PropertyId field. Adding it
/// is the scope of W#62 / W#62.1 (api-change PR against blocks-maintenance),
/// not this cockpit PR.
///
/// "Linked inspection if any" from the hand-off detail spec is also omitted
/// — WorkOrder has no Inspection FK; the cross-link is unresolved until
/// W#62 or a follow-up.
/// </summary>
public static class WorkOrdersEndpoint
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static RouteGroupBuilder MapWorkOrders(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.MapGet("/work-orders",        HandleListWorkOrdersAsync).WithName("CockpitListWorkOrders");
        group.MapGet("/work-orders/{id}",   HandleGetWorkOrderDetailAsync).WithName("CockpitGetWorkOrderDetail");
        return group;
    }

    internal static async Task<Ok<WorkOrderListDto>> HandleListWorkOrdersAsync(
        ITenantContext tenantContext,
        IMaintenanceService maintenance,
        string? status,
        string? vendorId,
        DateOnly? from,
        DateOnly? to,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        _ = tenantContext;

        var query = ListWorkOrdersQuery.Empty;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<WorkOrderStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query with { Status = parsedStatus };
        if (!string.IsNullOrWhiteSpace(vendorId))
            query = query with { VendorId = new VendorId(vendorId) };

        var rows = new List<WorkOrder>();
        await foreach (var wo in maintenance.ListWorkOrdersAsync(query, ct).ConfigureAwait(false))
        {
            // Date-range filter applied client-side: ListWorkOrdersQuery has no
            // date filter today. Acceptable for one tenant's WO volume; revisit
            // when the query record gains a scheduled-date filter.
            if (from is not null && wo.ScheduledDate < from) continue;
            if (to   is not null && wo.ScheduledDate > to)   continue;
            rows.Add(wo);
        }

        // Stable order: most-recently scheduled first, then by id for determinism.
        rows.Sort((a, b) =>
        {
            var d = b.ScheduledDate.CompareTo(a.ScheduledDate);
            return d != 0 ? d : string.CompareOrdinal(a.Id.Value, b.Id.Value);
        });

        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var idx = Math.Max(0, (page ?? 1) - 1);
        var total = rows.Count;
        var pageRows = rows.Skip(idx * size).Take(size).Select(MapSummary).ToArray();

        return TypedResults.Ok(new WorkOrderListDto(
            Items:    pageRows,
            Total:    total,
            Page:     idx + 1,
            PageSize: size));
    }

    internal static async Task<Results<Ok<WorkOrderDetailDto>, NotFound>> HandleGetWorkOrderDetailAsync(
        string id,
        ITenantContext tenantContext,
        IMaintenanceService maintenance,
        CancellationToken ct)
    {
        _ = tenantContext;

        var wo = await maintenance.GetWorkOrderAsync(new WorkOrderId(id), ct).ConfigureAwait(false);
        if (wo is null)
            return TypedResults.NotFound();

        var vendor = await maintenance.GetVendorAsync(wo.AssignedVendorId, ct).ConfigureAwait(false);

        var dto = new WorkOrderDetailDto(
            WorkOrderId:           wo.Id.Value,
            Status:                wo.Status.ToString(),
            ScheduledDate:         wo.ScheduledDate,
            CompletedDate:         wo.CompletedDate,
            VendorId:              wo.AssignedVendorId.Value,
            VendorDisplayName:     vendor?.DisplayName ?? wo.AssignedVendorId.Value,
            Notes:                 wo.Notes,
            EntryNotices:          wo.EntryNotices.Select(n => new EntryNoticeSummary(
                                       PlannedEntryUtc: n.PlannedEntryUtc,
                                       EntryReason:     n.EntryReason)).ToArray(),
            Appointment:           wo.Appointment is null ? null : new AppointmentSummary(
                                       SlotStartUtc:   wo.Appointment.SlotStartUtc,
                                       SlotEndUtc:     wo.Appointment.SlotEndUtc,
                                       Status:         wo.Appointment.Status.ToString()),
            CompletionAttestation: wo.CompletionAttestation is null ? null : new CompletionAttestationSummary(
                                       AttestedAt:     wo.CompletionAttestation.AttestedAt,
                                       SignatureRef:   wo.CompletionAttestation.Signature.SignatureEventId.ToString()),
            AuditTrail:            wo.AuditTrail.TakeLast(10).Select(g => g.ToString()).ToArray());

        return TypedResults.Ok(dto);
    }

    private static WorkOrderSummary MapSummary(WorkOrder wo) => new(
        WorkOrderId:       wo.Id.Value,
        Status:            wo.Status.ToString(),
        VendorId:          wo.AssignedVendorId.Value,
        ScheduledDate:     wo.ScheduledDate,
        CompletedDate:     wo.CompletedDate,
        AppointmentDate:   wo.Appointment?.SlotStartUtc);
}

// ── Wire formats ────────────────────────────────────────────────────────

public record WorkOrderListDto(
    IReadOnlyList<WorkOrderSummary> Items,
    int Total,
    int Page,
    int PageSize);

public record WorkOrderSummary(
    string WorkOrderId,
    string Status,
    string VendorId,
    DateOnly ScheduledDate,
    DateOnly? CompletedDate,
    DateTimeOffset? AppointmentDate);

public record WorkOrderDetailDto(
    string WorkOrderId,
    string Status,
    DateOnly ScheduledDate,
    DateOnly? CompletedDate,
    string VendorId,
    string VendorDisplayName,
    string? Notes,
    IReadOnlyList<EntryNoticeSummary> EntryNotices,
    AppointmentSummary? Appointment,
    CompletionAttestationSummary? CompletionAttestation,
    IReadOnlyList<string> AuditTrail);

public record EntryNoticeSummary(DateTimeOffset PlannedEntryUtc, string EntryReason);
public record AppointmentSummary(DateTimeOffset SlotStartUtc, DateTimeOffset SlotEndUtc, string Status);
public record CompletionAttestationSummary(DateTimeOffset AttestedAt, string SignatureRef);

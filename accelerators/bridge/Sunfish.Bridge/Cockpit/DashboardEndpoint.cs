using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Sunfish.Blocks.Inspections.Services;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;

namespace Sunfish.Bridge.Cockpit;

/// <summary>
/// W#29 Phase 5 — owner cockpit per-property dashboard.
/// GET /api/v1/cockpit/{propertyId}/dashboard returns four widget rollups:
/// vacancy rate, renewal radar (30/60/90-day buckets), work-order status
/// summary, and overdue-inspection unit list.
///
/// All four widgets cross-reference Property → PropertyUnit → {Lease /
/// Inspection / WorkOrder} via the W#62 substrate; PropertyId-filtered
/// WorkOrder query lands directly from W#62 PR 3.
/// </summary>
public static class DashboardEndpoint
{
    /// <summary>30/60/90 day renewal buckets per hand-off.</summary>
    private static readonly int[] RenewalBucketDays = [30, 60, 90];

    /// <summary>Inspections older than this are flagged as overdue.</summary>
    private static readonly TimeSpan OverdueThreshold = TimeSpan.FromDays(365);

    public static RouteGroupBuilder MapDashboard(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.MapGet("/{propertyId}/dashboard", HandleGetDashboardAsync).WithName("CockpitGetDashboard");
        return group;
    }

    internal static async Task<Results<Ok<DashboardDto>, NotFound>> HandleGetDashboardAsync(
        string propertyId,
        ITenantContext tenantContext,
        IPropertyRepository properties,
        IPropertyUnitRepository unitRepo,
        ILeaseService leases,
        IInspectionsService inspections,
        IMaintenanceService maintenance,
        CancellationToken ct)
    {
        TenantId tenant = tenantContext.TenantId;
        var typedId = new PropertyId(propertyId);

        var property = await properties.GetByIdAsync(tenant, typedId, ct).ConfigureAwait(false);
        if (property is null) return TypedResults.NotFound();

        var units = await unitRepo.ListByPropertyAsync(tenant, typedId, ct).ConfigureAwait(false);
        var unitIds = units.Select(u => u.Id).ToHashSet();

        // ── Vacancy rate ────────────────────────────────────────────────────
        var totalUnits  = units.Count;
        var vacantUnits = units.Count(u => u.Status == UnitStatus.Available);

        // ── Renewal radar: leases expiring in 30/60/90 days ─────────────────
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var bucketCounts = new int[RenewalBucketDays.Length];
        if (unitIds.Count > 0)
        {
            await foreach (var lease in leases.ListAsync(new ListLeasesQuery { Phase = LeasePhase.Active }, ct).ConfigureAwait(false))
            {
                if (!unitIds.Contains(lease.UnitId)) continue;
                var daysOut = lease.EndDate.DayNumber - today.DayNumber;
                if (daysOut < 0) continue;
                for (var i = 0; i < RenewalBucketDays.Length; i++)
                {
                    if (daysOut <= RenewalBucketDays[i])
                    {
                        bucketCounts[i]++;
                        break;
                    }
                }
            }
        }
        var upcomingRenewals = RenewalBucketDays
            .Select((days, idx) => new RenewalBucket(WithinDays: days, Count: bucketCounts[idx]))
            .ToArray();

        // ── Work-order status summary (per-property, no unit join needed) ───
        var openCount = 0;
        var inProgressCount = 0;
        var blockedCount = 0;
        await foreach (var wo in maintenance.ListWorkOrdersAsync(
            new ListWorkOrdersQuery { PropertyId = typedId }, ct).ConfigureAwait(false))
        {
            switch (wo.Status)
            {
                case WorkOrderStatus.Draft:
                case WorkOrderStatus.Sent:
                case WorkOrderStatus.Accepted:
                case WorkOrderStatus.Scheduled:
                    openCount++;
                    break;
                case WorkOrderStatus.InProgress:
                    inProgressCount++;
                    break;
                case WorkOrderStatus.OnHold:
                    blockedCount++;
                    break;
                // Completed / AwaitingSignOff / Invoiced / Paid / Closed / Cancelled /
                // Disputed are not counted in any of the three rollup buckets.
            }
        }

        // ── Overdue inspections: last inspection > 12 months per unit ───────
        var overdueUnitIds = new List<string>();
        if (unitIds.Count > 0)
        {
            // For each unit, find the most recent inspection (by ScheduledDate).
            // Surface the unit when no inspection exists OR latest is >12 months ago.
            var latestByUnit = new Dictionary<EntityId, DateOnly>();
            await foreach (var inspection in inspections.ListInspectionsAsync(new ListInspectionsQuery(), ct).ConfigureAwait(false))
            {
                if (!unitIds.Contains(inspection.UnitId)) continue;
                if (!latestByUnit.TryGetValue(inspection.UnitId, out var prev) || inspection.ScheduledDate > prev)
                    latestByUnit[inspection.UnitId] = inspection.ScheduledDate;
            }
            foreach (var unit in units)
            {
                if (!latestByUnit.TryGetValue(unit.Id, out var latest))
                {
                    overdueUnitIds.Add(unit.Id.ToString());
                    continue;
                }
                var age = today.DayNumber - latest.DayNumber;
                if (age > OverdueThreshold.TotalDays)
                    overdueUnitIds.Add(unit.Id.ToString());
            }
        }

        var dto = new DashboardDto(
            TotalUnits:               totalUnits,
            VacantUnits:              vacantUnits,
            UpcomingRenewals:         upcomingRenewals,
            WorkOrders:               new WorkOrderSummaryDto(Open: openCount, InProgress: inProgressCount, Blocked: blockedCount),
            OverdueInspectionUnitIds: overdueUnitIds);

        return TypedResults.Ok(dto);
    }
}

// ── Wire formats ────────────────────────────────────────────────────────

public record DashboardDto(
    int TotalUnits,
    int VacantUnits,
    IReadOnlyList<RenewalBucket> UpcomingRenewals,
    WorkOrderSummaryDto WorkOrders,
    IReadOnlyList<string> OverdueInspectionUnitIds);

public record RenewalBucket(int WithinDays, int Count);

public record WorkOrderSummaryDto(int Open, int InProgress, int Blocked);

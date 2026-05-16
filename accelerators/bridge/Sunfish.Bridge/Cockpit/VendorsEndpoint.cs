using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Authorization;
// Note: IW9DocumentService is intentionally NOT consumed here. The chip only
// needs to know whether a W9 is on file (vendor.W9 is null vs not null);
// resolving the full W9Document requires the tenant-key + encryption substrate
// and is out of scope for the cockpit read-only surface.

namespace Sunfish.Bridge.Cockpit;

/// <summary>
/// W#29 Phase 4 — owner cockpit vendors surface.
///
/// GET /api/v1/cockpit/vendors      — list with W-9 status + YTD-payment + 1099-readiness flags
/// GET /api/v1/cockpit/vendors/{id} — vendor detail (W-9, contacts, performance log last 5, work-order history)
///
/// 1099 readiness logic (per hand-off):
///   needsForm1099 = OnboardingState == Active
///                   AND W-9 missing (vendor.W9 == null)
///                   AND ytdPayments &gt; $600
///
/// Hand-off uses "OnboardingState == Completed" but the actual enum
/// (Pending / W9Requested / W9Received / Active / Suspended / Retired)
/// has no "Completed" — `Active` is the closest equivalent (vendor fully
/// onboarded and engageable).
///
/// YTD payments aggregated client-side from completed work orders' TotalCost
/// (USD assumed per Money model). Replace with a dedicated
/// IVendorPaymentsLog once one ships.
/// </summary>
public static class VendorsEndpoint
{
    public static RouteGroupBuilder MapVendors(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.MapGet("/vendors",      HandleListVendorsAsync).WithName("CockpitListVendors");
        group.MapGet("/vendors/{id}", HandleGetVendorDetailAsync).WithName("CockpitGetVendorDetail");
        return group;
    }

    internal const decimal Form1099Threshold = 600m;

    /// <summary>
    /// Pure 1099-readiness rule (per W#29 hand-off):
    ///   needsForm1099 = OnboardingState == Active
    ///                   AND vendor.W9 == null
    ///                   AND ytdPayments &gt; $600
    ///
    /// Extracted as a static so the rule can be unit-tested without standing up
    /// the full IMaintenanceService aggregation pipeline (TotalCost can only be
    /// set on a WorkOrder via the payments gateway which has no test seam).
    /// </summary>
    internal static bool NeedsForm1099(Vendor vendor, decimal ytdPayments)
        => vendor.OnboardingState == VendorOnboardingState.Active
           && vendor.W9 is null
           && ytdPayments > Form1099Threshold;

    internal static async Task<Ok<VendorListDto>> HandleListVendorsAsync(
        ITenantContext tenantContext,
        IMaintenanceService maintenance,
        CancellationToken ct)
    {
        _ = tenantContext;

        var vendors = new List<Vendor>();
        await foreach (var v in maintenance.ListVendorsAsync(ListVendorsQuery.Empty, ct).ConfigureAwait(false))
            vendors.Add(v);

        // Sum YTD payments across all completed work orders, grouped by vendor.
        var year = DateTime.UtcNow.Year;
        var ytdByVendor = new Dictionary<string, decimal>(StringComparer.Ordinal);
        await foreach (var wo in maintenance.ListWorkOrdersAsync(ListWorkOrdersQuery.Empty, ct).ConfigureAwait(false))
        {
            if (wo.CompletedDate?.Year != year) continue;
            if (wo.TotalCost is null) continue;
            var key = wo.AssignedVendorId.Value;
            var amount = wo.TotalCost.Value.Amount;
            ytdByVendor[key] = ytdByVendor.TryGetValue(key, out var prev)
                ? prev + amount
                : amount;
        }

        var items = new List<VendorSummaryDto>(vendors.Count);
        foreach (var v in vendors)
        {
            var w9Status = ResolveW9Status(v);
            var ytd      = ytdByVendor.TryGetValue(v.Id.Value, out var amt) ? amt : 0m;
            var needs1099 = NeedsForm1099(v, ytd);

            items.Add(new VendorSummaryDto(
                VendorId:        v.Id.Value,
                DisplayName:     v.DisplayName,
                Specialties:     v.Specialties.Select(s => s.ToString() ?? "").ToArray(),
                OnboardingState: v.OnboardingState.ToString(),
                W9Status:        w9Status,
                YtdPayments:     ytd,
                NeedsForm1099:   needs1099));
        }

        return TypedResults.Ok(new VendorListDto(items));
    }

    internal static async Task<Results<Ok<VendorDetailDto>, NotFound>> HandleGetVendorDetailAsync(
        string id,
        ITenantContext tenantContext,
        IMaintenanceService maintenance,
        IVendorContactService contacts,
        IVendorPerformanceLog performance,
        CancellationToken ct)
    {
        _ = tenantContext;

        var vendor = await maintenance.GetVendorAsync(new VendorId(id), ct).ConfigureAwait(false);
        if (vendor is null) return TypedResults.NotFound();

        var w9Status = ResolveW9Status(vendor);

        // Performance log — last 5 events. ListByVendorAsync returns oldest first;
        // take from the tail by enumerating fully (small per-vendor volume).
        var allPerf = new List<VendorPerformanceRecord>();
        await foreach (var p in performance.ListByVendorAsync(vendor.Id, skip: null, take: null, ct).ConfigureAwait(false))
            allPerf.Add(p);
        var perfTail = allPerf.TakeLast(5)
            .Select(p => new VendorPerformanceEntry(
                Event:      p.Event.ToString(),
                OccurredAt: p.OccurredAt,
                Notes:      p.Notes))
            .ToArray();

        // Work-order history — only this vendor's WOs.
        var wos = new List<VendorWorkOrderEntry>();
        await foreach (var wo in maintenance.ListWorkOrdersAsync(
            ListWorkOrdersQuery.Empty with { VendorId = vendor.Id }, ct).ConfigureAwait(false))
        {
            wos.Add(new VendorWorkOrderEntry(
                WorkOrderId:   wo.Id.Value,
                Status:        wo.Status.ToString(),
                ScheduledDate: wo.ScheduledDate,
                CompletedDate: wo.CompletedDate,
                TotalCost:     wo.TotalCost is null ? null : wo.TotalCost.Value.Amount));
        }
        wos.Sort((a, b) => b.ScheduledDate.CompareTo(a.ScheduledDate));

        // Contacts — IVendorContactService doesn't expose a "list-by-vendor"
        // surface today; the Vendor model carries a Contacts ID list but the
        // service has no batch resolver. Skip the per-contact resolution and
        // surface the contact IDs only until a batch accessor lands.
        var contactIds = vendor.Contacts.Select(c => c.Value.ToString()).ToArray();
        _ = contacts; // suppress warning until the resolver exists

        var dto = new VendorDetailDto(
            VendorId:        vendor.Id.Value,
            DisplayName:     vendor.DisplayName,
            Status:          vendor.Status.ToString(),
            OnboardingState: vendor.OnboardingState.ToString(),
            ContactName:     vendor.ContactName,
            ContactEmail:    vendor.ContactEmail,
            ContactPhone:    vendor.ContactPhone,
            Specialties:     vendor.Specialties.Select(s => s.ToString() ?? "").ToArray(),
            ContactIds:      contactIds,
            W9Status:        w9Status,
            PerformanceLog:  perfTail,
            WorkOrders:      wos);

        return TypedResults.Ok(dto);
    }

    private static string ResolveW9Status(Vendor vendor)
        => vendor.W9 is null ? "Awaiting" : "On file";
}

// ── Wire formats ────────────────────────────────────────────────────────

public record VendorListDto(IReadOnlyList<VendorSummaryDto> Vendors);

public record VendorSummaryDto(
    string VendorId,
    string DisplayName,
    IReadOnlyList<string> Specialties,
    string OnboardingState,
    string W9Status,
    decimal YtdPayments,
    bool NeedsForm1099);

public record VendorDetailDto(
    string VendorId,
    string DisplayName,
    string Status,
    string OnboardingState,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    IReadOnlyList<string> Specialties,
    IReadOnlyList<string> ContactIds,
    string W9Status,
    IReadOnlyList<VendorPerformanceEntry> PerformanceLog,
    IReadOnlyList<VendorWorkOrderEntry> WorkOrders);

public record VendorPerformanceEntry(string Event, DateTimeOffset OccurredAt, string? Notes);
public record VendorWorkOrderEntry(string WorkOrderId, string Status, DateOnly ScheduledDate, DateOnly? CompletedDate, decimal? TotalCost);

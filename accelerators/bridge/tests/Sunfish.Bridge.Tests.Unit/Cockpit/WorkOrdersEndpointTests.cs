using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.HttpResults;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Bridge.Cockpit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Cockpit;

/// <summary>
/// W#29 Phase 3 — work-orders cockpit endpoint tests.
/// Per XO ruling on 2026-05-16: property filter intentionally omitted
/// (WorkOrder model has no PropertyId; W#62/W#62.1 is the fix). Tests
/// cover the surfaced filters (status, vendor, pagination) + detail
/// happy path + 404.
/// </summary>
public sealed class WorkOrdersEndpointTests
{
    private static readonly TenantId TestTenant = new("tenant-wo-test");

    // ── List ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListWorkOrders_ReturnsPagedSummary()
    {
        var (svc, _) = await SeededServiceAsync(workOrderCount: 3);
        var ctx = new TestTenantContext(TestTenant.Value);

        var result = await WorkOrdersEndpoint.HandleListWorkOrdersAsync(
            tenantContext: ctx,
            maintenance:   svc,
            status:        null,
            vendorId:      null,
            from:          null,
            to:            null,
            page:          1,
            pageSize:      10,
            ct:            CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value!.Total);
        Assert.Equal(3, result.Value.Items.Count);
        Assert.Equal(1, result.Value.Page);
    }

    [Fact]
    public async Task ListWorkOrders_FiltersByStatus()
    {
        var (svc, vendor) = await SeededServiceAsync(workOrderCount: 2);

        // Transition one to Sent so the two records differ in status.
        var first = (await svc.ListWorkOrdersAsync(ListWorkOrdersQuery.Empty, CancellationToken.None).FirstAsync()).Id;
        await svc.TransitionWorkOrderAsync(first, WorkOrderStatus.Sent, CancellationToken.None);

        var ctx = new TestTenantContext(TestTenant.Value);
        var sentResult = await WorkOrdersEndpoint.HandleListWorkOrdersAsync(
            ctx, svc, status: "Sent", vendorId: null, from: null, to: null, page: 1, pageSize: 10, CancellationToken.None);

        Assert.Equal(1, sentResult.Value!.Total);
        Assert.Equal("Sent", sentResult.Value.Items[0].Status);
        _ = vendor;
    }

    [Fact]
    public async Task ListWorkOrders_RespectsPagination()
    {
        var (svc, _) = await SeededServiceAsync(workOrderCount: 5);
        var ctx = new TestTenantContext(TestTenant.Value);

        var p1 = await WorkOrdersEndpoint.HandleListWorkOrdersAsync(
            ctx, svc, null, null, null, null, page: 1, pageSize: 2, CancellationToken.None);
        var p2 = await WorkOrdersEndpoint.HandleListWorkOrdersAsync(
            ctx, svc, null, null, null, null, page: 2, pageSize: 2, CancellationToken.None);
        var p3 = await WorkOrdersEndpoint.HandleListWorkOrdersAsync(
            ctx, svc, null, null, null, null, page: 3, pageSize: 2, CancellationToken.None);

        Assert.Equal(5, p1.Value!.Total);
        Assert.Equal(2, p1.Value.Items.Count);
        Assert.Equal(2, p2.Value!.Items.Count);
        Assert.Single(p3.Value!.Items);
    }

    // ── Detail ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWorkOrderDetail_ReturnsDto_WithVendorName()
    {
        var (svc, vendor) = await SeededServiceAsync(workOrderCount: 1);
        var wo = await svc.ListWorkOrdersAsync(ListWorkOrdersQuery.Empty, CancellationToken.None).FirstAsync();
        var ctx = new TestTenantContext(TestTenant.Value);

        var result = await WorkOrdersEndpoint.HandleGetWorkOrderDetailAsync(
            id:            wo.Id.Value,
            tenantContext: ctx,
            maintenance:   svc,
            ct:            CancellationToken.None);

        var ok = Assert.IsType<Ok<WorkOrderDetailDto>>(result.Result);
        Assert.Equal(wo.Id.Value,           ok.Value!.WorkOrderId);
        Assert.Equal(vendor.DisplayName,    ok.Value.VendorDisplayName);
        Assert.Equal(wo.AssignedVendorId.Value, ok.Value.VendorId);
    }

    [Fact]
    public async Task GetWorkOrderDetail_Returns404_WhenUnknown()
    {
        var (svc, _) = await SeededServiceAsync(workOrderCount: 0);
        var ctx = new TestTenantContext(TestTenant.Value);

        var result = await WorkOrdersEndpoint.HandleGetWorkOrderDetailAsync(
            id: "DOES-NOT-EXIST",
            tenantContext: ctx,
            maintenance: svc,
            ct: CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    // ── Fixtures ────────────────────────────────────────────────────────────

    private static async Task<(InMemoryMaintenanceService svc, Vendor vendor)> SeededServiceAsync(int workOrderCount)
    {
        var svc = new InMemoryMaintenanceService();

        var vendor = await svc.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "Speedy Plumbing",
            Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing),
        });

        for (int i = 0; i < workOrderCount; i++)
        {
            var request = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
            {
                PropertyId             = new EntityId("urn", "sunfish.test", $"PROP-{i + 1:000}"),
                RequestedByDisplayName = "Alice Tenant",
                Description            = $"Work item {i + 1}",
                Priority               = MaintenancePriority.Normal,
                RequestedDate          = new DateOnly(2026, 5, 1).AddDays(i),
            });
            await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
            {
                Tenant           = TestTenant,
                RequestId        = request.Id,
                AssignedVendorId = vendor.Id,
                ScheduledDate    = new DateOnly(2026, 5, 15).AddDays(i),
            });
        }

        return (svc, vendor);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(string tenantId) => TenantId = tenantId;
        public string TenantId { get; }
        public string UserId => "test-user";
        public IReadOnlyList<string> Roles => new[] { CockpitPermissions.Roles.Owner };
        public bool HasPermission(string permission) => true;
    }
}

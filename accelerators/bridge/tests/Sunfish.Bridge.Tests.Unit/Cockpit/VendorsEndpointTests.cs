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
/// W#29 Phase 4 — vendors cockpit endpoint tests.
///
/// 1099 readiness rule from the hand-off (encoded in
/// <see cref="VendorsEndpoint.NeedsForm1099"/>):
///   needsForm1099 = OnboardingState == Active
///                   AND vendor.W9 == null
///                   AND ytdPayments &gt; $600
/// </summary>
public sealed class VendorsEndpointTests
{
    private static readonly TenantId TestTenant = new("tenant-vendor-test");

    // ── 1099 rule (pure logic) ──────────────────────────────────────────────

    [Theory]
    [InlineData(true,  null,                                      650, true,  "active vendor with no W9 + $650 YTD → needs 1099")]
    [InlineData(true,  null,                                      400, false, "active vendor with no W9 + under-threshold → no 1099")]
    [InlineData(true,  null,                                      600, false, "exactly $600 is not 'more than' the threshold")]
    [InlineData(true,  "11111111-1111-1111-1111-111111111111",    900, false, "active vendor with W9 on file does not need 1099")]
    [InlineData(false, null,                                      900, false, "non-Active onboarding state does not need 1099 (per ruling)")]
    public void NeedsForm1099_AppliesHandoffRule(bool isActive, string? w9Guid, decimal ytd, bool expected, string scenario)
    {
        var vendor = new Vendor
        {
            Id              = new VendorId("V-1"),
            DisplayName     = "Test Vendor",
            Status          = VendorStatus.Active,
            OnboardingState = isActive ? VendorOnboardingState.Active : VendorOnboardingState.Pending,
            W9              = w9Guid is null ? null : new W9DocumentId(System.Guid.Parse(w9Guid)),
        };

        var actual = VendorsEndpoint.NeedsForm1099(vendor, ytd);
        Assert.Equal(expected, actual);
        _ = scenario;
    }

    // ── Endpoint wiring ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListVendors_ReturnsAwaitingW9_AndZeroYtd_WhenVendorFresh()
    {
        var svc = new InMemoryMaintenanceService();
        var vendor = await svc.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "Reliable HVAC",
            Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing),
        });

        var ctx = new TestTenantContext(TestTenant.Value);
        var result = await VendorsEndpoint.HandleListVendorsAsync(ctx, svc, CancellationToken.None);

        Assert.NotNull(result.Value);
        var row = Assert.Single(result.Value!.Vendors);
        Assert.Equal(vendor.Id.Value, row.VendorId);
        Assert.Equal("Awaiting", row.W9Status);
        Assert.Equal(0m, row.YtdPayments);
        Assert.False(row.NeedsForm1099, "no completed work orders → YTD=0 → no 1099 even without W9");
    }

    [Fact]
    public async Task ListVendors_ReturnsEmpty_WhenNoVendors()
    {
        var svc = new InMemoryMaintenanceService();
        var ctx = new TestTenantContext(TestTenant.Value);

        var result = await VendorsEndpoint.HandleListVendorsAsync(ctx, svc, CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Empty(result.Value!.Vendors);
    }

    // ── Detail ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetVendorDetail_Returns404_WhenUnknown()
    {
        var svc = new InMemoryMaintenanceService();
        var ctx = new TestTenantContext(TestTenant.Value);

        var result = await VendorsEndpoint.HandleGetVendorDetailAsync(
            id: "DOES-NOT-EXIST",
            tenantContext: ctx,
            maintenance: svc,
            contacts: new InMemoryVendorContactService(),
            performance: new InMemoryVendorPerformanceLog(),
            ct: CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    [Fact]
    public async Task GetVendorDetail_ReturnsVendor_With_WorkOrderHistory()
    {
        var svc = new InMemoryMaintenanceService();
        var vendor = await svc.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "Reliable HVAC",
            Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing),
        });
        var req = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId             = new EntityId("urn", "sunfish.test", "PROP-1"),
            RequestedByDisplayName = "Owner",
            Description            = "Furnace tune-up",
            Priority               = MaintenancePriority.Normal,
            RequestedDate          = new DateOnly(2026, 5, 1),
        });
        var wo = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        {
            Tenant           = TestTenant,
            RequestId        = req.Id,
            AssignedVendorId = vendor.Id,
            ScheduledDate    = new DateOnly(2026, 5, 10),
        });

        var ctx = new TestTenantContext(TestTenant.Value);
        var result = await VendorsEndpoint.HandleGetVendorDetailAsync(
            id: vendor.Id.Value,
            tenantContext: ctx,
            maintenance: svc,
            contacts: new InMemoryVendorContactService(),
            performance: new InMemoryVendorPerformanceLog(),
            ct: CancellationToken.None);

        var ok = Assert.IsType<Ok<VendorDetailDto>>(result.Result);
        Assert.Equal("Reliable HVAC", ok.Value!.DisplayName);
        Assert.Equal("Awaiting", ok.Value.W9Status);
        Assert.Single(ok.Value.WorkOrders);
        Assert.Equal(wo.Id.Value, ok.Value.WorkOrders[0].WorkOrderId);
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

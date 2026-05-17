using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.HttpResults;
using Sunfish.Blocks.Inspections.Models;
using Sunfish.Blocks.Inspections.Services;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Bridge.Cockpit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;
using Xunit;
using PartyId = Sunfish.Blocks.People.Foundation.Models.PartyId;

namespace Sunfish.Bridge.Tests.Unit.Cockpit;

/// <summary>
/// W#29 Phase 5 — DashboardEndpoint handler tests.
/// Per hand-off, this DTO is the canonical dashboard widget set:
/// vacancy rate, 30/60/90-day renewal buckets, work-order status
/// summary, and overdue-inspection list (no inspection in 12 months).
/// </summary>
public sealed class DashboardEndpointTests
{
    private static readonly TenantId TestTenant = new("tenant-dash-test");

    [Fact]
    public async Task GetDashboard_Returns404_WhenPropertyNotFound()
    {
        var svcs = NewServices();
        var ctx = new TestTenantContext(TestTenant.Value);

        var result = await DashboardEndpoint.HandleGetDashboardAsync(
            propertyId: "DOES-NOT-EXIST",
            tenantContext: ctx,
            properties: svcs.Properties, unitRepo: svcs.Units,
            leases: svcs.Leases, inspections: svcs.Inspections, maintenance: svcs.Maintenance,
            ct: CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    [Fact]
    public async Task GetDashboard_ReportsVacancy_AndZeros_WhenPropertyHasNoActivity()
    {
        var propId = new PropertyId("PROP-Empty");
        var svcs = NewServices();
        await svcs.Properties.UpsertAsync(NewProperty(propId, PropertyKind.MultiUnit));

        var unitA = NewUnit(propId, "1", UnitStatus.Available);
        var unitB = NewUnit(propId, "2", UnitStatus.Occupied);
        var unitC = NewUnit(propId, "3", UnitStatus.Available);
        await svcs.Units.UpsertAsync(unitA);
        await svcs.Units.UpsertAsync(unitB);
        await svcs.Units.UpsertAsync(unitC);

        var dto = await CallOk(propId.Value, svcs);

        Assert.Equal(3, dto.TotalUnits);
        Assert.Equal(2, dto.VacantUnits);
        Assert.Equal(3, dto.UpcomingRenewals.Count);
        Assert.All(dto.UpcomingRenewals, b => Assert.Equal(0, b.Count));
        Assert.Equal(0, dto.WorkOrders.Open);
        Assert.Equal(0, dto.WorkOrders.InProgress);
        Assert.Equal(0, dto.WorkOrders.Blocked);
        // All 3 units have never been inspected → all overdue.
        Assert.Equal(3, dto.OverdueInspectionUnitIds.Count);
    }

    [Fact]
    public async Task GetDashboard_BucketsLeaseRenewals_InTo30_60_90()
    {
        var propId = new PropertyId("PROP-Renewals");
        var svcs = NewServices();
        await svcs.Properties.UpsertAsync(NewProperty(propId, PropertyKind.MultiUnit));

        var unit30 = NewUnit(propId, "30", UnitStatus.Occupied);
        var unit60 = NewUnit(propId, "60", UnitStatus.Occupied);
        var unit90 = NewUnit(propId, "90", UnitStatus.Occupied);
        var unitFar = NewUnit(propId, "Far", UnitStatus.Occupied);
        await svcs.Units.UpsertAsync(unit30);
        await svcs.Units.UpsertAsync(unit60);
        await svcs.Units.UpsertAsync(unit90);
        await svcs.Units.UpsertAsync(unitFar);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        await SeedActiveLeaseAsync(svcs.Leases, unit30.Id, today.AddDays(10));   // → 30-bucket
        await SeedActiveLeaseAsync(svcs.Leases, unit60.Id, today.AddDays(45));   // → 60-bucket
        await SeedActiveLeaseAsync(svcs.Leases, unit90.Id, today.AddDays(80));   // → 90-bucket
        await SeedActiveLeaseAsync(svcs.Leases, unitFar.Id, today.AddDays(200)); // → no bucket

        var dto = await CallOk(propId.Value, svcs);

        Assert.Equal(30, dto.UpcomingRenewals[0].WithinDays);
        Assert.Equal(1, dto.UpcomingRenewals[0].Count);
        Assert.Equal(60, dto.UpcomingRenewals[1].WithinDays);
        Assert.Equal(1, dto.UpcomingRenewals[1].Count);
        Assert.Equal(90, dto.UpcomingRenewals[2].WithinDays);
        Assert.Equal(1, dto.UpcomingRenewals[2].Count);
    }

    [Fact]
    public async Task GetDashboard_GroupsWorkOrdersBy_Open_InProgress_Blocked()
    {
        var propId = new PropertyId("PROP-WO");
        var svcs = NewServices();
        await svcs.Properties.UpsertAsync(NewProperty(propId, PropertyKind.SingleFamily));

        var vendor = await svcs.Maintenance.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "V",
            Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing),
        });
        await SeedWorkOrderAsync(svcs.Maintenance, vendor.Id, propId, status: WorkOrderStatus.Draft);
        await SeedWorkOrderAsync(svcs.Maintenance, vendor.Id, propId, status: WorkOrderStatus.Sent);
        await SeedWorkOrderAsync(svcs.Maintenance, vendor.Id, propId, status: WorkOrderStatus.InProgress);
        await SeedWorkOrderAsync(svcs.Maintenance, vendor.Id, propId, status: WorkOrderStatus.OnHold);
        await SeedWorkOrderAsync(svcs.Maintenance, vendor.Id, propId, status: WorkOrderStatus.Cancelled); // not counted

        var dto = await CallOk(propId.Value, svcs);

        Assert.Equal(2, dto.WorkOrders.Open);        // Draft + Sent
        Assert.Equal(1, dto.WorkOrders.InProgress);  // InProgress
        Assert.Equal(1, dto.WorkOrders.Blocked);     // OnHold
    }

    [Fact]
    public async Task GetDashboard_OverdueInspections_FlagsUnitsWith_NoInspection_OrLastOver12Months()
    {
        var propId = new PropertyId("PROP-Insp");
        var svcs = NewServices();
        await svcs.Properties.UpsertAsync(NewProperty(propId, PropertyKind.MultiUnit));

        var unitNeverInspected = NewUnit(propId, "Never", UnitStatus.Available);
        var unitRecent = NewUnit(propId, "Recent", UnitStatus.Available);
        var unitStale = NewUnit(propId, "Stale", UnitStatus.Available);
        await svcs.Units.UpsertAsync(unitNeverInspected);
        await svcs.Units.UpsertAsync(unitRecent);
        await svcs.Units.UpsertAsync(unitStale);

        var template = await svcs.Inspections.CreateTemplateAsync(new CreateTemplateRequest
        {
            Name = "T",
            Items = Array.Empty<InspectionChecklistItem>(),
        });
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        await svcs.Inspections.ScheduleAsync(new ScheduleInspectionRequest
        {
            TemplateId = template.Id, UnitId = unitRecent.Id, InspectorName = "I",
            ScheduledDate = today.AddDays(-30),  // fresh
        });
        await svcs.Inspections.ScheduleAsync(new ScheduleInspectionRequest
        {
            TemplateId = template.Id, UnitId = unitStale.Id, InspectorName = "I",
            ScheduledDate = today.AddDays(-400), // overdue
        });

        var dto = await CallOk(propId.Value, svcs);

        Assert.Equal(2, dto.OverdueInspectionUnitIds.Count);
        Assert.Contains(unitNeverInspected.Id.ToString(), dto.OverdueInspectionUnitIds);
        Assert.Contains(unitStale.Id.ToString(), dto.OverdueInspectionUnitIds);
        Assert.DoesNotContain(unitRecent.Id.ToString(), dto.OverdueInspectionUnitIds);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private sealed class TestServices
    {
        public InMemoryPropertyRepository Properties { get; } = new();
        public InMemoryPropertyUnitRepository Units { get; } = new();
        public InMemoryLeaseService Leases { get; } = new();
        public InMemoryInspectionsService Inspections { get; } = new();
        public InMemoryMaintenanceService Maintenance { get; } = new();
    }

    private static TestServices NewServices() => new();

    private static async Task<DashboardDto> CallOk(string propertyId, TestServices svcs)
    {
        var ctx = new TestTenantContext(TestTenant.Value);
        var result = await DashboardEndpoint.HandleGetDashboardAsync(
            propertyId: propertyId, tenantContext: ctx,
            properties: svcs.Properties, unitRepo: svcs.Units,
            leases: svcs.Leases, inspections: svcs.Inspections, maintenance: svcs.Maintenance,
            ct: CancellationToken.None);
        var ok = Assert.IsType<Ok<DashboardDto>>(result.Result);
        Assert.NotNull(ok.Value);
        return ok.Value!;
    }

    private static Property NewProperty(PropertyId id, PropertyKind kind) =>
        new()
        {
            Id          = id,
            TenantId    = TestTenant,
            DisplayName = id.Value,
            Address     = new PostalAddress
            {
                Line1       = "addr",
                City        = "city",
                Region      = "UT",
                PostalCode  = "00000",
                CountryCode = "US",
            },
            Kind      = kind,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

    private static PropertyUnit NewUnit(PropertyId property, string number, UnitStatus status) =>
        new()
        {
            Id          = PropertyUnit.NewId(TestTenant),
            TenantId    = TestTenant,
            PropertyId  = property,
            UnitNumber  = number,
            Status      = status,
            CreatedAt   = DateTimeOffset.UnixEpoch,
        };

    private static async Task SeedActiveLeaseAsync(InMemoryLeaseService leases, EntityId unitId, DateOnly endDate)
    {
        var lease = await leases.CreateAsync(new CreateLeaseRequest
        {
            UnitId      = unitId,
            Tenants     = [new PartyId("party-tenant")],
            Landlord    = new PartyId("party-landlord"),
            StartDate   = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            EndDate     = endDate,
            MonthlyRent = 1000m,
        });
        await leases.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, ActorId.System);
        await leases.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, ActorId.System);
        await leases.TransitionPhaseAsync(lease.Id, LeasePhase.Active, ActorId.System);
    }

    private static async Task SeedWorkOrderAsync(
        InMemoryMaintenanceService svc, VendorId vendorId, PropertyId propertyId, WorkOrderStatus status)
    {
        var req = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId             = new EntityId("urn", "sunfish.test", propertyId.Value),
            RequestedByDisplayName = "Owner",
            Description            = "x",
            Priority               = MaintenancePriority.Normal,
            RequestedDate          = new DateOnly(2026, 5, 1),
        });
        var wo = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        {
            Tenant           = TestTenant,
            RequestId        = req.Id,
            AssignedVendorId = vendorId,
            ScheduledDate    = new DateOnly(2026, 5, 10),
            PropertyId       = propertyId,
        });
        // Walk through Draft → target status via the allowed linear path.
        var transitions = status switch
        {
            WorkOrderStatus.Draft      => Array.Empty<WorkOrderStatus>(),
            WorkOrderStatus.Sent       => new[] { WorkOrderStatus.Sent },
            WorkOrderStatus.Accepted   => new[] { WorkOrderStatus.Sent, WorkOrderStatus.Accepted },
            WorkOrderStatus.Scheduled  => new[] { WorkOrderStatus.Sent, WorkOrderStatus.Accepted, WorkOrderStatus.Scheduled },
            WorkOrderStatus.InProgress => new[] { WorkOrderStatus.Sent, WorkOrderStatus.Accepted, WorkOrderStatus.Scheduled, WorkOrderStatus.InProgress },
            WorkOrderStatus.OnHold     => new[] { WorkOrderStatus.Sent, WorkOrderStatus.Accepted, WorkOrderStatus.Scheduled, WorkOrderStatus.InProgress, WorkOrderStatus.OnHold },
            WorkOrderStatus.Cancelled  => new[] { WorkOrderStatus.Sent, WorkOrderStatus.Cancelled },
            _ => throw new System.NotSupportedException($"Test helper does not support transitioning to {status}"),
        };
        foreach (var step in transitions)
            await svc.TransitionWorkOrderAsync(wo.Id, step, CancellationToken.None);
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

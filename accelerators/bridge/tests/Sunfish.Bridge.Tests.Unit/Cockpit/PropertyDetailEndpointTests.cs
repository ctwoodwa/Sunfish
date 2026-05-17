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
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Bridge.Cockpit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;
using Xunit;
using PartyId = Sunfish.Blocks.People.Foundation.Models.PartyId;

namespace Sunfish.Bridge.Tests.Unit.Cockpit;

/// <summary>
/// W#29 Phase 2 + W#29 Phase 1.5 / W#62 Phase 2 — PropertyDetailEndpoint
/// handler tests.
///
/// The Phase 2 tests pin the property card + equipment list path and the
/// OpenWorkOrderCount stub (still 0 until W#62 PR 3). The Phase 1.5 tests
/// pin the new lease + inspection aggregation via PropertyUnit join.
/// </summary>
public sealed class PropertyDetailEndpointTests
{
    private static readonly TenantId TestTenant = new("tenant-detail-test");

    // ── Property card + equipment (Phase 2) ────────────────────────────────

    [Fact]
    public async Task GetPropertyDetail_ReturnsPropertyCard_And_Equipment()
    {
        var propId = new PropertyId("PROP-100");
        var (propRepo, unitRepo, eqRepo, leases, inspections, maintenance) = NewServices();
        await propRepo.UpsertAsync(NewProperty(TestTenant, propId, "100 Mainline Ave", "Lehi", "UT", PropertyKind.MultiUnit));
        await eqRepo.UpsertAsync(NewEquipment(TestTenant, propId, "EQ-1", "Water Heater", EquipmentClass.WaterHeater, "Rheem", "XR50"));
        await eqRepo.UpsertAsync(NewEquipment(TestTenant, propId, "EQ-2", "HVAC", EquipmentClass.HVAC, null, null));

        var dto = await CallHandlerOk(propId.Value, propRepo, unitRepo, eqRepo, leases, inspections, maintenance);

        Assert.Equal("PROP-100", dto.PropertyId);
        Assert.Equal("MultiUnit", dto.Kind);
        Assert.Contains("Lehi", dto.DisplayAddress);
        Assert.Contains("UT", dto.DisplayAddress);
        Assert.Equal(2, dto.Equipment.Count);

        // No units / leases / inspections seeded → Phase 1.5 aggregation fields stay null.
        // OpenWorkOrderCount stays 0 (W#62 PR 3 is the upgrade for that one).
        Assert.Null(dto.ActiveLease);
        Assert.Equal(0, dto.OpenWorkOrderCount);
        Assert.Null(dto.LastInspectionDate);
        Assert.Null(dto.LastInspectionResult);
    }

    [Fact]
    public async Task GetPropertyDetail_Returns404_WhenPropertyNotFoundInTenant()
    {
        var (propRepo, unitRepo, eqRepo, leases, inspections, maintenance) = NewServices();
        var ctx = new TestTenantContext(TestTenant.Value);

        var result = await PropertyDetailEndpoint.HandleGetPropertyDetailAsync(
            propertyId: "DOES-NOT-EXIST",
            tenantContext: ctx,
            properties: propRepo,
            units: unitRepo,
            equipment: eqRepo,
            leases: leases,
            inspections: inspections,
            maintenance: maintenance,
            ct: CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    [Fact]
    public async Task GetPropertyDetail_DoesNotLeakAnotherTenants_Property()
    {
        var mine = new TenantId("tenant-mine");
        var other = new TenantId("tenant-other");
        var propId = new PropertyId("PROP-100");

        var (propRepo, unitRepo, eqRepo, leases, inspections, maintenance) = NewServices();
        await propRepo.UpsertAsync(NewProperty(other, propId, "100 Their Way", "Provo", "UT", PropertyKind.SingleFamily));
        var ctx = new TestTenantContext(mine.Value);

        var result = await PropertyDetailEndpoint.HandleGetPropertyDetailAsync(
            propertyId: propId.Value,
            tenantContext: ctx,
            properties: propRepo,
            units: unitRepo,
            equipment: eqRepo,
            leases: leases,
            inspections: inspections,
            maintenance: maintenance,
            ct: CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    // ── Lease + Inspection aggregation (Phase 1.5 / W#62 Phase 2) ──────────

    [Fact]
    public async Task GetPropertyDetail_PopulatesActiveLease_FromUnitJoin()
    {
        var propId = new PropertyId("PROP-200");
        var (propRepo, unitRepo, eqRepo, leases, inspections, maintenance) = NewServices();
        await propRepo.UpsertAsync(NewProperty(TestTenant, propId, "200 Lease Ln", "Lehi", "UT", PropertyKind.SingleFamily));

        // Seed a unit for this property.
        var unitId = PropertyUnit.NewId(TestTenant);
        await unitRepo.UpsertAsync(new PropertyUnit
        {
            Id          = unitId,
            TenantId    = TestTenant,
            PropertyId  = propId,
            UnitNumber  = "1",
            Status      = UnitStatus.Occupied,
            CreatedAt   = DateTimeOffset.UnixEpoch,
        });

        // Seed an Active lease on that unit.
        var lease = await leases.CreateAsync(new CreateLeaseRequest
        {
            UnitId      = unitId,
            Tenants     = [new PartyId("party-tenant-001")],
            Landlord    = new PartyId("party-landlord"),
            StartDate   = new DateOnly(2026, 1, 1),
            EndDate     = new DateOnly(2026, 12, 31),
            MonthlyRent = 1500m,
        });
        // Walk to Active via the allowed transitions.
        await leases.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, ActorId.System);
        await leases.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, ActorId.System);
        await leases.TransitionPhaseAsync(lease.Id, LeasePhase.Active, ActorId.System);

        var dto = await CallHandlerOk(propId.Value, propRepo, unitRepo, eqRepo, leases, inspections, maintenance);

        Assert.NotNull(dto.ActiveLease);
        Assert.Equal(lease.Id.Value, dto.ActiveLease!.LeaseId);
        Assert.Equal(1500m, dto.ActiveLease.MonthlyRent);
        Assert.Equal(new DateOnly(2026, 12, 31), dto.ActiveLease.EndDate);
    }

    [Fact]
    public async Task GetPropertyDetail_PopulatesLastInspectionDate_FromUnitJoin()
    {
        var propId = new PropertyId("PROP-300");
        var (propRepo, unitRepo, eqRepo, leases, inspections, maintenance) = NewServices();
        await propRepo.UpsertAsync(NewProperty(TestTenant, propId, "300 Insp Way", "Lehi", "UT", PropertyKind.SingleFamily));

        var unitId = PropertyUnit.NewId(TestTenant);
        await unitRepo.UpsertAsync(new PropertyUnit
        {
            Id          = unitId,
            TenantId    = TestTenant,
            PropertyId  = propId,
            UnitNumber  = "1",
            Status      = UnitStatus.Available,
            CreatedAt   = DateTimeOffset.UnixEpoch,
        });

        // Seed an inspection template + two inspections, only the later one
        // should drive LastInspectionDate.
        var template = await inspections.CreateTemplateAsync(new CreateTemplateRequest
        {
            Name  = "Annual Walkthrough",
            Items = Array.Empty<InspectionChecklistItem>(),
        });
        await inspections.ScheduleAsync(new ScheduleInspectionRequest
        {
            TemplateId    = template.Id,
            UnitId        = unitId,
            InspectorName = "Inspector A",
            ScheduledDate = new DateOnly(2026, 3, 15),
        });
        await inspections.ScheduleAsync(new ScheduleInspectionRequest
        {
            TemplateId    = template.Id,
            UnitId        = unitId,
            InspectorName = "Inspector B",
            ScheduledDate = new DateOnly(2026, 5, 1),
        });

        var dto = await CallHandlerOk(propId.Value, propRepo, unitRepo, eqRepo, leases, inspections, maintenance);

        Assert.Equal(new DateOnly(2026, 5, 1), dto.LastInspectionDate);
        Assert.NotNull(dto.LastInspectionResult);
    }

    [Fact]
    public async Task GetPropertyDetail_IgnoresLeasesOnUnitsOutsideTheProperty()
    {
        var propA = new PropertyId("PROP-A");
        var propB = new PropertyId("PROP-B");
        var (propRepo, unitRepo, eqRepo, leases, inspections, maintenance) = NewServices();
        await propRepo.UpsertAsync(NewProperty(TestTenant, propA, "Prop A", "Lehi", "UT", PropertyKind.SingleFamily));
        await propRepo.UpsertAsync(NewProperty(TestTenant, propB, "Prop B", "Lehi", "UT", PropertyKind.SingleFamily));

        var unitA = PropertyUnit.NewId(TestTenant);
        var unitB = PropertyUnit.NewId(TestTenant);
        await unitRepo.UpsertAsync(new PropertyUnit { Id = unitA, TenantId = TestTenant, PropertyId = propA, UnitNumber = "1", Status = UnitStatus.Available, CreatedAt = DateTimeOffset.UnixEpoch });
        await unitRepo.UpsertAsync(new PropertyUnit { Id = unitB, TenantId = TestTenant, PropertyId = propB, UnitNumber = "1", Status = UnitStatus.Available, CreatedAt = DateTimeOffset.UnixEpoch });

        // Lease only on unit-B; querying property-A should NOT see it.
        var leaseB = await leases.CreateAsync(new CreateLeaseRequest
        {
            UnitId      = unitB,
            Tenants     = [new PartyId("party-tenant-b")],
            Landlord    = new PartyId("party-landlord"),
            StartDate   = new DateOnly(2026, 1, 1),
            EndDate     = new DateOnly(2026, 12, 31),
            MonthlyRent = 2000m,
        });
        await leases.TransitionPhaseAsync(leaseB.Id, LeasePhase.AwaitingSignature, ActorId.System);
        await leases.TransitionPhaseAsync(leaseB.Id, LeasePhase.Executed, ActorId.System);
        await leases.TransitionPhaseAsync(leaseB.Id, LeasePhase.Active, ActorId.System);

        var dto = await CallHandlerOk(propA.Value, propRepo, unitRepo, eqRepo, leases, inspections, maintenance);

        Assert.Null(dto.ActiveLease);
    }

    // ── OpenWorkOrderCount (W#62 Phase 3) ──────────────────────────────────

    [Fact]
    public async Task GetPropertyDetail_PopulatesOpenWorkOrderCount_FromPropertyFilter()
    {
        var propId = new PropertyId("PROP-WO");
        var (propRepo, unitRepo, eqRepo, leases, inspections, maintenance) = NewServices();
        await propRepo.UpsertAsync(NewProperty(TestTenant, propId, "WO Way", "Lehi", "UT", PropertyKind.SingleFamily));

        // Two open work orders on this property; one Closed on this property
        // (must NOT count); one open on a different property (must NOT count).
        var vendor = await maintenance.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "Vendor",
            Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing),
        });
        await SeedWorkOrderAsync(maintenance, vendor.Id, propId, WorkOrderStatus.Draft);
        await SeedWorkOrderAsync(maintenance, vendor.Id, propId, WorkOrderStatus.Draft);
        await SeedWorkOrderAsync(maintenance, vendor.Id, propId, terminalStatus: WorkOrderStatus.Cancelled);
        await SeedWorkOrderAsync(maintenance, vendor.Id, new PropertyId("PROP-OTHER"), WorkOrderStatus.Draft);

        var dto = await CallHandlerOk(propId.Value, propRepo, unitRepo, eqRepo, leases, inspections, maintenance);

        Assert.Equal(2, dto.OpenWorkOrderCount);
    }

    private static async Task SeedWorkOrderAsync(
        InMemoryMaintenanceService svc, VendorId vendorId, PropertyId propertyId,
        WorkOrderStatus? terminalStatus = null, WorkOrderStatus? draftStatus = null)
    {
        var req = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId             = new EntityId("urn", "sunfish.test", propertyId.Value),
            RequestedByDisplayName = "Owner",
            Description            = "Test",
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

        if (terminalStatus == WorkOrderStatus.Cancelled)
        {
            // Draft → Sent → Cancelled.
            await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Sent, CancellationToken.None);
            await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Cancelled, CancellationToken.None);
        }
        _ = draftStatus;
    }

    // ── Fixtures ────────────────────────────────────────────────────────────

    private static (
        InMemoryPropertyRepository properties,
        InMemoryPropertyUnitRepository units,
        InMemoryEquipmentRepository equipment,
        InMemoryLeaseService leases,
        InMemoryInspectionsService inspections,
        InMemoryMaintenanceService maintenance) NewServices()
        => (new InMemoryPropertyRepository(),
            new InMemoryPropertyUnitRepository(),
            new InMemoryEquipmentRepository(new InMemoryEquipmentLifecycleEventStore()),
            new InMemoryLeaseService(),
            new InMemoryInspectionsService(),
            new InMemoryMaintenanceService());

    private static async Task<PropertyDetailDto> CallHandlerOk(
        string propertyId,
        IPropertyRepository properties,
        IPropertyUnitRepository units,
        IEquipmentRepository equipment,
        ILeaseService leases,
        IInspectionsService inspections,
        IMaintenanceService maintenance)
    {
        var ctx = new TestTenantContext(TestTenant.Value);
        var result = await PropertyDetailEndpoint.HandleGetPropertyDetailAsync(
            propertyId: propertyId,
            tenantContext: ctx,
            properties: properties,
            units: units,
            equipment: equipment,
            leases: leases,
            inspections: inspections,
            maintenance: maintenance,
            ct: CancellationToken.None);

        var ok = Assert.IsType<Ok<PropertyDetailDto>>(result.Result);
        Assert.NotNull(ok.Value);
        return ok.Value!;
    }

    private static Property NewProperty(TenantId tenant, PropertyId id, string line1, string city, string region, PropertyKind kind) =>
        new()
        {
            Id          = id,
            TenantId    = tenant,
            DisplayName = line1,
            Address     = new PostalAddress
            {
                Line1       = line1,
                City        = city,
                Region      = region,
                PostalCode  = "00000",
                CountryCode = "US",
            },
            Kind      = kind,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

    private static Equipment NewEquipment(TenantId tenant, PropertyId property, string id, string display, EquipmentClass cls, string? make, string? model) =>
        new()
        {
            Id          = new EquipmentId(id),
            TenantId    = tenant,
            Property    = property,
            Class       = cls,
            DisplayName = display,
            Make        = make,
            Model       = model,
            CreatedAt   = DateTimeOffset.UnixEpoch,
        };

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(string tenantId) => TenantId = tenantId;
        public string TenantId { get; }
        public string UserId => "test-user";
        public IReadOnlyList<string> Roles => new[] { CockpitPermissions.Roles.Owner };
        public bool HasPermission(string permission) => true;
    }
}

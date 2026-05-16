using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.HttpResults;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Bridge.Cockpit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Cockpit;

/// <summary>
/// W#29 Phase 2 — PropertyDetailEndpoint handler tests.
/// Per XO ruling on 2026-05-16: lease / WO / inspection fields are
/// stubbed to null/0 pending W#62 (PropertyUnit substrate). Tests assert
/// the property card + equipment list populate and the stub fields stay
/// null/0 so downstream consumers don't accidentally start treating them
/// as live data.
/// </summary>
public sealed class PropertyDetailEndpointTests
{
    [Fact]
    public async Task GetPropertyDetail_ReturnsPropertyCard_And_Equipment()
    {
        var tenant = new TenantId("tenant-detail-test");
        var propId = new PropertyId("PROP-100");

        var propRepo = new InMemoryPropertyRepository();
        await propRepo.UpsertAsync(NewProperty(tenant, propId, "100 Mainline Ave", "Lehi", "UT", PropertyKind.MultiUnit));

        var eqRepo = new InMemoryEquipmentRepository(new InMemoryEquipmentLifecycleEventStore());
        await eqRepo.UpsertAsync(NewEquipment(tenant, propId, "EQ-1", "Water Heater", EquipmentClass.WaterHeater, "Rheem", "XR50"));
        await eqRepo.UpsertAsync(NewEquipment(tenant, propId, "EQ-2", "HVAC", EquipmentClass.HVAC, null, null));

        var ctx = new TestTenantContext(tenant.Value);
        var result = await PropertyDetailEndpoint.HandleGetPropertyDetailAsync(
            propertyId: propId.Value,
            tenantContext: ctx,
            properties: propRepo,
            equipment: eqRepo,
            ct: CancellationToken.None);

        var ok = Assert.IsType<Ok<PropertyDetailDto>>(result.Result);
        Assert.NotNull(ok.Value);
        var dto = ok.Value!;

        Assert.Equal("PROP-100", dto.PropertyId);
        Assert.Equal("MultiUnit", dto.Kind);
        Assert.Contains("Lehi", dto.DisplayAddress);
        Assert.Contains("UT", dto.DisplayAddress);

        Assert.Equal(2, dto.Equipment.Count);
        Assert.Contains(dto.Equipment, e => e.EquipmentId == "EQ-1" && e.Make == "Rheem" && e.DisplayName == "Water Heater");

        // Stubbed aggregation fields — verify they stay null/0 per XO ruling.
        // These flip to populated once W#62 PropertyUnit substrate ships and
        // the Phase 1.5 hand-off upgrades the endpoint. Until then the test
        // pins them as stubs so the downstream React + Anchor consumers don't
        // silently regress on a partial backend rollout.
        Assert.Null(dto.ActiveLease);
        Assert.Equal(0, dto.OpenWorkOrderCount);
        Assert.Null(dto.LastInspectionDate);
        Assert.Null(dto.LastInspectionResult);
    }

    [Fact]
    public async Task GetPropertyDetail_Returns404_WhenPropertyNotFoundInTenant()
    {
        var tenant = new TenantId("tenant-detail-test");
        var propRepo = new InMemoryPropertyRepository();
        var eqRepo = new InMemoryEquipmentRepository(new InMemoryEquipmentLifecycleEventStore());
        var ctx = new TestTenantContext(tenant.Value);

        var result = await PropertyDetailEndpoint.HandleGetPropertyDetailAsync(
            propertyId: "DOES-NOT-EXIST",
            tenantContext: ctx,
            properties: propRepo,
            equipment: eqRepo,
            ct: CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    [Fact]
    public async Task GetPropertyDetail_DoesNotLeakAnotherTenants_Property()
    {
        var mine = new TenantId("tenant-mine");
        var other = new TenantId("tenant-other");
        var propId = new PropertyId("PROP-100");

        var propRepo = new InMemoryPropertyRepository();
        await propRepo.UpsertAsync(NewProperty(other, propId, "100 Their Way", "Provo", "UT", PropertyKind.SingleFamily));

        var eqRepo = new InMemoryEquipmentRepository(new InMemoryEquipmentLifecycleEventStore());
        var ctx = new TestTenantContext(mine.Value);

        var result = await PropertyDetailEndpoint.HandleGetPropertyDetailAsync(
            propertyId: propId.Value,
            tenantContext: ctx,
            properties: propRepo,
            equipment: eqRepo,
            ct: CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    // ── Fixtures ────────────────────────────────────────────────────────────

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

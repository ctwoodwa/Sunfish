using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Properties.Tests;

/// <summary>
/// W#62 Phase 1 — repository contract coverage for <see cref="InMemoryPropertyUnitRepository"/>.
/// Five cases per the hand-off acceptance criteria.
/// </summary>
public class InMemoryPropertyUnitRepositoryTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly PropertyId Prop1 = new("PROP-1");
    private static readonly PropertyId Prop2 = new("PROP-2");

    [Fact]
    public async Task UpsertThenListByProperty_ReturnsTheUnit()
    {
        var repo = new InMemoryPropertyUnitRepository();
        var unit = NewUnit(TenantA, Prop1, "101");
        await repo.UpsertAsync(unit);

        var result = await repo.ListByPropertyAsync(TenantA, Prop1);

        var single = Assert.Single(result);
        Assert.Equal("101", single.UnitNumber);
        Assert.Equal(Prop1, single.PropertyId);
    }

    [Fact]
    public async Task ListByPropertyAsync_ExcludesUnitsFromOtherProperties()
    {
        var repo = new InMemoryPropertyUnitRepository();
        await repo.UpsertAsync(NewUnit(TenantA, Prop1, "101"));
        await repo.UpsertAsync(NewUnit(TenantA, Prop1, "102"));
        await repo.UpsertAsync(NewUnit(TenantA, Prop2, "201"));

        var prop1Units = await repo.ListByPropertyAsync(TenantA, Prop1);

        Assert.Equal(2, prop1Units.Count);
        Assert.All(prop1Units, u => Assert.Equal(Prop1, u.PropertyId));
    }

    [Fact]
    public async Task ListByTenantAsync_ReturnsAllUnitsAcrossProperties()
    {
        var repo = new InMemoryPropertyUnitRepository();
        await repo.UpsertAsync(NewUnit(TenantA, Prop1, "101"));
        await repo.UpsertAsync(NewUnit(TenantA, Prop2, "201"));
        await repo.UpsertAsync(NewUnit(TenantB, Prop1, "999")); // Other tenant — must NOT leak

        var tenantAUnits = await repo.ListByTenantAsync(TenantA);

        Assert.Equal(2, tenantAUnits.Count);
        Assert.All(tenantAUnits, u => Assert.Equal(TenantA, u.TenantId));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForUnknownEntityId()
    {
        var repo = new InMemoryPropertyUnitRepository();
        var phantom = EntityId.Parse("unit:tenant-a/does-not-exist");

        var result = await repo.GetByIdAsync(TenantA, phantom);

        Assert.Null(result);
    }

    [Fact]
    public void NewId_ProducesUnitSchemeEntityIdWithTenantAuthority()
    {
        var id = PropertyUnit.NewId(TenantA);

        Assert.Equal("unit", id.Scheme);
        Assert.Equal("tenant-a", id.Authority);
        Assert.False(string.IsNullOrEmpty(id.LocalPart));
    }

    private static PropertyUnit NewUnit(TenantId tenant, PropertyId property, string unitNumber) =>
        new()
        {
            Id          = PropertyUnit.NewId(tenant),
            TenantId    = tenant,
            PropertyId  = property,
            UnitNumber  = unitNumber,
            Status      = UnitStatus.Available,
            CreatedAt   = DateTimeOffset.UnixEpoch,
        };
}

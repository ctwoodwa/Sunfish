using Sunfish.Blocks.Leases.Models;
using Xunit;

namespace Sunfish.Blocks.Leases.Tests;

public sealed class LeaseHolderRoleTests
{
    [Fact]
    public void EnumValues_Cover_AllFourRoles()
    {
        Assert.Equal(4, Enum.GetValues<LeaseHolderRole>().Length);
        Assert.Contains(LeaseHolderRole.PrimaryLeaseholder, Enum.GetValues<LeaseHolderRole>());
        Assert.Contains(LeaseHolderRole.CoLeaseholder, Enum.GetValues<LeaseHolderRole>());
        Assert.Contains(LeaseHolderRole.Occupant, Enum.GetValues<LeaseHolderRole>());
        Assert.Contains(LeaseHolderRole.Guarantor, Enum.GetValues<LeaseHolderRole>());
    }

    [Fact]
    public void LeasePartyRoleId_NewId_GeneratesUniqueValues()
    {
        var a = LeasePartyRoleId.NewId();
        var b = LeasePartyRoleId.NewId();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void LeasePartyRole_RoundTrip_PreservesAllFields()
    {
        var leaseId = LeaseId.NewId();
        var partyId = new PartyId("tenant-1");
        var roleId = LeasePartyRoleId.NewId();

        var role = new LeasePartyRole
        {
            Id = roleId,
            Lease = leaseId,
            Party = partyId,
            Role = LeaseHolderRole.PrimaryLeaseholder,
        };

        Assert.Equal(roleId, role.Id);
        Assert.Equal(leaseId, role.Lease);
        Assert.Equal(partyId, role.Party);
        Assert.Equal(LeaseHolderRole.PrimaryLeaseholder, role.Role);
    }

    [Fact]
    public void Lease_PartyRoles_DefaultsToEmpty()
    {
        var lease = new Lease
        {
            Id = LeaseId.NewId(),
            UnitId = new Sunfish.Foundation.Assets.Common.EntityId("unit", "test", "u-1"),
            Tenants = new[] { new PartyId("tenant-1") },
            Landlord = new PartyId("landlord-x"),
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2027, 4, 30),
            MonthlyRent = 1500m,
            Phase = LeasePhase.Draft,
        };

        Assert.Empty(lease.PartyRoles);
    }

    [Fact]
    public void Lease_With_PartyRoles_PreservesOrder()
    {
        var primary = LeasePartyRoleId.NewId();
        var co = LeasePartyRoleId.NewId();
        var occupant = LeasePartyRoleId.NewId();

        var lease = new Lease
        {
            Id = LeaseId.NewId(),
            UnitId = new Sunfish.Foundation.Assets.Common.EntityId("unit", "test", "u-1"),
            Tenants = new[] { new PartyId("t-1") },
            Landlord = new PartyId("l-1"),
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2027, 4, 30),
            MonthlyRent = 1500m,
            Phase = LeasePhase.Draft,
            PartyRoles = new[] { primary, co, occupant },
        };

        Assert.Equal(3, lease.PartyRoles.Count);
        Assert.Equal(primary, lease.PartyRoles[0]);
        Assert.Equal(co, lease.PartyRoles[1]);
        Assert.Equal(occupant, lease.PartyRoles[2]);
    }
}

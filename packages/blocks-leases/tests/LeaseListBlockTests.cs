using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Leases.Tests;

public class LeaseListBlockTests : BunitContext
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Lease MakeLease(string id = "lease-1") => new()
    {
        Id = new LeaseId(id),
        UnitId = new EntityId("unit", "test", id),
        Tenants = [new PartyId("tenant-a")],
        Landlord = new PartyId("landlord-x"),
        StartDate = new DateOnly(2025, 1, 1),
        EndDate = new DateOnly(2025, 12, 31),
        MonthlyRent = 1500m,
        Phase = LeasePhase.Active
    };

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void EmptyService_Renders_NoLeasesPlaceholder()
    {
        Services.AddSingleton<ILeaseService, InMemoryLeaseService>();

        var cut = Render<LeaseListBlock>();
        cut.WaitForState(() => !cut.Markup.Contains("sf-lease-list__loading"), TimeSpan.FromSeconds(5));

        Assert.Contains("No leases", cut.Markup);
    }

    [Fact]
    public async Task PopulatedService_Renders_LeaseRows()
    {
        var svc = new InMemoryLeaseService();
        var lease = await svc.CreateAsync(new CreateLeaseRequest
        {
            UnitId = new EntityId("unit", "test", "apt-1"),
            Tenants = [new PartyId("tenant-a")],
            Landlord = new PartyId("landlord-x"),
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            MonthlyRent = 1800m
        });

        Services.AddSingleton<ILeaseService>(svc);

        var cut = Render<LeaseListBlock>();
        cut.WaitForState(() => !cut.Markup.Contains("sf-lease-list__loading"), TimeSpan.FromSeconds(5));

        Assert.Contains(lease.Id.Value, cut.Markup);
        Assert.Contains("Draft", cut.Markup);
    }

    [Fact]
    public void LeaseListBlock_TypeIsPublicAndInBlocksLeasesNamespace()
    {
        var type = typeof(LeaseListBlock);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Leases", type.Namespace);
    }
}

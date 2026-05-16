using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Blocks.WorkOrders.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkOrders.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="Contractor"/> per
/// <c>blocks-work-schema-design.md</c> §2.11.
/// </summary>
public sealed class ContractorTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid PartyId = Guid.NewGuid();

    [Fact]
    public void Create_ValidContractor_StatusIsActive()
    {
        var c = NewContractor();
        Assert.Equal(ContractorStatus.Active, c.Status);
        Assert.Equal(PartyId, c.PartyId);
    }

    [Fact]
    public void Create_EmptyDisplayName_Throws()
    {
        Assert.Throws<ArgumentException>(() => Contractor.Create(
            Tenant, PartyId, "  ", new[] { TradeCategory.Plumbing }, Actor));
    }

    [Fact]
    public void Create_EmptyTrades_Throws()
    {
        Assert.Throws<ArgumentException>(() => Contractor.Create(
            Tenant, PartyId, "Joe's Plumbing", Array.Empty<TradeCategory>(), Actor));
    }

    [Fact]
    public void Blacklist_ActiveContractor_StatusIsBlacklisted()
    {
        var c = NewContractor();
        c.Blacklist("workmanship complaints", Actor);
        Assert.Equal(ContractorStatus.Blacklisted, c.Status);
        Assert.NotNull(c.Notes);
        Assert.Contains("workmanship complaints", c.Notes!);
    }

    [Fact]
    public void Blacklist_EmptyReason_Throws()
    {
        var c = NewContractor();
        Assert.Throws<ArgumentException>(() => c.Blacklist("  ", Actor));
    }

    [Fact]
    public void IsComplianceExpiringSoon_InsuranceIn29Days_ReturnsTrue()
    {
        var c = NewContractor();
        var today = new DateOnly(2026, 5, 1);
        c.UpdateCompliance(
            licenseNumber: "L-123", licenseExpiresOn: today.AddDays(180),
            insurancePolicyNumber: "I-456", insuranceExpiresOn: today.AddDays(29),
            bondedAmount: null, bondedCurrency: null,
            w9OnFile: true, w9ReceivedOn: today,
            updatedBy: Actor);

        Assert.True(c.IsComplianceExpiringSoon(today));
    }

    [Fact]
    public void IsComplianceExpiringSoon_InsuranceIn31Days_ReturnsFalse()
    {
        var c = NewContractor();
        var today = new DateOnly(2026, 5, 1);
        c.UpdateCompliance(
            licenseNumber: "L-123", licenseExpiresOn: today.AddDays(180),
            insurancePolicyNumber: "I-456", insuranceExpiresOn: today.AddDays(31),
            bondedAmount: null, bondedCurrency: null,
            w9OnFile: true, w9ReceivedOn: today,
            updatedBy: Actor);

        Assert.False(c.IsComplianceExpiringSoon(today));
    }

    [Fact]
    public void RecordRating_OutOfBounds_Throws()
    {
        var c = NewContractor();
        Assert.Throws<ArgumentOutOfRangeException>(() => c.RecordRating(6m, Actor));
        Assert.Throws<ArgumentOutOfRangeException>(() => c.RecordRating(0m, Actor));
    }

    [Fact]
    public void RecordRating_AveragesAcrossCalls()
    {
        var c = NewContractor();
        c.RecordRating(4m, Actor);
        c.RecordRating(5m, Actor);
        c.RecordRating(3m, Actor);
        Assert.Equal(3, c.RatingCount);
        Assert.Equal(4m, c.Rating);
    }

    [Fact]
    public async Task FindByTrade_ReturnsOnlyMatchingTrade()
    {
        var repo = new InMemoryContractorRepository();
        var plumber = Contractor.Create(Tenant, Guid.NewGuid(), "Plumber Joe",
            new[] { TradeCategory.Plumbing }, Actor);
        var hvac = Contractor.Create(Tenant, Guid.NewGuid(), "HVAC Sam",
            new[] { TradeCategory.Hvac }, Actor);
        repo.Upsert(plumber);
        repo.Upsert(hvac);

        var plumbers = await repo.FindByTradeAsync(TradeCategory.Plumbing);

        Assert.Single(plumbers);
        Assert.Equal(plumber.Id, plumbers[0].Id);
    }

    [Fact]
    public async Task FindByTrade_ExcludesBlacklisted()
    {
        var repo = new InMemoryContractorRepository();
        var plumber = Contractor.Create(Tenant, Guid.NewGuid(), "Plumber Joe",
            new[] { TradeCategory.Plumbing }, Actor);
        plumber.Blacklist("test", Actor);
        repo.Upsert(plumber);

        var plumbers = await repo.FindByTradeAsync(TradeCategory.Plumbing);

        Assert.Empty(plumbers);
    }

    [Fact]
    public async Task GetPreferredContractors_ReturnsPreferredOnly_OrderedByRatingDesc()
    {
        var repo = new InMemoryContractorRepository();
        var c1 = NewWithRating("Top-rated preferred", preferred: true, rating: 4.9m);
        var c2 = NewWithRating("Mid preferred", preferred: true, rating: 4.0m);
        var c3 = NewWithRating("Not preferred", preferred: false, rating: 5.0m);
        repo.Upsert(c1);
        repo.Upsert(c2);
        repo.Upsert(c3);

        var preferred = await repo.GetPreferredContractorsAsync();

        Assert.Equal(2, preferred.Count);
        Assert.Equal(c1.Id, preferred[0].Id);
        Assert.Equal(c2.Id, preferred[1].Id);
    }

    [Fact]
    public async Task InMemoryPartyReadModel_UnknownId_ReturnsNull()
    {
        var pr = new InMemoryPartyReadModel();
        var name = await pr.GetDisplayNameAsync(Guid.NewGuid());
        Assert.Null(name);
    }

    [Fact]
    public async Task InMemoryPartyReadModel_SeededId_ReturnsName()
    {
        var pr = new InMemoryPartyReadModel();
        var partyId = Guid.NewGuid();
        pr.Seed(partyId, "Acero Properties LLC");

        var name = await pr.GetDisplayNameAsync(partyId);

        Assert.Equal("Acero Properties LLC", name);
    }

    // ----- helpers ---------------------------------------------------

    private static Contractor NewContractor() => Contractor.Create(
        Tenant, PartyId, "Joe's Plumbing",
        new[] { TradeCategory.Plumbing }, Actor);

    private static Contractor NewWithRating(string name, bool preferred, decimal rating)
    {
        var c = Contractor.Create(Tenant, Guid.NewGuid(), name,
            new[] { TradeCategory.General }, Actor);
        c.UpdateOperational(preferred, hourlyRate: 75m, hourlyRateCurrency: "USD",
            emergencyAvailable: false, updatedBy: Actor);
        c.RecordRating(rating, Actor);
        return c;
    }
}

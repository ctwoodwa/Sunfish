using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Maintenance.Tests;

/// <summary>
/// W#18 Phase 2 — VendorContact CRUD + per-property primary override.
/// </summary>
public sealed class VendorContactServiceTests
{
    private static readonly VendorId VendorA = VendorId.NewId();
    private static readonly VendorId VendorB = VendorId.NewId();
    private static readonly EntityId PropertyAlpha = new("property", "test", "alpha");
    private static readonly EntityId PropertyBeta = new("property", "test", "beta");

    private static VendorContact MakeContact(
        VendorId vendor,
        string name = "Alice",
        string role = "Owner",
        bool isPrimary = false,
        IReadOnlyDictionary<EntityId, bool>? perPropertyOverrides = null) =>
        new()
        {
            Id = new VendorContactId(Guid.NewGuid()),
            Vendor = vendor,
            Name = name,
            RoleLabel = role,
            IsPrimaryForVendor = isPrimary,
            PrimaryForProperty = perPropertyOverrides ?? new Dictionary<EntityId, bool>(),
        };

    private static async Task<List<VendorContact>> CollectAsync(IAsyncEnumerable<VendorContact> source)
    {
        var list = new List<VendorContact>();
        await foreach (var c in source) list.Add(c);
        return list;
    }

    [Fact]
    public async Task AddContact_RoundTripsViaList()
    {
        var svc = new InMemoryVendorContactService();
        var contact = MakeContact(VendorA);

        await svc.AddContactAsync(contact, default);
        var listed = await CollectAsync(svc.ListContactsAsync(VendorA, default));

        Assert.Single(listed);
        Assert.Equal(contact.Id, listed[0].Id);
    }

    [Fact]
    public async Task UpdateContact_ReplacesById()
    {
        var svc = new InMemoryVendorContactService();
        var original = MakeContact(VendorA, name: "Alice", role: "Owner");
        await svc.AddContactAsync(original, default);

        var updated = original with { Name = "Alice Smith", RoleLabel = "CEO" };
        await svc.UpdateContactAsync(updated, default);

        var listed = await CollectAsync(svc.ListContactsAsync(VendorA, default));
        Assert.Equal("Alice Smith", listed[0].Name);
        Assert.Equal("CEO", listed[0].RoleLabel);
    }

    [Fact]
    public async Task UpdateContact_UnknownId_Throws()
    {
        var svc = new InMemoryVendorContactService();
        var ghost = MakeContact(VendorA);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateContactAsync(ghost, default));
    }

    [Fact]
    public async Task RemoveContact_TakesEffect()
    {
        var svc = new InMemoryVendorContactService();
        var contact = MakeContact(VendorA);
        await svc.AddContactAsync(contact, default);

        await svc.RemoveContactAsync(contact.Id, default);

        Assert.Empty(await CollectAsync(svc.ListContactsAsync(VendorA, default)));
    }

    [Fact]
    public async Task RemoveContact_UnknownId_NoOp()
    {
        var svc = new InMemoryVendorContactService();
        await svc.RemoveContactAsync(new VendorContactId(Guid.NewGuid()), default);
        // Reaches here without throw → pass.
    }

    [Fact]
    public async Task ListContacts_IsTenantIsolated_AcrossVendors()
    {
        var svc = new InMemoryVendorContactService();
        await svc.AddContactAsync(MakeContact(VendorA, "AliceA"), default);
        await svc.AddContactAsync(MakeContact(VendorB, "AliceB"), default);

        var aContacts = await CollectAsync(svc.ListContactsAsync(VendorA, default));
        var bContacts = await CollectAsync(svc.ListContactsAsync(VendorB, default));

        Assert.Single(aContacts);
        Assert.Equal("AliceA", aContacts[0].Name);
        Assert.Single(bContacts);
        Assert.Equal("AliceB", bContacts[0].Name);
    }

    [Fact]
    public async Task GetPrimaryForProperty_VendorWideDefault_NoOverride()
    {
        var svc = new InMemoryVendorContactService();
        var primary = MakeContact(VendorA, "PrimaryAlice", isPrimary: true);
        var secondary = MakeContact(VendorA, "Bob", isPrimary: false);
        await svc.AddContactAsync(primary, default);
        await svc.AddContactAsync(secondary, default);

        var resolved = await svc.GetPrimaryForPropertyAsync(VendorA, PropertyAlpha, default);

        Assert.NotNull(resolved);
        Assert.Equal(primary.Id, resolved!.Id);
    }

    [Fact]
    public async Task GetPrimaryForProperty_PerPropertyOverride_BeatsVendorDefault()
    {
        var svc = new InMemoryVendorContactService();
        var defaultPrimary = MakeContact(VendorA, "DefaultPrimary", isPrimary: true);
        var perPropPrimary = MakeContact(VendorA, "PerPropertyAlpha", isPrimary: false,
            perPropertyOverrides: new Dictionary<EntityId, bool> { [PropertyAlpha] = true });
        await svc.AddContactAsync(defaultPrimary, default);
        await svc.AddContactAsync(perPropPrimary, default);

        var alphaResolved = await svc.GetPrimaryForPropertyAsync(VendorA, PropertyAlpha, default);
        var betaResolved = await svc.GetPrimaryForPropertyAsync(VendorA, PropertyBeta, default);

        // Alpha: per-property override wins.
        Assert.Equal(perPropPrimary.Id, alphaResolved!.Id);
        // Beta: no override, falls through to vendor-wide default.
        Assert.Equal(defaultPrimary.Id, betaResolved!.Id);
    }

    [Fact]
    public async Task GetPrimaryForProperty_NullProperty_SkipsOverrides()
    {
        var svc = new InMemoryVendorContactService();
        var defaultPrimary = MakeContact(VendorA, "Default", isPrimary: true);
        var perPropPrimary = MakeContact(VendorA, "Override", isPrimary: false,
            perPropertyOverrides: new Dictionary<EntityId, bool> { [PropertyAlpha] = true });
        await svc.AddContactAsync(defaultPrimary, default);
        await svc.AddContactAsync(perPropPrimary, default);

        var resolved = await svc.GetPrimaryForPropertyAsync(VendorA, property: null, default);

        Assert.Equal(defaultPrimary.Id, resolved!.Id);
    }

    [Fact]
    public async Task GetPrimaryForProperty_NoPrimary_ReturnsNull()
    {
        var svc = new InMemoryVendorContactService();
        await svc.AddContactAsync(MakeContact(VendorA, isPrimary: false), default);

        var resolved = await svc.GetPrimaryForPropertyAsync(VendorA, PropertyAlpha, default);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task PrimaryInvariant_AddNewPrimary_DemotesExistingPrimary()
    {
        var svc = new InMemoryVendorContactService();
        var first = MakeContact(VendorA, "First", isPrimary: true);
        var second = MakeContact(VendorA, "Second", isPrimary: true);
        await svc.AddContactAsync(first, default);
        await svc.AddContactAsync(second, default);

        var listed = await CollectAsync(svc.ListContactsAsync(VendorA, default));

        var firstAfter = listed.Single(c => c.Id == first.Id);
        var secondAfter = listed.Single(c => c.Id == second.Id);
        Assert.False(firstAfter.IsPrimaryForVendor);
        Assert.True(secondAfter.IsPrimaryForVendor);
    }

    [Fact]
    public async Task PrimaryInvariant_DemotionStaysWithinVendor()
    {
        // Vendor A's primary should NOT be demoted when Vendor B gets a primary.
        var svc = new InMemoryVendorContactService();
        var aPrimary = MakeContact(VendorA, "AlicePrimary", isPrimary: true);
        var bPrimary = MakeContact(VendorB, "BobPrimary", isPrimary: true);
        await svc.AddContactAsync(aPrimary, default);
        await svc.AddContactAsync(bPrimary, default);

        var aListed = await CollectAsync(svc.ListContactsAsync(VendorA, default));
        Assert.True(aListed.Single().IsPrimaryForVendor);
    }
}

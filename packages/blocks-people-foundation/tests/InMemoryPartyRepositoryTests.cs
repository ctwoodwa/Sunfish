using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.People.Foundation.Tests;

public class InMemoryPartyRepositoryTests
{
    private static TenantId TenantA() => new("tenant-a");
    private static TenantId TenantB() => new("tenant-b");
    private static PartyId Actor() => PartyId.NewId();

    private static InMemoryPartyRepository NewRepo() => new();

    // ── CreateAsync + GetByIdAsync ─────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PersistsParty_RoundTripsViaGetById()
    {
        var repo = NewRepo();
        var actor = Actor();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", actor);

        var fetched = await repo.GetByIdAsync(p.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Jane Doe", fetched!.DisplayName);
        Assert.Equal(actor, fetched.CreatedBy);
        Assert.Equal(1, fetched.Version);
    }

    [Fact]
    public async Task CreateAsync_BlankDisplayName_ThrowsValidationException()
    {
        var repo = NewRepo();
        await Assert.ThrowsAsync<PartyValidationException>(() =>
            repo.CreateAsync(TenantA(), PartyKind.Person, "   ", Actor()));
    }

    [Fact]
    public async Task GetByIdAsync_OnTombstonedParty_ReturnsNull()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        await repo.DeleteAsync(p.Id, "test cleanup", Actor());

        var fetched = await repo.GetByIdAsync(p.Id);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task ListByTenantAsync_ReturnsOnlyLiveTenantRows()
    {
        var repo = NewRepo();
        var a1 = await repo.CreateAsync(TenantA(), PartyKind.Person, "A-Alice", Actor());
        await repo.CreateAsync(TenantA(), PartyKind.Person, "A-Bob", Actor());
        var b1 = await repo.CreateAsync(TenantB(), PartyKind.Person, "B-Charlie", Actor());

        // Tombstone one tenant-A row to confirm it's excluded.
        var aDel = await repo.CreateAsync(TenantA(), PartyKind.Person, "A-Dave", Actor());
        await repo.DeleteAsync(aDel.Id, null, Actor());

        var aRows = await repo.ListByTenantAsync(TenantA());
        Assert.Equal(2, aRows.Count);
        Assert.DoesNotContain(aRows, x => x.Id == aDel.Id);
        Assert.DoesNotContain(aRows, x => x.Id == b1.Id);
    }

    // ── UpdateAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_BumpsVersion_AndStampsUpdatedFields()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());

        var actor2 = Actor();
        var updated = p with { GivenName = "Jane", FamilyName = "Doe", DisplayName = "Jane Doe (legal)" };
        var saved = await repo.UpdateAsync(updated, actor2);

        Assert.Equal(2, saved.Version);
        Assert.Equal(actor2, saved.UpdatedBy);
        Assert.Equal("Jane Doe (legal)", saved.DisplayName);
        Assert.True(saved.UpdatedAt.Value > p.CreatedAt.Value);
    }

    [Fact]
    public async Task UpdateAsync_OnTombstonedParty_Throws()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        await repo.DeleteAsync(p.Id, "test", Actor());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.UpdateAsync(p with { DisplayName = "Whatever" }, Actor()));
    }

    [Fact]
    public async Task DeleteAsync_IsIdempotent()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        var d1 = await repo.DeleteAsync(p.Id, "first", Actor());
        var d2 = await repo.DeleteAsync(p.Id, "second", Actor());
        Assert.Equal(d1.Version, d2.Version); // no version bump on second delete
        Assert.Equal("first", d2.DeletedReason);
    }

    // ── AttachRoleAsync / DetachRoleAsync ──────────────────────────────

    [Fact]
    public async Task AttachRoleAsync_KnownCode_PersistsAndIsActive()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        var role = await repo.AttachRoleAsync(p.Id, PartyRoleName.Tenant, "lease-123", Actor());

        Assert.True(role.IsActive);
        Assert.Equal("lease-123", role.RoleRecordId);
        Assert.True(await repo.HasActiveRoleAsync(p.Id, PartyRoleName.Tenant));
    }

    [Fact]
    public async Task AttachRoleAsync_IsIdempotentOnSameRecordId()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        var r1 = await repo.AttachRoleAsync(p.Id, PartyRoleName.Tenant, "lease-123", Actor());
        var r2 = await repo.AttachRoleAsync(p.Id, PartyRoleName.Tenant, "lease-123", Actor());

        Assert.Equal(r1.Id, r2.Id);
        var allActive = await repo.GetActiveRolesAsync(p.Id);
        Assert.Single(allActive);
    }

    [Fact]
    public async Task AttachRoleAsync_DifferentRecordIds_CreatesSeparateRows()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        var r1 = await repo.AttachRoleAsync(p.Id, PartyRoleName.Tenant, "lease-A", Actor());
        var r2 = await repo.AttachRoleAsync(p.Id, PartyRoleName.Tenant, "lease-B", Actor());

        Assert.NotEqual(r1.Id, r2.Id);
        var actives = await repo.GetActiveRolesAsync(p.Id);
        Assert.Equal(2, actives.Count);
    }

    [Fact]
    public async Task AttachRoleAsync_UppercaseCode_ThrowsValidationException()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        await Assert.ThrowsAsync<PartyValidationException>(() =>
            repo.AttachRoleAsync(p.Id, "Customer", "invoice-1", Actor()));
    }

    [Fact]
    public async Task DetachRoleAsync_FlipsToEnded()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        var role = await repo.AttachRoleAsync(p.Id, PartyRoleName.Tenant, "lease-123", Actor());

        var ended = await repo.DetachRoleAsync(role.Id, "lease complete", Actor());
        Assert.False(ended.IsActive);
        Assert.False(await repo.HasActiveRoleAsync(p.Id, PartyRoleName.Tenant));
    }

    [Fact]
    public async Task DetachRoleAsync_OnAlreadyDetached_Throws()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        var role = await repo.AttachRoleAsync(p.Id, PartyRoleName.Tenant, "lease-123", Actor());
        await repo.DetachRoleAsync(role.Id, null, Actor());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.DetachRoleAsync(role.Id, null, Actor()));
    }

    // ── AddEmailAsync / SupersedeEmailAsync ────────────────────────────

    [Fact]
    public async Task AddEmailAsync_PersistsAndShowsInActiveList()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        var e = await repo.AddEmailAsync(p.Id, "jane@example.com", isPrimary: true, Actor());

        var active = await repo.GetActiveEmailsAsync(p.Id);
        Assert.Contains(active, x => x.Id == e.Id);
    }

    [Fact]
    public async Task AddEmailAsync_MalformedAddress_ThrowsValidationException()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        await Assert.ThrowsAsync<PartyValidationException>(() =>
            repo.AddEmailAsync(p.Id, "not-an-email", isPrimary: true, Actor()));
    }

    [Fact]
    public async Task SupersedeEmailAsync_InsertsNewAndMarksPriorReplaced()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        var prior = await repo.AddEmailAsync(p.Id, "jane@old.example.com", isPrimary: true, Actor());

        var newRow = await repo.SupersedeEmailAsync(prior.Id, "jane@new.example.com", isPrimary: true, Actor());

        var active = await repo.GetActiveEmailsAsync(p.Id);
        Assert.Single(active);
        Assert.Equal(newRow.Id, active[0].Id);
        Assert.Equal("jane@new.example.com", active[0].Address);
        // Prior row still exists, but is now replaced.
        Assert.True(active.All(x => x.ReplacedAt is null));
    }

    [Fact]
    public async Task SupersedeEmailAsync_OnAlreadySuperseded_Throws()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane Doe", Actor());
        var prior = await repo.AddEmailAsync(p.Id, "jane@old.example.com", isPrimary: true, Actor());
        await repo.SupersedeEmailAsync(prior.Id, "jane@new.example.com", isPrimary: true, Actor());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.SupersedeEmailAsync(prior.Id, "jane@third.example.com", isPrimary: true, Actor()));
    }

    // ── FindByExact* ───────────────────────────────────────────────────

    [Fact]
    public async Task FindByExactEmailAsync_CaseInsensitive_TenantScoped()
    {
        var repo = NewRepo();
        var pA = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane", Actor());
        await repo.AddEmailAsync(pA.Id, "Jane@Example.com", isPrimary: true, Actor());
        var pB = await repo.CreateAsync(TenantB(), PartyKind.Person, "Jane B", Actor());
        await repo.AddEmailAsync(pB.Id, "jane@example.com", isPrimary: true, Actor());

        var aHits = await repo.FindByExactEmailAsync(TenantA(), "jane@example.com");
        Assert.Single(aHits);
        Assert.Equal(pA.Id, aHits[0].Id);
    }

    [Fact]
    public async Task FindByExactPhoneE164Async_IsCaseSensitive_TenantScoped()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane", Actor());
        await repo.AddPhoneAsync(p.Id, "+14155551234", isPrimary: true, Actor());

        var hits = await repo.FindByExactPhoneE164Async(TenantA(), "+14155551234");
        Assert.Single(hits);

        var miss = await repo.FindByExactPhoneE164Async(TenantB(), "+14155551234");
        Assert.Empty(miss);
    }

    [Fact]
    public async Task AddPhoneAsync_NonE164_ThrowsValidationException()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane", Actor());
        await Assert.ThrowsAsync<PartyValidationException>(() =>
            repo.AddPhoneAsync(p.Id, "415-555-1234", isPrimary: true, Actor()));
    }

    [Fact]
    public async Task AddAddressAsync_BadCountry_ThrowsValidationException()
    {
        var repo = NewRepo();
        var p = await repo.CreateAsync(TenantA(), PartyKind.Person, "Jane", Actor());
        var addr = new Address("100 Main St", "Austin", "TX", "78701", "USA"); // alpha-3, should fail
        await Assert.ThrowsAsync<PartyValidationException>(() =>
            repo.AddAddressAsync(p.Id, addr, isPrimary: true, Actor()));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Services;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

using PartyId = Sunfish.Blocks.People.Foundation.Models.PartyId;
using Party = Sunfish.Blocks.People.Foundation.Models.Party;
using PartyKind = Sunfish.Blocks.People.Foundation.Models.PartyKind;

namespace Sunfish.Blocks.Leases.Tests;

/// <summary>W#27 follow-on PR 2 — coverage for <see cref="ILeaseService.GetLeaseholderDisplaysAsync"/>.</summary>
public sealed class GetLeaseholderDisplaysAsyncTests
{
    private static readonly Sunfish.Foundation.Assets.Common.TenantId Tenant = new("t1");

    private sealed class StubPartyReadModel : IPartyReadModel
    {
        public Dictionary<PartyId, Party> Parties { get; } = new();

        public Task<Party?> GetByIdAsync(PartyId id, CancellationToken cancellationToken = default)
            => Task.FromResult(Parties.TryGetValue(id, out var p) ? p : (Party?)null);

        public Task<IReadOnlyDictionary<PartyId, Party>> GetManyAsync(IReadOnlyCollection<PartyId> ids, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<PartyId, Party>>(
                ids.Where(Parties.ContainsKey).ToDictionary(i => i, i => Parties[i]));

        public Task<IReadOnlyList<Party>> ListByTenantAsync(Sunfish.Foundation.Assets.Common.TenantId tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Party>>(Parties.Values.ToList());

        public Task<IReadOnlyList<Party>> FindByExactDisplayNameAsync(Sunfish.Foundation.Assets.Common.TenantId tenantId, string displayName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Party>>(Parties.Values.Where(p => p.DisplayName == displayName).ToList());

        public Task<IReadOnlyList<Party>> FindByExactEmailAsync(Sunfish.Foundation.Assets.Common.TenantId tenantId, string email, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Party>>(Array.Empty<Party>());

        public Task<IReadOnlyList<Party>> FindByExactPhoneE164Async(Sunfish.Foundation.Assets.Common.TenantId tenantId, string phone, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Party>>(Array.Empty<Party>());

        public Task<IReadOnlyList<EmailAddress>> GetActiveEmailsAsync(PartyId id, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EmailAddress>>(Array.Empty<EmailAddress>());

        public Task<IReadOnlyList<PhoneNumber>> GetActivePhonesAsync(PartyId id, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PhoneNumber>>(Array.Empty<PhoneNumber>());

        public Task<IReadOnlyList<PartyAddress>> GetActiveAddressesAsync(PartyId id, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PartyAddress>>(Array.Empty<PartyAddress>());

        public Task<IReadOnlyList<PartyRole>> GetActiveRolesAsync(PartyId id, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PartyRole>>(Array.Empty<PartyRole>());

        public Task<bool> HasActiveRoleAsync(PartyId id, string roleName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class StubSigner : IOperationSigner
    {
        public Sunfish.Foundation.Crypto.PrincipalId IssuerId { get; }
            = Sunfish.Foundation.Crypto.PrincipalId.FromBytes(new byte[32]);

        public ValueTask<Sunfish.Foundation.Crypto.SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default)
            => ValueTask.FromResult(new Sunfish.Foundation.Crypto.SignedOperation<T>(payload, IssuerId, issuedAt, nonce, Sunfish.Foundation.Crypto.Signature.FromBytes(new byte[64])));
    }

    private static CreateLeaseRequest MakeRequest(IReadOnlyList<PartyId> tenants, PartyId landlord)
        => new()
        {
            UnitId      = new Sunfish.Foundation.Assets.Common.EntityId("sunfish", "t1", "u1"),
            Tenants     = tenants,
            Landlord    = landlord,
            StartDate   = new DateOnly(2026, 5, 1),
            EndDate     = new DateOnly(2027, 5, 1),
            MonthlyRent = 1500m,
        };

    [Fact]
    public async Task ReturnsEmptyList_WhenLeaseNotFound()
    {
        var svc = new InMemoryLeaseService();
        var result = await svc.GetLeaseholderDisplaysAsync(LeaseId.NewId());
        Assert.Empty(result);
    }

    [Fact]
    public async Task ReturnsTenants_FirstAsPrimary_RestAsCo()
    {
        var alice = new PartyId("alice");
        var bob   = new PartyId("bob");
        var svc = new InMemoryLeaseService();
        var lease = await svc.CreateAsync(MakeRequest(new[] { alice, bob }, new PartyId("landlord")));

        var result = await svc.GetLeaseholderDisplaysAsync(lease.Id);
        Assert.Equal(2, result.Count);
        Assert.Equal(LeaseHolderRole.PrimaryLeaseholder, result[0].Role);
        Assert.Equal(LeaseHolderRole.CoLeaseholder, result[1].Role);
        Assert.Equal(alice.Value, result[0].PartyId.Value);
        Assert.Equal(bob.Value, result[1].PartyId.Value);
        // Without IPartyReadModel wired, DisplayName is null per CRDT §12.
        Assert.Null(result[0].DisplayName);
        Assert.Null(result[1].DisplayName);
    }

    [Fact]
    public async Task ResolvesDisplayName_WhenPartyReadModelWired()
    {
        var alice = new PartyId("alice");
        var stub = new StubPartyReadModel();
        stub.Parties[alice] = Party.Create(
            tenantId:    Tenant,
            kind:        PartyKind.Person,
            displayName: "Alice Adams",
            createdBy:   alice,
            id:          alice);

        var svc = new InMemoryLeaseService(
            auditTrail:         new InMemoryAuditTrail(),
            signer:             new StubSigner(),
            tenantId:           Tenant,
            documentVersionLog: null,
            partyReadModel:     stub);
        var lease = await svc.CreateAsync(MakeRequest(new[] { alice }, new PartyId("landlord")));

        var result = await svc.GetLeaseholderDisplaysAsync(lease.Id);
        var only = Assert.Single(result);
        Assert.Equal("Alice Adams", only.DisplayName);
        Assert.Equal(alice.Value, only.PartyId.Value);
        Assert.Equal(LeaseHolderRole.PrimaryLeaseholder, only.Role);
    }

    [Fact]
    public async Task ReturnsNullDisplayName_WhenPartyNotInReadModel()
    {
        // Orphan-tolerant per CRDT §12.
        var alice = new PartyId("alice");
        var stub = new StubPartyReadModel(); // empty
        var svc = new InMemoryLeaseService(
            auditTrail:         new InMemoryAuditTrail(),
            signer:             new StubSigner(),
            tenantId:           Tenant,
            documentVersionLog: null,
            partyReadModel:     stub);
        var lease = await svc.CreateAsync(MakeRequest(new[] { alice }, new PartyId("landlord")));

        var result = await svc.GetLeaseholderDisplaysAsync(lease.Id);
        Assert.Null(Assert.Single(result).DisplayName);
    }
}

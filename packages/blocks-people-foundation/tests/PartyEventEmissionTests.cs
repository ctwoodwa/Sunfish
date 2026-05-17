using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Models.Events;
using Sunfish.Blocks.People.Foundation.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;
using Xunit;

namespace Sunfish.Blocks.People.Foundation.Tests;

/// <summary>
/// Confirms the seven canonical event types fire on the seven write
/// operations. We use a recording publisher rather than testing the
/// envelope at the bit level — envelope shape is foundation-events's
/// concern.
/// </summary>
public class PartyEventEmissionTests
{
    private sealed class RecordingPublisher : IDomainEventPublisher
    {
        public List<(string Type, string IdempotencyKey, object Payload)> Events { get; } = new();

        public Task PublishAsync<TPayload>(
            DomainEventEnvelope<TPayload> envelope,
            CancellationToken cancellationToken = default)
        {
            Events.Add((envelope.EventType, envelope.IdempotencyKey, envelope.Payload!));
            return Task.CompletedTask;
        }
    }

    private static TenantId Tenant() => new("acme");
    private static PartyId Actor() => PartyId.NewId();

    [Fact]
    public async Task CreateAsync_EmitsPartyCreated()
    {
        var rec = new RecordingPublisher();
        var repo = new InMemoryPartyRepository(rec);
        var p = await repo.CreateAsync(Tenant(), PartyKind.Person, "Jane", Actor());

        var ev = Assert.Single(rec.Events);
        Assert.Equal(PeopleFoundationEventNames.PartyCreated, ev.Type);
        Assert.Equal($"party-created:{p.Id.Value}", ev.IdempotencyKey);
        Assert.IsType<PartyCreatedPayload>(ev.Payload);
    }

    [Fact]
    public async Task DeleteAsync_EmitsPartyDeleted()
    {
        var rec = new RecordingPublisher();
        var repo = new InMemoryPartyRepository(rec);
        var p = await repo.CreateAsync(Tenant(), PartyKind.Person, "Jane", Actor());
        rec.Events.Clear();

        await repo.DeleteAsync(p.Id, "test", Actor());

        var ev = Assert.Single(rec.Events);
        Assert.Equal(PeopleFoundationEventNames.PartyDeleted, ev.Type);
        Assert.Equal($"party-deleted:{p.Id.Value}", ev.IdempotencyKey);
    }

    [Fact]
    public async Task AttachRoleAsync_EmitsRoleAttached_AndIdempotentAttachDoesNot()
    {
        var rec = new RecordingPublisher();
        var repo = new InMemoryPartyRepository(rec);
        var p = await repo.CreateAsync(Tenant(), PartyKind.Person, "Jane", Actor());
        rec.Events.Clear();

        await repo.AttachRoleAsync(p.Id, PartyRoleName.Tenant, "lease-1", Actor());
        await repo.AttachRoleAsync(p.Id, PartyRoleName.Tenant, "lease-1", Actor()); // dup

        var attached = rec.Events.Where(e => e.Type == PeopleFoundationEventNames.RoleAttached).ToList();
        Assert.Single(attached);
    }

    [Fact]
    public async Task DetachRoleAsync_EmitsRoleDetached()
    {
        var rec = new RecordingPublisher();
        var repo = new InMemoryPartyRepository(rec);
        var p = await repo.CreateAsync(Tenant(), PartyKind.Person, "Jane", Actor());
        var role = await repo.AttachRoleAsync(p.Id, PartyRoleName.Tenant, "lease-1", Actor());
        rec.Events.Clear();

        await repo.DetachRoleAsync(role.Id, "lease complete", Actor());

        var ev = Assert.Single(rec.Events);
        Assert.Equal(PeopleFoundationEventNames.RoleDetached, ev.Type);
        Assert.Equal($"role-detached:{role.Id.Value}", ev.IdempotencyKey);
    }

    [Fact]
    public async Task AddEmailAsync_EmitsEmailAddressAdded()
    {
        var rec = new RecordingPublisher();
        var repo = new InMemoryPartyRepository(rec);
        var p = await repo.CreateAsync(Tenant(), PartyKind.Person, "Jane", Actor());
        rec.Events.Clear();

        var email = await repo.AddEmailAsync(p.Id, "jane@example.com", true, Actor());

        var ev = Assert.Single(rec.Events);
        Assert.Equal(PeopleFoundationEventNames.EmailAddressAdded, ev.Type);
        Assert.Equal($"email-added:{email.Id.Value}", ev.IdempotencyKey);
    }

    [Fact]
    public async Task SupersedeEmailAsync_EmitsEmailAddressAdded_OnNewRow()
    {
        var rec = new RecordingPublisher();
        var repo = new InMemoryPartyRepository(rec);
        var p = await repo.CreateAsync(Tenant(), PartyKind.Person, "Jane", Actor());
        var prior = await repo.AddEmailAsync(p.Id, "jane@old.example.com", true, Actor());
        rec.Events.Clear();

        var newRow = await repo.SupersedeEmailAsync(prior.Id, "jane@new.example.com", true, Actor());

        var ev = Assert.Single(rec.Events);
        Assert.Equal(PeopleFoundationEventNames.EmailAddressAdded, ev.Type);
        Assert.Equal($"email-added:{newRow.Id.Value}", ev.IdempotencyKey);
    }

    [Fact]
    public async Task AddPhoneAsync_EmitsPhoneNumberAdded()
    {
        var rec = new RecordingPublisher();
        var repo = new InMemoryPartyRepository(rec);
        var p = await repo.CreateAsync(Tenant(), PartyKind.Person, "Jane", Actor());
        rec.Events.Clear();

        var phone = await repo.AddPhoneAsync(p.Id, "+14155551234", true, Actor());

        var ev = Assert.Single(rec.Events);
        Assert.Equal(PeopleFoundationEventNames.PhoneNumberAdded, ev.Type);
        Assert.Equal($"phone-added:{phone.Id.Value}", ev.IdempotencyKey);
    }

    [Fact]
    public async Task AddAddressAsync_EmitsAddressAdded()
    {
        var rec = new RecordingPublisher();
        var repo = new InMemoryPartyRepository(rec);
        var p = await repo.CreateAsync(Tenant(), PartyKind.Person, "Jane", Actor());
        rec.Events.Clear();

        var addr = new Address("100 Main St", "Austin", "TX", "78701", "US");
        var saved = await repo.AddAddressAsync(p.Id, addr, true, Actor());

        var ev = Assert.Single(rec.Events);
        Assert.Equal(PeopleFoundationEventNames.AddressAdded, ev.Type);
        Assert.Equal($"address-added:{saved.Id.Value}", ev.IdempotencyKey);
    }

    [Fact]
    public async Task EventTypes_AllStartWithPeoplePrefix()
    {
        var rec = new RecordingPublisher();
        var repo = new InMemoryPartyRepository(rec);
        var p = await repo.CreateAsync(Tenant(), PartyKind.Person, "Jane", Actor());
        var role = await repo.AttachRoleAsync(p.Id, PartyRoleName.Tenant, "lease-1", Actor());
        await repo.AddEmailAsync(p.Id, "jane@example.com", true, Actor());
        await repo.AddPhoneAsync(p.Id, "+14155551234", true, Actor());
        await repo.AddAddressAsync(p.Id, new Address("100 Main St", "Austin", "TX", "78701", "US"), true, Actor());
        await repo.DetachRoleAsync(role.Id, null, Actor());
        await repo.DeleteAsync(p.Id, "test", Actor());

        Assert.All(rec.Events, e => Assert.StartsWith("People.", e.Type, StringComparison.Ordinal));
    }
}

using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Messaging.Data;
using Sunfish.Blocks.Messaging.DependencyInjection;
using Sunfish.Blocks.Messaging.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Messaging;
using Sunfish.Foundation.Persistence;
using Xunit;

namespace Sunfish.Blocks.Messaging.Tests;

public sealed class InMemoryThreadStoreTests
{
    private static readonly TenantId Tenant = new("tenant-a");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static Participant NewParticipant(string id, string name, string email) => new()
    {
        Id = new ParticipantId(Guid.NewGuid()),
        Identity = new ActorId(id),
        DisplayName = name,
        EmailAddress = email,
    };

    [Fact]
    public async Task Create_PersistsThread()
    {
        var store = new InMemoryThreadStore();
        var participants = new[] { NewParticipant("u1", "Alice", "alice@example.com"), NewParticipant("u2", "Bob", "bob@example.com") };

        var threadId = await store.CreateAsync(Tenant, participants, MessageVisibility.Public, Ct);
        var snapshot = await store.GetAsync(Tenant, threadId, Ct);

        Assert.NotNull(snapshot);
        Assert.Equal(threadId, snapshot!.Id);
        Assert.Equal(2, snapshot.Participants.Count);
        Assert.Equal(MessageVisibility.Public, snapshot.DefaultVisibility);
        Assert.Empty(snapshot.MessageIds);
    }

    [Fact]
    public async Task Create_RejectsEmptyParticipantSet()
    {
        var store = new InMemoryThreadStore();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CreateAsync(Tenant, Array.Empty<Participant>(), MessageVisibility.Public, Ct));
    }

    [Fact]
    public async Task Create_RejectsDefaultTenant()
    {
        var store = new InMemoryThreadStore();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CreateAsync(default, new[] { NewParticipant("u1", "A", "a@x") }, MessageVisibility.Public, Ct));
    }

    [Fact]
    public async Task Get_ReturnsNullForUnknownThread()
    {
        var store = new InMemoryThreadStore();
        var snapshot = await store.GetAsync(Tenant, new ThreadId(Guid.NewGuid()), Ct);
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Get_IsTenantScoped()
    {
        var store = new InMemoryThreadStore();
        var threadId = await store.CreateAsync(Tenant, new[] { NewParticipant("u1", "A", "a@x") }, MessageVisibility.Public, Ct);

        var crossTenant = await store.GetAsync(new TenantId("other"), threadId, Ct);
        Assert.Null(crossTenant);
    }

    [Fact]
    public async Task AppendMessage_AddsIdInOrder()
    {
        var store = new InMemoryThreadStore();
        var threadId = await store.CreateAsync(Tenant, new[] { NewParticipant("u1", "A", "a@x") }, MessageVisibility.Public, Ct);
        var m1 = new MessageId(Guid.NewGuid());
        var m2 = new MessageId(Guid.NewGuid());

        await store.AppendMessageAsync(Tenant, threadId, m1, Ct);
        await store.AppendMessageAsync(Tenant, threadId, m2, Ct);

        var snapshot = await store.GetAsync(Tenant, threadId, Ct);
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot!.MessageIds.Count);
        Assert.Equal(m1, snapshot.MessageIds[0]);
        Assert.Equal(m2, snapshot.MessageIds[1]);
    }

    [Fact]
    public async Task AppendMessage_ParallelAppends_PreservesAllIds()
    {
        var store = new InMemoryThreadStore();
        var threadId = await store.CreateAsync(Tenant, new[] { NewParticipant("u1", "A", "a@x") }, MessageVisibility.Public, Ct);

        var ids = Enumerable.Range(0, 25).Select(_ => new MessageId(Guid.NewGuid())).ToArray();
        await Task.WhenAll(ids.Select(id => store.AppendMessageAsync(Tenant, threadId, id, Ct)));

        var snapshot = await store.GetAsync(Tenant, threadId, Ct);
        Assert.NotNull(snapshot);
        Assert.Equal(25, snapshot!.MessageIds.Count);
        Assert.Equal(ids.OrderBy(i => i.Value).Select(i => i.Value), snapshot.MessageIds.OrderBy(i => i.Value).Select(i => i.Value));
    }

    [Fact]
    public async Task Split_CreatesChildThreadWithCopiedMessageIds()
    {
        var store = new InMemoryThreadStore();
        var op = NewParticipant("operator", "Operator", "ops@example.com");
        var vendor = NewParticipant("vendor", "Vendor", "vendor@example.com");
        var tenant = NewParticipant("tenant", "Tenant", "tenant@example.com");

        var sourceId = await store.CreateAsync(Tenant, new[] { op, vendor, tenant }, MessageVisibility.Public, Ct);
        var m1 = new MessageId(Guid.NewGuid());
        await store.AppendMessageAsync(Tenant, sourceId, m1, Ct);

        var childId = await store.SplitAsync(
            Tenant, sourceId,
            newParticipants: new[] { op, vendor },
            copyForwardMessageIds: new[] { m1 },
            newDefaultVisibility: MessageVisibility.PartyPair,
            Ct);

        var child = await store.GetAsync(Tenant, childId, Ct);
        Assert.NotNull(child);
        Assert.Equal(2, child!.Participants.Count);
        Assert.Equal(MessageVisibility.PartyPair, child.DefaultVisibility);
        Assert.Single(child.MessageIds);
        Assert.Equal(m1, child.MessageIds[0]);

        // Source thread untouched.
        var source = await store.GetAsync(Tenant, sourceId, Ct);
        Assert.Equal(3, source!.Participants.Count);
    }

    [Fact]
    public async Task Split_RejectsUnknownSource()
    {
        var store = new InMemoryThreadStore();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SplitAsync(Tenant, new ThreadId(Guid.NewGuid()),
                new[] { NewParticipant("u1", "A", "a@x") },
                Array.Empty<MessageId>(),
                MessageVisibility.Public, Ct));
    }

    [Fact]
    public async Task ThreeValueVisibility_AllValuesUsableOnSplit()
    {
        var store = new InMemoryThreadStore();
        var p = new[] { NewParticipant("u1", "A", "a@x") };
        var src = await store.CreateAsync(Tenant, p, MessageVisibility.Public, Ct);

        var partyPair = await store.SplitAsync(Tenant, src, p, Array.Empty<MessageId>(), MessageVisibility.PartyPair, Ct);
        var operatorOnly = await store.SplitAsync(Tenant, src, p, Array.Empty<MessageId>(), MessageVisibility.OperatorOnly, Ct);

        Assert.Equal(MessageVisibility.PartyPair, (await store.GetAsync(Tenant, partyPair, Ct))!.DefaultVisibility);
        Assert.Equal(MessageVisibility.OperatorOnly, (await store.GetAsync(Tenant, operatorOnly, Ct))!.DefaultVisibility);
    }

    [Fact]
    public void DI_RegistersInMemoryStubs_AndEntityModule()
    {
        var sp = new ServiceCollection().AddInMemoryMessaging().BuildServiceProvider();

        Assert.IsType<InMemoryThreadStore>(sp.GetRequiredService<IThreadStore>());
        Assert.IsType<InMemoryMessagingGateway>(sp.GetRequiredService<IMessagingGateway>());

        var modules = sp.GetServices<ISunfishEntityModule>().ToList();
        Assert.Contains(modules, m => m is MessagingEntityModule);
    }
}

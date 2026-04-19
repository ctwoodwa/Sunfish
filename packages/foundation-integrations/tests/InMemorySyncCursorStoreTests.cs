using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations;

namespace Sunfish.Foundation.Integrations.Tests;

public class InMemorySyncCursorStoreTests
{
    [Fact]
    public async Task Get_returns_null_when_absent()
    {
        var store = new InMemorySyncCursorStore();

        var cursor = await store.GetAsync("p", new TenantId("t"), "scope");

        Assert.Null(cursor);
    }

    [Fact]
    public async Task Put_then_Get_roundtrip()
    {
        var store = new InMemorySyncCursorStore();
        var original = new SyncCursor
        {
            ProviderKey = "p",
            TenantId = new TenantId("t"),
            Scope = "scope",
            Value = Encoding.UTF8.GetBytes("token-123"),
        };

        await store.PutAsync(original);
        var roundtripped = await store.GetAsync("p", new TenantId("t"), "scope");

        Assert.NotNull(roundtripped);
        Assert.Equal("token-123", Encoding.UTF8.GetString(roundtripped!.Value));
    }

    [Fact]
    public async Task Put_replaces_existing_cursor()
    {
        var store = new InMemorySyncCursorStore();
        await store.PutAsync(new SyncCursor
        {
            ProviderKey = "p",
            Scope = "s",
            Value = Encoding.UTF8.GetBytes("v1"),
        });
        await store.PutAsync(new SyncCursor
        {
            ProviderKey = "p",
            Scope = "s",
            Value = Encoding.UTF8.GetBytes("v2"),
        });

        var cursor = await store.GetAsync("p", tenantId: null, "s");

        Assert.NotNull(cursor);
        Assert.Equal("v2", Encoding.UTF8.GetString(cursor!.Value));
    }

    [Fact]
    public async Task Separate_tenants_do_not_collide()
    {
        var store = new InMemorySyncCursorStore();
        await store.PutAsync(new SyncCursor
        {
            ProviderKey = "p",
            TenantId = new TenantId("a"),
            Scope = "s",
            Value = Encoding.UTF8.GetBytes("a-val"),
        });
        await store.PutAsync(new SyncCursor
        {
            ProviderKey = "p",
            TenantId = new TenantId("b"),
            Scope = "s",
            Value = Encoding.UTF8.GetBytes("b-val"),
        });

        var a = await store.GetAsync("p", new TenantId("a"), "s");
        var b = await store.GetAsync("p", new TenantId("b"), "s");

        Assert.Equal("a-val", Encoding.UTF8.GetString(a!.Value));
        Assert.Equal("b-val", Encoding.UTF8.GetString(b!.Value));
    }
}

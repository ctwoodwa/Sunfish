using Sunfish.Foundation.LocalFirst;

namespace Sunfish.Foundation.LocalFirst.Tests;

public class InMemoryOfflineStoreTests
{
    [Fact]
    public async Task Write_and_read_roundtrip()
    {
        var store = new InMemoryOfflineStore();
        var bytes = Encoding.UTF8.GetBytes("hello");

        await store.WriteAsync("a/1", bytes);
        var result = await store.ReadAsync("a/1");

        Assert.Equal(bytes, result);
    }

    [Fact]
    public async Task Read_returns_null_for_missing_key()
    {
        var store = new InMemoryOfflineStore();

        Assert.Null(await store.ReadAsync("missing"));
    }

    [Fact]
    public async Task Delete_removes_the_key()
    {
        var store = new InMemoryOfflineStore();
        await store.WriteAsync("a/1", Encoding.UTF8.GetBytes("x"));

        Assert.True(await store.DeleteAsync("a/1"));
        Assert.False(await store.DeleteAsync("a/1"));
        Assert.Null(await store.ReadAsync("a/1"));
    }

    [Fact]
    public async Task ListKeysAsync_returns_prefix_matches_in_ordinal_order()
    {
        var store = new InMemoryOfflineStore();
        await store.WriteAsync("a/2", Encoding.UTF8.GetBytes("x"));
        await store.WriteAsync("a/1", Encoding.UTF8.GetBytes("x"));
        await store.WriteAsync("b/1", Encoding.UTF8.GetBytes("x"));

        var keys = await store.ListKeysAsync("a/");

        Assert.Equal(new[] { "a/1", "a/2" }, keys);
    }
}

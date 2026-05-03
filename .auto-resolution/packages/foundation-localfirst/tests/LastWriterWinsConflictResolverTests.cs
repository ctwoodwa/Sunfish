using Sunfish.Foundation.LocalFirst;

namespace Sunfish.Foundation.LocalFirst.Tests;

public class LastWriterWinsConflictResolverTests
{
    [Fact]
    public async Task Later_local_timestamp_wins()
    {
        var resolver = new LastWriterWinsConflictResolver();
        var conflict = new SyncConflict
        {
            Key = "k",
            LocalVersion = Encoding.UTF8.GetBytes("local"),
            RemoteVersion = Encoding.UTF8.GetBytes("remote"),
            LocalModifiedAt = new DateTimeOffset(2026, 4, 19, 10, 0, 0, TimeSpan.Zero),
            RemoteModifiedAt = new DateTimeOffset(2026, 4, 19, 9, 0, 0, TimeSpan.Zero),
        };

        var merged = await resolver.ResolveAsync(conflict);

        Assert.Equal("local", Encoding.UTF8.GetString(merged));
    }

    [Fact]
    public async Task Later_remote_timestamp_wins()
    {
        var resolver = new LastWriterWinsConflictResolver();
        var conflict = new SyncConflict
        {
            Key = "k",
            LocalVersion = Encoding.UTF8.GetBytes("local"),
            RemoteVersion = Encoding.UTF8.GetBytes("remote"),
            LocalModifiedAt = new DateTimeOffset(2026, 4, 19, 9, 0, 0, TimeSpan.Zero),
            RemoteModifiedAt = new DateTimeOffset(2026, 4, 19, 10, 0, 0, TimeSpan.Zero),
        };

        var merged = await resolver.ResolveAsync(conflict);

        Assert.Equal("remote", Encoding.UTF8.GetString(merged));
    }

    [Fact]
    public async Task Missing_timestamps_default_to_remote()
    {
        var resolver = new LastWriterWinsConflictResolver();
        var conflict = new SyncConflict
        {
            Key = "k",
            LocalVersion = Encoding.UTF8.GetBytes("local"),
            RemoteVersion = Encoding.UTF8.GetBytes("remote"),
        };

        var merged = await resolver.ResolveAsync(conflict);

        Assert.Equal("remote", Encoding.UTF8.GetString(merged));
    }
}

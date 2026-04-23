using Sunfish.Kernel.SchemaRegistry.Compaction;

namespace Sunfish.Kernel.SchemaRegistry.Tests;

/// <summary>
/// Contract coverage for <see cref="StreamArchive"/> — archive / lookup / record retrieval / listing.
/// </summary>
public class StreamArchiveTests
{
    [Fact]
    public async Task ArchiveAsync_MarksLogArchived()
    {
        var archive = new StreamArchive();
        var record = new StreamArchiveRecord(
            SourceLogName: "log-2024",
            TargetLogName: "log-2024-compact",
            ArchivedAt: DateTimeOffset.UtcNow,
            TargetSchemaVersion: "v2",
            EventsMigrated: 42);

        await archive.ArchiveAsync(record, CancellationToken.None);

        Assert.True(await archive.IsArchivedAsync("log-2024", CancellationToken.None));
    }

    [Fact]
    public async Task IsArchivedAsync_FalseForUnknownLog()
    {
        var archive = new StreamArchive();

        Assert.False(await archive.IsArchivedAsync("nonexistent", CancellationToken.None));
    }

    [Fact]
    public async Task GetArchiveRecordAsync_ReturnsStoredRecord()
    {
        var archive = new StreamArchive();
        var record = new StreamArchiveRecord(
            SourceLogName: "log-2024",
            TargetLogName: "log-2024-compact",
            ArchivedAt: new DateTimeOffset(2024, 7, 1, 12, 0, 0, TimeSpan.Zero),
            TargetSchemaVersion: "v2",
            EventsMigrated: 42);

        await archive.ArchiveAsync(record, CancellationToken.None);

        var fetched = await archive.GetArchiveRecordAsync("log-2024", CancellationToken.None);
        Assert.NotNull(fetched);
        Assert.Equal(record, fetched);

        var missing = await archive.GetArchiveRecordAsync("unknown", CancellationToken.None);
        Assert.Null(missing);
    }

    [Fact]
    public async Task AllArchives_ListsInInsertionOrder()
    {
        var archive = new StreamArchive();
        var now = DateTimeOffset.UtcNow;
        var a = new StreamArchiveRecord("log-a", "log-a-c", now, "v1", 1);
        var b = new StreamArchiveRecord("log-b", "log-b-c", now, "v1", 2);
        var c = new StreamArchiveRecord("log-c", "log-c-c", now, "v1", 3);

        await archive.ArchiveAsync(a, CancellationToken.None);
        await archive.ArchiveAsync(b, CancellationToken.None);
        await archive.ArchiveAsync(c, CancellationToken.None);

        var all = archive.AllArchives;
        Assert.Equal(3, all.Count);
        Assert.Equal("log-a", all[0].SourceLogName);
        Assert.Equal("log-b", all[1].SourceLogName);
        Assert.Equal("log-c", all[2].SourceLogName);
    }
}

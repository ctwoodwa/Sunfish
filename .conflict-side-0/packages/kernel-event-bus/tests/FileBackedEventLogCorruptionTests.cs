using Microsoft.Extensions.Options;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Events;

namespace Sunfish.Kernel.EventBus.Tests;

/// <summary>
/// File-specific integrity and layout tests that are meaningless for an in-memory log:
/// tail-truncation recovery, cold-start on a missing directory, and size-based rollover.
/// Paper §2.5 ("a partially written entry does not corrupt prior entries").
/// </summary>
public class FileBackedEventLogCorruptionTests : IAsyncDisposable
{
    private readonly string _dir;

    public FileBackedEventLogCorruptionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "sunfish-eventlog-corruption", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public async Task TornTail_IsTruncatedToLastGoodBoundaryOnReopen()
    {
        // Arrange — append three events, close the log, then append garbage bytes to simulate a
        // crash mid-write.
        var opts = Options.Create(new EventLogOptions { Directory = _dir, EpochId = "epoch-0" });
        await using (var log = new FileBackedEventLog(opts))
        {
            await log.AppendAsync(BuildEvent("A"), default);
            await log.AppendAsync(BuildEvent("B"), default);
            await log.AppendAsync(BuildEvent("C"), default);
        }

        var file = Path.Combine(_dir, "events-epoch-0.log");
        var lengthBefore = new FileInfo(file).Length;

        // Corruption: append a torn length prefix (only 2 of 4 bytes) followed by nothing.
        using (var fs = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.None))
        {
            fs.Write(new byte[] { 0xFF, 0xFF });
        }
        Assert.True(new FileInfo(file).Length > lengthBefore);

        // Act — reopen: recovery should truncate back to the last good boundary.
        await using var reopened = new FileBackedEventLog(opts);

        // Assert — three events remain, file is back to its pre-corruption length, and the next
        // append continues the sequence at 4.
        Assert.Equal(3UL, reopened.CurrentSequence);
        Assert.Equal(lengthBefore, new FileInfo(file).Length);

        var entries = await ToListAsync(reopened.ReadAfterAsync(0, default));
        Assert.Equal(3, entries.Count);
        Assert.Equal(new[] { "A", "B", "C" }, entries.Select(e => (string)e.Event.Payload["tag"]!));

        var next = await reopened.AppendAsync(BuildEvent("D"), default);
        Assert.Equal(4UL, next);
    }

    [Fact]
    public async Task TornPayload_IsTruncatedOnReopen()
    {
        // Arrange — two good events.
        var opts = Options.Create(new EventLogOptions { Directory = _dir, EpochId = "epoch-0" });
        await using (var log = new FileBackedEventLog(opts))
        {
            await log.AppendAsync(BuildEvent("A"), default);
            await log.AppendAsync(BuildEvent("B"), default);
        }

        var file = Path.Combine(_dir, "events-epoch-0.log");
        var goodLength = new FileInfo(file).Length;

        // Write a valid-looking length prefix then fewer bytes than it advertises.
        using (var fs = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.None))
        {
            // length-prefix says 500 bytes, we write 10.
            fs.Write(new byte[] { 0x00, 0x00, 0x01, 0xF4 });
            fs.Write(new byte[10]);
        }

        // Act — reopen.
        await using var reopened = new FileBackedEventLog(opts);

        // Assert — truncated back to goodLength; two events intact.
        Assert.Equal(2UL, reopened.CurrentSequence);
        Assert.Equal(goodLength, new FileInfo(file).Length);
    }

    [Fact]
    public async Task MissingDirectoryAndFile_YieldsEmptyLog()
    {
        // Point at a directory that doesn't exist yet — the log should create it and come up empty.
        var scratch = Path.Combine(_dir, "does-not-exist");
        var opts = Options.Create(new EventLogOptions { Directory = scratch, EpochId = "epoch-0" });

        await using var log = new FileBackedEventLog(opts);

        Assert.Equal(0UL, log.CurrentSequence);
        var entries = await ToListAsync(log.ReadAfterAsync(0, default));
        Assert.Empty(entries);

        // And the first append still works.
        var seq = await log.AppendAsync(BuildEvent("first"), default);
        Assert.Equal(1UL, seq);
    }

    [Fact]
    public async Task Rollover_ProducesPartFilesAndPreservesSequence()
    {
        // Set a tiny file-size cap so a few appends force a rollover. Each event is ~100 bytes
        // framed, so 512 bytes gives us several events per file and a handful of parts.
        var opts = Options.Create(new EventLogOptions
        {
            Directory = _dir,
            EpochId = "epoch-0",
            MaxFileSizeBytes = 512,
        });

        const int n = 30;
        await using (var log = new FileBackedEventLog(opts))
        {
            for (var i = 0; i < n; i++)
            {
                await log.AppendAsync(BuildEvent($"e{i:D3}"), default);
            }
        }

        // Multiple part files should exist.
        var files = Directory.GetFiles(_dir, "events-epoch-0*.log");
        Assert.True(files.Length >= 2, $"Expected >= 2 part files, found {files.Length}.");

        // Reopen and verify: sequence is n, all events readable in order across parts, first
        // append after reopen continues at n+1.
        await using var reopened = new FileBackedEventLog(opts);
        Assert.Equal((ulong)n, reopened.CurrentSequence);

        var entries = await ToListAsync(reopened.ReadAfterAsync(0, default));
        Assert.Equal(n, entries.Count);
        for (var i = 0; i < n; i++)
        {
            Assert.Equal((ulong)(i + 1), entries[i].Sequence);
            Assert.Equal($"e{i:D3}", (string)entries[i].Event.Payload["tag"]!);
        }

        var next = await reopened.AppendAsync(BuildEvent("after"), default);
        Assert.Equal((ulong)(n + 1), next);
    }

    [Fact]
    public async Task Events_ArePersistedAcrossProcessLifetime()
    {
        // Smoke test for fsync durability: write, dispose (close files), reopen, events still there.
        var opts = Options.Create(new EventLogOptions { Directory = _dir, EpochId = "epoch-0" });
        await using (var log = new FileBackedEventLog(opts))
        {
            await log.AppendAsync(BuildEvent("persisted-1"), default);
            await log.AppendAsync(BuildEvent("persisted-2"), default);
        }

        await using var reopened = new FileBackedEventLog(opts);
        Assert.Equal(2UL, reopened.CurrentSequence);
        var entries = await ToListAsync(reopened.ReadAfterAsync(0, default));
        Assert.Equal(2, entries.Count);
        Assert.Equal("persisted-1", (string)entries[0].Event.Payload["tag"]!);
        Assert.Equal("persisted-2", (string)entries[1].Event.Payload["tag"]!);
    }

    public async ValueTask DisposeAsync()
    {
        // Give the OS a moment in case a file handle is still being released, then clean up.
        try
        {
            await Task.Yield();
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup — test isolation is via per-instance GUID directory names.
        }
    }

    private static KernelEvent BuildEvent(string tag)
        => new(
            Id: EventId.NewId(),
            EntityId: EntityId.Parse("property:acme/1"),
            Kind: "entity.updated",
            OccurredAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Payload: new Dictionary<string, object?> { ["tag"] = tag });

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var x in source) list.Add(x);
        return list;
    }
}

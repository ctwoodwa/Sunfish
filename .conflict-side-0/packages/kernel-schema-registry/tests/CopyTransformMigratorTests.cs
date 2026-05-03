using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Events;
using Sunfish.Kernel.SchemaRegistry.Lenses;
using Sunfish.Kernel.SchemaRegistry.Migration;

namespace Sunfish.Kernel.SchemaRegistry.Tests;

/// <summary>
/// Contract coverage for <see cref="CopyTransformMigrator"/> — lens-driven
/// transformation, no-path pass-through, identity pass-through, unknown-version
/// warning, and bad-lens-output drop counting.
/// </summary>
public class CopyTransformMigratorTests
{
    private const string Kind = "record.updated";
    private const string V1 = "v1";
    private const string V2 = "v2";

    private static readonly EntityId SampleEntity = new("sunfish", "local", "record-1");

    private static KernelEvent MakeEvent(string schemaVersion, params (string key, object? value)[] extras)
    {
        var payload = new Dictionary<string, object?>
        {
            [CopyTransformMigrator.SchemaVersionPayloadKey] = schemaVersion,
        };
        foreach (var (k, v) in extras) payload[k] = v;
        return new KernelEvent(
            Id: EventId.NewId(),
            EntityId: SampleEntity,
            Kind: Kind,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: payload);
    }

    /// <summary>Lens that maps v1 -> v2 by renaming "name" to "fullName".</summary>
    private sealed class RenameLens : ISchemaLens
    {
        public string EventType => Kind;
        public string FromVersion => V1;
        public string ToVersion => V2;

        public object ForwardTransform(object olderEvent)
        {
            var map = (IReadOnlyDictionary<string, object?>)olderEvent;
            var copy = new Dictionary<string, object?>(map);
            if (copy.Remove("name", out var name))
            {
                copy["fullName"] = name;
            }
            return copy;
        }

        public object BackwardTransform(object newerEvent)
        {
            var map = (IReadOnlyDictionary<string, object?>)newerEvent;
            var copy = new Dictionary<string, object?>(map);
            if (copy.Remove("fullName", out var full))
            {
                copy["name"] = full;
            }
            return copy;
        }
    }

    /// <summary>Lens that returns a non-map object, used to exercise the drop path.</summary>
    private sealed class BadOutputLens : ISchemaLens
    {
        public string EventType => Kind;
        public string FromVersion => V1;
        public string ToVersion => V2;
        public object ForwardTransform(object olderEvent) => "not-a-map";
        public object BackwardTransform(object newerEvent) => "not-a-map";
    }

    [Fact]
    public async Task Migrate_NoLensPath_CopiesThroughWithWarning()
    {
        var source = new InMemoryEventLog();
        var target = new InMemoryEventLog();
        await source.AppendAsync(MakeEvent(V1, ("name", "Chris")), CancellationToken.None);

        var migrator = new CopyTransformMigrator();
        var result = await migrator.MigrateAsync(source, target, new LensGraph(), V2, CancellationToken.None);

        Assert.Equal(1UL, result.EventsRead);
        Assert.Equal(1UL, result.EventsWritten);
        Assert.Equal(0UL, result.EventsDropped);
        Assert.Single(result.Warnings);
        Assert.Contains("no lens path", result.Warnings[0]);
    }

    [Fact]
    public async Task Migrate_WithLens_TransformsAndStampsVersion()
    {
        var source = new InMemoryEventLog();
        var target = new InMemoryEventLog();
        await source.AppendAsync(MakeEvent(V1, ("name", "Chris")), CancellationToken.None);

        var graph = new LensGraph();
        graph.AddLens(new RenameLens());

        var migrator = new CopyTransformMigrator();
        var result = await migrator.MigrateAsync(source, target, graph, V2, CancellationToken.None);

        Assert.Equal(1UL, result.EventsRead);
        Assert.Equal(1UL, result.EventsWritten);
        Assert.Empty(result.Warnings);

        var written = new List<LogEntry>();
        await foreach (var e in target.ReadAfterAsync(0UL, CancellationToken.None))
        {
            written.Add(e);
        }
        Assert.Single(written);
        var payload = written[0].Event.Payload;
        Assert.Equal("Chris", payload["fullName"]);
        Assert.False(payload.ContainsKey("name"));
        Assert.Equal(V2, payload[CopyTransformMigrator.SchemaVersionPayloadKey]);
    }

    [Fact]
    public async Task Migrate_EventAlreadyAtTargetVersion_CopiesThrough()
    {
        var source = new InMemoryEventLog();
        var target = new InMemoryEventLog();
        await source.AppendAsync(MakeEvent(V2, ("fullName", "Chris")), CancellationToken.None);

        var migrator = new CopyTransformMigrator();
        var result = await migrator.MigrateAsync(source, target, new LensGraph(), V2, CancellationToken.None);

        Assert.Equal(1UL, result.EventsRead);
        Assert.Equal(1UL, result.EventsWritten);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Migrate_MissingSchemaVersionKey_WarnsAndCopiesThrough()
    {
        var source = new InMemoryEventLog();
        var target = new InMemoryEventLog();
        // No _schemaVersion key in the payload at all.
        var payload = new Dictionary<string, object?> { ["name"] = "Chris" };
        await source.AppendAsync(
            new KernelEvent(EventId.NewId(), SampleEntity, Kind, DateTimeOffset.UtcNow, payload),
            CancellationToken.None);

        var migrator = new CopyTransformMigrator();
        var result = await migrator.MigrateAsync(source, target, new LensGraph(), V2, CancellationToken.None);

        Assert.Equal(1UL, result.EventsRead);
        Assert.Equal(1UL, result.EventsWritten);
        Assert.Single(result.Warnings);
        Assert.Contains("had no", result.Warnings[0]);
    }

    [Fact]
    public async Task Migrate_BadLensOutput_DropsEvent()
    {
        var source = new InMemoryEventLog();
        var target = new InMemoryEventLog();
        await source.AppendAsync(MakeEvent(V1, ("name", "Chris")), CancellationToken.None);

        var graph = new LensGraph();
        graph.AddLens(new BadOutputLens());

        var migrator = new CopyTransformMigrator();
        var result = await migrator.MigrateAsync(source, target, graph, V2, CancellationToken.None);

        Assert.Equal(1UL, result.EventsRead);
        Assert.Equal(0UL, result.EventsWritten);
        Assert.Equal(1UL, result.EventsDropped);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public async Task Migrate_EventCountRoundtripsWhenAllPathed()
    {
        var source = new InMemoryEventLog();
        var target = new InMemoryEventLog();
        for (var i = 0; i < 5; i++)
        {
            await source.AppendAsync(MakeEvent(V1, ("name", $"Chris-{i}")), CancellationToken.None);
        }

        var graph = new LensGraph();
        graph.AddLens(new RenameLens());

        var migrator = new CopyTransformMigrator();
        var result = await migrator.MigrateAsync(source, target, graph, V2, CancellationToken.None);

        Assert.Equal(5UL, result.EventsRead);
        Assert.Equal(5UL, result.EventsWritten);
        Assert.Equal(0UL, result.EventsDropped);
        Assert.Empty(result.Warnings);
    }
}

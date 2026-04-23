namespace Sunfish.Kernel.Runtime.Tests;

/// <summary>Test plugin. Records OnLoad/OnUnload into a shared log.</summary>
internal sealed class TestPlugin : ILocalNodePlugin
{
    private readonly List<string> _log;
    private readonly Action<IPluginContext>? _onLoad;

    public TestPlugin(string id, string[] deps, List<string> log, Action<IPluginContext>? onLoad = null)
    {
        Id = id;
        Dependencies = deps;
        _log = log;
        _onLoad = onLoad;
    }

    public string Id { get; }
    public string Version => "1.0.0";
    public IReadOnlyCollection<string> Dependencies { get; }

    public int LoadCount { get; private set; }
    public int UnloadCount { get; private set; }

    public Task OnLoadAsync(IPluginContext context, CancellationToken ct)
    {
        LoadCount++;
        _log.Add($"load:{Id}");
        _onLoad?.Invoke(context);
        return Task.CompletedTask;
    }

    public Task OnUnloadAsync(CancellationToken ct)
    {
        UnloadCount++;
        _log.Add($"unload:{Id}");
        return Task.CompletedTask;
    }
}

internal sealed record FakeStream(string StreamId, string SchemaVersion, IReadOnlyCollection<string> EventTypes,
    IReadOnlyCollection<string> BucketContributions) : IStreamDefinition;

internal sealed class FakeProjection : IProjectionBuilder
{
    public FakeProjection(string projectionId, string sourceStreamId)
    {
        ProjectionId = projectionId;
        SourceStreamId = sourceStreamId;
    }
    public string ProjectionId { get; }
    public string SourceStreamId { get; }
    public Task RebuildAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed record FakeSchemaVersion(string EventType, string Version,
    IReadOnlyCollection<string> SupportedVersions) : ISchemaVersion
{
    public object? Upcast(object olderEvent, string fromVersion) => olderEvent;
}

internal sealed record FakeUiBlock(string BlockId, string DisplayName, string Category,
    IReadOnlyCollection<string> RequiredStreamIds,
    IReadOnlyCollection<string> RequiredAttestations) : IUiBlockManifest;

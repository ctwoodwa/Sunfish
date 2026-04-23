using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Kernel.Runtime.Tests;

public sealed class PluginRegistryTests
{
    private static PluginRegistry NewRegistry()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        return new PluginRegistry(services);
    }

    [Fact]
    public async Task Loads_single_plugin_with_no_dependencies()
    {
        var log = new List<string>();
        var plugin = new TestPlugin("a", Array.Empty<string>(), log);
        var registry = NewRegistry();

        await registry.LoadAllAsync(new[] { plugin }, CancellationToken.None);

        Assert.Single(registry.LoadedPlugins);
        Assert.Equal(1, plugin.LoadCount);
        Assert.Equal(new[] { "load:a" }, log);
    }

    [Fact]
    public async Task Loads_two_plugins_in_dependency_order()
    {
        var log = new List<string>();
        var parent = new TestPlugin("a", Array.Empty<string>(), log);
        var child = new TestPlugin("b", new[] { "a" }, log);
        var registry = NewRegistry();

        // Deliberately pass child first — topological sort must reorder.
        await registry.LoadAllAsync(new ILocalNodePlugin[] { child, parent }, CancellationToken.None);

        Assert.Equal(new[] { "load:a", "load:b" }, log);
        Assert.Equal(new[] { "a", "b" }, registry.LoadedPlugins.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task Throws_PluginCyclicDependencyException_on_cycle()
    {
        var log = new List<string>();
        var a = new TestPlugin("a", new[] { "b" }, log);
        var b = new TestPlugin("b", new[] { "a" }, log);
        var registry = NewRegistry();

        var ex = await Assert.ThrowsAsync<PluginCyclicDependencyException>(
            () => registry.LoadAllAsync(new ILocalNodePlugin[] { a, b }, CancellationToken.None));

        Assert.Contains("a", ex.CycleMembers);
        Assert.Contains("b", ex.CycleMembers);
    }

    [Fact]
    public async Task Throws_when_dependency_not_present()
    {
        var log = new List<string>();
        var plugin = new TestPlugin("a", new[] { "missing" }, log);
        var registry = NewRegistry();

        var ex = await Assert.ThrowsAsync<PluginMissingDependencyException>(
            () => registry.LoadAllAsync(new[] { plugin }, CancellationToken.None));

        Assert.Equal("a", ex.PluginId);
        Assert.Equal("missing", ex.MissingDependencyId);
    }

    [Fact]
    public async Task Calls_OnLoadAsync_in_topological_order()
    {
        var log = new List<string>();
        var a = new TestPlugin("a", Array.Empty<string>(), log);
        var b = new TestPlugin("b", new[] { "a" }, log);
        var c = new TestPlugin("c", new[] { "b" }, log);
        var registry = NewRegistry();

        await registry.LoadAllAsync(new ILocalNodePlugin[] { c, a, b }, CancellationToken.None);

        Assert.Equal(new[] { "load:a", "load:b", "load:c" }, log);
    }

    [Fact]
    public async Task Calls_OnUnloadAsync_in_reverse_topological_order()
    {
        var log = new List<string>();
        var a = new TestPlugin("a", Array.Empty<string>(), log);
        var b = new TestPlugin("b", new[] { "a" }, log);
        var c = new TestPlugin("c", new[] { "b" }, log);
        var registry = NewRegistry();

        await registry.LoadAllAsync(new ILocalNodePlugin[] { a, b, c }, CancellationToken.None);
        log.Clear();

        await registry.UnloadAllAsync(CancellationToken.None);

        Assert.Equal(new[] { "unload:c", "unload:b", "unload:a" }, log);
    }

    [Fact]
    public async Task Registers_stream_definitions_through_context()
    {
        var log = new List<string>();
        var stream = new FakeStream("s1", "1.0.0", new[] { "Created" }, new[] { "bucket-a" });
        var plugin = new TestPlugin("a", Array.Empty<string>(), log,
            onLoad: ctx => ctx.RegisterStream(stream));
        var registry = NewRegistry();

        await registry.LoadAllAsync(new[] { plugin }, CancellationToken.None);

        Assert.True(registry.Streams.ContainsKey("a"));
        Assert.Single(registry.Streams["a"]);
        Assert.Equal("s1", registry.Streams["a"].Single().StreamId);
    }

    [Fact]
    public async Task Registers_projections_through_context()
    {
        var log = new List<string>();
        var projection = new FakeProjection("p1", "s1");
        var plugin = new TestPlugin("a", Array.Empty<string>(), log,
            onLoad: ctx => ctx.RegisterProjection(projection));
        var registry = NewRegistry();

        await registry.LoadAllAsync(new[] { plugin }, CancellationToken.None);

        Assert.Single(registry.Projections["a"]);
        Assert.Equal("p1", registry.Projections["a"].Single().ProjectionId);
    }

    [Fact]
    public async Task Registers_schema_versions_and_ui_blocks_through_context()
    {
        var log = new List<string>();
        var schema = new FakeSchemaVersion("EvtA", "2.0.0", new[] { "1.0.0" });
        var block = new FakeUiBlock("block-a", "A", "Testing",
            new[] { "s1" }, Array.Empty<string>());
        var plugin = new TestPlugin("a", Array.Empty<string>(), log,
            onLoad: ctx =>
            {
                ctx.RegisterSchemaVersion(schema);
                ctx.RegisterUiBlock(block);
            });
        var registry = NewRegistry();

        await registry.LoadAllAsync(new[] { plugin }, CancellationToken.None);

        Assert.Single(registry.SchemaVersions["a"]);
        Assert.Single(registry.UiBlocks["a"]);
        Assert.Equal("EvtA", registry.SchemaVersions["a"].Single().EventType);
        Assert.Equal("block-a", registry.UiBlocks["a"].Single().BlockId);
    }

    [Fact]
    public async Task Unloads_idempotently()
    {
        var log = new List<string>();
        var plugin = new TestPlugin("a", Array.Empty<string>(), log);
        var registry = NewRegistry();

        await registry.LoadAllAsync(new[] { plugin }, CancellationToken.None);
        await registry.UnloadAllAsync(CancellationToken.None);
        await registry.UnloadAllAsync(CancellationToken.None); // second call no-op
        await registry.UnloadAllAsync(CancellationToken.None); // third call no-op

        Assert.Empty(registry.LoadedPlugins);
        Assert.Equal(1, plugin.UnloadCount);
    }

    [Fact]
    public async Task LoadedPlugins_reflects_current_state()
    {
        var log = new List<string>();
        var a = new TestPlugin("a", Array.Empty<string>(), log);
        var b = new TestPlugin("b", new[] { "a" }, log);
        var registry = NewRegistry();

        Assert.Empty(registry.LoadedPlugins);

        await registry.LoadAllAsync(new ILocalNodePlugin[] { a, b }, CancellationToken.None);
        Assert.Equal(2, registry.LoadedPlugins.Count);
        Assert.Equal(new[] { "a", "b" }, registry.LoadedPlugins.Select(p => p.Id).ToArray());

        await registry.UnloadAllAsync(CancellationToken.None);
        Assert.Empty(registry.LoadedPlugins);
    }

    [Fact]
    public async Task Rejects_second_load_while_loaded()
    {
        var log = new List<string>();
        var plugin = new TestPlugin("a", Array.Empty<string>(), log);
        var registry = NewRegistry();

        await registry.LoadAllAsync(new[] { plugin }, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.LoadAllAsync(new[] { plugin }, CancellationToken.None));
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sunfish.Kernel.Runtime;

/// <summary>
/// Default <see cref="IPluginRegistry"/>. Performs Kahn-style topological sort
/// on plugin dependencies, loads in order, and unloads in reverse. The
/// collecting <see cref="IPluginContext"/> given to each plugin captures the
/// plugin's extension-point registrations for later kernel use.
/// </summary>
public sealed class PluginRegistry : IPluginRegistry
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PluginRegistry> _logger;
    private readonly List<ILocalNodePlugin> _loaded = new();
    private readonly List<CollectingPluginContext> _contexts = new();

    /// <summary>Create a registry bound to the host <paramref name="services"/> container.</summary>
    /// <param name="services">Host DI container. Passed through to plugins via <see cref="IPluginContext.Services"/>.</param>
    /// <param name="logger">Optional logger. Falls back to <see cref="NullLogger{T}"/> when null.</param>
    public PluginRegistry(IServiceProvider services, ILogger<PluginRegistry>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _logger = logger ?? NullLogger<PluginRegistry>.Instance;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ILocalNodePlugin> LoadedPlugins => _loaded;

    /// <summary>
    /// Collected stream definitions across all loaded plugins, keyed by the
    /// plugin ID that registered them. Exposed for kernel subsystems that
    /// aggregate cross-plugin registrations (stream topology, projection
    /// scheduler, etc.).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<IStreamDefinition>> Streams =>
        _contexts.ToDictionary(c => c.PluginId, c => (IReadOnlyCollection<IStreamDefinition>)c.Streams);

    /// <summary>Collected projections across all loaded plugins, keyed by plugin ID.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<IProjectionBuilder>> Projections =>
        _contexts.ToDictionary(c => c.PluginId, c => (IReadOnlyCollection<IProjectionBuilder>)c.Projections);

    /// <summary>Collected schema versions across all loaded plugins, keyed by plugin ID.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<ISchemaVersion>> SchemaVersions =>
        _contexts.ToDictionary(c => c.PluginId, c => (IReadOnlyCollection<ISchemaVersion>)c.SchemaVersions);

    /// <summary>Collected UI block manifests across all loaded plugins, keyed by plugin ID.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<IUiBlockManifest>> UiBlocks =>
        _contexts.ToDictionary(c => c.PluginId, c => (IReadOnlyCollection<IUiBlockManifest>)c.UiBlocks);

    /// <inheritdoc />
    public async Task LoadAllAsync(IEnumerable<ILocalNodePlugin> plugins, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        if (_loaded.Count > 0)
        {
            throw new InvalidOperationException(
                "PluginRegistry.LoadAllAsync was called while plugins are still loaded. "
                + "Call UnloadAllAsync first.");
        }

        var input = plugins.ToList();
        var ordered = TopologicalSort(input);

        foreach (var plugin in ordered)
        {
            ct.ThrowIfCancellationRequested();
            var context = new CollectingPluginContext(plugin.Id, _services);
            _contexts.Add(context);
            _logger.LogDebug("Loading plugin {PluginId} v{Version}", plugin.Id, plugin.Version);
            await plugin.OnLoadAsync(context, ct).ConfigureAwait(false);
            _loaded.Add(plugin);
        }
    }

    /// <inheritdoc />
    public async Task UnloadAllAsync(CancellationToken ct)
    {
        // Reverse order is the load order reversed — safe for plugins that
        // released their dependencies' services.
        for (var i = _loaded.Count - 1; i >= 0; i--)
        {
            var plugin = _loaded[i];
            _logger.LogDebug("Unloading plugin {PluginId}", plugin.Id);
            try
            {
                await plugin.OnUnloadAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Swallow per-plugin unload failures to keep the teardown
                // idempotent — log and move on. The kernel treats unload as
                // best-effort release; partial failures do not block
                // subsequent restarts.
                _logger.LogWarning(ex, "Plugin {PluginId} failed OnUnloadAsync", plugin.Id);
            }
        }

        _loaded.Clear();
        _contexts.Clear();
    }

    // Kahn's algorithm. We intentionally process ready plugins in a stable
    // order (by Id) so that equal-depth dependencies load deterministically
    // — important for test-observability and deterministic replay.
    private static List<ILocalNodePlugin> TopologicalSort(IReadOnlyList<ILocalNodePlugin> input)
    {
        var byId = new Dictionary<string, ILocalNodePlugin>(StringComparer.Ordinal);
        foreach (var plugin in input)
        {
            if (plugin is null)
            {
                throw new ArgumentException("Plugin list contains a null entry.", nameof(input));
            }
            byId[plugin.Id] = plugin;
        }

        // Validate declared dependencies exist in the input set.
        foreach (var plugin in input)
        {
            foreach (var dep in plugin.Dependencies ?? Array.Empty<string>())
            {
                if (!byId.ContainsKey(dep))
                {
                    throw new PluginMissingDependencyException(plugin.Id, dep);
                }
            }
        }

        // In-degree = count of dependencies still unresolved.
        var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var plugin in input)
        {
            inDegree[plugin.Id] = plugin.Dependencies?.Count ?? 0;
        }

        // Reverse adjacency: for each plugin X, list of plugins that depend on X.
        var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var plugin in input)
        {
            foreach (var dep in plugin.Dependencies ?? Array.Empty<string>())
            {
                if (!dependents.TryGetValue(dep, out var list))
                {
                    list = new List<string>();
                    dependents[dep] = list;
                }
                list.Add(plugin.Id);
            }
        }

        // Use a sorted set so equal-depth nodes come out in Id order.
        var ready = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (id, degree) in inDegree)
        {
            if (degree == 0)
            {
                ready.Add(id);
            }
        }

        var result = new List<ILocalNodePlugin>(input.Count);
        while (ready.Count > 0)
        {
            var next = ready.Min!;
            ready.Remove(next);
            result.Add(byId[next]);

            if (dependents.TryGetValue(next, out var deps))
            {
                foreach (var dependentId in deps)
                {
                    inDegree[dependentId]--;
                    if (inDegree[dependentId] == 0)
                    {
                        ready.Add(dependentId);
                    }
                }
            }
        }

        if (result.Count != input.Count)
        {
            // Plugins still with in-degree > 0 participate in a cycle.
            var cycleMembers = inDegree
                .Where(kvp => kvp.Value > 0)
                .Select(kvp => kvp.Key)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            throw new PluginCyclicDependencyException(cycleMembers);
        }

        return result;
    }

    // Collects per-plugin registrations. Plugins never see each other's
    // contexts — one context per plugin keeps registrations traceable to
    // their origin.
    private sealed class CollectingPluginContext : IPluginContext
    {
        public string PluginId { get; }
        public IServiceProvider Services { get; }
        public List<IStreamDefinition> Streams { get; } = new();
        public List<IProjectionBuilder> Projections { get; } = new();
        public List<ISchemaVersion> SchemaVersions { get; } = new();
        public List<IUiBlockManifest> UiBlocks { get; } = new();

        public CollectingPluginContext(string pluginId, IServiceProvider services)
        {
            PluginId = pluginId;
            Services = services;
        }

        public void RegisterStream(IStreamDefinition stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            Streams.Add(stream);
        }

        public void RegisterProjection(IProjectionBuilder projection)
        {
            ArgumentNullException.ThrowIfNull(projection);
            Projections.Add(projection);
        }

        public void RegisterSchemaVersion(ISchemaVersion schema)
        {
            ArgumentNullException.ThrowIfNull(schema);
            SchemaVersions.Add(schema);
        }

        public void RegisterUiBlock(IUiBlockManifest block)
        {
            ArgumentNullException.ThrowIfNull(block);
            UiBlocks.Add(block);
        }
    }
}

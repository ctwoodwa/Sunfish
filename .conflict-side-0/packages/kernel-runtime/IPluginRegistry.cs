namespace Sunfish.Kernel.Runtime;

/// <summary>
/// Loads plugins in dependency order and keeps track of the live set.
/// Load is a one-shot operation — the registry rejects a second
/// <see cref="LoadAllAsync"/> call until <see cref="UnloadAllAsync"/> returns.
/// </summary>
public interface IPluginRegistry
{
    /// <summary>
    /// Topologically sort <paramref name="plugins"/> by their declared
    /// dependencies and call <see cref="ILocalNodePlugin.OnLoadAsync"/> on
    /// each in order. Throws <see cref="PluginCyclicDependencyException"/>
    /// on cycles and <see cref="PluginMissingDependencyException"/> when a
    /// declared dependency is not in the input set.
    /// </summary>
    Task LoadAllAsync(IEnumerable<ILocalNodePlugin> plugins, CancellationToken ct);

    /// <summary>
    /// Call <see cref="ILocalNodePlugin.OnUnloadAsync"/> on all loaded
    /// plugins in reverse topological order. Idempotent: repeated calls are
    /// no-ops once the registry is empty.
    /// </summary>
    Task UnloadAllAsync(CancellationToken ct);

    /// <summary>The plugins currently loaded, in load (topological) order.</summary>
    IReadOnlyCollection<ILocalNodePlugin> LoadedPlugins { get; }
}

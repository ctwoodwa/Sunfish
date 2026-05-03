namespace Sunfish.Kernel.Runtime;

/// <summary>
/// Primary plugin contract. All domain plugins implement this interface.
/// Discovered at startup via the plugin registry (see <see cref="IPluginRegistry"/>).
/// </summary>
/// <remarks>
/// Paper §5.3 extension-point contract. The kernel cannot distinguish a
/// first-party plugin from a compatibility adapter — both satisfy the same
/// contract. Plugins declare their dependencies by plugin <see cref="Id"/>;
/// the registry loads them in topological order.
/// </remarks>
public interface ILocalNodePlugin
{
    /// <summary>Unique plugin identifier (reverse-DNS style, e.g., "com.sunfish.blocks.accounting").</summary>
    string Id { get; }

    /// <summary>Semantic version of this plugin.</summary>
    string Version { get; }

    /// <summary>Other plugin IDs this plugin requires to be loaded first.</summary>
    IReadOnlyCollection<string> Dependencies { get; }

    /// <summary>Called during kernel startup. Register services, projections, stream definitions here.</summary>
    /// <param name="context">Plugin context used to register streams, projections, schema versions, and UI blocks.</param>
    /// <param name="ct">Cancellation token observed during load.</param>
    Task OnLoadAsync(IPluginContext context, CancellationToken ct);

    /// <summary>Called during kernel shutdown. Release resources.</summary>
    /// <param name="ct">Cancellation token observed during unload.</param>
    Task OnUnloadAsync(CancellationToken ct);
}

namespace Sunfish.Kernel.Runtime;

/// <summary>
/// Thrown by <see cref="IPluginRegistry.LoadAllAsync"/> when the plugin
/// dependency graph contains a cycle.
/// </summary>
public sealed class PluginCyclicDependencyException : Exception
{
    /// <summary>Plugin IDs participating in the cycle (in discovery order).</summary>
    public IReadOnlyCollection<string> CycleMembers { get; }

    /// <summary>Creates a new <see cref="PluginCyclicDependencyException"/>.</summary>
    /// <param name="cycleMembers">Plugin IDs participating in the cycle.</param>
    public PluginCyclicDependencyException(IReadOnlyCollection<string> cycleMembers)
        : base(BuildMessage(cycleMembers))
    {
        CycleMembers = cycleMembers;
    }

    private static string BuildMessage(IReadOnlyCollection<string> cycleMembers)
    {
        ArgumentNullException.ThrowIfNull(cycleMembers);
        return $"Plugin dependency cycle detected among: {string.Join(" -> ", cycleMembers)}.";
    }
}

/// <summary>
/// Thrown by <see cref="IPluginRegistry.LoadAllAsync"/> when a plugin declares
/// a dependency on a plugin ID that is not present in the registry input.
/// </summary>
public sealed class PluginMissingDependencyException : Exception
{
    /// <summary>ID of the plugin that declared the missing dependency.</summary>
    public string PluginId { get; }

    /// <summary>ID of the missing dependency.</summary>
    public string MissingDependencyId { get; }

    /// <summary>Creates a new <see cref="PluginMissingDependencyException"/>.</summary>
    /// <param name="pluginId">ID of the plugin that declared the missing dependency.</param>
    /// <param name="missingDependencyId">ID of the missing dependency.</param>
    public PluginMissingDependencyException(string pluginId, string missingDependencyId)
        : base($"Plugin '{pluginId}' declares a dependency on '{missingDependencyId}', which is not registered.")
    {
        PluginId = pluginId;
        MissingDependencyId = missingDependencyId;
    }
}

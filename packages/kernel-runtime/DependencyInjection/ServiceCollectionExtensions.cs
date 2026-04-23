using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.Kernel.Runtime.DependencyInjection;

/// <summary>
/// DI extensions for registering the Sunfish kernel runtime (paper §5.1).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPluginRegistry"/> and <see cref="INodeHost"/> as
    /// singletons backed by <see cref="PluginRegistry"/> and
    /// <see cref="NodeHost"/>. Uses <c>TryAddSingleton</c> so prior
    /// registrations (test doubles, alternative hosts) win.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishKernelRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IPluginRegistry, PluginRegistry>();
        services.TryAddSingleton<INodeHost, NodeHost>();
        return services;
    }
}

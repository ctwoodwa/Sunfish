using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Sunfish.Kernel.Crdt.Backends;

namespace Sunfish.Kernel.Crdt.DependencyInjection;

/// <summary>
/// DI extensions for registering the Sunfish CRDT engine (paper §2.2, §9; ADR 0028).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="ICrdtEngine"/> as a singleton, backed by the default
    /// backend for this build. Uses <c>TryAddSingleton</c> so a prior registration
    /// (for example a Loro or Yjs/yrs backend in a future wave) wins.
    /// </summary>
    /// <remarks>
    /// The current default backend is the provisional in-memory stub
    /// (<see cref="StubCrdtEngine"/>). See ADR 0028 and the file banner in
    /// <c>Backends/StubCrdtEngine.cs</c> for the spike outcome and the path to a
    /// production backend.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishCrdtEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ICrdtEngine, StubCrdtEngine>();
        return services;
    }
}

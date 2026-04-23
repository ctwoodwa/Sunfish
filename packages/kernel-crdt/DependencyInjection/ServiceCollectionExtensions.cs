using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Sunfish.Kernel.Crdt.Backends;
using Sunfish.Kernel.Crdt.GarbageCollection;
using Sunfish.Kernel.Crdt.SnapshotScheduling;

namespace Sunfish.Kernel.Crdt.DependencyInjection;

/// <summary>
/// DI extensions for registering the Sunfish CRDT engine (paper §2.2, §9; ADR 0028).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="ICrdtEngine"/> as a singleton, backed by the best available
    /// backend for this build. Uses <c>TryAddSingleton</c> so a prior explicit
    /// registration wins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// As of the 2026-04-22 spike re-validation, the default is
    /// <see cref="YDotNetCrdtEngine"/> (Yjs/yrs via YDotNet 0.6.0). See
    /// <c>packages/kernel-crdt/SPIKE-OUTCOME.md</c> and ADR 0028 for why YDotNet
    /// was selected over Loro for the real backend, and why the stub is retained.
    /// </para>
    /// <para>
    /// To opt out of the native backend (for example in a unit-test host where
    /// the YDotNet native binaries are not available), call
    /// <see cref="AddSunfishCrdtEngineStub"/> before any other registration, or
    /// call <c>services.AddSingleton&lt;ICrdtEngine, StubCrdtEngine&gt;()</c> directly.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishCrdtEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ICrdtEngine, YDotNetCrdtEngine>();
        return services;
    }

    /// <summary>
    /// Register the YDotNet (Yjs/yrs) CRDT backend explicitly. Use this when you
    /// want to be explicit in host wiring rather than relying on the default.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishCrdtEngineYDotNet(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ICrdtEngine, YDotNetCrdtEngine>();
        return services;
    }

    /// <summary>
    /// Register the in-memory stub backend. Retained as a test harness and as an
    /// escape hatch for environments where YDotNet's native binaries cannot be
    /// loaded. See the banner in <c>Backends/StubCrdtEngine.cs</c> — the stub is
    /// NOT a production CRDT.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishCrdtEngineStub(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ICrdtEngine, StubCrdtEngine>();
        return services;
    }

    /// <summary>
    /// Register the paper §9 CRDT growth-mitigation services: shallow-snapshot manager,
    /// default (conservative) policy, and document garbage collector facade.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers as singletons:
    /// <list type="bullet">
    ///   <item><see cref="IShallowSnapshotPolicy"/> → <see cref="NeverShallowSnapshotPolicy"/>
    ///     (paper §9: "default policy is conservative: full history is retained").</item>
    ///   <item><see cref="IShallowSnapshotManager"/> → <see cref="ShallowSnapshotManager"/>.</item>
    ///   <item><see cref="IDocumentGarbageCollector"/> → <see cref="DocumentGarbageCollector"/>.</item>
    /// </list>
    /// Uses <c>TryAddSingleton</c> throughout so a host can override any of these with a
    /// per-document-type policy or a differently-wired manager before calling this
    /// extension.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishCrdtGarbageCollection(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IShallowSnapshotPolicy, NeverShallowSnapshotPolicy>();
        services.TryAddSingleton<IShallowSnapshotManager, ShallowSnapshotManager>();
        services.TryAddSingleton<IDocumentGarbageCollector, DocumentGarbageCollector>();
        return services;
    }
}

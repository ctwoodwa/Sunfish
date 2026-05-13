using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// DI registration for the foundation-tier Engine Room substrate (ADR 0079).
/// Per cohort <c>AddSunfishX()</c> convention. Phase 1 binds options
/// surface only — concrete <see cref="IEngineRoomDataProvider"/> +
/// <see cref="IEngineRoomCommandService"/> implementations land in Phase 2
/// (<c>blocks-engine-room</c>).
/// </summary>
public static class EngineRoomServiceCollectionExtensions
{
    /// <summary>
    /// Register the Engine Room substrate. Phase 1 ships interface
    /// definitions only; hosts MUST register concrete bindings via Phase 2
    /// or their own DI composition before invoking the surfaces.
    /// </summary>
    /// <param name="services">DI container.</param>
    public static IServiceCollection AddSunfishEngineRoom(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Phase 1 ships contract surface only. Phase 2 (`blocks-engine-room`)
        // adds concrete IEngineRoomDataProvider + IEngineRoomCommandService
        // implementations + OTel meter registration. Hosts MUST register
        // concrete bindings before invoking the surfaces — DI resolution at
        // runtime throws if a Phase 2 implementation is missing.

        return services;
    }

    /// <summary>
    /// Registers a host-supplied <see cref="IDocumentQuarantineStore"/>
    /// implementation per ADR 0079 §2. Hosts MUST call this before invoking
    /// <see cref="IEngineRoomCommandService"/> — the command service throws
    /// at resolution time if no store is registered.
    /// </summary>
    /// <typeparam name="TImpl">The concrete store implementation.</typeparam>
    public static IServiceCollection AddEngineRoomQuarantineStore<TImpl>(
        this IServiceCollection services)
        where TImpl : class, IDocumentQuarantineStore
    {
        ArgumentNullException.ThrowIfNull(services);
        // Scoped lifetime: store implementations typically hold a DbContext or per-request state;
        // Singleton would capture a Scoped dep (captive dep bug). TryAdd semantics: if the host
        // pre-registered an IDocumentQuarantineStore before calling this method, the host-supplied
        // registration wins regardless of its lifetime — the host is responsible for choosing Scoped.
        services.TryAddScoped<IDocumentQuarantineStore, TImpl>();
        return services;
    }
}

using System;
using Microsoft.Extensions.DependencyInjection;

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
}

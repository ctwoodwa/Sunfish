using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.EngineRoom;

namespace Sunfish.Blocks.EngineRoom;

/// <summary>
/// DI registration for the block-tier Engine Room reference implementations
/// per W#50 Phase 2a + Phase 2b. Per cohort <c>AddSunfishXDefaults()</c>
/// convention.
/// </summary>
public static class EngineRoomServiceCollectionExtensions
{
    /// <summary>
    /// Registers the reference <see cref="IEngineRoomDataProvider"/>
    /// (<see cref="DefaultEngineRoomDataProvider"/>) and the reference
    /// <see cref="IEngineRoomCommandService"/>
    /// (<see cref="DefaultEngineRoomCommandService"/>). Binds
    /// <see cref="EngineRoomOptions"/>. Hosts that run a real sync
    /// daemon or CRDT document store also register
    /// <see cref="ISyncDaemonHealthSource"/> and/or
    /// <see cref="ICrdtDocumentRegistry"/>; if they don't, the data
    /// provider returns sensible defaults
    /// (<see cref="SyncDaemonStatus.Unavailable"/> + zeros for the
    /// daemon snapshot; an empty stream for CRDT growth metrics).
    /// </summary>
    /// <remarks>
    /// Hosts MUST also register an <see cref="IDocumentQuarantineStore"/>
    /// implementation via
    /// <see cref="Foundation.EngineRoom.EngineRoomServiceCollectionExtensions.AddEngineRoomQuarantineStore{TImpl}"/>
    /// before invoking <see cref="IEngineRoomCommandService"/> — the
    /// command service will throw at DI resolution time if no store is
    /// registered.
    /// </remarks>
    public static IServiceCollection AddSunfishEngineRoomDefaults(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<EngineRoomOptions>();
        services.TryAddSingleton<IEngineRoomDataProvider, DefaultEngineRoomDataProvider>();
        services.TryAddSingleton<IEngineRoomCommandService, DefaultEngineRoomCommandService>();
        return services;
    }
}

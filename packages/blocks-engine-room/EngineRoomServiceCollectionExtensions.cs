using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.EngineRoom;

namespace Sunfish.Blocks.EngineRoom;

/// <summary>
/// DI registration for the block-tier Engine Room reference data
/// provider per W#50 Phase 2a. Per cohort
/// <c>AddSunfishXDefaults()</c> convention.
/// </summary>
public static class EngineRoomServiceCollectionExtensions
{
    /// <summary>
    /// Registers the reference <see cref="IEngineRoomDataProvider"/>
    /// implementation (<see cref="DefaultEngineRoomDataProvider"/>) and
    /// binds <see cref="EngineRoomOptions"/>. Hosts that run a real sync
    /// daemon or CRDT document store also register
    /// <see cref="ISyncDaemonHealthSource"/> + / or
    /// <see cref="ICrdtDocumentRegistry"/>; if they don't, the data
    /// provider returns sensible defaults
    /// (<see cref="SyncDaemonStatus.Unavailable"/> + zeros for the
    /// daemon snapshot; an empty stream for CRDT growth metrics).
    /// </summary>
    /// <remarks>
    /// Phase 2b will add <c>DefaultEngineRoomCommandService</c> +
    /// <c>IDocumentQuarantineStore</c> registrations once the EOOW +
    /// IPermissionResolver wiring lands.
    /// </remarks>
    public static IServiceCollection AddSunfishEngineRoomDefaults(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<EngineRoomOptions>();
        services.TryAddSingleton<IEngineRoomDataProvider, DefaultEngineRoomDataProvider>();
        return services;
    }
}

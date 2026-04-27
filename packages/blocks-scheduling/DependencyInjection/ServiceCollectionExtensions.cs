using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.Blocks.Scheduling.DependencyInjection;

/// <summary>
/// DI extensions for the <c>Sunfish.Blocks.Scheduling</c> reservation
/// coordinator. Registers <see cref="IScheduleReservationCoordinator"/> as a
/// singleton — the in-memory reservation index is process-wide, and the
/// underlying <see cref="Sunfish.Kernel.Lease.ILeaseCoordinator"/> singleton
/// (registered by <c>AddSunfishKernelLease()</c>) carries the cluster-wide
/// CP coordination per paper §6.3.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="IScheduleReservationCoordinator"/>.
    /// </summary>
    /// <remarks>
    /// Depends on <see cref="Sunfish.Kernel.Lease.ILeaseCoordinator"/> being
    /// registered — call <c>AddSunfishKernelLease()</c> first (or another
    /// registration that supplies the interface). Without that dependency
    /// the resolver throws at first use, which is exactly what we want for
    /// the D6 wiring contract: a block that ships the reservation coordinator
    /// without a lease coordinator is a configuration error, not a degraded
    /// fallback.
    /// </remarks>
    public static IServiceCollection AddSunfishBlocksScheduling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IScheduleReservationCoordinator, ScheduleReservationCoordinator>();
        return services;
    }
}

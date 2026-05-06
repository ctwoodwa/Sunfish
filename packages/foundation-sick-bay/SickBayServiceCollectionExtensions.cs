using System;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// DI registration for the foundation-tier Sick Bay substrate (ADR 0082).
/// Per cohort <c>AddSunfishX()</c> convention.
/// </summary>
public static class SickBayServiceCollectionExtensions
{
    /// <summary>
    /// Register the Sick Bay substrate. Phase 1 ships
    /// <see cref="SickBayOptions"/> binding only — concrete
    /// <see cref="ISickBayDataProvider"/>, <see cref="ISickBayCommandService"/>,
    /// <see cref="IMedevacService"/>, <see cref="IFirstAidSurface"/>,
    /// <see cref="IStretcherBearerPolicy"/>, and
    /// <see cref="IKeyRotationScheduler"/> implementations land in Phase
    /// 2 / Phase 3b (<c>blocks-sick-bay</c>).
    /// </summary>
    public static IServiceCollection AddSunfishSickBay(
        this IServiceCollection services,
        Action<SickBayOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<SickBayOptions>().Configure(opts => configure?.Invoke(opts));

        // Phase 1 ships interface registrations only — implementations
        // land in Phase 2 (`blocks-sick-bay` data provider + Stretcher
        // Bearer policy + First-Aid surface + Noop key-rotation
        // scheduler) and Phase 3b (command service + medevac service +
        // real key-rotation scheduler).

        return services;
    }
}

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// DI registration for the foundation-tier Quarterdeck substrate
/// (ADR 0080). Per cohort <c>AddSunfishX()</c> convention. Phase 1
/// binds <see cref="QuarterdeckOptions"/> only; concrete
/// <see cref="IQuarterdeckDataProvider"/> +
/// <see cref="IQuarterdeckCommandService"/> implementations land in
/// Phase 2 (<c>blocks-quarterdeck</c>).
/// </summary>
public static class QuarterdeckServiceCollectionExtensions
{
    /// <summary>
    /// Register the Quarterdeck substrate. Phase 1 ships interface
    /// surface + options binding; hosts MUST register concrete
    /// <see cref="IQuarterdeckDataProvider"/> +
    /// <see cref="IQuarterdeckCommandService"/> bindings via Phase 2
    /// or their own DI composition before invoking the surfaces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>§5.1 startup ShipAction registration check + §5.3 source
    /// uniqueness check</b> live with the Phase 2
    /// <c>DefaultQuarterdeckDataProvider</c> registration — they
    /// require concrete provider + source instances to validate
    /// against. Phase 1 does not register an
    /// <see cref="Microsoft.Extensions.Hosting.IHostedService"/> for
    /// these checks; Phase 2 wires them in alongside the data
    /// provider.
    /// </para>
    /// </remarks>
    /// <param name="services">DI container.</param>
    /// <param name="configure">
    /// Optional configuration callback invoked against a fresh
    /// options instance seeded with canonical defaults.
    /// </param>
    public static IServiceCollection AddSunfishQuarterdeck(
        this IServiceCollection services,
        Action<QuarterdeckOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<QuarterdeckOptions>().Configure(opts =>
        {
            // Defaults already applied via property initializers; the
            // configure callback (when supplied) overrides individual
            // fields.
            configure?.Invoke(opts);
        });

        return services;
    }
}

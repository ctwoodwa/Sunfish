using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.SickBay;

namespace Sunfish.Blocks.SickBay;

/// <summary>
/// DI registration for the block-tier Sick Bay reference
/// implementations per W#54 Phase 2, Phase 2b, and Phase 3b. Per cohort
/// <c>AddSunfishXDefaults()</c> convention (W#48 P1.5 precedent):
/// <c>foundation-sick-bay</c> registers contracts + options binding;
/// <c>blocks-sick-bay</c> registers default implementations on top.
/// </summary>
public static class SickBayServiceCollectionExtensions
{
    /// <summary>
    /// Registers reference implementations for the Sick Bay
    /// aggregation surface (ADR 0082 / W#54 Phase 2 + Phase 2b + Phase 3b):
    /// <list type="bullet">
    /// <item><description><see cref="ISickBayDataProvider"/> →
    /// <see cref="SickBayDataProvider"/> (k=3-anonymized pharmacy +
    /// Mission Envelope atmosphere projection).</description></item>
    /// <item><description><see cref="IFirstAidSurface"/> →
    /// <see cref="DefaultFirstAidSurface"/> (built-in hint library
    /// for pharmacy / lab / atmosphere keys).</description></item>
    /// <item><description><see cref="IStretcherBearerPolicy"/> →
    /// <see cref="DefaultStretcherBearerPolicy"/> (returns the four
    /// canonical <see cref="StretcherBearerRole"/> values
    /// unconditionally for v1).</description></item>
    /// <item><description><see cref="IKeyRotationScheduler"/> →
    /// <see cref="NoopKeyRotationScheduler"/> — registered ONLY when
    /// <see cref="SickBayOptions.RegisterNoopKeyRotationScheduler"/>
    /// is <c>true</c> (opt-in per ADR 0082-A1.4 §Trust posture).
    /// </description></item>
    /// <item><description><see cref="ISickBayCommandService"/> →
    /// <see cref="SickBayCommandService"/> (audit + scheduler).
    /// </description></item>
    /// <item><description><see cref="IMedevacService"/> →
    /// <see cref="MedevacServiceImpl"/> (six-state machine; four-eyes
    /// invariant; audit-before-operation per ADR 0082 §2).
    /// </description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Hosts MUST also call <c>AddSunfishSickBay()</c> (from
    /// <c>foundation-sick-bay</c>) to bind <see cref="SickBayOptions"/>;
    /// this method assumes the foundation-tier registration has been
    /// applied first. Registrations use <c>TryAddSingleton</c> so
    /// host overrides remain authoritative.
    /// </remarks>
    public static IServiceCollection AddSunfishSickBayDefaults(
        this IServiceCollection services,
        Action<SickBayOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Bind configure lambda into IOptions<SickBayOptions> so runtime callers
        // reading IOptions<SickBayOptions>.Value get the full configured state —
        // not just a local probe copy (W#54 P2b council Blocking B1).
        if (configure is not null)
            services.Configure<SickBayOptions>(configure);

        // Defensive: idempotent options registration so callers who skip
        // AddSunfishSickBay() get a working IOptions<SickBayOptions> rather
        // than a silent blank-snapshot foot-gun (W#54 P2 council Major).
        services.AddOptions<SickBayOptions>();
        services.TryAddSingleton<ISickBayDataProvider, SickBayDataProvider>();
        services.TryAddSingleton<IFirstAidSurface, DefaultFirstAidSurface>();
        services.TryAddSingleton<IStretcherBearerPolicy, DefaultStretcherBearerPolicy>();

        // Probe a separate local instance for the registration-time branching
        // decision; the DI-bound IOptions<SickBayOptions> is the runtime authority.
        var probe = new SickBayOptions();
        configure?.Invoke(probe);
        if (probe.RegisterNoopKeyRotationScheduler)
        {
            services.TryAddSingleton<IKeyRotationScheduler, NoopKeyRotationScheduler>();
        }

        // Phase 3b: command service + medevac state machine.
        services.TryAddSingleton<ISickBayCommandService, SickBayCommandService>();
        services.TryAddSingleton<IMedevacService, MedevacServiceImpl>();

        return services;
    }
}

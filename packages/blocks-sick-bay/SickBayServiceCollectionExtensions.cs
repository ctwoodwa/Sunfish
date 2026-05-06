using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.SickBay;

namespace Sunfish.Blocks.SickBay;

/// <summary>
/// DI registration for the block-tier Sick Bay reference
/// implementations per W#54 Phase 2. Per cohort
/// <c>AddSunfishXDefaults()</c> convention (W#48 P1.5 precedent):
/// <c>foundation-sick-bay</c> registers contracts + options binding;
/// <c>blocks-sick-bay</c> registers default implementations on top.
/// </summary>
public static class SickBayServiceCollectionExtensions
{
    /// <summary>
    /// Registers reference implementations for the Sick Bay
    /// aggregation surface (ADR 0082 / W#54 Phase 2):
    /// <list type="bullet">
    /// <item><description><see cref="ISickBayDataProvider"/> →
    /// <see cref="SickBayDataProvider"/> (k=3-anonymized projection
    /// over <see cref="SickBayOptions"/>).</description></item>
    /// <item><description><see cref="IFirstAidSurface"/> →
    /// <see cref="DefaultFirstAidSurface"/> (built-in hint library
    /// for pharmacy / lab / atmosphere keys).</description></item>
    /// <item><description><see cref="IStretcherBearerPolicy"/> →
    /// <see cref="DefaultStretcherBearerPolicy"/> (returns the four
    /// canonical <see cref="StretcherBearerRole"/> values
    /// unconditionally for v1).</description></item>
    /// <item><description><see cref="IKeyRotationScheduler"/> →
    /// <see cref="NoopKeyRotationScheduler"/> (Phase 2 stub; Phase 3b
    /// wires the W#32 / ADR 0046-A2 rotation pipeline).</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Hosts MUST also call <c>AddSunfishSickBay()</c> (from
    /// <c>foundation-sick-bay</c>) to bind <see cref="SickBayOptions"/>;
    /// this method assumes the foundation-tier registration has been
    /// applied first. Registrations use <c>TryAddSingleton</c> so
    /// host overrides remain authoritative.
    /// </remarks>
    public static IServiceCollection AddSunfishSickBayDefaults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ISickBayDataProvider, SickBayDataProvider>();
        services.TryAddSingleton<IFirstAidSurface, DefaultFirstAidSurface>();
        services.TryAddSingleton<IStretcherBearerPolicy, DefaultStretcherBearerPolicy>();
        services.TryAddSingleton<IKeyRotationScheduler, NoopKeyRotationScheduler>();
        return services;
    }
}

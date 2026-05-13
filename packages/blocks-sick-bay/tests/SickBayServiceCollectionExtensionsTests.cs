using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.SickBay;
using System;
using Xunit;

namespace Sunfish.Blocks.SickBay.Tests;

public class SickBayServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSunfishSickBayDefaults_RegistersThreeReferenceImplementations()
    {
        var services = new ServiceCollection();
        services.AddSunfishSickBay();
        services.AddSunfishSickBayDefaults();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ISickBayDataProvider>());
        Assert.IsType<SickBayDataProvider>(provider.GetService<ISickBayDataProvider>());
        Assert.IsType<DefaultFirstAidSurface>(provider.GetService<IFirstAidSurface>());
        Assert.IsType<DefaultStretcherBearerPolicy>(provider.GetService<IStretcherBearerPolicy>());
    }

    [Fact]
    public void AddSunfishSickBayDefaults_DoesNotRegisterNoopKeyRotationScheduler_ByDefault()
    {
        // ADR 0082-A1.4 §Trust posture: hosts MUST NOT register the Noop scheduler
        // unless they explicitly opt in — prevents silent green-wash in prod envs
        // that surface a "rotation triggered" affordance.
        var services = new ServiceCollection();
        services.AddSunfishSickBay();
        services.AddSunfishSickBayDefaults();

        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IKeyRotationScheduler>());
    }

    [Fact]
    public void AddSunfishSickBayDefaults_RegistersNoopKeyRotationScheduler_WhenOptInFlagSet()
    {
        var services = new ServiceCollection();
        services.AddSunfishSickBay();
        services.AddSunfishSickBayDefaults(opts => opts.RegisterNoopKeyRotationScheduler = true);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<NoopKeyRotationScheduler>(provider.GetService<IKeyRotationScheduler>());
    }

    [Fact]
    public void AddSunfishSickBayDefaults_Configure_PropagatesFallbackPollingIntervalToIOptions()
    {
        // Regression for W#54 P2b council Blocking B1: configure lambda MUST bind
        // to IOptions<SickBayOptions> via services.Configure(), not only to a local probe.
        var expected = TimeSpan.FromSeconds(30);
        var services = new ServiceCollection();
        services.AddSunfishSickBay();
        services.AddSunfishSickBayDefaults(opts => opts.FallbackPollingInterval = expected);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SickBayOptions>>();
        Assert.Equal(expected, options.Value.FallbackPollingInterval);
    }

    [Fact]
    public void AddSunfishSickBayDefaults_DoesNotOverrideExistingRegistrations()
    {
        var customStretcher = new CustomPolicy();
        var services = new ServiceCollection();
        services.AddSunfishSickBay();
        services.AddSingleton<IStretcherBearerPolicy>(customStretcher);
        services.AddSunfishSickBayDefaults();

        using var provider = services.BuildServiceProvider();
        Assert.Same(customStretcher, provider.GetService<IStretcherBearerPolicy>());
    }

    private sealed class CustomPolicy : IStretcherBearerPolicy
    {
        public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<StretcherBearerRole>>
            GetEligibleRespondersAsync(
                Sunfish.Foundation.Assets.Common.TenantId tenant,
                System.Threading.CancellationToken ct = default)
            => System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IReadOnlyList<StretcherBearerRole>>(
                System.Array.Empty<StretcherBearerRole>());
    }
}

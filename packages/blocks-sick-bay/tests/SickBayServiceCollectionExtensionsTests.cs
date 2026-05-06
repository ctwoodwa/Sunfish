using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.SickBay;
using Xunit;

namespace Sunfish.Blocks.SickBay.Tests;

public class SickBayServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSunfishSickBayDefaults_RegistersAllFourReferenceImplementations()
    {
        var services = new ServiceCollection();
        services.AddSunfishSickBay();
        services.AddSunfishSickBayDefaults();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ISickBayDataProvider>());
        Assert.IsType<SickBayDataProvider>(provider.GetService<ISickBayDataProvider>());
        Assert.IsType<DefaultFirstAidSurface>(provider.GetService<IFirstAidSurface>());
        Assert.IsType<DefaultStretcherBearerPolicy>(provider.GetService<IStretcherBearerPolicy>());
        Assert.IsType<NoopKeyRotationScheduler>(provider.GetService<IKeyRotationScheduler>());
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

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MissionSpace.DependencyInjection;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInMemoryMissionSpace_AuditDisabled_RegistersAllProbesAndForceEnableSurface()
    {
        var services = new ServiceCollection();
        services.AddInMemoryMissionSpace();
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IDimensionProbe<HardwareCapabilities>>());
        Assert.NotNull(sp.GetRequiredService<IDimensionProbe<RuntimeCapabilities>>());
        Assert.NotNull(sp.GetRequiredService<IDimensionProbe<NetworkCapabilities>>());
        Assert.NotNull(sp.GetRequiredService<IDimensionProbe<UserCapabilities>>());
        Assert.NotNull(sp.GetRequiredService<IDimensionProbe<EditionCapabilities>>());
        Assert.NotNull(sp.GetRequiredService<IDimensionProbe<RegulatoryCapabilities>>());
        Assert.NotNull(sp.GetRequiredService<IDimensionProbe<TrustAnchorCapabilities>>());
        Assert.NotNull(sp.GetRequiredService<IDimensionProbe<SyncStateSnapshot>>());
        Assert.NotNull(sp.GetRequiredService<IDimensionProbe<VersionVectorSnapshot>>());
        Assert.NotNull(sp.GetRequiredService<IDimensionProbe<FormFactorSnapshot>>());
        Assert.NotNull(sp.GetRequiredService<IFeatureForceEnableSurface>());
    }

    [Fact]
    public void AddInMemoryMissionSpace_AuditEnabled_WiresAuditAndSigner()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());

        var services = new ServiceCollection();
        services.AddSingleton<IAuditTrail>(trail);
        services.AddSingleton<IOperationSigner>(signer);
        services.AddInMemoryMissionSpace(new TenantId("tenant-x"));
        var sp = services.BuildServiceProvider();

        var surface = sp.GetRequiredService<IFeatureForceEnableSurface>();
        Assert.NotNull(surface);
        Assert.IsType<DefaultFeatureForceEnableSurface>(surface);
    }

    [Fact]
    public void AddInMemoryMissionSpace_AuditEnabled_DefaultTenant_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddInMemoryMissionSpace(default));
    }

    [Fact]
    public void AddInMemoryMissionSpace_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddInMemoryMissionSpace());
        Assert.Throws<ArgumentNullException>(() => services!.AddInMemoryMissionSpace(new TenantId("t")));
    }

    [Fact]
    public async Task AddInMemoryMissionSpace_HardwareProbe_ReturnsHealthySnapshot()
    {
        var services = new ServiceCollection();
        services.AddInMemoryMissionSpace();
        var sp = services.BuildServiceProvider();
        var hw = await sp.GetRequiredService<IDimensionProbe<HardwareCapabilities>>().ProbeAsync();
        Assert.Equal(ProbeStatus.Healthy, hw.ProbeStatus);
    }

    [Fact]
    public void AddInMemoryMissionSpace_TryAddSingleton_DoesNotOverrideExisting()
    {
        var services = new ServiceCollection();
        var custom = Substitute.For<IDimensionProbe<HardwareCapabilities>>();
        services.AddSingleton(custom);
        services.AddInMemoryMissionSpace();
        var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IDimensionProbe<HardwareCapabilities>>();
        Assert.Same(custom, resolved);
    }
}

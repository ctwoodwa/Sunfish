using System;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MissionSpace.DependencyInjection;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class RequirementsDiExtensionTests
{
    [Fact]
    public void AddInMemoryMissionSpace_AuditDisabled_RegistersRequirementsSurfaces()
    {
        var services = new ServiceCollection();
        services.AddInMemoryMissionSpace();
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IMinimumSpecResolver>());
        Assert.IsType<DefaultMinimumSpecResolver>(sp.GetRequiredService<IMinimumSpecResolver>());

        Assert.NotNull(sp.GetRequiredService<IInstallForceEnableSurface>());
        Assert.IsType<DefaultInstallForceEnableSurface>(sp.GetRequiredService<IInstallForceEnableSurface>());
    }

    [Fact]
    public void AddInMemoryMissionSpace_AuditEnabled_RegistersRequirementsSurfaces()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());

        var services = new ServiceCollection();
        services.AddSingleton<IAuditTrail>(trail);
        services.AddSingleton<IOperationSigner>(signer);
        services.AddInMemoryMissionSpace(new TenantId("tenant-x"));
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IMinimumSpecResolver>());
        Assert.NotNull(sp.GetRequiredService<IInstallForceEnableSurface>());
    }

    [Fact]
    public void AddInMemoryMissionSpace_TryAddSingleton_HostOverridePreserved()
    {
        var services = new ServiceCollection();
        var custom = Substitute.For<IMinimumSpecResolver>();
        services.AddSingleton(custom);
        services.AddInMemoryMissionSpace();
        var sp = services.BuildServiceProvider();
        Assert.Same(custom, sp.GetRequiredService<IMinimumSpecResolver>());
    }
}

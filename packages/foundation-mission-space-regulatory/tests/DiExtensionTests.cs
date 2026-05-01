using System;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MissionSpace.Regulatory.Bridge;
using Sunfish.Foundation.MissionSpace.Regulatory.DependencyInjection;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Tests;

public sealed class DiExtensionTests
{
    [Fact]
    public void AddInMemoryRegulatoryPolicy_AuditDisabled_RegistersAllSurfaces()
    {
        var services = new ServiceCollection();
        services.AddInMemoryRegulatoryPolicy();
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IPolicyRuleSource>());
        Assert.NotNull(sp.GetRequiredService<IDataResidencyConstraintSource>());
        Assert.NotNull(sp.GetRequiredService<ISanctionsListSource>());
        Assert.NotNull(sp.GetRequiredService<ICompositeJurisdictionProbe>());
        Assert.NotNull(sp.GetRequiredService<IPolicyEvaluator>());
        Assert.NotNull(sp.GetRequiredService<IDataResidencyEnforcer>());
        Assert.NotNull(sp.GetRequiredService<ISanctionsScreener>());
        Assert.NotNull(sp.GetRequiredService<IDataResidencyEnforcerMiddleware>());
    }

    [Fact]
    public void AddInMemoryRegulatoryPolicy_AuditEnabled_WiresAuditAndSigner()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());

        var services = new ServiceCollection();
        services.AddSingleton<IAuditTrail>(trail);
        services.AddSingleton<IOperationSigner>(signer);
        services.AddInMemoryRegulatoryPolicy(new TenantId("tenant-x"));
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IPolicyEvaluator>());
        Assert.NotNull(sp.GetRequiredService<ISanctionsScreener>());
        Assert.NotNull(sp.GetRequiredService<IDataResidencyEnforcer>());
        Assert.NotNull(sp.GetRequiredService<ICompositeJurisdictionProbe>());
        Assert.NotNull(sp.GetRequiredService<IDataResidencyEnforcerMiddleware>());
    }

    [Fact]
    public void AddInMemoryRegulatoryPolicy_AuditEnabled_DefaultTenant_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddInMemoryRegulatoryPolicy(default));
    }

    [Fact]
    public void AddInMemoryRegulatoryPolicy_AuditEnabled_EmptyOperator_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddInMemoryRegulatoryPolicy(new TenantId("t"), ScreeningPolicy.Default, ""));
    }

    [Fact]
    public void AddInMemoryRegulatoryPolicy_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddInMemoryRegulatoryPolicy());
        Assert.Throws<ArgumentNullException>(() => services!.AddInMemoryRegulatoryPolicy(new TenantId("t")));
    }

    [Fact]
    public void AddInMemoryRegulatoryPolicy_TryAddSingleton_DoesNotOverrideExisting()
    {
        var services = new ServiceCollection();
        var custom = Substitute.For<IPolicyRuleSource>();
        services.AddSingleton(custom);
        services.AddInMemoryRegulatoryPolicy();
        var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IPolicyRuleSource>();
        Assert.Same(custom, resolved);
    }

    [Fact]
    public void AddInMemoryRegulatoryPolicy_AuditEnabled_AdvisoryOnly_Resolves()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());

        var services = new ServiceCollection();
        services.AddSingleton<IAuditTrail>(trail);
        services.AddSingleton<IOperationSigner>(signer);
        services.AddInMemoryRegulatoryPolicy(
            new TenantId("tenant-x"),
            ScreeningPolicy.AdvisoryOnly,
            "ops-admin");
        var sp = services.BuildServiceProvider();

        var screener = sp.GetRequiredService<ISanctionsScreener>();
        Assert.Equal(ScreeningPolicy.AdvisoryOnly, screener.Policy);
    }
}

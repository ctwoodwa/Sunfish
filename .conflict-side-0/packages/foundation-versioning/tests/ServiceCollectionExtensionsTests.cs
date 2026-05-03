using System;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Versioning.DependencyInjection;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Versioning.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private static readonly TenantId TenantA = new("tenant-a");

    [Fact]
    public void AddInMemoryVersioning_RegistersAllThreeContracts()
    {
        var services = new ServiceCollection().AddInMemoryVersioning();
        var sp = services.BuildServiceProvider();

        Assert.IsType<DefaultCompatibilityRelation>(sp.GetRequiredService<ICompatibilityRelation>());
        Assert.IsType<InMemoryVersionVectorExchange>(sp.GetRequiredService<IVersionVectorExchange>());
        Assert.IsType<InMemoryVersionVectorIncompatibility>(sp.GetRequiredService<IVersionVectorIncompatibility>());
    }

    [Fact]
    public void AddInMemoryVersioning_RegistersSingletons()
    {
        var sp = new ServiceCollection().AddInMemoryVersioning().BuildServiceProvider();

        var relation1 = sp.GetRequiredService<ICompatibilityRelation>();
        var relation2 = sp.GetRequiredService<ICompatibilityRelation>();
        var exchange1 = sp.GetRequiredService<IVersionVectorExchange>();
        var exchange2 = sp.GetRequiredService<IVersionVectorExchange>();
        var incompat1 = sp.GetRequiredService<IVersionVectorIncompatibility>();
        var incompat2 = sp.GetRequiredService<IVersionVectorIncompatibility>();

        Assert.Same(relation1, relation2);
        Assert.Same(exchange1, exchange2);
        Assert.Same(incompat1, incompat2);
    }

    [Fact]
    public void AddInMemoryVersioning_AuditEnabled_RegistersAllThreeContracts()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IAuditTrail>());
        services.AddSingleton<IOperationSigner>(new Ed25519Signer(KeyPair.Generate()));
        services.AddInMemoryVersioning(TenantA);
        var sp = services.BuildServiceProvider();

        Assert.IsType<DefaultCompatibilityRelation>(sp.GetRequiredService<ICompatibilityRelation>());
        Assert.IsType<InMemoryVersionVectorExchange>(sp.GetRequiredService<IVersionVectorExchange>());
        Assert.IsType<InMemoryVersionVectorIncompatibility>(sp.GetRequiredService<IVersionVectorIncompatibility>());
    }

    [Fact]
    public void AddInMemoryVersioning_AuditEnabled_RejectsDefaultTenantId()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddInMemoryVersioning(default));
    }

    [Fact]
    public void AddInMemoryVersioning_AuditEnabled_FailsLazilyWithoutAuditTrail()
    {
        var services = new ServiceCollection().AddInMemoryVersioning(TenantA);
        var sp = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IVersionVectorIncompatibility>());
    }

    [Fact]
    public void AddInMemoryVersioning_TryAddSemantics_DoesNotOverrideExistingRegistration()
    {
        var customRelation = Substitute.For<ICompatibilityRelation>();
        var services = new ServiceCollection();
        services.AddSingleton(customRelation);
        services.AddInMemoryVersioning();
        var sp = services.BuildServiceProvider();

        Assert.Same(customRelation, sp.GetRequiredService<ICompatibilityRelation>());
    }

    [Fact]
    public void AddInMemoryVersioning_NullServices_Throws()
    {
        IServiceCollection? services = null;

        Assert.Throws<ArgumentNullException>(() => services!.AddInMemoryVersioning());
        Assert.Throws<ArgumentNullException>(() => services!.AddInMemoryVersioning(TenantA));
    }
}

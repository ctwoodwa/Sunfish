using System;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Migration.DependencyInjection;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Migration.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private static readonly TenantId TenantA = new("tenant-a");

    [Fact]
    public void AddInMemoryMigration_RegistersBothContracts()
    {
        var sp = new ServiceCollection().AddInMemoryMigration().BuildServiceProvider();

        Assert.IsType<InMemorySequestrationStore>(sp.GetRequiredService<ISequestrationStore>());
        Assert.IsType<InMemoryFormFactorMigrationService>(sp.GetRequiredService<IFormFactorMigrationService>());
    }

    [Fact]
    public void AddInMemoryMigration_RegistersSingletons()
    {
        var sp = new ServiceCollection().AddInMemoryMigration().BuildServiceProvider();

        Assert.Same(sp.GetRequiredService<ISequestrationStore>(), sp.GetRequiredService<ISequestrationStore>());
        Assert.Same(sp.GetRequiredService<IFormFactorMigrationService>(), sp.GetRequiredService<IFormFactorMigrationService>());
    }

    [Fact]
    public void AddInMemoryMigration_AuditEnabled_ResolvesService()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IAuditTrail>());
        services.AddSingleton<IOperationSigner>(new Ed25519Signer(KeyPair.Generate()));
        services.AddInMemoryMigration(TenantA);
        var sp = services.BuildServiceProvider();

        Assert.IsType<InMemoryFormFactorMigrationService>(sp.GetRequiredService<IFormFactorMigrationService>());
    }

    [Fact]
    public void AddInMemoryMigration_AuditEnabled_RejectsDefaultTenantId()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddInMemoryMigration(default));
    }

    [Fact]
    public void AddInMemoryMigration_AuditEnabled_FailsLazilyWithoutAuditTrail()
    {
        var services = new ServiceCollection().AddInMemoryMigration(TenantA);
        var sp = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IFormFactorMigrationService>());
    }

    [Fact]
    public void AddInMemoryMigration_TryAddSemantics_DoesNotOverrideExistingRegistration()
    {
        var customStore = Substitute.For<ISequestrationStore>();
        var services = new ServiceCollection();
        services.AddSingleton(customStore);
        services.AddInMemoryMigration();
        var sp = services.BuildServiceProvider();

        Assert.Same(customStore, sp.GetRequiredService<ISequestrationStore>());
    }

    [Fact]
    public void AddInMemoryMigration_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddInMemoryMigration());
        Assert.Throws<ArgumentNullException>(() => services!.AddInMemoryMigration(TenantA));
    }
}

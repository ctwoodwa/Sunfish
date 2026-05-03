using System;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport.DependencyInjection;
using Sunfish.Foundation.Transport.Relay;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Transport.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly BridgeRelayOptions Relay = new() { RelayUrl = new Uri("wss://127.0.0.1:8443/sync") };

    [Fact]
    public void AddSunfishTransport_WithoutTier3_FailsLazilyAtFirstResolution()
    {
        // Selector resolves on first GetService; no Tier-3 → ArgumentException
        // is the contract from DefaultTransportSelector's constructor.
        var services = new ServiceCollection();
        services.AddSunfishTransport();
        var sp = services.BuildServiceProvider();

        Assert.Throws<ArgumentException>(() => sp.GetRequiredService<ITransportSelector>());
    }

    [Fact]
    public void AddBridgeRelay_RegistersAsTier3()
    {
        var services = new ServiceCollection().AddBridgeRelay(Relay);
        var sp = services.BuildServiceProvider();

        var transports = sp.GetServices<IPeerTransport>();
        Assert.Single(transports);
        Assert.Equal(TransportTier.ManagedRelay, Assert.Single(transports).Tier);
    }

    [Fact]
    public void AddSunfishTransport_WithBridgeRelay_ResolvesSelector()
    {
        var services = new ServiceCollection()
            .AddBridgeRelay(Relay)
            .AddSunfishTransport();
        var sp = services.BuildServiceProvider();

        var selector = sp.GetRequiredService<ITransportSelector>();
        Assert.IsType<DefaultTransportSelector>(selector);
    }

    [Fact]
    public void AddSunfishTransport_RegistersAsSingleton()
    {
        var services = new ServiceCollection()
            .AddBridgeRelay(Relay)
            .AddSunfishTransport();
        var sp = services.BuildServiceProvider();

        var first = sp.GetRequiredService<ITransportSelector>();
        var second = sp.GetRequiredService<ITransportSelector>();
        Assert.Same(first, second);
    }

    [Fact]
    public void AddSunfishTransport_AuditEnabled_ResolvesSelector()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IAuditTrail>());
        services.AddSingleton<IOperationSigner>(new Ed25519Signer(KeyPair.Generate()));
        services.AddBridgeRelay(Relay);
        services.AddSunfishTransport(TenantA);
        var sp = services.BuildServiceProvider();

        var selector = sp.GetRequiredService<ITransportSelector>();
        Assert.IsType<DefaultTransportSelector>(selector);
    }

    [Fact]
    public void AddSunfishTransport_AuditEnabled_RejectsDefaultTenantId()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddSunfishTransport(default));
    }

    [Fact]
    public void AddSunfishTransport_AuditEnabled_FailsLazilyWithoutAuditTrail()
    {
        var services = new ServiceCollection()
            .AddBridgeRelay(Relay)
            .AddSunfishTransport(TenantA);
        var sp = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<ITransportSelector>());
    }

    [Fact]
    public void AddSunfishTransport_TryAddSemantics_DoesNotOverrideExistingRegistration()
    {
        var custom = Substitute.For<ITransportSelector>();
        var services = new ServiceCollection();
        services.AddSingleton(custom);
        services.AddSunfishTransport(); // should NOT override
        var sp = services.BuildServiceProvider();

        Assert.Same(custom, sp.GetRequiredService<ITransportSelector>());
    }

    [Fact]
    public void AddSunfishTransport_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddSunfishTransport());
        Assert.Throws<ArgumentNullException>(() => services!.AddSunfishTransport(TenantA));
    }

    [Fact]
    public void AddBridgeRelay_NullArgs_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddBridgeRelay(Relay));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddBridgeRelay(null!));
    }
}

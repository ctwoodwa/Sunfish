using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Bridge.Subscription.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private static readonly TenantId TenantA = new("tenant-a");

    private static IServiceCollection NewServicesWithHttp()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new HttpClient());
        return services;
    }

    [Fact]
    public void AddInMemoryBridgeSubscription_RegistersAllSubstrateContracts()
    {
        var sp = NewServicesWithHttp().AddInMemoryBridgeSubscription().BuildServiceProvider();

        Assert.IsType<HmacSha256EventSigner>(sp.GetRequiredService<IEventSigner>());
        Assert.IsType<InMemoryIdempotencyCache>(sp.GetRequiredService<IIdempotencyCache>());
        Assert.IsType<InMemorySharedSecretStore>(sp.GetRequiredService<ISharedSecretStore>());
        Assert.IsType<InMemoryDeadLetterQueue>(sp.GetRequiredService<IDeadLetterQueue>());
        Assert.IsType<DefaultWebhookRegistrationService>(sp.GetRequiredService<IWebhookRegistrationService>());
        Assert.IsType<DefaultTrustChainResolver>(sp.GetRequiredService<ITrustChainResolver>());
        Assert.IsType<ReplayWindow>(sp.GetRequiredService<ReplayWindow>());
        Assert.IsType<DefaultWebhookDeliveryService>(sp.GetRequiredService<IWebhookDeliveryService>());
        Assert.IsType<InMemoryBridgeSubscriptionEventHandler>(sp.GetRequiredService<IBridgeSubscriptionEventHandler>());
    }

    [Fact]
    public void AddInMemoryBridgeSubscription_RegistersAsSingletons()
    {
        var sp = NewServicesWithHttp().AddInMemoryBridgeSubscription().BuildServiceProvider();
        var first = sp.GetRequiredService<IBridgeSubscriptionEventHandler>();
        var second = sp.GetRequiredService<IBridgeSubscriptionEventHandler>();
        Assert.Same(first, second);
    }

    [Fact]
    public void AddInMemoryBridgeSubscription_AuditEnabled_ResolvesHandler()
    {
        var services = NewServicesWithHttp();
        services.AddSingleton(Substitute.For<IAuditTrail>());
        services.AddSingleton<IOperationSigner>(new Ed25519Signer(KeyPair.Generate()));
        services.AddInMemoryBridgeSubscription(TenantA);
        var sp = services.BuildServiceProvider();

        Assert.IsType<InMemoryBridgeSubscriptionEventHandler>(sp.GetRequiredService<IBridgeSubscriptionEventHandler>());
    }

    [Fact]
    public void AddInMemoryBridgeSubscription_AuditEnabled_RejectsDefaultTenantId()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddInMemoryBridgeSubscription(default));
    }

    [Fact]
    public void AddInMemoryBridgeSubscription_AuditEnabled_FailsLazilyWithoutAuditTrail()
    {
        var sp = NewServicesWithHttp().AddInMemoryBridgeSubscription(TenantA).BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IBridgeSubscriptionEventHandler>());
    }

    [Fact]
    public void AddInMemoryBridgeSubscription_TryAddSemantics_DoesNotOverrideExisting()
    {
        var customSigner = Substitute.For<IEventSigner>();
        var services = NewServicesWithHttp();
        services.AddSingleton(customSigner);
        services.AddInMemoryBridgeSubscription();
        var sp = services.BuildServiceProvider();

        Assert.Same(customSigner, sp.GetRequiredService<IEventSigner>());
    }

    [Fact]
    public void AddInMemoryBridgeSubscription_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddInMemoryBridgeSubscription());
        Assert.Throws<ArgumentNullException>(() => services!.AddInMemoryBridgeSubscription(TenantA));
    }

    [Fact]
    public void AddInMemoryBridgeSubscription_OptionalEditionUpdater_ResolvesWithoutIt()
    {
        // The handler accepts a null IEditionCacheUpdater — host wires
        // their own when ADR 0062 EditionCapabilities consumer is in play.
        var sp = NewServicesWithHttp().AddInMemoryBridgeSubscription().BuildServiceProvider();
        var handler = sp.GetRequiredService<IBridgeSubscriptionEventHandler>();
        Assert.NotNull(handler);
    }
}

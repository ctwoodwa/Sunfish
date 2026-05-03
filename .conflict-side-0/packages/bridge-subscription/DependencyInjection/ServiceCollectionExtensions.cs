using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Bridge.Subscription.DependencyInjection;

/// <summary>
/// DI registration for the Bridge → Anchor subscription-event-emitter
/// substrate (ADR 0031-A1+A1.12).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory reference implementations of every
    /// substrate contract introduced by W#36:
    /// <see cref="IEventSigner"/> (HMAC-SHA256),
    /// <see cref="IIdempotencyCache"/>,
    /// <see cref="ISharedSecretStore"/>,
    /// <see cref="IDeadLetterQueue"/>,
    /// <see cref="IWebhookDeliveryService"/>,
    /// <see cref="IWebhookRegistrationService"/>,
    /// <see cref="ITrustChainResolver"/>,
    /// <see cref="ReplayWindow"/>,
    /// <see cref="IBridgeSubscriptionEventHandler"/>.
    /// </summary>
    /// <remarks>
    /// Audit emission is disabled in this overload (test / bootstrap).
    /// Production hosts call
    /// <see cref="AddInMemoryBridgeSubscription(IServiceCollection, TenantId)"/>
    /// once an <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/>
    /// are registered (W#32 both-or-neither pattern). Hosts that want
    /// real webhook delivery must also register an <see cref="System.Net.Http.HttpClient"/>
    /// (typically via <c>IHttpClientFactory</c>) before resolving
    /// <see cref="IWebhookDeliveryService"/>.
    /// </remarks>
    public static IServiceCollection AddInMemoryBridgeSubscription(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IEventSigner>(_ => new HmacSha256EventSigner());
        services.TryAddSingleton<IIdempotencyCache>(_ => new InMemoryIdempotencyCache());
        services.TryAddSingleton<ISharedSecretStore>(_ => new InMemorySharedSecretStore());
        services.TryAddSingleton<IDeadLetterQueue>(_ => new InMemoryDeadLetterQueue());
        services.TryAddSingleton<IWebhookRegistrationService>(_ => new DefaultWebhookRegistrationService());
        services.TryAddSingleton<ITrustChainResolver>(_ => new DefaultTrustChainResolver());
        services.TryAddSingleton<ReplayWindow>(_ => new ReplayWindow());

        services.TryAddSingleton<IWebhookDeliveryService>(sp =>
            new DefaultWebhookDeliveryService(
                sp.GetRequiredService<System.Net.Http.HttpClient>(),
                sp.GetRequiredService<IDeadLetterQueue>()));

        services.TryAddSingleton<IBridgeSubscriptionEventHandler>(sp =>
            new InMemoryBridgeSubscriptionEventHandler(
                sp.GetRequiredService<IEventSigner>(),
                sp.GetRequiredService<ISharedSecretStore>(),
                sp.GetRequiredService<IIdempotencyCache>(),
                sp.GetRequiredService<ReplayWindow>(),
                sp.GetService<IEditionCacheUpdater>()));

        return services;
    }

    /// <summary>
    /// Audit-enabled overload — wires
    /// <see cref="InMemoryBridgeSubscriptionEventHandler"/> with an
    /// <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/>
    /// resolved from the container plus the supplied
    /// <paramref name="tenantId"/>. W#32 both-or-neither contract.
    /// </summary>
    public static IServiceCollection AddInMemoryBridgeSubscription(this IServiceCollection services, TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }

        services.TryAddSingleton<IEventSigner>(_ => new HmacSha256EventSigner());
        services.TryAddSingleton<IIdempotencyCache>(_ => new InMemoryIdempotencyCache());
        services.TryAddSingleton<ISharedSecretStore>(_ => new InMemorySharedSecretStore());
        services.TryAddSingleton<IDeadLetterQueue>(_ => new InMemoryDeadLetterQueue());
        services.TryAddSingleton<IWebhookRegistrationService>(_ => new DefaultWebhookRegistrationService());
        services.TryAddSingleton<ITrustChainResolver>(_ => new DefaultTrustChainResolver());
        services.TryAddSingleton<ReplayWindow>(_ => new ReplayWindow());

        services.TryAddSingleton<IWebhookDeliveryService>(sp =>
            new DefaultWebhookDeliveryService(
                sp.GetRequiredService<System.Net.Http.HttpClient>(),
                sp.GetRequiredService<IDeadLetterQueue>()));

        services.TryAddSingleton<IBridgeSubscriptionEventHandler>(sp =>
            new InMemoryBridgeSubscriptionEventHandler(
                sp.GetRequiredService<IEventSigner>(),
                sp.GetRequiredService<ISharedSecretStore>(),
                sp.GetRequiredService<IIdempotencyCache>(),
                sp.GetRequiredService<IAuditTrail>(),
                sp.GetRequiredService<IOperationSigner>(),
                tenantId,
                sp.GetRequiredService<ReplayWindow>(),
                sp.GetService<IEditionCacheUpdater>()));

        return services;
    }
}

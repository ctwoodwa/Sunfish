using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Sunfish.Foundation.BusinessLogic;
using Sunfish.Foundation.BusinessLogic.Rules;
using Sunfish.Kernel.Events;

namespace Sunfish.Foundation.RuleEngine.EventBridge.DependencyInjection;

/// <summary>
/// DI extensions for registering <see cref="BusinessRuleEventSubscriber"/> as a hosted service.
/// </summary>
public static class RuleEngineEventBridgeServiceCollectionExtensions
{
    /// <summary>
    /// Adds a <see cref="BusinessRuleEventSubscriber"/> as a hosted service. The subscriber
    /// pulls <see cref="IEventBus"/> and <see cref="BusinessRuleEngine"/> from the container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The caller must ensure <see cref="IEventBus"/> and <see cref="BusinessRuleEngine"/>
    /// are already registered before this call. Typically:
    /// <code>
    /// services.AddSunfishKernelEventBus();
    /// services.AddSingleton(myRuleEngine);
    /// services.AddBusinessRuleSubscriber(
    ///     new EventSubscription("my-subscriber"),
    ///     onEvaluated: (evt, broken) => { /* handle */ });
    /// </code>
    /// </para>
    /// <para>
    /// Multiple subscribers can be registered for different subscriptions or engines;
    /// each call to <c>AddBusinessRuleSubscriber</c> adds an independent
    /// <see cref="Microsoft.Extensions.Hosting.IHostedService"/> registration.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="subscription">
    /// The subscription parameters (subscriber id, optional entity and kind filters).
    /// </param>
    /// <param name="onEvaluated">
    /// Optional callback invoked after each evaluation. See
    /// <see cref="BusinessRuleEventSubscriber"/> for threading notes.
    /// </param>
    /// <param name="extractor">
    /// Optional function that maps a <see cref="KernelEvent"/> to a domain object
    /// before rule evaluation. When <see langword="null"/>, the raw event is evaluated.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddBusinessRuleSubscriber(
        this IServiceCollection services,
        EventSubscription subscription,
        Action<KernelEvent, IReadOnlyList<BrokenRule>>? onEvaluated,
        Func<KernelEvent, object>? extractor = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(subscription);

        services.AddHostedService(sp => new BusinessRuleEventSubscriber(
            bus: sp.GetRequiredService<IEventBus>(),
            engine: sp.GetRequiredService<BusinessRuleEngine>(),
            subscription: subscription,
            onEvaluated: onEvaluated,
            extractor: extractor,
            logger: sp.GetService<Microsoft.Extensions.Logging.ILogger<BusinessRuleEventSubscriber>>()));

        return services;
    }
}

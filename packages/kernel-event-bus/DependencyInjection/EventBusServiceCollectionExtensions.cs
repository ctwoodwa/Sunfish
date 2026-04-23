using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Sunfish.Kernel.Events;

namespace Sunfish.Kernel.Events.DependencyInjection;

/// <summary>
/// DI extensions for registering the Sunfish kernel event bus (spec §3.6) and event log
/// (paper §2.5, §8).
/// </summary>
public static class EventBusServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory event-bus backend as a singleton
    /// <see cref="IEventBus"/>. Does NOT register
    /// <see cref="Sunfish.Foundation.Crypto.IOperationVerifier"/> — the caller
    /// must have already registered one (typically via
    /// <c>AddSunfishDecentralization</c> or
    /// <c>services.AddSingleton&lt;IOperationVerifier, Ed25519Verifier&gt;()</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses <c>TryAddSingleton</c> so a prior registration of
    /// <see cref="IEventBus"/> (for example a test double or a future
    /// distributed backend) wins. The call is therefore idempotent: repeated
    /// invocations do not throw or stack multiple registrations.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishKernelEventBus(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IEventBus, InMemoryEventBus>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IEventLog"/> singleton backed by
    /// <see cref="FileBackedEventLog"/>. Paper §2.5 / §8.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses <c>TryAddSingleton</c> so callers can preempt the registration with
    /// <see cref="UseInMemoryEventLog"/> (for tests) or a custom backend before calling this.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Optional options configurator.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishEventLog(
        this IServiceCollection services,
        Action<EventLogOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            // Ensure there is an IOptions<EventLogOptions> available even when the caller passes
            // no configurator — the FileBackedEventLog constructor requires it.
            services.AddOptions<EventLogOptions>();
        }
        services.TryAddSingleton<IEventLog, FileBackedEventLog>();
        return services;
    }

    /// <summary>
    /// Forces the <see cref="IEventLog"/> registration to be backed by <see cref="InMemoryEventLog"/>.
    /// Intended for tests; call <b>before</b> <see cref="AddSunfishEventLog"/> (or use it on its
    /// own) so the <c>TryAddSingleton</c> inside <see cref="AddSunfishEventLog"/> sees an existing
    /// registration and becomes a no-op.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection UseInMemoryEventLog(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.RemoveAll<IEventLog>();
        services.AddSingleton<IEventLog, InMemoryEventLog>();
        return services;
    }
}

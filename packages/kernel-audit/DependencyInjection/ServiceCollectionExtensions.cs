using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.Kernel.Audit.DependencyInjection;

/// <summary>
/// DI extensions for the Sunfish kernel audit-trail subsystem (ADR 0049).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the audit subsystem — <see cref="IAuditTrail"/> and
    /// <see cref="IAuditEventStream"/> — as singletons. Direct parallel to
    /// <c>AddSunfishKernelLedger</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Depends on <c>IEventLog</c> (from <c>Sunfish.Kernel.EventBus</c>) and
    /// <c>IOperationVerifier</c> (from <c>Sunfish.Foundation.Crypto</c>) being
    /// registered by the caller. Typical composition:
    /// </para>
    /// <code>
    /// services
    ///     .AddSunfishEventLog(o => o.RootDirectory = "./data/events")
    ///     .AddSunfishKernelAudit();
    /// </code>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishKernelAudit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The trail publishes directly to the concrete in-memory stream to
        // keep replay deterministic; register the concrete first, then expose
        // it via the IAuditEventStream interface for consumers.
        services.TryAddSingleton<InMemoryAuditEventStream>();
        services.TryAddSingleton<IAuditEventStream>(sp =>
            sp.GetRequiredService<InMemoryAuditEventStream>());

        services.TryAddSingleton<IAuditTrail, EventLogBackedAuditTrail>();

        return services;
    }
}

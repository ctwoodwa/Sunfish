using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// DI registration for the foundation-tier Wayfinder substrate (ADR 0065).
/// Per cohort <c>AddSunfishX()</c> convention (W#34 / W#35 / W#36 / W#39 /
/// W#40 / W#41).
/// </summary>
public static class WayfinderServiceExtensions
{
    /// <summary>
    /// Register the Wayfinder substrate without an audit-emitter coupling.
    /// Phase 1 registers no concrete <see cref="IStandingOrderRepository"/> /
    /// <see cref="IStandingOrderIssuer"/> implementations — those land in
    /// Phase 2 (CRDT-backed) and are wired by their own DI extensions.
    /// </summary>
    /// <remarks>
    /// Hosts add validators via
    /// <see cref="AddStandingOrderValidator{TValidator}(IServiceCollection)"/>
    /// (for example, a <c>SchemaValidator</c> at
    /// <see cref="StandingOrderValidatorPriority.Schema"/> or a
    /// <c>PolicyValidator</c> at <see cref="StandingOrderValidatorPriority.Policy"/>).
    /// </remarks>
    public static IServiceCollection AddSunfishWayfinder(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // Phase 1 is contract-only. No singleton repository / issuer here;
        // those land in Phase 2 with the CRDT-backed implementations.
        return services;
    }

    /// <summary>
    /// Register a concrete <see cref="IStandingOrderValidator"/> implementation
    /// into the validator chain. Validators run in ascending
    /// <see cref="IStandingOrderValidator.Priority"/> order at issuance time.
    /// Multiple validators may share a priority slot; their relative order
    /// within the slot is registration order.
    /// </summary>
    /// <typeparam name="TValidator">The validator implementation type.</typeparam>
    /// <param name="services">DI container.</param>
    public static IServiceCollection AddStandingOrderValidator<TValidator>(this IServiceCollection services)
        where TValidator : class, IStandingOrderValidator
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStandingOrderValidator, TValidator>());
        return services;
    }
}

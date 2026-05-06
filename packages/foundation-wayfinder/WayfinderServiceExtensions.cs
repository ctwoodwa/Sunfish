using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// DI registration for the foundation-tier Wayfinder substrate (ADR 0065).
/// Per cohort <c>AddSunfishX()</c> convention (W#34 / W#35 / W#36 / W#39 /
/// W#40 / W#41).
/// </summary>
public static class WayfinderServiceExtensions
{
    /// <summary>
    /// Register the Wayfinder substrate: CRDT-backed
    /// <see cref="CrdtStandingOrderRepository"/> + reference
    /// <see cref="DefaultStandingOrderIssuer"/> + a <see cref="TimeProvider"/>
    /// fallback (<see cref="TimeProvider.System"/>) when none has been
    /// registered. Hosts MUST separately register an
    /// <see cref="Sunfish.Kernel.Crdt.ICrdtEngine"/> and an
    /// <see cref="Sunfish.Foundation.Crypto.IOperationSigner"/>; the issuer
    /// resolves them at first construction.
    /// </summary>
    /// <remarks>
    /// Hosts add validators via
    /// <see cref="AddStandingOrderValidator{TValidator}(IServiceCollection)"/>
    /// (for example, a <c>SchemaValidator</c> at
    /// <see cref="StandingOrderValidatorPriority.Schema"/> or a
    /// <c>PolicyValidator</c> at <see cref="StandingOrderValidatorPriority.Policy"/>).
    /// Validators run in ascending priority at issuance time; ties resolve
    /// to registration order.
    /// </remarks>
    public static IServiceCollection AddSunfishWayfinder(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<CrdtStandingOrderRepository>();
        services.TryAddSingleton<IStandingOrderRepository>(
            sp => sp.GetRequiredService<CrdtStandingOrderRepository>());
        // ADR 0065-A1 §A1.5 — in-process Standing-Order applied-event
        // stream. The concrete InMemoryStandingOrderEventStream is
        // registered as a singleton so the issuer can resolve the
        // publish surface; consumers resolve the public
        // IStandingOrderEventStream abstraction (same instance).
        services.TryAddSingleton<InMemoryStandingOrderEventStream>();
        services.TryAddSingleton<IStandingOrderEventStream>(
            sp => sp.GetRequiredService<InMemoryStandingOrderEventStream>());

        services.TryAddSingleton<IStandingOrderIssuer, DefaultStandingOrderIssuer>();
        // Phase 3a — Atlas projector (LWW projection + linear search).
        services.TryAddSingleton<DefaultAtlasProjector>();
        services.TryAddSingleton<IAtlasProjector>(
            sp => sp.GetRequiredService<DefaultAtlasProjector>());
        services.TryAddSingleton(TimeProvider.System);

        // W#49 P2 — OOD watch rotation primitive. Hosts MUST separately
        // register a concrete repository binding for BOTH IOodWatchRepository
        // (per-tenant operations) and IOodWatchSweepRepository (cross-tenant
        // sweep enumerator, internal — only OodWatchExpiryService can resolve
        // it). The R4 split (XO post-merge council 2026-05-06) means hosts
        // typically register one concrete impl and bind it to both interfaces:
        //
        //   services.AddSingleton<MyOodWatchRepository>();
        //   services.AddSingleton<IOodWatchRepository>(sp =>
        //       sp.GetRequiredService<MyOodWatchRepository>());
        //   services.AddSingleton<IOodWatchSweepRepository>(sp =>
        //       sp.GetRequiredService<MyOodWatchRepository>());
        //
        // No in-memory default exists in Phase 2; Phase 3 will add one.
        // IAuditTrail + IOperationSigner are MANDATORY for OOD authority
        // operations per ADR 0078 §Trust; DefaultOodWatchService throws
        // InvalidOperationException at first invocation if either is missing.
        // ILogger<T> is auto-resolved when the host calls AddLogging() (every
        // ASP.NET Core or Aspire host does).
        services.TryAddSingleton<IOodWatchService, DefaultOodWatchService>();
        services.AddHostedService<OodWatchExpiryService>();

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

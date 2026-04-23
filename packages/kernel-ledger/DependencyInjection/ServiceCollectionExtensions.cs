using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Sunfish.Kernel.Ledger.CQRS;
using Sunfish.Kernel.Ledger.Periods;

namespace Sunfish.Kernel.Ledger.DependencyInjection;

/// <summary>
/// DI extensions for the Sunfish kernel ledger subsystem (paper §12).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the ledger subsystem — <see cref="IPostingEngine"/>,
    /// <see cref="IBalanceProjection"/>, <see cref="IStatementProjection"/>,
    /// and <see cref="IPeriodCloser"/> — as singletons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Depends on <c>ILeaseCoordinator</c> and <c>IEventLog</c> being
    /// registered by the caller. Typical composition:
    /// </para>
    /// <code>
    /// services
    ///     .AddSunfishEventLog(o => o.RootDirectory = "./data/events")
    ///     .AddSunfishKernelLease(localNodeId: "node-a")
    ///     .AddSunfishKernelLedger();
    /// </code>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishKernelLedger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // PeriodCloseState is shared state between the posting engine (reader)
        // and the period closer (writer). Register the concrete first, then
        // expose it via the IPeriodCloseState interface.
        services.TryAddSingleton<PeriodCloseState>();
        services.TryAddSingleton<IPeriodCloseState>(sp => sp.GetRequiredService<PeriodCloseState>());

        // The PostingEngine is the concrete type because it doubles as the
        // ILedgerEventStream (internal) source for projections. IPostingEngine
        // is a facade over the same singleton.
        services.TryAddSingleton<PostingEngine>();
        services.TryAddSingleton<IPostingEngine>(sp => sp.GetRequiredService<PostingEngine>());
        services.TryAddSingleton<ILedgerEventStream>(sp => sp.GetRequiredService<PostingEngine>());

        services.TryAddSingleton<IBalanceProjection, BalanceProjection>();
        services.TryAddSingleton<IStatementProjection, StatementProjection>();

        services.TryAddSingleton<IPeriodCloser, PeriodCloser>();

        return services;
    }
}

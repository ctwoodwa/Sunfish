using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPeriods.Migration;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialPeriods.DependencyInjection;

/// <summary>
/// DI extensions for <c>blocks-financial-periods</c>. PR 2 wires the
/// soft-close service + period resolver; PR 3 extends to hard-close +
/// year-end rollover; PR 4 wires the ERPNext importer hooks.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the period-management surface. Does NOT register
    /// <see cref="IFiscalPeriodRepository"/> or
    /// <see cref="IFiscalYearRepository"/> implementations — the host
    /// composition root supplies those (SQLite in production; the
    /// in-memory fakes via <see cref="AddInMemoryBlocksFinancialPeriods"/>
    /// in tests / demos).
    /// </summary>
    public static IServiceCollection AddBlocksFinancialPeriods(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Period-state transitions per Stage 02 §6.5(a) + §8.5 row 3.
        services.TryAddSingleton<IPeriodCloseService, PeriodCloseService>();

        // Year-end close + retained-earnings rollover per Stage 02 §6.5(b).
        services.TryAddSingleton<IFiscalYearCloseService, FiscalYearCloseService>();

        // Production resolver — projects FiscalPeriod rows into the
        // ledger's minimal PeriodSnapshot contract.
        services.TryAddSingleton<IPeriodResolver, SqlitePeriodResolver>();

        // Canonical IDomainEventPublisher is registered by the host's
        // composition root via Sunfish.Foundation.Events.
        // ServiceCollectionExtensions.AddFoundationEvents(). The local
        // Noop fallback was removed in the foundation-events PR 6 sweep.

        // ERPNext importer hooks (PR 4) — synthesize the period set
        // for an imported FY since ERPNext does not export periods as
        // a discrete doctype.
        services.TryAddSingleton<IErpnextFiscalYearImporter, ErpnextFiscalYearImporter>();
        services.TryAddSingleton<IErpnextFiscalPeriodImporter, ErpnextFiscalPeriodImporter>();

        return services;
    }

    /// <summary>
    /// Register the in-memory repository fakes alongside the period-
    /// management surface. Suitable for tests, kitchen-sink demos, and
    /// ERPNext migration dry-runs. Production hosts replace these with
    /// the SQLite-backed implementations.
    /// </summary>
    /// <remarks>
    /// The <c>InMemory*</c> repositories are registered as
    /// <see cref="ServiceLifetime.Singleton"/> deliberately — they hold
    /// in-process state that must survive scope boundaries to behave
    /// like a real database for demos. This matches the sibling
    /// <c>AddInMemoryAccounting</c> precedent in
    /// <c>blocks-financial-ledger</c>. Concurrent updates use a
    /// compare-and-swap loop inside the repository
    /// (<see cref="InMemoryFiscalPeriodRepository.UpdateAsync"/>) to
    /// keep the Singleton lifetime safe for multi-caller demo flows.
    /// </remarks>
    public static IServiceCollection AddInMemoryBlocksFinancialPeriods(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFiscalPeriodRepository, InMemoryFiscalPeriodRepository>();
        services.TryAddSingleton<IFiscalYearRepository, InMemoryFiscalYearRepository>();
        services.TryAddSingleton<IChartRepository, InMemoryChartRepository>();
        services.TryAddSingleton<IAccountTypeQuery, InMemoryAccountTypeQuery>();
        services.TryAddSingleton<IBalanceComputer, InMemoryBalanceComputer>();
        return services.AddBlocksFinancialPeriods();
    }
}

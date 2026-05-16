using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPeriods.Services;

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

        // Period-state transitions per Stage 02 §6.5(a). PR 3 will extend.
        services.TryAddSingleton<IPeriodCloseService, PeriodCloseService>();

        // Production resolver — projects FiscalPeriod rows into the
        // ledger's minimal PeriodSnapshot contract.
        services.TryAddSingleton<IPeriodResolver, SqlitePeriodResolver>();

        // Local event-publisher seam until the canonical foundation/
        // kernel-events home is ratified (cob-question filed PR 2).
        services.TryAddSingleton<IDomainEventPublisher, NoopDomainEventPublisher>();

        return services;
    }

    /// <summary>
    /// Register the in-memory repository fakes alongside the period-
    /// management surface. Suitable for tests, kitchen-sink demos, and
    /// ERPNext migration dry-runs. Production hosts replace these with
    /// the SQLite-backed implementations.
    /// </summary>
    public static IServiceCollection AddInMemoryBlocksFinancialPeriods(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFiscalPeriodRepository, InMemoryFiscalPeriodRepository>();
        services.TryAddSingleton<IFiscalYearRepository, InMemoryFiscalYearRepository>();
        return services.AddBlocksFinancialPeriods();
    }
}

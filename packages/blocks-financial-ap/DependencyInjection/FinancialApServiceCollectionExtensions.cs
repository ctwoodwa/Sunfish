using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.FinancialAp.Migration;
using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialAp.DependencyInjection;

/// <summary>
/// DI helpers for the accounts-payable cluster.
/// </summary>
public static class FinancialApServiceCollectionExtensions
{
    /// <summary>
    /// Register the in-memory bill substrate. Uses
    /// <c>TryAddSingleton</c> so persistence-backed implementations
    /// registered earlier by the host shadow these defaults.
    /// </summary>
    public static IServiceCollection AddBlocksFinancialAp(
        this IServiceCollection services,
        Action<BlocksFinancialApOptions>? configure = null)
    {
        var options = new BlocksFinancialApOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IBillRepository, InMemoryBillRepository>();
        services.TryAddSingleton<IApAgingService, ApAgingService>();
        services.TryAddSingleton<ITaxCalculator, NoOpTaxCalculator>();
        services.TryAddSingleton<IDomainEventPublisher, NoopDomainEventPublisher>();
        services.TryAddSingleton<IBillPostingService, BillPostingService>();
        services.TryAddSingleton<IErpnextPurchaseInvoiceImporter, ErpnextPurchaseInvoiceImporter>();
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.FinancialAr.Migration;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialAr.DependencyInjection;

/// <summary>
/// DI helpers for the accounts-receivable cluster.
/// </summary>
public static class FinancialArServiceCollectionExtensions
{
    /// <summary>
    /// Register the in-memory invoice substrate. Uses
    /// <c>TryAddSingleton</c> so persistence-backed implementations
    /// registered earlier by the host shadow these defaults.
    ///
    /// <para>
    /// Optional <paramref name="configure"/> lets the host set
    /// <see cref="BlocksFinancialArOptions.LocalReplicaId"/>. Without
    /// it the sentinel <c>"AA"</c> is used — fine for single-replica
    /// tests, but installs SHOULD override at boot so two devices on
    /// the same install can't mint colliding invoice numbers.
    /// </para>
    /// </summary>
    public static IServiceCollection AddBlocksFinancialAr(
        this IServiceCollection services,
        Action<BlocksFinancialArOptions>? configure = null)
    {
        var options = new BlocksFinancialArOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IInvoiceRepository, InMemoryInvoiceRepository>();
        services.TryAddSingleton<IInvoiceNumberingService>(_ =>
            new InMemoryInvoiceNumberingService(options.LocalReplicaId));
        services.TryAddSingleton<ITaxCalculator, NoOpTaxCalculator>();
        services.TryAddSingleton<IDomainEventPublisher, NoopDomainEventPublisher>();
        services.TryAddSingleton<IInvoicePostingService, InvoicePostingService>();
        services.TryAddSingleton<IArAgingService, ArAgingService>();
        services.TryAddSingleton<IErpnextSalesInvoiceImporter, ErpnextSalesInvoiceImporter>();
        return services;
    }
}

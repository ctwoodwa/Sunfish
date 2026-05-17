using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.FinancialAr.Services;

namespace Sunfish.Blocks.FinancialAr.DependencyInjection;

/// <summary>
/// DI helpers for the accounts-receivable cluster.
/// </summary>
public static class FinancialArServiceCollectionExtensions
{
    /// <summary>
    /// Register the in-memory invoice substrate. Uses
    /// <c>TryAddSingleton</c> so a persistence-backed
    /// <see cref="IInvoiceRepository"/> registered earlier by the host
    /// shadows this default.
    /// </summary>
    public static IServiceCollection AddBlocksFinancialAr(this IServiceCollection services)
    {
        services.TryAddSingleton<IInvoiceRepository, InMemoryInvoiceRepository>();
        return services;
    }
}

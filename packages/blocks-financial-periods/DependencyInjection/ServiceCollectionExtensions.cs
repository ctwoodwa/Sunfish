using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Blocks.FinancialPeriods.DependencyInjection;

/// <summary>
/// DI extension for <c>blocks-financial-periods</c>. PR 1 ships entities
/// only — service registrations land in PR 2 (soft-close + period
/// resolver) and PR 3 (hard-close + year-end rollover). The extension
/// exists in PR 1 so consumers can wire
/// <see cref="AddBlocksFinancialPeriods"/> without compile breakage as
/// the chain lands.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <c>blocks-financial-periods</c> services. PR 1 is a no-op;
    /// PR 2 + PR 3 + PR 4 wire the close service, period resolver, and
    /// importer hooks respectively.
    /// </summary>
    public static IServiceCollection AddBlocksFinancialPeriods(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // Service registrations land in PR 2 + PR 3 + PR 4.
        return services;
    }
}

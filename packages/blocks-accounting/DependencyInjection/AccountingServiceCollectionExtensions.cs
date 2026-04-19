using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Accounting.Services;

namespace Sunfish.Blocks.Accounting.DependencyInjection;

/// <summary>
/// Extension methods for registering accounting services in a
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class AccountingServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="InMemoryAccountingService"/> as the
    /// <see cref="IAccountingService"/> implementation, and a singleton
    /// <see cref="QuickBooksIifExporter"/> as <see cref="IQuickBooksJournalEntryExporter"/>.
    /// Suitable for testing, prototyping, and kitchen-sink demos.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInMemoryAccounting(this IServiceCollection services)
    {
        services.AddSingleton<IAccountingService, InMemoryAccountingService>();
        services.AddSingleton<IQuickBooksJournalEntryExporter, QuickBooksIifExporter>();
        return services;
    }
}

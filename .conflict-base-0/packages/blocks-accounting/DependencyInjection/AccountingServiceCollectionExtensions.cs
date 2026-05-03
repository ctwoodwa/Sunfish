using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.Accounting.Services;
using Sunfish.Foundation.Localization;

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
    /// Also contributes the open-generic <see cref="ISunfishLocalizer{T}"/> binding
    /// so consumers can resolve the accounting <c>SharedResource</c> bundle. Caller
    /// is responsible for wiring <c>services.AddLocalization()</c> in the composition
    /// root (matches the Bridge pattern; class libraries don't take a hard
    /// PackageReference on <c>Microsoft.Extensions.Localization</c>).
    /// Suitable for testing, prototyping, and kitchen-sink demos.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInMemoryAccounting(this IServiceCollection services)
    {
        services.AddSingleton<IAccountingService, InMemoryAccountingService>();
        services.AddSingleton<IQuickBooksJournalEntryExporter, QuickBooksIifExporter>();
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>)));
        return services;
    }
}

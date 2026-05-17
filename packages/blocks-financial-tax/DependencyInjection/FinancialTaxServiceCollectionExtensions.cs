using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.FinancialTax.Migration;
using Sunfish.Blocks.FinancialTax.Services;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialTax.DependencyInjection;

/// <summary>
/// DI registration for the blocks-financial-tax package. Covers the
/// jurisdiction / code / rate / calculation / form-line-map services
/// + the ledger-wiring + ERPNext import + event-publisher seams from
/// PRs 1-5 of the blocks-financial-tax-stage06-handoff.
/// </summary>
public static class FinancialTaxServiceCollectionExtensions
{
    /// <summary>
    /// Register the blocks-financial-tax services. Call once during
    /// app composition; idempotency is the consumer's responsibility
    /// (use <c>TryAdd*</c> in their own composition if needed).
    ///
    /// <para>
    /// <see cref="IDomainEventPublisher"/> is supplied by the host's
    /// composition root via
    /// <c>Sunfish.Foundation.Events.ServiceCollectionExtensions.AddFoundationEvents()</c>.
    /// The local Noop fallback was removed in the foundation-events
    /// PR 6 sweep — hosts MUST call <c>AddFoundationEvents()</c>
    /// before (or after) this method to register the canonical
    /// publisher.
    /// </para>
    /// </summary>
    public static IServiceCollection AddBlocksFinancialTax(this IServiceCollection services)
    {
        services.AddSingleton<ITaxJurisdictionStore, InMemoryTaxJurisdictionStore>();
        services.AddSingleton<ITaxJurisdictionResolver, InMemoryTaxJurisdictionResolver>();
        services.AddSingleton<ITaxRateLookup, InMemoryTaxRateLookup>();
        services.AddSingleton<ITaxCodeStore, InMemoryTaxCodeStore>();
        services.AddSingleton<ITaxCalculationService, TaxCalculationService>();
        services.AddSingleton<ITaxFormLineMapStore, InMemoryTaxFormLineMapStore>();
        services.AddSingleton<IJournalTaxLineGenerator, JournalTaxLineGenerator>();
        services.AddSingleton<IErpnextTaxImporter, ErpnextTaxImporter>();
        //
        // NOTE: InMemoryTaxRateLookup + ErpnextTaxImporter require an
        // IAccountResolver from blocks-financial-ledger. Composition
        // consumers must call services.AddBlocksFinancialLedger(...)
        // (or otherwise register an IAccountResolver implementation)
        // before resolving ITaxRateLookup or IErpnextTaxImporter.
        return services;
    }
}

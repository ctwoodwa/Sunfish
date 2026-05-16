using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.FinancialTax.Services;

namespace Sunfish.Blocks.FinancialTax.DependencyInjection;

/// <summary>
/// DI registration for the blocks-financial-tax package. PR 1 ships
/// the in-memory jurisdiction store + resolver; rate / calculation /
/// form-line-map / ledger-wiring services land in PR 2–5 of the
/// blocks-financial-tax-stage06-handoff.
/// </summary>
public static class FinancialTaxServiceCollectionExtensions
{
    /// <summary>
    /// Register the blocks-financial-tax services. Call once during
    /// app composition; idempotency is the consumer's responsibility
    /// (use <c>TryAdd*</c> in their own composition if needed).
    /// </summary>
    public static IServiceCollection AddBlocksFinancialTax(this IServiceCollection services)
    {
        services.AddSingleton<ITaxJurisdictionStore, InMemoryTaxJurisdictionStore>();
        services.AddSingleton<ITaxJurisdictionResolver, InMemoryTaxJurisdictionResolver>();
        services.AddSingleton<ITaxRateLookup, InMemoryTaxRateLookup>();
        // PR 3 adds ITaxCalculationService; PR 4 adds ITaxFormLineMapStore;
        // PR 5 adds the ledger-wiring service.
        //
        // NOTE: InMemoryTaxRateLookup requires an IAccountResolver from
        // blocks-financial-ledger. Composition consumers must call
        // services.AddBlocksFinancialLedger(...) (or otherwise register
        // an IAccountResolver implementation) before resolving
        // ITaxRateLookup.
        return services;
    }
}

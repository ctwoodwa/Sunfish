using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.FinancialTaxBridge.Adapters;

namespace Sunfish.Blocks.FinancialTaxBridge.DependencyInjection;

/// <summary>
/// DI extension for <c>blocks-financial-tax-bridge</c>.
/// </summary>
public static class FinancialTaxBridgeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the canonical-tax bridge. AR's + AP's
    /// <c>NoOpTaxCalculator</c> registrations are REPLACED (not
    /// merely augmented) with adapters that delegate to
    /// <c>ITaxCalculationService</c>. Call AFTER
    /// <c>AddBlocksFinancialAr()</c>, <c>AddBlocksFinancialAp()</c>,
    /// and <c>AddBlocksFinancialTax()</c> — the Replace calls require
    /// the prior registrations to exist.
    /// </summary>
    /// <remarks>
    /// Hosts that need a non-default jurisdiction must Configure the
    /// options BEFORE calling this method:
    /// <code>
    /// services.Configure&lt;BlocksFinancialTaxBridgeOptions&gt;(o =>
    ///     o.DefaultLocation = new TaxLocationContext(IsoCountry: "US", Region: "US-VA"));
    /// services.AddBlocksFinancialTaxBridge();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddBlocksFinancialTaxBridge(this IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        services.AddOptions<BlocksFinancialTaxBridgeOptions>();

        services.Replace(ServiceDescriptor.Singleton<
            Sunfish.Blocks.FinancialAr.Services.ITaxCalculator,
            ArTaxCalculatorAdapter>());

        services.Replace(ServiceDescriptor.Singleton<
            Sunfish.Blocks.FinancialAp.Services.ITaxCalculator,
            ApTaxCalculatorAdapter>());

        return services;
    }
}

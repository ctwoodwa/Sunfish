using Sunfish.Blocks.FinancialTax.Models;

namespace Sunfish.Blocks.FinancialTaxBridge.DependencyInjection;

/// <summary>
/// Options for <see cref="FinancialTaxBridgeServiceCollectionExtensions.AddBlocksFinancialTaxBridge"/>.
/// </summary>
/// <remarks>
/// AR's + AP's local <c>ITaxCalculator</c> surface is
/// <c>(taxCodeId, taxableBase, transactionDate) → decimal</c>; it
/// does NOT carry a per-line <see cref="TaxLocationContext"/>. The
/// canonical <c>ITaxCalculationService</c> requires one. The bridge
/// closes the gap by supplying a per-host default
/// <see cref="DefaultLocation"/> for every call. Hosts that need
/// per-call jurisdiction selection register a richer
/// <c>ITaxCalculator</c> decorator instead of (or in front of) this
/// bridge.
/// </remarks>
public sealed class BlocksFinancialTaxBridgeOptions
{
    /// <summary>
    /// Location context forwarded into every
    /// <c>TaxCalculationInput</c>. Default: US (no region). Hosts that
    /// operate in a single jurisdiction set this once at boot:
    /// <code>
    /// services.Configure&lt;BlocksFinancialTaxBridgeOptions&gt;(o =>
    ///     o.DefaultLocation = new TaxLocationContext(IsoCountry: "US", Region: "US-VA"));
    /// </code>
    /// </summary>
    public TaxLocationContext DefaultLocation { get; set; } = new(IsoCountry: "US");
}

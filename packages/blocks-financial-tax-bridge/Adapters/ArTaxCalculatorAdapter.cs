using Microsoft.Extensions.Options;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;
using Sunfish.Blocks.FinancialTaxBridge.DependencyInjection;
using CanonicalTax = Sunfish.Blocks.FinancialTax.Services;

namespace Sunfish.Blocks.FinancialTaxBridge.Adapters;

/// <summary>
/// Bridges AR's local <see cref="ITaxCalculator"/> to the canonical
/// <see cref="CanonicalTax.ITaxCalculationService"/>. Returns
/// <c>0m</c> when the supplied tax-code id is null/whitespace OR
/// when the canonical call reports a non-<see cref="TaxCalculationError.None"/>
/// error (per AR's local interface contract).
/// </summary>
public sealed class ArTaxCalculatorAdapter : ITaxCalculator
{
    private readonly CanonicalTax.ITaxCalculationService _canonical;
    private readonly BlocksFinancialTaxBridgeOptions _options;

    /// <summary>Construct an adapter bound to the canonical engine + bridge options.</summary>
    public ArTaxCalculatorAdapter(
        CanonicalTax.ITaxCalculationService canonical,
        IOptions<BlocksFinancialTaxBridgeOptions> options)
    {
        _canonical = canonical ?? throw new ArgumentNullException(nameof(canonical));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<decimal> CalculateAsync(
        string? taxCodeId,
        decimal taxableBase,
        DateOnly transactionDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taxCodeId))
            return 0m;

        var input = new TaxCalculationInput(
            TaxCodeId:       new TaxCodeId(taxCodeId),
            Subtotal:        taxableBase,
            TransactionDate: transactionDate,
            Location:        _options.DefaultLocation);

        var result = await _canonical.CalculateAsync(input, cancellationToken).ConfigureAwait(false);
        return result.Error == TaxCalculationError.None ? result.TaxAmount : 0m;
    }
}

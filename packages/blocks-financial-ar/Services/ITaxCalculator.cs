namespace Sunfish.Blocks.FinancialAr.Services;

/// <summary>
/// AR-internal tax abstraction. Per-line tax compute reduced to its
/// minimum surface — just <c>(taxCodeId, taxableBase, txnDate)</c> →
/// <c>taxAmount</c>. Keeps AR independent of <c>blocks-financial-tax</c>'s
/// exact API shape (TaxJurisdictionId, JurisdictionLevel, breakdown
/// rows, etc.) — that cluster's calculation engine is much richer than
/// what AR needs at the line level.
///
/// <para>
/// A bridge adapter between this interface and
/// <c>Sunfish.Blocks.FinancialTax.Services.ITaxCalculationService</c>
/// can land in a separate package; AR consumers register that
/// adapter when they want real tax. Without it,
/// <see cref="NoOpTaxCalculator"/> returns zero.
/// </para>
/// </summary>
public interface ITaxCalculator
{
    /// <summary>
    /// Compute tax for a single line. Returns 0 when
    /// <paramref name="taxCodeId"/> is null/empty (no tax) or when the
    /// implementation can't resolve the code.
    /// </summary>
    Task<decimal> CalculateAsync(
        string? taxCodeId,
        decimal taxableBase,
        DateOnly transactionDate,
        CancellationToken cancellationToken = default);
}

/// <summary>No-op default. Returns zero unconditionally.</summary>
public sealed class NoOpTaxCalculator : ITaxCalculator
{
    /// <inheritdoc />
    public Task<decimal> CalculateAsync(string? taxCodeId, decimal taxableBase, DateOnly transactionDate, CancellationToken cancellationToken = default)
        => Task.FromResult(0m);
}

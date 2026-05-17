namespace Sunfish.Blocks.FinancialAp.Services;

/// <summary>
/// AP-internal tax abstraction. Per-line tax compute reduced to its
/// minimum surface — <c>(taxCodeId, taxableBase, txnDate)</c> →
/// <c>taxAmount</c>. Keeps AP independent of
/// <c>blocks-financial-tax</c>'s richer API. A bridge adapter wires
/// the canonical tax engine behind this interface in a follow-on.
/// Mirrors the AR-side <c>Sunfish.Blocks.FinancialAr.Services.ITaxCalculator</c>.
/// </summary>
public interface ITaxCalculator
{
    /// <summary>
    /// Compute tax for a single bill line. Returns 0 when
    /// <paramref name="taxCodeId"/> is null/empty or the implementation
    /// can't resolve the code.
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

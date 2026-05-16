using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// Expands a set of pre-tax <c>JournalEntryLine</c> candidates (each
/// optionally carrying a <c>TaxCodeId</c>) into a balanced line set
/// that includes the tax-payable lines per the resolved rates.
///
/// <para>
/// Called by Invoice/Bill posting code <b>before</b> constructing the
/// final <c>JournalEntry</c>. The journal-posting service itself
/// remains tax-agnostic per the hand-off scope discipline (tax is a
/// layer above posting). The caller is responsible for the offsetting
/// AR/AP debit/credit; this service only emits the tax-payable side.
/// </para>
///
/// <para>
/// Per Stage 02 §6.4: "the per-rate breakdown is stored on the line so
/// that GL posting can split tax-payable into one line per
/// jurisdiction." That's why this service produces one
/// <c>JournalEntryLine</c> per
/// <see cref="TaxRateBreakdownLine"/> — preserving the per-
/// jurisdiction audit trail rather than collapsing to a single
/// per-code total.
/// </para>
/// </summary>
public interface IJournalTaxLineGenerator
{
    /// <summary>
    /// Compute tax for each pre-tax line that carries a
    /// <c>TaxCodeId</c> and return the union of pre-tax + tax-payable
    /// lines. The result is structurally balanced from the tax side
    /// only — the caller still has to supply the offsetting AR/AP
    /// posting.
    /// </summary>
    Task<JournalTaxLineGenerationResult> GenerateAsync(
        IReadOnlyList<FL.JournalEntryLine> preTaxLines,
        DateOnly transactionDate,
        TaxLocationContext location,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of <see cref="IJournalTaxLineGenerator.GenerateAsync"/>.
/// </summary>
/// <param name="AllLines">
/// Concatenation of the input pre-tax lines + the generated tax-
/// payable lines (one per <see cref="TaxRateBreakdownLine"/>). Order
/// is: pre-tax lines in input order, then tax-payable lines
/// grouped by their originating pre-tax line.
/// </param>
/// <param name="TotalTaxAmount">Sum of every generated tax-payable line's Credit.</param>
/// <param name="PerLineResults">
/// One <see cref="TaxCalculationResult"/> per pre-tax line that
/// carried a <c>TaxCodeId</c> (lines without a code are skipped).
/// Same order as the input lines.
/// </param>
/// <param name="FirstError">
/// First non-<see cref="TaxCalculationError.None"/> error encountered,
/// or null on full success. Generation stops on the first error and
/// returns a partial result so the caller can decide whether to
/// abort the post or surface the diagnostic.
/// </param>
/// <param name="Detail">Human-readable elaboration of <see cref="FirstError"/>.</param>
public sealed record JournalTaxLineGenerationResult(
    IReadOnlyList<FL.JournalEntryLine> AllLines,
    decimal TotalTaxAmount,
    IReadOnlyList<TaxCalculationResult> PerLineResults,
    TaxCalculationError? FirstError,
    string? Detail);

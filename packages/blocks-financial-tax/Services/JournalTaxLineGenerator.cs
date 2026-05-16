using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// Reference implementation of <see cref="IJournalTaxLineGenerator"/>.
/// Walks the input lines, calls <see cref="ITaxCalculationService"/>
/// for each line carrying a <c>TaxCodeId</c>, and emits one tax-
/// payable journal line per <see cref="TaxRateBreakdownLine"/> in the
/// result.
///
/// <para>
/// Per-jurisdiction emission (rather than aggregated per-code) is the
/// canonical pattern per Stage 02 §6.4 — preserves the audit trail
/// the Schedule E generator + per-jurisdiction GL reports rely on.
/// </para>
/// </summary>
public sealed class JournalTaxLineGenerator : IJournalTaxLineGenerator
{
    private readonly ITaxCalculationService _calc;

    public JournalTaxLineGenerator(ITaxCalculationService calc)
    {
        _calc = calc ?? throw new ArgumentNullException(nameof(calc));
    }

    /// <inheritdoc />
    public async Task<JournalTaxLineGenerationResult> GenerateAsync(
        IReadOnlyList<FL.JournalEntryLine> preTaxLines,
        DateOnly transactionDate,
        TaxLocationContext location,
        CancellationToken cancellationToken = default)
    {
        if (preTaxLines is null) throw new ArgumentNullException(nameof(preTaxLines));
        if (location is null) throw new ArgumentNullException(nameof(location));

        var all = new List<FL.JournalEntryLine>(capacity: preTaxLines.Count * 2);
        var perLineResults = new List<TaxCalculationResult>();
        decimal totalTax = 0m;
        TaxCalculationError? firstError = null;
        string? firstErrorDetail = null;

        foreach (var line in preTaxLines)
        {
            all.Add(line);
            if (line.TaxCodeId is null) continue;

            // The ledger's TaxCodeId placeholder + the tax package's
            // canonical TaxCodeId are distinct C# types that both wrap
            // a string. Bridge by lifting the string. PR 5's downstream
            // sweep retires the placeholder; until then this conversion
            // is the documented interop point.
            var ledgerCodeIdString = line.TaxCodeId.Value.Value;
            var taxPackageCodeId = new TaxCodeId(ledgerCodeIdString);

            // Determine the pre-tax base for this line. A line's
            // Debit/Credit semantics depend on whether it's an AR/AP
            // entry — for invoice lines (Revenue credit) we treat the
            // Credit as the subtotal; for bill lines (Expense debit)
            // we treat the Debit as the subtotal. Whichever side is
            // non-zero IS the subtotal — the constructor guarantees
            // exactly one is.
            var subtotal = line.Debit != 0m ? line.Debit : line.Credit;

            var input = new TaxCalculationInput(
                TaxCodeId: taxPackageCodeId,
                Subtotal: subtotal,
                TransactionDate: transactionDate,
                Location: location);

            var result = await _calc.CalculateAsync(input, cancellationToken).ConfigureAwait(false);
            perLineResults.Add(result);

            if (result.Error != TaxCalculationError.None)
            {
                firstError ??= result.Error;
                firstErrorDetail ??= result.Detail;
                continue;
            }

            foreach (var breakdown in result.Breakdown)
            {
                if (breakdown.TaxAmount == 0m) continue;
                var taxLine = new FL.JournalEntryLine(
                    accountId: breakdown.PayableAccountId,
                    debit: 0m,
                    credit: breakdown.TaxAmount,
                    notes: $"Tax — {breakdown.JurisdictionLevel} ({breakdown.RatePercent:F4}%)")
                    {
                        // Propagate the originating TaxCodeId so a future
                        // audit / reverse-lookup can trace from a tax-
                        // payable line back to its source.
                        TaxCodeId = line.TaxCodeId,
                        // Inherit the source line's property/class
                        // dimensional tags so per-property GL splits
                        // remain coherent.
                        PropertyId = line.PropertyId,
                        ClassId = line.ClassId,
                    };
                all.Add(taxLine);
                totalTax += breakdown.TaxAmount;
            }
        }

        return new JournalTaxLineGenerationResult(
            AllLines: all,
            TotalTaxAmount: totalTax,
            PerLineResults: perLineResults,
            FirstError: firstError,
            Detail: firstErrorDetail);
    }
}

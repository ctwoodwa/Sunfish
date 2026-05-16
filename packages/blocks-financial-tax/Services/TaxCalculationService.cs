using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using Sunfish.Blocks.FinancialTax.Models;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// Reference implementation of <see cref="ITaxCalculationService"/>.
/// Stateless (apart from the injected stores); pure-function
/// algorithms; banker's rounding at 2-decimal boundary to prevent
/// systematic over- or under-collection.
///
/// <para>
/// Fiscal-correctness blast radius: any change here ripples through
/// every Invoice/Bill posting. Treat the regression battery in
/// <c>TaxCalculationServiceTests</c> as load-bearing — extend, don't
/// rewrite. Security-engineering council reviewed this PR per the
/// hand-off discipline.
/// </para>
/// </summary>
public sealed class TaxCalculationService : ITaxCalculationService
{
    private readonly ITaxRateLookup _rates;
    private readonly ITaxJurisdictionResolver _jurisdictions;
    private readonly ITaxCodeStore _codes;

    public TaxCalculationService(
        ITaxRateLookup rates,
        ITaxJurisdictionResolver jurisdictions,
        ITaxCodeStore codes)
    {
        _rates = rates ?? throw new ArgumentNullException(nameof(rates));
        _jurisdictions = jurisdictions ?? throw new ArgumentNullException(nameof(jurisdictions));
        _codes = codes ?? throw new ArgumentNullException(nameof(codes));
    }

    /// <inheritdoc />
    public async Task<TaxCalculationResult> CalculateAsync(
        TaxCalculationInput input,
        CancellationToken cancellationToken = default)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var code = await _codes.GetAsync(input.TaxCodeId, cancellationToken).ConfigureAwait(false);
        if (code is null)
        {
            return Fail(input, TaxCalculationError.TaxCodeNotFound,
                $"TaxCodeId {input.TaxCodeId} not found.");
        }

        // Exempt codes short-circuit: zero tax, empty breakdown, no
        // jurisdiction resolution. Audit-trail preserving — the line
        // still references an explicit TaxCode rather than null.
        if (code.Kind == TaxKind.Exempt)
        {
            return new TaxCalculationResult(
                SubtotalIn: input.Subtotal,
                TaxAmount: 0m,
                TotalIn: input.Subtotal,
                Breakdown: Array.Empty<TaxRateBreakdownLine>(),
                Error: TaxCalculationError.None,
                Detail: null);
        }

        var jurisdictions = await _jurisdictions.ResolveAsync(input.Location, cancellationToken).ConfigureAwait(false);
        var jurisdictionIds = jurisdictions.Select(j => j.Id).ToList();
        var applicableRates = await _rates.GetActiveRatesAsync(
            code.Id, input.TransactionDate, jurisdictionIds, cancellationToken).ConfigureAwait(false);

        if (applicableRates.Count == 0)
        {
            return Fail(input, TaxCalculationError.NoApplicableRates,
                $"No active rates for code {code.Code} on {input.TransactionDate} in jurisdictions [{string.Join(",", jurisdictions.Select(j => j.Name))}].");
        }

        // Pair each rate with its jurisdiction for breakdown metadata.
        var jurisLookup = jurisdictions.ToDictionary(j => j.Id);
        var withJur = applicableRates
            .Select(r => (Rate: r, Jur: jurisLookup[r.JurisdictionId]))
            .ToList();

        return code.Application switch
        {
            TaxApplication.OnSubtotal => ApplyOnSubtotal(input, withJur),
            TaxApplication.Compound => ApplyCompound(input, withJur),
            TaxApplication.Inclusive => input.Subtotal == 0m
                ? Fail(input, TaxCalculationError.InclusiveWithZeroSubtotal,
                       "Inclusive application requires non-zero subtotal (avoid /0).")
                : ApplyInclusive(input, withJur),
            _ => Fail(input, TaxCalculationError.UnknownApplication,
                     $"TaxApplication.{code.Application} not implemented."),
        };
    }

    // ── OnSubtotal: tax = subtotal * rate (each rate independently) ──────────

    private static TaxCalculationResult ApplyOnSubtotal(
        TaxCalculationInput input,
        IReadOnlyList<(TaxRate Rate, TaxJurisdiction Jur)> rates)
    {
        var breakdown = new List<TaxRateBreakdownLine>(capacity: rates.Count);
        decimal totalTax = 0m;
        foreach (var (r, j) in rates)
        {
            var taxAmt = RoundMinor(input.Subtotal * (r.RatePercent / 100m));
            breakdown.Add(new TaxRateBreakdownLine(
                TaxRateId: r.Id,
                JurisdictionId: j.Id,
                JurisdictionLevel: j.Level,
                RatePercent: r.RatePercent,
                TaxableBase: input.Subtotal,
                TaxAmount: taxAmt,
                PayableAccountId: r.PayableAccountId));
            totalTax += taxAmt;
        }
        return new TaxCalculationResult(
            SubtotalIn: input.Subtotal,
            TaxAmount: totalTax,
            TotalIn: input.Subtotal + totalTax,
            Breakdown: breakdown,
            Error: TaxCalculationError.None,
            Detail: null);
    }

    // ── Compound: tax_i = (subtotal + sum of prior taxes) * rate_i ───────────
    //              outermost jurisdiction first per Stage 02 §6.4.

    private static TaxCalculationResult ApplyCompound(
        TaxCalculationInput input,
        IReadOnlyList<(TaxRate Rate, TaxJurisdiction Jur)> rates)
    {
        var ordered = rates
            .OrderBy(t => t.Jur.Level.OrderIndex())
            .ThenBy(t => t.Jur.Id.Value, StringComparer.Ordinal)
            .ToList();

        var breakdown = new List<TaxRateBreakdownLine>(capacity: ordered.Count);
        decimal running = input.Subtotal;
        decimal totalTax = 0m;
        foreach (var (r, j) in ordered)
        {
            var taxAmt = RoundMinor(running * (r.RatePercent / 100m));
            breakdown.Add(new TaxRateBreakdownLine(
                TaxRateId: r.Id,
                JurisdictionId: j.Id,
                JurisdictionLevel: j.Level,
                RatePercent: r.RatePercent,
                TaxableBase: running,
                TaxAmount: taxAmt,
                PayableAccountId: r.PayableAccountId));
            totalTax += taxAmt;
            running += taxAmt;
        }
        return new TaxCalculationResult(
            SubtotalIn: input.Subtotal,
            TaxAmount: totalTax,
            TotalIn: input.Subtotal + totalTax,
            Breakdown: breakdown,
            Error: TaxCalculationError.None,
            Detail: null);
    }

    // ── Inclusive: subtotal already includes tax; back it out. ───────────────
    //              tax = subtotal * total_rate / (1 + total_rate)
    //              preTax = subtotal - tax
    //              Per-rate breakdown is pro-rated by share-of-total-rate,
    //              with the last row absorbing any rounding residual.

    private static TaxCalculationResult ApplyInclusive(
        TaxCalculationInput input,
        IReadOnlyList<(TaxRate Rate, TaxJurisdiction Jur)> rates)
    {
        var totalRatePct = rates.Sum(t => t.Rate.RatePercent);
        var totalRate = totalRatePct / 100m;
        var totalTax = RoundMinor(input.Subtotal * totalRate / (1m + totalRate));
        var preTaxBase = input.Subtotal - totalTax;

        var breakdown = new List<TaxRateBreakdownLine>(capacity: rates.Count);
        decimal allocated = 0m;
        for (int i = 0; i < rates.Count; i++)
        {
            var (r, j) = rates[i];
            decimal share;
            if (i == rates.Count - 1)
            {
                // Last row catches rounding residual so the breakdown
                // sum equals totalTax to the cent.
                share = totalTax - allocated;
            }
            else
            {
                // Pro-rate by share-of-total-rate. Guard against /0
                // even though the caller-side InclusiveWithZeroSubtotal
                // check should make totalRatePct > 0 here.
                share = totalRatePct == 0m
                    ? 0m
                    : RoundMinor(totalTax * (r.RatePercent / totalRatePct));
                allocated += share;
            }
            breakdown.Add(new TaxRateBreakdownLine(
                TaxRateId: r.Id,
                JurisdictionId: j.Id,
                JurisdictionLevel: j.Level,
                RatePercent: r.RatePercent,
                TaxableBase: preTaxBase,
                TaxAmount: share,
                PayableAccountId: r.PayableAccountId));
        }

        return new TaxCalculationResult(
            SubtotalIn: input.Subtotal,
            TaxAmount: totalTax,
            // Inclusive: gross total IS the subtotal (the tax is baked in).
            TotalIn: input.Subtotal,
            Breakdown: breakdown,
            Error: TaxCalculationError.None,
            Detail: null);
    }

    /// <summary>
    /// Banker's rounding (a.k.a. round-half-to-even, IEEE 754 default)
    /// at the 2-decimal minor-unit boundary. Prevents the systematic
    /// over-collection that simple round-half-up causes when half-cent
    /// values are common (e.g. 8.125% tax on a 100.00 amount yields
    /// 4.125; banker's rounds to 4.12, half-up would round to 4.13).
    /// </summary>
    private static decimal RoundMinor(decimal value) =>
        Math.Round(value, 2, MidpointRounding.ToEven);

    private static TaxCalculationResult Fail(
        TaxCalculationInput input,
        TaxCalculationError error,
        string? detail) =>
        new(SubtotalIn: input.Subtotal,
            TaxAmount: 0m,
            TotalIn: input.Subtotal,
            Breakdown: Array.Empty<TaxRateBreakdownLine>(),
            Error: error,
            Detail: detail);
}

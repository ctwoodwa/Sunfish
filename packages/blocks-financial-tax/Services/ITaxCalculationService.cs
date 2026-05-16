using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// Computes the tax due on a single line (invoice, bill, or
/// arbitrary subtotal+code pairing) per Stage 02 §6.4. Handles the
/// three <see cref="TaxApplication"/> modes — <c>OnSubtotal</c>,
/// <c>Compound</c>, and <c>Inclusive</c> — with banker's rounding at
/// the minor-unit boundary and a per-rate breakdown the caller can
/// split into GL postings.
///
/// <para>
/// Fiscal correctness matters here. The algorithm + tests in this
/// package are the canonical regression battery; downstream callers
/// (invoice posting, AR aging, Schedule E rollup) should not
/// re-implement.
/// </para>
/// </summary>
public interface ITaxCalculationService
{
    /// <summary>
    /// Compute the tax due for a single line. Always returns a
    /// <see cref="TaxCalculationResult"/> — failures are signalled
    /// via the <see cref="TaxCalculationResult.Error"/> field rather
    /// than thrown, so callers can branch deterministically.
    /// </summary>
    Task<TaxCalculationResult> CalculateAsync(
        TaxCalculationInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Input to <see cref="ITaxCalculationService.CalculateAsync"/>.
/// </summary>
/// <param name="TaxCodeId">Which code to apply.</param>
/// <param name="Subtotal">
/// Line amount in major units (e.g. <c>100.00m</c> USD). For
/// <see cref="TaxApplication.Inclusive"/> codes this is the gross
/// (price-with-tax-baked-in); for <see cref="TaxApplication.OnSubtotal"/>
/// and <see cref="TaxApplication.Compound"/> it's the pre-tax base.
/// </param>
/// <param name="TransactionDate">Used for <see cref="ITaxRateLookup"/> effective-date lookup.</param>
/// <param name="Location">Used for <see cref="ITaxJurisdictionResolver"/> resolution.</param>
public sealed record TaxCalculationInput(
    TaxCodeId TaxCodeId,
    decimal Subtotal,
    DateOnly TransactionDate,
    TaxLocationContext Location);

/// <summary>
/// Output of <see cref="ITaxCalculationService.CalculateAsync"/>.
/// </summary>
/// <param name="SubtotalIn">Echo of <see cref="TaxCalculationInput.Subtotal"/>.</param>
/// <param name="TaxAmount">Total tax due, rounded to minor units.</param>
/// <param name="TotalIn">
/// Gross total. For OnSubtotal / Compound: <c>SubtotalIn + TaxAmount</c>.
/// For Inclusive: the same as <c>SubtotalIn</c> (the tax is already baked in).
/// </param>
/// <param name="Breakdown">
/// Per-rate breakdown — one row per applicable rate. The sum of
/// <see cref="TaxRateBreakdownLine.TaxAmount"/> across rows equals
/// <see cref="TaxAmount"/> to the cent (Inclusive: last row absorbs
/// the rounding residual).
/// </param>
/// <param name="Error">Non-<see cref="TaxCalculationError.None"/> on failure.</param>
/// <param name="Detail">
/// <b>Internal / operator-facing only.</b> Carries identifiers
/// (TaxCodeId, jurisdiction names, account ids) that help diagnose
/// calculation failures in logs + admin UI. <b>Not safe to surface
/// to end-users without redaction</b> — leakage of internal graph
/// shape.
/// </param>
/// <param name="CalculatedAtUtc">
/// Wall-clock when the calculation ran. Downstream audit trails
/// (journal-entry attribution, Schedule E rollup) persist this so a
/// year-later reconstruction can prove which rate-snapshot applied.
/// </param>
/// <param name="TaxCodeVersion">
/// The <see cref="TaxCode.Version"/> at calc time. Combined with
/// <see cref="TaxRateBreakdownLine.TaxRateId"/> + <see cref="CalculatedAtUtc"/>
/// it lets a historical query identify the exact code-state used,
/// even if the code was edited (with version bumped) after the fact.
/// Zero for failure results (no code resolved).
/// </param>
public sealed record TaxCalculationResult(
    decimal SubtotalIn,
    decimal TaxAmount,
    decimal TotalIn,
    IReadOnlyList<TaxRateBreakdownLine> Breakdown,
    TaxCalculationError Error,
    string? Detail,
    DateTimeOffset CalculatedAtUtc,
    int TaxCodeVersion);

/// <summary>
/// One row of the per-rate breakdown — the data the downstream GL
/// poster needs to split a tax credit across jurisdictions.
/// </summary>
/// <param name="TaxRateId">Which rate row produced this slice.</param>
/// <param name="JurisdictionId">Which jurisdiction this slice belongs to.</param>
/// <param name="JurisdictionLevel">Federal/State/County/City — useful for reporting groupings.</param>
/// <param name="RatePercent">The rate value applied (e.g. <c>5.30m</c>).</param>
/// <param name="TaxableBase">
/// The base this row's tax was computed on. For OnSubtotal:
/// <c>input.Subtotal</c>. For Compound: <c>input.Subtotal + sum of
/// prior rows' TaxAmount</c>. For Inclusive: the pre-tax base
/// (<c>SubtotalIn - total tax</c>), same for all rows.
/// </param>
/// <param name="TaxAmount">This row's contribution to the total.</param>
/// <param name="PayableAccountId">Liability/TaxesPayable account to credit.</param>
public sealed record TaxRateBreakdownLine(
    TaxRateId TaxRateId,
    TaxJurisdictionId JurisdictionId,
    JurisdictionLevel JurisdictionLevel,
    decimal RatePercent,
    decimal TaxableBase,
    decimal TaxAmount,
    FL.GLAccountId PayableAccountId);

/// <summary>Structured failure modes for <see cref="ITaxCalculationService"/>.</summary>
public enum TaxCalculationError
{
    /// <summary>No error — calculation succeeded.</summary>
    None,
    /// <summary>The <see cref="TaxCalculationInput.TaxCodeId"/> doesn't resolve via the injected <see cref="ITaxCodeStore"/>.</summary>
    TaxCodeNotFound,
    /// <summary>The code resolved, but no rates are active for the resolved jurisdictions on <see cref="TaxCalculationInput.TransactionDate"/>.</summary>
    NoApplicableRates,
    /// <summary>The code is <see cref="TaxApplication.Inclusive"/> and the subtotal is zero — division-by-zero guard.</summary>
    InclusiveWithZeroSubtotal,
    /// <summary>
    /// <see cref="TaxCalculationInput.Subtotal"/> is negative. Engine
    /// rejects so credit-memo / reversal flows must use an explicit
    /// reversal path rather than feeding negative subtotals into the
    /// standard calc engine (silent negative-tax production is a
    /// fiscal hazard).
    /// </summary>
    InvalidSubtotal,
    /// <summary>Forward-compat: a new <see cref="TaxApplication"/> enum member exists that this engine doesn't know how to apply.</summary>
    UnknownApplication,
}

using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// A tax rate effective for a (TaxCode, Jurisdiction) pair over a date
/// range, per Stage 02 §3.13.
///
/// <para>
/// <b>Append-only per CRDT §4</b> — rate changes are NEW rows (new
/// <see cref="Id"/>, new <see cref="EffectiveDate"/>, and the
/// preceding row's <see cref="ExpiryDate"/> populated to the day
/// before). Editing a rate in place is forbidden; use
/// <c>ITaxRateLookup.SupersedeAsync</c> for the atomic expire-and-
/// insert operation.
/// </para>
///
/// <para>
/// Constructor validation:
/// </para>
/// <list type="number">
///   <item><see cref="RatePercent"/> must be in <c>[0, 100]</c>.</item>
///   <item><see cref="ExpiryDate"/> (if set) must be on or after <see cref="EffectiveDate"/>.</item>
/// </list>
/// <para>
/// Service-layer validation lives on <c>ITaxRateLookup.UpsertAsync</c>:
/// non-overlapping date ranges per (TaxCode, Jurisdiction), and
/// <see cref="PayableAccountId"/> resolves to a
/// <see cref="GLAccountType"/> = Liability account with subtype
/// <see cref="AccountSubtype"/> = TaxesPayable.
/// </para>
/// </summary>
/// <param name="Id">Stable identity.</param>
/// <param name="TaxCodeId">Owning tax code (FK).</param>
/// <param name="JurisdictionId">Jurisdiction this rate applies in (FK).</param>
/// <param name="RatePercent">Percentage, <c>0..100</c>; five-decimal precision (e.g. <c>8.25000</c>).</param>
/// <param name="EffectiveDate">First day the rate applies (inclusive).</param>
/// <param name="ExpiryDate">Last day the rate applies (inclusive); <c>null</c> = open-ended.</param>
/// <param name="PayableAccountId">Liability/TaxesPayable account this rate accrues to (FK).</param>
/// <param name="Description">Free-form operator notes.</param>
/// <param name="CreatedAtUtc">When the row was first written.</param>
/// <param name="DeletedAtUtc">Tombstone — set only for erroneous adds (rare; audit-trail-preserving).</param>
public sealed record TaxRate(
    TaxRateId Id,
    TaxCodeId TaxCodeId,
    TaxJurisdictionId JurisdictionId,
    decimal RatePercent,
    DateOnly EffectiveDate,
    DateOnly? ExpiryDate,
    GLAccountId PayableAccountId,
    string? Description,
    Instant? CreatedAtUtc = null,
    Instant? DeletedAtUtc = null)
{
    /// <summary>
    /// Create a freshly-stamped <see cref="TaxRate"/>. Validates the
    /// rate range + effective/expiry ordering. Service-layer validation
    /// (date-range overlap, payable-account type/subtype) runs on
    /// <c>ITaxRateLookup.UpsertAsync</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When <paramref name="ratePercent"/> falls outside <c>[0, 100]</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// When <paramref name="expiryDate"/> precedes <paramref name="effectiveDate"/>.
    /// </exception>
    public static TaxRate Create(
        TaxCodeId taxCodeId,
        TaxJurisdictionId jurisdictionId,
        decimal ratePercent,
        DateOnly effectiveDate,
        GLAccountId payableAccountId,
        DateOnly? expiryDate = null,
        string? description = null,
        Instant? createdAtUtc = null)
    {
        if (ratePercent < 0m || ratePercent > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ratePercent),
                ratePercent,
                "TaxRate.ratePercent must be in [0, 100].");
        }
        if (expiryDate is not null && expiryDate < effectiveDate)
        {
            throw new ArgumentException(
                $"TaxRate expiryDate ({expiryDate}) must be on or after effectiveDate ({effectiveDate}).",
                nameof(expiryDate));
        }

        return new TaxRate(
            Id: TaxRateId.NewId(),
            TaxCodeId: taxCodeId,
            JurisdictionId: jurisdictionId,
            RatePercent: ratePercent,
            EffectiveDate: effectiveDate,
            ExpiryDate: expiryDate,
            PayableAccountId: payableAccountId,
            Description: description,
            CreatedAtUtc: createdAtUtc ?? Instant.Now);
    }

    /// <summary>True iff this rate applies on the given date (not tombstoned, within the effective-to-expiry window).</summary>
    public bool IsActiveOn(DateOnly date) =>
        DeletedAtUtc is null
        && EffectiveDate <= date
        && (ExpiryDate is null || ExpiryDate >= date);
}

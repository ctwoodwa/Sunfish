using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// Query + upsert surface for <see cref="TaxRate"/> rows. The PR 3
/// calculation engine consumes <see cref="GetActiveRatesAsync"/> as
/// its rate-source; admin / importer code paths use
/// <see cref="UpsertAsync"/> + <see cref="SupersedeAsync"/>.
///
/// <para>
/// PR 2 ships the in-memory implementation only. The SQLite-backed
/// implementation lands in a later persistence-layer hand-off; the
/// in-memory <see cref="SupersedeAsync"/> holds a per-(TaxCode,
/// Jurisdiction) lock for atomicity, the SQLite version will use a
/// transaction.
/// </para>
/// </summary>
public interface ITaxRateLookup
{
    /// <summary>
    /// Returns the active rate(s) for a <see cref="TaxCode"/> on the
    /// given date, filtered to the jurisdiction set (typically from
    /// <see cref="ITaxJurisdictionResolver.ResolveAsync"/>).
    /// </summary>
    Task<IReadOnlyList<TaxRate>> GetActiveRatesAsync(
        TaxCodeId taxCodeId,
        DateOnly date,
        IReadOnlyCollection<TaxJurisdictionId> jurisdictionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full effective-dated history for a (<see cref="TaxCode"/>,
    /// <see cref="TaxJurisdiction"/>) pair, ordered oldest-first by
    /// <see cref="TaxRate.EffectiveDate"/>. Includes expired and
    /// tombstoned rows so the caller can audit the full timeline.
    /// </summary>
    Task<IReadOnlyList<TaxRate>> GetHistoryAsync(
        TaxCodeId taxCodeId,
        TaxJurisdictionId jurisdictionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every <see cref="TaxRate"/> for a <see cref="TaxCode"/>
    /// across all jurisdictions, ordered by (JurisdictionId,
    /// EffectiveDate). Backs the <c>TaxCode.GetRatesAsync</c> derived
    /// accessor.
    /// </summary>
    Task<IReadOnlyList<TaxRate>> GetAllForTaxCodeAsync(
        TaxCodeId taxCodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new <see cref="TaxRate"/> row. Validates:
    /// <list type="bullet">
    ///   <item>Non-overlapping date range against existing rates for
    ///         (<paramref name="candidate"/>.<see cref="TaxRate.TaxCodeId"/>,
    ///         <paramref name="candidate"/>.<see cref="TaxRate.JurisdictionId"/>).</item>
    ///   <item><see cref="TaxRate.PayableAccountId"/> resolves to an
    ///         account of type <see cref="Sunfish.Blocks.FinancialLedger.Models.GLAccountType.Liability"/>
    ///         and subtype <see cref="Sunfish.Blocks.FinancialLedger.Models.AccountSubtype.TaxesPayable"/>.</item>
    /// </list>
    /// Validation failure returns a structured
    /// <see cref="TaxRateUpsertResult"/> with a non-<c>None</c>
    /// <see cref="TaxRateValidationError"/> rather than throwing.
    /// </summary>
    Task<TaxRateUpsertResult> UpsertAsync(
        TaxRate candidate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically supersedes the currently-active rate for
    /// (<paramref name="taxCodeId"/>, <paramref name="jurisdictionId"/>)
    /// with a new rate effective on <paramref name="newEffectiveDate"/>:
    /// <list type="number">
    ///   <item>Expires the current rate by setting
    ///         <see cref="TaxRate.ExpiryDate"/> to
    ///         <c>newEffectiveDate.AddDays(-1)</c>.</item>
    ///   <item>Inserts the new rate.</item>
    /// </list>
    /// Both writes are atomic: a failure on the insert step rolls back
    /// the expiry. Emits <c>Financial.TaxRateAdded</c> +
    /// <c>Financial.TaxRateExpired</c> events (event emission lands in
    /// PR 5 of the blocks-financial-tax-stage06-handoff).
    /// </summary>
    Task<TaxRateSupersedeResult> SupersedeAsync(
        TaxCodeId taxCodeId,
        TaxJurisdictionId jurisdictionId,
        decimal newRatePercent,
        DateOnly newEffectiveDate,
        FL.GLAccountId payableAccountId,
        CancellationToken cancellationToken = default);
}

/// <summary>Structured validation outcome for <see cref="ITaxRateLookup"/> writes.</summary>
public enum TaxRateValidationError
{
    /// <summary>No error — operation succeeded.</summary>
    None,
    /// <summary>The candidate's effective-to-expiry range overlaps an existing rate for the same (TaxCode, Jurisdiction).</summary>
    DateRangeOverlap,
    /// <summary>The candidate's <see cref="TaxRate.PayableAccountId"/> doesn't resolve via the injected <c>IAccountResolver</c>.</summary>
    PayableAccountNotFound,
    /// <summary>The resolved account isn't <see cref="Sunfish.Blocks.FinancialLedger.Models.GLAccountType.Liability"/>.</summary>
    PayableAccountWrongType,
    /// <summary>The resolved account is Liability but not <see cref="Sunfish.Blocks.FinancialLedger.Models.AccountSubtype.TaxesPayable"/>.</summary>
    PayableAccountWrongSubtype,
    /// <summary><see cref="ITaxRateLookup.SupersedeAsync"/> was called but no active rate exists to supersede.</summary>
    NoActiveRateToSupersede,
}

/// <summary>
/// Result of <see cref="ITaxRateLookup.UpsertAsync"/>. On success
/// <see cref="Rate"/> carries the persisted row; on failure
/// <see cref="Error"/> identifies the rule that fired and
/// <see cref="Detail"/> carries human-readable context.
/// </summary>
public readonly record struct TaxRateUpsertResult(
    TaxRate? Rate,
    TaxRateValidationError Error,
    string? Detail);

/// <summary>
/// Result of <see cref="ITaxRateLookup.SupersedeAsync"/>. On success
/// both <see cref="OldRate"/> (now expired) and <see cref="NewRate"/>
/// are populated; on failure neither write is persisted.
/// </summary>
public readonly record struct TaxRateSupersedeResult(
    TaxRate? OldRate,
    TaxRate? NewRate,
    TaxRateValidationError Error,
    string? Detail);

using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// A single node in the general ledger chart of accounts.
/// </summary>
/// <param name="Id">Unique account identifier.</param>
/// <param name="Code">
/// Human-readable account code, e.g. <c>"4000"</c> for Rental Revenue.
/// Codes must be unique within a chart of accounts.
/// </param>
/// <param name="Name">Display name of the account, e.g. <c>"Rental Revenue"</c>.</param>
/// <param name="Type">Accounting category (Asset, Liability, Equity, Revenue, Expense).</param>
/// <param name="ParentAccountId">
/// Optional reference to a parent account, enabling hierarchical chart-of-accounts structures.
/// <see langword="null"/> for top-level accounts.
/// </param>
/// <param name="ChartId">
/// FK to the <see cref="ChartOfAccounts"/> this account belongs to. Optional in PR 2
/// (existing call sites do not yet supply it); becomes mandatory once
/// <see cref="ChartOfAccounts"/> registration enforces the FK at service-level.
/// </param>
/// <param name="Subtype">
/// Sub-classification per Stage 02 §3.1 (e.g. <see cref="AccountSubtype.BankAccount"/>,
/// <see cref="AccountSubtype.AccumulatedDepreciation"/>). Drives financial-statement
/// presentation grouping.
/// </param>
/// <param name="NormalBalance">
/// The side the balance normally accumulates on. Caller-provided so historical
/// accounts with non-standard normal balances (e.g. contra-asset
/// <see cref="AccountSubtype.AccumulatedDepreciation"/> with credit-normal balance)
/// can be modeled. <see cref="GLAccount.Create"/> derives the default per
/// <see cref="GLAccountType"/>.
/// </param>
/// <param name="Description">Optional human-readable description shown in admin UI.</param>
/// <param name="Currency">
/// ISO 4217 currency code, e.g. <c>"USD"</c>. Inherits from
/// <see cref="ChartOfAccounts.BaseCurrency"/> for single-currency installs;
/// multi-currency accounts override.
/// </param>
/// <param name="IsActive">Soft-delete flag.</param>
/// <param name="IsPostable">
/// <c>false</c> for header / summary nodes that should not receive journal-entry
/// postings directly.
/// </param>
/// <param name="TaxLineMappingId">Optional FK to the tax-line mapping for reporting.</param>
/// <param name="ExternalRef">Optional external system reference (e.g. ERPNext import id).</param>
/// <param name="CreatedAtUtc">Creation timestamp.</param>
/// <param name="UpdatedAtUtc">Last-mutation timestamp.</param>
public sealed record GLAccount(
    GLAccountId Id,
    string Code,
    string Name,
    GLAccountType Type,
    GLAccountId? ParentAccountId = null,
    // PR 2 extensions per Stage 02 §3.1 — all optional with defaults so the
    // pre-PR-2 constructor signature continues to compile unchanged.
    ChartOfAccountsId? ChartId = null,
    AccountSubtype? Subtype = null,
    NormalBalance? NormalBalance = null,
    string? Description = null,
    string? Currency = null,
    bool IsActive = true,
    bool IsPostable = true,
    string? TaxLineMappingId = null,
    string? ExternalRef = null,
    Instant? CreatedAtUtc = null,
    Instant? UpdatedAtUtc = null)
{
    /// <summary>
    /// Build a well-formed <see cref="GLAccount"/> with a derived
    /// <see cref="NormalBalance"/> matching the supplied <see cref="GLAccountType"/>
    /// (Asset/Expense → Debit; Liability/Equity/Revenue → Credit).
    /// Contra-balance accounts (e.g. AccumulatedDepreciation) should construct
    /// directly with an explicit <c>NormalBalance</c> override.
    /// </summary>
    public static GLAccount Create(
        GLAccountId id,
        ChartOfAccountsId chartId,
        string code,
        string name,
        GLAccountType type,
        AccountSubtype subtype,
        string currency,
        GLAccountId? parentAccountId = null,
        bool isPostable = true,
        string? description = null,
        string? taxLineMappingId = null,
        string? externalRef = null,
        Instant? createdAtUtc = null)
    {
        var normal = type switch
        {
            GLAccountType.Asset or GLAccountType.Expense => Models.NormalBalance.Debit,
            _ => Models.NormalBalance.Credit,
        };
        var now = createdAtUtc ?? Instant.Now;
        return new GLAccount(
            Id: id,
            Code: code,
            Name: name,
            Type: type,
            ParentAccountId: parentAccountId,
            ChartId: chartId,
            Subtype: subtype,
            NormalBalance: normal,
            Description: description,
            Currency: currency,
            IsActive: true,
            IsPostable: isPostable,
            TaxLineMappingId: taxLineMappingId,
            ExternalRef: externalRef,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    /// <summary>
    /// Run the Stage 02 §3.1 invariants over this account, optionally against
    /// a supplied <paramref name="parent"/>. The validation is opt-in (the
    /// constructor does not invoke it) — <see cref="Services.IAccountingService"/>
    /// callers invoke <see cref="Validate"/> at registration time; tests
    /// invoke it directly to assert rule coverage.
    /// </summary>
    public AccountValidationResult Validate(GLAccount? parent = null)
    {
        var errors = new List<string>();

        // Rule 1: NormalBalance matches Type when supplied. (When null, the
        // Create factory would have derived it — skip this rule for callers
        // who chose to omit it on the positional constructor.)
        if (NormalBalance is { } nb)
        {
            var expected = Type switch
            {
                GLAccountType.Asset or GLAccountType.Expense => Models.NormalBalance.Debit,
                _ => Models.NormalBalance.Credit,
            };
            if (nb != expected
                // Allow contra-balance subtypes (e.g. AccumulatedDepreciation
                // is Asset/Credit-normal — opposite of the default).
                && !IsContraBalanceSubtype(Subtype))
            {
                errors.Add(
                    $"NormalBalance {nb} does not match the default for Type {Type} (expected {expected}). "
                    + "Use a contra-balance Subtype if this is intentional.");
            }
        }

        // Rule 2: parent FK consistency.
        if (parent is not null)
        {
            if (parent.Type != Type)
            {
                errors.Add($"Parent account Type {parent.Type} differs from child Type {Type}.");
            }
            if (parent.ChartId is { } parentChart && ChartId is { } childChart
                && !parentChart.Equals(childChart))
            {
                errors.Add($"Parent ChartId {parentChart} differs from child ChartId {childChart}.");
            }
        }

        // Rule 3: ISO 4217 currency length.
        if (Currency is not null && Currency.Length != 3)
        {
            errors.Add(
                $"Currency must be a 3-letter ISO 4217 code (got '{Currency}', length {Currency.Length}).");
        }

        return errors.Count == 0 ? AccountValidationResult.Ok : AccountValidationResult.Fail(errors);
    }

    private static bool IsContraBalanceSubtype(AccountSubtype? subtype) => subtype switch
    {
        AccountSubtype.AccumulatedDepreciation => true,
        _ => false,
    };
}

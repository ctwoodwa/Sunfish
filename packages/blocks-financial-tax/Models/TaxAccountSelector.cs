namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// A selector that picks zero or more general-ledger accounts for
/// inclusion under a single <see cref="TaxFormLineMap"/> line. Per
/// <c>blocks-reports-schema-design.md</c> §3.
///
/// <para>
/// Composition: a <see cref="TaxFormLineMap"/> carries an ordered
/// list of selectors; the Schedule E generator (downstream, ships in
/// <c>Sunfish.Blocks.Reports.Tax</c>) walks each candidate
/// <see cref="GLAccountReference"/> and includes it in the line's aggregation
/// iff any selector returns <c>true</c> from <see cref="Matches"/>.
/// Use <see cref="Invert"/> on a selector to express "exclude".
/// </para>
/// </summary>
/// <param name="AccountCode">Exact chart-of-accounts code (e.g. <c>"5100"</c>).</param>
/// <param name="AccountCodePrefix">Prefix match (e.g. <c>"61"</c> = all utility-prefixed codes).</param>
/// <param name="AccountTag">Tag/category lookup (consumer-defined tag set on the account).</param>
/// <param name="Invert">When true, the selector excludes the matched account rather than including it.</param>
public sealed record TaxAccountSelector(
    string? AccountCode = null,
    string? AccountCodePrefix = null,
    string? AccountTag = null,
    bool Invert = false)
{
    /// <summary>
    /// True iff the underlying selector (code / prefix / tag) matches
    /// <paramref name="account"/>. With <see cref="Invert"/> set, the
    /// result is flipped. A selector with no fields set never matches.
    /// </summary>
    public bool Matches(GLAccountReference account)
    {
        if (account is null) return false;
        bool hit = false;
        if (AccountCode is not null && string.Equals(account.Code, AccountCode, StringComparison.Ordinal))
        {
            hit = true;
        }
        else if (AccountCodePrefix is not null && account.Code.StartsWith(AccountCodePrefix, StringComparison.Ordinal))
        {
            hit = true;
        }
        else if (AccountTag is not null && account.Tags.Contains(AccountTag, StringComparer.Ordinal))
        {
            hit = true;
        }
        return Invert ? !hit : hit;
    }
}

/// <summary>
/// Minimal projection of a <c>GLAccount</c> that <see cref="TaxAccountSelector"/>
/// matches against. Decouples the selector from the full account
/// shape (which lives in <c>blocks-financial-ledger</c>) so the
/// Schedule E generator can synthesize tagging from whatever tag
/// surface the consumer prefers (account-metadata column, sidecar
/// table, whatever).
/// </summary>
/// <param name="Code">Chart-of-accounts code (e.g. <c>"5100"</c>).</param>
/// <param name="Tags">Tag set; empty when the consumer has no tag layer yet.</param>
public sealed record GLAccountReference(string Code, IReadOnlyCollection<string> Tags);

using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// A mapping from a chart-of-accounts subset (described by
/// <see cref="TaxAccountSelector"/> rows) to a specific line on a
/// specific tax form for a specific year. Per
/// <c>blocks-reports-schema-design.md</c> §3 + §8.
///
/// <para>
/// Authoring discipline: rows are seeded provisionally from IRS Pub
/// 527 + Schedule E (Form 1040) instructions. ONR research will
/// later ratify each row (flip <see cref="IsProvisional"/> to
/// <c>false</c>) and may add/remove/edit rows. Until ONR lands,
/// every row carries <see cref="IsProvisional"/> <c>= true</c> and
/// a <see cref="ProvisionalRationale"/> pointing at the ONR file
/// path. User edits are permitted but should bump
/// <see cref="Version"/> per CRDT-conventions §3.
/// </para>
///
/// <para>
/// CRDT conventions applied:
/// </para>
/// <list type="bullet">
///   <item>§1 — Guid id via <see cref="TaxFormLineMapId"/>.</item>
///   <item>§2 — <see cref="DeletedAtUtc"/> tombstone (soft-delete).</item>
///   <item>§3 — <see cref="Version"/> bumped on each upsert.</item>
///   <item>§5 — <see cref="TaxFormKind"/> stable string code.</item>
/// </list>
/// </summary>
/// <param name="Id">Stable identity.</param>
/// <param name="ChartId">Owning chart of accounts (FK).</param>
/// <param name="FormKind">Which form this row maps onto.</param>
/// <param name="TaxYear">Which form year (forms change year-over-year).</param>
/// <param name="Line">Form-line key (e.g. <c>"Line5"</c>, <c>"Line22"</c>).</param>
/// <param name="Description">Human-readable line label (e.g. <c>"Advertising"</c>).</param>
/// <param name="AccountSelectors">Ordered list of selectors that pick accounts under this line.</param>
/// <param name="PerPropertyDimension">
/// When true, the line is aggregated per-property (Schedule E lines
/// 3-19); when false the line is portfolio-wide (rare).
/// </param>
/// <param name="IsProvisional">
/// True iff the row was seeded pending ONR ratification. Flipped to
/// false when ONR confirms; user edits preserve the bit unless the
/// user explicitly ratifies via admin UI.
/// </param>
/// <param name="ProvisionalRationale">Free-form note explaining the provisional status (e.g. ONR file path).</param>
/// <param name="CitationSource">IRS publication + line number citation (audit trail).</param>
/// <param name="IsActive">When false, the row is retained but excluded from aggregation.</param>
/// <param name="Version">CRDT §3 version vector. Starts at 1; bump on each upsert.</param>
/// <param name="CreatedAtUtc">When the row was first written.</param>
/// <param name="UpdatedAtUtc">When the row was last upserted.</param>
/// <param name="DeletedAtUtc">Tombstone — when set, the row is soft-deleted.</param>
public sealed record TaxFormLineMap(
    TaxFormLineMapId Id,
    FL.ChartOfAccountsId ChartId,
    TaxFormKind FormKind,
    int TaxYear,
    string Line,
    string Description,
    IReadOnlyList<TaxAccountSelector> AccountSelectors,
    bool PerPropertyDimension,
    bool IsProvisional,
    string? ProvisionalRationale,
    string? CitationSource,
    bool IsActive = true,
    int Version = 1,
    Instant? CreatedAtUtc = null,
    Instant? UpdatedAtUtc = null,
    Instant? DeletedAtUtc = null)
{
    /// <summary>
    /// Create a freshly-stamped <see cref="TaxFormLineMap"/>. New id,
    /// version 1, created+updated timestamps matched. Callers with a
    /// stable id (e.g. an ERPNext importer) should construct the
    /// record directly.
    /// </summary>
    public static TaxFormLineMap Create(
        FL.ChartOfAccountsId chartId,
        TaxFormKind formKind,
        int taxYear,
        string line,
        string description,
        IReadOnlyList<TaxAccountSelector> selectors,
        bool perPropertyDimension,
        bool isProvisional,
        string? provisionalRationale = null,
        string? citationSource = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new TaxFormLineMap(
            Id: TaxFormLineMapId.NewId(),
            ChartId: chartId,
            FormKind: formKind,
            TaxYear: taxYear,
            Line: line,
            Description: description,
            AccountSelectors: selectors,
            PerPropertyDimension: perPropertyDimension,
            IsProvisional: isProvisional,
            ProvisionalRationale: provisionalRationale,
            CitationSource: citationSource,
            IsActive: true,
            Version: 1,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }
}

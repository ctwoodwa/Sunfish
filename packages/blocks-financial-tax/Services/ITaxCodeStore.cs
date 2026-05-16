using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// CRUD surface for <see cref="TaxCode"/> rows — sibling to
/// <see cref="ITaxJurisdictionStore"/>. The calculation engine (see
/// <see cref="ITaxCalculationService"/>) resolves codes via
/// <see cref="GetAsync"/>; admin / importer code paths use
/// <see cref="UpsertAsync"/>.
///
/// <para>
/// PR 3 ships the in-memory implementation only. The SQLite-backed
/// implementation lands in a later persistence-layer hand-off.
/// </para>
/// </summary>
public interface ITaxCodeStore
{
    /// <summary>Look up by id. Returns null when missing or soft-deleted.</summary>
    Task<TaxCode?> GetAsync(TaxCodeId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Look up by (chart, code) — the human-stable surface used by
    /// the ERPNext importer to match incoming rows against existing
    /// codes. Returns null when missing or soft-deleted.
    /// </summary>
    Task<TaxCode?> GetByCodeAsync(FL.ChartOfAccountsId chartId, string code, CancellationToken cancellationToken = default);

    /// <summary>All active codes in the chart. Soft-deleted rows excluded.</summary>
    Task<IReadOnlyList<TaxCode>> GetByChartAsync(FL.ChartOfAccountsId chartId, CancellationToken cancellationToken = default);

    /// <summary>Insert or update by id. Implementations bump <c>UpdatedAtUtc</c> + <c>Version</c>.</summary>
    Task UpsertAsync(TaxCode taxCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-delete by id. Idempotent: deleting an already-deleted row
    /// or a non-existent id is a no-op (no exception).
    /// </summary>
    Task SoftDeleteAsync(TaxCodeId id, Instant deletedAtUtc, CancellationToken cancellationToken = default);
}

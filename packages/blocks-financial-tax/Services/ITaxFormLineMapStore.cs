using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// CRUD + query surface for <see cref="TaxFormLineMap"/> rows.
/// Consumers: the Schedule E generator (in
/// <c>Sunfish.Blocks.Reports.Tax</c>) reads via
/// <see cref="GetForFormAsync"/>; admin / setup wizards seed via
/// <see cref="SeedScheduleEAsync"/>.
///
/// <para>
/// PR 4 ships the in-memory implementation only. SQLite-backed
/// implementation lands in a later persistence-layer hand-off.
/// </para>
/// </summary>
public interface ITaxFormLineMapStore
{
    /// <summary>Look up by id. Returns null when missing or soft-deleted.</summary>
    Task<TaxFormLineMap?> GetAsync(TaxFormLineMapId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// All active rows for a given chart + form + tax year, ordered
    /// by the form-line number ascending (lexicographic on the
    /// <c>Line</c> field, which uses zero-padded sortable form like
    /// "Line03" or unpadded "Line3" — implementations may sort
    /// either way; the canonical seed uses unpadded). Soft-deleted
    /// rows are excluded.
    /// </summary>
    Task<IReadOnlyList<TaxFormLineMap>> GetForFormAsync(
        FL.ChartOfAccountsId chartId,
        TaxFormKind formKind,
        int taxYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert or update by id. Implementations bump
    /// <see cref="TaxFormLineMap.UpdatedAtUtc"/> + <c>Version</c>.
    /// </summary>
    Task UpsertAsync(TaxFormLineMap map, CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently insert the canonical Schedule E seed (from
    /// <see cref="Seeds.DefaultTaxFormLineMap.ScheduleE"/>) for a
    /// given chart + tax year. Returns the number of rows inserted —
    /// <c>0</c> when the (chart, ScheduleE, year) trio is already
    /// seeded (preserves any user edits).
    /// </summary>
    Task<int> SeedScheduleEAsync(
        FL.ChartOfAccountsId chartId,
        int taxYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-delete by id. Idempotent: deleting an already-deleted row
    /// or a non-existent id is a no-op (no exception).
    /// </summary>
    Task SoftDeleteAsync(TaxFormLineMapId id, Instant deletedAtUtc, CancellationToken cancellationToken = default);
}

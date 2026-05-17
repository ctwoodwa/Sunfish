using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialAp.Services;

/// <summary>
/// CRUD surface over the AP bill substrate. Mirrors AR's
/// <c>IInvoiceRepository</c> with vendor-specific query methods
/// (lookup by vendor + vendor-supplied bill number, list open bills
/// per vendor, etc.).
/// </summary>
public interface IBillRepository
{
    /// <summary>Insert or update a bill. Throws on a tombstoned target.</summary>
    Task UpsertAsync(Bill bill, CancellationToken cancellationToken = default);

    /// <summary>Get a bill by id. Returns null when missing OR tombstoned.</summary>
    Task<Bill?> GetAsync(BillId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find a bill by the vendor's own number, scoped to (chart, vendor).
    /// The composite key — bills don't carry a Sunfish-minted number — is
    /// what catches duplicate-bill submissions from the same vendor.
    /// </summary>
    Task<Bill?> GetByVendorBillNumberAsync(
        ChartOfAccountsId chartId,
        PartyId vendorId,
        string billNumber,
        CancellationToken cancellationToken = default);

    /// <summary>Find a bill by its external-ref tag (e.g. ERPNext sync).</summary>
    Task<Bill?> GetByExternalRefAsync(
        ChartOfAccountsId chartId,
        string externalRef,
        CancellationToken cancellationToken = default);

    /// <summary>List all live (non-tombstoned) bills in a chart.</summary>
    Task<IReadOnlyList<Bill>> ListByChartAsync(ChartOfAccountsId chartId, CancellationToken cancellationToken = default);

    /// <summary>List all live bills for a given vendor in a chart.</summary>
    Task<IReadOnlyList<Bill>> ListByVendorAsync(ChartOfAccountsId chartId, PartyId vendorId, CancellationToken cancellationToken = default);

    /// <summary>List bills currently in an Open state (Received / Approved / PartiallyPaid). Optionally filter by vendor / property.</summary>
    Task<IReadOnlyList<Bill>> QueryOpenAsync(
        ChartOfAccountsId chartId,
        PartyId? vendorId = null,
        string? propertyId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tombstone a bill (sets <c>DeletedAtUtc</c> + <c>DeletedReason</c>).
    /// Idempotent. Returns false when the id is unknown.
    /// </summary>
    Task<bool> SoftDeleteAsync(BillId id, PartyId actor, string? reason, CancellationToken cancellationToken = default);
}

using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAr.Services;

/// <summary>
/// CRUD surface over the AR invoice substrate. Persistence-backed
/// implementations (SQLite, Postgres) ship in a follow-on substrate
/// hand-off; <see cref="InMemoryInvoiceRepository"/> backs the v1 desktop
/// path. Posting / numbering / aging / event emission ride on top in PRs
/// 2–4.
/// </summary>
public interface IInvoiceRepository
{
    /// <summary>Insert or update an invoice. Throws on a tombstoned target.</summary>
    Task UpsertAsync(Invoice invoice, CancellationToken cancellationToken = default);

    /// <summary>Get an invoice by id. Returns null when missing OR tombstoned.</summary>
    Task<Invoice?> GetAsync(InvoiceId id, CancellationToken cancellationToken = default);

    /// <summary>Find an invoice by its customer-facing number, scoped to a chart.</summary>
    Task<Invoice?> GetByNumberAsync(ChartOfAccountsId chartId, string invoiceNumber, CancellationToken cancellationToken = default);

    /// <summary>List all live (non-tombstoned) invoices in a chart.</summary>
    Task<IReadOnlyList<Invoice>> ListByChartAsync(ChartOfAccountsId chartId, CancellationToken cancellationToken = default);

    /// <summary>List all live invoices for a given customer in a chart.</summary>
    Task<IReadOnlyList<Invoice>> ListByCustomerAsync(ChartOfAccountsId chartId, PartyId customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tombstone an invoice (sets <c>DeletedAtUtc</c> + <c>DeletedReason</c>). Idempotent:
    /// a second call on an already-deleted row is a no-op. Returns false when the id is unknown.
    /// </summary>
    Task<bool> SoftDeleteAsync(InvoiceId id, PartyId actor, string? reason, CancellationToken cancellationToken = default);
}

using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAr.Migration;

/// <summary>
/// Upsert ERPNext <c>Sales Invoice</c> records into the canonical AR
/// substrate. Pass-2 importer (the AR side); customer resolution lives
/// upstream in <c>blocks-people-foundation.IErpnextPartyImporter</c>'s
/// pass-1 — by the time this importer runs, the caller already holds
/// the canonical <see cref="PartyId"/> for the source's
/// <c>customer</c>.
///
/// <para>
/// <b>Idempotency:</b> the importer marks every imported invoice with
/// <c>ExternalRef = "erpnext:sinv:{Name}"</c> (the FK) and stores
/// <c>erpnextModified:{Modified}</c> in <see cref="Invoice.Notes"/>.
/// Re-running with the same <c>Modified</c> returns <c>Skipped</c>;
/// a newer <c>Modified</c> returns <c>Updated</c> after rewriting
/// the canonical row.
/// </para>
///
/// <para>
/// <b>No GL posting in v1.</b> Imported invoices arrive at the AR
/// substrate already-balanced from ERPNext's perspective; we do NOT
/// re-post a journal entry. If the host wants the canonical GL to
/// mirror ERPNext's GL, that's a separate ledger-side migration
/// importer flow.
/// </para>
/// </summary>
public interface IErpnextSalesInvoiceImporter
{
    /// <summary>
    /// Upsert a Sales Invoice. Caller supplies cross-cluster fields
    /// (chart, customer party, AR account, default income account)
    /// that ERPNext's payload can't carry directly.
    /// </summary>
    /// <param name="source">The ERPNext payload.</param>
    /// <param name="tenantId">Multi-tenant scope.</param>
    /// <param name="chartId">Chart-of-accounts this invoice posts against.</param>
    /// <param name="customerPartyId">Canonical Party id for the ERPNext customer (resolved upstream).</param>
    /// <param name="arAccountId">AR control account.</param>
    /// <param name="defaultIncomeAccountId">Income account used when a source line's <c>IncomeAccount</c> is null.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<ImportOutcome<Invoice>> UpsertSalesInvoiceAsync(
        ErpnextSalesInvoiceSource source,
        TenantId tenantId,
        ChartOfAccountsId chartId,
        PartyId customerPartyId,
        GLAccountId arAccountId,
        GLAccountId defaultIncomeAccountId,
        CancellationToken cancellationToken = default);
}

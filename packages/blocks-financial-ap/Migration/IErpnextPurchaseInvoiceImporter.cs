using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAp.Migration;

/// <summary>
/// Upsert ERPNext <c>Purchase Invoice</c> records into the canonical AP
/// substrate. Pass-2 importer (the AP side); supplier resolution lives
/// upstream in <c>blocks-people-foundation.IErpnextPartyImporter</c>'s
/// pass-1 — by the time this importer runs, the caller already holds
/// the canonical <see cref="PartyId"/> for the source's
/// <c>supplier</c>.
///
/// <para>
/// <b>Idempotency:</b> the importer marks every imported bill with
/// <c>ExternalRef = "erpnext:pinv:{Name}"</c> (the FK) and stores
/// <c>erpnextModified:{Modified}</c> in <see cref="Bill.Notes"/>.
/// Re-running with the same <c>Modified</c> returns <c>Skipped</c>;
/// a newer <c>Modified</c> returns <c>Updated</c>.
/// </para>
///
/// <para>
/// <b>No GL posting in v1.</b> Imported bills arrive at the AP substrate
/// already-balanced from ERPNext's perspective; we do NOT re-post a
/// journal entry. If the host wants the canonical GL to mirror ERPNext's,
/// that's a separate ledger-side importer flow.
/// </para>
/// </summary>
public interface IErpnextPurchaseInvoiceImporter
{
    /// <summary>
    /// Upsert a Purchase Invoice. Caller supplies cross-cluster fields
    /// (chart, supplier party, AP account, default expense account) that
    /// ERPNext's payload can't carry directly.
    /// </summary>
    Task<ImportOutcome<Bill>> UpsertPurchaseInvoiceAsync(
        ErpnextPurchaseInvoiceSource source,
        TenantId tenantId,
        ChartOfAccountsId chartId,
        PartyId vendorPartyId,
        GLAccountId apAccountId,
        GLAccountId defaultExpenseAccountId,
        CancellationToken cancellationToken = default);
}

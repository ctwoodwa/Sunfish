using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialLedger.Migration;

/// <summary>
/// Pass 1 of the ERPNext → Anchor-native migration: idempotent upsert
/// of <see cref="GLAccount"/> records from <see cref="ErpnextAccountSource"/>.
/// Lookup by <c>GLAccount.ExternalRef == source.Name</c>. Returns
/// <see cref="ImportAction.Skipped"/> when the local version is at-or-newer
/// than the source.
/// </summary>
public interface IErpnextAccountImporter
{
    Task<ImportOutcome<GLAccount>> UpsertFromErpnextAsync(
        ErpnextAccountSource source,
        ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default);
}

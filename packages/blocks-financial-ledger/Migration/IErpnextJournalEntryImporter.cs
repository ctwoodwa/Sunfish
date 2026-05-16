using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialLedger.Migration;

/// <summary>
/// Pass 3 of the ERPNext → Anchor-native migration: idempotent upsert
/// of <see cref="JournalEntry"/> records from
/// <see cref="ErpnextJournalEntrySource"/>. Posted entries are
/// immutable per the migration-importer spec §5.2 — re-import of a
/// previously-imported entry returns <see cref="ImportAction.Skipped"/>
/// (with a warning detail string if any field has drifted).
/// </summary>
public interface IErpnextJournalEntryImporter
{
    Task<ImportOutcome<JournalEntry>> UpsertFromErpnextAsync(
        ErpnextJournalEntrySource source,
        ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default);
}

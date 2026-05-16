using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Seeds;

namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// Materializes a starter <see cref="ChartOfAccounts"/> + the
/// corresponding <see cref="GLAccount"/> records from a
/// <see cref="ChartTemplate"/>. The seeded accounts carry
/// <see cref="GLAccount.ChartId"/>, derived <see cref="NormalBalance"/>,
/// resolved <see cref="GLAccount.ParentAccountId"/> FKs, and the
/// chart's base currency.
/// </summary>
public interface IChartSeedingService
{
    /// <summary>
    /// Build a chart with <paramref name="chartName"/> for
    /// <paramref name="legalEntityId"/> + expand
    /// <paramref name="template"/> into <see cref="GLAccount"/> records.
    /// Returns the seeded <see cref="ChartOfAccounts"/>; the seeded
    /// accounts are persisted to the implementation's backing store
    /// (in-memory for <see cref="InMemoryChartSeedingService"/>;
    /// SQLite in production).
    /// </summary>
    Task<ChartOfAccounts> SeedChartAsync(
        LegalEntityId legalEntityId,
        string chartName,
        ChartTemplate template,
        string baseCurrency = "USD",
        CancellationToken cancellationToken = default);
}

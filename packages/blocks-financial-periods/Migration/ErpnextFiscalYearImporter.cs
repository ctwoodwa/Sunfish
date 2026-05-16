using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialPeriods.Migration;

/// <summary>
/// Default <see cref="IErpnextFiscalYearImporter"/>. Reads + writes
/// through <see cref="IFiscalYearRepository"/>; idempotent on
/// <see cref="ErpnextFiscalYearSource.Name"/> via
/// <see cref="IFiscalYearRepository.GetByExternalRefAsync"/>.
/// </summary>
public sealed class ErpnextFiscalYearImporter : IErpnextFiscalYearImporter
{
    private readonly IFiscalYearRepository _years;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, string> _versionCache = new();

    public ErpnextFiscalYearImporter(
        IFiscalYearRepository years,
        TimeProvider time)
    {
        _years = years ?? throw new ArgumentNullException(nameof(years));
        _time  = time  ?? throw new ArgumentNullException(nameof(time));
    }

    /// <inheritdoc />
    public async Task<ImportOutcome<FiscalYear>> UpsertFromErpnextAsync(
        ErpnextFiscalYearSource source,
        ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var existing = await _years.GetByExternalRefAsync(source.Name, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            var inserted = FiscalYear.CreateOpen(
                id:           FiscalYearId.NewId(),
                chartId:      targetChart,
                label:        DeriveLabel(source),
                startDate:    source.YearStartDate,
                endDate:      source.YearEndDate,
                createdAtUtc: new Instant(_time.GetUtcNow()))
                with { ExternalRef = source.Name };
            await _years.InsertAsync(inserted, cancellationToken).ConfigureAwait(false);
            // Track the external-ref → version mapping so subsequent
            // upserts can decide Updated vs Skipped. The
            // IFiscalYearRepository contract doesn't store the ERPNext
            // version (only external-ref); the importer carries it.
            _versionCache[source.Name] = source.Modified;
            return new ImportOutcome<FiscalYear>(inserted, ImportAction.Inserted, null);
        }

        // Existing row — decide Skipped vs Updated based on the
        // ERPNext modified timestamp (ISO-string lexicographic order).
        if (!_versionCache.TryGetValue(source.Name, out var priorVersion))
            // First time we see this external-ref in-process and the
            // row already existed — treat as a re-bootstrap; conservatively
            // refresh fields + record the version.
            priorVersion = string.Empty;

        if (string.Compare(source.Modified, priorVersion, StringComparison.Ordinal) <= 0)
            return new ImportOutcome<FiscalYear>(existing, ImportAction.Skipped, null);

        // Update path — refresh label + dates, but never flip status
        // (Closed FY stays Closed; an ERPNext re-export does NOT
        // reopen).
        var updated = existing with
        {
            Label     = DeriveLabel(source),
            StartDate = source.YearStartDate,
            EndDate   = source.YearEndDate,
            Version   = existing.Version + 1,
        };
        if (!await _years.UpdateAsync(updated, cancellationToken).ConfigureAwait(false))
            return new ImportOutcome<FiscalYear>(existing, ImportAction.Skipped,
                "Update rejected by repository CAS (concurrent edit?).");
        _versionCache[source.Name] = source.Modified;
        return new ImportOutcome<FiscalYear>(updated, ImportAction.Updated, null);
    }

    private static string DeriveLabel(ErpnextFiscalYearSource source)
    {
        var shortName = string.IsNullOrWhiteSpace(source.CompanyShortName)
            ? null
            : source.CompanyShortName.Trim();
        // ERPNext convention: "FY26" style (last 2 digits of start
        // year). Distinct charts can collide on label across companies
        // — prefix with the company short-name when available.
        var yearSuffix = source.YearStartDate.Year % 100;
        var fyToken = $"FY{yearSuffix:00}";
        return shortName is null ? fyToken : $"{shortName} {fyToken}";
    }
}

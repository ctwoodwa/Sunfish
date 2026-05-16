using System.Globalization;
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
/// <see cref="IFiscalYearRepository.GetByExternalRefAsync"/>; the
/// stored <see cref="FiscalYear.ExternalModifiedAtUtc"/> column carries
/// the version stamp so a fresh process instance + re-import on the
/// same source still decides Skipped-vs-Updated correctly.
/// </summary>
public sealed class ErpnextFiscalYearImporter : IErpnextFiscalYearImporter
{
    /// <summary>
    /// ERPNext Frappe ORM emits <c>modified</c> as one of these two
    /// fixed-width formats. The importer parses (rather than
    /// lex-compares) so a caller drift (e.g., truncated microseconds)
    /// fails loudly with <see cref="FormatException"/> instead of
    /// silently mis-ordering versions.
    /// </summary>
    private static readonly string[] ErpnextModifiedFormats =
    {
        "yyyy-MM-dd HH:mm:ss.ffffff",
        "yyyy-MM-dd HH:mm:ss",
    };

    private readonly IFiscalYearRepository _years;
    private readonly TimeProvider _time;

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

        var sourceModified = ParseErpnextModified(source.Modified);

        var existing = await _years.GetByExternalRefAsync(source.Name, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            var inserted = FiscalYear.CreateOpen(
                id:                    FiscalYearId.NewId(),
                chartId:               targetChart,
                label:                 DeriveLabel(source),
                startDate:             source.YearStartDate,
                endDate:               source.YearEndDate,
                createdAtUtc:          new Instant(_time.GetUtcNow()),
                externalRef:           source.Name,
                externalModifiedAtUtc: sourceModified);
            await _years.InsertAsync(inserted, cancellationToken).ConfigureAwait(false);
            return new ImportOutcome<FiscalYear>(inserted, ImportAction.Inserted, null);
        }

        // Existing row — decide Skipped vs Updated based on the
        // persisted ExternalModifiedAtUtc column. Process-restart safe:
        // the comparison reads from the row, not an in-process cache.
        var priorModified = existing.ExternalModifiedAtUtc?.Value
            ?? DateTimeOffset.MinValue;
        if (sourceModified.Value <= priorModified)
            return new ImportOutcome<FiscalYear>(existing, ImportAction.Skipped, null);

        // Update path — refresh label + dates, but never flip status
        // (Closed FY stays Closed; an ERPNext re-export does NOT
        // reopen). ClosedAtUtc + ClosingJournalEntryId preserved by
        // the C# `with` clone (unmentioned fields keep their value).
        var updated = existing with
        {
            Label                 = DeriveLabel(source),
            StartDate             = source.YearStartDate,
            EndDate               = source.YearEndDate,
            Version               = existing.Version + 1,
            ExternalModifiedAtUtc = sourceModified,
        };
        if (!await _years.UpdateAsync(updated, cancellationToken).ConfigureAwait(false))
            return new ImportOutcome<FiscalYear>(existing, ImportAction.Skipped,
                "Update rejected by repository CAS (concurrent edit?).");
        return new ImportOutcome<FiscalYear>(updated, ImportAction.Updated, null);
    }

    /// <summary>
    /// Parse the ERPNext <c>modified</c> timestamp using the documented
    /// fixed-width formats. Throws <see cref="FormatException"/> on a
    /// drift so a malformed source fails loudly instead of mis-ordering.
    /// </summary>
    internal static Instant ParseErpnextModified(string modified)
    {
        if (string.IsNullOrWhiteSpace(modified))
            throw new FormatException(
                "ErpnextFiscalYearSource.Modified must be a non-empty Frappe-style ISO timestamp.");
        if (!DateTimeOffset.TryParseExact(
                modified,
                ErpnextModifiedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
        {
            throw new FormatException(
                $"ErpnextFiscalYearSource.Modified value '{modified}' does not match any expected "
                + "Frappe format (yyyy-MM-dd HH:mm:ss[.ffffff]).");
        }
        return new Instant(dto);
    }

    private static string DeriveLabel(ErpnextFiscalYearSource source)
    {
        var shortName = string.IsNullOrWhiteSpace(source.CompanyShortName)
            ? null
            : source.CompanyShortName.Trim();
        var yearSuffix = source.YearStartDate.Year % 100;
        var fyToken = $"FY{yearSuffix:00}";
        return shortName is null ? fyToken : $"{shortName} {fyToken}";
    }
}

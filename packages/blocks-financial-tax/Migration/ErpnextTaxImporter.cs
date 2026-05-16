using System.Text.Json;
using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using FL = Sunfish.Blocks.FinancialLedger.Models;
using LedgerMigration = Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;

namespace Sunfish.Blocks.FinancialTax.Migration;

/// <summary>
/// Pass 2 of the ERPNext migration importer — maps ERPNext "Sales
/// Taxes and Charges Template" + "Account Tax" rows to the
/// (<see cref="TaxCode"/>, <see cref="TaxRate"/>) pair. Idempotent on
/// <see cref="ErpnextTaxSource.Name"/>; preserves <see cref="TaxCode.Notes"/>'s
/// <c>externalRef:&lt;source.Name&gt;</c> marker as the cross-system
/// FK until a proper <c>ExternalRef</c> field lands in a later
/// schema iteration.
/// </summary>
/// <remarks>
/// This is <b>not the primary tax-import path</b> for Wave / Rentler /
/// Mac (those source systems don't model tax the way ERPNext does).
/// It exists for completeness against the migration-importer spec at
/// <c>_shared/engineering/erpnext-to-anchor-migration-importer-spec.md</c>.
/// </remarks>
public interface IErpnextTaxImporter
{
    /// <summary>
    /// Upsert a <see cref="TaxCode"/> + its <see cref="TaxRate"/>
    /// history from an ERPNext source record. Idempotent: same
    /// <c>(source.Name)</c> on a same-or-newer-version run returns
    /// <see cref="LedgerMigration.ImportAction.Skipped"/>.
    /// </summary>
    Task<LedgerMigration.ImportOutcome<TaxCode>> UpsertFromErpnextAsync(
        ErpnextTaxSource source,
        FL.ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Single ERPNext source row passed to the importer. The
/// <see cref="TaxRateRowsJson"/> string is the inlined JSON shape of
/// ERPNext's <c>taxes</c> table: an array of
/// <c>{account_head, rate, included_in_print_rate}</c> objects.
/// </summary>
/// <param name="Name">ERPNext <c>name</c> — stable id; the FK we dedupe on.</param>
/// <param name="Modified">ERPNext <c>modified</c> — version key (string-ordinal compare).</param>
/// <param name="TaxName">Human-stable tax-code label.</param>
/// <param name="TaxRateRowsJson">JSON-serialized list of <see cref="ErpnextTaxRateRow"/>.</param>
/// <param name="Disabled">When true, the local <see cref="TaxCode"/> is marked <c>IsActive = false</c>.</param>
public sealed record ErpnextTaxSource(
    string Name,
    string Modified,
    string TaxName,
    string TaxRateRowsJson,
    bool Disabled);

/// <summary>One row of an ERPNext <c>taxes</c> table.</summary>
/// <param name="AccountHead">ERPNext <c>account_head</c> — resolves to a local <see cref="FL.GLAccount"/> via <see cref="IAccountResolver"/>.</param>
/// <param name="Rate">Percentage rate (e.g. <c>5.3m</c> for 5.3%).</param>
/// <param name="IncludedInPrintRate">When true, the rate is Inclusive (price-with-tax); else OnSubtotal. ERPNext "Compound" tax has its own column we don't yet read.</param>
public sealed record ErpnextTaxRateRow(
    string AccountHead,
    decimal Rate,
    bool IncludedInPrintRate);

/// <summary>
/// Reference implementation. Resolves ERPNext <c>account_head</c>
/// to a <see cref="FL.GLAccountId"/> via the ledger's
/// <see cref="IAccountResolver"/> (assumes the chart has already
/// been seeded — typically by ledger Pass 1).
/// </summary>
public sealed class ErpnextTaxImporter : IErpnextTaxImporter
{
    private const string ExternalRefPrefix = "externalRef:";

    private readonly ITaxCodeStore _codes;
    private readonly ITaxRateLookup _rates;
    private readonly ITaxJurisdictionResolver _jurisdictions;
    private readonly IAccountResolver _accounts;

    public ErpnextTaxImporter(
        ITaxCodeStore codes,
        ITaxRateLookup rates,
        ITaxJurisdictionResolver jurisdictions,
        IAccountResolver accounts)
    {
        _codes = codes ?? throw new ArgumentNullException(nameof(codes));
        _rates = rates ?? throw new ArgumentNullException(nameof(rates));
        _jurisdictions = jurisdictions ?? throw new ArgumentNullException(nameof(jurisdictions));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
    }

    /// <inheritdoc />
    public async Task<LedgerMigration.ImportOutcome<TaxCode>> UpsertFromErpnextAsync(
        ErpnextTaxSource source,
        FL.ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var externalRef = ExternalRefPrefix + source.Name;
        var existing = await FindExistingByExternalRefAsync(targetChart, externalRef, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null && existing.Notes is not null
            && existing.Notes.Contains($"|modified:{source.Modified}", StringComparison.Ordinal))
        {
            return new LedgerMigration.ImportOutcome<TaxCode>(
                existing, LedgerMigration.ImportAction.Skipped,
                $"Already imported at modified={source.Modified}.");
        }

        // Decide application: if any row says IncludedInPrintRate, treat
        // the whole code as Inclusive. Otherwise OnSubtotal. (ERPNext's
        // "is_compound" is on a separate column we don't yet ingest.)
        var rateRows = ParseRateRows(source.TaxRateRowsJson);
        var application = rateRows.Any(r => r.IncludedInPrintRate)
            ? TaxApplication.Inclusive
            : TaxApplication.OnSubtotal;
        var kind = application == TaxApplication.Inclusive ? TaxKind.VAT : TaxKind.Sales;

        var notes = $"{ExternalRefPrefix}{source.Name}|modified:{source.Modified}";

        TaxCode upserted;
        LedgerMigration.ImportAction action;
        if (existing is null)
        {
            upserted = TaxCode.Create(
                chartId: targetChart,
                code: source.TaxName,
                name: source.TaxName,
                kind: kind,
                application: application,
                notes: notes);
            if (source.Disabled)
            {
                upserted = upserted with { IsActive = false };
            }
            await _codes.UpsertAsync(upserted, cancellationToken).ConfigureAwait(false);
            action = LedgerMigration.ImportAction.Inserted;
        }
        else
        {
            upserted = existing with
            {
                Code = source.TaxName,
                Name = source.TaxName,
                Kind = kind,
                Application = application,
                IsActive = !source.Disabled,
                Notes = notes,
            };
            await _codes.UpsertAsync(upserted, cancellationToken).ConfigureAwait(false);
            action = LedgerMigration.ImportAction.Updated;
        }

        // ERPNext doesn't model jurisdictions — each tax row in the
        // source is conceptually its own per-tax-account scope. Mint
        // a fresh placeholder jurisdiction id per row so the
        // (TaxCode, Jurisdiction) overlap detector doesn't reject
        // legitimate multi-row imports that share an effective date.
        // Admin UI can later collapse placeholders into real
        // jurisdictions when the user adds location context.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        foreach (var row in rateRows)
        {
            var account = await _accounts.GetAsync(new FL.GLAccountId(row.AccountHead), cancellationToken)
                .ConfigureAwait(false);
            if (account is null) continue; // Pass-1 should have created it; skip silently.
            var perRowJurisdictionId = TaxJurisdictionId.NewId();
            var candidate = TaxRate.Create(
                taxCodeId: upserted.Id,
                jurisdictionId: perRowJurisdictionId,
                ratePercent: row.Rate,
                effectiveDate: today,
                payableAccountId: account.Id);
            await _rates.UpsertAsync(candidate, cancellationToken).ConfigureAwait(false);
        }

        return new LedgerMigration.ImportOutcome<TaxCode>(
            upserted, action, $"Imported with {rateRows.Count} rate row(s).");
    }

    private async Task<TaxCode?> FindExistingByExternalRefAsync(
        FL.ChartOfAccountsId chartId,
        string externalRefMarker,
        CancellationToken cancellationToken)
    {
        var all = await _codes.GetByChartAsync(chartId, cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(c => c.Notes is not null
            && c.Notes.StartsWith(externalRefMarker, StringComparison.Ordinal));
    }

    private static IReadOnlyList<ErpnextTaxRateRow> ParseRateRows(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<ErpnextTaxRateRow>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<ErpnextTaxRateRow>();
            var rows = new List<ErpnextTaxRateRow>();
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                var accountHead = elem.TryGetProperty("account_head", out var ah) ? ah.GetString() ?? "" : "";
                var rate = elem.TryGetProperty("rate", out var r) && r.ValueKind == JsonValueKind.Number
                    ? r.GetDecimal()
                    : 0m;
                var inclusive = elem.TryGetProperty("included_in_print_rate", out var ip)
                    && (ip.ValueKind == JsonValueKind.True
                        || (ip.ValueKind == JsonValueKind.Number && ip.GetInt32() != 0));
                if (string.IsNullOrEmpty(accountHead) || rate <= 0m) continue;
                rows.Add(new ErpnextTaxRateRow(accountHead, rate, inclusive));
            }
            return rows;
        }
        catch (JsonException)
        {
            return Array.Empty<ErpnextTaxRateRow>();
        }
    }
}

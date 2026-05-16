using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialLedger.Migration;

/// <summary>
/// Default <see cref="IErpnextAccountImporter"/>. Maps the ERPNext
/// <c>account_type</c> string to the local
/// <see cref="GLAccountType"/> + <see cref="AccountSubtype"/> pair per
/// the migration-importer spec §3.2 enum-mapping table. Idempotent on
/// <c>ExternalRef == source.Name</c>.
/// </summary>
public sealed class ErpnextAccountImporter : IErpnextAccountImporter
{
    private readonly InMemoryAccountResolver _accounts;

    public ErpnextAccountImporter(InMemoryAccountResolver accounts)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
    }

    /// <inheritdoc />
    public Task<ImportOutcome<GLAccount>> UpsertFromErpnextAsync(
        ErpnextAccountSource source,
        ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var existing = _accounts.SeededAccounts
            .FirstOrDefault(a => string.Equals(a.ExternalRef, source.Name, StringComparison.Ordinal));

        if (existing is not null)
        {
            // Compare opaque version key. ERPNext "modified" is
            // ISO-8601 so string-comparison matches temporal order.
            var localVersion = existing.ExternalRef is null ? string.Empty : source.Modified;
            var compareToStored = string.CompareOrdinal(source.Modified, localVersion);
            // Stored existing.UpdatedAtUtc is the local timestamp; we
            // round-trip the source.Modified into ExternalRef-adjacent
            // storage via a deterministic suffix-free hash check.
            // Simpler: skip when the same name was already imported.
            // ERPNext rarely back-dates Modified, so equality on a
            // re-import is the "same version" path.
            if (string.Equals(source.Modified, existing.Description, StringComparison.Ordinal))
            {
                return Task.FromResult(new ImportOutcome<GLAccount>(
                    existing, ImportAction.Skipped, "same source version"));
            }

            var updated = existing with
            {
                Name = source.AccountName,
                Code = source.AccountNumber ?? existing.Code,
                IsPostable = !source.IsGroup,
                IsActive = !source.Disabled,
                Description = source.Modified,
                UpdatedAtUtc = Instant.Now,
            };
            _accounts.Upsert(updated);
            return Task.FromResult(new ImportOutcome<GLAccount>(
                updated, ImportAction.Updated, $"version {source.Modified}"));
        }

        var (type, subtype) = MapAccountType(source.AccountType);
        var parentId = ResolveParentByExternalRef(source.ParentAccountName);
        var account = GLAccount.Create(
            id:              GLAccountId.NewId(),
            chartId:         targetChart,
            code:            source.AccountNumber ?? source.Name,
            name:            source.AccountName,
            type:            type,
            subtype:         subtype,
            currency:        "USD",
            parentAccountId: parentId,
            isPostable:      !source.IsGroup,
            description:     source.Modified, // store source version in Description for back-compat (no dedicated field yet)
            externalRef:     source.Name) with
        {
            IsActive = !source.Disabled,
        };
        _accounts.Upsert(account);
        return Task.FromResult(new ImportOutcome<GLAccount>(
            account, ImportAction.Inserted, null));
    }

    private GLAccountId? ResolveParentByExternalRef(string? parentName)
    {
        if (parentName is null) return null;
        var parent = _accounts.SeededAccounts
            .FirstOrDefault(a => string.Equals(a.ExternalRef, parentName, StringComparison.Ordinal));
        return parent?.Id;
    }

    /// <summary>
    /// ERPNext account_type → (GLAccountType, AccountSubtype). Per
    /// migration-importer spec §3.2.
    /// </summary>
    public static (GLAccountType Type, AccountSubtype Subtype) MapAccountType(string? accountType)
    {
        return accountType switch
        {
            "Bank"             => (GLAccountType.Asset,     AccountSubtype.BankAccount),
            "Cash"             => (GLAccountType.Asset,     AccountSubtype.CurrentAsset),
            "Receivable"       => (GLAccountType.Asset,     AccountSubtype.AccountsReceivable),
            "Fixed Asset"      => (GLAccountType.Asset,     AccountSubtype.FixedAsset),
            "Stock"            => (GLAccountType.Asset,     AccountSubtype.InventoryAsset),
            "Accumulated Depreciation" => (GLAccountType.Asset, AccountSubtype.AccumulatedDepreciation),

            "Payable"          => (GLAccountType.Liability, AccountSubtype.AccountsPayable),
            "Tax"              => (GLAccountType.Liability, AccountSubtype.TaxesPayable),
            "Liability"        => (GLAccountType.Liability, AccountSubtype.CurrentLiability),

            "Equity"           => (GLAccountType.Equity,    AccountSubtype.OwnersEquity),

            "Income Account"   => (GLAccountType.Revenue,   AccountSubtype.OperatingIncome),
            "Income"           => (GLAccountType.Revenue,   AccountSubtype.OperatingIncome),

            "Expense Account"  => (GLAccountType.Expense,   AccountSubtype.OperatingExpense),
            "Expense"          => (GLAccountType.Expense,   AccountSubtype.OperatingExpense),
            "Cost of Goods Sold" => (GLAccountType.Expense, AccountSubtype.CostOfGoodsSold),
            "Depreciation"     => (GLAccountType.Expense,   AccountSubtype.DepreciationExpense),

            _ => (GLAccountType.Asset, AccountSubtype.OtherAsset),
        };
    }
}

using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Seeds;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// In-memory <see cref="IChartSeedingService"/>. Topologically sorts
/// the <see cref="ChartTemplate.Accounts"/> by
/// <see cref="ChartTemplateAccount.ParentCode"/> so parents are
/// materialized before any child that references them, then expands
/// each row via <see cref="GLAccount.Create"/>. The seeded
/// <see cref="GLAccount"/> records are surfaced via
/// <see cref="SeededAccounts"/> for test assertions; production
/// implementations persist via SQLite.
/// </summary>
public sealed class InMemoryChartSeedingService : IChartSeedingService
{
    private readonly List<ChartOfAccounts> _charts = new();
    private readonly List<GLAccount> _accounts = new();

    /// <summary>All charts seeded so far.</summary>
    public IReadOnlyList<ChartOfAccounts> SeededCharts => _charts;

    /// <summary>All GL accounts seeded so far.</summary>
    public IReadOnlyList<GLAccount> SeededAccounts => _accounts;

    /// <inheritdoc />
    public Task<ChartOfAccounts> SeedChartAsync(
        LegalEntityId legalEntityId,
        string chartName,
        ChartTemplate template,
        string baseCurrency = "USD",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(chartName);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrEmpty(baseCurrency);

        var now = Instant.Now;
        var chart = new ChartOfAccounts(
            Id:                          ChartOfAccountsId.NewId(),
            LegalEntityId:               legalEntityId,
            Name:                        chartName,
            BaseCurrency:                baseCurrency,
            FiscalYearStartMonth:        1,
            FiscalYearStartDay:          1,
            RetainedEarningsAccountId:   null,
            IsActive:                    true,
            CreatedAtUtc:                now,
            UpdatedAtUtc:                now);

        // Topological sort by ParentCode. Nodes with null ParentCode
        // (top-level groups) materialize first; children follow as
        // their parents become available. Cycle / dangling-ref guard:
        // if a pass makes no progress the template is malformed.
        var byCode = template.Accounts.ToDictionary(a => a.Code);
        var remaining = template.Accounts.ToList();
        var codeToId = new Dictionary<string, GLAccountId>(template.Accounts.Count);
        var seededOrder = new List<GLAccount>(template.Accounts.Count);

        while (remaining.Count > 0)
        {
            var madeProgress = false;
            for (var i = remaining.Count - 1; i >= 0; i--)
            {
                var t = remaining[i];
                if (t.ParentCode is { } parent)
                {
                    if (!byCode.ContainsKey(parent))
                    {
                        throw new InvalidOperationException(
                            $"Chart template '{template.Name}' references unknown ParentCode '{parent}' on '{t.Code}'.");
                    }
                    if (!codeToId.ContainsKey(parent))
                    {
                        continue; // wait for parent
                    }
                }
                var id = GLAccountId.NewId();
                var account = GLAccount.Create(
                    id:              id,
                    chartId:         chart.Id,
                    code:            t.Code,
                    name:            t.Name,
                    type:            t.Type,
                    subtype:         t.Subtype,
                    currency:        baseCurrency,
                    parentAccountId: t.ParentCode is { } pc ? (GLAccountId?)codeToId[pc] : null,
                    isPostable:      t.IsPostable,
                    createdAtUtc:    now);
                codeToId[t.Code] = id;
                seededOrder.Add(account);
                remaining.RemoveAt(i);
                madeProgress = true;
            }
            if (!madeProgress)
            {
                var stuck = string.Join(", ", remaining.Select(r => r.Code));
                throw new InvalidOperationException(
                    $"Chart template '{template.Name}' has a parent-cycle or dangling refs; cannot resolve: {stuck}.");
            }
        }

        _charts.Add(chart);
        _accounts.AddRange(seededOrder);
        return Task.FromResult(chart);
    }
}

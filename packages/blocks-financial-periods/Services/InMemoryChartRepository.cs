using System.Collections.Concurrent;
using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// In-memory <see cref="IChartRepository"/> for tests + demos.
/// Production hosts wire a SQLite-backed implementation.
/// </summary>
public sealed class InMemoryChartRepository : IChartRepository
{
    private readonly ConcurrentDictionary<ChartOfAccountsId, ChartOfAccounts> _byId = new();

    /// <summary>Seed or replace a chart in the backing store.</summary>
    public void Upsert(ChartOfAccounts chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        _byId[chart.Id] = chart;
    }

    /// <inheritdoc />
    public Task<ChartOfAccounts?> GetAsync(
        ChartOfAccountsId chartId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(chartId, out var c) ? c : null);
}

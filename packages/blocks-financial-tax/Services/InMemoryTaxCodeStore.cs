using System.Collections.Concurrent;
using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// In-memory <see cref="ITaxCodeStore"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Test- + desktop-
/// in-process scenarios; SQLite-backed implementation lands later.
/// </summary>
public sealed class InMemoryTaxCodeStore : ITaxCodeStore
{
    private readonly ConcurrentDictionary<TaxCodeId, TaxCode> _rows = new();

    /// <inheritdoc />
    public Task<TaxCode?> GetAsync(TaxCodeId id, CancellationToken cancellationToken = default)
    {
        if (_rows.TryGetValue(id, out var row) && row.DeletedAtUtc is null)
        {
            return Task.FromResult<TaxCode?>(row);
        }
        return Task.FromResult<TaxCode?>(null);
    }

    /// <inheritdoc />
    public Task<TaxCode?> GetByCodeAsync(FL.ChartOfAccountsId chartId, string code, CancellationToken cancellationToken = default)
    {
        var hit = _rows.Values.FirstOrDefault(r =>
            r.DeletedAtUtc is null
            && r.ChartId == chartId
            && string.Equals(r.Code, code, StringComparison.Ordinal));
        return Task.FromResult<TaxCode?>(hit);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TaxCode>> GetByChartAsync(FL.ChartOfAccountsId chartId, CancellationToken cancellationToken = default)
    {
        var hits = _rows.Values
            .Where(r => r.DeletedAtUtc is null && r.ChartId == chartId)
            .OrderBy(r => r.Code, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult<IReadOnlyList<TaxCode>>(hits);
    }

    /// <inheritdoc />
    public Task UpsertAsync(TaxCode taxCode, CancellationToken cancellationToken = default)
    {
        if (taxCode is null) throw new ArgumentNullException(nameof(taxCode));
        var now = Instant.Now;
        var stamped = _rows.TryGetValue(taxCode.Id, out var existing)
            ? taxCode with { UpdatedAtUtc = now, Version = existing.Version + 1 }
            : taxCode with { CreatedAtUtc = taxCode.CreatedAtUtc ?? now, UpdatedAtUtc = now };
        _rows[stamped.Id] = stamped;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SoftDeleteAsync(TaxCodeId id, Instant deletedAtUtc, CancellationToken cancellationToken = default)
    {
        if (_rows.TryGetValue(id, out var existing) && existing.DeletedAtUtc is null)
        {
            _rows[id] = existing with { DeletedAtUtc = deletedAtUtc, UpdatedAtUtc = deletedAtUtc };
        }
        return Task.CompletedTask;
    }
}

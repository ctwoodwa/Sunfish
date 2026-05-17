using System.Collections.Concurrent;
using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAp.Services;

/// <summary>
/// In-memory <see cref="IBillRepository"/>. Mirrors AR's
/// <c>InMemoryInvoiceRepository</c>; secondary queries (by vendor +
/// number, by external-ref, open-only) scan the values — fine for the
/// in-memory v1.
/// </summary>
public sealed class InMemoryBillRepository : IBillRepository
{
    private readonly ConcurrentDictionary<BillId, Bill> _bills = new();

    /// <inheritdoc />
    public Task UpsertAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        if (bill is null) throw new ArgumentNullException(nameof(bill));
        if (_bills.TryGetValue(bill.Id, out var existing) && existing.DeletedAtUtc is not null)
            throw new InvalidOperationException($"Bill '{bill.Id.Value}' is tombstoned; further mutations are not permitted.");
        _bills[bill.Id] = bill;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Bill?> GetAsync(BillId id, CancellationToken cancellationToken = default)
    {
        _bills.TryGetValue(id, out var bill);
        return Task.FromResult(bill is null || bill.DeletedAtUtc is not null ? null : bill);
    }

    /// <inheritdoc />
    public Task<Bill?> GetByVendorBillNumberAsync(
        ChartOfAccountsId chartId,
        PartyId vendorId,
        string billNumber,
        CancellationToken cancellationToken = default)
    {
        var hit = _bills.Values.FirstOrDefault(b =>
            b.DeletedAtUtc is null
            && b.ChartId == chartId
            && b.VendorId == vendorId
            && string.Equals(b.BillNumber, billNumber, StringComparison.Ordinal));
        return Task.FromResult<Bill?>(hit);
    }

    /// <inheritdoc />
    public Task<Bill?> GetByExternalRefAsync(
        ChartOfAccountsId chartId,
        string externalRef,
        CancellationToken cancellationToken = default)
    {
        var hit = _bills.Values.FirstOrDefault(b =>
            b.DeletedAtUtc is null
            && b.ChartId == chartId
            && string.Equals(b.ExternalRef, externalRef, StringComparison.Ordinal));
        return Task.FromResult<Bill?>(hit);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Bill>> ListByChartAsync(ChartOfAccountsId chartId, CancellationToken cancellationToken = default)
    {
        var rows = _bills.Values
            .Where(b => b.DeletedAtUtc is null && b.ChartId == chartId)
            .ToList();
        return Task.FromResult<IReadOnlyList<Bill>>(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Bill>> ListByVendorAsync(ChartOfAccountsId chartId, PartyId vendorId, CancellationToken cancellationToken = default)
    {
        var rows = _bills.Values
            .Where(b => b.DeletedAtUtc is null && b.ChartId == chartId && b.VendorId == vendorId)
            .ToList();
        return Task.FromResult<IReadOnlyList<Bill>>(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Bill>> QueryOpenAsync(
        ChartOfAccountsId chartId,
        PartyId? vendorId = null,
        string? propertyId = null,
        CancellationToken cancellationToken = default)
    {
        var rows = _bills.Values.Where(b =>
            b.DeletedAtUtc is null
            && b.ChartId == chartId
            && b.Status.IsOpen()
            && (vendorId is null || b.VendorId == vendorId.Value)
            && (propertyId is null || string.Equals(b.PropertyId, propertyId, StringComparison.Ordinal)))
            .ToList();
        return Task.FromResult<IReadOnlyList<Bill>>(rows);
    }

    /// <inheritdoc />
    public Task<bool> SoftDeleteAsync(BillId id, PartyId actor, string? reason, CancellationToken cancellationToken = default)
    {
        if (!_bills.TryGetValue(id, out var bill)) return Task.FromResult(false);
        if (bill.DeletedAtUtc is not null) return Task.FromResult(true);

        var now = Instant.Now;
        _bills[id] = bill with
        {
            DeletedAtUtc = now,
            DeletedBy = actor,
            DeletedReason = reason,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = bill.Version + 1,
        };
        return Task.FromResult(true);
    }
}

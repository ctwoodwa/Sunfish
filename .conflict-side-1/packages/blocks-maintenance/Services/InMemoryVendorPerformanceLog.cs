using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// In-memory append-only <see cref="IVendorPerformanceLog"/> for tests +
/// non-production hosts. Preserves chronological order via a per-vendor
/// list backed by a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// Idempotent on duplicate <see cref="VendorPerformanceRecordId"/>.
/// </summary>
public sealed class InMemoryVendorPerformanceLog : IVendorPerformanceLog
{
    private readonly ConcurrentDictionary<VendorId, List<VendorPerformanceRecord>> _byVendor = new();
    private readonly ConcurrentDictionary<VendorPerformanceRecordId, byte> _seenIds = new();

    /// <inheritdoc />
    public Task<VendorPerformanceRecord> AppendAsync(VendorPerformanceRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        ct.ThrowIfCancellationRequested();

        if (!_seenIds.TryAdd(record.Id, 0))
        {
            // Idempotent: same id already appended → return the original
            // (or an equivalent — caller passes its own record so we
            // simply return what they gave us; storage already has it).
            return Task.FromResult(record);
        }

        var bucket = _byVendor.GetOrAdd(record.Vendor, _ => new List<VendorPerformanceRecord>());
        lock (bucket)
        {
            bucket.Add(record);
        }
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<VendorPerformanceRecord> ListByVendorAsync(
        VendorId vendor,
        int? skip,
        int? take,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_byVendor.TryGetValue(vendor, out var bucket))
        {
            yield break;
        }

        VendorPerformanceRecord[] snapshot;
        lock (bucket) { snapshot = bucket.ToArray(); }

        var s = skip ?? 0;
        var t = take ?? int.MaxValue;
        var end = Math.Min(snapshot.Length, s + t);
        for (var i = s; i < end; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return snapshot[i];
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public Task<VendorPerformanceRecord> ProjectFromWorkOrderAsync(
        VendorId vendor,
        WorkOrderId workOrder,
        VendorPerformanceEvent eventType,
        ActorId recordedBy,
        DateTimeOffset occurredAt,
        string? notes,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var record = new VendorPerformanceRecord
        {
            Id = new VendorPerformanceRecordId(Guid.NewGuid()),
            Vendor = vendor,
            Event = eventType,
            OccurredAt = occurredAt,
            RecordedBy = recordedBy,
            RelatedWorkOrder = workOrder,
            Notes = notes,
        };
        return AppendAsync(record, ct);
    }
}

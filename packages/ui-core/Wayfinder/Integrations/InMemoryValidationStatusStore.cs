using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Process-local in-memory implementation of
/// <see cref="IValidationStatusStore"/>. Suitable for
/// development hosts + the Phase 2
/// <c>InMemoryIntegrationAtlasProvider</c> test harness; production
/// hosts override with a durable store via DI.
/// </summary>
/// <remarks>
/// <b>Persistence:</b> none — all state is lost on process restart.
/// <b>Concurrency:</b> uses
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> for the per-tenant
/// keyspace; per-key history append + current-pointer update happen
/// under a per-key lock so external observers see one or the other,
/// not a torn write.
/// <b>Bound:</b> per-key history retains the most-recent 200 entries
/// — sufficient for the Atlas trend pane without unbounded growth
/// in a long-lived dev process.
/// </remarks>
public sealed class InMemoryValidationStatusStore : IValidationStatusStore
{
    private const int MaxRetainedEntriesPerKey = 200;

    private readonly ConcurrentDictionary<Key, Entry> _entries = new();

    private readonly record struct Key(TenantId TenantId, IntegrationCategory Category, string ProviderId);

    private sealed class Entry
    {
        public ProviderValidationStatusEntry? Current { get; set; }
        public List<ProviderValidationStatusEntry> History { get; } = new();
        public object Lock { get; } = new();
    }

    /// <inheritdoc />
    public Task<ProviderValidationStatusEntry?> GetCurrentAsync(
        TenantId tenantId,
        IntegrationCategory category,
        string providerId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ct.ThrowIfCancellationRequested();
        var entry = _entries.TryGetValue(new Key(tenantId, category, providerId), out var e) ? e.Current : null;
        return Task.FromResult(entry);
    }

    /// <inheritdoc />
    public Task UpdateAsync(
        TenantId tenantId,
        IntegrationCategory category,
        string providerId,
        IntegrationValidationResult result,
        ActorId actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(result);
        ct.ThrowIfCancellationRequested();

        var key = new Key(tenantId, category, providerId);
        var entry = _entries.GetOrAdd(key, _ => new Entry());

        var newRecord = new ProviderValidationStatusEntry(
            tenantId,
            category,
            providerId,
            result,
            actor,
            result.ValidatedAt);

        lock (entry.Lock)
        {
            entry.Current = newRecord;
            entry.History.Add(newRecord);
            if (entry.History.Count > MaxRetainedEntriesPerKey)
            {
                entry.History.RemoveAt(0);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ProviderValidationStatusEntry> HistoryAsync(
        TenantId tenantId,
        IntegrationCategory category,
        string providerId,
        int maxEntries = 20,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        if (maxEntries <= 0)
        {
            yield break;
        }

        ProviderValidationStatusEntry[] snapshot;
        if (_entries.TryGetValue(new Key(tenantId, category, providerId), out var entry))
        {
            lock (entry.Lock)
            {
                snapshot = entry.History
                    .AsEnumerable()
                    .Reverse()
                    .Take(maxEntries)
                    .ToArray();
            }
        }
        else
        {
            snapshot = Array.Empty<ProviderValidationStatusEntry>();
        }

        foreach (var item in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
            await Task.Yield();
        }
    }
}

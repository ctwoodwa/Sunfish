using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Kernel.Crdt;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// CRDT-backed implementation of <see cref="IStandingOrderRepository"/>. Per
/// ADR 0065 §2: each tenant has a dedicated CRDT document at
/// <c>wayfinder/standing-orders/{tenantId}</c>; orders are stored in a single
/// CRDT map container keyed by <see cref="StandingOrderId.Value"/> with a
/// canonical-JSON encoded <see cref="StandingOrder"/> as the value.
/// </summary>
/// <remarks>
/// <para>
/// <b>Concurrency.</b> Concurrent <see cref="AppendAsync"/> calls on disjoint
/// (Scope, Path) pairs merge cleanly via the underlying engine; the
/// last-writer-wins-by-IssuedAt-then-IssuedBy semantics at the per-(Scope, Path)
/// grain (per ADR 0065 §2) is applied at projection time by the Atlas
/// projector — this repository only stores the append-only log.
/// </para>
/// <para>
/// <b>Idempotency.</b> Re-appending an order with the same
/// <see cref="StandingOrder.Id"/> on a single replica is a no-op (the local
/// <c>ContainsKey</c> guard preserves the first-written canonical bytes).
/// Cross-replica concurrent appends with the same Id but diverging content
/// fall through to the underlying engine's CRDT tie-break — the
/// <see cref="StandingOrderState.Conflicted"/> state surfaces the loser per
/// ADR 0065 §2 conflict UX.
/// </para>
/// </remarks>
public sealed class CrdtStandingOrderRepository : IStandingOrderRepository
{
    private const string OrdersContainerName = "orders";

    private readonly ICrdtEngine _engine;
    private readonly ConcurrentDictionary<TenantId, ICrdtDocument> _documents = new();

    /// <summary>
    /// Tenants that have a document materialized in this repository. Used by
    /// <see cref="DefaultStandingOrderIssuer.RescindAsync"/> to locate an
    /// order whose tenant the caller does not supply (per ADR 0065 §1's
    /// rescission API surface). Phase 3a's Atlas projector replaces this
    /// scan with a tenant-aware index.
    /// </summary>
    public IReadOnlyCollection<TenantId> KnownTenants => _documents.Keys.ToArray();

    /// <summary>
    /// Construct a new repository over the supplied CRDT engine. Per-tenant
    /// documents are created lazily on first access.
    /// </summary>
    public CrdtStandingOrderRepository(ICrdtEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc />
    public ValueTask AppendAsync(StandingOrder order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        ct.ThrowIfCancellationRequested();

        var doc = GetOrCreateDocument(order.TenantId);
        var map = doc.GetMap(OrdersContainerName);
        var key = order.Id.Value.ToString("N");

        if (!map.ContainsKey(key))
        {
            var bytes = CanonicalJson.Serialize(order);
            map.Set(key, Encoding.UTF8.GetString(bytes));
        }
        return default;
    }

    /// <inheritdoc />
    public ValueTask<StandingOrder?> GetAsync(TenantId tenantId, StandingOrderId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (tenantId == default)
        {
            return ValueTask.FromResult<StandingOrder?>(null);
        }

        if (!_documents.TryGetValue(tenantId, out var doc))
        {
            return ValueTask.FromResult<StandingOrder?>(null);
        }

        var map = doc.GetMap(OrdersContainerName);
        var key = id.Value.ToString("N");
        var json = map.Get<string>(key);
        if (string.IsNullOrEmpty(json))
        {
            return ValueTask.FromResult<StandingOrder?>(null);
        }

        var order = JsonSerializer.Deserialize<StandingOrder>(json);
        return ValueTask.FromResult(order);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StandingOrder> EnumerateAsync(
        TenantId tenantId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (tenantId == default)
        {
            yield break;
        }
        if (!_documents.TryGetValue(tenantId, out var doc))
        {
            yield break;
        }

        var map = doc.GetMap(OrdersContainerName);
        var keys = new List<string>(map.Keys);

        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();
            var json = map.Get<string>(key);
            if (string.IsNullOrEmpty(json))
            {
                continue;
            }
            var order = JsonSerializer.Deserialize<StandingOrder>(json);
            if (order is not null)
            {
                yield return order;
            }
            await Task.Yield();
        }
    }

    private ICrdtDocument GetOrCreateDocument(TenantId tenantId)
        => _documents.GetOrAdd(tenantId, t => _engine.CreateDocument($"wayfinder/standing-orders/{t.Value}"));
}

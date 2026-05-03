using System.Text.Json;

namespace Sunfish.Kernel.Crdt.Sharding;

/// <summary>
/// Default <see cref="IShardedDocument"/> backed by an <see cref="ICrdtEngine"/>. Paper §9
/// mitigation 2 — application-level sharding.
/// </summary>
/// <remarks>
/// <para>
/// Structure:
/// <code>
/// parent ICrdtDocument
///   └── ICrdtMap "shards"
///         ├── "shard-key-1" → base64(sub-document snapshot bytes)
///         ├── "shard-key-2" → base64(sub-document snapshot bytes)
///         └── ...
/// </code>
/// </para>
/// <para>
/// Sub-document snapshot bytes are stored as base64 strings in the map so the parent
/// document's value encoding stays string/JSON-friendly across backends. When the real
/// Loro/Yjs backend lands, the encoding can be switched to native binary values without
/// changing the public surface.
/// </para>
/// <para>
/// Sub-documents materialized via <see cref="GetOrCreateShard"/> are cached until
/// <see cref="RetireShard"/> removes the key or <see cref="DisposeAsync"/> is called.
/// Their bytes are flushed back into the shards map lazily on:
/// <list type="bullet">
///   <item><see cref="ToSnapshot"/> — for wire / persistence transport.</item>
///   <item><see cref="DisposeAsync"/> — final flush before the sharded document closes.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class ShardedDocument : IShardedDocument
{
    private const string ShardsMapName = "shards";

    private readonly ICrdtEngine _engine;
    private readonly ICrdtDocument _parent;
    private readonly Dictionary<string, ICrdtDocument> _live = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private bool _disposed;

    /// <summary>Create a new sharded document over the given CRDT engine.</summary>
    public ShardedDocument(ICrdtEngine engine, string documentId)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentException.ThrowIfNullOrEmpty(documentId);
        _engine = engine;
        _parent = engine.CreateDocument(documentId);
    }

    /// <inheritdoc />
    public string DocumentId => _parent.DocumentId;

    /// <inheritdoc />
    public ICrdtMap Shards => _parent.GetMap(ShardsMapName);

    /// <inheritdoc />
    public IReadOnlyList<string> ActiveShardKeys
    {
        get
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                return Shards.Keys.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public ICrdtDocument GetOrCreateShard(string shardKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(shardKey);
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_live.TryGetValue(shardKey, out var existing))
            {
                return existing;
            }
            var payload = Shards.Get<string>(shardKey);
            byte[] bytes = string.IsNullOrEmpty(payload)
                ? Array.Empty<byte>()
                : Convert.FromBase64String(payload);
            var sub = bytes.Length == 0
                ? _engine.CreateDocument(ShardDocId(shardKey))
                : _engine.OpenDocument(ShardDocId(shardKey), bytes);
            _live[shardKey] = sub;
            // Seed the map with an empty record if this is a newly created shard so that
            // ActiveShardKeys reflects the shard immediately. If the shard came from an
            // existing snapshot the map key already exists and Set is idempotent.
            if (bytes.Length == 0 && !Shards.ContainsKey(shardKey))
            {
                Shards.Set(shardKey, string.Empty);
            }
            return sub;
        }
    }

    /// <inheritdoc />
    public bool RetireShard(string shardKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(shardKey);
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!Shards.ContainsKey(shardKey)) return false;
            if (_live.Remove(shardKey, out var live))
            {
                // Fire-and-dispose; sub-documents are lightweight in the stub.
                _ = live.DisposeAsync().AsTask();
            }
            Shards.Remove(shardKey);
            return true;
        }
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> ToSnapshot()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            FlushLive();
            return _parent.ToSnapshot();
        }
    }

    /// <inheritdoc />
    public void ApplySnapshot(ReadOnlyMemory<byte> snapshot)
    {
        if (snapshot.IsEmpty) return;
        lock (_sync)
        {
            ThrowIfDisposed();
            // Drop any live sub-documents; their state is superseded by the incoming snapshot.
            foreach (var live in _live.Values)
            {
                _ = live.DisposeAsync().AsTask();
            }
            _live.Clear();
            _parent.ApplySnapshot(snapshot);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        List<ICrdtDocument> toDispose;
        lock (_sync)
        {
            if (_disposed) return;
            FlushLive();
            toDispose = _live.Values.ToList();
            _live.Clear();
            _disposed = true;
        }
        foreach (var sub in toDispose)
        {
            await sub.DisposeAsync().ConfigureAwait(false);
        }
        await _parent.DisposeAsync().ConfigureAwait(false);
    }

    private void FlushLive()
    {
        foreach (var (key, sub) in _live)
        {
            var bytes = sub.ToSnapshot();
            var encoded = bytes.IsEmpty ? string.Empty : Convert.ToBase64String(bytes.Span);
            Shards.Set(key, encoded);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ShardedDocument));
    }

    private string ShardDocId(string shardKey) => $"{DocumentId}::{shardKey}";
}

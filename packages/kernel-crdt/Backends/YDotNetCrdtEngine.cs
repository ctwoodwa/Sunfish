// ============================================================================
//  YDotNet (Yjs/yrs) CRDT backend — Wave 1.2 spike outcome (2026-04-22 revisit)
// ============================================================================
//  ADR 0028 selected Loro as the primary engine with Yjs/yrs as fallback. The
//  first spike (2026-04-22 morning) retained the stub because:
//    - LoroCs (NuGet v1.10.3) was "very bare bones" — no snapshot/delta/vector-
//      clock surface exposed.
//    - YDotNet (NuGet v0.6.0) targeted net8.0 and had not been validated on
//      .NET 11 preview.
//
//  Re-validation (2026-04-22 afternoon): YDotNet v0.6.0 + YDotNet.Native v0.6.0
//  BUILD AND RUN on .NET 11 preview (SDK 11.0.100-preview.3.26207.106) via the
//  net8.0 compatibility path. A two-peer convergence probe (both inserting text
//  concurrently, exchanging StateVectorV1/StateDiffV1, applying ApplyV1) produced
//  "Hello, World!" on both sides — real CRDT merge, not the stub's total-order
//  replay. This unblocks the fallback path documented in ADR 0028.
//
//  Mapping from Sunfish contracts to YDotNet:
//    ICrdtDocument.ToSnapshot()            -> Transaction.StateDiffV1(null)
//    ICrdtDocument.ApplySnapshot(bytes)    -> Transaction.ApplyV1(bytes)
//    ICrdtDocument.VectorClock             -> Transaction.StateVectorV1()
//    ICrdtDocument.EncodeDelta(peerSv)     -> Transaction.StateDiffV1(peerSv)
//    ICrdtDocument.ApplyDelta(bytes)       -> Transaction.ApplyV1(bytes)
//    ICrdtText.Insert/Delete               -> Yjs Text insert/remove ops
//    ICrdtMap.Get/Set/Remove               -> Yjs Map ops (JSON-encoded values)
//    ICrdtList.Insert/Push/RemoveAt        -> Yjs Array ops (JSON-encoded values)
//
//  Map/List values are JSON-encoded strings so the generic T surface stays
//  backend-agnostic; the trade-off is a small marshalling overhead vs native
//  typed cells. If a Sunfish consumer needs typed cells, that's a follow-up.
//
//  The stub backend (StubCrdtEngine) is retained as a test harness per the DI
//  extension contract; it also runs anywhere YDotNet's native binaries don't
//  (unusual RIDs, restricted sandboxes).
// ============================================================================

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

using YDotNet.Document;
using YDotNet.Document.Cells;
using YDotNet.Document.Options;
using YDotNet.Document.Transactions;
using YDotNet.Document.Types.Maps;
using YDotNet.Document.Types.Texts;

using YArray = YDotNet.Document.Types.Arrays.Array;

namespace Sunfish.Kernel.Crdt.Backends;

/// <summary>
/// YDotNet-backed <see cref="ICrdtEngine"/> — Yjs/yrs CRDT with real merge semantics.
/// Paper §2.2 / §9, ADR 0028 fallback option.
/// </summary>
public sealed class YDotNetCrdtEngine : ICrdtEngine
{
    /// <inheritdoc />
    public string EngineName => "ydotnet";

    /// <inheritdoc />
    public string EngineVersion => "0.6.0";

    /// <inheritdoc />
    public ICrdtDocument CreateDocument(string documentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);
        return new YDotNetCrdtDocument(documentId);
    }

    /// <inheritdoc />
    public ICrdtDocument OpenDocument(string documentId, ReadOnlyMemory<byte> snapshot)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);
        var doc = new YDotNetCrdtDocument(documentId);
        if (!snapshot.IsEmpty)
        {
            doc.ApplySnapshot(snapshot);
        }
        return doc;
    }
}

internal sealed class YDotNetCrdtDocument : ICrdtDocument
{
    private readonly Doc _doc;
    private readonly ConcurrentDictionary<string, YDotNetCrdtText> _texts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, YDotNetCrdtMap> _maps = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, YDotNetCrdtList> _lists = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private bool _disposed;

    public YDotNetCrdtDocument(string documentId)
    {
        DocumentId = documentId;
        // CRITICAL: YDotNet 0.6.0 / yrs-ffi encodes client IDs as uint32 on the
        // wire despite exposing DocOptions.Id as ulong. IDs above 2^32 cause
        // concurrent-insertion RGA tiebreak to diverge across peers because
        // the high 32 bits get truncated inconsistently. We constrain client
        // IDs to the uint32 range to guarantee correct convergence. Collision
        // probability at 2^32 is ~birthday-paradox-safe for documents with
        // <65k concurrent writers; per paper §2.2 the Sunfish federation
        // envelope is well under that bound. See SPIKE-OUTCOME.md.
        _doc = new Doc(new DocOptions { Id = GenerateClientId() });
    }

    private static ulong GenerateClientId()
    {
        Span<byte> buf = stackalloc byte[4];
        RandomNumberGenerator.Fill(buf);
        var v = (ulong)BitConverter.ToUInt32(buf);
        return v == 0 ? 1UL : v;
    }

    public string DocumentId { get; }

    internal Doc UnderlyingDoc => _doc;

    internal object SyncRoot => _sync;

    public ICrdtText GetText(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _texts.GetOrAdd(name, static (n, doc) => new YDotNetCrdtText(doc, n), this);
    }

    public ICrdtMap GetMap(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _maps.GetOrAdd(name, static (n, doc) => new YDotNetCrdtMap(doc, n), this);
    }

    public ICrdtList GetList(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _lists.GetOrAdd(name, static (n, doc) => new YDotNetCrdtList(doc, n), this);
    }

    public ReadOnlyMemory<byte> ToSnapshot()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            using var tx = _doc.ReadTransaction();
            // Full-history state diff — pass null state vector to encode everything.
            return tx.StateDiffV1(null);
        }
    }

    public void ApplySnapshot(ReadOnlyMemory<byte> snapshot)
    {
        if (snapshot.IsEmpty) return;
        lock (_sync)
        {
            ThrowIfDisposed();
            var bytes = snapshot.ToArray();
            using var tx = _doc.WriteTransaction();
            if (tx is null)
            {
                throw new InvalidOperationException("Could not open YDotNet write transaction for ApplySnapshot.");
            }
            var result = tx.ApplyV1(bytes);
            if (result != TransactionUpdateResult.Ok)
            {
                throw new InvalidDataException($"YDotNet rejected snapshot: {result}.");
            }
        }
        NotifyAllContainers();
    }

    public ReadOnlyMemory<byte> EncodeDelta(ReadOnlyMemory<byte> peerVectorClock)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            byte[]? peerSv = peerVectorClock.IsEmpty ? null : peerVectorClock.ToArray();
            using var tx = _doc.ReadTransaction();
            return tx.StateDiffV1(peerSv);
        }
    }

    public void ApplyDelta(ReadOnlyMemory<byte> delta)
    {
        if (delta.IsEmpty) return;
        lock (_sync)
        {
            ThrowIfDisposed();
            var bytes = delta.ToArray();
            using var tx = _doc.WriteTransaction();
            if (tx is null)
            {
                throw new InvalidOperationException("Could not open YDotNet write transaction for ApplyDelta.");
            }
            var result = tx.ApplyV1(bytes);
            if (result != TransactionUpdateResult.Ok)
            {
                throw new InvalidDataException($"YDotNet rejected delta: {result}.");
            }
        }
        NotifyAllContainers();
    }

    public ReadOnlyMemory<byte> VectorClock
    {
        get
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                using var tx = _doc.ReadTransaction();
                return tx.StateVectorV1();
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            _texts.Clear();
            _maps.Clear();
            _lists.Clear();
            _doc.Dispose();
        }
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(YDotNetCrdtDocument));
    }

    private void NotifyAllContainers()
    {
        // Remote updates can change every container; fire best-effort change events.
        foreach (var t in _texts.Values) t.NotifyExternalChange();
        foreach (var m in _maps.Values) m.NotifyExternalChange();
        foreach (var l in _lists.Values) l.NotifyExternalChange();
    }
}

internal sealed class YDotNetCrdtText : ICrdtText
{
    private readonly YDotNetCrdtDocument _owner;
    private readonly Text _text;
    private string _lastValue = string.Empty;

    public event EventHandler<CrdtTextChangedEventArgs>? Changed;

    public YDotNetCrdtText(YDotNetCrdtDocument owner, string name)
    {
        _owner = owner;
        _text = owner.UnderlyingDoc.Text(name);
        _lastValue = SnapshotValue();
    }

    public string Value
    {
        get
        {
            lock (_owner.SyncRoot)
            {
                _lastValue = SnapshotValue();
                return _lastValue;
            }
        }
    }

    public int Length => Value.Length;

    public void Insert(int index, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) return;
        lock (_owner.SyncRoot)
        {
            var len = SnapshotLength();
            if ((uint)index > (uint)len)
                throw new ArgumentOutOfRangeException(nameof(index));
            using var tx = _owner.UnderlyingDoc.WriteTransaction();
            _text.Insert(tx, (uint)index, text);
        }
        FireChange(prevLengthHint: null);
    }

    public void Delete(int index, int length)
    {
        if (length <= 0) return;
        lock (_owner.SyncRoot)
        {
            var len = SnapshotLength();
            if ((uint)index > (uint)len)
                throw new ArgumentOutOfRangeException(nameof(index));
            var clamped = Math.Min((uint)length, (uint)(len - index));
            if (clamped == 0) return;
            using var tx = _owner.UnderlyingDoc.WriteTransaction();
            _text.RemoveRange(tx, (uint)index, clamped);
        }
        FireChange(prevLengthHint: null);
    }

    internal void NotifyExternalChange() => FireChange(prevLengthHint: null);

    private void FireChange(int? prevLengthHint)
    {
        if (Changed is null) return;
        var prev = _lastValue;
        var next = SnapshotValue();
        _lastValue = next;
        if (!string.Equals(prev, next, StringComparison.Ordinal))
        {
            Changed.Invoke(this, new CrdtTextChangedEventArgs(0, next.Length, prev.Length, next));
        }
    }

    private string SnapshotValue()
    {
        using var tx = _owner.UnderlyingDoc.ReadTransaction();
        return _text.String(tx);
    }

    private int SnapshotLength()
    {
        using var tx = _owner.UnderlyingDoc.ReadTransaction();
        return (int)_text.Length(tx);
    }
}

internal sealed class YDotNetCrdtMap : ICrdtMap
{
    private readonly YDotNetCrdtDocument _owner;
    private readonly Map _map;
    private HashSet<string> _lastKeys = new(StringComparer.Ordinal);

    public event EventHandler<CrdtMapChangedEventArgs>? Changed;

    public YDotNetCrdtMap(YDotNetCrdtDocument owner, string name)
    {
        _owner = owner;
        _map = owner.UnderlyingDoc.Map(name);
        _lastKeys = SnapshotKeys();
    }

    public int Count
    {
        get
        {
            lock (_owner.SyncRoot)
            {
                using var tx = _owner.UnderlyingDoc.ReadTransaction();
                return (int)_map.Length(tx);
            }
        }
    }

    public IEnumerable<string> Keys
    {
        get
        {
            lock (_owner.SyncRoot)
            {
                using var tx = _owner.UnderlyingDoc.ReadTransaction();
                using var iter = _map.Iterate(tx);
                // Materialize to an array before returning so the enumerator's transaction
                // lifetime does not escape.
                var list = new List<string>();
                foreach (var kvp in iter)
                {
                    list.Add(kvp.Key);
                }
                return list;
            }
        }
    }

    public T? Get<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_owner.SyncRoot)
        {
            using var tx = _owner.UnderlyingDoc.ReadTransaction();
            var output = _map.Get(tx, key);
            return CoerceOutput<T>(output);
        }
    }

    public void Set<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var json = JsonSerializer.Serialize(value);
        lock (_owner.SyncRoot)
        {
            using var tx = _owner.UnderlyingDoc.WriteTransaction();
            using var input = Input.String(json);
            _map.Insert(tx, key, input);
        }
        FireChange(key, IsDeleted: false);
    }

    public bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        bool removed;
        lock (_owner.SyncRoot)
        {
            using var tx = _owner.UnderlyingDoc.WriteTransaction();
            removed = _map.Remove(tx, key);
        }
        if (removed) FireChange(key, IsDeleted: true);
        return removed;
    }

    public bool ContainsKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_owner.SyncRoot)
        {
            using var tx = _owner.UnderlyingDoc.ReadTransaction();
            return _map.Get(tx, key) is not null;
        }
    }

    internal void NotifyExternalChange()
    {
        var next = SnapshotKeys();
        if (Changed is null)
        {
            _lastKeys = next;
            return;
        }
        foreach (var k in next)
        {
            if (!_lastKeys.Contains(k))
            {
                Changed.Invoke(this, new CrdtMapChangedEventArgs(k, IsDeleted: false));
            }
        }
        foreach (var k in _lastKeys)
        {
            if (!next.Contains(k))
            {
                Changed.Invoke(this, new CrdtMapChangedEventArgs(k, IsDeleted: true));
            }
        }
        _lastKeys = next;
    }

    private HashSet<string> SnapshotKeys()
    {
        using var tx = _owner.UnderlyingDoc.ReadTransaction();
        using var iter = _map.Iterate(tx);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kvp in iter) keys.Add(kvp.Key);
        return keys;
    }

    private void FireChange(string key, bool IsDeleted)
    {
        if (IsDeleted) _lastKeys.Remove(key); else _lastKeys.Add(key);
        Changed?.Invoke(this, new CrdtMapChangedEventArgs(key, IsDeleted));
    }

    internal static T? CoerceOutput<T>(Output? output)
    {
        if (output is null) return default;
        try
        {
            if (output.Tag == OutputTag.String)
            {
                var json = output.String;
                // Values we store are always JSON-encoded strings. Try to deserialize;
                // if the stored cell is a raw string (e.g. from a different writer),
                // return it directly when T == string.
                if (typeof(T) == typeof(string) && !LooksLikeJson(json))
                {
                    return (T)(object)json;
                }
                return JsonSerializer.Deserialize<T>(json);
            }
        }
        catch (JsonException)
        {
            return default;
        }
        return default;
    }

    private static bool LooksLikeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var first = value[0];
        return first == '"' || first == '{' || first == '[' || first == 't' || first == 'f' || first == 'n' || char.IsDigit(first) || first == '-';
    }
}

internal sealed class YDotNetCrdtList : ICrdtList
{
    private readonly YDotNetCrdtDocument _owner;
    private readonly YArray _array;
    private int _lastCount;

    public event EventHandler<CrdtListChangedEventArgs>? Changed;

    public YDotNetCrdtList(YDotNetCrdtDocument owner, string name)
    {
        _owner = owner;
        _array = owner.UnderlyingDoc.Array(name);
        _lastCount = SnapshotCount();
    }

    public int Count
    {
        get
        {
            lock (_owner.SyncRoot)
            {
                return SnapshotCount();
            }
        }
    }

    public T? Get<T>(int index)
    {
        lock (_owner.SyncRoot)
        {
            using var tx = _owner.UnderlyingDoc.ReadTransaction();
            if ((uint)index >= _array.Length(tx)) return default;
            var output = _array.Get(tx, (uint)index);
            return YDotNetCrdtMap.CoerceOutput<T>(output);
        }
    }

    public void Insert<T>(int index, T value)
    {
        var json = JsonSerializer.Serialize(value);
        int prevCount;
        lock (_owner.SyncRoot)
        {
            prevCount = SnapshotCount();
            if ((uint)index > (uint)prevCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            using var tx = _owner.UnderlyingDoc.WriteTransaction();
            using var input = Input.String(json);
            _array.InsertRange(tx, (uint)index, input);
        }
        FireChange(index, inserted: 1, deleted: 0);
    }

    public void Push<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        int prevCount;
        lock (_owner.SyncRoot)
        {
            prevCount = SnapshotCount();
            using var tx = _owner.UnderlyingDoc.WriteTransaction();
            using var input = Input.String(json);
            _array.InsertRange(tx, (uint)prevCount, input);
        }
        FireChange(prevCount, inserted: 1, deleted: 0);
    }

    public bool RemoveAt(int index)
    {
        lock (_owner.SyncRoot)
        {
            using var tx = _owner.UnderlyingDoc.WriteTransaction();
            if ((uint)index >= _array.Length(tx)) return false;
            _array.RemoveRange(tx, (uint)index, 1);
        }
        FireChange(index, inserted: 0, deleted: 1);
        return true;
    }

    internal void NotifyExternalChange()
    {
        var next = SnapshotCount();
        var prev = _lastCount;
        _lastCount = next;
        if (Changed is not null && prev != next)
        {
            Changed.Invoke(this, new CrdtListChangedEventArgs(0, next, prev));
        }
    }

    private int SnapshotCount()
    {
        using var tx = _owner.UnderlyingDoc.ReadTransaction();
        return (int)_array.Length(tx);
    }

    private void FireChange(int index, int inserted, int deleted)
    {
        _lastCount = SnapshotCount();
        Changed?.Invoke(this, new CrdtListChangedEventArgs(index, inserted, deleted));
    }
}

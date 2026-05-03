// ============================================================================
//  WARNING — PROVISIONAL STUB BACKEND, NOT A PRODUCTION CRDT
// ============================================================================
//  This file is the Wave 1.2 stub backend for Sunfish.Kernel.Crdt. Per ADR 0028
//  ("CRDT Engine Selection") the chosen engine is Loro, with Yjs/yrs as the
//  fallback. A 1-week validation spike precedes full commitment.
//
//  Spike outcome (2026-04-22):
//    - LoroCs (https://github.com/sensslen/loro-cs, NuGet v1.10.3) is the only
//      known C# binding to Loro. It is self-described as "very bare bones",
//      lists .NET Standard 2.0 through .NET 10 (not .NET 11), and does not yet
//      expose the snapshot / delta surface this contract requires.
//    - YDotNet (NuGet v0.6.0, 2026-02-14) is the fallback candidate. Mature,
//      but targets net8.0–net10.0; .NET 11 support must be validated.
//    - The spike therefore elects a provisional in-memory stub backend to
//      unblock Wave 1.2 dependents (kernel-event-bus integration, paper §15
//      property-based test harness, federation sync prototypes).
//
//  TODO (ADR 0028 implementation checklist):
//    1. Re-evaluate LoroCs once it exposes snapshot/delta APIs OR upstream a
//       contribution adding them. See https://github.com/sensslen/loro-cs.
//    2. If LoroCs remains "bare bones" at Wave 1B close, execute ADR 0028's
//       documented fallback: swap to YDotNet. Gate: .NET 11 compatibility
//       validated on Windows / macOS / Linux.
//    3. On backend swap, the ICrdtEngine contract is the only surface that
//       changes — by design, per ADR 0028 compatibility plan.
//
//  THIS STUB CONVERGES BY TOTAL-ORDER REPLAY of an op log (not by real CRDT
//  merge semantics). It is deterministic and correct for the tests in this
//  package but does NOT provide the tombstone GC / shallow-snapshot compaction
//  mandated by paper §9. DO NOT SHIP TO PRODUCTION.
// ============================================================================

using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Sunfish.Kernel.Crdt.Backends;

/// <summary>
/// Provisional in-memory stub backend. See file banner.
/// </summary>
public sealed class StubCrdtEngine : ICrdtEngine
{
    /// <inheritdoc />
    public string EngineName => "stub";

    /// <inheritdoc />
    public string EngineVersion => "0.1.0-stub";

    /// <inheritdoc />
    public ICrdtDocument CreateDocument(string documentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);
        return new StubCrdtDocument(documentId);
    }

    /// <inheritdoc />
    public ICrdtDocument OpenDocument(string documentId, ReadOnlyMemory<byte> snapshot)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);
        var doc = new StubCrdtDocument(documentId);
        if (!snapshot.IsEmpty)
        {
            doc.ApplySnapshot(snapshot);
        }
        return doc;
    }
}

/// <summary>
/// Operation kinds in the stub log. Deliberately a tight enum — every op in the
/// document's history serializes to one of these variants.
/// </summary>
internal enum StubOpKind
{
    TextInsert,
    TextDelete,
    MapSet,
    MapDelete,
    ListInsert,
    ListDelete,
}

/// <summary>
/// One entry in the stub op log. (Actor, Lamport) uniquely identifies an op;
/// ties in lamport are broken by actor-id string comparison for determinism.
/// </summary>
internal sealed record StubOp(
    string Actor,
    ulong Lamport,
    StubOpKind Kind,
    string Container,
    int Index,
    int Length,
    string? Text,
    JsonElement? Value);

internal sealed class StubCrdtDocument : ICrdtDocument
{
    private readonly string _actorId = Guid.NewGuid().ToString("N");
    private readonly List<StubOp> _ops = new();
    private readonly HashSet<(string Actor, ulong Lamport)> _seen = new();
    private readonly Dictionary<string, ulong> _clock = new();
    private readonly Dictionary<string, StubCrdtText> _texts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StubCrdtMap> _maps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StubCrdtList> _lists = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private ulong _lamport;

    public StubCrdtDocument(string documentId)
    {
        DocumentId = documentId;
    }

    public string DocumentId { get; }

    internal string ActorId => _actorId;

    public ICrdtText GetText(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        lock (_sync)
        {
            if (!_texts.TryGetValue(name, out var t))
            {
                t = new StubCrdtText(this, name);
                _texts[name] = t;
                t.Replay(_ops);
            }
            return t;
        }
    }

    public ICrdtMap GetMap(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        lock (_sync)
        {
            if (!_maps.TryGetValue(name, out var m))
            {
                m = new StubCrdtMap(this, name);
                _maps[name] = m;
                m.Replay(_ops);
            }
            return m;
        }
    }

    public ICrdtList GetList(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        lock (_sync)
        {
            if (!_lists.TryGetValue(name, out var l))
            {
                l = new StubCrdtList(this, name);
                _lists[name] = l;
                l.Replay(_ops);
            }
            return l;
        }
    }

    public ReadOnlyMemory<byte> ToSnapshot()
    {
        lock (_sync)
        {
            var payload = new StubSnapshot
            {
                DocumentId = DocumentId,
                Ops = _ops.Select(SerializableOp.From).ToList(),
            };
            return JsonSerializer.SerializeToUtf8Bytes(payload, StubSerializer.Options);
        }
    }

    public void ApplySnapshot(ReadOnlyMemory<byte> snapshot)
    {
        if (snapshot.IsEmpty) return;
        StubSnapshot? payload;
        try
        {
            payload = JsonSerializer.Deserialize<StubSnapshot>(snapshot.Span, StubSerializer.Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Snapshot payload is not a valid stub CRDT snapshot.", ex);
        }
        if (payload is null) return;
        lock (_sync)
        {
            foreach (var op in payload.Ops)
            {
                IngestOp(op.ToOp());
            }
            ReplayAll();
        }
    }

    public ReadOnlyMemory<byte> EncodeDelta(ReadOnlyMemory<byte> peerVectorClock)
    {
        Dictionary<string, ulong> peerClock = peerVectorClock.IsEmpty
            ? new Dictionary<string, ulong>()
            : DecodeClock(peerVectorClock);

        lock (_sync)
        {
            var missing = _ops
                .Where(o => !peerClock.TryGetValue(o.Actor, out var peerLamport) || o.Lamport > peerLamport)
                .Select(SerializableOp.From)
                .ToList();
            var payload = new StubDelta { Ops = missing };
            return JsonSerializer.SerializeToUtf8Bytes(payload, StubSerializer.Options);
        }
    }

    public void ApplyDelta(ReadOnlyMemory<byte> delta)
    {
        if (delta.IsEmpty) return;
        StubDelta? payload;
        try
        {
            payload = JsonSerializer.Deserialize<StubDelta>(delta.Span, StubSerializer.Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Delta payload is not a valid stub CRDT delta.", ex);
        }
        if (payload is null || payload.Ops.Count == 0) return;
        lock (_sync)
        {
            var any = false;
            foreach (var op in payload.Ops)
            {
                if (IngestOp(op.ToOp())) any = true;
            }
            if (any) ReplayAll();
        }
    }

    public ReadOnlyMemory<byte> VectorClock
    {
        get
        {
            lock (_sync)
            {
                return JsonSerializer.SerializeToUtf8Bytes(_clock, StubSerializer.Options);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            _ops.Clear();
            _seen.Clear();
            _clock.Clear();
            _texts.Clear();
            _maps.Clear();
            _lists.Clear();
        }
        return ValueTask.CompletedTask;
    }

    internal void AppendLocal(StubOpKind kind, string container, int index, int length, string? text, object? value)
    {
        lock (_sync)
        {
            _lamport = Math.Max(_lamport, LamportFor(_actorId)) + 1;
            JsonElement? jsonValue = value is null
                ? null
                : JsonSerializer.SerializeToElement(value, StubSerializer.Options);
            var op = new StubOp(_actorId, _lamport, kind, container, index, length, text, jsonValue);
            IngestOp(op);
            ReplayAll();
        }
    }

    private ulong LamportFor(string actor)
        => _clock.TryGetValue(actor, out var l) ? l : 0UL;

    private bool IngestOp(StubOp op)
    {
        if (!_seen.Add((op.Actor, op.Lamport))) return false;
        _ops.Add(op);
        _ops.Sort(StubOpComparer.Instance);
        if (!_clock.TryGetValue(op.Actor, out var cur) || op.Lamport > cur)
        {
            _clock[op.Actor] = op.Lamport;
        }
        if (op.Lamport > _lamport) _lamport = op.Lamport;
        return true;
    }

    private void ReplayAll()
    {
        foreach (var t in _texts.Values) t.Replay(_ops);
        foreach (var m in _maps.Values) m.Replay(_ops);
        foreach (var l in _lists.Values) l.Replay(_ops);
    }

    private static Dictionary<string, ulong> DecodeClock(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, ulong>>(bytes.Span, StubSerializer.Options)
                ?? new Dictionary<string, ulong>();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Vector clock payload is not valid.", ex);
        }
    }
}

internal sealed class StubOpComparer : IComparer<StubOp>
{
    public static readonly StubOpComparer Instance = new();

    public int Compare(StubOp? x, StubOp? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        var cmp = x.Lamport.CompareTo(y.Lamport);
        if (cmp != 0) return cmp;
        return string.CompareOrdinal(x.Actor, y.Actor);
    }
}

internal sealed class StubSnapshot
{
    public string DocumentId { get; set; } = string.Empty;
    public List<SerializableOp> Ops { get; set; } = new();
}

internal sealed class StubDelta
{
    public List<SerializableOp> Ops { get; set; } = new();
}

internal sealed class SerializableOp
{
    public string Actor { get; set; } = string.Empty;
    public ulong Lamport { get; set; }
    public StubOpKind Kind { get; set; }
    public string Container { get; set; } = string.Empty;
    public int Index { get; set; }
    public int Length { get; set; }
    public string? Text { get; set; }
    public JsonElement? Value { get; set; }

    public static SerializableOp From(StubOp op) => new()
    {
        Actor = op.Actor,
        Lamport = op.Lamport,
        Kind = op.Kind,
        Container = op.Container,
        Index = op.Index,
        Length = op.Length,
        Text = op.Text,
        Value = op.Value,
    };

    public StubOp ToOp() => new(Actor, Lamport, Kind, Container, Index, Length, Text, Value);
}

internal static class StubSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = false,
        WriteIndented = false,
    };
}

internal sealed class StubCrdtText : ICrdtText
{
    private readonly StubCrdtDocument _doc;
    private readonly string _name;
    private string _value = string.Empty;

    public event EventHandler<CrdtTextChangedEventArgs>? Changed;

    public StubCrdtText(StubCrdtDocument doc, string name)
    {
        _doc = doc;
        _name = name;
    }

    public string Value => _value;
    public int Length => _value.Length;

    public void Insert(int index, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) return;
        if ((uint)index > (uint)_value.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        _doc.AppendLocal(StubOpKind.TextInsert, _name, index, 0, text, null);
    }

    public void Delete(int index, int length)
    {
        if (length <= 0) return;
        if ((uint)index > (uint)_value.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        length = Math.Min(length, _value.Length - index);
        if (length <= 0) return;
        _doc.AppendLocal(StubOpKind.TextDelete, _name, index, length, null, null);
    }

    internal void Replay(IReadOnlyList<StubOp> ops)
    {
        var prev = _value;
        var sb = new StringBuilder();
        foreach (var op in ops)
        {
            if (!string.Equals(op.Container, _name, StringComparison.Ordinal)) continue;
            switch (op.Kind)
            {
                case StubOpKind.TextInsert when op.Text is not null:
                {
                    var idx = Math.Min(op.Index, sb.Length);
                    sb.Insert(idx, op.Text);
                    break;
                }
                case StubOpKind.TextDelete:
                {
                    var idx = Math.Min(op.Index, sb.Length);
                    var len = Math.Min(op.Length, sb.Length - idx);
                    if (len > 0) sb.Remove(idx, len);
                    break;
                }
                default:
                    break;
            }
        }
        _value = sb.ToString();
        if (!string.Equals(prev, _value, StringComparison.Ordinal))
        {
            // Best-effort single event; stub replay does not diff precisely.
            Changed?.Invoke(this, new CrdtTextChangedEventArgs(0, _value.Length, prev.Length, _value));
        }
    }
}

internal sealed class StubCrdtMap : ICrdtMap
{
    private readonly StubCrdtDocument _doc;
    private readonly string _name;
    private readonly Dictionary<string, JsonElement> _values = new(StringComparer.Ordinal);

    public event EventHandler<CrdtMapChangedEventArgs>? Changed;

    public StubCrdtMap(StubCrdtDocument doc, string name)
    {
        _doc = doc;
        _name = name;
    }

    public int Count => _values.Count;
    public IEnumerable<string> Keys => _values.Keys.ToArray();

    public T? Get<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_values.TryGetValue(key, out var element)) return default;
        try
        {
            return element.Deserialize<T>(StubSerializer.Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public void Set<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _doc.AppendLocal(StubOpKind.MapSet, _name, 0, 0, key, value);
    }

    public bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!_values.ContainsKey(key)) return false;
        _doc.AppendLocal(StubOpKind.MapDelete, _name, 0, 0, key, null);
        return true;
    }

    public bool ContainsKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _values.ContainsKey(key);
    }

    internal void Replay(IReadOnlyList<StubOp> ops)
    {
        var prev = new Dictionary<string, JsonElement>(_values, StringComparer.Ordinal);
        _values.Clear();
        foreach (var op in ops)
        {
            if (!string.Equals(op.Container, _name, StringComparison.Ordinal)) continue;
            var key = op.Text;
            if (key is null) continue;
            switch (op.Kind)
            {
                case StubOpKind.MapSet when op.Value is not null:
                    _values[key] = op.Value.Value;
                    break;
                case StubOpKind.MapDelete:
                    _values.Remove(key);
                    break;
                default:
                    break;
            }
        }
        EmitChanges(prev, _values);
    }

    private void EmitChanges(Dictionary<string, JsonElement> prev, Dictionary<string, JsonElement> current)
    {
        if (Changed is null) return;
        foreach (var key in current.Keys)
        {
            if (!prev.ContainsKey(key))
            {
                Changed.Invoke(this, new CrdtMapChangedEventArgs(key, IsDeleted: false));
            }
        }
        foreach (var key in prev.Keys)
        {
            if (!current.ContainsKey(key))
            {
                Changed.Invoke(this, new CrdtMapChangedEventArgs(key, IsDeleted: true));
            }
        }
    }
}

internal sealed class StubCrdtList : ICrdtList
{
    private readonly StubCrdtDocument _doc;
    private readonly string _name;
    private readonly List<JsonElement> _items = new();

    public event EventHandler<CrdtListChangedEventArgs>? Changed;

    public StubCrdtList(StubCrdtDocument doc, string name)
    {
        _doc = doc;
        _name = name;
    }

    public int Count => _items.Count;

    public T? Get<T>(int index)
    {
        if ((uint)index >= (uint)_items.Count) return default;
        try
        {
            return _items[index].Deserialize<T>(StubSerializer.Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public void Insert<T>(int index, T value)
    {
        if ((uint)index > (uint)_items.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _doc.AppendLocal(StubOpKind.ListInsert, _name, index, 0, null, value);
    }

    public void Push<T>(T value)
    {
        _doc.AppendLocal(StubOpKind.ListInsert, _name, _items.Count, 0, null, value);
    }

    public bool RemoveAt(int index)
    {
        if ((uint)index >= (uint)_items.Count) return false;
        _doc.AppendLocal(StubOpKind.ListDelete, _name, index, 1, null, null);
        return true;
    }

    internal void Replay(IReadOnlyList<StubOp> ops)
    {
        var prevCount = _items.Count;
        _items.Clear();
        foreach (var op in ops)
        {
            if (!string.Equals(op.Container, _name, StringComparison.Ordinal)) continue;
            switch (op.Kind)
            {
                case StubOpKind.ListInsert when op.Value is not null:
                {
                    var idx = Math.Min(op.Index, _items.Count);
                    _items.Insert(idx, op.Value.Value);
                    break;
                }
                case StubOpKind.ListDelete:
                {
                    var idx = op.Index;
                    if ((uint)idx < (uint)_items.Count)
                    {
                        _items.RemoveAt(idx);
                    }
                    break;
                }
                default:
                    break;
            }
        }
        if (prevCount != _items.Count)
        {
            Changed?.Invoke(this, new CrdtListChangedEventArgs(0, _items.Count, prevCount));
        }
    }
}

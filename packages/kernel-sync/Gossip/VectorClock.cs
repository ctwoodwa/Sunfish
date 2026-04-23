using System.Formats.Cbor;

namespace Sunfish.Kernel.Sync.Gossip;

/// <summary>
/// Relationship between two vector clocks. Per Lamport / Mattern:
/// every pair is one of these four cases. Used by the gossip scheduler
/// to decide whether to send a delta, receive one, or do both.
/// </summary>
public enum VectorClockRelationship
{
    /// <summary>Both clocks carry identical seq numbers for every known node.</summary>
    Equal,
    /// <summary>The left clock dominates — every entry is &gt;= the right, at least one strictly &gt;.</summary>
    Dominates,
    /// <summary>The left clock is dominated by the right — strict mirror of <see cref="Dominates"/>.</summary>
    Dominated,
    /// <summary>Neither clock dominates — each has at least one entry the other does not.</summary>
    Concurrent,
}

/// <summary>
/// Mutable vector clock keyed by node-id (hex-string form of the 16-byte UUID).
/// Paper §6.1 and sync-daemon-protocol §3.6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Key form:</b> keys are hex strings of the 16-byte <c>node_id</c> so the
/// clock can be rendered to text logs directly and so the CBOR key-type (<c>tstr</c>)
/// matches the <c>GOSSIP_PING.vector_clock</c> encoding in
/// <see cref="Protocol.GossipPingMessage"/>. The spec's map&lt;bstr, u64&gt;
/// shape is honored by the helper methods that translate node-id byte
/// arrays to/from hex on the boundary.
/// </para>
/// <para>
/// <b>Thread safety:</b> the clock guards its internal dictionary under a
/// simple lock so Set / Merge / Get
/// may safely interleave on the gossip scheduler thread and the receive loop.
/// </para>
/// </remarks>
public sealed class VectorClock
{
    private readonly object _lock = new();
    private readonly Dictionary<string, ulong> _entries;

    public VectorClock()
    {
        _entries = new Dictionary<string, ulong>(StringComparer.Ordinal);
    }

    public VectorClock(IReadOnlyDictionary<string, ulong> seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        _entries = new Dictionary<string, ulong>(seed, StringComparer.Ordinal);
    }

    /// <summary>Snapshot of the clock as an immutable dictionary. Cheap — copies once.</summary>
    public IReadOnlyDictionary<string, ulong> Snapshot()
    {
        lock (_lock)
        {
            return new Dictionary<string, ulong>(_entries, StringComparer.Ordinal);
        }
    }

    /// <summary>Set or overwrite the entry for the given node id. A value that goes
    /// backwards is allowed — callers own the invariant, we do not enforce monotonicity.</summary>
    public void Set(string nodeId, ulong sequence)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        lock (_lock)
        {
            _entries[nodeId] = sequence;
        }
    }

    /// <summary>Byte-id overload; the caller's raw <c>node_id</c> is hex-encoded first.</summary>
    public void Set(byte[] nodeId, ulong sequence)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        Set(Convert.ToHexString(nodeId), sequence);
    }

    /// <summary>Return the entry for the given node id, or zero if absent.</summary>
    public ulong Get(string nodeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        lock (_lock)
        {
            return _entries.TryGetValue(nodeId, out var v) ? v : 0ul;
        }
    }

    public ulong Get(byte[] nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        return Get(Convert.ToHexString(nodeId));
    }

    /// <summary>
    /// Pointwise-max merge. A key present in either clock appears in the result
    /// with the higher sequence; missing entries default to zero.
    /// </summary>
    public void Merge(VectorClock other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var snap = other.Snapshot();
        lock (_lock)
        {
            foreach (var kvp in snap)
            {
                if (!_entries.TryGetValue(kvp.Key, out var mine) || mine < kvp.Value)
                {
                    _entries[kvp.Key] = kvp.Value;
                }
            }
        }
    }

    /// <summary>
    /// Does this clock dominate <paramref name="other"/>? True iff every entry
    /// in <paramref name="other"/> is &lt;= this one AND at least one is
    /// strictly less. A clock does not dominate itself.
    /// </summary>
    public bool Dominates(VectorClock other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Compare(this, other) == VectorClockRelationship.Dominates;
    }

    /// <summary>
    /// Classify the relationship between <paramref name="left"/> and
    /// <paramref name="right"/>. Returns <see cref="VectorClockRelationship.Equal"/>
    /// when the two clocks agree on every known key, etc.
    /// </summary>
    public static VectorClockRelationship Compare(VectorClock left, VectorClock right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var l = left.Snapshot();
        var r = right.Snapshot();

        var leftAhead = false;
        var rightAhead = false;

        // Walk the union of keys so entries missing on one side count as zero.
        var keys = new HashSet<string>(l.Keys, StringComparer.Ordinal);
        foreach (var k in r.Keys) keys.Add(k);

        foreach (var k in keys)
        {
            var lv = l.TryGetValue(k, out var lvRaw) ? lvRaw : 0ul;
            var rv = r.TryGetValue(k, out var rvRaw) ? rvRaw : 0ul;
            if (lv > rv) leftAhead = true;
            else if (lv < rv) rightAhead = true;
            if (leftAhead && rightAhead) return VectorClockRelationship.Concurrent;
        }

        if (leftAhead) return VectorClockRelationship.Dominates;
        if (rightAhead) return VectorClockRelationship.Dominated;
        return VectorClockRelationship.Equal;
    }

    /// <summary>Serialize the clock to canonical CBOR as a <c>map&lt;tstr, u64&gt;</c>.</summary>
    public byte[] ToCbor()
    {
        var snap = Snapshot();
        var writer = new CborWriter(CborConformanceMode.Canonical, convertIndefiniteLengthEncodings: true);
        writer.WriteStartMap(snap.Count);
        foreach (var kvp in snap)
        {
            writer.WriteTextString(kvp.Key);
            writer.WriteUInt64(kvp.Value);
        }
        writer.WriteEndMap();
        return writer.Encode();
    }

    /// <summary>Deserialize a canonical-CBOR <c>map&lt;tstr, u64&gt;</c>.</summary>
    public static VectorClock FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = new CborReader(cbor.ToArray(), CborConformanceMode.Canonical);
        var count = reader.ReadStartMap();
        var dict = new Dictionary<string, ulong>(count ?? 0, StringComparer.Ordinal);
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            var value = reader.ReadUInt64();
            dict[key] = value;
        }
        reader.ReadEndMap();
        return new VectorClock(dict);
    }
}

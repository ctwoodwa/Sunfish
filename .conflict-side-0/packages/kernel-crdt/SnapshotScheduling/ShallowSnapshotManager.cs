namespace Sunfish.Kernel.Crdt.SnapshotScheduling;

/// <summary>
/// Default in-process implementation of <see cref="IShallowSnapshotManager"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stub-backend caveat (paper §9 + ADR 0028):</b> the provisional
/// <see cref="Backends.StubCrdtEngine"/> has no op-history to discard — a "shallow
/// snapshot" on the stub is just a serialization of current state. This manager
/// therefore records the snapshot bytes and a timestamp, but <see cref="ShallowSnapshotRecord.OperationsDiscarded"/>
/// is always reported as <c>0</c> for the stub. When the backend swaps to Loro/Yjs,
/// genuine op-history truncation becomes available and this implementation will
/// surface the real discarded count.
/// </para>
/// <para>
/// TODO (ADR 0028): when the real backend lands, extend <see cref="ICrdtDocument"/>
/// with a shallow-snapshot primitive that returns the discarded op count and replace
/// the body of <see cref="TakeSnapshotAsyncCore"/> accordingly.
/// </para>
/// </remarks>
public sealed class ShallowSnapshotManager : IShallowSnapshotManager
{
    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.Ordinal);
    private readonly List<ShallowSnapshotRecord> _snapshots = new();
    private readonly Dictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private readonly TimeProvider _clock;

    /// <summary>Create a manager with the system clock.</summary>
    public ShallowSnapshotManager() : this(TimeProvider.System) { }

    /// <summary>Create a manager with a pluggable <see cref="TimeProvider"/> for tests.</summary>
    public ShallowSnapshotManager(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <inheritdoc />
    public event EventHandler<ShallowSnapshotTakenEventArgs>? SnapshotTaken;

    /// <inheritdoc />
    public IReadOnlyList<ShallowSnapshotRecord> Snapshots
    {
        get
        {
            lock (_sync) { return _snapshots.ToArray(); }
        }
    }

    /// <inheritdoc />
    public void Register(ICrdtDocument doc, IShallowSnapshotPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(policy);
        lock (_sync)
        {
            _registrations[doc.DocumentId] = new Registration(doc, policy);
            if (!_gates.ContainsKey(doc.DocumentId))
            {
                _gates[doc.DocumentId] = new SemaphoreSlim(1, 1);
            }
        }
    }

    /// <inheritdoc />
    public async Task<ShallowSnapshotRecord> TakeSnapshotAsync(string documentId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);
        Registration reg;
        SemaphoreSlim gate;
        lock (_sync)
        {
            if (!_registrations.TryGetValue(documentId, out var r))
            {
                throw new KeyNotFoundException($"Document '{documentId}' is not registered with the shallow-snapshot manager.");
            }
            reg = r;
            gate = _gates[documentId];
        }

        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return TakeSnapshotAsyncCore(reg);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShallowSnapshotRecord>> RunEvaluationAsync(CancellationToken ct = default)
    {
        Registration[] registrations;
        lock (_sync) { registrations = _registrations.Values.ToArray(); }

        var taken = new List<ShallowSnapshotRecord>();
        var now = _clock.GetUtcNow();

        foreach (var reg in registrations)
        {
            ct.ThrowIfCancellationRequested();
            var stats = reg.BuildStatistics(now);
            if (!reg.Policy.ShouldTakeShallowSnapshot(reg.Document, stats, now)) continue;

            var record = await TakeSnapshotAsync(reg.Document.DocumentId, ct).ConfigureAwait(false);
            taken.Add(record);
        }
        return taken;
    }

    private ShallowSnapshotRecord TakeSnapshotAsyncCore(Registration reg)
    {
        // Capture the snapshot while holding the per-document gate. The stub backend has
        // no notion of "op history to discard", so OperationsDiscarded is reported as 0
        // for now — see class remarks and the backend-swap TODO.
        var bytes = reg.Document.ToSnapshot();
        var now = _clock.GetUtcNow();
        var record = new ShallowSnapshotRecord(
            DocumentId: reg.Document.DocumentId,
            TakenAt: now,
            OperationsDiscarded: 0UL,
            SnapshotBytes: bytes);

        lock (_sync)
        {
            _snapshots.Add(record);
            reg.LastSnapshotAt = now;
        }
        SnapshotTaken?.Invoke(this, new ShallowSnapshotTakenEventArgs(record));
        return record;
    }

    private sealed class Registration
    {
        public Registration(ICrdtDocument doc, IShallowSnapshotPolicy policy)
        {
            Document = doc;
            Policy = policy;
        }

        public ICrdtDocument Document { get; }
        public IShallowSnapshotPolicy Policy { get; }
        public DateTimeOffset? LastSnapshotAt { get; set; }

        public DocumentStatistics BuildStatistics(DateTimeOffset now)
        {
            // The stub backend doesn't expose an op-count; use the serialized byte size
            // and vector-clock entry-count as proxies. When a real backend lands it should
            // surface an explicit op-count on ICrdtDocument.
            var snapshot = Document.ToSnapshot();
            ulong byteSize = (ulong)snapshot.Length;
            var vc = Document.VectorClock;
            ulong opCount = EstimateOpCount(vc);
            return new DocumentStatistics(
                OperationCount: opCount,
                ByteSize: byteSize,
                LastOperationAt: now,
                LastShallowSnapshotAt: LastSnapshotAt);
        }

        private static ulong EstimateOpCount(ReadOnlyMemory<byte> vectorClock)
        {
            // Vector clock is an opaque JSON blob in the stub: { actor: lamport, ... }.
            // Sum of lamports is a conservative lower bound on op count. Real backends
            // should replace this with a native op-counter.
            if (vectorClock.IsEmpty) return 0UL;
            try
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ulong>>(vectorClock.Span);
                if (dict is null) return 0UL;
                ulong sum = 0UL;
                foreach (var v in dict.Values) { sum += v; }
                return sum;
            }
            catch (System.Text.Json.JsonException)
            {
                return 0UL;
            }
        }
    }
}

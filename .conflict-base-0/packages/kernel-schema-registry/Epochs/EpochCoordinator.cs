namespace Sunfish.Kernel.SchemaRegistry.Epochs;

/// <summary>
/// In-memory <see cref="IEpochCoordinator"/>. Backed by a simple <c>List&lt;EpochRecord&gt;</c>
/// guarded by a single mutex. Intended for tests, local-node single-process scenarios, and
/// as the placeholder that Wave 2.3 replaces once the distributed Flease lease lands.
/// </summary>
/// <remarks>
/// <para>
/// The coordinator starts with a single <see cref="EpochStatus.Active"/> epoch named
/// <c>epoch-1</c> so callers always have a current epoch to scope writes against.
/// Announcing subsequent epochs appends records in <see cref="EpochStatus.Announced"/>
/// status; freezing an epoch promotes the next-announced epoch to
/// <see cref="EpochStatus.Active"/> if no currently-active epoch exists.
/// </para>
/// <para>
/// <b>Concurrency:</b> all mutations and reads hold a single mutex. This is fine for the
/// low-frequency epoch-transition path (epochs are rare) and keeps the state machine easy
/// to reason about.
/// </para>
/// </remarks>
public sealed class EpochCoordinator : IEpochCoordinator
{
    private readonly object _gate = new();
    private readonly List<EpochRecord> _epochs = new();
    private readonly TimeProvider _time;

    /// <summary>Create a coordinator seeded with a genesis epoch. Uses <see cref="TimeProvider.System"/> for timestamps.</summary>
    public EpochCoordinator() : this(TimeProvider.System) { }

    /// <summary>Create a coordinator using the supplied <paramref name="time"/> provider (testability).</summary>
    public EpochCoordinator(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;

        _epochs.Add(new EpochRecord(
            Id: "epoch-1",
            PreviousId: string.Empty,
            AnnouncedAt: _time.GetUtcNow(),
            Status: EpochStatus.Active,
            CutoverNodes: Array.Empty<string>(),
            ReasonSummary: "Genesis epoch."));
    }

    /// <inheritdoc />
    public string CurrentEpochId
    {
        get
        {
            lock (_gate)
            {
                // Latest non-frozen epoch wins. Walk in reverse so newly-announced
                // epochs take precedence over older Active ones once they're
                // available.
                for (var i = _epochs.Count - 1; i >= 0; i--)
                {
                    if (_epochs[i].Status != EpochStatus.Frozen)
                    {
                        return _epochs[i].Id;
                    }
                }
                // All epochs frozen — shouldn't happen in practice but fall back to the newest.
                return _epochs[^1].Id;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<EpochRecord> Epochs
    {
        get
        {
            lock (_gate)
            {
                return _epochs.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<EpochAnnouncedEventArgs>? EpochAnnounced;

    /// <inheritdoc />
    public event EventHandler<EpochFrozenEventArgs>? EpochFrozen;

    /// <inheritdoc />
    public Task<string> AnnounceEpochAsync(string reasonSummary, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reasonSummary);
        ct.ThrowIfCancellationRequested();

        EpochRecord announced;
        lock (_gate)
        {
            var previous = _epochs[^1];
            var nextIndex = _epochs.Count + 1;
            announced = new EpochRecord(
                Id: $"epoch-{nextIndex}",
                PreviousId: previous.Id,
                AnnouncedAt: _time.GetUtcNow(),
                Status: EpochStatus.Announced,
                CutoverNodes: Array.Empty<string>(),
                ReasonSummary: reasonSummary);
            _epochs.Add(announced);
        }

        EpochAnnounced?.Invoke(this, new EpochAnnouncedEventArgs(announced));
        return Task.FromResult(announced.Id);
    }

    /// <inheritdoc />
    public Task RecordNodeCutoverAsync(string nodeId, string epochId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        ArgumentException.ThrowIfNullOrEmpty(epochId);
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var index = _epochs.FindIndex(e => e.Id == epochId);
            if (index < 0)
            {
                throw new ArgumentException(
                    $"Epoch '{epochId}' is not known to this coordinator.",
                    nameof(epochId));
            }

            var existing = _epochs[index];
            if (existing.CutoverNodes.Contains(nodeId))
            {
                // Idempotent — already recorded.
                return Task.CompletedTask;
            }

            var updated = new List<string>(existing.CutoverNodes) { nodeId };
            _epochs[index] = existing with { CutoverNodes = updated };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task FreezeEpochAsync(string epochId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(epochId);
        ct.ThrowIfCancellationRequested();

        EpochRecord frozen;
        lock (_gate)
        {
            var index = _epochs.FindIndex(e => e.Id == epochId);
            if (index < 0)
            {
                throw new ArgumentException(
                    $"Epoch '{epochId}' is not known to this coordinator.",
                    nameof(epochId));
            }

            var existing = _epochs[index];
            if (existing.Status == EpochStatus.Frozen)
            {
                throw new InvalidOperationException(
                    $"Epoch '{epochId}' is already frozen.");
            }

            frozen = existing with { Status = EpochStatus.Frozen };
            _epochs[index] = frozen;

            // Promote the latest Announced epoch to Active if no Active epoch remains.
            if (!_epochs.Any(e => e.Status == EpochStatus.Active))
            {
                for (var i = _epochs.Count - 1; i >= 0; i--)
                {
                    if (_epochs[i].Status == EpochStatus.Announced)
                    {
                        _epochs[i] = _epochs[i] with { Status = EpochStatus.Active };
                        break;
                    }
                }
            }
        }

        EpochFrozen?.Invoke(this, new EpochFrozenEventArgs(frozen));
        return Task.CompletedTask;
    }
}

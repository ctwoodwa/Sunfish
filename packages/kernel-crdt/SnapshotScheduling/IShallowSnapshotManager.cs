namespace Sunfish.Kernel.Crdt.SnapshotScheduling;

/// <summary>
/// Coordinates shallow snapshots for a collection of registered documents. Paper §9.
/// </summary>
public interface IShallowSnapshotManager
{
    /// <summary>
    /// Register a document for potential snapshot management under the given policy.
    /// Re-registering a previously-registered document replaces its policy.
    /// </summary>
    void Register(ICrdtDocument doc, IShallowSnapshotPolicy policy);

    /// <summary>
    /// Take a shallow snapshot of the document now, regardless of policy. The document must
    /// have been registered; throws <see cref="KeyNotFoundException"/> otherwise.
    /// </summary>
    Task<ShallowSnapshotRecord> TakeSnapshotAsync(string documentId, CancellationToken ct = default);

    /// <summary>
    /// Run a single policy-evaluation pass across all registered documents and take a
    /// snapshot of each one whose policy returns <c>true</c>. Passes are expected to run
    /// on a cadence driven by the host (background service, on-idle hook, manual trigger);
    /// this contract does not prescribe the cadence.
    /// </summary>
    Task<IReadOnlyList<ShallowSnapshotRecord>> RunEvaluationAsync(CancellationToken ct = default);

    /// <summary>History of every snapshot taken by this manager, newest last.</summary>
    IReadOnlyList<ShallowSnapshotRecord> Snapshots { get; }

    /// <summary>Raised synchronously after each shallow snapshot is recorded.</summary>
    event EventHandler<ShallowSnapshotTakenEventArgs>? SnapshotTaken;
}

/// <summary>A record of a single shallow-snapshot event.</summary>
/// <param name="DocumentId">Identifier of the document that was snapshotted.</param>
/// <param name="TakenAt">Timestamp at which the snapshot was recorded.</param>
/// <param name="OperationsDiscarded">Number of operations conceptually discarded by this snapshot. See remarks on the stub limitation in <see cref="ShallowSnapshotManager"/>.</param>
/// <param name="SnapshotBytes">The captured snapshot bytes.</param>
public sealed record ShallowSnapshotRecord(
    string DocumentId,
    DateTimeOffset TakenAt,
    ulong OperationsDiscarded,
    ReadOnlyMemory<byte> SnapshotBytes);

/// <summary>Event arguments for <see cref="IShallowSnapshotManager.SnapshotTaken"/>.</summary>
public sealed record ShallowSnapshotTakenEventArgs(ShallowSnapshotRecord Record);

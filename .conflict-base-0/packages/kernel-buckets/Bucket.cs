namespace Sunfish.Kernel.Buckets;

/// <summary>
/// Runtime instance of a bucket: the declarative <see cref="BucketDefinition"/> paired with the
/// live set of subscribed peers and the current membership of records that passed the filter.
/// </summary>
/// <remarks>
/// <para>
/// This is a light-weight, in-memory composition. It does <b>not</b> own a persistence layer —
/// the actual record bodies live in <see cref="Sunfish.Kernel.Events.IEventLog"/>; the bucket
/// simply tracks which record ids belong to which bucket so the sync daemon can filter gossip
/// deltas.
/// </para>
/// <para>
/// Thread-safety: all mutations are performed under an internal lock. Reads return snapshots.
/// Callers that need consistency across multiple reads should take them within a single call.
/// </para>
/// </remarks>
public sealed class Bucket
{
    private readonly object _gate = new();
    private readonly HashSet<string> _memberRecordIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _subscribedPeerIds = new(StringComparer.Ordinal);

    /// <summary>Creates a runtime bucket instance from a declarative definition.</summary>
    /// <param name="definition">The parsed bucket definition (paper §10.2).</param>
    public Bucket(BucketDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>The declarative definition this runtime bucket is instantiated from.</summary>
    public BucketDefinition Definition { get; }

    /// <summary>Convenience accessor for <see cref="BucketDefinition.Name"/>.</summary>
    public string Name => Definition.Name;

    /// <summary>Current count of records whose filter evaluation matched.</summary>
    public int MemberCount
    {
        get { lock (_gate) { return _memberRecordIds.Count; } }
    }

    /// <summary>Current count of peers that have subscribed to this bucket.</summary>
    public int SubscribedPeerCount
    {
        get { lock (_gate) { return _subscribedPeerIds.Count; } }
    }

    /// <summary>Record-ids that currently pass the bucket's filter. Snapshot at call time.</summary>
    public IReadOnlyCollection<string> MemberRecordIds
    {
        get
        {
            lock (_gate)
            {
                return _memberRecordIds.ToArray();
            }
        }
    }

    /// <summary>Peer identifiers currently subscribed to this bucket. Snapshot at call time.</summary>
    public IReadOnlyCollection<string> SubscribedPeerIds
    {
        get
        {
            lock (_gate)
            {
                return _subscribedPeerIds.ToArray();
            }
        }
    }

    /// <summary>Add a record to this bucket's membership set.</summary>
    /// <returns><c>true</c> if the record was newly added; <c>false</c> if it was already present.</returns>
    public bool AddRecord(string recordId)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordId);
        lock (_gate) { return _memberRecordIds.Add(recordId); }
    }

    /// <summary>Remove a record from this bucket's membership set.</summary>
    /// <returns><c>true</c> if the record was removed; <c>false</c> if it was not present.</returns>
    public bool RemoveRecord(string recordId)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordId);
        lock (_gate) { return _memberRecordIds.Remove(recordId); }
    }

    /// <summary>Register a peer as a subscriber to this bucket (post-eligibility check).</summary>
    /// <returns><c>true</c> if newly added; <c>false</c> if already subscribed.</returns>
    public bool AddSubscriber(string peerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(peerId);
        lock (_gate) { return _subscribedPeerIds.Add(peerId); }
    }

    /// <summary>Remove a peer's subscription (peer disconnect / attestation revocation).</summary>
    public bool RemoveSubscriber(string peerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(peerId);
        lock (_gate) { return _subscribedPeerIds.Remove(peerId); }
    }
}

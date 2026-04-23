namespace Sunfish.Kernel.SchemaRegistry.Compaction;

/// <summary>
/// Registry of upcasters that have been retired after a stream-compaction run.
/// Paper §7.2: <i>"Old upcasters are retired after compaction; the old stream is archived
/// for deep audit."</i>
/// </summary>
/// <remarks>
/// <para>
/// Retirement is <i>logical</i>, not physical. Upcasters remain registered with
/// <see cref="Upcasters.UpcasterChain"/>; retirement only marks them as skipped on the hot
/// read path. This preserves the full history of which upcasters ever existed — useful
/// when the archived pre-compaction stream is re-read for "deep audit" and the old
/// transform semantics need to be reconstructed.
/// </para>
/// </remarks>
public interface IUpcasterRetirement
{
    /// <summary>Mark an upcaster as retired. Retired upcasters are ignored by <see cref="Upcasters.UpcasterChain.ApplyChain"/>.</summary>
    void Retire(string eventType, string fromVersion, string toVersion);

    /// <summary>Is the upcaster described by <paramref name="eventType"/>/<paramref name="fromVersion"/>/<paramref name="toVersion"/> retired?</summary>
    bool IsRetired(string eventType, string fromVersion, string toVersion);

    /// <summary>All retired upcasters, in the order they were retired.</summary>
    IReadOnlyList<RetiredUpcaster> Retirements { get; }
}

/// <summary>A retirement record.</summary>
/// <param name="EventType">Event type the retired upcaster applied to.</param>
/// <param name="FromVersion">Source schema version for the retired edge.</param>
/// <param name="ToVersion">Target schema version for the retired edge.</param>
/// <param name="RetiredAt">Wall-clock timestamp at which the retirement was recorded.</param>
public sealed record RetiredUpcaster(
    string EventType,
    string FromVersion,
    string ToVersion,
    DateTimeOffset RetiredAt);

namespace Sunfish.Kernel.Buckets;

/// <summary>
/// Replication strategy for a bucket (paper §10.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Eager</b> — every eligible record is replicated in full to every eligible peer as soon as
/// gossip delivers it. Used for "hot" record types that the UI needs immediately.
/// </para>
/// <para>
/// <b>Lazy</b> — eligible records are represented locally as <see cref="LazyFetch.BucketStub"/>
/// instances. Full content is fetched on demand (paper §10.3). Lazy buckets are the only ones
/// subject to LRU eviction under storage-budget pressure.
/// </para>
/// </remarks>
public enum ReplicationMode
{
    /// <summary>Full-content replication at gossip time.</summary>
    Eager,

    /// <summary>Stub-only replication; content fetched on demand.</summary>
    Lazy,
}

/// <summary>
/// Declarative description of a sync bucket (paper §10.2). Parsed from YAML via
/// <see cref="IBucketYamlLoader"/> and evaluated at capability negotiation by
/// <see cref="IBucketRegistry.EligibleBucketsFor"/>.
/// </summary>
/// <param name="Name">Human-readable bucket identifier (unique within a registry). Example: <c>team_core</c>.</param>
/// <param name="RecordTypes">Record types carried by this bucket. Example: <c>["projects", "tasks"]</c>.</param>
/// <param name="Filter">
/// Optional filter expression evaluated per-record by <see cref="IBucketFilterEvaluator"/>.
/// Null means "no filter — all records of <paramref name="RecordTypes"/> match".
/// The minimal grammar supported by <see cref="SimpleBucketFilterEvaluator"/> is
/// <c>&lt;field&gt;(.&lt;prop&gt;)? (=|!=) &lt;value&gt; (AND &lt;expr&gt;)*</c>.
/// </param>
/// <param name="Replication"><see cref="ReplicationMode.Eager"/> or <see cref="ReplicationMode.Lazy"/>.</param>
/// <param name="RequiredAttestation">
/// Role token (e.g. <c>team_member</c>, <c>financial_role</c>) that a peer must present to be
/// eligible for this bucket. Matches <see cref="Sunfish.Kernel.Security.Attestation.RoleAttestation.Role"/>.
/// </param>
/// <param name="MaxLocalAgeDays">
/// For <see cref="ReplicationMode.Lazy"/> buckets only: maximum age before full content becomes
/// eligible for eviction. Null means "never evict by age" (still evictable under storage pressure).
/// </param>
public sealed record BucketDefinition(
    string Name,
    IReadOnlyList<string> RecordTypes,
    string? Filter,
    ReplicationMode Replication,
    string RequiredAttestation,
    int? MaxLocalAgeDays);

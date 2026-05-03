using Sunfish.Federation.EntitySync.Protocol;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.EntitySync;

/// <summary>
/// Local per-peer store of signed change records. Federation's <see cref="IEntitySyncer"/> consults
/// this store to advertise heads, walk reachability, and ingest received changes. Consumers outside
/// the federation layer populate the store by whatever mechanism they use to produce changes (typically
/// a kernel or domain-service that signs and appends).
/// </summary>
public interface IChangeStore
{
    /// <summary>Returns <c>true</c> iff a change with the given <paramref name="version"/> is stored.</summary>
    bool Contains(VersionId version);

    /// <summary>Returns the stored change for <paramref name="version"/>, or <c>null</c> if absent.</summary>
    SignedOperation<ChangeRecord>? TryGet(VersionId version);

    /// <summary>Upserts <paramref name="change"/> into the store keyed by its <see cref="ChangeRecord.VersionId"/>.</summary>
    void Put(SignedOperation<ChangeRecord> change);

    /// <summary>
    /// Returns the current head versions within an optional <paramref name="scope"/>. A head is any
    /// <see cref="VersionId"/> that appears as a <see cref="ChangeRecord.VersionId"/> but is never a
    /// <see cref="ChangeRecord.ParentVersionId"/>. When <paramref name="scope"/> is <c>null</c>, all
    /// entities are considered.
    /// </summary>
    IReadOnlyList<VersionId> GetHeads(EntityId? scope);

    /// <summary>
    /// Returns the set of stored changes reachable backward from <paramref name="heads"/> by walking
    /// <see cref="ChangeRecord.ParentVersionId"/> links. Walking stops at any version in
    /// <paramref name="stopAt"/> (which is typically the other peer's heads — everything at or below
    /// those heads is already on that peer).
    /// </summary>
    IReadOnlyList<SignedOperation<ChangeRecord>> GetReachableFrom(
        IReadOnlyList<VersionId> heads,
        IReadOnlyCollection<VersionId> stopAt);
}

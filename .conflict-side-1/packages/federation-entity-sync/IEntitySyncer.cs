using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Federation.EntitySync;

/// <summary>
/// Drives Automerge-style head-announcement / change-exchange sync between the local peer and a
/// remote peer. Implementations wrap an <see cref="ISyncTransport"/> and a local <see cref="IChangeStore"/>.
/// </summary>
public interface IEntitySyncer
{
    /// <summary>
    /// Pulls missing changes from <paramref name="peer"/> into the local store. Sends the local heads
    /// to the peer; the peer responds with the minimal set of changes needed to catch the local store
    /// up to the peer's heads. Received changes are signature-verified before being applied.
    /// </summary>
    ValueTask<SyncResult> PullFromAsync(PeerDescriptor peer, EntityId? scope, CancellationToken ct = default);

    /// <summary>
    /// Pushes new local changes to <paramref name="peer"/>. First requests the peer's heads (via a
    /// <see cref="SyncMessageKind.EntityHeadsAnnouncement"/> round-trip), then ships the minimal set
    /// of local changes reachable from our heads but not already on the peer.
    /// </summary>
    ValueTask<SyncResult> PushToAsync(PeerDescriptor peer, EntityId? scope, CancellationToken ct = default);
}

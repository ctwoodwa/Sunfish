using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Federation.EntitySync.Protocol;

/// <summary>
/// Payload for <see cref="Sunfish.Federation.Common.SyncMessageKind.EntityChangesRequest"/>. The
/// requester sends its own heads so the responder can compute the minimal set of missing changes.
/// </summary>
/// <param name="Scope">Optional entity scope; <c>null</c> means all entities.</param>
/// <param name="LocalHeads">The requester's current head versions — used as the stop-set for the
///   responder's reachability walk.</param>
/// <param name="WantedHeads">Optional explicit set of heads the requester wants (may be empty when
///   the requester relies on the responder to enumerate its own heads).</param>
public sealed record ChangesRequest(
    EntityId? Scope,
    IReadOnlyList<VersionId> LocalHeads,
    IReadOnlyList<VersionId> WantedHeads);

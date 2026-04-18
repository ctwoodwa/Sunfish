using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Federation.EntitySync.Protocol;

/// <summary>
/// Payload for <see cref="Sunfish.Federation.Common.SyncMessageKind.EntityHeadsAnnouncement"/>. The
/// sender advertises its current set of heads for one entity (<paramref name="Scope"/>) or all
/// entities (<c>Scope == null</c>). Receivers compute the delta they need to request or push.
/// </summary>
/// <param name="Scope">The entity scope, or <c>null</c> meaning "all entities known locally".</param>
/// <param name="LocalHeads">The sender's current head versions within the scope.</param>
public sealed record HeadsAnnouncement(
    EntityId? Scope,
    IReadOnlyList<VersionId> LocalHeads);

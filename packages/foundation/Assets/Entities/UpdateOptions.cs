using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Entities;

/// <summary>
/// Options controlling entity updates.
/// </summary>
/// <param name="Actor">The actor performing the update.</param>
/// <param name="ExpectedVersion">
/// Optimistic concurrency guard. When non-null, the update proceeds only if the entity's
/// current sequence equals <paramref name="ExpectedVersion"/>.<see cref="VersionId.Sequence"/>.
/// A mismatch raises <see cref="ConcurrencyException"/>.
/// </param>
/// <param name="ValidFrom">
/// When the new version becomes valid. Defaults to <see cref="DateTimeOffset.UtcNow"/>.
/// The previous version's <c>ValidTo</c> is set to this same instant to keep the history
/// contiguous.
/// </param>
/// <param name="Justification">
/// Optional human-readable reason for the change (surfaces in audit).
/// </param>
public sealed record UpdateOptions(
    ActorId Actor,
    VersionId? ExpectedVersion = null,
    DateTimeOffset? ValidFrom = null,
    string? Justification = null);

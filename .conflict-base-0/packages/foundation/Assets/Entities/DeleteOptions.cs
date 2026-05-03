using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Entities;

/// <summary>Options controlling entity deletion.</summary>
/// <param name="Actor">The actor performing the delete.</param>
/// <param name="ValidFrom">
/// When the tombstone becomes effective. Defaults to <see cref="DateTimeOffset.UtcNow"/>.
/// </param>
/// <param name="Justification">Optional human-readable reason for deletion.</param>
public sealed record DeleteOptions(
    ActorId Actor,
    DateTimeOffset? ValidFrom = null,
    string? Justification = null);

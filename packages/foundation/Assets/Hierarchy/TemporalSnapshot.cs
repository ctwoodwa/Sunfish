using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Hierarchy;

/// <summary>A projection of a subtree rooted at <see cref="Root"/> as of a given instant.</summary>
public sealed record TemporalSnapshot(
    EntityId Root,
    DateTimeOffset AsOf,
    IReadOnlyList<ClosureEntry> Closure);

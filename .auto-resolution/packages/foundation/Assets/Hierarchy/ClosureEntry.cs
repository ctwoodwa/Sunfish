using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Temporal;

namespace Sunfish.Foundation.Assets.Hierarchy;

/// <summary>
/// A single materialized entry in the hierarchy closure table.
/// </summary>
/// <remarks>
/// Plan D-HIERARCHY. <c>Depth = 0</c> denotes the self-entry; <c>Depth = 1</c> denotes a
/// direct parent relationship, etc.
/// </remarks>
public sealed record ClosureEntry(
    EntityId Ancestor,
    EntityId Descendant,
    int Depth,
    TemporalRange Validity);

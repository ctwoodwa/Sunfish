namespace Sunfish.Foundation.Assets.Hierarchy;

/// <summary>
/// Logical kinds of edges in the Sunfish asset graph.
/// </summary>
/// <remarks>
/// Spec §5.6. Phase A enumerates the three kinds used by hierarchy evolution; additional
/// vertical-specific kinds may be promoted later (see parking-lot item 12).
/// </remarks>
public enum EdgeKind
{
    /// <summary>Parent-child containment (e.g. building contains units).</summary>
    ChildOf = 0,
    /// <summary>Weak reference between peers.</summary>
    References = 1,
    /// <summary>Supersession (this entity was replaced by another).</summary>
    SupersededBy = 2,
}

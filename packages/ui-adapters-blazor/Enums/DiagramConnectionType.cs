namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// The visual routing style applied to a diagram connection (edge).
/// </summary>
public enum DiagramConnectionType
{
    /// <summary>A straight line between the centers of the source and target nodes.</summary>
    Straight,

    /// <summary>A smooth cubic Bezier curve between the source and target nodes.</summary>
    Bezier,

    /// <summary>An orthogonal (right-angle) polyline between the source and target nodes.</summary>
    Orthogonal
}

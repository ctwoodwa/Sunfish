namespace Sunfish.Core.Enums;

/// <summary>
/// Specifies which edges and corners of a resizable container have resize handles.
/// This is a flags enum allowing combinations of edges.
/// </summary>
[Flags]
public enum SunfishResizeEdges
{
    /// <summary>No resize handles.</summary>
    None = 0,

    /// <summary>Right edge only.</summary>
    Right = 1,

    /// <summary>Bottom edge only.</summary>
    Bottom = 2,

    /// <summary>Left edge only.</summary>
    Left = 4,

    /// <summary>Top edge only.</summary>
    Top = 8,

    /// <summary>Bottom-right corner (default).</summary>
    BottomRight = Bottom | Right,

    /// <summary>Top-left corner.</summary>
    TopLeft = Top | Left,

    /// <summary>Top-right corner.</summary>
    TopRight = Top | Right,

    /// <summary>Bottom-left corner.</summary>
    BottomLeft = Bottom | Left,

    /// <summary>All edges and corners.</summary>
    All = Top | Bottom | Left | Right
}

/// <summary>
/// Specifies the axis along which a resize operation occurs.
/// </summary>
public enum SunfishResizeAxis
{
    /// <summary>Horizontal resize only.</summary>
    Horizontal,

    /// <summary>Vertical resize only.</summary>
    Vertical,

    /// <summary>Both horizontal and vertical.</summary>
    Both
}

using System.ComponentModel;

namespace Sunfish.Foundation.Models;

/// <summary>
/// Describes the geometric type of a diagram shape.
/// </summary>
public enum DiagramShapeType
{
    /// <summary>A rectangle with optional rounded corners.</summary>
    Rectangle,

    /// <summary>An axis-aligned ellipse.</summary>
    Ellipse,

    /// <summary>A diamond (rotated square).</summary>
    Diamond,

    /// <summary>An equilateral triangle pointing upward.</summary>
    Triangle,

    /// <summary>A circle (equal width and height ellipse).</summary>
    Circle
}

/// <summary>
/// Describes a shape (vertex) in a SunfishDiagram.
/// </summary>
public class DiagramShapeDescriptor
{
    /// <summary>Unique identifier for the shape.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Text displayed inside the shape.</summary>
    public string? Text { get; set; }

    /// <summary>Shape type that determines the SVG element rendered.</summary>
    public DiagramShapeType Type { get; set; } = DiagramShapeType.Rectangle;

    /// <summary>X position (pixels) within the diagram canvas.</summary>
    public double X { get; set; }

    /// <summary>Y position (pixels) within the diagram canvas.</summary>
    public double Y { get; set; }

    /// <summary>Width of the shape in pixels.</summary>
    public double Width { get; set; } = 100;

    /// <summary>Height of the shape in pixels.</summary>
    public double Height { get; set; } = 60;

    /// <summary>Optional CSS class applied to the shape SVG element.</summary>
    public string? CssClass { get; set; }

    /// <summary>Optional tooltip text displayed on hover (rendered as SVG &lt;title&gt;).</summary>
    public string? TooltipText { get; set; }
}

/// <summary>
/// Describes a connection (edge) between two shapes in a SunfishDiagram.
/// </summary>
public class DiagramConnectionDescriptor
{
    /// <summary>Unique identifier for the connection.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Id of the source shape.</summary>
    public string FromShapeId { get; set; } = string.Empty;

    /// <summary>Id of the target shape.</summary>
    public string ToShapeId { get; set; } = string.Empty;

    /// <summary>Optional label displayed along the connection.</summary>
    public string? Text { get; set; }

    /// <summary>Optional CSS class applied to the connection SVG element.</summary>
    public string? CssClass { get; set; }
}

/// <summary>
/// Event arguments for the SunfishDiagram.OnShapeClick event.
/// </summary>
public class DiagramShapeClickEventArgs : EventArgs
{
    /// <summary>The shape that was clicked.</summary>
    public DiagramShapeDescriptor Shape { get; set; } = default!;
}

/// <summary>
/// Event arguments for the SunfishDiagram.OnConnectionClick event.
/// </summary>
public class DiagramConnectionClickEventArgs : EventArgs
{
    /// <summary>The connection that was clicked.</summary>
    public DiagramConnectionDescriptor Connection { get; set; } = default!;
}

/// <summary>
/// Event arguments for the SunfishDiagram.OnSelectionChanged event.
/// </summary>
public class DiagramSelectionChangedEventArgs : EventArgs
{
    /// <summary>The current set of selected shape IDs after the change.</summary>
    public IReadOnlyList<string> SelectedShapeIds { get; set; } = [];
}

// ── Legacy aliases (kept for compilation compatibility) ──────────────

/// <summary>
/// Legacy alias for <see cref="DiagramShapeDescriptor"/>. Use <see cref="DiagramShapeDescriptor"/> instead.
/// </summary>
[Obsolete("Use DiagramShapeDescriptor instead.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DiagramNode
{
    /// <summary>Unique identifier for the node.</summary>
    public string Id { get; set; } = "";

    /// <summary>Text displayed inside the node shape.</summary>
    public string? Text { get; set; }

    /// <summary>X position (pixels) within the diagram canvas.</summary>
    public double X { get; set; }

    /// <summary>Y position (pixels) within the diagram canvas.</summary>
    public double Y { get; set; }

    /// <summary>Width of the node shape in pixels.</summary>
    public double Width { get; set; } = 100;

    /// <summary>Height of the node shape in pixels.</summary>
    public double Height { get; set; } = 60;

    /// <summary>Shape type (e.g., "rectangle", "ellipse").</summary>
    public string? Shape { get; set; } = "rectangle";
}

/// <summary>
/// Legacy alias for <see cref="DiagramConnectionDescriptor"/>. Use <see cref="DiagramConnectionDescriptor"/> instead.
/// </summary>
[Obsolete("Use DiagramConnectionDescriptor instead.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DiagramEdge
{
    /// <summary>Unique identifier for the edge.</summary>
    public string Id { get; set; } = "";

    /// <summary>Id of the source node.</summary>
    public string FromId { get; set; } = "";

    /// <summary>Id of the target node.</summary>
    public string ToId { get; set; } = "";

    /// <summary>Optional label displayed along the edge.</summary>
    public string? Text { get; set; }
}

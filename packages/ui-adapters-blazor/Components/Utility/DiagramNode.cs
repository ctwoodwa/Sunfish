using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Utility;

/// <summary>
/// Model describing a node (shape) rendered inside a <see cref="SunfishDiagram"/>.
/// </summary>
/// <remarks>
/// This is a mutable POCO so that consumers can feed live collections into the diagram
/// and mutate <see cref="X"/>/<see cref="Y"/> in response to drag events. The diagram itself
/// mutates <see cref="X"/> and <see cref="Y"/> during drag unless the consumer cancels the
/// <see cref="SunfishDiagram.OnNodeDragEnd"/> callback.
/// </remarks>
public sealed class DiagramNode
{
    /// <summary>Stable identifier used by connections to reference this node.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>X position of the top-left corner of the node's bounding box (SVG units).</summary>
    public double X { get; set; }

    /// <summary>Y position of the top-left corner of the node's bounding box (SVG units).</summary>
    public double Y { get; set; }

    /// <summary>Width of the node's bounding box (SVG units).</summary>
    public double Width { get; set; } = 120;

    /// <summary>Height of the node's bounding box (SVG units).</summary>
    public double Height { get; set; } = 48;

    /// <summary>The geometric shape rendered for this node.</summary>
    public DiagramNodeShape Shape { get; set; } = DiagramNodeShape.Rectangle;

    /// <summary>Optional text label rendered at the node's center.</summary>
    public string? Label { get; set; }

    /// <summary>Optional CSS color used for the node's fill (any valid CSS color).</summary>
    public string? Fill { get; set; }

    /// <summary>Optional CSS color used for the node's stroke (any valid CSS color).</summary>
    public string? Stroke { get; set; }

    /// <summary>Arbitrary consumer-supplied payload associated with this node.</summary>
    public object? Data { get; set; }
}

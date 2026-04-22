namespace Sunfish.UIAdapters.Blazor.Components.Utility;

/// <summary>
/// Cancellable event payload for <see cref="SunfishDiagram.OnNodeDragEnd"/>.
/// Consumers set <see cref="Cancel"/> to <c>true</c> to veto the position change.
/// </summary>
public sealed class DiagramNodeDragEndEventArgs
{
    /// <summary>The <see cref="DiagramNode.Id"/> of the node that was dragged.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The proposed new X position (SVG units) of the node's top-left corner.</summary>
    public double NewX { get; init; }

    /// <summary>The proposed new Y position (SVG units) of the node's top-left corner.</summary>
    public double NewY { get; init; }

    /// <summary>When set to <c>true</c> by a handler, the drag is cancelled and the node reverts.</summary>
    public bool Cancel { get; set; }
}

using System.Globalization;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.UIAdapters.Blazor.Components.Utility;

/// <summary>
/// A framework-agnostic SVG node/edge diagram with draggable nodes and curved (or straight /
/// orthogonal) connections. Implements the MVP surface defined for Tier 3 Wave 3 (mapping status
/// "partial") — consumers provide <see cref="Nodes"/> and <see cref="Connections"/> collections
/// and react to <see cref="OnNodeClick"/>, <see cref="OnConnectionClick"/>, and
/// <see cref="OnNodeDragEnd"/> events.
/// </summary>
public partial class SunfishDiagram : SunfishComponentBase
{
    // ── Fields ─────────────────────────────────────────────────────────────

    private ElementReference _rootRef;
    private DiagramNode? _dragNode;
    private double _dragStartPointerX;
    private double _dragStartPointerY;
    private double _dragStartNodeX;
    private double _dragStartNodeY;

    /// <summary>Invariant culture used for SVG numeric attributes so decimal separators never become commas.</summary>
    private static readonly IFormatProvider CultureInvariant = CultureInfo.InvariantCulture;

    // ── Parameters ─────────────────────────────────────────────────────────

    /// <summary>The collection of nodes to render.</summary>
    [Parameter] public IList<DiagramNode>? Nodes { get; set; }

    /// <summary>The collection of connections (edges) to render between nodes.</summary>
    [Parameter] public IList<DiagramConnection>? Connections { get; set; }

    /// <summary>CSS width of the diagram surface (any valid CSS length). Default <c>"100%"</c>.</summary>
    [Parameter] public string Width { get; set; } = "100%";

    /// <summary>CSS height of the diagram surface (any valid CSS length). Default <c>"480px"</c>.</summary>
    [Parameter] public string Height { get; set; } = "480px";

    /// <summary>Fires when the user clicks a node.</summary>
    [Parameter] public EventCallback<DiagramNode> OnNodeClick { get; set; }

    /// <summary>Fires when the user clicks a connection (edge).</summary>
    [Parameter] public EventCallback<DiagramConnection> OnConnectionClick { get; set; }

    /// <summary>
    /// Fires when a node drag completes. Handlers may set <see cref="DiagramNodeDragEndEventArgs.Cancel"/>
    /// to <c>true</c> to revert the node to its pre-drag position.
    /// </summary>
    [Parameter] public EventCallback<DiagramNodeDragEndEventArgs> OnNodeDragEnd { get; set; }

    // ── Event handlers ─────────────────────────────────────────────────────

    private Task HandleNodeClick(DiagramNode node) => OnNodeClick.InvokeAsync(node);

    private Task HandleConnectionClick(DiagramConnection connection) => OnConnectionClick.InvokeAsync(connection);

    private void HandlePointerDown(PointerEventArgs e, DiagramNode node)
    {
        _dragNode = node;
        _dragStartPointerX = e.ClientX;
        _dragStartPointerY = e.ClientY;
        _dragStartNodeX = node.X;
        _dragStartNodeY = node.Y;
    }

    private void HandlePointerMove(PointerEventArgs e)
    {
        if (_dragNode is null) return;

        var dx = e.ClientX - _dragStartPointerX;
        var dy = e.ClientY - _dragStartPointerY;
        _dragNode.X = _dragStartNodeX + dx;
        _dragNode.Y = _dragStartNodeY + dy;
        StateHasChanged();
    }

    private async Task HandlePointerUp(PointerEventArgs e)
    {
        if (_dragNode is null) return;

        var node = _dragNode;
        _dragNode = null;

        // Only raise the drag-end event if the node actually moved (avoid conflating clicks with drags).
        if (Math.Abs(node.X - _dragStartNodeX) < 0.5 && Math.Abs(node.Y - _dragStartNodeY) < 0.5)
        {
            return;
        }

        var args = new DiagramNodeDragEndEventArgs
        {
            Id = node.Id,
            NewX = node.X,
            NewY = node.Y
        };

        await OnNodeDragEnd.InvokeAsync(args);

        if (args.Cancel)
        {
            node.X = _dragStartNodeX;
            node.Y = _dragStartNodeY;
            StateHasChanged();
        }
    }

    // ── Geometry helpers ───────────────────────────────────────────────────

    private string BuildDiamondPoints(DiagramNode node)
    {
        var cx = node.X + node.Width / 2d;
        var cy = node.Y + node.Height / 2d;
        var halfW = node.Width / 2d;
        var halfH = node.Height / 2d;

        return string.Format(CultureInvariant,
            "{0},{1} {2},{3} {4},{5} {6},{7}",
            cx, node.Y,                // top
            node.X + node.Width, cy,   // right
            cx, node.Y + node.Height,  // bottom
            node.X, cy);               // left
    }

    private sealed record ConnectionPath(string Data, double LabelX, double LabelY);

    private ConnectionPath? BuildConnectionPath(DiagramConnection conn)
    {
        if (Nodes is null) return null;

        DiagramNode? source = null;
        DiagramNode? target = null;
        foreach (var n in Nodes)
        {
            if (n.Id == conn.SourceNodeId) source = n;
            if (n.Id == conn.TargetNodeId) target = n;
        }

        if (source is null || target is null) return null;

        var sx = source.X + source.Width / 2d;
        var sy = source.Y + source.Height / 2d;
        var tx = target.X + target.Width / 2d;
        var ty = target.Y + target.Height / 2d;
        var labelX = (sx + tx) / 2d;
        var labelY = (sy + ty) / 2d;

        string data = conn.Type switch
        {
            DiagramConnectionType.Straight =>
                string.Format(CultureInvariant, "M {0} {1} L {2} {3}", sx, sy, tx, ty),

            DiagramConnectionType.Orthogonal =>
                // Horizontal-then-vertical polyline. Midpoint is on the elbow, matching the Bezier label anchor visually.
                string.Format(CultureInvariant, "M {0} {1} L {2} {1} L {2} {3}", sx, sy, tx, ty),

            // Default: cubic Bezier whose control points are offset horizontally toward the midpoint.
            _ => BuildBezierPath(sx, sy, tx, ty)
        };

        return new ConnectionPath(data, labelX, labelY);
    }

    private static string BuildBezierPath(double sx, double sy, double tx, double ty)
    {
        // Control-point offset: half the horizontal distance, so the curve bulges smoothly even when nodes are stacked vertically.
        var dx = Math.Abs(tx - sx) / 2d;
        var minCtrlOffset = 40d;
        var ctrlOffset = Math.Max(dx, minCtrlOffset);

        var c1x = sx + ctrlOffset;
        var c1y = sy;
        var c2x = tx - ctrlOffset;
        var c2y = ty;

        return string.Format(CultureInvariant,
            "M {0} {1} C {2} {3}, {4} {5}, {6} {7}",
            sx, sy, c1x, c1y, c2x, c2y, tx, ty);
    }
}

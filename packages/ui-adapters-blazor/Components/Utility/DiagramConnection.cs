using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Utility;

/// <summary>
/// Model describing a connection (edge) rendered inside a <see cref="SunfishDiagram"/>.
/// </summary>
public sealed class DiagramConnection
{
    /// <summary>Stable identifier for this connection.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The <see cref="DiagramNode.Id"/> of the source node.</summary>
    public string SourceNodeId { get; set; } = string.Empty;

    /// <summary>The <see cref="DiagramNode.Id"/> of the target node.</summary>
    public string TargetNodeId { get; set; } = string.Empty;

    /// <summary>The visual routing style applied to this connection.</summary>
    public DiagramConnectionType Type { get; set; } = DiagramConnectionType.Bezier;

    /// <summary>Optional label rendered near the connection midpoint.</summary>
    public string? Label { get; set; }

    /// <summary>Optional CSS color used for the connection stroke (any valid CSS color).</summary>
    public string? Color { get; set; }
}

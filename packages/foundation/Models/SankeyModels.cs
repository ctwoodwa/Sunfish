namespace Sunfish.Foundation.Models;

/// <summary>
/// A node in a Sankey diagram.
/// </summary>
public class SankeyNode
{
    /// <summary>Unique identifier for the node.</summary>
    public string Id { get; set; } = "";

    /// <summary>Display label for the node.</summary>
    public string? Label { get; set; }

    /// <summary>Optional fill color for the node (CSS color value).</summary>
    public string? Color { get; set; }
}

/// <summary>
/// A directed, weighted link between two <see cref="SankeyNode"/> items.
/// </summary>
public class SankeyLink
{
    /// <summary>Id of the source node.</summary>
    public string SourceId { get; set; } = "";

    /// <summary>Id of the target node.</summary>
    public string TargetId { get; set; } = "";

    /// <summary>Numeric value representing the flow quantity.</summary>
    public double Value { get; set; }

    /// <summary>Optional color for the link path (CSS color value).</summary>
    public string? Color { get; set; }
}

/// <summary>
/// Complete data model for a <c>SunfishSankey</c> diagram.
/// </summary>
public class SankeyData
{
    /// <summary>The nodes in the diagram.</summary>
    public List<SankeyNode> Nodes { get; set; } = new();

    /// <summary>The links connecting nodes.</summary>
    public List<SankeyLink> Links { get; set; } = new();
}

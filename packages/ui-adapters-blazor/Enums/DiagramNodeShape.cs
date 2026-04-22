namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// The geometric shape rendered for a <see cref="Sunfish.UIAdapters.Blazor.Components.Utility.SunfishDiagram"/> node.
/// </summary>
public enum DiagramNodeShape
{
    /// <summary>A plain rectangle.</summary>
    Rectangle,

    /// <summary>An ellipse inscribed in the node's bounding box.</summary>
    Ellipse,

    /// <summary>A diamond (rhombus) inscribed in the node's bounding box.</summary>
    Diamond,

    /// <summary>A rectangle with rounded corners.</summary>
    Rounded
}

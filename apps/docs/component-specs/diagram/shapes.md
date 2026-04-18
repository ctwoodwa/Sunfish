---
title: Shapes
page_title: Diagram - Shapes
description: Learn about the Blazor Diagram shape types and their properties.
slug: diagram-shapes
tags: sunfish,blazor,diagram
published: True
position: 20
components: ["diagram"]
---
# Blazor Diagram Shapes

Shapes are the primary visual elements (vertices) in the SunfishDiagram. Each shape is defined by a `DiagramShapeDescriptor` instance and rendered as an SVG element.

## Shape Types

The `DiagramShapeType` enum defines five built-in shape types:

| Type | SVG Element | Description |
|------|-------------|-------------|
| `Rectangle` (default) | `<rect>` | A rectangle with rounded corners (`rx="4"`) |
| `Ellipse` | `<ellipse>` | An axis-aligned ellipse |
| `Diamond` | `<polygon>` | A rotated square (diamond) |
| `Triangle` | `<polygon>` | An equilateral triangle pointing upward |
| `Circle` | `<circle>` | A circle using the smaller of Width/Height as diameter |

## DiagramShapeDescriptor Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `""` | Unique identifier for the shape |
| `Text` | `string?` | `null` | Text displayed inside the shape |
| `Type` | `DiagramShapeType` | `Rectangle` | Shape type that determines the SVG element |
| `X` | `double` | `0` | X position (pixels) within the diagram canvas |
| `Y` | `double` | `0` | Y position (pixels) within the diagram canvas |
| `Width` | `double` | `100` | Width of the shape in pixels |
| `Height` | `double` | `60` | Height of the shape in pixels |
| `CssClass` | `string?` | `null` | Optional CSS class applied to the SVG element |
| `TooltipText` | `string?` | `null` | Optional tooltip text displayed on hover (rendered as SVG `<title>`) |

## Example

>caption Using different shape types

````RAZOR
<SunfishDiagram Shapes="_shapes" Height="300px">
</SunfishDiagram>

@code {
    private List<DiagramShapeDescriptor> _shapes = new()
    {
        new() { Id = "rect", Text = "Rectangle", Type = DiagramShapeType.Rectangle, X = 20, Y = 100 },
        new() { Id = "ellipse", Text = "Ellipse", Type = DiagramShapeType.Ellipse, X = 160, Y = 90, Width = 110, Height = 60 },
        new() { Id = "diamond", Text = "Diamond", Type = DiagramShapeType.Diamond, X = 310, Y = 80, Width = 90, Height = 80 },
        new() { Id = "triangle", Text = "Triangle", Type = DiagramShapeType.Triangle, X = 440, Y = 80, Width = 100, Height = 80 },
        new() { Id = "circle", Text = "Circle", Type = DiagramShapeType.Circle, X = 580, Y = 90, Width = 60, Height = 60 },
    };
}
````

## CSS Classes

Each shape element receives the BEM class `mar-diagram__shape`. When a `CssClass` is set on the descriptor, it is appended to the element's class list. Shape text labels receive `mar-diagram__shape-label`.

## Deferred Features

The following shape features are deferred to future versions:

* Declarative `<DiagramShape>` child tags
* Custom shape templates (`RenderFragment`)
* Shape connectors (the 5 dots on boundaries for drag-to-connect)
* Shape images and icons
* Shape fill and stroke configuration via descriptor properties
* Shape rotation

## See Also

* [Diagram Overview](slug:diagram-overview)
* [Diagram Connections](slug:diagram-connections)
* [Diagram Events](slug:diagram-events)

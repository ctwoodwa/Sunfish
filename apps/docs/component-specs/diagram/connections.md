---
title: Connections
page_title: Diagram - Connections
description: Learn about the Blazor Diagram connections and how to link shapes.
slug: diagram-connections
tags: sunfish,blazor,diagram
published: True
position: 30
components: ["diagram"]
---
# Blazor Diagram Connections

Connections represent the relationships (edges) between shapes in the SunfishDiagram. Each connection is defined by a `DiagramConnectionDescriptor` and rendered as an SVG line between the centers of the linked shapes.

## Basics

A connection links two shapes by referencing their `Id` values through `FromShapeId` and `ToShapeId`. Connections render as SVG `<line>` elements with an arrowhead marker at the target end.

## DiagramConnectionDescriptor Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `""` | Unique identifier for the connection |
| `FromShapeId` | `string` | `""` | Id of the source shape |
| `ToShapeId` | `string` | `""` | Id of the target shape |
| `Text` | `string?` | `null` | Optional label displayed at the midpoint of the connection |
| `CssClass` | `string?` | `null` | Optional CSS class applied to the connection SVG element |

## Example

>caption Basic Diagram connections

````RAZOR
<SunfishDiagram Shapes="_shapes"
               Connections="_connections"
               Height="300px" />

@code {
    private List<DiagramShapeDescriptor> _shapes = new()
    {
        new() { Id = "a", Text = "A", X = 50, Y = 100, Width = 80, Height = 50 },
        new() { Id = "b", Text = "B", X = 220, Y = 50, Width = 80, Height = 50 },
        new() { Id = "c", Text = "C", X = 220, Y = 160, Width = 80, Height = 50 },
    };

    private List<DiagramConnectionDescriptor> _connections = new()
    {
        new() { Id = "ab", FromShapeId = "a", ToShapeId = "b", Text = "Yes" },
        new() { Id = "ac", FromShapeId = "a", ToShapeId = "c", Text = "No" },
    };
}
````

## CSS Classes

Each connection line receives the BEM class `mar-diagram__connection`. When a `CssClass` is set on the descriptor, it is appended to the element's class list. Connection text labels receive `mar-diagram__connection-label`.

## Deferred Features

The following connection features are deferred to future versions:

* Declarative `<DiagramConnection>` child tags
* Connection types (Cascading, Polyline)
* Connection points (waypoints)
* Cap types (ArrowEnd, FilledCircle, None)
* Connection selection and drag-editing
* Connection tooltips
* Visual functions
* Bi-directional and non-directional connection appearance
* Connection stroke styling via descriptor properties

## See Also

* [Diagram Overview](slug:diagram-overview)
* [Diagram Shapes](slug:diagram-shapes)
* [Diagram Events](slug:diagram-events)

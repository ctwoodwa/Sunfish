---
title: Events
page_title: Diagram - Events
description: Learn about the Blazor Diagram component events and experiment with them in the provided runnable code examples.
slug: diagram-events
tags: sunfish,blazor,diagram
published: True
position: 100
components: ["diagram"]
---
# Blazor Diagram Events

The SunfishDiagram fires events when the user interacts with shapes.

## OnShapeClick

The `OnShapeClick` event fires when the user clicks on a shape. The event argument is of type `DiagramShapeClickEventArgs` and provides the full `DiagramShapeDescriptor` of the clicked shape via the `Shape` property.

>caption Using the Diagram OnShapeClick event

````RAZOR
<SunfishDiagram Shapes="_shapes"
               OnShapeClick="@OnDiagramShapeClick"
               Height="300px" />

@DiagramEventLog

@code {
    private string DiagramEventLog { get; set; } = string.Empty;

    private void OnDiagramShapeClick(DiagramShapeClickEventArgs args)
    {
        DiagramEventLog = $"Clicked shape '{args.Shape.Text}' (ID: {args.Shape.Id}, Type: {args.Shape.Type}).";
    }

    private List<DiagramShapeDescriptor> _shapes = new()
    {
        new() { Id = "s1", Text = "Shape 1", Type = DiagramShapeType.Rectangle, X = 50, Y = 80, Width = 100, Height = 50 },
        new() { Id = "s2", Text = "Shape 2", Type = DiagramShapeType.Ellipse, X = 220, Y = 80, Width = 120, Height = 60 },
        new() { Id = "s3", Text = "Shape 3", Type = DiagramShapeType.Diamond, X = 410, Y = 70, Width = 100, Height = 70 },
    };
}
````

## DiagramShapeClickEventArgs

| Property | Type | Description |
|----------|------|-------------|
| `Shape` | `DiagramShapeDescriptor` | The descriptor of the shape that was clicked |

## Deferred Events

The following events are deferred to future versions:

* `OnConnectionClick` with `DiagramConnectionClickEventArgs`
* `OnShapeDoubleClick`
* `OnShapeDragStart` / `OnShapeDragEnd`
* `OnConnectionCreate`
* `OnSelectionChanged`

## See Also

* [Diagram Overview](slug:diagram-overview)
* [Diagram Shapes](slug:diagram-shapes)
* [Diagram Connections](slug:diagram-connections)

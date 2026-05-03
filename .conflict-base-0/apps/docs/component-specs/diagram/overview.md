---
title: Overview
page_title: Diagram - Overview
description: The Blazor Diagram component
slug: diagram-overview
tags: sunfish,blazor,diagram
published: True
position: 0
components: ["diagram"]
---
# Blazor Diagram Overview

The [Blazor Diagram component](https://sunfish.dev/blazor-ui/diagram) displays relationships between objects or concepts, for example, hierarchy. The Diagram renders shapes and connections as SVG elements, allowing customization of shape types, positions, and styling.

## v1 Authoring Model

The Diagram v1 API uses **flat descriptor lists** for both shapes and connections:

* `Shapes` accepts an `IReadOnlyList<DiagramShapeDescriptor>` that defines the shapes and their properties.
* `Connections` accepts an `IReadOnlyList<DiagramConnectionDescriptor>` that defines the connections between shapes.

> Declarative child tags (e.g. `<DiagramShape>`, `<DiagramConnection>`) are deferred to a future version.

## Diagram Elements

The Diagram component UI consists of the following elements:

* [**Shapes**](slug:diagram-shapes) are the Diagram nodes ([vertices](https://en.wikipedia.org/wiki/Vertex_(graph_theory))). Shapes can display text and are rendered as SVG elements corresponding to their `DiagramShapeType`.
* [**Connections**](slug:diagram-connections) are the links ([edges](https://en.wikipedia.org/wiki/Glossary_of_graph_theory#edge)) between Diagram shapes. Each connection renders as an SVG line between shape centers with an optional arrowhead.

## Creating Blazor Diagram

Define the shapes and connections as lists and pass them to the `SunfishDiagram` component:

>caption Basic Blazor Diagram

````RAZOR
<SunfishDiagram Shapes="_shapes"
               Connections="_connections"
               Height="350px"
               OnShapeClick="HandleShapeClick" />

@code {
    private List<DiagramShapeDescriptor> _shapes = new()
    {
        new() { Id = "s1", Text = "Start", Type = DiagramShapeType.Rectangle, X = 50, Y = 40, Width = 100, Height = 50 },
        new() { Id = "s2", Text = "Process", Type = DiagramShapeType.Rectangle, X = 220, Y = 40, Width = 120, Height = 50 },
        new() { Id = "s3", Text = "Decision", Type = DiagramShapeType.Diamond, X = 410, Y = 30, Width = 100, Height = 70 },
    };

    private List<DiagramConnectionDescriptor> _connections = new()
    {
        new() { Id = "c1", FromShapeId = "s1", ToShapeId = "s2" },
        new() { Id = "c2", FromShapeId = "s2", ToShapeId = "s3", Text = "Next" },
    };

    private void HandleShapeClick(DiagramShapeClickEventArgs args)
    {
        Console.WriteLine($"Clicked: {args.Shape.Text}");
    }
}
````

## Shapes

The shapes are the graph nodes and the main building blocks of the Diagram component. The v1 API supports five shape types via `DiagramShapeType`: Rectangle, Ellipse, Diamond, Triangle, and Circle. See [Shape types and configuration](slug:diagram-shapes).

## Connections

Connections link shapes in the Diagram by referencing their `Id` values through `FromShapeId` and `ToShapeId`. See [Diagram connection features](slug:diagram-connections).

## Events

The Diagram fires `OnShapeClick` with `DiagramShapeClickEventArgs` when the user clicks a shape. See [Diagram events](slug:diagram-events).

## Diagram Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Shapes` | `IReadOnlyList<DiagramShapeDescriptor>?` | `null` | Collection of shape descriptors to render |
| `Connections` | `IReadOnlyList<DiagramConnectionDescriptor>?` | `null` | Collection of connection descriptors to render |
| `Width` | `string?` | `"100%"` | CSS width of the diagram container |
| `Height` | `string?` | `"500px"` | CSS height of the diagram container |
| `AriaLabel` | `string?` | `null` | Accessible label for the diagram container and SVG element |
| `OnShapeClick` | `EventCallback<DiagramShapeClickEventArgs>` | | Fires when a shape is clicked |

## Deferred Features

The following features are explicitly deferred to future versions:

* Declarative child tags (`<DiagramShape>`, `<DiagramConnection>`, etc.)
* Data binding via `ShapesData`/`ConnectionsData` with nested descriptor classes (`Fill`, `Content`, `Stroke`)
* Layout engines (tree, force-directed, layered) via `<DiagramLayout>` child tag
* Drag-and-drop shape repositioning
* Zoom and pan
* Selection (single and multi-select)
* Ports / connectors on shape boundaries
* JSON import/export
* Connection routing (cascading, polyline)
* Visual functions

## Next Steps

* [Configure Diagram shapes](slug:diagram-shapes)
* [Customize Diagram connections](slug:diagram-connections)
* [Handle Diagram events](slug:diagram-events)

## See Also

* [Live Demos: Diagram](https://demos.sunfish.dev/blazor-ui/diagram/overview)

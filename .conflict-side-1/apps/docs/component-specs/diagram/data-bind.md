---
title: Data Binding
page_title: Diagram - Data Binding
description: Learn how to bind the Blazor Diagram to data using descriptor classes for shapes and connections.
slug: diagram-data-binding
tags: sunfish,blazor,diagram,data,binding
published: True
position: 2
components: ["diagram"]
---

# Diagram Data Binding

This article explains how to bind the Diagram component to data using descriptor classes. The v1 API uses flat descriptor lists passed to the `Shapes` and `Connections` parameters.

## Parameters

* `Shapes` accepts an `IReadOnlyList<DiagramShapeDescriptor>` that defines the shapes and their properties.
* `Connections` accepts an `IReadOnlyList<DiagramConnectionDescriptor>` that defines the connections between shapes and their properties.

## Descriptor Classes

* [`DiagramShapeDescriptor`](slug:diagram-shapes) contains properties: `Id`, `Text`, `Type`, `X`, `Y`, `Width`, `Height`, `CssClass`, `TooltipText`.
* [`DiagramConnectionDescriptor`](slug:diagram-connections) contains properties: `Id`, `FromShapeId`, `ToShapeId`, `Text`, `CssClass`.

> Nested descriptor classes for fill, stroke, and content styling (`DiagramShapeFillDescriptor`, `DiagramShapeContentDescriptor`, `DiagramConnectionStrokeDescriptor`, `DiagramConnectionContentDescriptor`) are deferred to a future version alongside declarative child tags.

## Binding Data from Custom Models

You can map data from your existing model classes to the descriptor classes. This approach provides flexibility and allows you to integrate the Diagram with your application data.

>caption Binding the Diagram to data from custom models

````RAZOR
<SunfishDiagram Shapes="@ShapesData"
               Connections="@ConnectionsData"
               Height="400px"
               OnShapeClick="HandleClick" />

@code {
    private List<DiagramShapeDescriptor> ShapesData { get; set; } = new();
    private List<DiagramConnectionDescriptor> ConnectionsData { get; set; } = new();

    protected override void OnInitialized()
    {
        var nodes = GetOrganizationNodes();

        foreach (var node in nodes)
        {
            ShapesData.Add(new DiagramShapeDescriptor()
            {
                Id = node.Id,
                Text = node.Label,
                Width = node.Width,
                Height = node.Height,
                X = node.X,
                Y = node.Y,
                TooltipText = $"{node.Label} ({node.Role})"
            });
        }

        var connections = GetOrganizationConnections();

        foreach (var connection in connections)
        {
            ConnectionsData.Add(new DiagramConnectionDescriptor()
            {
                Id = connection.Id,
                FromShapeId = connection.FromId,
                ToShapeId = connection.ToId,
                Text = connection.Label
            });
        }
    }

    private void HandleClick(DiagramShapeClickEventArgs args)
    {
        Console.WriteLine($"Clicked: {args.Shape.Text}");
    }

    private List<OrganizationNode> GetOrganizationNodes() => new()
    {
        new() { Id = "ceo", Label = "CEO", Role = "Executive", Width = 120, Height = 50, X = 200, Y = 20 },
        new() { Id = "cto", Label = "CTO", Role = "Technology", Width = 120, Height = 50, X = 80, Y = 120 },
        new() { Id = "cfo", Label = "CFO", Role = "Finance", Width = 120, Height = 50, X = 320, Y = 120 },
    };

    private List<OrganizationConnection> GetOrganizationConnections() => new()
    {
        new() { Id = "c1", FromId = "ceo", ToId = "cto", Label = "Supervises" },
        new() { Id = "c2", FromId = "ceo", ToId = "cfo", Label = "Supervises" },
    };

    public class OrganizationNode
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Role { get; set; } = "";
        public double Width { get; set; }
        public double Height { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class OrganizationConnection
    {
        public string Id { get; set; } = "";
        public string FromId { get; set; } = "";
        public string ToId { get; set; } = "";
        public string Label { get; set; } = "";
    }
}
````

## See Also

* [Diagram Overview](slug:diagram-overview)
* [Diagram Shapes](slug:diagram-shapes)
* [Diagram Connections](slug:diagram-connections)

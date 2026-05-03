---
title: Layouts
page_title: Diagram - Layouts
description: Planned layout engines for the Blazor Diagram component (deferred).
slug: diagram-layouts
tags: sunfish,blazor,diagram
published: True
position: 10
components: ["diagram"]
---
# Blazor Diagram Layouts (Deferred)

Layout engines are **deferred to a future version** of the SunfishDiagram. The v1 API requires manual positioning of shapes via the `X` and `Y` properties on `DiagramShapeDescriptor`.

## Planned Layout Types

The following layout algorithms are planned for future implementation:

* **Tree Layout** -- positions shapes in a hierarchical tree structure. Subtypes: Down, Up, Left, Right, MindMapHorizontal, MindMapVertical, Radial, TipOver.
* **Layered Layout** -- positions shapes to minimize connection crossings and emphasize directional flow.
* **Force-directed Layout** -- uses spring-embedder simulation to find a balanced arrangement.

## Planned API Shape

Layouts will be configured via a `<DiagramLayout>` child tag:

```razor
<SunfishDiagram Shapes="@_shapes" Connections="@_connections">
    <DiagramLayout Type="@DiagramLayoutType.Tree"
                   Subtype="@DiagramLayoutSubtype.Down"
                   HorizontalSeparation="90"
                   VerticalSeparation="50" />
</SunfishDiagram>
```

## Current Workaround

Until layout engines are available, set `X` and `Y` on each `DiagramShapeDescriptor` to position shapes manually.

## See Also

* [Diagram Overview](slug:diagram-overview)
* [Diagram Shapes](slug:diagram-shapes)
* [Diagram Connections](slug:diagram-connections)

---
uid: component-tooltip-overview
title: Tooltip
description: The SunfishTooltip component displays a text popup on hover, positioned relative to its child content.
---

# Tooltip

## Overview

The `SunfishTooltip` component wraps any content and displays a text tooltip on hover. The tooltip position is configurable (Top, Bottom, Left, Right) and styling is driven by the active CSS provider.

## Creating a Tooltip

````razor
<SunfishTooltip Text="Save your changes" Position="TooltipPosition.Top">
    <SunfishButton Variant="ButtonVariant.Primary">Save</SunfishButton>
</SunfishTooltip>
````

## Features

- **Positioning** -- Top, Bottom, Left, or Right via `TooltipPosition`. See [Appearance](appearance.md).
- **Simple text** -- The `Text` parameter accepts a plain string displayed in the tooltip popup.
- **Wrap anything** -- Any Razor content placed inside `ChildContent` becomes the hover trigger.
- **Provider-driven styling** -- CSS classes are resolved via `ISunfishCssProvider.TooltipClass(position)`.

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `Text` | `string` | `""` | The text displayed in the tooltip popup. |
| `Position` | `TooltipPosition` | `TooltipPosition.Top` | The position of the tooltip relative to the child content. |
| `ChildContent` | `RenderFragment?` | `null` | The content that triggers the tooltip on hover. |

## See Also

- [API Reference](xref:Sunfish.Components.Blazor.Components.DataDisplay.SunfishTooltip)

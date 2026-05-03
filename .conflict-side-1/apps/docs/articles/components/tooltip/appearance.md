---
uid: component-tooltip-appearance
title: Tooltip Appearance
description: Customize SunfishTooltip position and presentation.
---

# Tooltip Appearance

## Position

The `Position` parameter controls where the tooltip appears relative to its trigger content:

```razor
<SunfishTooltip Text="Top" Position="TooltipPosition.Top">
    <span>Hover me</span>
</SunfishTooltip>

<SunfishTooltip Text="Bottom" Position="TooltipPosition.Bottom">
    <span>Hover me</span>
</SunfishTooltip>

<SunfishTooltip Text="Left" Position="TooltipPosition.Left">
    <span>Hover me</span>
</SunfishTooltip>

<SunfishTooltip Text="Right" Position="TooltipPosition.Right">
    <span>Hover me</span>
</SunfishTooltip>
```

## See Also

- [Tooltip Overview](xref:component-tooltip-overview)

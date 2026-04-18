---
uid: component-icon-appearance
title: Icon Appearance
description: Customize SunfishIcon size, flip direction, and theme color.
---

# Icon Appearance

## Sizes

The `Size` parameter controls the rendered dimensions of the icon:

```razor
<SunfishIcon Name="star" Size="IconSize.Small" />
<SunfishIcon Name="star" Size="IconSize.Medium" />
<SunfishIcon Name="star" Size="IconSize.Large" />
```

## Flip

Mirror the icon along an axis:

```razor
<SunfishIcon Name="arrow-right" Flip="IconFlip.Horizontal" />
<SunfishIcon Name="arrow-down" Flip="IconFlip.Vertical" />
```

## Theme colors

Apply semantic colors to the icon:

```razor
<SunfishIcon Name="check-circle" ThemeColor="IconThemeColor.Success" />
<SunfishIcon Name="alert-circle" ThemeColor="IconThemeColor.Error" />
```

`IconThemeColor.Base` (the default) inherits the current text color from the parent element.

## See Also

- [Icon Overview](xref:component-icon-overview)

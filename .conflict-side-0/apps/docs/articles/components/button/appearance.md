---
uid: component-button-appearance
title: Button Appearance
description: Customize the visual appearance of SunfishButton with variants, sizes, and outline mode.
---

# Button Appearance

## Variants

The `Variant` parameter controls the button's color scheme:

```razor
<SunfishButton Variant="ButtonVariant.Primary">Primary</SunfishButton>
<SunfishButton Variant="ButtonVariant.Secondary">Secondary</SunfishButton>
<SunfishButton Variant="ButtonVariant.Danger">Danger</SunfishButton>
```

## Sizes

The `Size` parameter controls padding and font size:

```razor
<SunfishButton Size="ButtonSize.Small">Small</SunfishButton>
<SunfishButton Size="ButtonSize.Medium">Medium</SunfishButton>
<SunfishButton Size="ButtonSize.Large">Large</SunfishButton>
```

## Outline mode

Set `IsOutline="true"` for a button with a transparent background and a visible border:

```razor
<SunfishButton Variant="ButtonVariant.Primary" IsOutline="true">Outline</SunfishButton>
```

## Disabled state

```razor
<SunfishButton Disabled="true">Disabled</SunfishButton>
```

## See Also

- [Button Overview](xref:component-button-overview)

---
uid: globalization-rtl
title: RTL Support
description: Right-to-left layout support in Sunfish components.
---

# RTL Support

Sunfish components support right-to-left (RTL) text direction through a single theme property. When RTL mode is enabled, `SunfishThemeProvider` adds a `dir="rtl"` attribute to its root element, and component styles respond using CSS logical properties.

## Enabling RTL Mode

Set `SunfishTheme.IsRtl` to `true` on the active theme before or after the provider is initialized. The most common approach is to configure it at startup based on the user's locale:

```csharp
// Program.cs or a theme configuration service
var theme = new SunfishTheme
{
    IsRtl = IsRtlCulture(CultureInfo.CurrentCulture)
};

builder.Services.AddSunfish()
    .UseFluentUI()
    .WithTheme(theme);
```

```csharp
private static bool IsRtlCulture(CultureInfo culture)
{
    return culture.TextInfo.IsRightToLeft;
}
```

To switch RTL mode at runtime, update the theme through `ThemeService`:

```csharp
@inject ThemeService ThemeService

// Toggle RTL
await ThemeService.SetRtlAsync(true);
```

## How It Works

When `IsRtl` is `true`, `SunfishThemeProvider` renders:

```html
<div dir="rtl" data-sunfish-theme="light" style="--sunfish-...">
    @ChildContent
</div>
```

The `dir="rtl"` attribute activates CSS logical property mirroring for all descendant elements. This is a standard browser behavior — no JavaScript is required to flip layouts.

## CSS Logical Properties

Sunfish component styles use CSS logical properties exclusively instead of physical directional properties. This means layouts mirror automatically in RTL mode without any additional CSS rules.

| Physical property | Logical equivalent used by Sunfish |
|-------------------|------------------------------------|
| `margin-left` | `margin-inline-start` |
| `margin-right` | `margin-inline-end` |
| `padding-left` | `padding-inline-start` |
| `padding-right` | `padding-inline-end` |
| `border-left` | `border-inline-start` |
| `border-right` | `border-inline-end` |
| `left` | `inset-inline-start` |
| `right` | `inset-inline-end` |
| `text-align: left` | `text-align: start` |
| `text-align: right` | `text-align: end` |

Because the browser interprets all inline-axis properties relative to the writing direction, a component that uses `padding-inline-start: 12px` will have 12px of padding on the left in LTR and on the right in RTL with no additional style rules needed.

## Flex Direction Mirroring

Flex containers that use `flex-direction: row` automatically mirror their content order in RTL mode. Sunfish components rely on this browser behavior for all horizontal arrangements (icon + label, input + addon, etc.).

If you are writing custom layout code alongside Sunfish components, prefer `flex-direction: row` with `gap` over absolute positioning or physical margins to benefit from the same automatic mirroring.

## Icon Mirroring

Some icons are directional — an arrow pointing right conveys "next" in LTR but "previous" in RTL. Sunfish marks directional icons with the `mar-icon--rtl-mirror` class. When `dir="rtl"` is active, this class applies a `transform: scaleX(-1)` rule to horizontally flip the icon.

Icons that are non-directional (checkmarks, warnings, close buttons) are not mirrored.

When using the `SunfishIcon` component with a custom SVG, pass `Mirrored="true"` to opt the icon into RTL mirroring:

```razor
<SunfishIcon Name="arrow-right" Mirrored="true" />
```

## Testing RTL Layouts

To verify RTL behavior in bUnit tests, set `IsRtl` on the theme before rendering:

```csharp
public class RtlTests : SunfishTestBase
{
    [Fact]
    public void ThemeProvider_SetsRtlAttribute_WhenIsRtlTrue()
    {
        ThemeService.SetTheme(new SunfishTheme { IsRtl = true });

        var cut = RenderComponent<SunfishThemeProvider>(p => p
            .AddChildContent("<p>content</p>"));

        Assert.Equal("rtl", cut.Find("[dir]").GetAttribute("dir"));
    }
}
```

## Browser Support

CSS logical properties are supported in all modern browsers (Chrome, Firefox, Safari, Edge). The `dir` attribute has been a standard HTML attribute since HTML 4 and is universally supported.

## See Also

- [Globalization Overview](xref:globalization-overview)

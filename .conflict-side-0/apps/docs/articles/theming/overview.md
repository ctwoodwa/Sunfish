---
uid: theming-overview
title: Theming Overview
description: Understand the Sunfish provider system and learn how to customize colors, typography, and shape with SunfishTheme.
---

# Theming Overview

Sunfish separates component behavior from visual styling through its **provider system**. Every component asks a provider for its CSS classes, icons, and JS interop behavior at render time. You control the look and feel by configuring a `SunfishTheme` and wrapping your application in a `SunfishThemeProvider`.

## The SunfishTheme record

`SunfishTheme` is a C# record that holds all design tokens:

```csharp
public record SunfishTheme
{
    public SunfishColorPalette Colors { get; init; } = new();
    public SunfishTypographyScale Typography { get; init; } = new();
    public SunfishShape Shape { get; init; } = new();
    public bool IsRtl { get; init; }
}
```

### Color palette

`SunfishColorPalette` defines the semantic colors used throughout the component library -- primary, secondary, success, warning, error, surface, and background tokens. Each provider maps these tokens to its own design system values.

### Typography scale

`SunfishTypographyScale` provides font family, size, weight, and line-height values for headings, body text, captions, and other typographic roles.

### Shape

`SunfishShape` controls border radius values at small, medium, and large scales, giving you consistent rounding across all components.

## Using SunfishThemeProvider

Wrap your application layout in `SunfishThemeProvider` to make the theme available to all child components:

```razor
<SunfishThemeProvider Theme="@customTheme">
    <Router AppAssembly="@typeof(App).Assembly">
        <!-- ... -->
    </Router>
</SunfishThemeProvider>

@code {
    private SunfishTheme customTheme = new()
    {
        Colors = new SunfishColorPalette
        {
            Primary = "#0078D4",
            Secondary = "#2B88D8",
            Success = "#107C10",
            Warning = "#FFB900",
            Error = "#D13438"
        }
    };
}
```

When no custom theme is supplied, `SunfishThemeProvider` uses the provider's default theme values.

## Runtime theme switching

Inject `ISunfishThemeService` to change the theme at runtime:

```csharp
@inject ISunfishThemeService ThemeService

private void SwitchToDark()
{
    ThemeService.SetTheme(new SunfishTheme
    {
        Colors = new SunfishColorPalette { Background = "#1E1E1E", Surface = "#252525" }
    });
}
```

All components re-render automatically when the theme changes.

## See also

- [Providers](xref:theming-providers) -- the three provider contracts in detail.
- [Creating a Custom Provider](xref:theming-custom-provider) -- build your own provider from scratch.

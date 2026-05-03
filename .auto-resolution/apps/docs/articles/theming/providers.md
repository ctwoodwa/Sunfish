---
uid: theming-providers
title: Providers
description: Learn about ISunfishCssProvider, ISunfishIconProvider, and ISunfishJsInterop -- the three contracts that define a Sunfish provider.
---

# Providers

A Sunfish **provider** is a set of three service implementations that give components their visual identity. Each service is registered via dependency injection and consumed by the component base class at render time.

## ISunfishCssProvider

This is the core contract. It contains one method per component (or component state) that returns the CSS class string to apply:

```csharp
public interface ISunfishCssProvider
{
    string ButtonClass(ButtonVariant variant, ButtonSize size, bool isOutline, bool isDisabled);
    string CardClass();
    string AlertClass(AlertSeverity severity);
    string DialogClass();
    string TooltipClass(TooltipPosition position);
    // ... 80+ additional methods covering every component
}
```

The Fluent UI provider implements each method by mapping parameters to BEM-style class names defined in its SCSS:

```csharp
public class FluentUICssProvider : ISunfishCssProvider
{
    public string ButtonClass(ButtonVariant variant, ButtonSize size, bool isOutline, bool isDisabled)
    {
        var css = $"mar-button mar-button--{variant.ToString().ToLowerInvariant()} mar-button--{size.ToString().ToLowerInvariant()}";
        if (isOutline) css += " mar-button--outline";
        if (isDisabled) css += " mar-button--disabled";
        return css;
    }
    // ...
}
```

## ISunfishIconProvider

Responsible for resolving icon names to SVG markup:

```csharp
public interface ISunfishIconProvider
{
    MarkupString GetIcon(string name, IconSize size = IconSize.Medium);
    string GetIconSpriteUrl();
}
```

- `GetIcon` returns an SVG `MarkupString` for inline rendering.
- `GetIconSpriteUrl` returns the path to an SVG sprite sheet, used by components that reference icons via `<use>` elements.

## ISunfishJsInterop

Encapsulates all JavaScript interop calls the component library needs:

```csharp
public interface ISunfishJsInterop : IAsyncDisposable
{
    ValueTask InitializeAsync();
    ValueTask<bool> ShowModalAsync(string modalId);
    ValueTask HideModalAsync(string modalId);
    ValueTask<BoundingBox> GetElementBoundsAsync(ElementReference element);
    ValueTask ObserveScrollAsync(ElementReference element, DotNetObjectReference<object> callback);
}
```

Each provider ships its own JavaScript module and implements this interface to bridge Blazor with the browser DOM.

## How the FluentUI provider registers everything

The `UseFluentUI()` extension method on `SunfishBuilder` registers all three contracts:

```csharp
public static SunfishBuilder UseFluentUI(this SunfishBuilder builder, Action<FluentUIOptions>? configure = null)
{
    builder.Services.AddScoped<ISunfishCssProvider, FluentUICssProvider>();
    builder.Services.AddScoped<ISunfishIconProvider, FluentUIIconProvider>();
    builder.Services.AddScoped<ISunfishJsInterop, FluentUIJsInterop>();
    // ...
    return builder;
}
```

This pattern makes it straightforward to create a new provider: implement the three interfaces, register them in an extension method, and every Sunfish component picks them up automatically.

## Bootstrap provider

Register Bootstrap 5.3 as the active provider:

```csharp
// Program.cs
builder.Services.AddSunfish().UseBootstrap();
```

Reference the stylesheet in `App.razor`:

```html
<link rel="stylesheet" href="_content/Sunfish.Providers.Bootstrap/css/sunfish-bootstrap.css" />
```

The Bootstrap provider maps Sunfish component state to native Bootstrap 5.3 utility classes wherever a direct equivalent exists, and falls back to BEM-style `mar-bs-*` classes for component-specific structure that Bootstrap does not cover. Bootstrap's own Sass variables remain globally available inside the provider's SCSS so `$primary`, `$border-color`, and similar variables are always in scope without `@use`.

## Material 3 provider

Register Material Design 3 as the active provider:

```csharp
// Program.cs
builder.Services.AddSunfish().UseMaterial();
```

Reference the stylesheet in `App.razor`:

```html
<link rel="stylesheet" href="_content/Sunfish.Providers.Material/css/sunfish-material.css" />
```

The Material 3 provider uses a two-layer token architecture. A `$material-ref-palette` Sass map holds the raw 13-stop palette; the SCSS then emits semantic role custom properties (`--md-sys-color-primary`, etc.) from that map. Sunfish's `--sunfish-*` tokens are mapped to the M3 role tokens so all built-in components stay consistent with Material You color expectations.

## Built-in providers

| Provider | Extension | CSS prefix | Design system | Dark mode |
| --- | --- | --- | --- | --- |
| FluentUI | `UseFluentUI()` | `mar-` | Fluent UI 2 | `[data-sunfish-theme="dark"]` |
| Bootstrap | `UseBootstrap()` | `mar-bs-` | Bootstrap 5.3 | `[data-sunfish-theme="dark"]` + Bootstrap color-mode |
| Material 3 | `UseMaterial()` | `mar-` | Material Design 3 | `[data-sunfish-theme="dark"]` |

All three providers implement the same `ISunfishCssProvider`, `ISunfishIconProvider`, and `ISunfishJsInterop` contracts, so switching between them requires only changing the extension method call and the stylesheet `<link>` in `App.razor`.

## See also

- [Theming Overview](xref:theming-overview) -- configure colors, typography, and shape.
- [Token Reference](xref:theming-token-reference) -- complete `--sunfish-*` CSS custom property reference.
- [Dark Mode](xref:theming-dark-mode) -- enabling and toggling dark mode.
- [Runtime Provider Switching](xref:theming-runtime-switching) -- swap providers at runtime without a page reload.
- [Creating a Custom Provider](xref:theming-custom-provider) -- step-by-step guide.

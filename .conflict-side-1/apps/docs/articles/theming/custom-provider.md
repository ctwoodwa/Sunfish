---
uid: theming-custom-provider
title: Creating a Custom Provider
description: Step-by-step guide to building your own Sunfish provider with custom CSS, icons, and JS interop.
---

# Creating a Custom Provider

This guide walks through building a provider from scratch. By the end you will have a NuGet-ready project that plugs into any Sunfish application with a single extension method call.

## 1. Create the project

```bash
dotnet new razorclasslib -n Sunfish.Providers.MyDesign
cd Sunfish.Providers.MyDesign
dotnet add reference ../Sunfish.Core/Sunfish.Core.csproj
```

## 2. Implement ISunfishCssProvider

Create a class that returns CSS class strings for every component. The interface contains 80+ methods; start with the components your application uses and fill in the rest over time.

```csharp
using Sunfish.Core.Contracts;
using Sunfish.Core.Enums;

namespace Sunfish.Providers.MyDesign;

public class MyDesignCssProvider : ISunfishCssProvider
{
    public string ButtonClass(ButtonVariant variant, ButtonSize size, bool isOutline, bool isDisabled)
    {
        var css = $"md-btn md-btn--{variant.ToString().ToLowerInvariant()} md-btn--{size.ToString().ToLowerInvariant()}";
        if (isOutline) css += " md-btn--outline";
        if (isDisabled) css += " md-btn--disabled";
        return css;
    }

    public string CardClass() => "md-card";

    public string AlertClass(AlertSeverity severity) =>
        $"md-alert md-alert--{severity.ToString().ToLowerInvariant()}";

    // Implement remaining methods ...
}
```

## 3. Create SCSS styles

Add a `Styles/` folder with your SCSS source files. Each component should have its own partial:

```text
Styles/
  _variables.scss
  _button.scss
  _card.scss
  _alert.scss
  sunfish-mydesign.scss   <-- imports all partials
```

Example `_button.scss`:

```scss
.md-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  border: 1px solid transparent;
  border-radius: var(--md-radius-md, 4px);
  cursor: pointer;

  &--primary { background: var(--md-color-primary); color: #fff; }
  &--secondary { background: var(--md-color-secondary); color: #fff; }
  &--small { padding: 0.25rem 0.5rem; font-size: 0.875rem; }
  &--medium { padding: 0.5rem 1rem; font-size: 1rem; }
  &--large { padding: 0.75rem 1.5rem; font-size: 1.125rem; }
  &--outline { background: transparent; border-color: currentColor; }
  &--disabled { opacity: 0.5; pointer-events: none; }
}
```

Compile the SCSS to `wwwroot/css/sunfish-mydesign.css` and mark it as static web asset.

## SCSS Architecture

All built-in providers follow the same folder contract. Your custom provider should too:

```text
Styles/
├── foundation/          Design tokens, Sass maps, CSS custom properties
│   ├── _colors.scss
│   ├── _spacing.scss
│   ├── _typography.scss
│   ├── _radius.scss
│   ├── _elevation.scss
│   ├── _motion.scss
│   ├── _functions.scss
│   ├── _mixins.scss
│   └── _tokens.scss
├── patterns/            Cross-component patterns
│   ├── _interactive-states.scss
│   ├── _field-base.scss
│   ├── _overlay.scss
│   ├── _validation.scss
│   └── _density.scss
├── components/          One file per component
│   ├── _button.scss
│   ├── _data-grid.scss
│   └── ...
├── _index.scss          Import aggregator
└── sunfish-myprovider.scss  Build entrypoint
```

Import order in `_index.scss`: Foundation then Patterns then Components (alphabetical within each group).

`_index.scss` uses `@forward` for each partial:

```scss
// foundation
@forward 'foundation/colors';
@forward 'foundation/spacing';
@forward 'foundation/typography';
@forward 'foundation/radius';
@forward 'foundation/elevation';
@forward 'foundation/motion';
@forward 'foundation/functions';
@forward 'foundation/mixins';
@forward 'foundation/tokens';

// patterns
@forward 'patterns/interactive-states';
@forward 'patterns/field-base';
@forward 'patterns/overlay';
@forward 'patterns/validation';
@forward 'patterns/density';

// components (alphabetical)
@forward 'components/alert';
@forward 'components/button';
@forward 'components/card';
// ...
```

The build entrypoint `sunfish-myprovider.scss` simply re-exports the aggregator:

```scss
@forward 'index';
```

### Color tokens

`foundation/_colors.scss` must emit both the light token block and the dark override block. Co-locating them keeps light/dark pairs together and avoids split-file confusion:

```scss
:root {
  --sunfish-color-primary:    #your-primary;
  --sunfish-color-surface:    #ffffff;
  --sunfish-color-text:       #1a1a1a;
  // ...
}

[data-sunfish-theme="dark"] {
  --sunfish-color-primary:    #your-primary-dark-variant;
  --sunfish-color-surface:    #1e1e1e;
  --sunfish-color-text:       #f0f0f0;
  // ...
}
```

See [Token Reference](xref:theming-token-reference) for the full list of `--sunfish-*` tokens your provider should define.

### Interactive state tints

When computing hover/selected/active tints with `color-mix()`, always use `var(--sunfish-color-surface)` as the mix base, never a hard-coded hex. This ensures tints are dark-tinted in dark mode:

```scss
// Correct
.mar-button:hover {
  background: color-mix(in srgb, var(--sunfish-color-primary) 10%, var(--sunfish-color-surface));
}

// Wrong -- produces light tint even in dark mode
.mar-button:hover {
  background: color-mix(in srgb, var(--sunfish-color-primary) 10%, #ffffff);
}
```

## 4. Implement ISunfishIconProvider

```csharp
public class MyDesignIconProvider : ISunfishIconProvider
{
    public MarkupString GetIcon(string name, IconSize size = IconSize.Medium)
    {
        // Return inline SVG or reference a sprite sheet
        return new MarkupString($"<svg class=\"md-icon md-icon--{size.ToString().ToLowerInvariant()}\"><use href=\"_content/Sunfish.Providers.MyDesign/icons/sprite.svg#{name}\"/></svg>");
    }

    public string GetIconSpriteUrl() =>
        "_content/Sunfish.Providers.MyDesign/icons/sprite.svg";
}
```

## 5. Implement ISunfishJsInterop

```csharp
public class MyDesignJsInterop : ISunfishJsInterop
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public MyDesignJsInterop(IJSRuntime js) => _js = js;

    public async ValueTask InitializeAsync()
    {
        _module = await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sunfish.Providers.MyDesign/js/sunfish-mydesign.js");
    }

    // Implement remaining methods using _module.InvokeAsync ...

    public async ValueTask DisposeAsync()
    {
        if (_module is not null) await _module.DisposeAsync();
    }
}
```

## 6. Register via SunfishBuilder extension

Create an extension method so consumers can opt in with a single call:

```csharp
using Sunfish.Core.Contracts;
using Sunfish.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Providers.MyDesign;

public static class SunfishBuilderExtensions
{
    public static SunfishBuilder UseMyDesign(this SunfishBuilder builder)
    {
        builder.Services.AddScoped<ISunfishCssProvider, MyDesignCssProvider>();
        builder.Services.AddScoped<ISunfishIconProvider, MyDesignIconProvider>();
        builder.Services.AddScoped<ISunfishJsInterop, MyDesignJsInterop>();
        return builder;
    }
}
```

Consumers register it in `Program.cs`:

```csharp
builder.Services.AddSunfish().UseMyDesign();
```

## 7. Reference the stylesheet

In the consuming application's `App.razor`:

```html
<link rel="stylesheet" href="_content/Sunfish.Providers.MyDesign/css/sunfish-mydesign.css" />
```

## See also

- [Theming Overview](xref:theming-overview) -- `SunfishTheme` and `SunfishThemeProvider`.
- [Providers](xref:theming-providers) -- full reference for the three provider contracts.

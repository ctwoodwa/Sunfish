---
uid: theming-dark-mode
title: Dark Mode
description: How to enable and toggle dark mode in Sunfish applications.
---

# Dark Mode

Sunfish dark mode works by adding a `data-sunfish-theme="dark"` attribute to the element rendered by `SunfishThemeProvider`. All provider SCSS files include a `[data-sunfish-theme="dark"]` block that redefines `--sunfish-*` tokens for dark surfaces. Because the overrides are purely CSS custom property reassignments, switching is instant and requires no page reload.

## How it works

`SunfishThemeProvider` renders a wrapping element and manages the `data-sunfish-theme` attribute:

```razor
<!-- rendered output when dark mode is active -->
<div data-sunfish-theme="dark">
    <!-- your application content -->
</div>
```

Each provider's `_colors.scss` co-locates the light token block (`:root`) and the dark override block in the same file:

```scss
// foundation/_colors.scss (FluentUI example)
:root {
  --sunfish-color-background: #FAF9F8;
  --sunfish-color-surface:    #ffffff;
  --sunfish-color-text:       #323130;
  // ...
}

[data-sunfish-theme="dark"] {
  --sunfish-color-background: #1B1A19;
  --sunfish-color-surface:    #252423;
  --sunfish-color-text:       #F3F2F1;
  // ...
}
```

Any component that reads `--sunfish-color-surface` or another token automatically picks up the dark value when the attribute is present on an ancestor.

## Enabling dark mode at startup

Pass `IsDarkMode = true` when constructing `SunfishTheme`:

```csharp
// Program.cs
builder.Services.AddSunfish().UseFluentUI();

// or configure the initial theme
```

```razor
@* App.razor *@
<SunfishThemeProvider Theme="@_theme">
    <Router AppAssembly="@typeof(App).Assembly">
        <Found Context="routeData">
            <RouteView RouteData="routeData" DefaultLayout="@typeof(MainLayout)" />
        </Found>
    </Router>
</SunfishThemeProvider>

@code {
    private SunfishTheme _theme = new() { IsDarkMode = true };
}
```

## Programmatic control with ISunfishThemeService

Inject `ISunfishThemeService` anywhere in the component tree to read or change the dark mode state at runtime:

```razor
@inject ISunfishThemeService ThemeService

<button @onclick="ToggleDark">
    @(ThemeService.IsDarkMode ? "Light mode" : "Dark mode")
</button>

@code {
    private Task ToggleDark() =>
        ThemeService.SetDarkModeAsync(!ThemeService.IsDarkMode);
}
```

### ISunfishThemeService members

| Member | Description |
| --- | --- |
| `bool IsDarkMode` | Returns `true` when dark mode is currently active. |
| `Task SetDarkModeAsync(bool isDark)` | Sets dark mode on or off. Use this for all state restoration (localStorage, user preferences) -- it is idempotent. |
| `event Action? OnThemeChanged` | Raised after any theme change, including dark mode toggles. |

> Always use `SetDarkModeAsync(bool)` when restoring state from storage. `ToggleDarkModeAsync()` flips the current value, so calling it during initialization can produce the wrong result if `InitializeAsync` has already pre-set `IsDarkMode`.

## localStorage persistence

The Demo app persists the user's preference across page loads using `IJSRuntime`. The pattern is:

```razor
@* MainLayout.razor *@
@inject ISunfishThemeService ThemeService
@inject IJSRuntime JS

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var stored = await JS.InvokeAsync<string?>("localStorage.getItem", "sunfish-dark");
            if (stored is "true" or "false")
                await ThemeService.SetDarkModeAsync(stored == "true");

            ThemeService.OnThemeChanged += OnThemeChanged;
        }
    }

    private async void OnThemeChanged()
    {
        await JS.InvokeVoidAsync(
            "localStorage.setItem", "sunfish-dark",
            ThemeService.IsDarkMode.ToString().ToLowerInvariant());
    }

    public void Dispose() =>
        ThemeService.OnThemeChanged -= OnThemeChanged;
}
```

## Bootstrap color-mode integration

When using `Sunfish.Providers.Bootstrap`, the Bootstrap provider listens for `[data-sunfish-theme="dark"]` and also applies Bootstrap's own `[data-bs-theme="dark"]` attribute so Bootstrap's own `--bs-*` tokens flip correctly. Provider SCSS includes a dual-selector block for every Bootstrap-native token that needs to change in dark mode:

```scss
[data-sunfish-theme="dark"],
[data-bs-theme="dark"] {
  --bs-body-bg:      #1a1a2e;
  --bs-body-color:   #dee2e6;
  --bs-border-color: rgba(255,255,255,0.15);
  // ...
}
```

This ensures Bootstrap utility classes (`bg-body`, `text-body`, etc.) render correctly without requiring the application to manage `data-bs-theme` independently.

## Runtime switching without page reload

Because dark mode is implemented entirely through CSS custom properties, switching is paint-only -- no Blazor re-render of component trees is triggered. `SunfishThemeProvider` updates the DOM attribute directly via JS interop, which causes the browser to recompute computed styles for all affected elements in a single pass.

Components that cache rendered markup (such as virtualized lists) may briefly show stale colors until their next render cycle. This is generally imperceptible at toggle speeds.

## Defining dark overrides in a custom provider

If you are building a custom provider (see [Creating a Custom Provider](xref:theming-custom-provider)), add a `[data-sunfish-theme="dark"]` block to your `foundation/_colors.scss`:

```scss
:root {
  --sunfish-color-surface: #ffffff;
  --sunfish-color-text:    #1a1a1a;
}

[data-sunfish-theme="dark"] {
  --sunfish-color-surface: #1e1e1e;
  --sunfish-color-text:    #f0f0f0;
}
```

Use `color-mix()` with `var(--sunfish-color-surface)` as the mix base -- never hard-code `#ffffff` -- so interactive state tints (hover, selected, active) automatically become dark-tinted in dark mode:

```scss
// Correct: tracks the surface token
background: color-mix(in srgb, var(--sunfish-color-primary) 10%, var(--sunfish-color-surface));

// Wrong: always produces a light tint
background: color-mix(in srgb, var(--sunfish-color-primary) 10%, #ffffff);
```

## See also

- [Theming Overview](xref:theming-overview) -- `SunfishTheme` and `SunfishThemeProvider`.
- [Token Reference](xref:theming-token-reference) -- full list of `--sunfish-*` tokens and their dark defaults.
- [Runtime Provider Switching](xref:theming-runtime-switching) -- swap providers at runtime.

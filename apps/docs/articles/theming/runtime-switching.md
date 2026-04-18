---
uid: theming-runtime-switching
title: Runtime Provider Switching
description: How to switch between FluentUI, Bootstrap, and Material 3 providers at runtime.
---

# Runtime Provider Switching

Sunfish supports switching the active design-system provider while the application is running. The Demo app uses this to let users preview FluentUI, Bootstrap, and Material 3 side-by-side without navigating to a different URL.

Because each provider ships a separate compiled CSS file, a provider switch must reload the page to ensure the new stylesheet is in place and the old one is gone. The pattern below uses `location.reload()` after persisting the selection to `localStorage`, with an inline `<script>` in `App.razor` that reads the stored value before the Blazor framework initializes to prevent a flash of the wrong styles (FOUC).

---

## Architecture overview

Provider switching involves three layers:

1. **`ProviderSwitcher` service** -- a C# adapter that holds concrete instances of all three providers and delegates `ISunfishCssProvider`, `ISunfishIconProvider`, and `ISunfishJsInterop` calls to whichever one is currently active.
2. **CSS `<link>` tag toggling** -- each provider's stylesheet is loaded with a unique `id`. All but the active one have `disabled` set. The inline `<script>` in `App.razor` applies the stored choice immediately, before Blazor renders.
3. **`localStorage` persistence** -- the active provider key (`"fluentui"`, `"bootstrap"`, or `"material"`) is stored so it survives page reloads.

---

## Step 1 -- The ProviderSwitcher adapter

`ProviderSwitcher` implements all three provider interfaces and holds a concrete instance of each provider. Switching is a single property assignment:

```csharp
// Services/ProviderSwitcher.cs
public enum DesignProvider { FluentUI, Bootstrap, Material }

public class ProviderSwitcher : ISunfishCssProvider, ISunfishIconProvider, ISunfishJsInterop
{
    private readonly ISunfishCssProvider _fluentCss;
    private readonly ISunfishCssProvider _bootstrapCss;
    private readonly ISunfishCssProvider _materialCss;
    // ... icon and JS interop fields follow the same pattern

    public DesignProvider ActiveProvider { get; private set; } = DesignProvider.FluentUI;

    public event Action? OnProviderChanged;

    public ProviderSwitcher(
        FluentUICssProvider fluentCss,
        BootstrapCssProvider bootstrapCss,
        MaterialCssProvider materialCss,
        /* ... icon and JS interop concrete types ... */)
    {
        _fluentCss = fluentCss;
        _bootstrapCss = bootstrapCss;
        _materialCss = materialCss;
        // ...
    }

    private ISunfishCssProvider Css => ActiveProvider switch
    {
        DesignProvider.Bootstrap => _bootstrapCss,
        DesignProvider.Material  => _materialCss,
        _                        => _fluentCss,
    };

    // Delegate every interface method to Css / Icons / JsInterop
    public string ButtonClass(ButtonVariant variant, ButtonSize size, bool isOutline, bool isDisabled)
        => Css.ButtonClass(variant, size, isOutline, isDisabled);
    // ... all other ISunfishCssProvider methods ...

    public void SetProvider(DesignProvider provider)
    {
        if (ActiveProvider == provider) return;
        ActiveProvider = provider;
        OnProviderChanged?.Invoke();
    }
}
```

Register it in `Program.cs` in place of a single provider, and register all three concrete providers as non-interface types so `ProviderSwitcher` can receive them via constructor injection:

```csharp
// Program.cs
builder.Services.AddScoped<FluentUICssProvider>();
builder.Services.AddScoped<BootstrapCssProvider>();
builder.Services.AddScoped<MaterialCssProvider>();
// ... same for icon and JS interop types ...

builder.Services.AddScoped<ProviderSwitcher>();
builder.Services.AddScoped<ISunfishCssProvider>(sp => sp.GetRequiredService<ProviderSwitcher>());
builder.Services.AddScoped<ISunfishIconProvider>(sp => sp.GetRequiredService<ProviderSwitcher>());
builder.Services.AddScoped<ISunfishJsInterop>(sp => sp.GetRequiredService<ProviderSwitcher>());
```

---

## Step 2 -- CSS link tags and FOUC prevention

Load all three stylesheets in `App.razor`. The inactive ones start with `disabled`. An inline `<script>` reads `localStorage` before Blazor initializes and enables the correct link immediately:

```html
<!-- App.razor <head> -->
<link id="sunfish-provider-fluentui"
      rel="stylesheet"
      href="_content/Sunfish.Providers.FluentUI/css/sunfish-fluentui.css" />
<link id="sunfish-provider-bootstrap"
      rel="stylesheet"
      href="_content/Sunfish.Providers.Bootstrap/css/sunfish-bootstrap.css"
      disabled />
<link id="sunfish-provider-material"
      rel="stylesheet"
      href="_content/Sunfish.Providers.Material/css/sunfish-material.css"
      disabled />

<script>
    // Apply stored provider immediately to prevent flash of wrong styles (FOUC).
    // This runs synchronously before any Blazor rendering.
    try {
        var p = localStorage.getItem('marilo:provider');
        if (p === 'bootstrap') {
            document.getElementById('sunfish-provider-fluentui').disabled = true;
            document.getElementById('sunfish-provider-bootstrap').disabled = false;
        } else if (p === 'material') {
            document.getElementById('sunfish-provider-fluentui').disabled = true;
            document.getElementById('sunfish-provider-material').disabled = false;
        }
    } catch(e) {}
</script>
```

---

## Step 3 -- The provider-switcher JS module

A small ES module handles persistence and the actual reload:

```js
// wwwroot/js/provider-switcher.js
export function setProvider(provider) {
    try {
        localStorage.setItem('marilo:provider', provider);
    } catch { }
    location.reload();
}

export function getStoredProvider() {
    try {
        return localStorage.getItem('marilo:provider') || 'fluentui';
    } catch {
        return 'fluentui';
    }
}

export function applyProvider(provider) {
    const fluentLink    = document.getElementById('sunfish-provider-fluentui');
    const bootstrapLink = document.getElementById('sunfish-provider-bootstrap');
    const materialLink  = document.getElementById('sunfish-provider-material');

    if (fluentLink)    fluentLink.disabled    = true;
    if (bootstrapLink) bootstrapLink.disabled = true;
    if (materialLink)  materialLink.disabled  = true;

    if (provider === 'bootstrap' && bootstrapLink) {
        bootstrapLink.disabled = false;
    } else if (provider === 'material' && materialLink) {
        materialLink.disabled = false;
    } else if (fluentLink) {
        fluentLink.disabled = false;
    }
}
```

`setProvider` writes to storage and calls `location.reload()`. The reload triggers the inline `<script>` in `App.razor` which re-enables the correct `<link>` before any paint, eliminating flash.

`applyProvider` is used on the Blazor side (without reload) to keep the in-memory `ProviderSwitcher` in sync during the same session when switching back to a provider that was already loaded.

---

## Step 4 -- Triggering a switch from a component

Inject `ProviderSwitcher` and the JS module reference into your layout:

```razor
@inject ProviderSwitcher ProviderSwitcher
@inject IJSRuntime JS

@code {
    private DesignProvider _activeProvider = DesignProvider.FluentUI;
    private IJSObjectReference? _providerModule;

    private async Task SwitchProvider(DesignProvider provider)
    {
        if (_activeProvider == provider) return;

        try
        {
            _providerModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./js/provider-switcher.js");

            var key = provider switch
            {
                DesignProvider.Bootstrap => "bootstrap",
                DesignProvider.Material  => "material",
                _                        => "fluentui",
            };

            // Persists to localStorage and calls location.reload()
            await _providerModule.InvokeVoidAsync("setProvider", key);
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _providerModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./js/provider-switcher.js");

            // Restore the stored provider without reloading (page already loaded correctly)
            var storedProvider = await _providerModule.InvokeAsync<string>("getStoredProvider");
            if (storedProvider == "bootstrap")
            {
                _activeProvider = DesignProvider.Bootstrap;
                ProviderSwitcher.SetProvider(DesignProvider.Bootstrap);
                await _providerModule.InvokeVoidAsync("applyProvider", "bootstrap");
            }
            else if (storedProvider == "material")
            {
                _activeProvider = DesignProvider.Material;
                ProviderSwitcher.SetProvider(DesignProvider.Material);
                await _providerModule.InvokeVoidAsync("applyProvider", "material");
            }

            StateHasChanged();
        }
    }
}
```

Note the distinction between `setProvider` (stores + reloads) and `applyProvider` (DOM-only, no reload). `applyProvider` is called in `OnAfterRenderAsync` to re-synchronize the `<link>` disable state with the value that was already applied by the inline script, without triggering a second reload.

---

## Why `location.reload()` instead of in-place CSS swapping

Each provider's stylesheet is hundreds of kilobytes and may declare global resets, `:root` tokens, and structural rules that conflict with another provider's rules if both are active simultaneously. Disabling a `<link>` element removes it from the cascade but the browser retains the parsed rules in memory; re-enabling a different link does not always guarantee a clean cascade. Reloading the page ensures:

- Only one provider's rules are parsed and active.
- Any component state that captured provider-specific values (e.g., measured scroll heights, JS module references) is discarded and re-initialized with the new provider.
- The FOUC-prevention inline script fires before any render, so there is no visible flash.

The trade-off is a full page load on each provider switch, which is acceptable in a settings or demo context but would not be appropriate for a frequently-triggered UI action.

---

## See also

- [Providers](xref:theming-providers) -- the three provider contracts and built-in comparison table.
- [Dark Mode](xref:theming-dark-mode) -- toggling dark mode without a page reload.
- [Creating a Custom Provider](xref:theming-custom-provider) -- building a fourth provider to add to the switcher.

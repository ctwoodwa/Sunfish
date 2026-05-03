---
uid: getting-started-first-component
title: First Component
description: Build a simple Blazor page using a SunfishButton with an OnClick handler.
---

# First Component

This walkthrough creates a minimal Blazor page that uses `SunfishButton` to demonstrate how Sunfish components work.

## Create the page

Add a new Razor page to your project:

```razor
@page "/hello"

<h1>Hello Sunfish</h1>

<SunfishButton Variant="ButtonVariant.Primary"
              Size="ButtonSize.Medium"
              OnClick="@HandleClick">
    Say Hello
</SunfishButton>

@if (!string.IsNullOrEmpty(greeting))
{
    <SunfishAlert Severity="AlertSeverity.Success">
        @greeting
    </SunfishAlert>
}

@code {
    private string greeting = "";

    private void HandleClick()
    {
        greeting = $"Hello from Sunfish! The time is {DateTime.Now:T}.";
    }
}
```

## What is happening

1. **`SunfishButton`** renders a `<button>` element whose CSS classes come from the registered `ISunfishCssProvider`. The `Variant` and `Size` parameters control the visual style without you writing any CSS.
2. **`OnClick`** is an `EventCallback<MouseEventArgs>` that fires when the user clicks the button. Sunfish automatically calls `StateHasChanged` after the callback completes.
3. **`SunfishAlert`** shows a success banner. The `Severity` parameter maps to a provider-defined color scheme (info, success, warning, error).

## Try different variants

Swap in other parameter values to explore the component API:

```razor
<SunfishButton Variant="ButtonVariant.Danger" IsOutline="true" Size="ButtonSize.Large">
    Delete
</SunfishButton>

<SunfishButton Variant="ButtonVariant.Secondary" Disabled="true">
    Disabled
</SunfishButton>
```

## Next steps

- Browse the [Components](xref:component-button-overview) section for the full parameter reference of every component.
- Read [Theming Overview](xref:theming-overview) to learn how to customize the look and feel.

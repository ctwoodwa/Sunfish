---
uid: component-button-overview
title: Button
description: The SunfishButton component renders a styled button with variant, size, outline, and disabled options.
---

# Button

## Overview

The `SunfishButton` component renders a `<button>` element styled through the active CSS provider. It supports multiple visual variants, three sizes, an outline mode, and a disabled state. Use it for primary actions, form submissions, and any interactive trigger in your application.

## Creating a Button

````razor
<SunfishButton Variant="ButtonVariant.Primary" OnClick="@HandleClick">
    Save Changes
</SunfishButton>

@code {
    private void HandleClick()
    {
        // handle the click
    }
}
````

## Features

- **Variants** -- Primary, Secondary, Danger, and more via `ButtonVariant`. See [Appearance](appearance.md).
- **Sizes** -- Small, Medium, Large via `ButtonSize`.
- **Outline mode** -- Set `IsOutline="true"` for a transparent background with a border.
- **Disabled state** -- Set `Disabled="true"` to prevent interaction and apply disabled styling.
- **Click handling** -- Bind to `OnClick` for user interaction. See [Events](events.md).
- **Provider-driven styling** -- All CSS classes come from `ISunfishCssProvider.ButtonClass`, so the button automatically matches your chosen design system.

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `Variant` | `ButtonVariant` | `ButtonVariant.Primary` | The visual style variant of the button. |
| `Size` | `ButtonSize` | `ButtonSize.Medium` | The size of the button (Small, Medium, Large). |
| `IsOutline` | `bool` | `false` | When `true`, renders the button with a transparent background and a border. |
| `Disabled` | `bool` | `false` | When `true`, disables the button and applies disabled styling. |
| `OnClick` | `EventCallback<MouseEventArgs>` | -- | Callback fired when the button is clicked. |
| `ChildContent` | `RenderFragment?` | `null` | The content displayed inside the button. |

## See Also

- [API Reference](xref:Sunfish.UIAdapters.Blazor.Components.Buttons.SunfishButton)

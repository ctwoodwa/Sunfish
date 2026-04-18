---
uid: component-dialog-overview
title: Dialog
description: The SunfishDialog component displays a modal overlay with a title, content area, and close behavior.
---

# Dialog

## Overview

The `SunfishDialog` component renders a modal dialog over an overlay backdrop. It supports an optional title bar and fires a callback when the user closes it (by clicking the overlay or a close action). The dialog only renders when `IsOpen` is `true`.

## Creating a Dialog

````razor
<SunfishButton OnClick="@(() => isOpen = true)">Open Dialog</SunfishButton>

<SunfishDialog IsOpen="@isOpen" Title="Confirm Action" OnClose="@(() => isOpen = false)">
    <p>Are you sure you want to proceed?</p>
    <SunfishButton Variant="ButtonVariant.Primary" OnClick="@(() => isOpen = false)">
        Yes
    </SunfishButton>
    <SunfishButton Variant="ButtonVariant.Secondary" OnClick="@(() => isOpen = false)">
        Cancel
    </SunfishButton>
</SunfishDialog>

@code {
    private bool isOpen = false;
}
````

## Features

- **Conditional rendering** -- The dialog markup is only present in the DOM when `IsOpen` is `true`.
- **Overlay dismiss** -- Clicking the overlay backdrop fires `OnClose`. Click propagation is stopped on the dialog content itself.
- **Optional title** -- When `Title` is set, a title bar is rendered above the content.
- **Flexible content** -- Any Razor markup can be placed inside, including buttons, forms, or other Sunfish components.

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `IsOpen` | `bool` | `false` | Controls whether the dialog is visible. |
| `Title` | `string?` | `null` | Optional title displayed in the dialog header. |
| `OnClose` | `EventCallback` | -- | Callback fired when the dialog should close (overlay click). |
| `ChildContent` | `RenderFragment?` | `null` | The content displayed inside the dialog body. |

## See Also

- [API Reference](xref:Sunfish.Components.Feedback.SunfishDialog)

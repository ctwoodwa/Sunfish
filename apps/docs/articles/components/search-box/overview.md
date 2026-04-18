---
uid: component-searchbox-overview
title: Search Box
description: The SunfishSearchBox component provides a search input with clear button, keyboard hint, and search/escape callbacks.
---

# Search Box

## Overview

The `SunfishSearchBox` component builds on `SunfishTextField` to provide a dedicated search experience. It includes a built-in search icon prefix, a clear button that appears when the input has a value, an optional keyboard shortcut hint badge, and callbacks for search submission and escape key handling.

## Creating a Search Box

````razor
<SunfishSearchBox @bind-Value="query"
                 Placeholder="Search items..."
                 KbdHint="/"
                 OnSearch="@HandleSearch"
                 OnEscape="@HandleEscape" />

@code {
    private string query = "";

    private void HandleSearch(string value)
    {
        // Perform search with value
    }

    private void HandleEscape()
    {
        // Close search or clear focus
    }
}
````

## Features

- **Built-in search icon** -- A search icon is automatically rendered as a prefix.
- **Clear button** -- When the input has a value, a clear button replaces the keyboard hint and resets the input.
- **Keyboard hint** -- Set `KbdHint` to display a `<kbd>` badge (e.g., `/`) when the input is empty, indicating the keyboard shortcut to focus the search box.
- **Search on Enter** -- The `OnSearch` callback fires when the user presses Enter.
- **Escape handling** -- The `OnEscape` callback fires when the user presses Escape.
- **Two-way binding** -- Use `@bind-Value` for value synchronization.

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `Value` | `string` | `""` | The current search text. |
| `ValueChanged` | `EventCallback<string>` | -- | Callback fired when the value changes. Used by `@bind-Value`. |
| `Placeholder` | `string?` | `null` | Placeholder text. Defaults to "Search..." when not specified. |
| `KbdHint` | `string?` | `null` | Keyboard shortcut hint displayed as a `<kbd>` element when the input is empty. |
| `OnSearch` | `EventCallback<string>` | -- | Callback fired when the user presses Enter. |
| `OnEscape` | `EventCallback` | -- | Callback fired when the user presses Escape. |
| `Disabled` | `bool` | `false` | When `true`, disables the search input and clear button. |

## See Also

- [API Reference](xref:Sunfish.Components.Blazor.Components.Forms.Inputs.SunfishSearchBox)

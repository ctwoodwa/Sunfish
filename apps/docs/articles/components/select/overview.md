---
uid: component-select-overview
title: Select
description: The SunfishSelect component renders a styled dropdown select element with validation and two-way binding.
---

# Select

## Overview

The `SunfishSelect` component renders a native HTML `<select>` element with provider-driven styling. It supports two-way binding, validation state, and accepts standard `<option>` elements as child content.

## Creating a Select

````razor
<SunfishSelect @bind-Value="selectedColor">
    <option value="">-- Choose a color --</option>
    <option value="red">Red</option>
    <option value="green">Green</option>
    <option value="blue">Blue</option>
</SunfishSelect>

@code {
    private string selectedColor = "";
}
````

## Features

- **Two-way binding** -- Use `@bind-Value` for automatic synchronization.
- **Native `<option>` elements** -- Place standard HTML `<option>` elements inside `ChildContent`.
- **Validation** -- Set `IsInvalid="true"` to apply error styling.
- **Provider-driven styling** -- CSS classes are resolved via `ISunfishCssProvider.SelectClass(isInvalid)`.

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `Value` | `string` | `""` | The currently selected value. |
| `ValueChanged` | `EventCallback<string>` | -- | Callback fired when the selection changes. Used by `@bind-Value`. |
| `IsInvalid` | `bool` | `false` | When `true`, applies invalid/error styling. |
| `ChildContent` | `RenderFragment?` | `null` | The `<option>` elements rendered inside the select. |

## See Also

- [API Reference](xref:Sunfish.Components.Forms.Inputs.SunfishSelect)

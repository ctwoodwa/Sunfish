---
uid: component-textfield-overview
title: Text Field
description: The SunfishTextField component renders a styled text input with prefix/suffix slots, validation, and two-way binding.
---

# Text Field

## Overview

The `SunfishTextField` component renders a text `<input>` element inside a styled wrapper. It supports two-way binding, placeholder text, prefix and suffix render fragments, input type switching, validation state, and a disabled mode.

## Creating a Text Field

````razor
<SunfishTextField @bind-Value="name" Placeholder="Enter your name" />

@code {
    private string name = "";
}
````

## Features

- **Two-way binding** -- Use `@bind-Value` for automatic synchronization between the component and your model.
- **Input type** -- Set `InputType` to `"text"`, `"password"`, `"email"`, or any valid HTML input type.
- **Prefix and Suffix** -- Render custom content (icons, labels, buttons) before or after the input using `Prefix` and `Suffix` render fragments. See [Appearance](appearance.md).
- **Prefix/Suffix separators** -- `ShowPrefixSeparator` and `ShowSuffixSeparator` add a visual divider between the slot and the input.
- **Validation** -- Set `IsInvalid="true"` to apply error styling.
- **Disabled state** -- Set `Disabled="true"` to prevent interaction.

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `Value` | `string` | `""` | The current value of the input. |
| `ValueChanged` | `EventCallback<string>` | -- | Callback fired when the value changes. Used by `@bind-Value`. |
| `Placeholder` | `string?` | `null` | Placeholder text shown when the input is empty. |
| `InputType` | `string` | `"text"` | The HTML input type attribute. |
| `IsInvalid` | `bool` | `false` | When `true`, applies invalid/error styling. |
| `Disabled` | `bool` | `false` | When `true`, disables the input. |
| `Prefix` | `RenderFragment?` | `null` | Content rendered before the input. |
| `Suffix` | `RenderFragment?` | `null` | Content rendered after the input. |
| `ShowPrefixSeparator` | `bool` | `false` | When `true`, shows a visual separator after the prefix. |
| `ShowSuffixSeparator` | `bool` | `false` | When `true`, shows a visual separator before the suffix. |

## See Also

- [API Reference](xref:Sunfish.Components.Blazor.Components.Forms.Inputs.SunfishTextBox)

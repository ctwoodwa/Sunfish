---
uid: component-alert-overview
title: Alert
description: The SunfishAlert component displays contextual feedback messages with severity levels and optional dismissal.
---

# Alert

## Overview

The `SunfishAlert` component renders a banner that communicates a message to the user. It supports four severity levels (Info, Success, Warning, Error) and can optionally be dismissed by the user.

## Creating an Alert

````razor
<SunfishAlert Severity="AlertSeverity.Success">
    Your changes have been saved.
</SunfishAlert>
````

## Features

- **Severity levels** -- Info, Success, Warning, and Error, each with provider-defined color styling. See [Appearance](appearance.md).
- **Dismissible** -- Set `IsDismissible="true"` to show a close button. See [Events](events.md).
- **OnDismiss callback** -- Fires when the user clicks the dismiss button, allowing you to remove the alert or perform cleanup.
- **Flexible content** -- Accepts any Razor content via `ChildContent`.

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `Severity` | `AlertSeverity` | `AlertSeverity.Info` | The severity level that controls color and icon styling. |
| `IsDismissible` | `bool` | `false` | When `true`, a close button is rendered inside the alert. |
| `OnDismiss` | `EventCallback` | -- | Callback fired when the dismiss button is clicked. |
| `ChildContent` | `RenderFragment?` | `null` | The message content displayed inside the alert. |

## See Also

- [API Reference](xref:Sunfish.UIAdapters.Blazor.Components.Feedback.SunfishAlert)

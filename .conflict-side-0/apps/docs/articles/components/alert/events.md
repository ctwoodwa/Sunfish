---
uid: component-alert-events
title: Alert Events
description: Handle dismiss events from the SunfishAlert component.
---

# Alert Events

## OnDismiss

The `OnDismiss` parameter is an `EventCallback` that fires when the user clicks the dismiss button. This callback only fires when `IsDismissible` is `true`.

```razor
@if (showAlert)
{
    <SunfishAlert Severity="AlertSeverity.Info" IsDismissible="true" OnDismiss="@Dismiss">
        You have new notifications.
    </SunfishAlert>
}

@code {
    private bool showAlert = true;

    private void Dismiss()
    {
        showAlert = false;
    }
}
```

## See Also

- [Alert Overview](xref:component-alert-overview)

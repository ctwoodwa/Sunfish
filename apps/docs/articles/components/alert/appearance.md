---
uid: component-alert-appearance
title: Alert Appearance
description: Customize SunfishAlert severity levels and visual presentation.
---

# Alert Appearance

## Severity levels

The `Severity` parameter determines the color scheme of the alert:

```razor
<SunfishAlert Severity="AlertSeverity.Info">Informational message.</SunfishAlert>
<SunfishAlert Severity="AlertSeverity.Success">Operation completed.</SunfishAlert>
<SunfishAlert Severity="AlertSeverity.Warning">Please review before continuing.</SunfishAlert>
<SunfishAlert Severity="AlertSeverity.Error">An error occurred.</SunfishAlert>
```

Each severity maps to a distinct CSS class via `ISunfishCssProvider.AlertClass(severity)`. The provider determines the exact colors, icons, and border styles.

## Dismissible alerts

When `IsDismissible` is `true`, a close button appears inside the alert:

```razor
<SunfishAlert Severity="AlertSeverity.Warning" IsDismissible="true" OnDismiss="@HideAlert">
    This alert can be dismissed.
</SunfishAlert>
```

## See Also

- [Alert Overview](xref:component-alert-overview)

---
uid: component-dialog-events
title: Dialog Events
description: Handle close events from the SunfishDialog component.
---

# Dialog Events

## OnClose

The `OnClose` parameter is an `EventCallback` that fires when the user clicks the overlay backdrop. Use it to set `IsOpen` to `false`:

```razor
<SunfishDialog IsOpen="@isOpen" Title="My Dialog" OnClose="@CloseDialog">
    <p>Dialog content.</p>
</SunfishDialog>

@code {
    private bool isOpen = false;

    private void CloseDialog()
    {
        isOpen = false;
    }
}
```

Clicking inside the dialog content does not trigger `OnClose` because click propagation is stopped on the dialog element.

## See Also

- [Dialog Overview](xref:component-dialog-overview)

---
uid: component-button-events
title: Button Events
description: Handle click events from the SunfishButton component.
---

# Button Events

## OnClick

The `OnClick` parameter is an `EventCallback<MouseEventArgs>` that fires when the user clicks the button.

```razor
<SunfishButton OnClick="@HandleClick">Click Me</SunfishButton>

@code {
    private void HandleClick()
    {
        // Respond to the click
    }
}
```

You can also use an async handler:

```razor
@code {
    private async Task HandleClick()
    {
        await SaveDataAsync();
    }
}
```

The component automatically calls `StateHasChanged` after the callback completes.

## See Also

- [Button Overview](xref:component-button-overview)

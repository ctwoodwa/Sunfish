---
uid: component-textfield-events
title: Text Field Events
description: Handle value changes from the SunfishTextField component.
---

# Text Field Events

## ValueChanged

The `ValueChanged` parameter is an `EventCallback<string>` that fires on every input change. It is typically used via `@bind-Value`:

```razor
<SunfishTextField @bind-Value="searchTerm" />

@code {
    private string searchTerm = "";
}
```

For manual handling without two-way binding:

```razor
<SunfishTextField Value="@name" ValueChanged="@OnNameChanged" />

@code {
    private string name = "";

    private void OnNameChanged(string newValue)
    {
        name = newValue;
        // Perform additional logic
    }
}
```

## See Also

- [Text Field Overview](xref:component-textfield-overview)

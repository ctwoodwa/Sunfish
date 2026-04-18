---
uid: component-select-events
title: Select Events
description: Handle selection change events from the SunfishSelect component.
---

# Select Events

## ValueChanged

The `ValueChanged` parameter is an `EventCallback<string>` that fires when the user selects a different option. It is typically used via `@bind-Value`:

```razor
<SunfishSelect @bind-Value="country">
    <option value="us">United States</option>
    <option value="ca">Canada</option>
    <option value="uk">United Kingdom</option>
</SunfishSelect>

@code {
    private string country = "us";
}
```

For manual handling without two-way binding:

```razor
<SunfishSelect Value="@country" ValueChanged="@OnCountryChanged">
    <option value="us">United States</option>
    <option value="ca">Canada</option>
</SunfishSelect>

@code {
    private string country = "us";

    private void OnCountryChanged(string newValue)
    {
        country = newValue;
        // Load data for the selected country
    }
}
```

## See Also

- [Select Overview](xref:component-select-overview)

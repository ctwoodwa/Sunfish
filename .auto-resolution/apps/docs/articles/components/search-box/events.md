---
uid: component-searchbox-events
title: Search Box Events
description: Handle search and escape events from the SunfishSearchBox component.
---

# Search Box Events

## OnSearch

The `OnSearch` parameter is an `EventCallback<string>` that fires when the user presses **Enter** inside the search box. The current value is passed as the argument:

```razor
<SunfishSearchBox @bind-Value="query" OnSearch="@HandleSearch" />

@code {
    private string query = "";

    private void HandleSearch(string value)
    {
        // Execute search with the submitted value
    }
}
```

## OnEscape

The `OnEscape` parameter is an `EventCallback` that fires when the user presses **Escape**:

```razor
<SunfishSearchBox @bind-Value="query" OnEscape="@HandleEscape" />

@code {
    private string query = "";

    private void HandleEscape()
    {
        query = "";
        // Close search panel or return focus
    }
}
```

## ValueChanged

The `ValueChanged` callback fires on every keystroke, enabling real-time filtering. It is typically consumed via `@bind-Value`.

## See Also

- [Search Box Overview](xref:component-searchbox-overview)

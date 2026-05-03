---
uid: component-tabs-events
title: Tabs Events
description: Handle tab change events from the SunfishTabs component.
---

# Tabs Events

## ActiveTabIndexChanged

The `ActiveTabIndexChanged` parameter is an `EventCallback<int>` that fires when the user selects a different tab. It is typically used via `@bind-ActiveTabIndex`:

```razor
<SunfishTabs @bind-ActiveTabIndex="activeTab">
    <SunfishTabPanel Title="Tab 1">Content 1</SunfishTabPanel>
    <SunfishTabPanel Title="Tab 2">Content 2</SunfishTabPanel>
</SunfishTabs>

@code {
    private int activeTab = 0;
}
```

For manual handling:

```razor
<SunfishTabs ActiveTabIndex="@activeTab" ActiveTabIndexChanged="@OnTabChanged">
    <SunfishTabPanel Title="Tab 1">Content 1</SunfishTabPanel>
    <SunfishTabPanel Title="Tab 2">Content 2</SunfishTabPanel>
</SunfishTabs>

@code {
    private int activeTab = 0;

    private void OnTabChanged(int index)
    {
        activeTab = index;
        // Perform additional logic, e.g. lazy-load data
    }
}
```

## See Also

- [Tabs Overview](xref:component-tabs-overview)

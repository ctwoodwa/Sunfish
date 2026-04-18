---
uid: component-tabs-overview
title: Tabs
description: The SunfishTabs component organizes content into tabbed panels with automatic tab strip rendering.
---

# Tabs

## Overview

The `SunfishTabs` component displays a horizontal tab strip and shows the content of the active panel. Each panel is defined with `SunfishTabPanel`. The tab strip is generated automatically from the panel titles.

## Creating Tabs

````razor
<SunfishTabs @bind-ActiveTabIndex="activeTab">
    <SunfishTabPanel Title="Profile">
        <p>Profile content goes here.</p>
    </SunfishTabPanel>
    <SunfishTabPanel Title="Settings">
        <p>Settings content goes here.</p>
    </SunfishTabPanel>
    <SunfishTabPanel Title="Notifications">
        <p>Notifications content goes here.</p>
    </SunfishTabPanel>
</SunfishTabs>

@code {
    private int activeTab = 0;
}
````

## Features

- **Automatic tab strip** -- Tab buttons are rendered from the `Title` property of each `SunfishTabPanel`.
- **Two-way binding** -- Use `@bind-ActiveTabIndex` to synchronize the selected tab with your code.
- **Dynamic panels** -- Add or remove `SunfishTabPanel` children at runtime; the tab strip updates automatically.
- **Accessibility** -- Uses `role="tablist"` and `aria-selected` attributes.

## Parameters

### SunfishTabs

| Name | Type | Default | Description |
|---|---|---|---|
| `ActiveTabIndex` | `int` | `0` | The zero-based index of the currently active tab. |
| `ActiveTabIndexChanged` | `EventCallback<int>` | -- | Callback fired when the active tab changes. Used by `@bind-ActiveTabIndex`. |
| `ChildContent` | `RenderFragment?` | `null` | The `SunfishTabPanel` children. |

### SunfishTabPanel

| Name | Type | Default | Description |
|---|---|---|---|
| `Title` | `string` | `""` | The text displayed on the tab button. |
| `ChildContent` | `RenderFragment?` | `null` | The panel content shown when this tab is active. |

## See Also

- [API Reference](xref:Sunfish.Components.Layout.SunfishTabs)

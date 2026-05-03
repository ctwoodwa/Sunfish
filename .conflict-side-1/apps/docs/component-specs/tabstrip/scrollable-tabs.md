---
title: Tab Overflow
page_title: TabStrip Tab Overflow
description: Learn how to configure overflow behavior for tabs in the Blazor TabStrip component.
slug: tabstrip-scroll-tabs
tags: sunfish,blazor,tab,strip,tabstrip,scroll,overflow,tabs
published: True
position: 15
tag: updated
components: ["tabstrip"]
---
# TabStrip Tab Overflow

When more tabs exceed the available space, the Blazor TabStrip can handle the overflow automatically. Use the `OverflowMode` parameter to control this behavior.

The `OverflowMode` parameter accepts a value of type `TabStripOverflowMode`:

* `None` (default)&mdash;No overflow handling. All tabs are always visible and will wrap or overflow outside the container if they exceed the available width.
* `Scroll`&mdash;When tabs exceed the available space, scroll buttons appear so users can scroll through the tab list. This mode replaces the now-deprecated `Scrollable` parameter.
* `Menu`&mdash;When tabs exceed the available space, the overflowing tabs are collected into a dropdown menu. Users can open the menu to access and activate any hidden tab.

> The `Scrollable` parameter is obsolete. Use `OverflowMode="@TabStripOverflowMode.Scroll"` instead.

## Scroll Mode

Set `OverflowMode` to `TabStripOverflowMode.Scroll` to enable scrolling of the tab list. Set the `Width` parameter (for tabs at top or bottom position) or `Height` parameter (for tabs at left or right position) as well to define the visible area.

>caption TabStrip with scroll overflow

````RAZOR
<SunfishTabStrip OverflowMode="@TabStripOverflowMode.Scroll"
                 Width="300px">
    @{
        for (int i = 1; i <= 10; i++)
        {
            var index = i;
            <TabStripTab Title="@("Tab" + index)" @key="@index">
                Tab @index content.
            </TabStripTab>
        }
    }
</SunfishTabStrip>
````

### Scroll Buttons Visibility

You can configure the TabStrip to show scroll buttons on both ends of the tab list. To customize this behavior, use the `ScrollButtonsVisibility` parameter of the TabStrip. This option accepts the following values of type `TabStripScrollButtonsVisibility`:

* `Visible` (default)&mdash;The scroll buttons are always visible. If the tabs fit inside the TabStrip boundaries, the buttons are disabled.
* `Auto`&mdash;The scroll buttons appear only when the tabs do not fit inside the TabStrip boundaries.
* `Hidden`&mdash;The scroll buttons are not displayed.

````RAZOR
<SunfishTabStrip OverflowMode="@TabStripOverflowMode.Scroll"
                 ScrollButtonsVisibility="@TabStripScrollButtonsVisibility.Auto"
                 Width="30vw">
    <TabStripTab Title="First">
        First tab content.
    </TabStripTab>
    <TabStripTab Title="Second">
        Second tab content.
    </TabStripTab>
    <TabStripTab Title="Third">
        Third tab content.
    </TabStripTab>
    <TabStripTab Title="Fourth">
        Fourth tab content.
    </TabStripTab>
    <TabStripTab Title="Fifth">
        Fifth tab content.
    </TabStripTab>
</SunfishTabStrip>
````

### Scroll Buttons Position

By default, the TabStrip renders its scroll buttons on both ends of the tab list. The `ScrollButtonsPosition` parameter accepts the following values of type `TabStripScrollButtonsPosition`:

* `Split` (default)&mdash;The scroll buttons are rendered on both sides of the tab list.
* `Start`&mdash;The scroll buttons are rendered before the tab list.
* `End`&mdash;The scroll buttons are rendered after the tab list.

````RAZOR
<SunfishTabStrip OverflowMode="@TabStripOverflowMode.Scroll"
                 ScrollButtonsPosition="@TabStripScrollButtonsPosition.Start"
                 Width="280px">
    <TabStripTab Title="First">
        First tab content.
    </TabStripTab>
    <TabStripTab Title="Second">
        Second tab content.
    </TabStripTab>
    <TabStripTab Title="Third">
        Third tab content.
    </TabStripTab>
    <TabStripTab Title="Fourth">
        Fourth tab content.
    </TabStripTab>
    <TabStripTab Title="Fifth">
        Fifth tab content.
    </TabStripTab>
</SunfishTabStrip>
````

## Menu Mode

Set `OverflowMode` to `TabStripOverflowMode.Menu` to collect overflowing tabs into a dropdown menu. The menu button appears after the tab list and lists all tabs that do not fit in the available space. Users can click a menu item to activate the corresponding tab.

>caption TabStrip with menu overflow

````RAZOR
<SunfishTabStrip OverflowMode="@TabStripOverflowMode.Menu"
                 Width="300px">
    @{
        for (int i = 1; i <= 10; i++)
        {
            var index = i;
            <TabStripTab Title="@("Tab" + index)" @key="@index">
                Tab @index content.
            </TabStripTab>
        }
    }
</SunfishTabStrip>
````

### TabStripSuffixTemplate

By default, the overflow menu button is rendered automatically after the tab list. Use the `TabStripSuffixTemplate` to add custom content after the tab list—for example, action buttons or additional navigation elements.

When you define a `TabStripSuffixTemplate`, the automatic overflow menu button is no longer rendered. You have to explicitly include the `TabStripOverflowMenu` component inside your template to preserve the overflow menu functionality.

>caption TabStrip suffix template with a custom overflow menu button

````RAZOR
<SunfishTabStrip OverflowMode="@TabStripOverflowMode.Menu"
                 Width="300px">
    <TabStripSuffixTemplate>
        @{
            for (int i = 1; i <= 10; i++)
            {
                var index = i;
                <TabStripTab Title="@("Tab" + @index)" @key="@index">
                    Tab @index content.
                </TabStripTab>
            }
        }
        <TabStripOverflowMenu Icon="@SvgIcon.Menu"
                              ThemeColor="@ThemeConstants.Button.ThemeColor.Primary"
                              FillMode="@ThemeConstants.Button.FillMode.Outline" />
        <SunfishButton Icon="@SvgIcon.Gear" FillMode="@ThemeConstants.Button.FillMode.Flat" />
    </TabStripSuffixTemplate>
</SunfishTabStrip>
````

### TabStripOverflowMenu

The `TabStripOverflowMenu` is a tool component that appears as a dropdown button. It collects tabs that do not fit the visible tab list and lets users activate them from the menu. By default, this component is rendered automatically inside the `TabStripSuffixTemplate` when `OverflowMode` is set to `TabStripOverflowMode.Menu`.

The following parameters allow you to customize the appearance of the overflow menu button:

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Parameter | Type | Description |
| --- | --- | --- |
| `Id` | `string` | The `id` HTML attribute of the component. |
| `Class` | `string` | The CSS class rendered on the component. |
| `Title` | `string` | The `title` HTML attribute of the component. |
| `Icon` | `object` | The icon displayed on the menu button. |
| `ThemeColor` | `string` | The theme color of the menu button. |
| `Size` | `string` | The size of the menu button. |
| `Rounded` | `string` | The border-radius of the menu button. |
| `FillMode` | `string` | The fill mode of the menu button. |

## See Also

* [Live Demo: TabStrip - Scrollable Tabs](https://demos.sunfish.dev/blazor-ui/tabstrip/scrollable-tabs)
* [TabStrip Overview](slug:components/tabstrip/overview)


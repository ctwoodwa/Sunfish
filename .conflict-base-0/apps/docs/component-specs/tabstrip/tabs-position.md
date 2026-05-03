---
title: Tabs Position
page_title: TabStrip Tabs Position
description: The TabPosition parameter in the TabStrip component allows you to control the positioning of the tabs. By default, tabs are positioned at the top of the TabStrip.
slug: tabstrip-tabs-position
tags: sunfish,blazor,tab,strip,tabstrip,position
published: True
position: 10
components: ["tabstrip"]
---
# TabStrip Tabs Position

By default, the tab titles display on top of the tab content.

You can customize their position through the optional `TabPosition` attribute of the `SunfishTabStrip` tag. It takes a member of the `Sunfish.Blazor.TabPosition` enumeration:

* `Top` (default)
* `Left`
* `Right`
* `Bottom`

>caption Set the desired tab position.

````RAZOR
<SunfishTabStrip TabPosition="@TabPosition.Bottom">
    <TabStripTab Title="First">
        First tab content.
    </TabStripTab>
    <TabStripTab Title="Second">
        Second tab content.        
    </TabStripTab>
    <TabStripTab Title="Third">
        Third tab content.
    </TabStripTab>
</SunfishTabStrip>
````

## See Also

  * [Live Demo: TabStrip - Tabs Position and Alignment](https://demos.sunfish.dev/blazor-ui/tabstrip/tab-positions)
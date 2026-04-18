---
title: Active Tab Index
page_title: TabStrip Active Tab Index
description: TabStrip Active Tab Index
slug: tabstrip-activetabindex
tags: sunfish,blazor,tab,strip,tabstrip,active,tab,index
published: True
position: 5
components: ["tabstrip"]
---
# TabStrip Active Tab Index

The TabStrip allows you to get or set the index of the currently selected (active) tab through the `ActiveTabIndex` parameter. It supports two-way binding (the `@bind-ActiveTabIndex` syntax) and one-way binding used along with the [`ActiveTabIndexChanged` event](slug:tabstrip-events#activetabindexchanged).

To deactivate all tabs, set the `ActiveTabIndex` parameter to `-1`.

>caption Get and set the selected tab index

````RAZOR
Active Tab Index: @ActiveTabIndex

<SunfishTabStrip @bind-ActiveTabIndex="@ActiveTabIndex">
	<TabStripTab Title="First">
		First tab content.
	</TabStripTab>
	<TabStripTab Title="Second">
		Second tab content. I will be active initially due to the default value of the parameter.
        <br />
        <SunfishButton OnClick="@SelectThirdTab">Select Third Tab</SunfishButton>
	</TabStripTab>
	<TabStripTab Title="Third">
		Third tab content.
	</TabStripTab>
</SunfishTabStrip>

@code {
	public int ActiveTabIndex { get; set; } = 1;

    void SelectThirdTab()
    {
        ActiveTabIndex = 2;
    }
}
````

> The `ActiveTabIndexChanged` event and `ActiveTabIndex` parameter will be deprecated in a future releases. It is recommended to use the [`ActiveTabId`](slug:tabstrip-tabs-collection) parameter with [`ActiveTabIdChanged`](slug:tabstrip-events#activetabidchanged) event instead.

## See Also

  * [Live Demo: TabStrip](https://demos.sunfish.dev/blazor-ui/tabstrip/overview)


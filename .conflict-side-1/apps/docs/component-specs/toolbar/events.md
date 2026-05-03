---
title: Events
page_title: ToolBar - Events
description: Events in the Sunfish ToolBar for Blazor
slug: toolbar-events
tags: sunfish,blazor,toolbar,events
published: True
position: 25
components: ["toolbar"]
---
# Events

The ToolBar exposes events for its [built-in buttons](slug:toolbar-built-in-tools) which allow you to react when the user perform actions on them. Such actions would be a click event or changing the selected state for the toggle button. The available events are:

* [OnClick](#onclick)
* [SelectedChanged](#selectedchanged)

For [custom (templated) items](slug:toolbar-templated-item), handle the corresponding events exposed by the components/elements you place inside them.

## OnClick

The `OnClick` event fires when the user clicks on a button in the ToolBar (also applicable to toggle buttons).

>caption The OnClick event for the ToolBar buttons

````RAZOR
@*When clicking on the button a message will be printed in your console*@

<SunfishToolBar>
    <ToolBarButton Icon="@SvgIcon.Envelope" OnClick="@OnHyperlinkClick">Hyperlink</ToolBarButton>
</SunfishToolBar>


@code {
    public void OnHyperlinkClick()
    {
        Console.WriteLine("The user clicked on the hyperlink button");
    }
}
````

## SelectedChanged

The `SelectedChanged` event will fire when the user changes the state of the [`ToolBarToggleButton`](slug:toolbar-built-in-tools#toolbartogglebutton). It is used for one-way data binding of the `Selected` parameter and will prevent you from using two-way data binding (`@bind-Selected`)

>caption The SelectedChanged event for the ToolBarToggleButton

````RAZOR
@*Handle the SelectedChangedEvent*@

<SunfishToolBar>
    <ToolBarToggleButton Selected="@Selected" SelectedChanged="@SelectedChangedHandler">Select me</ToolBarToggleButton>

</SunfishToolBar>


@code {
    public bool Selected { get; set; }

    public void SelectedChangedHandler(bool value)
    {
        Selected = value;

        //your application logic regarding the change of the value
    }
}
````

## See Also

  * [Live Demo: ToolBar Overview](https://demos.sunfish.dev/blazor-ui/toolbar/overview)
  * [ToolBar Overview](slug:toolbar-overview)

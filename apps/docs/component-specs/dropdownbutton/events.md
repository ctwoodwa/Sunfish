---
title: Events
page_title: DropDownButton - Events
description: Explore the events that the DropDownButton for Blazor fires. See how you can handle the OnClick of the primary button and the secondary items to respond to the user action.
slug: dropdownbutton-events
tags: sunfish,blazor,dropdownbutton,events
published: True
position: 15
components: ["dropdownbutton"]
---
# DropDownButton Events

The DropDownButton exposes an `OnClick` event that you can use to initiate an action within the application.

The `OnClick` event fires when the user clicks or taps the primary button or a secondary button. The primary `<SunfishDropDownButton>` and each of its `<DropDownButtonItem>` instances execute a separate `OnClick` handler.

The event argument type is [`MouseEventArgs`](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.web.mouseeventargs).
 
The event handler can be synchronous (`void`) or asynchronous (`async Task`).

>caption Handling the DropDownButton OnClick event

````RAZOR
@clickedItemInfo
<br />
@moreInfo

<br />
<SunfishDropDownButton Icon="@SvgIcon.Clipboard" OnClick="@((args) => OnItemClick("Primary", args))">
    <DropDownButtonContent>Paste</DropDownButtonContent>

    <DropDownButtonItems>
        <DropDownButtonItem Icon="@SvgIcon.ClipboardText" OnClick="@((args) => OnItemClick("Paste Text", args))">Paste Text</DropDownButtonItem>
        <DropDownButtonItem Icon="@SvgIcon.ClipboardCode" OnClick="@((args) => OnItemClick("Paste as HTML", args))">Paste as HTML</DropDownButtonItem>
        <DropDownButtonItem Icon="@SvgIcon.ClipboardMarkdown" OnClick="@((args) => OnItemClick("Paste Markdown", args))">Paste Markdown</DropDownButtonItem>
        <DropDownButtonItem OnClick="@((args) => OnItemClick("Set Default Paste", args))">Set Default Paste</DropDownButtonItem>
    </DropDownButtonItems>

</SunfishDropDownButton>

@code {
    private string clickedItemInfo;

    private string moreInfo;

    private void OnItemClick(string item, MouseEventArgs args)
    {
        clickedItemInfo = $"User clicked the {item} option.";

        moreInfo = "Ctrl was pressed when clicking the primary button: " + args.CtrlKey;
    }
}
````


## See Also

* [DropDownButton API](slug:Sunfish.Blazor.Components.SunfishDropDownButton)
* [Live Demo: DropDownButton](https://demos.sunfish.dev/blazor-ui/dropdownbutton/overview)

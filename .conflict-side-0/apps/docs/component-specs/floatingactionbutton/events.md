---
title: Events
page_title: Floating Action Button - Events
description: Events of the Floating Action Button for Blazor.
slug: fab-events
tags: sunfish,blazor,floating action button,events
published: True
position: 3
components: ["floatingactionbutton"]
---
# Button Events

This article explains the events available in the Sunfish Floating Action Button for Blazor:

* [OnClick](#onclick)

## OnClick 

The `OnClick` event fires when the user clicks or taps the button.

It receives argument of type [MouseEventArgs](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.web.mouseeventargs?view=aspnetcore-5.0).

>caption Handle the button click

````RAZOR
<SunfishFloatingActionButton Id="floating-action-button"
                             Icon="@((IsPopupVisible ? SvgIcon.Minus : SvgIcon.Plus))"
                             VerticalAlign="@FloatingActionButtonVerticalAlign.Top"
                             HorizontalAlign="@FloatingActionButtonHorizontalAlign.Center"
                             OnClick="@TogglePopup" />

<SunfishPopup @ref="@PopupRef"
              AnchorSelector="#floating-action-button"
              AnimationType="@AnimationType.SlideDown"
              AnimationDuration="200">
    <div class="k-d-flex k-flex-col k-gap-1.5 k-p-1.5 k-flex-wrap k-align-content-around">
        <SunfishButton Icon="@SvgIcon.Download"
                       Rounded="@ThemeConstants.Button.Rounded.Full"
                       FillMode="@ThemeConstants.Button.FillMode.Flat"
                       ThemeColor="@ThemeConstants.Button.ThemeColor.Info"
                       Size="@ThemeConstants.Button.Size.Large"
                       Title="Download" />
        <SunfishButton Icon="@SvgIcon.Trash"
                       Rounded="@ThemeConstants.Button.Rounded.Full"
                       FillMode="@ThemeConstants.Button.FillMode.Flat"
                       ThemeColor="@ThemeConstants.Button.ThemeColor.Error"
                       Size="@ThemeConstants.Button.Size.Large"
                       Title="Delete" />
        <SunfishButton Icon="@SvgIcon.Upload"
                       Rounded="@ThemeConstants.Button.Rounded.Full"
                       FillMode="@ThemeConstants.Button.FillMode.Flat"
                       ThemeColor="@ThemeConstants.Button.ThemeColor.Success"
                       Size="@ThemeConstants.Button.Size.Large"
                       Title="Upload" />
    </div>
</SunfishPopup>

<style>
    .k-popup {
        border-radius: 23px;
    }
</style>

@code {
    private bool IsPopupVisible { get; set; }
    private SunfishPopup? PopupRef { get; set; }

    private void TogglePopup()
    {
        if (IsPopupVisible)
        {
            PopupRef?.Hide();
        }
        else
        {
            PopupRef?.Show();
        }

        IsPopupVisible = !IsPopupVisible;
    }
}
````

---
title: Events
page_title: Dialog - Events
description: Events of the Dialog for Blazor.
slug: dialog-events
tags: sunfish,blazor,dialog,events
published: True
position: 10
components: ["dialog"]
---
# Dialog Events

This article explains the events available in the Sunfish Dialog for Blazor:

* [VisibleChanged](#visiblechanged)

@[template](/_contentTemplates/common/general-info.md#event-callback-can-be-async) 


## VisibleChanged

You can use the `VisibleChanged` event to get notifications when the user tries to close the Dialog. You can effectively cancel the event by *not* propagating the new visibility state to the variable the `Visible` property is bound to. This is the way to cancel the event and keep the dialog open.

>caption React to the user closing the Dialog.

````RAZOR
@* An example with the VisibleChanged event. *@

@result

<SunfishButton OnClick="@ToggleDialog">Toggle the Dialog</SunfishButton>

<SunfishDialog Visible="@Visible" VisibleChanged="@VisibleChangedHandler"
               Title="@Title">
    <DialogContent>
        A new version of <strong>Sunfish UI for Blazor</strong> is available. Would you like to download and install it now?
    </DialogContent>
    <DialogButtons>
        <SunfishButton OnClick="@ToggleDialog">Skip this version</SunfishButton>
        <SunfishButton OnClick="@ToggleDialog">Remind me later</SunfishButton>
        <SunfishButton OnClick="@ToggleDialog" ThemeColor="primary">Install update</SunfishButton>
    </DialogButtons>
</SunfishDialog>

@code {
    private bool Visible { get; set; } = true;
    private string Title { get; set; } = "Software Update";
    string result { get; set; }

    void VisibleChangedHandler(bool currVisible)
    {
        Visible = currVisible; // If you don't do this, the Dialog won't close because of the user action

        result = $"The Dialog is now visible: {Visible}";

        Console.WriteLine("The user closed the Dialog with the [x] button on its toolbar");
    }

    public void ToggleDialog()
    {
        Visible = !Visible;

        result = $"The Dialog is now visible: {Visible}";
    }
}
````

>caption Prevent the user from closing the Dialog based on a condition.

````RAZOR
@* Not propagating the visible value from the handler to the model can prevent the user from closing the Dialog.
    Using the application code to explicitly set the visibility of the Dialog will still close it as it will not fire the event.*@

<SunfishButton OnClick="@(() => { Visible = true; })">Toggle the Dialog</SunfishButton>

<SunfishDialog Visible="@Visible" VisibleChanged="@VisibleChangedHandler"
               Title="@Title">
    <DialogContent>
        Try closing the Dialog with the [x] button on its toolbar, then toggle the checkbox and try again.
        <br />
        <label>
            The user can close the dialog with the [x] button:
            <SunfishCheckBox @bind-Value="@isClosable" />
        </label>
    </DialogContent>
</SunfishDialog>

@code {
    private bool Visible { get; set; } = true;
    private string Title { get; set; } = "Closable Dialog";
    bool isClosable { get; set; }

    void VisibleChangedHandler(bool currVisible)
    {
        if (isClosable)
        {
            Visible = currVisible; // If you don't do this, the Dialog won't close because of the user action.
        }
        else
        {
            Console.WriteLine("The user tried to close the Dialog but the code didn't let them.");
        }

    }
}
````


## See Also

* [Focus TextBox on Dialog Open](slug:window-kb-focus-button-textbox-on-open)

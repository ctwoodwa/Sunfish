---
title: Events
page_title: Events - ColorGradient for Blazor
description: Events in the ColorGradient for Blazor.
slug: colorgradient-events
tags: sunfish,blazor,colorgradient,events
published: true
position: 10
components: ["colorgradient"]
---
# ColorGradient Events

This article describes the available events of the Sunfish ColorGradient for Blazor.

* [FormatChanged](#formatchanged)
* [ValueChanged](#valuechanged)


## FormatChanged

The `FormatChanged` event fires when the user clicks on the toggle button, which changes the input format. The event can help you persist the selected `Format` at a later stage.

When using this event, make sure to update the component `Format` programmatically in the event handler.

>caption Handle the ColorGradient FormatChanged event

````RAZOR
@* Handle the ColorGradient FormatChanged event *@

<SunfishColorGradient
    @bind-Value="@Value"
    Format="@Format"
    FormatChanged="@FormatChangedHandler" />

@code {
    string Value { get; set; }
    ColorFormat Format { get; set; }

    async Task FormatChangedHandler(ColorFormat newFormat)
    {
        Format = newFormat;
    }
}
````

## ValueChanged

The `ValueChanged` event fires continuously while the user is dragging the component handles, or changing the textbox values.

When using this event, make sure to update the component `Value` programmatically in the event handler.

>caption Handle the ColorGradient ValueChanged event

````RAZOR
@* Handle the ColorGradient ValueChanged event *@

<SunfishColorGradient
    Value="@Value"
    ValueChanged="@ValueChangedHandler" />

@code {
    string Value { get; set; }

    async Task ValueChangedHandler(string newValue)
    {
        Value = newValue;
    }
}

````


## See Also

* [ColorGradient Overview](slug:colorgradient-overview)
* [ColorGradient Live Demo](https://demos.sunfish.dev/blazor-ui/colorgradient/overview)

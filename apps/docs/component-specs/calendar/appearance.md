---
title: Appearance
page_title: Calendar Appearance
description: Appearance settings of the Calendar for Blazor.
slug: calendar-appearance
tags: sunfish,blazor,calendar,appearance
published: True
position: 30
components: ["calendar"]
---
# Appearance Settings

This article outlines the available Calendar parameters, which control its appearance.

## Size

You can increase or decrease the size of the Calendar by setting the `Size` attribute to a member of the `Sunfish.Blazor.ThemeConstants.Calendar.Size` class:

| Class member | Manual declaration |
|---|---|
|`Small` |`sm`|
|`Medium`|`md`|
|`Large`|`lg`|

This configuration affects the size of the whole Calendar and its inner elements - header, navigation buttons, cells. The elements' size, padding and font-size vary depending on the selected Calendar size.

>caption The built-in Calendar sizes

````RAZOR
@{
    var fields = typeof(Sunfish.Blazor.ThemeConstants.Calendar.Size)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string size = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishCalendar @bind-Value="@DateValue" Size="@size"></SunfishCalendar>
        </div>
    }
}

@code {
    private DateTime DateValue { get; set; } = DateTime.Now;
}
````

@[template](/_contentTemplates/common/themebuilder-section.md#appearance-themebuilder)

## See Also

* [Live Demo: Calendar - Appearance](https://demos.sunfish.dev/blazor-ui/calendar/appearance)

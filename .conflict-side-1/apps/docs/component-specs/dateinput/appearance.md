---
title: Appearance
page_title: DateInput Appearance
description: Appearance settings of the DateInput for Blazor.
slug: dateinput-appearance
tags: sunfish,blazor,dateinput,appearance
published: True
position: 35
components: ["dateinput"]
---
# Appearance Settings

You can control the appearance of the DateInput by setting the following attribute:

* [Size](#size)
* [Rounded](#rounded)
* [FillMode](#fillmode)


## Size

You can increase or decrease the size of the DateInput by setting the `Size` attribute to a member of the `Sunfish.Blazor.ThemeConstants.DateInput.Size` class:

| Class members | Manual declarations |
|------------|--------|
|`Small` |`sm`|
|`Medium`|`md`|
|`Large`|`lg`|

>caption The built-in sizes

````RAZOR
@{
    var fields = typeof(Sunfish.Blazor.ThemeConstants.DateInput.Size)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string size = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishDateInput @bind-Value="@DateValue" Size="@size"></SunfishDateInput>
        </div>
    }
}

@code {
    private DateTime DateValue { get; set; } = DateTime.Now;
}
````

## Rounded

The `Rounded` attribute applies the `border-radius` CSS rule to the DateInput to achieve curving of the edges. You can set it to a member of the `Sunfish.Blazor.ThemeConstants.DateInput.Rounded` class:

| Class members | Manual declarations |
|------------|--------|
|`Small` |`sm`|
|`Medium`|`md`|
|`Large`|`lg`|
|`Full`|`full`|

>caption The built-in values of the Rounded attribute

````RAZOR
@* The built-in values of the Rounded attribute.  *@

@{
    var fields = typeof(Sunfish.Blazor.ThemeConstants.DateInput.Rounded)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string rounded = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishDateInput @bind-Value="@DateValue" Rounded="@rounded"></SunfishDateInput>
        </div>
    }
}

@code {
    private DateTime DateValue { get; set; } = DateTime.Now;
} 
````

## FillMode

The `FillMode` controls how the SunfishDateInput is filled. You can set it to a member of the `Sunfish.Blazor.ThemeConstants.DateInput.FillMode` class:

| Class members | Result |
|------------|--------|
|`Solid` <br /> default value|`solid`|
|`Flat`|`flat`|
|`Outline`|`outline`|

>caption The built-in Fill modes

````RAZOR
@* These are all built-in fill modes *@

@{
    var fields = typeof(Sunfish.Blazor.ThemeConstants.DateInput.FillMode)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string fillmode = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishDateInput @bind-Value="@DateValue" FillMode="@fillmode"></SunfishDateInput>
        </div>
    }
}

@code {
    private DateTime DateValue { get; set; } = DateTime.Now;
}
````

@[template](/_contentTemplates/common/themebuilder-section.md#appearance-themebuilder)

## See Also

* [Live Demo: DateInput - Appearance](https://demos.sunfish.dev/blazor-ui/dateinput/appearance)

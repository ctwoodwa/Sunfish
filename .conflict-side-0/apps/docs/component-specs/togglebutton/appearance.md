---
title: Appearance
page_title: ToggleButton Appearance
description: Appearance settings of the ToggleButton for Blazor.
slug: togglebutton-appearance
tags: sunfish,blazor,button,toggle,togglebutton,appearance
published: True
position: 35
components: ["togglebutton"]
---
# Appearance Settings

You can control the appearance of the toggle button by setting the following attributes:

* [FillMode](#fillmode)
* [Rounded](#rounded)
* [Size](#size)
* [ThemeColor](#themecolor)

You can use all of them together to achieve the desired appearance. This article will explain their effect one by one.

## FillMode

The `FillMode` toggles the background and border of the SunfishToggleButton. You can set the parameter to a member of the `Sunfish.Blazor.ThemeConstants.Button.FillMode` class:

| Class members | Manual declarations |
|------------|--------|
|`Solid` <br /> default value|`solid`|
|`Flat`|`flat`|
|`Outline`|`outline`|
|`Link`|`link`|
|`Clear`|`clear`|

>caption The built-in Fill modes

````RAZOR
@* These are all built-in fill modes *@

@{ 
    var fields = typeof(Sunfish.Blazor.ThemeConstants.Button.FillMode)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string fillmode = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishToggleButton FillMode="@fillmode">@fillmode</SunfishToggleButton>
        </div>
    }
}
````

## Rounded

The `Rounded` parameter applies the `border-radius` CSS rule to the button to achieve curving of the edges. You can set it to a member of the `Sunfish.Blazor.ThemeConstants.Button.Rounded` class:

| Class members | Manual declarations |
|------------|--------|
|`Small` |`sm`|
|`Medium`|`md`|
|`Large`|`lg`|
|`Full`|`full`|

>caption The built-in values of the Rounded attribute

````RAZOR
@* The built-in rounded edges of the button.  *@

@{
    var fields = typeof(Sunfish.Blazor.ThemeConstants.Button.Rounded)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string rounded = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishToggleButton Rounded="@rounded">@rounded</SunfishToggleButton>
        </div>
    }
}
````

## Size

You can increase or decrease the size of the button by setting the `Size` parameter to a member of the `Sunfish.Blazor.ThemeConstants.Button.Size` class:

| Class members | Manual declarations |
|---------------|--------|
| `Small`   |`sm`|
| `Medium`   |`md`|
| `Large`   |`lg`|

>caption The built-in button sizes

````RAZOR
@{
    var fields = typeof(Sunfish.Blazor.ThemeConstants.Button.Size)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string size = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishToggleButton Size="@size">@size</SunfishToggleButton>
        </div>
    }
}
````

## ThemeColor

The color of the button is controlled through the `ThemeColor` parameter. You can set it to a member of the `Sunfish.Blazor.ThemeConstants.Button.ThemeColor` class:

| Class members | Manual declarations |
|------------|--------|
|`Base` <br /> default value |`base`|
|`Primary`|`primary`|
|`Secondary`|`secondary`|
|`Tertiary`|`tertiary`|
|`Info`|`info`|
|`Success`|`success`|
|`Warning`|`warning`|
|`Error`|`error`|
|`Dark`|`dark`|
|`Light`|`light`|
|`Inverse`|`inverse`|


>caption The built-in ThemeColors

````RAZOR
@* The built-in button colors *@

@{
    var fields = typeof(Sunfish.Blazor.ThemeConstants.Button.ThemeColor)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string themeColor = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishToggleButton ThemeColor="@themeColor">@themeColor</SunfishToggleButton>
        </div>
    }
}
````

@[template](/_contentTemplates/common/themebuilder-section.md#appearance-themebuilder)

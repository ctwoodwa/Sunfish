---
title: Appearance
page_title: ComboBox Appearance
description: Appearance settings of the ComboBox for Blazor.
slug: combobox-appearance
tags: sunfish,blazor,combobox,appearance
published: True
position: 65
components: ["combobox"]
---
# Appearance Settings

You can control the appearance of the ComboBox by setting the following attribute:

* [Size](#size)
* [Rounded](#rounded)
* [FillMode](#fillmode)


## Size

You can increase or decrease the size of the ComboBox by setting the `Size` attribute to a member of the `Sunfish.Blazor.ThemeConstants.ComboBox.Size` class:

| Class members | Manual declarations |
|------------|--------|
|`Small` |`sm`|
|`Medium`|`md`|
|`Large`|`lg`|

>caption The built-in sizes

````RAZOR
@{
    var fields = typeof(Sunfish.Blazor.ThemeConstants.ComboBox.Size)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string size = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishComboBox Data="@myComboData"
                             Size="@size"
                             TextField="MyTextField" 
                             ValueField="MyValueField" 
                             @bind-Value="selectedValue"
                             Placeholder="Select an item..." 
                             ShowClearButton="true" 
                             Filterable="true">
            </SunfishComboBox>
        </div>
    }
}

@code {
    IEnumerable<MyDdlModel> myComboData = Enumerable.Range(1, 20).Select(x => new MyDdlModel { MyTextField = "item " + x, MyValueField = x });

    int selectedValue { get; set; }

    protected override void OnInitialized()
    {
        selectedValue = 3;
    }

    public class MyDdlModel
    {
        public int MyValueField { get; set; }
        public string MyTextField { get; set; }
    }
}
````

## Rounded

The `Rounded` attribute applies the `border-radius` CSS rule to the ComboBox to achieve curving of the edges. You can set it to a member of the `Sunfish.Blazor.ThemeConstants.ComboBox.Rounded` class:

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
    var fields = typeof(Sunfish.Blazor.ThemeConstants.ComboBox.Rounded)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string rounded = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishComboBox Data="@myComboData"
                             Rounded="@rounded"
                             TextField="MyTextField" 
                             ValueField="MyValueField" 
                             @bind-Value="selectedValue"
                             Placeholder="Select an item..." 
                             ShowClearButton="true" 
                             Filterable="true">
            </SunfishComboBox>
        </div>
    }
}

@code {
    IEnumerable<MyDdlModel> myComboData = Enumerable.Range(1, 20).Select(x => new MyDdlModel { MyTextField = "item " + x, MyValueField = x });

    int selectedValue { get; set; }

    protected override void OnInitialized()
    {
        selectedValue = 3;
    }

    public class MyDdlModel
    {
        public int MyValueField { get; set; }
        public string MyTextField { get; set; }
    }
}
````

## FillMode

The `FillMode` controls how the SunfishComboBox is filled. You can set it to a member of the `Sunfish.Blazor.ThemeConstants.ComboBox.FillMode` class:

| Class members | Result |
|------------|--------|
|`Solid` <br /> default value|`solid`|
|`Flat`|`flat`|
|`Outline`|`outline`|

>caption The built-in Fill modes

````RAZOR
@* These are all built-in fill modes *@

@{
    var fields = typeof(Sunfish.Blazor.ThemeConstants.ComboBox.FillMode)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string fillMode = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishComboBox Data="@myComboData"
                             FillMode="@fillMode"
                             TextField="MyTextField" 
                             ValueField="MyValueField" 
                             @bind-Value="selectedValue"
                             Placeholder="Select an item..." 
                             ShowClearButton="true" 
                             Filterable="true">
            </SunfishComboBox>
        </div>
    }
}

@code {
    IEnumerable<MyDdlModel> myComboData = Enumerable.Range(1, 20).Select(x => new MyDdlModel { MyTextField = "item " + x, MyValueField = x });

    int selectedValue { get; set; }

    protected override void OnInitialized()
    {
        selectedValue = 3;
    }

    public class MyDdlModel
    {
        public int MyValueField { get; set; }
        public string MyTextField { get; set; }
    }
}
````

@[template](/_contentTemplates/common/themebuilder-section.md#appearance-themebuilder)

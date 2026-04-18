---
title: Overview
page_title: DropDownList Overview
description: The Blazor DropDownList allows users to select an option from a list, enabling dynamic data binding and event handling in web apps.
slug: components/dropdownlist/overview
tags: sunfish,blazor,dropdownlist,dropdown,list,overview
published: True
hideCta: True
position: 0
components: ["dropdownlist"]
---
# Blazor DropDownList Overview

The Blazor DropDownList component allows the user to choose an option from a predefined set of choices presented in a dropdown list popup. The developer can control the [data](slug:components/dropdownlist/databind), sizes, and various appearance options like class and [templates](slug:components/dropdownlist/templates).

<span class="cta-panel-big-module--container--c08a9 d-print-none "><span class="row align-items-center justify-content-center cta-panel-big-module--row--9b71a"><span class="col-auto"><img class="cta-panel-big-module--icon--a648c" src="/images/avatar-ninja.svg" alt="ninja-icon"></span><span class="col-12 col-sm"><span class="cta-panel-big-module--message--40a0f">Tired of reading docs? With our new AI Coding Assistants, you can add, configure, and troubleshoot Sunfish UI for Blazor components—right inside your favorite AI-powered IDE: Visual Studio, VS Code, Cursor, and more. Start building faster, smarter, and with contextual intelligence powered by our docs/APIs:</span></span><span class="col-12 col-lg-auto"><a class="cta-panel-big-module--btnTrial--38b3e" href="https://sunfish.dev/blazor-ui/documentation/ai/overview?utm_source=ai-assistants-docs" target="_blank">Try AI Assistants</a></span></span></span>

## Creating Blazor DropDownList

1. Use the `SunfishDropDownList` tag to add the Blazor dropdown list to your razor page.
1. Populate its `Data` property with the collection of items you want to appear in the dropdown list.
1. Set the `TextField` and `ValueField` properties to point to the corresponding names of the model.
1. [Bind the value of the component](slug:get-started-value-vs-data-binding#value-binding) to a variable of the same type as the type defined in the `ValueField` parameter.
1. (optional) Set the `Value` property to the initial value of the model.

>caption DropDownList [data binding](slug:components/dropdownlist/databind), two-way value binding, and main features


## Data Binding


## Filtering


## Grouping


## Templates

@[template](/_contentTemplates/dropdowns/features.md#templates) [Read more about the Blazor DropDownList templates...](slug:components/dropdownlist/templates).

## Validation

@[template](/_contentTemplates/dropdowns/features.md#validation)

## Virtualization

@[template](/_contentTemplates/dropdowns/features.md#virtualization) [Read more about the Blazor DropDownList virtualization...](slug:dropdownlist-virtualization)

## Adaptive Rendering


@[template](/_contentTemplates/dropdowns/adaptive-rendering.md#intro)

## DropDownList Parameters

>caption The Blazor dropdown list provides various parameters that allow you to configure the component:

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Parameter      | Type | Description
| ----------- | ----------- | -----------|
| `AdaptiveMode` | `AdaptiveMode` <br /> (`None`) | The [adaptive mode](slug:adaptive-rendering) of the component. |
| `Data` | `IEnumerable<TItem>` | Allows you to provide the data source. Required. |
| `DefaultText` | `string` | Simple hint to be displayed when no item is selected yet. In order for it to be shown, the `Value` parameter should be set to a default value depending on the type defined in the `ValueField` parameter. For example, `0` for an `int`, and `null` for an `int?` or `string`. You need to make sure that it does not match the value of an existing item in the data source. See the first example in the [Examples section](#examples) in this article and in the [Input Validation](slug:common-features/input-validation#dropdownlist) article. |
| `Enabled` | `bool` | Whether the component is enabled. |
| `ReadOnly` | `bool` | If set to `true`, the component will be readonly and will not allow user input. The component is not readonly by default and allows user input. |
|`Filterable` | `bool` | Whether [filtering](slug:components/dropdownlist/filter) is enabled for the end user. |
| `FilterDebounceDelay` | `int` <br/> 150 | Time in milliseconds between the last typed symbol and the filter input value update. Applicable to filtering only. Use it to balance between client-side performance and number of database queries. |
| `FilterOperator` | `StringFilterOperator` <br /> (`StartsWith`)| The method of [filtering](slug:components/dropdownlist/filter) the items. |
| `FilterPlaceholder` |  `string` | The hint that will be displayed in the filter input when it has no value.
| `Id` | `string` | The `id` attribute rendered on the main wrapping element of the component (`<span class="k-dropdownlist">`). You can use it to attach a `<label for="">` to it. |
| `InputMode` | `string` | The [`inputmode` attribute](https://developer.mozilla.org/en-US/docs/Web/HTML/Global_attributes/inputmode) of the `<input />` element. |
| `TItem` | `Type`| The type of the model to which the component is bound. Required if you can't provide `Data` or `Value`. Determines the type of the reference object. |
| `TValue` | `Type` | The type of the value field from the model to which the component is bound. Required if you can't provide `Data` or `Value`. Determines the type of the reference object. The type of the values can be:<br /> - `number` (such as `int`, `double`, and so on)<br /> - `string`<br /> - `Guid`<br /> - `Enum` |
| `Title` | `string` | The title text rendered in the header of the dropdown list popup (action sheet). Applicable only when [`AdaptiveMode` is set to `Auto`](slug:adaptive-rendering). |
| `TabIndex` | `int?` | The `tabindex` attribute rendered on the dropdown list. |
| `TextField` | `string` <br /> (`Text`)| The name of the field from the model that will be shown to the user. |
| `ValueField` | `string` <br /> (`Value`) | The name of the field from the model that will be the underlying `value`. |
|`Value` and `bind-Value`| `TValue` | Gets/sets the value of the component, can be used for binding. If you set it to a value allowed by the model class value field, the corresponding item from the data collection will be pre-selected. Use the `bind-Value` syntax for two-way binding, for example, to a variable of your own. |


### Styling and Appearance

The following parameters enable you to customize the [appearance](slug:dropdownlist-appearance) of the Blazor DropDownList component:

@[template](/_contentTemplates/dropdowns/features.md#styling)

>tip To learn more about the appearance, anatomy, and accessibility of the DropDownList, visit the [Progress Design System Kit documentation](https://sunfish.dev/design-system/docs/components/dropdownlist/)—an information portal offering rich component usage guidelines, descriptions of the available style variables, and globalization support details.

### Popup Settings

The DropDownList exposes settings for its dropdown (popup). To configure the options, declare a  `<DropDownListPopupSettings>` tag inside a `<DropDownListSettings>` tag:

````RAZOR
<SunfishDropDownList Data="@DropDownData"
                     @bind-Value="@SelectedItem"
                     Filterable="true"
                     FilterOperator="@StringFilterOperator.Contains"
                     FilterPlaceholder="Filter by digit or letter"
                     Width="240px">
    <DropDownListSettings>
        <DropDownListPopupSettings Height="auto" MaxHeight="200px" MinHeight="75px" />
    </DropDownListSettings>
</SunfishDropDownList>

@code {
    private List<string> DropDownData { get; set; } = Enumerable.Range(1, 50)
        .Select(x => { return $"Item {x} {(char)Random.Shared.Next(65, 91)}{(char)Random.Shared.Next(65, 91)}"; })
        .ToList();

    private string SelectedItem { get; set; }
}
````

The DropDownList provides the following popup settings:

@[template](/_contentTemplates/dropdowns/features.md#popup-settings)

## DropDownList Reference and Methods

Add a reference to the component instance to use the [DropDownList's methods](slug:Sunfish.Blazor.Components.SunfishDropDownList-2). Note that the [DropDownList is a generic component](slug:common-features-data-binding-overview#component-type).


@[template](/_contentTemplates/dropdowns/methods.md#methods-list)

````RAZOR
<SunfishDropDownList @ref="@DropDownListRef"
                     Data="@DropDownListData"
                     @bind-Value="@DropDownListValue"
                     Width="300px" />

<SunfishButton OnClick="@OpenPopup">Open Popup</SunfishButton>

@code {
    private SunfishDropDownList<string, string> DropDownListRef { get; set; }

    private string DropDownListValue { get; set; }

    private List<string> DropDownListData { get; set; } = new List<string> { "first", "second", "third" };

    private void OpenPopup()
    {
        DropDownListRef.Open();
        
        DropDownListValue = DropDownListData.First();

        DropDownListRef.Refresh();
    }
}
````

## Selected Item and DefaultText

By default, if no `Value` is provided and no `DefaultText` is defined, the DropDownList will appear empty.

* To display `DefaultText` - `Value` should be `0` or `null` depending on the data type you are using in the `ValueField` and the `DefaultText` should be defined.

* To display a selected item when the component renders - provide the `Value` of the desired element. Note that it must match an item of the component's data source.


## Examples

>caption Default text (hint) to show when no actual item is selected

````RAZOR
@MyStringItem
<SunfishDropDownList Data="@MyStringList" @bind-Value="@MyStringItem" DefaultText="Select something">
</SunfishDropDownList>

<br />
<br />

@MyIntItem
<SunfishDropDownList Data="@MyIntList" @bind-Value="@MyIntItem" DefaultText="Select another thing">
</SunfishDropDownList>

@code {
    protected List<string> MyStringList = new List<string>() { "first", "second", "third" };

    //Current value is null (no item is sellected) which allows the DefaultText to be displayed.
    protected string MyStringItem { get; set; }

    protected List<int> MyIntList = new List<int>() { 1, 2, 3 };

    //Current value is 0 (no item is sellected) which allows the DefaultText to be displayed.
    protected int MyIntItem { get; set; }
}
````


>caption Get selected item from external code

````RAZOR
@result
<br />

<SunfishDropDownList Data="@myDdlData" TextField="MyTextField" ValueField="MyValueField"
                     @bind-Value="@DdlValue" DefaultText="Select something">
</SunfishDropDownList>

<SunfishButton OnClick="@GetSelectedItem">Get Selected Item</SunfishButton>

@code {
    string result;
    int DdlValue { get; set; } = 5;
    void GetSelectedItem()
    {
        // extract the data item from the data source by using the value
        MyDdlModel selectedItem = myDdlData.Where(d => d.MyValueField == DdlValue).FirstOrDefault();
        if (selectedItem != null)
        {
            result = selectedItem.MyTextField;
        }
        else
        {
            result = "no item selected";
        }

        StateHasChanged();
    }

    public class MyDdlModel
    {
        public int MyValueField { get; set; }
        public string MyTextField { get; set; }
    }

    IEnumerable<MyDdlModel> myDdlData = Enumerable.Range(1, 20).Select(x => new MyDdlModel { MyTextField = "item " + x, MyValueField = x });
}
````


@[template](/_contentTemplates/common/get-model-from-dropdowns.md#get-model-from-dropdowns)

## Next Steps

* [Binding the DropDownList to Data](slug:components/dropdownlist/databind)
* [Pre-Selecting Items for the User](slug:dropdownlist-pre-select-item)

## See Also

* [Data Binding](slug:components/dropdownlist/databind)
* [DropDownList API Reference](slug:sunfish.blazor.components.sunfishdropdownlist-2)
* [Live Demo: DropDownList](https://demos.sunfish.dev/blazor-ui/dropdownlist/overview)
* [Live Demo: DropDownList Validation](https://demos.sunfish.dev/blazor-ui/dropdownlist/validation)
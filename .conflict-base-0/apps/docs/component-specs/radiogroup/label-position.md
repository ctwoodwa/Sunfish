---
title: Label Position
page_title: RadioGroup Label Position
description: Label Position of the RadioButtonGroup for Blazor.
slug: radiogroup-label-position
tags: sunfish,blazor,radiobuttongroup,radio,list,label,position
published: True
position: 15
components: ["radiogroup"]
---
# RadioGroup Label Position

The Blazor Radio Button Group component lets you render the labels of the radio buttons before or after the buttons themselves.

By default, the labels are after the buttons, and you can change that through the `LabelPosition` parameter that takes a member of the `Sunfish.Blazor.RadioGroupLabelPosition` enum.

>caption Labels before and after the radio buttons

````RAZOR
@* If you want the labels before the buttons, you can set the value directly to the parameter *@

<select @bind="@thePosition">
   <option value="@RadioGroupLabelPosition.Before">Before</option>
   <option value="@RadioGroupLabelPosition.After">After</option>
</select>
<br />
<SunfishRadioGroup Data="@Data" @bind-Value="@SelectedValue" LabelPosition="@thePosition"></SunfishRadioGroup>
@code{
    RadioGroupLabelPosition thePosition { get; set; } = RadioGroupLabelPosition.Before;

    int SelectedValue { get; set; } = 2;
    IEnumerable<int> Data { get; set; } = Enumerable.Range(1, 5);
}
````

![Label Position in the ButtonGroup component](images/radio-group-label-position.gif)


## See Also

* [RadioGroup Overview](slug:radiogroup-overview)
* [RadioGroup Data Binding](slug:radiogroup-databind)
* [Live Demo: RadioGroup Customization](https://demos.sunfish.dev/blazor-ui/radiogroup/customization)

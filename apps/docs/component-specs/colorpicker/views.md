---
title: Views
page_title: Views - ColorPicker for Blazor
description: Views of the ColorPicker for Blazor.
slug: colorpicker-views
tags: sunfish,blazor,colorpicker,views,overview
published: True
position: 0
components: ["colorpicker"]
---
# ColorPicker Views

The ColorPicker popup can display different views. They allow users to select a color from an interface that matches their preferences. The supported views are:

* [ColorPickerGradientView](#colorpickergradientview) - allows unlimited color selection from an [HSVA](https://en.wikipedia.org/wiki/HSL_and_HSV) canvas. This view also renders textboxes for typing a color in [RGB or HEX format](slug:colorpicker-overview#supported-value-formats).
* [ColorPickerPaletteView](#colorpickerpaletteview) - allows color selection from a predefined collection of colors.

By default, both views are enabled and the gradient view is displayed first. Both behaviors can be configured via the ColorPicker [`ColorPickerViews` container and `View` attribute](slug:colorpicker-overview#colorpicker-parameters).

## ColorPickerGradientView

The GradientView uses a [`SunfishColorGradient` component](slug:colorgradient-overview). The following [ColorGradient attributes](slug:colorgradient-overview#colorgradient-parameters) are exposed in the `ColorPickerGradientView` tag:

* `Format`
* `Formats`
* `ShowOpacityEditor`

## ColorPickerPaletteView

The PaletteView uses a [`SunfishColorPalette` component](slug:colorpalette-overview). The following [ColorPalette attributes](slug:colorpalette-overview#colorpalette-parameters) are exposed in the `ColorPickerPaletteView` tag:

* `Columns`
* `Colors`
* `TileWidth`
* `TileHeight`

## Example

````RAZOR
@* Blazor ColorPicker Views *@

<SunfishColorPicker @bind-Value="@Color">
    <ColorPickerViews>
        <ColorPickerGradientView Format="ColorFormat.Hex"
                                 ShowOpacityEditor="true" />
        <ColorPickerPaletteView Colors="ColorPalettePresets.Basic"
                                Columns="5"
                                TileWidth="40px"
                                TileHeight="40px" />
    </ColorPickerViews>
</SunfishColorPicker>

@code {
    string Color { get; set; }
}
````


## See Also

* [ColorPicker Events](slug:colorpicker-events)
* [ColorPicker Views Demo](https://demos.sunfish.dev/blazor-ui/colorpicker/views)

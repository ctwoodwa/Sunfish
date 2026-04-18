---
title: Appearance
page_title: SpeechToTextButton Appearance
description: Customize the appearance of the SpeechToTextButton component in Blazor applications.
slug: speechtotextbutton-appearance
tags: blazor, speech recognition, appearance, customization
published: true
position: 2
components: ["speechtotextbutton"]
---
# SpeechToTextButton Appearance

You can customize the appearance of the SpeechToTextButton component by using its built-in parameters and CSS classes. The component supports the same appearance options as the [Sunfish UI for Blazor Button](slug:components/button/overview).

## Size

You can increase or decrease the size of the button by setting the `Size` parameter to a member of the `Sunfish.Blazor.ThemeConstants.Button.Size` class. 

To review all available values for the `Size` parameter, see the [Button Size API Reference](https://docs.sunfish.dev/blazor-ui/api/Sunfish.Blazor.ThemeConstants.Button.Size.html).

>caption Example of setting the Button Size


## Fill Mode

The `FillMode` toggles the background and border of the SunfishSpeechToTextButton. You can set the parameter to a member of the `Sunfish.Blazor.ThemeConstants.Button.FillMode` class.

To review all available values for the `FillMode` parameter, see the [Button FillMode API Reference](https://docs.sunfish.dev/blazor-ui/api/Sunfish.Blazor.ThemeConstants.Button.FillMode.html).

>caption Example of setting the FillMode


## Theme Color

The color of the button is controlled through the `ThemeColor` parameter. You can set it to a member of the `Sunfish.Blazor.ThemeConstants.Button.ThemeColor` class.

To review all available values for the `ThemeColor` parameter, see the [Button ThemeColor API Reference](https://docs.sunfish.dev/blazor-ui/api/Sunfish.Blazor.ThemeConstants.Button.ThemeColor.html).

>caption Example of setting the ThemeColor


## Rounded

The `Rounded` parameter applies the border-radius CSS rule to the button to achieve curving of the edges. You can set it to a member of the `Sunfish.Blazor.ThemeConstants.Button.Rounded` class.

To review all available values for the `Rounded` parameter, see the [Button Rounded API Reference](https://docs.sunfish.dev/blazor-ui/api/Sunfish.Blazor.ThemeConstants.Button.Rounded.html).

>caption Example of Setting the Rounded parameter


## Icon

Set the `Icon` parameter to display an icon. You can use a predefined [Sunfish icon](slug:common-features-icons) or a custom one.

>caption Example of customizing the default icon


## Custom Styles

Use the `Class` parameter to apply custom CSS classes. You can further style the button by targeting these classes.

>caption Example of custom styling


## See Also

- [SpeechToTextButton Overview](slug:speechtotextbutton-overview)
- [SpeechToTextButton Events](slug:speechtotextbutton-events)
- [SpeechToTextButton Integration](slug:speechtotextbutton-integration)
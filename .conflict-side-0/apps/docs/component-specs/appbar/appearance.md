---
title: Appearance
page_title: AppBar Appearance
description: Appearance settings of the AppBar for Blazor.
slug: appbar-appearance
tags: sunfish,blazor,appbar,navbar,appearance
published: True
position: 35
components: ["appbar"]
---
# Appearance Settings

This article outlines the available AppBar parameters, which control its appearance.

## ThemeColor

You can change the color of the AppBar by setting the `ThemeColor` parameter to a member of the `Sunfish.Blazor.ThemeConstants.AppBar.ThemeColor` class:

| Class members | Manual declarations |
|---------------|--------|
| `Base` | `base`   |
| `Primary` | `primary`|
| `Secondary` | `secondary`|
| `Tertiary` | `tertiary`|
| `Info` | `info`   |
| `Success` | `success`|
| `Warning` | `warning`|
| `Error` | `error`  |
| `Dark` | `dark`   |
| `Light` | `light`  |
| `Inverse` | `inverse`|

>caption The built-in AppBar colors

````RAZOR
<SunfishDropDownList Data="@ThemeColors" @bind-Value="@SelectedColor" Width="150px"></SunfishDropDownList>

<SunfishAppBar ThemeColor="@SelectedColor">
    <AppBarSection>
        <span>Our Logo</span>
    </AppBarSection>

    <AppBarSpacer Size="25%"></AppBarSpacer>

    <AppBarSection>
        <span>Our Products</span>
    </AppBarSection>

    <AppBarSpacer Size="50px"></AppBarSpacer>

    <AppBarSection>
        <span>Our Mission</span>
    </AppBarSection>

    <AppBarSpacer></AppBarSpacer>

    <AppBarSection>
        <SunfishSvgIcon Icon="@SvgIcon.User"></SunfishSvgIcon>
    </AppBarSection>

    <AppBarSeparator></AppBarSeparator>

    <AppBarSection>
        <SunfishSvgIcon Icon="@SvgIcon.Logout"></SunfishSvgIcon>
    </AppBarSection>
</SunfishAppBar>

@code {
    private string SelectedColor { get; set; } = "base";

    private List<string> ThemeColors { get; set; } = new List<string>()
    {
        "base",
        "primary",
        "secondary",
        "tertiary",
        "info",
        "success",
        "warning",
        "error",
        "dark",
        "light",
        "inverse"
    };
}
````
---
title: Overview
page_title: Skeleton Overview
description: Overview of the Skeleton for Blazor.
slug: skeleton-overview
tags: sunfish,blazor,skeleton,overview
published: True
position: 0
components: ["skeleton"]
---
# Blazor Skeleton Overview

The <a href = "https://sunfish.dev/blazor-ui/skeleton" target="_blank">Blazor Skeleton</a> is a loading indicator. What separates it from conventional loaders is that it mimics the page layout by showing elements in a similar shape as the actual content that will appear after loading.


## Creating Blazor Skeleton

1. Use the `<SunfishSkeleton>` tag.
1. Set the `Visible` parameter to a `bool` property or expression.
1. Set the `Width` and `Height` parameters. 
1. Use the `ShapeType` parameter to set the shape of the Skeleton.

>caption Basic Skeleton

````RAZOR
@* The Sunfish UI for Blazor Skeleton component with its basic settings. *@

@if (String.IsNullOrEmpty(Text))
{
    <SunfishSkeleton ShapeType="@SkeletonShapeType.Rectangle"
                     Width="61px"
                     Height="28px"
                     Visible="@isVisible"></SunfishSkeleton>
}
else
{
    <SunfishButton Size="medium">@Text</SunfishButton>
}

@code {
    string Text { get; set; } = string.Empty;
    bool isVisible { get; set; }

    protected override async Task OnInitializedAsync()
    {
        isVisible = true;
        await Task.Delay(2000);

        Text = "Button";

        isVisible = false;
    }
}
````


## Appearance

The Sunfish UI for Blazor Skeleton provides various options to control its [visual appearance](slug:skeleton-appearance):

* `ShapeType`
* `AnimationType`

## Skeleton Parameters

The table below, lists the available parameters for the Skeleton component.

| Parameter | Type and Default&nbsp;Value | Description |
| --- | --- | --- |
| `Class` | `string` | Renders a custom CSS class to the `<span class="k-skeleton">` element. |
| `ShapeType` | `SkeletonShapeType` enum<br />(`Text`) | Sets the [shape](slug:skeleton-appearance#shapetype). |
| `AnimationType` | `SkeletonAnimationType` enum<br />(`Pulse`) | Sets the [animation of the Skeleton](slug:skeleton-appearance#animationtype). |
| `Width` | `string` | Sets the width of the Skeleton component. Required for every shape. |
| `Height` | `string` | Sets the height of the Skeleton component. Require for the `Circle` and `Rectangle` shapes. The `Text` shape calculates its own height, but you can override it with the `Height` parameter. |
| `Visible` | `bool`<br />(`true`) | Controls if the Skeleton is rendered on the page. |


## Next Steps

* [Check the Skeleton appearance settings](slug:skeleton-appearance)


## See Also

* [Live Demo: Skeleton](https://demos.sunfish.dev/blazor-ui/skeleton/overview)
* [Skeleton API Reference](slug:Sunfish.Blazor.Components.SunfishSkeleton)

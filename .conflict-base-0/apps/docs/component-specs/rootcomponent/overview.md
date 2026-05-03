---
title: Overview
page_title: RootComponent - Overview
description: Overview of the Sunfish Root Component for Blazor.
slug: rootcomponent-overview
tags: sunfish,blazor,sunfishrootcomponent,rootcomponent
published: True
position: 0
components: ["general"]
---
# SunfishRootComponent Overview

The `SunfishRootComponent` is a special component in Sunfish UI for Blazor. Its placement and configuration affects all other Sunfish Blazor components. This article describes the purpose and usage of `SunfishRootComponent`.


## Purpose

The `SunfishRootComponent` is responsible for the following tasks:

* It provides settings to all its child Sunfish components, for example, for the [icon type](slug:common-features-icons#set-global-blazor-icon-type) or [right-to-left (RTL) support](slug:rtl-support).
* It renders all Sunfish popups, which has the following benefits:
    * It's more reliable that the popups will display on top of the other page content.
    * There is no risk for the popups to be trapped by scrollable containers, or clipped by containers with an `overflow:hidden` style.
* It exposes the `DialogFactory` for using [predefined dialogs](slug:dialog-predefined).
* It helps with the integration of components, for example, when using [connected ListBoxes](slug:listbox-connect) or a [Chart Breadcrumb](slug:chart-drilldown#).

The `SunfishRootComponent` achieves all these tasks with the help of [cascading values](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/cascading-values-and-parameters). That's why it is crucial for the Root Component to wrap all other Sunfish components in the app, otherwise an [exception may occur](slug:common-kb-component-requires-sunfishrootcomponent). To ensure correct popup position, it is also highly recommended for the `SunfishRootComponent` to be the top-level component in the app and wrap all other content, including the application layout.


## Using SunfishRootComponent

This section applies to:

* .NET 8 and .NET 9 Blazor Web Apps with **Global** interactivity location. If your app has **Per page/component** interactivity, then refer to section [Interactivity Considerations](#interactivity-considerations) below.
* Blazor Server, WebAssembly and Hybrid apps in all .NET versions

The recommended way to add `SunfishRootComponent` to a Blazor app is to:

@[template](/_contentTemplates/rootcomponent/setup.md#define-in-sunfishlayout)

The above approach has the following benefits:

* There is a separation of concerns and a single `SunfishRootComponent` can be a parent of multiple other layouts.
* You can use `DialogFactory` (predefined Sunfish dialogs) in `MainLayout.razor`.

However, you can also add `<SunfishRootComponent>` directly to an existing application layout, instead of creating a new one.

>caption Adding SunfishRootComponent to MainLayout.razor

<div class="skip-repl"></div>

````RAZOR
@inherits LayoutComponentBase

<SunfishRootComponent>
    @* All the MainLayout.razor content becomes nested in the Sunfish root component. *@
</SunfishRootComponent>
````


## Interactivity Considerations

.NET 8 introduced new [render modes for Blazor web apps](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes) and the concept of static Blazor apps with optional interactive components. The following requirements and considerations apply to the `SunfishRootComponent`:

* The `SunfishRootComponent` must reside in an interactive layout or component.
* Application layouts are interactive only if the whole app is interactive. To achieve this, set **Interactivity location** of the app to **Global** during app creation.

If you are using Sunfish components in a Blazor app with **Per page/component** interactivity location, then learn [how to correctly add the `SunfishRootComponent`](slug:rootcomponent-percomponent) in this case.


## SunfishRootComponent Parameters

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Parameter | Type and Default&nbsp;Value | Description |
| --- | --- | --- |
| `EnableRtl` | `bool` | Enables [right-to-left (RTL) support](slug:rtl-support). |
| `IconType` | `IconType` enum <br /> (`Svg`) | The icon type, which other Sunfish components will use to render internal icons. Regardless of this parameter value, you can freely use the [`<SunfishFontIcon>`](slug:common-features-icons#fonticon-component) and [`<SunfishSvgIcon>`](slug:common-features-icons#svgicon-component) components, and [set the `Icon` parameter of other Sunfish components](slug:button-icons) to any type that you wish. |
| `Localizer` | `Sunfish.Blazor.Services.ISunfishStringLocalizer` | The Sunfish localization service. The recommended approach is to [define the localizer as a service in `Program.cs`](slug:globalization-localization). Use the `Localizer` parameter only in special cases when this is not possible. |

### SunfishRootComponent Settings

The `SunfishRootComponent` exposes and additional `<RootComponentSettings>` tag for further customizations. You can use it to configure the screen breakpoints for the adaptive rendering of the supported components. [Learn how to customize the default adaptive breakpoints](slug:adaptive-rendering#customize-the-default-adaptive-breakpoints).

## See Also

* [Popup Troubleshooting](slug:troubleshooting-general-issues)
* [Setting up Sunfish Blazor apps](slug:getting-started/what-you-need)
* [Exception: Sunfish component requires a SunfishRootComponent](slug:common-kb-component-requires-sunfishrootcomponent)

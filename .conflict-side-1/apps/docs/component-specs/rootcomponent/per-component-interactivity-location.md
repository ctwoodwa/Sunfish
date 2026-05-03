---
title: Using with Per Component Interactivity
page_title: RootComponent - Using with Per Page/Component Interactivity Location
description: Learn how to use the SunfishRootComponent in the Blazor Web App project template when the Interactivity Location is set to Per page/component.
slug: rootcomponent-percomponent
tags: sunfish,blazor,sunfishrootcomponent,rootcomponent
published: True
position: 10
components: ["sankey"]
---
# Using SunfishRootComponent with Per Page/Component Interactivity

.NET 8.0 introduced new [render modes for Blazor web apps](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes). The default render mode is static, while Sunfish Blazor components require interactive render mode. This article explains how to use the `SunfishRootComponent` in static apps with specific interactive Razor components.


## Fundamentals

The `SunfishRootComponent` must [reside in an interactive layout or component in order to pass cascading values](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/cascading-values-and-parameters?view=aspnetcore-8.0#cascading-valuesparameters-and-render-mode-boundaries) to all other Sunfish Blazor components. The `SunfishRootComponent` placement in the app depends on the selected **Interactivity location** during app creation.

In apps with **Global** interactivity location, it's best to [add the `SunfishRootComponent` to a layout](slug:rootcomponent-overview#using-sunfishrootcomponent).

In apps with **Per page/component** interactivity, the layout files are static. There are three options to use the `SunfishRootComponent` in this case:

* [Add a `SunfishRootComponent` to all interactive `.razor` pages](#add-sunfishrootcomponent-to-interactive-pages) that host Sunfish Blazor components.
* [Change the application's render mode to interactive at runtime](#change-the-app-render-mode-at-runtime) for specific pages.
* [Use an empty layout for pages with Sunfish components](#use-empty-layout) and duplicate the contents of the regular app layout to another `.razor` file.

The sections below provide additional information for each of the three options. Review this [Blazor Web App sample project on GitHub](https://github.com/ctwoodwa/sunfish-archive/blazor-ui/tree/master/rootcomponent/BlazorWebAppServer), which also demonstrates all three options.


## Add SunfishRootComponent to Interactive Pages

In this scenario, add a `SunfishRootComponent` to all interactive `.razor` pages, which host Sunfish Blazor components. The `SunfishRootComponent` will not wrap all the page content, so a possible side effect may be [wrong popup position](slug:troubleshooting-general-issues#wrong-popup-position). Component interactivity is inherited, so nested components will not need to be explicitly interactive.

Here are the detailed steps:

**1\.** Create a `SunfishContainer.razor` file and configure the `<SunfishRootComponent>` in it. The `SunfishContainer` component is not required, but it allows you to reuse a single `SunfishRootComponent` with the same settings across the whole app.

>caption SunfishContainer.razor

<div class="skip-repl"></div>

````RAZOR
<SunfishRootComponent IconType="@IconType.Svg"
                    EnableRtl="false">
    @ChildContent
</SunfishRootComponent>

@code {
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
````

**2\.** Enable interactive render mode for the `.razor` page, which will hold Sunfish Blazor components, for example, `Home.razor`.

>caption Home.razor

<div class="skip-repl"></div>

````RAZOR
@page "/"

@rendermode InteractiveServer


````

**3\.** Add the `SunfishContainer` component to `Home.razor` and add Sunfish Blazor components as child content. Make sure that `<SunfishContainer>` is recognized as a Razor component. Add a `@using` statement to achieve this, for example, if the two `.razor` files are in different folders.

>caption Home.razor

<div class="skip-repl"></div>

````RAZOR
@page "/"

@rendermode InteractiveServer

<SunfishContainer>

    <SunfishDatePicker @bind-Value="@DatePickerValue" Width="200px" />

</SunfishContainer>

@code {
    private DateTime DatePickerValue { get; set; } = DateTime.Today;
}
````

> When the `SunfishRootComponent` is added to a `.razor` file, you cannot reference the `DialogFactory` and use [predefined dialogs](slug:dialog-predefined) in the same `.razor` file. The `DialogFactory` will be available to child components of the `SunfishRootComponent`. However, a [workaround exists](https://github.com/ctwoodwa/sunfish-archive/blazor-ui/tree/master/rootcomponent/BlazorWebAppServer).


## Change the App Render Mode at Runtime

In this scenario, [add a `SunfishRootComponent` to a layout](slug:rootcomponent-overview#using-sunfishrootcomponent) as if the application has **Global** interactivity location. Then, enable global interactivity at runtime when the user navigates to a page (component) with Sunfish components inside. To do this, [set the `@rendermode` conditionally in `App.razor`](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-8.0#set-the-render-mode-by-component-instance). Blazor Web Apps with identity use the [same approach to disable interactivity in the `Account` section](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-8.0#area-folder-of-static-ssr-components).

>caption Change render mode at runtime in App.razor

<div class="skip-repl"></div>

````RAZOR
<!DOCTYPE html>
<html lang="en">

<head>
    @ ... @
    <HeadOutlet @rendermode="@RenderModeForPage" />
</head>

<body>
    <Routes @rendermode="@RenderModeForPage" />
    @ ... @
</body>

</html>

@code {
    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    private IComponentRenderMode? RenderModeForPage =>
        HttpContext.Request.Path.StartsWithSegments("/page-with-sunfish-components")
            ? InteractiveServer
            : null;
}
````

## Use Empty Layout

In this scenario, use a regular layout (`MainLayout.razor`) for static pages and another empty layout (for example, `EmptyLayout.razor`) for interactive pages with Sunfish components. The contents of `MainLayout.razor` must be copied to a non-layout `.razor` page, which uses the empty layout. Here are the detailed steps, which refer to a [Blazor Web App sample project on GitHub](https://github.com/ctwoodwa/sunfish-archive/blazor-ui/tree/master/rootcomponent/BlazorWebAppServer):

1. Create a [new layout file `EmptyLayout.razor`](https://github.com/ctwoodwa/sunfish-archive/blazor-ui/blob/master/rootcomponent/BlazorWebAppServer/Components/Layout/EmptyLayout.razor) in the same folder as `MainLayout.razor`.
1. Copy the contents for `MainLayout.razor` to a [non-layout `.razor` file, for example, `Shared/LayoutContainer.razor`](https://github.com/ctwoodwa/sunfish-archive/blazor-ui/blob/master/rootcomponent/BlazorWebAppServer/Components/Shared/LayoutContainer.razor).
1. Copy `MainLayout.razor.css` as [`Shared/LayoutContainer.razor.css`](https://github.com/ctwoodwa/sunfish-archive/blazor-ui/blob/master/rootcomponent/BlazorWebAppServer/Components/Shared/LayoutContainer.razor.css).
1. Replace `@Body` with `@ChildContent` in the copied layout content (`LayoutContainer.razor`).
1. [Reference `EmptyLayout.razor` in a Razor component with Sunfish Blazor components inside](https://github.com/ctwoodwa/sunfish-archive/blazor-ui/blob/master/rootcomponent/BlazorWebAppServer/Components/Pages/PageWithEmptyLayout.razor).
1. Wrap all Sunfish components in a `<LayoutContainer>` component.

This code duplication requires more effort to maintain, but avoids [possible issues with popup position](slug:troubleshooting-general-issues#wrong-popup-position). The approach is applicable to Blazor Web Apps with **Server** render mode. Apps with **WebAssembly** or **Auto** render mode, and **Per page/component** interactivity, have their layout files and interactive `.razor` components in separate projects, which limits the ability to switch layouts.


## See Also

* [Blazor Web App sample project on GitHub](https://github.com/ctwoodwa/sunfish-archive/blazor-ui/tree/master/rootcomponent/BlazorWebAppServer)
* [Setting up Sunfish Blazor apps](slug:getting-started/what-you-need)
* [ASP.NET Core Blazor render modes](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes)
* [Video: Intro to Blazor in .NET 8 - SSR, Stream Rendering, Auto](https://www.youtube.com/watch?v=walv3nLTJ5g)

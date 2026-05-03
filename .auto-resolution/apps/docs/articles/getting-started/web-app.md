---
uid: getting-started-web-app
title: Blazor Web App
description: Set up Sunfish in a Blazor Web App project with interactive server rendering.
---

# Blazor Web App

This guide walks through adding Sunfish to a new Blazor Web App project targeting .NET 10 with interactive server rendering.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022 (17.12 or later), VS Code with the C# Dev Kit extension, or JetBrains Rider

## Create the Project

**Using the .NET CLI:**

```bash
dotnet new blazorserver -n MyApp
cd MyApp
```

**Using Visual Studio:**

1. Select **Create a new project**.
2. Choose **Blazor Web App**.
3. Set the framework to **.NET 10**.
4. Set the interactive render mode to **Server**.
5. Click **Create**.

## Install Packages

Add the Sunfish core package and your chosen provider package. This guide uses FluentUI; substitute `Bootstrap` or `Material` as needed.

```bash
dotnet add package Sunfish.Components
dotnet add package Sunfish.Providers.FluentUI
```

> [!NOTE]
> See [Choosing a Provider](xref:getting-started-provider-selection) for guidance on which provider best fits your project.

## Register Services

Open `Program.cs` and add the Sunfish service registration after `builder.Services.AddRazorComponents()`:

```csharp
using Sunfish.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Sunfish and select the FluentUI provider
builder.Services.AddSunfish().UseFluentUI();

var app = builder.Build();

// ... configure pipeline ...

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

## Add the Provider Stylesheet

Open `Components/App.razor` and add the provider stylesheet link inside `<head>`, after any existing stylesheets:

```html
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <link rel="stylesheet" href="app.css" />
    <link rel="stylesheet" href="MyApp.styles.css" />

    <!-- Sunfish FluentUI provider stylesheet -->
    <link rel="stylesheet" href="_content/Sunfish.Providers.FluentUI/css/sunfish-fluentui.css" />

    <HeadOutlet />
</head>
```

## Wrap the Layout in SunfishThemeProvider

Open `Components/Layout/MainLayout.razor` and wrap the layout body with `<SunfishThemeProvider>`:

```razor
@inherits LayoutComponentBase

<SunfishThemeProvider>
    <div class="page">
        <div class="sidebar">
            <NavMenu />
        </div>

        <main>
            <div class="top-row px-4">
                <a href="https://learn.microsoft.com/aspnet/core/" target="_blank">About</a>
            </div>

            <article class="content px-4">
                @Body
            </article>
        </main>
    </div>
</SunfishThemeProvider>
```

`SunfishThemeProvider` emits the `--sunfish-*` CSS custom property token block and applies the `data-sunfish-theme` attribute to its root element. All Sunfish components must be descendants of a `SunfishThemeProvider`.

## Add Your First Component

Open a page — for example, `Components/Pages/Home.razor` — and add a Sunfish component:

```razor
@page "/"

<PageTitle>Home</PageTitle>

<h1>Hello, Sunfish!</h1>

<SunfishButton Variant="ButtonVariant.Primary" OnClick="HandleClick">
    Click me
</SunfishButton>

<p>@_message</p>

@code {
    private string _message = string.Empty;

    private void HandleClick()
    {
        _message = "Button clicked!";
    }
}
```

## Verify It Works

Run the project:

```bash
dotnet run
```

Open `https://localhost:<port>` in a browser. You should see the styled button rendered with FluentUI tokens. Clicking the button updates the message via Blazor's interactive server rendering.

### Checklist

If components do not render correctly, work through the following:

- Confirm `builder.Services.AddSunfish().UseFluentUI()` is present in `Program.cs`.
- Confirm the `<link>` tag for `sunfish-fluentui.css` is present in `App.razor`.
- Confirm `<SunfishThemeProvider>` wraps the layout body in `MainLayout.razor`.
- Confirm the project targets `.NET 10`.

## Next Steps

- [Choosing a Provider](xref:getting-started-provider-selection) — understand the differences between FluentUI, Bootstrap, and Material 3.
- [Theming Overview](xref:theming-overview) — customize colors, typography, and dark mode.
- [Testing with bUnit](xref:testing-bunit) — write unit tests for components using bUnit v2.

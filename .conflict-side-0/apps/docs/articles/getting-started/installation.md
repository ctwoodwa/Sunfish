---
uid: getting-started-installation
title: Installation
description: Install the Sunfish component library and FluentUI provider into a Blazor project.
---

# Installation

## 1. Add the NuGet packages

Every Sunfish project needs the core component package. You also need at least one provider -- here we use the Fluent UI provider.

```bash
dotnet add package Sunfish.Components
dotnet add package Sunfish.Providers.FluentUI
```

## 2. Register services in Program.cs

Open `Program.cs` and register Sunfish with the Fluent UI provider:

```csharp
builder.Services.AddSunfish().UseFluentUI();
```

`AddSunfish()` registers core services (theming, dialogs, notifications) and returns a `SunfishBuilder`. The `UseFluentUI()` extension method registers the Fluent UI implementations of `ISunfishCssProvider`, `ISunfishIconProvider`, and `ISunfishJsInterop`.

## 3. Add the stylesheet

In your `App.razor` (or `index.html` for Blazor WebAssembly), add the provider CSS inside the `<head>` section:

```html
<link rel="stylesheet" href="_content/Sunfish.Providers.FluentUI/css/sunfish-fluentui.css" />
```

## 4. Add the imports

Open `_Imports.razor` and add:

```razor
@using Sunfish.Components
@using Sunfish.Components.Buttons
@using Sunfish.Components.Forms.Inputs
@using Sunfish.Components.DataDisplay
@using Sunfish.Components.Feedback
@using Sunfish.Components.Layout
@using Sunfish.Components.Navigation
@using Sunfish.Components.Utility
```

## Verify the setup

Create a quick test page to confirm everything is wired up:

```razor
@page "/test"

<SunfishButton Variant="ButtonVariant.Primary" OnClick="@(() => message = "It works!")">
    Click me
</SunfishButton>

<p>@message</p>

@code {
    private string message = "";
}
```

Run the application and navigate to `/test`. If you see a styled button that updates the message on click, the installation is complete.

## Next steps

- [First Component](xref:getting-started-first-component) -- a guided walkthrough of your first Sunfish page.
- [Theming Overview](xref:theming-overview) -- customize colors, typography, and shape.

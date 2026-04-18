---
uid: common-features-icons
title: Icons
description: Using SVG icons in Sunfish components — built-in Tabler Icons, custom icons, and provider icon architecture.
---

# Icons

Sunfish ships with built-in SVG icon support through the `SunfishIcon` component and a provider-aware icon architecture that lets each UI provider supply its own icon implementation.

## SunfishIcon Component

`SunfishIcon` renders a single SVG icon by name.

```razor
<SunfishIcon Name="home" />

<SunfishIcon Name="user" Size="IconSize.Large" />

<SunfishIcon Name="settings" Color="var(--sunfish-color-primary)" />
```

### Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Name` | `string` | — | The icon name (see Tabler Icons). |
| `Size` | `IconSize` | `IconSize.Medium` | Predefined size. |
| `Width` | `string?` | `null` | Explicit width, overrides `Size`. |
| `Height` | `string?` | `null` | Explicit height, overrides `Size`. |
| `Color` | `string?` | `null` | SVG stroke or fill color. Accepts any CSS color value. |
| `Title` | `string?` | `null` | Accessible title for the SVG element (`aria-label`). |
| `Class` | `string?` | `null` | Additional CSS classes. |
| `Style` | `string?` | `null` | Inline styles applied to the SVG element. |

## Tabler Icons

The default icon set is [Tabler Icons](https://tabler.io/icons), an open-source collection of 5,000+ SVG icons distributed under the MIT license.

Icons are referenced by their Tabler name — lowercase, hyphen-separated:

```razor
<SunfishIcon Name="brand-github" />
<SunfishIcon Name="arrow-up-right" />
<SunfishIcon Name="circle-check" />
<SunfishIcon Name="alert-triangle" />
```

Icons are delivered as an SVG sprite embedded in the Sunfish bundle. No additional CDN requests are made at runtime.

## Icon Sizes

`IconSize` is an enum with four predefined values:

| Value | Pixel size |
|---|---|
| `IconSize.Small` | 16 px |
| `IconSize.Medium` | 20 px |
| `IconSize.Large` | 24 px |
| `IconSize.ExtraLarge` | 32 px |

For sizes outside these steps, set `Width` and `Height` directly:

```razor
<SunfishIcon Name="star" Width="48px" Height="48px" />
```

## Provider Icon Architecture

Each UI provider (FluentUI, Bootstrap, Material 3) registers an `ISunfishIconProvider` implementation. This interface controls how `SunfishIcon` resolves and renders its output — allowing providers to use their own icon font, SVG system, or native icon component while keeping the `SunfishIcon` API stable.

```csharp
public interface ISunfishIconProvider
{
    RenderFragment Render(string name, IconSize size, string? color, string? title);
}
```

The active provider is resolved from DI. When the FluentUI provider is active, icons use Fluent's SVG sprite; the Bootstrap provider falls back to the same Tabler sprites with Bootstrap-compatible sizing tokens.

You can register a custom provider in `Program.cs`:

```csharp
builder.Services.AddSingleton<ISunfishIconProvider, MyCustomIconProvider>();
```

## Custom Icons

To use icons outside the Tabler set, implement `ISunfishIconProvider` or use `SunfishIcon` with `ChildContent` when you need a fully custom SVG inline:

```razor
<SunfishIcon Name="my-logo" Size="IconSize.Large">
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
        <path d="M12 2 ..." />
    </svg>
</SunfishIcon>
```

Alternatively, render the SVG directly and apply the same spacing classes manually for consistency.

## Icons in Components

Many Sunfish components accept icon parameters directly, so you rarely need to nest `SunfishIcon` by hand.

```razor
<!-- Button with icon -->
<SunfishButton Icon="download" Variant="ButtonVariant.Primary">Export</SunfishButton>

<!-- NavLink with icon -->
<SunfishNavLink Href="/dashboard" Icon="dashboard">Dashboard</SunfishNavLink>

<!-- Menu item with icon -->
<SunfishMenuItem Icon="edit" OnClick="Edit">Edit</SunfishMenuItem>

<!-- Alert with icon -->
<SunfishAlert Severity="AlertSeverity.Warning" Icon="alert-triangle">
    Check your input before proceeding.
</SunfishAlert>
```

These parameters accept the same Tabler icon name strings used with `SunfishIcon`.

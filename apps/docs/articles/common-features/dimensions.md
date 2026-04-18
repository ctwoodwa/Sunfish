---
uid: common-features-dimensions
title: Dimensions
description: How to control the size and dimensions of Sunfish components.
---

# Dimensions

Sunfish components provide a consistent set of parameters for controlling their physical size. Most components expand to fill their container by default and accept explicit overrides through `Width`, `Height`, and provider-aware CSS custom properties.

## Width and Height Parameters

The majority of Sunfish components accept `Width` and `Height` as string parameters. These accept any valid CSS size value.

```razor
<!-- Fixed pixel width -->
<SunfishTextBox Width="300px" />

<!-- Percentage of container -->
<SunfishDropDown Width="100%" />

<!-- Viewport unit -->
<SunfishDataGrid Height="60vh" />

<!-- CSS calc() -->
<SunfishMultiSelect Width="calc(100% - 48px)" />
```

These values are applied as inline `style` attributes on the component root element. You can combine them with the `Style` parameter for additional inline styles:

```razor
<SunfishDataGrid
    Width="100%"
    Height="400px"
    Style="border-radius: var(--sunfish-radius-medium);" />
```

## Responsive Behavior

By default, most block-level Sunfish components use `width: 100%` and inherit height from their content. This means they adapt to their container without any explicit sizing.

To constrain a component within a flexible layout, wrap it or set `Width` to a specific value:

```razor
<div style="display: flex; gap: 16px;">
    <SunfishTextBox Width="240px" Placeholder="Search..." />
    <SunfishButton>Search</SunfishButton>
</div>
```

For responsive grid layouts, rely on the container's grid or flex rules and leave `Width` unset:

```razor
<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px;">
    <SunfishTextBox @bind-Value="model.FirstName" />
    <SunfishTextBox @bind-Value="model.LastName" />
</div>
```

## CSS Custom Properties

Sunfish exposes a set of `--sunfish-*` spacing tokens that you can use for consistent sizing across components. These are set at the `:root` level and adapt to the active provider.

| Token | Purpose |
|---|---|
| `--sunfish-spacing-xs` | Extra-small gap (4 px) |
| `--sunfish-spacing-sm` | Small gap (8 px) |
| `--sunfish-spacing-md` | Medium gap (16 px) |
| `--sunfish-spacing-lg` | Large gap (24 px) |
| `--sunfish-spacing-xl` | Extra-large gap (32 px) |

Use these tokens in `Width`, `Height`, or custom container styles to align sizing with the rest of the design system:

```razor
<SunfishCard Style="padding: var(--sunfish-spacing-md);">
    <SunfishTextBox Width="100%" />
</SunfishCard>
```

## Component-Specific Sizing

Some components have their own discrete size scales rather than free-form strings.

### Button Sizes

`SunfishButton` accepts a `Size` parameter of type `ButtonSize`:

```razor
<SunfishButton Size="ButtonSize.Small">Small</SunfishButton>
<SunfishButton Size="ButtonSize.Medium">Medium</SunfishButton>
<SunfishButton Size="ButtonSize.Large">Large</SunfishButton>
```

### Avatar Sizes

`SunfishAvatar` accepts a `Size` parameter of type `AvatarSize`:

```razor
<SunfishAvatar Name="Jane Smith" Size="AvatarSize.Small" />
<SunfishAvatar Name="Jane Smith" Size="AvatarSize.Medium" />
<SunfishAvatar Name="Jane Smith" Size="AvatarSize.Large" />
```

### Icon Sizes

`SunfishIcon` accepts `IconSize.Small` (16 px), `IconSize.Medium` (20 px), `IconSize.Large` (24 px), and `IconSize.ExtraLarge` (32 px). See <xref:common-features-icons> for details.

### DataGrid Row Height

`SunfishDataGrid` exposes a `RowHeight` parameter (integer, pixels) that controls the height of each data row. The default is 36 px.

```razor
<SunfishDataGrid TItem="Order" RowHeight="48" OnRead="@LoadOrders">
    ...
</SunfishDataGrid>
```

## Setting Width and Height via Style Parameter

For cases where `Width` and `Height` are not available on a particular component, use the `Style` parameter directly:

```razor
<SunfishCard Style="width: 320px; min-height: 200px;">
    ...
</SunfishCard>
```

All Sunfish components inherit from `SunfishComponentBase`, which merges the `Style` parameter with any internally generated inline styles via the `CombineStyles()` helper.

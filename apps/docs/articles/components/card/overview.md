---
uid: component-card-overview
title: Card
description: The SunfishCard component provides a content container composed with CardHeader, CardBody, and CardActions.
---

# Card

## Overview

The `SunfishCard` component renders a styled container typically used to group related content and actions. It is a composition component -- you build its structure by nesting `SunfishCardHeader`, `SunfishCardBody`, and `SunfishCardActions` inside it.

## Creating a Card

````razor
<SunfishCard>
    <SunfishCardHeader>
        <h3>Card Title</h3>
    </SunfishCardHeader>
    <SunfishCardBody>
        <p>This is the card body content.</p>
    </SunfishCardBody>
    <SunfishCardActions>
        <SunfishButton Variant="ButtonVariant.Primary">Action</SunfishButton>
    </SunfishCardActions>
</SunfishCard>
````

## Features

- **Composable structure** -- Combine `SunfishCardHeader`, `SunfishCardBody`, and `SunfishCardActions` in any order, or use only the parts you need.
- **Provider-driven styling** -- CSS classes are resolved via `ISunfishCssProvider.CardClass()`, `CardHeaderClass()`, `CardBodyClass()`, and `CardActionsClass()`.
- **Flexible content** -- Each section accepts arbitrary Razor content through `ChildContent`.

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` | The content of the card, typically composed of `SunfishCardHeader`, `SunfishCardBody`, and `SunfishCardActions`. |

### SunfishCardHeader Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` | Header content (title, subtitle, avatar). |

### SunfishCardBody Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` | Main body content of the card. |

### SunfishCardActions Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` | Action buttons or links displayed at the bottom of the card. |

## See Also

- [API Reference](xref:Sunfish.Components.Blazor.Components.DataDisplay.SunfishCard)

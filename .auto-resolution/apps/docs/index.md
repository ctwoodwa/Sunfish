---
_layout: landing
---

# Sunfish Documentation

Sunfish is a provider-first Blazor component library. Components define behavior; providers supply visual styling. Swap providers to change the entire look-and-feel without touching component code.

## Quick Links

- [Getting Started](articles/getting-started/overview.md) - Installation and first component
- [Components](https://localhost:5301/components) - Live component demos and usage guides
- [Theming](articles/theming/overview.md) - Provider system and custom themes
- [API Reference](api/toc.yml) - Auto-generated API docs from source code

## Architecture

```
Sunfish.Core          - Contracts, base classes, enums, configuration
Sunfish.Components    - Provider-agnostic Razor components
Sunfish.Icons         - Custom SVG icon set + icon provider
Sunfish.Providers.*   - Provider implementations (FluentUI, Bootstrap, etc.)
```

## Current Providers

| Provider | Package | Status |
|---|---|---|
| Fluent UI | `Sunfish.Providers.FluentUI` | Available |
| Bootstrap 5 | `Sunfish.Providers.Bootstrap` | Planned |
| Material Design 3 | `Sunfish.Providers.Material` | Planned |

# Sunfish.Icons.Legacy

**Obsolete.** Provides the original Sunfish/Marilo legacy icon sprite (362 SVG symbols) as an
`ISunfishIconProvider` for backward compatibility with code migrated from Marilo. New projects
should prefer **[Sunfish.Icons.Tabler](../Tabler/README.md)**, which ships a larger, actively
maintained icon set. Because this package's public API is marked `[Obsolete]`, consumers will
receive compiler warnings (`CS0618`) when registering it — that is intentional.

## Breaking changes from Marilo.Icons

When migrating from `Marilo.Icons`, note the following contract changes:

- **Icon lookup by short name.** Use `GetIcon("search")` — the provider now auto-prefixes. The
  legacy `GetIcon("marilo-search")` form no longer applies; you may pass `"sf-search"` (the new
  prefix) and it will be respected as-is, but the `"marilo-"` prefix is gone.
- **Symbol IDs renamed.** Every sprite symbol's `id` attribute has been renamed from `marilo-*`
  to `sf-*`. Any hard-coded `<use href="...#marilo-foo">` markup must be updated to
  `<use href="...#sf-foo">`.

## Usage

Register the provider in `Program.cs`:

```csharp
services.AddSunfishIconsLegacy(); // CS0618: obsolete — see migration guide below
```

Then reference icons by name in Razor markup:

```razor
<SunfishIcon Name="search" />
```

The sprite is served from `_content/Sunfish.Icons.Legacy/icons/sprite.svg` and referenced via
`<use href="...#sf-{name}" />`, so the browser fetches the sprite exactly once per app.

## Migration guide

To upgrade off the legacy icon set, replace the DI registration:

```diff
- services.AddSunfishIconsLegacy();
+ services.AddSunfishIconsTabler();
```

Only one icon provider should be registered; if both are registered the last one wins. Icon
names carry across cleanly for most glyphs, but review your markup — a handful of legacy names
differ from their Tabler counterparts.

## Package size

The sprite asset is approximately **112 KB** (362 symbols). It is served as a static web asset
and cached by the browser.

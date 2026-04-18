# Sunfish.Icons.Tabler

An icon sprite provider that ships the Tabler Icons SVG sprite (5,039 MIT-licensed icons) to
Sunfish Blazor applications via a registered `IIconProvider`. Use it when you want a large,
modern, consistent-stroke icon set without pulling a per-glyph component library.

## Usage

Register the provider in `Program.cs`:

```csharp
services.AddSunfishIconsTabler();
```

Then reference icons by name anywhere in your Razor markup:

```razor
<SunfishIcon Name="home" />
```

The sprite is served from `_content/Sunfish.Icons.Tabler/icons/tabler-sprite.svg` and referenced
by `<use href="...#tabler-{name}" />`, so the browser fetches the sprite exactly once per app.

## Package size

The sprite asset is approximately **2.1 MB** (5,039 symbols). It is served as a static web asset
and cached by the browser; it is not inlined into every page.

## License / attribution

Tabler Icons (c) Tabler contributors, MIT licensed. See <https://tabler.io/icons>.

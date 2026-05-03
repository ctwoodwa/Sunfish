# DataDisplay Components - Gap Analysis (Part 1)

Compared against `docs/component-specs/*/overview.md`. Base class provides: `Class`, `Style`, `AdditionalAttributes`.

---

## 1. SunfishAvatar

**Implemented:** `Size` (AvatarSize), `Src`, `Alt`, `Initials`

| Gap | Spec Requirement | Severity |
|-----|-----------------|----------|
| Missing `Type` parameter | Spec defines `AvatarType` (Text, Icon, Image) to control content type | **[High]** |
| Missing `ThemeColor` parameter | Spec shows theming support via `ThemeConstants.Avatar.ThemeColor` | **[High]** |
| Missing `Bordered` parameter | Spec defines `bool Bordered` for border display | **[Medium]** |
| Missing `Rounded` parameter | Spec mentions rounded appearance setting | **[Medium]** |
| Missing `FillMode` parameter | Spec references fill mode customization | **[Medium]** |
| Missing `Width`/`Height` parameters | Spec defines these to override `Size` | **[Low]** |
| No `ChildContent` support | Spec uses ChildContent for flexible content (text/icon/image), implementation uses fixed `Initials` string | **[High]** |

---

## 2. SunfishBadge

**Implemented:** `Variant` (BadgeVariant), `ChildContent`

| Gap | Spec Requirement | Severity |
|-----|-----------------|----------|
| Missing `Position` parameter | Spec defines `BadgePosition` (Edge, etc.) for container-relative positioning | **[High]** |
| Missing `HorizontalAlign` parameter | Spec defines `BadgeHorizontalAlign` (End, etc.) | **[High]** |
| Missing `VerticalAlign` parameter | Spec defines `BadgeVerticalAlign` (Top, etc.) | **[High]** |
| Missing `ShowCutoutBorder` parameter | Spec defines `bool` for visual separation from container | **[Medium]** |
| Missing `ThemeColor` parameter | Spec defines theme color (Primary, etc.) | **[Medium]** |
| Missing `FillMode` parameter | Spec defines fill mode (Solid, etc.) | **[Medium]** |
| Missing `Rounded` parameter | Spec defines border radius control | **[Low]** |
| Missing `Size` parameter | Spec defines size (Medium, etc.) | **[Medium]** |
| `Variant` vs spec naming | Implementation uses `Variant`; spec uses separate `ThemeColor`/`FillMode` parameters | **[Medium]** |

---

## 3. SunfishCard

**Implemented:** `ChildContent`

| Gap | Spec Requirement | Severity |
|-----|-----------------|----------|
| Missing `Width` parameter | Spec defines width on the component | **[High]** |
| Missing `Height` parameter | Spec defines height on the component | **[Medium]** |
| Missing `Orientation` parameter | Spec defines `CardOrientation` (Horizontal/Vertical) | **[High]** |
| Missing `ThemeColor` parameter | Spec defines predefined theme colors (primary, secondary, etc.) | **[Medium]** |

---

## 4. SunfishCardActions

**Implemented:** `ChildContent`

| Gap | Spec Requirement | Severity |
|-----|-----------------|----------|
| Missing `Layout` parameter | Spec example shows `CardActionsLayout.Stretch` | **[High]** |

---

## 5. SunfishCardBody

**Implemented:** `ChildContent`

No dedicated spec parameters beyond ChildContent. Appears complete for its role as a container.

**Status:** Likely complete (container only).

---

## 6. SunfishCardHeader

**Implemented:** `ChildContent`

No dedicated spec parameters beyond ChildContent. Appears complete for its role as a container.

**Status:** Likely complete (container only).

**Note:** Spec also references `CardTitle`, `CardSubTitle`, `CardSeparator`, `CardImage`, `CardFooter` sub-components that do not exist in the implementation.

| Gap | Spec Requirement | Severity |
|-----|-----------------|----------|
| Missing `CardTitle` component | Spec uses `<CardTitle>` inside header/body | **[Medium]** |
| Missing `CardSubTitle` component | Spec uses `<CardSubTitle>` | **[Low]** |
| Missing `CardSeparator` component | Spec uses `<CardSeparator>` | **[Low]** |
| Missing `CardImage` component | Spec uses `<CardImage>` with `Src` | **[Medium]** |
| Missing `CardFooter` component | Spec uses `<CardFooter>` | **[Medium]** |

---

## 7. SunfishCarousel

**Implemented:** `ActiveIndex`, `ActiveIndexChanged`, `ItemCount`, `AutoPlay`, `IntervalMs`, `ChildContent`, prev/next/dot navigation, auto-play timer with disposal.

| Gap | Spec Requirement | Severity |
|-----|-----------------|----------|
| Missing `Data` property | Spec is generic (`SunfishCarousel<TItem>`), binds to a data collection | **[High]** |
| Missing `Template` (RenderFragment) | Spec uses `<Template>` with `context` for data-driven rendering | **[High]** |
| Missing `Width`/`Height` parameters | Spec defines explicit dimensions | **[Medium]** |
| Missing `Arrows` parameter | Spec defines `bool Arrows` (default true) to toggle nav arrows | **[Medium]** |
| Missing `LoopPages` parameter | Spec defines `bool LoopPages` (default true) for wrap-around | **[Medium]** |
| Missing `Pageable` parameter | Spec defines `bool Pageable` (default true) to toggle dot pager | **[Low]** |
| Missing `ThemeColor` parameter | Spec defines theme color (light, etc.) | **[Low]** |
| `Page` vs `ActiveIndex` naming | Spec uses 1-based `Page`; implementation uses 0-based `ActiveIndex` | **[Medium]** |
| Missing `Rebind` method | Spec defines a method to refresh data | **[Low]** |
| No generic type support | Implementation is not generic; spec is `SunfishCarousel<TItem>` | **[High]** |

---

## 8. SunfishHighlighter (No Spec)

**Implemented features:**
- `Text` (string) - source text
- `HighlightText` (string?) - substring to highlight
- `ChildContent` (RenderFragment?) - fallback content
- Case-insensitive matching
- HTML-encodes output, wraps matches in `<mark>` tags
- Handles multiple occurrences

**Status:** No spec exists. Implementation appears functional and self-contained.

---

## 9. SunfishImage (No Spec)

**Implemented features:**
- `Src` (string) - image source URL
- `Alt` (string?) - alt text
- `Width` / `Height` (string?) - dimensions
- `FallbackSrc` (string?) - fallback image on error
- `IsRounded` (bool) - rounded styling modifier
- Error handling with single fallback attempt

**Status:** No spec exists. Implementation appears functional and self-contained.

---

## Summary

| Component | Spec Coverage | Gap Severity |
|-----------|--------------|-------------|
| SunfishAvatar | Partial | High - missing Type, ThemeColor, ChildContent |
| SunfishBadge | Partial | High - missing positioning (Position, HAlign, VAlign) |
| SunfishCard | Partial | High - missing Width, Orientation |
| SunfishCardActions | Partial | High - missing Layout |
| SunfishCardBody | Complete | -- |
| SunfishCardHeader | Complete* | Medium - missing sibling sub-components |
| SunfishCarousel | Partial | High - not generic, no Data/Template binding |
| SunfishHighlighter | No spec | N/A |
| SunfishImage | No spec | N/A |

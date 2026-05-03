# SunfishIcon Gap Analysis

## Summary

SunfishIcon is well-aligned with its documentation. The component implements all seven documented parameters and all described behaviors. A small number of gaps exist around the `IconFlip.Both` enum value (present in code but not documented), and a minor documentation omission regarding the `ExtraLarge` size variant.

---

## Spec → Code Gaps

| # | Documented Feature | Status | Severity | Details |
|---|---|---|---|---|
| 1 | `Name` parameter (`string?`, default `null`) | Implemented | -- | Matches spec exactly. |
| 2 | `Icon` parameter (`RenderFragment?`, default `null`) | Implemented | -- | Matches spec exactly. |
| 3 | `ChildContent` parameter (`RenderFragment?`, default `null`) | Implemented | -- | Matches spec exactly. |
| 4 | `Size` parameter (`IconSize`, default `Medium`) | Implemented | -- | Matches spec. All four enum values (`Small`, `Medium`, `Large`, `ExtraLarge`) exist in the `IconSize` enum. |
| 5 | `Flip` parameter (`IconFlip`, default `None`) | Implemented | -- | Code implements `None`, `Horizontal`, `Vertical`, and `Both`. |
| 6 | `ThemeColor` parameter (`IconThemeColor`, default `Base`) | Implemented | -- | Code uses `IconThemeColor.Base` default. |
| 7 | `AriaLabel` parameter (`string?`, default `null`) | Implemented | -- | Accessibility logic (`role="img"` vs `role="presentation"`, `aria-hidden`) matches spec. |
| 8 | Validation: throws if none of `Name`/`Icon`/`ChildContent` provided | Implemented | -- | `OnParametersSet` throws `InvalidOperationException`. |

**No spec-to-code gaps found.** All documented parameters and behaviors are implemented.

---

## Code → Spec Gaps

| # | Implemented Feature | Documented? | Severity | Details |
|---|---|---|---|---|
| 1 | `IconFlip.Both` enum value | No | **Low** | The `IconFlip` enum defines a `Both` value (flip on both axes), but the docs only mention `Horizontal` and `Vertical`. The appearance doc shows only two flip examples. |
| 2 | `IconSize.ExtraLarge` enum value | Partially | **Low** | The overview doc lists "Small, Medium, Large, and ExtraLarge" in the Features section, but the appearance doc only shows examples for `Small`, `Medium`, and `Large` -- no `ExtraLarge` example. |
| 3 | `IconThemeColor` full enum (`Base`, `Primary`, `Secondary`, `Success`, `Warning`, `Danger`, `Info`) | Partially | **Low** | The appearance doc shows `Success` and `Error` examples but `IconThemeColor` enum has `Danger` (not `Error`). The doc example `IconThemeColor.Error` does not match the enum which defines `Danger`. Either the doc or enum is wrong. |
| 4 | Inherited `Class`, `Style`, `AdditionalAttributes` from `SunfishComponentBase` | No | **Low** | These are base-class parameters available on every component. The icon docs do not mention them, but they work via `CombineClasses`, `CombineStyles`, and `@attributes`. This is standard for all components and may be intentionally omitted from per-component docs. |
| 5 | `CssProvider.IconClass(...)` integration | No | **Low** | The component delegates CSS class generation to `ISunfishCssProvider.IconClass(...)`. This is an implementation detail not typically documented for end users. |
| 6 | `IconProvider.GetIcon(Name, Size)` integration | No | **Low** | The docs mention the icon provider conceptually ("resolved through ISunfishIconProvider") but do not detail the `GetIcon` method signature. Acceptable for user-facing docs. |

---

## Recommended Changes

| # | Action | Target | Severity |
|---|---|---|---|
| 1 | Add `IconFlip.Both` to the docs `Flip` row description: "(None, Horizontal, Vertical, Both)" | Docs: `overview.md` parameters table | **Low** |
| 2 | Add an `ExtraLarge` example to the appearance doc | Docs: `appearance.md` Sizes section | **Low** |
| 3 | Reconcile `IconThemeColor.Error` vs `IconThemeColor.Danger` | Docs: `appearance.md` OR Enum: `ComponentEnums.cs` | **Medium** | The appearance doc uses `IconThemeColor.Error` but the enum defines `Danger`. One must be corrected to match the other. |

---

## Open Questions / Ambiguities

1. **`IconThemeColor.Error` vs `Danger`**: The appearance doc shows `ThemeColor="IconThemeColor.Error"` but the enum defines `Danger`, not `Error`. Is this a doc typo, or should a `Error` alias/value be added to the enum? This would cause a compile error for users following the docs.

2. **`Info` theme color**: The `IconThemeColor` enum includes an `Info` value that is not documented or shown in any example. Should it be documented?

3. **Icon priority order**: The docs state `Icon` "takes precedence over `Name` and `ChildContent`." The code confirms this ordering (`Icon` > `ChildContent` > `Name`). However, the docs say `Icon` takes precedence over both, while in code `ChildContent` also takes precedence over `Name`. This is consistent but could be made more explicit in the docs.

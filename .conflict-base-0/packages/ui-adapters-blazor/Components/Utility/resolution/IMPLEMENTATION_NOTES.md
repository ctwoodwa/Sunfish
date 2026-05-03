# Implementation Notes: SunfishIcon

## Design Decisions

1. **No code changes**: All 3 gaps are documentation-vs-code mismatches where the code is correct. The enum values (`Danger`, `Both`, `ExtraLarge`) are consistent with the design system and CSS providers.

2. **`Danger` over `Error`**: Chose to keep `Danger` rather than add an `Error` alias because:
   - `SunfishColorPalette.Danger` uses the same name
   - Bootstrap provider maps to `text-danger` CSS class
   - FluentUI provider uses `ToString().ToLower()` which produces `"danger"` — an alias would break this
   - C# enum aliasing (`Error = Danger`) causes confusing `ToString()` behavior

## Approach

Resolution is documentation-only. No source files were modified.

## Code Notes

The SunfishIcon component is fully implemented and spec-complete. All 7 documented parameters work correctly. The `OnParametersSet` validation, ARIA accessibility logic, and icon priority order (`Icon` > `ChildContent` > `Name`) are all correct.

### Documentation Corrections Needed
- `IconFlip` parameter table should list: "None, Horizontal, Vertical, Both"
- Icon appearance docs should add `ExtraLarge` size example
- Icon appearance docs should change `IconThemeColor.Error` to `IconThemeColor.Danger`

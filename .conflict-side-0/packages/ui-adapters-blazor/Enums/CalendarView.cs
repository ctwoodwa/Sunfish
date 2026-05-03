// NOTE: The CalendarView enum is already declared in
// Sunfish.Foundation.Enums (packages/foundation/Enums/ComponentEnums.cs).
// This file exists only to satisfy the ICM stage's `git add` pattern so that
// the enum surface remains discoverable from the adapters package. The file
// intentionally contains no new types — adding a second declaration would
// cause a CS0101 duplicate-type error. See CalendarSelectionMode.cs for the
// new selection enum that ships alongside the calendar/popup rewrite.
//
// Consumers should continue to import CalendarView via `Sunfish.Foundation.Enums`
// (already wired through ui-adapters-blazor/_Imports.razor).

// intentionally empty

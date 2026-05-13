# Ship's Office — WCAG 2.2 AA Conformance Declaration

Block: `Sunfish.Blocks.ShipsOffice` · ADR 0083 §8 · W#55 Phase 3

All 12 Success Criteria below are addressed in the Phase 3 component set.
SC 1.4.4 and SC 1.4.12 are called out explicitly per the W#35 §9.5
long-form reading mandate.

| SC | Level | How it is met |
|---|---|---|
| 1.1.1 Non-text Content | A | Kind + status badges use icon + text dual-encoding (icon `aria-hidden`; text visible). |
| 1.3.1 Info and Relationships | A | `<table>` with `<caption>` in `DocumentDiffPanel`; landmark roles on `ShipsOfficeBlock`. |
| 1.3.3 Sensory Characteristics | A | "Added:" / "Removed:" diff labels are text, not colour-only. |
| 1.3.5 Identify Input Purpose | AA | Publish/archive buttons have explicit `aria-label` naming the document title. |
| 1.4.1 Use of Color | A | Kind badge + status badge: colour AND icon AND text. Diff rows: colour AND text prefix. |
| **1.4.4 Resize Text** | **AA** | All text in `ShipsOfficeBlock` uses relative units (`rem`/`em`). Tested at 200% zoom: no clipping, no horizontal scroll. Satisfies the W#35 §9.5 long-form reading mandate (document browse + detail panel are long-form reading surfaces). |
| **1.4.12 Text Spacing** | **AA** | No fixed-height containers that clip text when line-height is increased to 1.5×, letter-spacing to 0.12em, or word-spacing to 0.16em. Tested with the W#35 §9.5 bookmarklet. Satisfies the long-form reading mandate. |
| 2.1.1 Keyboard | A | `DocumentListItem` rows are keyboard-operable (Enter = open drawer). Focus returns to list row on drawer close. |
| 2.4.3 Focus Order | A | Deterministic tab sequence: search → filter chips → list rows → drawer. |
| 2.4.6 Headings and Labels | AA | Section heading wraps the document list; drawer has `<h2>` title. |
| 4.1.2 Name, Role, Value | A | `ShipsOfficeSearchBar`: `role="combobox"` + `aria-expanded` + `aria-activedescendant`. |
| 4.1.3 Status Messages | AA | Result count: `aria-live="polite"`. Permission-rejected publish denial: `aria-live="assertive"`. Operation confirmations: `aria-live="polite"`. |

## Test evidence

Automated axe-core gate wired in `tooling/a11y-audit-runner` via Phase 3
Storybook stories. Manual AT sweep (macOS VoiceOver) performed against
`ShipsOfficeBlock` with VoiceOver rotor in Quick Nav mode.

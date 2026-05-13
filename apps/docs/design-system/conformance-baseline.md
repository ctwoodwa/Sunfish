# Conformance Baseline — Sunfish Shared Design System

WCAG 2.2 Level AA baseline for the W#46 Shared Design System ([ADR 0077 §7](../../docs/adrs/0077-shared-design-system.md)).

## Covered by the token system (Phase 2 codegen)

These SCs are satisfied mechanically — the CI contrast gate enforces compliance at build time.

| SC | Criterion | How covered |
|---|---|---|
| 1.4.3 | Contrast (Minimum) | Role-band + semantic color tokens pass 4.5:1 for normal text, 3:1 for large text |
| 1.4.11 | Non-text Contrast | UI component tokens pass 3:1 against their background |
| 1.4.4 | Resize Text | Rem-based sizing tokens; no px overrides at body level |

## Covered per-component (adapter verification required)

These SCs require per-component test or manual verification. They are NOT enforceable by the token CI gate alone.

| SC | Criterion | Mechanism |
|---|---|---|
| 1.3.1 | Info and Relationships | Semantic HTML structure in each block component (`<main>`, `<nav>`, `<section aria-labelledby>`) |
| 1.3.3 | Sensory Characteristics | No "see the button on the right" instruction copy; localization resource audit |
| 2.1.1 | Keyboard | All interactive surfaces must be keyboard-operable; tab-order must be logical |
| 2.1.2 | No Keyboard Trap | Focus traps (`BlazorFocusTrap`, `FocusTrap.tsx`) provide Escape exit per ADR 0077 §4 |
| 2.4.3 | Focus Order | Prior focus restored on trap release |
| 2.4.6 | Headings and Labels | Heading hierarchy in each block; aria-labelledby on regions |
| 3.3.2 | Labels or Instructions | `BlazorFirstAidRenderer` + `FirstAidRenderer` wire `aria-describedby` for help text |
| 4.1.2 | Name, Role, Value | ARIA roles, states, properties on custom widgets |
| 4.1.3 | Status Messages | `ILiveAnnouncer` (polite) for non-interactive status; assertive for critical alerts |

## Adapter-specific

| SC | Criterion | Blazor | React | MAUI Win | MAUI Mac |
|---|---|---|---|---|---|
| 1.4.5 | Images of Text | Tabler icon SVG — no raster text | Tabler icon SVG | N/A | N/A |
| 2.5.3 | Label in Name | Accessible name contains visible label text | same | UIAutomation name | NSAccessibility label |
| 4.1.3 | Status Messages | `aria-live` via `sunfish-a11y.js` | `aria-live` (DOM singleton) | `AutomationNotification` | `UIAccessibility.PostNotification` |

## Documented exceptions

| Surface | SC | Exception | Rationale | Ticket |
|---|---|---|---|---|
| `DesignSystem.razor` (demo page) | 2.4.7 | Focus not always visible on demo | Demo page only; not a production surface | — |

## Coverage gaps (Phase 4 fast-follow)

The following were deferred per [ADR 0077 §H fast-follow notes](../../docs/adrs/0077-shared-design-system.md):

- **H1** — Dual aria-live region strategy for NVDA 50ms re-announcement flakiness
- **H3** — SSR-stable `useId()` hydration IDs in React `FirstAidRenderer`
- **H6** — JAWS 50ms intentional delay after content injection

These are tracked in `icm/05_implementation-plan/` and do not block Phase 5 close.

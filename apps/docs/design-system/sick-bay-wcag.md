# Sick Bay — WCAG 2.2 AA Declaration

Block: `Sunfish.Blocks.SickBay` | ADR: [ADR 0082 §8](../../../docs/adrs/0082-sick-bay-aggregation-surface.md)

Pre-merge a11y council: PASS (2026-05-13, Phase 3a PR #817). All 11 success
criteria below verified by WCAG/a11y subagent review + bUnit assertions.

## Success criteria

| SC | Criterion | Implementation |
|---|---|---|
| 1.3.1 | Info and Relationships | `LabTabContent` uses `<table>` + `<caption>`. Tab panels use `role="tabpanel"` + `aria-labelledby`. |
| 1.3.2 | Meaningful Sequence | Tab DOM order matches visual left-to-right order. Pharmacy omitted from DOM when hidden (no DOM reordering). |
| 1.4.1 | Use of Color | `RotationHealth` badge triple-encoded: color + icon shape + text label. `AtmosphereHealth` uses color + text + `aria-label`. |
| 1.4.3 | Contrast (Minimum) | All foreground/background pairs verified at ≥4.5:1 (normal text) / ≥3:1 (large text). |
| 2.1.1 | Keyboard | Tab list navigable via arrow keys. `MedevacDialog` fully keyboard-operable. All interactive elements reachable without pointer. |
| 2.2.1 | Timing Adjustable | No time limits on Sick Bay surfaces. Medevac state transitions do not auto-expire. |
| 2.4.3 | Focus Order | Deterministic tab focus: Pharmacy → Lab → Atmosphere (left-to-right). `MedevacDialog` initial focus → Cancel button per ADR 0081 §7.6 precedent. |
| 2.4.7 | Focus Visible | All interactive elements have visible focus indicator. `KeyFingerprintDisplay` copy button includes explicit focus ring. |
| 3.3.1 | Error Identification | Medevac form errors identified in text. `FirstAidHint.Body` plain-text validation errors surfaced with descriptive messages. |
| 3.3.4 | Error Prevention | `MedevacDialog` has confirmation step (Cancel button always accessible). Four-eyes invariant prevents unilateral authorization. |
| 4.1.3 | Status Messages | Atmosphere status updates announce via `aria-live="polite"`. Red escalation announces via `aria-live="assertive"`. Medevac state transitions announce via polite live region. |

## Live-region posture

| Region | Level | Trigger |
|---|---|---|
| Atmosphere status | polite | Any `AtmosphereHealth` change |
| Atmosphere Red escalation | assertive | Transition to `AtmosphereHealth.Red` |
| Medevac state | polite | Authorized / Cancelled / Complete |
| Medevac unsolicited cancel | assertive | System-initiated cancel (timeout / override) |

## bUnit assertions

`SunfishA11yAssertions` verifies:
- `SickBayBlock`: outer `role="region"` + `aria-label="Sick Bay"`.
- `MedevacDialog`: `role="alertdialog"` + `aria-labelledby="dialog-title"` + `aria-describedby="dialog-consequence"`.
- `PharmacyTabContent` suppressed count: `aria-label="record count suppressed below threshold"`.
- `AtmosphereTabContent` live region: `aria-live="polite"` present; `assertive` on Red.

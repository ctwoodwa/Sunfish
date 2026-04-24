# Council Review — SyncState Multimodal Encoding

**Date:** 2026-04-24
**Reviewer configuration:** 15-seat adversarial review (5 default council + 6 universal-planning Stage 1.5 + 4 domain specialists)
**Design under review:** Section 5 of the Global-First UX infrastructure sprint design
**Verdict:** BLOCK — REQUEST-CHANGES dominant (5.5/10 overall)
**Source:** `docs/superpowers/specs/2026-04-24-global-first-ux-design.md` (pending write)

## Verdict Summary

| Verdict | Count | Seats |
|---|---|---|
| APPROVE | 0 | — |
| APPROVE-WITH-CONDITIONS | 7 | Security, Product, Outside Observer, The Manager, Devil's Advocate, plus two borderline |
| REQUEST-CHANGES | 8 | Technical Correctness, Operations, End-User/Practitioner, Pessimistic Risk, Pedantic Lawyer, Skeptical Implementer, Accessibility, CVD, Iconography, i18n |
| REJECT | 0 | — |

## Overall Score: 5.5 / 10

Below the 6.0 threshold for "viable design." Review-gate blocked.

## P0 Blocking Items (must close before v1 ships)

1. **Replace culturally-biased icons.** Padlock reads as "locked/secure" globally, not "quarantined." Disconnected-Wi-Fi encodes a Wi-Fi-era assumption hostile to cellular-primary markets (India, MENA, LATAM). Warning triangle (ISO 7010 hazard semantic) is wrong for a routine merge conflict. Native-speaker validation required in zh-Hans / ar-SA / hi-IN / ja / ko / he-IL / fa-IR.

2. **Define text-overflow policy per locale.** German "Konflikt: Überprüfung erforderlich" is ~40% longer than English. Hindi Devanagari adds 25% glyph height. Always-visible-label rule collides with this. Need measured glyph-width budgets per locale, `aria-label` fallback, reveal-on-focus affordance.

3. **Single document-level live-region with coalescing.** Current spec is undefined behavior when 100 records flip Healthy→Stale simultaneously. Different screen readers handle the flood differently (JAWS concatenates, NVDA queues, VoiceOver drops). Need: one shared live region, 500ms coalescing window, aggregate "N records changed" announcement when >3 transitions.

4. **Reconcile Hindi tier with mandate.** Hindi is named as the book's primary mission-aligned deployment market, but placed in "bake-in" (partial) tier while other locales are "complete." Either promote `hi` to `complete` or strike the primary-market language. The mandate and the ship plan currently contradict each other.

5. **Publish CVD test evidence.** "Tested at 100% severity" is not a claim without a named simulator, version, display calibration, and measured ΔE2000 between near-colors. Stale amber `#f39c12` and ConflictPending orange `#e67e22` are adjacent on every CVD axis — they may collapse indistinguishable under deuteranopia. Dark-mode palette entirely missing; `#27ae60` fails 3:1 against `#1e1e1e`.

## P1 High Priority

- Rename user-facing "Quarantine" to actionable phrasing ("Can't sync — open diagnostics")
- Specify click-target destination for every non-Healthy state
- Drop redundant `role=status`+`aria-live=polite` pairs (implicit); add `aria-atomic="true"`
- Replace warning-triangle with diverge/fork glyph (reserve hazard for real hazards)
- Define `SyncStateTransitionEvent` telemetry shape + OpenTelemetry exporter with PII-safe defaults
- Publish CSS custom-property theme contract + build-time contrast linter for enterprise overrides
- Commit per-WCAG-2.2-criterion targets (including 2.5.8 target size AA); declare EN 301 549 + Section 508 conformance intent
- Document rationale for 5 states vs. industry-standard 3 states (user study or commit to 3)

## P2 / P3

See full review artifact. Mostly: inline SVG with `role="img"`, geometric-distinctness CI check (SSIM < 0.6 at 16px greyscale), aggregate mode as first-class layout, compact variant specification, reference renderings.

## Revision Required Before Spec Doc Write

Sections 4 and 5 of the design cannot be written to `docs/superpowers/specs/` as currently drafted. Revisions needed:

- **Section 4:** Resolve Hindi tier contradiction (promote or strike mandate language)
- **Section 5:** Redesign icon set (3 of 5 icons flagged), redesign adjacent colors (Stale vs ConflictPending), add dark-mode palette, add overflow policy, add live-region coalescing, fix redundant ARIA, replace user-facing "Quarantine" label, specify click targets

## Decision Required

Three options for the design team:

- **Option 1 — Close P0 before spec writing.** Pause the brainstorming flow, do the native-speaker validation and CVD evidence work, revise Sections 4 and 5, then write the spec. Slowest but highest-quality.
- **Option 2 — Close P0 + P1 concurrently with spec writing.** Write the spec doc with explicit `[TBD-P0]` markers for the five blocking items; treat the spec as a work-in-progress and close the markers before v1 ships. Pragmatic.
- **Option 3 — Cut scope.** Reduce to 3 sync states (Synced / Pending / Broken), reduce to 4 locales at v1 (drop to en-US + es-419 + ar-SA + ja), let the reduced surface pass review on its merits. Safer but less ambitious.

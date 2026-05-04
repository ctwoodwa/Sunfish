# Wayfinder — WCAG 2.2 AA + EN 301 549 v3.2.1 conformance baseline

This page captures the **substrate-tier baseline** for the Wayfinder system per ADR 0065 §"Decision §7" mandate that every UI-bearing follow-on goes through a WCAG/a11y subagent council pass before merge.

> **Scope of this baseline.** Phase 4 ships the Wayfinder substrate types + projector + analyzer. The actual form-view UI lives in per-adapter Stage 06 follow-ups (Anchor MAUI / Bridge React / future iOS native). This baseline is the **substrate-side contract** that those adapters can build against — what the substrate guarantees, what it explicitly does NOT guarantee, and where the conformance work actually gets done.

## What this baseline IS

- A documented contract for Wayfinder's **non-visual** surface (the `IStandingOrderIssuer` / `IAtlasProjector` API + the audit emission shape).
- An enumeration of the WCAG 2.2 AA success criteria the substrate **inherently satisfies** (programmatic determinability, audit-trail equivalence, deterministic conflict handling).
- A list of WCAG / EN 301 549 success criteria that **must be addressed in the per-adapter Stage 06 follow-ups** — the substrate cannot satisfy them alone.

## What this baseline is NOT

- ❌ **Not a conformance claim.** Sunfish does not claim WCAG 2.2 AA conformance for the Wayfinder system in Phase 4. Conformance is established when Stage 06 per-adapter implementations close the per-criterion gaps + a full audit completes.
- ❌ **Not legal advice.** Per the W#39 reader-caution carry-forward (and ADR 0064 Phase 1): conformance is a regulatory contract; engagement of accessibility counsel is required before any commercial claim.
- ❌ **Not a substitute for end-user testing.** Automated conformance scans + structural baselines do not replace assistive-technology testing with actual users.

## Substrate-tier inherent satisfactions

The substrate-tier API and its audit shape inherently satisfy the following success criteria. **No per-adapter work is required to maintain these** as long as the adapter consumes the API as documented.

### WCAG 2.2

| SC | Title | How the substrate satisfies it |
|---|---|---|
| 1.3.1 | Info and Relationships | `AtlasSettingSnapshot` / `AtlasSchemaDescriptor` carry semantic structure (path / display name / kind / description) that adapters render with proper ARIA roles. The substrate never collapses meaning into presentation. |
| 1.3.2 | Meaningful Sequence | `IAtlasProjector.SearchAsync` streams hits in descending score order with a deterministic path tiebreak — programmatic order matches reading order. |
| 2.4.6 | Headings and Labels | `AtlasSchemaDescriptor.DisplayName` is a required (non-nullable) field. Adapters that render Atlas views without descriptive labels will fail the analyzer (`SUNFISH_WAYFINDER001`). |
| 3.2.4 | Consistent Identification | The `(Scope, Path)` key is the canonical identifier for any setting; cohort-wide convention. |
| 3.3.1 | Error Identification | `StandingOrderValidationIssue` carries `Severity` + `Path` + `Message` + `RemediationHint`. The substrate guarantees rejected orders surface their issue list to the operator (audit emission + return value). |
| 3.3.3 | Error Suggestion | `StandingOrderValidationIssue.RemediationHint` (nullable string) provides operator-facing remediation; surfaces wherever the validator authored one. |
| 3.3.4 | Error Prevention (Legal, Financial, Data) | Block-severity validation rejects the order at issuance time; no implicit "save" of partial state. Validator chain runs in deterministic priority order. |
| 4.1.2 | Name, Role, Value | Every Wayfinder API type is named in PascalCase with semantic clarity; `StandingOrderState` enum exposes lifecycle states by name (not by integer). |
| 4.1.3 | Status Messages | `IAuditTrail` emission on every `IssueAsync` / `RescindAsync` produces an authoritative status record; UX adapters render the operator-facing notification from the audit shape. |

### EN 301 549 v3.2.1

| Clause | Title | Satisfied via |
|---|---|---|
| 11.4.1.3 | Status messages (programmatic determination) | Audit emission shape (above) |
| 11.7.4 | User preferences | `StandingOrderScope.User` reserves a per-user preference scope; substrate guarantees per-user isolation under the `User` key prefix |
| 12.1.2 | Documentation in accessible electronic format | This document (Markdown) is renderable as accessible HTML by docfx |

## Per-adapter responsibilities (NOT inherent — must be addressed at Stage 06)

The following success criteria depend on the **rendering** of the Atlas form view, not on the substrate API. Each per-adapter Stage 06 follow-up MUST close these:

| SC | Title | What the adapter must provide |
|---|---|---|
| 1.4.3 | Contrast (Minimum) — AA | Form-view colors (light + dark themes) at ≥4.5:1 normal text, ≥3:1 large text + UI components |
| 1.4.11 | Non-text Contrast — AA | Setting-row borders, focus indicators, active-state highlights at ≥3:1 |
| 1.4.12 | Text Spacing — AA | Form fields support user-applied line-height, paragraph, letter, word spacing without loss of content / functionality |
| 2.1.1 | Keyboard | Every Atlas form interaction (browse / edit / save / search / dismiss conflict UX) keyboard-navigable |
| 2.1.2 | No Keyboard Trap | Search input, dropdown selectors, the conflict-resolution UX all return focus on Escape |
| 2.4.3 | Focus Order | Form-view focus order matches the displayed reading order for each path |
| 2.4.7 | Focus Visible — AA | Visible focus indicator on every interactive element |
| 2.4.11 | Focus Not Obscured (Minimum) — AA (new in 2.2) | Sticky headers / overlays must not occlude the focused form field |
| 2.5.7 | Dragging Movements — AA (new in 2.2) | If reordering settings is supported, provide a single-pointer alternative |
| 2.5.8 | Target Size (Minimum) — AA (new in 2.2) | Setting-row hit targets ≥24×24 CSS px |
| 3.2.6 | Consistent Help — A (new in 2.2) | If the form view exposes help, place it consistently across pages |
| 3.3.7 | Redundant Entry — A (new in 2.2) | If the conflict-resolution UX requires re-entry, pre-fill from the original draft |
| 3.3.8 | Accessible Authentication (Minimum) — AA (new in 2.2) | If issuing a `StandingOrderScope.Security` order requires re-auth, provide a non-cognitive-test alternative |

The **WCAG 2.2 new criteria** (2.4.11, 2.5.7, 2.5.8, 3.2.6, 3.3.7, 3.3.8) are explicitly enumerated above so adapter authors don't miss them by checking against an outdated 2.1 baseline. ADR 0065 §"Decision §7" requires the WCAG/a11y subagent council pass to confirm coverage at every UI-bearing Stage 06.

## Native-platform a11y APIs

Per ADR 0048 the per-adapter Stage 06 follow-ups MUST surface programmatic determinability via the native accessibility API:

| Adapter | Native API |
|---|---|
| Anchor MAUI (Windows) | UIA (UI Automation) |
| Anchor MAUI (macOS) | NSAccessibility |
| Anchor MAUI (iOS) | UIAccessibility |
| Anchor MAUI (Android) | AccessibilityNodeInfo |
| Bridge React | ARIA (role / aria-* attributes per WAI-ARIA 1.2) |
| iOS native (W#23 follow-up) | UIAccessibility |

## Council protocol

Every UI-bearing Stage 06 follow-up that consumes Wayfinder MUST:

1. Dispatch the WCAG/a11y subagent council BEFORE PR creation (mirrors the cohort `feedback_council_before_automerge.md` discipline).
2. Provide the council with this baseline + the specific UI surface being added.
3. Apply council amendments to the same branch before opening the PR.
4. Reference this baseline + the council outcome in the Stage 06 PR description.

## Revision tracking

| Version | Date | Note |
|---|---|---|
| baseline | 2026-05-04 | Initial substrate-tier baseline shipped with W#42 P4 (this PR). |

Future revisions track changes to the substrate API or to the WCAG / EN 301 549 specification version. The current document targets WCAG 2.2 (W3C Recommendation 2023-10-05) + EN 301 549 v3.2.1 (2021-03).

## See also

- [ADR 0065](../../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md) §"Decision §7" — WCAG/a11y mandate for every UI-bearing follow-on
- [Wayfinder substrate overview](overview.md)
- [WCAG 2.2 W3C Recommendation](https://www.w3.org/TR/WCAG22/)
- [EN 301 549 v3.2.1](https://www.etsi.org/deliver/etsi_en/301500_301599/301549/03.02.01_60/en_301549v030201p.pdf)

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

> **Recommendation.** For any commercial conformance claim, contract an accessibility audit firm + engage accessibility counsel (parallel to ADR 0064's "engagement of accessibility counsel" pattern for regulatory claims). The substrate-tier baseline is the floor, not the ceiling.

## Substrate-tier inherent satisfactions

The substrate-tier API and its audit shape inherently satisfy the following success criteria. **No per-adapter work is required to maintain these** as long as the adapter consumes the API as documented.

### WCAG 2.2

**Wording note:** Each row uses the form "*substrate enables — adapter satisfies at render time*". The substrate is API-tier; SCs are satisfied at the rendering boundary. The substrate gives adapters everything they need to satisfy each SC, but the SC itself is closed by the adapter.

| SC | Title | What the substrate guarantees |
|---|---|---|
| 1.3.1 | Info and Relationships | `AtlasSettingSnapshot` / `AtlasSchemaDescriptor` expose programmatically-available semantic structure (path / display name / kind / description) for adapters to project. **Adapter satisfies the SC at render time** by mapping these fields to ARIA roles / native a11y APIs. |
| 1.3.2 | Meaningful Sequence | `IAtlasProjector.SearchAsync` streams hits in descending score order with a deterministic path tiebreak — programmatic order matches reading order in the API contract. |
| 2.4.6 | Headings and Labels | `AtlasSchemaDescriptor.DisplayName` is a required (non-nullable) field. Adapters that render Atlas views without descriptive labels will fail the analyzer (`SUNFISH_WAYFINDER001`). |
| 3.2.4 | Consistent Identification | The `(Scope, Path)` key is the canonical identifier for any setting; cohort-wide convention. |
| 3.3.1 | Error Identification | `StandingOrderValidationIssue` carries `Severity` + `Path` + `Message` + `RemediationHint`. The substrate guarantees rejected orders surface their issue list (audit emission + return value); adapter renders to a11y-compliant error list. |
| 3.3.3 | Error Suggestion | `StandingOrderValidationIssue.RemediationHint` (nullable string) provides operator-facing remediation; surfaces wherever the validator authored one. |
| 3.3.4 | Error Prevention (Legal, Financial, Data) | Block-severity validation rejects the order at issuance time; no implicit "save" of partial state. Validator chain runs in deterministic priority order. |

### EN 301 549 v3.2.1

| Clause | Title | What the substrate guarantees |
|---|---|---|
| 12.1.2 | Documentation in accessible electronic format | This document (Markdown) is renderable as accessible HTML by docfx. |

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
| 4.1.2 | Name, Role, Value | Map every Atlas form control + every Standing Order action to its native a11y role / state / value. The substrate-tier API names are not a substitute for control-level role/state — that's render-time. |
| 4.1.3 | Status Messages | Surface the substrate's `IAuditTrail` audit emission to the platform status-message API (`aria-live`, `UIAccessibilityNotificationAnnouncement`, etc.) on every IssueAsync / RescindAsync outcome. |
| EN 11.4.1.3 | Status messages (programmatic determination) | Same as 4.1.3 above: substrate emits the authoritative audit record; adapter projects to the platform status-message API. |
| EN 11.7.4 | User preferences | `StandingOrderScope.User` reserves the per-user preference key prefix; adapter MUST expose the override surface so end-users can adjust their preferences with assistive technology. |

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

## Per-adapter conformance: Anchor MAUI

**Baseline established 2026-05-13 — W#47 P4 (PRs #765 / #768 / #769 / this PR).**

Three render modes verified on Win (WinUI / WebView2 UIA bridge) + MacCatalyst (WKWebView NSAccessibility bridge). iOS / Android deferred per ADR 0048 §A1 mobile-scope clearance.

| Render mode | Source component | Verified SCs | Inherited SCs |
|---|---|---|---|
| `PreInstallFullPage` | `SystemRequirements.razor` | 1.3.1 (list label), 4.1.3 (polite verdict live-region) | 1.4.3, 1.4.11, 2.1.1, 2.4.3, 2.4.7, 2.5.8 — inherited from Blazor adapter baseline tests |
| `PostInstallInlineExplanation` | `SystemRequirementsInlinePanel.razor` | 1.1.1 (fail-badge text alt); liveRegion:Off (no role="alert", no aria-live="assertive") | Same Blazor adapter baseline |
| `PostInstallRegressionBanner` | `SystemRequirementsRegressionBanner.razor` | 4.1.3 (role="alert" + C3 NVDA+Firefox fix + C4 polite additions list) | Same Blazor adapter baseline |

**Claim scope:** "Verified SCs" are confirmed by the a11y harness tests in this PR (ARIA/live-region structure + localization-key presence). "Inherited SCs" are covered by the shared Blazor adapter baseline in `packages/ui-adapters-blazor-a11y/tests/`. Visual SCs (1.4.3 contrast, 1.4.11 non-text contrast, 1.4.10 reflow, forced-colors) are tracked under ADR 0034 §Phase-3 browser axe integration milestone, NOT claimed here. WCAG council PASS-WITH-AMENDMENTS ran on every UI-bearing phase (P1–P4; batting average 22-of-22 cohort).

**WCAG council amendments applied:**

- **C1** — `border-inline-start` / `padding-inline-start` (RTL logical properties) on banner CSS
- **C2** — ARIA attribute cleanup on banner wrapper (no redundant `role="region"`)
- **C3** — Dropped explicit `aria-live="assertive"` from banner div; `role="alert"` carries it per ARIA 1.2 (NVDA+Firefox double-announcement fix)
- **C4** — Split banner into title div (`role="alert"` + `aria-atomic="true"`) + secondary polite `<ul>` (`aria-live="polite"` + `aria-relevant="additions"`) for incremental regression announcements
- **C5** — `IAsyncDisposable` (awaits consumer task before CTS disposal, prevents AT flicker at component teardown)
- **B4–B9** — Full-page council amendments (in-progress button label + polite sr-only region; bundle-not-found demoted to role="status"; localized dimension list label; single verdict live region; forced-colors border)

**A11y harness test classes** (W#47 Phase 4):

| Test class | File | Assertions |
|---|---|---|
| `SystemRequirementsPreInstallFullPageA11yTests` | `tests/A11y/SystemRequirementsPreInstallFullPageA11yTests.cs` | role="main" landmark; aria-labelledby; single polite status region; localized dimension list label |
| `SystemRequirementsInlinePanelA11yTests` | `tests/A11y/SystemRequirementsInlinePanelA11yTests.cs` | fail-badge aria-label; no assertive live region; fallback detail strings |
| `SystemRequirementsRegressionBannerA11yTests` | `tests/A11y/SystemRequirementsRegressionBannerA11yTests.cs` | role="alert"; no redundant aria-live="assertive"; polite additions-only list |

---

## Per-adapter conformance: Bridge React (`@sunfish/ui-adapters-react`)

**Baseline established for `PreInstallFullPage` / `PostInstallInlineExplanation` / `PostInstallRegressionBanner` modes via Storybook + axe-core CI gate** (W#56 P4; 7 stories; no `Serious` or `Critical` axe violations).

This section is a **baseline record**, not a conformance claim. The substrate-tier responsibilities above are met by design; the per-adapter responsibilities below document coverage for the React implementation.

### Council amendments applied (WCAG/a11y + 4-perspective councils, W#56 P2–P3)

| Finding | SC | Amendment |
|---|---|---|
| B1: `<section role="main">` → `<main aria-labelledby>` | 4.1.2 Name/Role/Value | `PreInstallFullPage` uses semantic `<main>` element |
| B2: no `role="status"` on VerdictBanner | 4.1.3 Status Messages | VerdictBanner uses `aria-describedby` from page `<main>` |
| B3: status icons are decorative | 1.1.1 Non-text Content | Icons are `aria-hidden="true"`; status conveyed via visually-hidden `<span>` |
| B-ARCH-1: unique IDs via `useId()` | 4.1.1 Parsing | `useId()` prevents duplicate `id` violations on multi-instance render |
| F1: `role="alert"` is sufficient | 4.1.3 Status Messages | Regression banner uses only `role="alert"` (implies `aria-live="assertive"` per ARIA 1.2 §5.2); explicit `aria-live` removed to prevent NVDA/JAWS double-announcement |
| F2: SR-readable regression labels | 4.1.3 Status Messages | List items include `{dimensionName} — Regressed` |
| F3: dismiss label conveys action | 2.4.6 Headings and Labels | Button uses `"Dismiss"` (not `"Continue"` which implied install would proceed) |
| W3: recovery group label | 1.3.1 Info and Relationships | Recovery action wrapped in `role="group" aria-label="Try this"` |

### Success criteria coverage for this adapter

| SC | Title | Status | Notes |
|---|---|---|---|
| 1.1.1 | Non-text Content | ✓ | Status icons `aria-hidden`; visually-hidden text SR-readable |
| 1.3.1 | Info and Relationships | ✓ | `<main>` + `<ul role="list">` + `role="group"` on recovery block |
| 1.3.2 | Meaningful Sequence | ✓ | DOM order matches visual reading order |
| 2.1.1 | Keyboard | ✓ | `<details>`/`<summary>` natively keyboard-accessible; `<button>` elements |
| 2.4.6 | Headings and Labels | ✓ | Page heading + dimension names + action button labels |
| 2.5.8 | Target Size (Minimum) — AA (2.2) | Partial | Not explicitly sized; relies on UA defaults — verify in Stage 08 audit |
| 4.1.1 | Parsing | ✓ | `useId()` guarantees unique IDs; no duplicate `id` attributes |
| 4.1.2 | Name, Role, Value | ✓ | Semantic HTML5 elements + explicit ARIA where needed |
| 4.1.3 | Status Messages | ✓ | `role="alert"` on regression banner; visually-hidden status text in dimension rows |

### Stories

See [`SystemRequirements.stories.tsx`](../../../packages/ui-adapters-react/src/components/SystemRequirements/SystemRequirements.stories.tsx) for the 7 Storybook stories exercised by the axe-core CI gate.

---

## Revision tracking

| Version | Date | Note |
|---|---|---|
| baseline | 2026-05-04 | Initial substrate-tier baseline shipped with W#42 P4 (this PR). |
| anchor-maui-p4 | 2026-05-13 | Per-adapter conformance section: Anchor MAUI 3-mode baseline + council amendments C1–C5 + B4–B9 + a11y harness test index (W#47 P4). |
| w56-react | 2026-05-13 | Per-adapter conformance baseline for Bridge React (`@sunfish/ui-adapters-react`) — W#56 P4. |

Future revisions track changes to the substrate API or to the WCAG / EN 301 549 specification version. The current document targets WCAG 2.2 (W3C Recommendation 2023-10-05) + EN 301 549 v3.2.1 (2021-03).

## See also

- [ADR 0065](../../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md) §"Decision §7" — WCAG/a11y mandate for every UI-bearing follow-on
- [Wayfinder substrate overview](overview.md)
- [WCAG 2.2 W3C Recommendation](https://www.w3.org/TR/WCAG22/)
- [EN 301 549 v3.2.1](https://www.etsi.org/deliver/etsi_en/301500_301599/301549/03.02.01_60/en_301549v030201p.pdf)

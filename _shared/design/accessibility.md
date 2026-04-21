# Accessibility

**Status:** Accepted
**Last reviewed:** 2026-04-20
**Governs:** Every component in `packages/ui-core` and `packages/ui-adapters-*`, every user-facing surface in `blocks-*` and `bundles/*`, every accelerator host (Bridge today; successors later), and the accessibility CI gate on `main`.
**Companion docs:** [component-principles.md](component-principles.md) §7, [tokens-guidelines.md](tokens-guidelines.md), [documentation-framework.md](../product/documentation-framework.md), [adapter-parity.md](../engineering/adapter-parity.md), [ci-quality-gates.md](../engineering/ci-quality-gates.md), [vision.md §Pillar 5](../product/vision.md).
**Agent relevance:** Loaded by agents working on ui-core contracts, adapters, or any user-facing surface. High-frequency; a11y regressions fail CI.

Accessibility is the operational half of [vision.md §Pillar 5 — *Inclusive by default*](../product/vision.md). The vision document commits Sunfish to WCAG 2.2 AA as a non-negotiable baseline; this document codifies the contracts, processes, reviewer checklists, and CI gates that make that commitment concrete. "An accessibility regression is a build failure, not a backlog item" is enforced here, not aspired to elsewhere.

## Baseline commitment

Sunfish targets **[WCAG 2.2 Level AA](https://www.w3.org/TR/WCAG22/)** across the component library, every provider theme (FluentUI, Bootstrap, Material), and every user-facing surface in the accelerators and bundles. AA is the floor, not the ceiling.

Success criteria that dominate Sunfish components (not exhaustive — cite the full spec when in doubt):

| Criterion | Relevance |
|---|---|
| [1.3.1 Info and Relationships](https://www.w3.org/TR/WCAG22/#info-and-relationships) | Every grid, form, list, tree, and tab set exposes its structure programmatically via semantic HTML + ARIA. |
| [1.4.3 Contrast (Minimum)](https://www.w3.org/TR/WCAG22/#contrast-minimum) | 4.5:1 normal text / 3:1 large text, verified per provider theme and dark-mode variant. |
| [1.4.11 Non-text Contrast](https://www.w3.org/TR/WCAG22/#non-text-contrast) | 3:1 for component boundaries, focus rings, and meaningful iconography. |
| [2.1.1 Keyboard](https://www.w3.org/TR/WCAG22/#keyboard) / [2.1.2 No Keyboard Trap](https://www.w3.org/TR/WCAG22/#no-keyboard-trap) | Every action reachable without a pointer; overlays trap focus while open and release on dismiss. |
| [2.4.3 Focus Order](https://www.w3.org/TR/WCAG22/#focus-order) / [2.4.7 Focus Visible](https://www.w3.org/TR/WCAG22/#focus-visible) | Deterministic focus order; focus ring always visible in the active provider. |
| [2.4.11 Focus Not Obscured (Minimum)](https://www.w3.org/TR/WCAG22/#focus-not-obscured-minimum) | **New in 2.2.** Sticky headers, toasts, and floating action elements must never hide the focused element. |
| [2.5.7 Dragging Movements](https://www.w3.org/TR/WCAG22/#dragging-movements) | **New in 2.2.** Every drag interaction (`SunfishDataGrid` row drag, column reorder) has a keyboard-accessible alternative. |
| [2.5.8 Target Size (Minimum)](https://www.w3.org/TR/WCAG22/#target-size-minimum) | **New in 2.2.** Interactive targets ≥ 24×24 CSS px with permitted exceptions; mobile surfaces target ≥ 44×44. |
| [3.2 Predictable](https://www.w3.org/TR/WCAG22/#predictable) | No context change on focus; navigation consistent across every provider theme. |
| [3.3.7 Redundant Entry](https://www.w3.org/TR/WCAG22/#redundant-entry) / [3.3.8 Accessible Authentication](https://www.w3.org/TR/WCAG22/#accessible-authentication-minimum) | **New in 2.2.** Forms in `SunfishForm` and bundle auth flows honor these. |
| [4.1.2 Name, Role, Value](https://www.w3.org/TR/WCAG22/#name-role-value) | Every interactive element has an accessible name and correct role; state changes are announced. |

**On Level AAA.** Sunfish does not commit AAA platform-wide — WCAG itself notes AAA is not achievable for all content. Per-component AAA is aspirational where practical (e.g., 1.4.6 enhanced contrast in dark-mode tokens, 2.4.8 location breadcrumbs in the shell). Each AAA win is noted on the component's contract; none block the build.

## Per-component accessibility contract

Every component's public documentation carries an **Accessibility** section. Per [component-principles.md §7](component-principles.md), this is a contract, not a decoration — it's what adapter-parity tests verify against. The section is mandatory for every component in `ui-core` and every adapter implementation; incomplete contracts fail review.

The contract has eight entries. An empty entry is allowed only when the component is trivially static (e.g., a layout stack); omission is never allowed.

1. **ARIA role + attributes.** Declare the role (`role="grid"`, `role="dialog"`, `role="tablist"`, …) and every `aria-*` attribute the component owns. Reference the matching [WAI-ARIA 1.2 Authoring Practices](https://www.w3.org/WAI/ARIA/apg/) pattern by name (e.g., "Data Grid Pattern", "Dialog (Modal) Pattern"). Deviations from the APG pattern are justified in the contract.
2. **Keyboard interaction map.** Table of keys → actions. Arrow keys, Home/End, PageUp/PageDown, Enter, Space, Esc, Tab, Shift+Tab at minimum for interactive components. Mirrors the APG pattern unless an explicit deviation is documented.
3. **Focus behavior.** Initial focus on activation (e.g., dialog opens → focus first focusable); focus trap for overlays (dialog, drawer, menu, popover with `aria-modal="true"`); focus restore on dismissal (return focus to the trigger). Focus rings honor the `--sf-focus-ring-*` tokens.
4. **Screen-reader expectations.** Accessible name source (`aria-label`, `aria-labelledby`, visible text, or associated `<label>`); live-region behavior if the component announces state changes (`aria-live="polite"` for toasts, `"assertive"` for critical alerts); named testing matrix: **NVDA + Firefox/Chrome, JAWS + Chrome, VoiceOver + Safari**.
5. **Reduced-motion adaptation.** If the component animates, declare what happens under `@media (prefers-reduced-motion: reduce)`. The rule: remove non-essential transitions; never remove state transitions the user relies on (a spinner still spins, but a decorative fade does not).
6. **Color-contrast budget.** Declare the minimum contrast ratio the component requires from its tokens (typically 4.5:1 text / 3:1 non-text per [1.4.3](https://www.w3.org/TR/WCAG22/#contrast-minimum) and [1.4.11](https://www.w3.org/TR/WCAG22/#non-text-contrast)). Provider themes are responsible for resolving `--sf-color-*` tokens to values that meet the budget; see [tokens-guidelines.md](tokens-guidelines.md).
7. **Target-size compliance.** Interactive targets ≥ 24×24 CSS px for desktop-density components; ≥ 44×44 for components rendered in bundles flagged `"surface": "mobile"` in their manifest. Documented as a size floor in the contract, enforced per provider in CSS.
8. **Shadow DOM exposure (ADR 0017).** Sunfish authors components as Web Components backed by Lit ([ADR 0017](../../docs/adrs/0017-web-components-lit-technical-basis.md)). The Shadow DOM boundary affects how ARIA references cross roots. Rules:
   - `aria-labelledby` and `aria-describedby` **cannot** cross shadow roots in today's browsers without ElementInternals / reflective ARIA. The component owns its own labelled-by target inside its shadow root, or exposes the ARIA surface through the reflective [ARIAMixin](https://www.w3.org/TR/wai-aria-1.2/#ARIAMixin) properties (`element.ariaLabel`, `element.role`) so the light-tree consumer sets them directly on the host element.
   - Form-associated Custom Elements use [ElementInternals](https://developer.mozilla.org/docs/Web/API/ElementInternals) with `attachInternals()` to participate in the host form's validity and labels.
   - Scoped Custom Element Registries (per ADR 0017) do not change accessibility semantics; the assistive-technology tree traverses the flattened tree regardless of registry scope.

### Example contract shape (authoritative template)

Components publish this verbatim on their reference page (`apps/docs/reference/components/<component>.md` per [documentation-framework.md](../product/documentation-framework.md)):

```md
## Accessibility

- **ARIA pattern:** WAI-ARIA 1.2 APG "Dialog (Modal)" — https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- **Role:** dialog; aria-modal="true"; aria-labelledby bound to the dialog title element.
- **Keyboard:** Esc closes; Tab/Shift+Tab cycles within the dialog (focus trap); no Enter-to-submit unless a default action is designated.
- **Focus:** On open, first focusable child receives focus (configurable via `initial-focus`). On close, focus returns to the element that opened the dialog.
- **Screen reader:** NVDA + Firefox, JAWS + Chrome, VoiceOver + Safari all announce the dialog title and modality. Live-region: none.
- **Reduced motion:** Backdrop fade and scale-in are suppressed under `prefers-reduced-motion: reduce`; the dialog still opens/closes, just without animation.
- **Contrast budget:** 4.5:1 body text; 3:1 border vs. backdrop.
- **Target size:** Close button ≥ 24×24 (desktop); ≥ 44×44 in mobile-flagged bundles.
- **Shadow DOM:** Title is inside the component's shadow root; consumers pass a string via the `heading` property, not via external `aria-labelledby`.
```

## Shadow DOM and accessibility

Contract item #8 states the per-component rules; this section pulls them together with the supporting context reviewers need when a cross-root ARIA question comes up.

### Open Shadow DOM is the default

Sunfish components attach their shadow roots with `{ mode: 'open' }` (Lit's default). The accessibility tree traverses open shadow roots natively in Chrome, Firefox, Safari, and every major AT (NVDA, JAWS, VoiceOver, TalkBack) — the user-facing a11y experience is equivalent to light-DOM for the purposes of role, label, and state announcement. **Closed shadow roots (`mode: 'closed'`) are prohibited** — they break AT tree exposure in some engines and defeat open-source composability. A component author who believes closed-mode is the right call opens an ADR first.

### Why cross-root ARIA references break

ARIA IDREFs (`aria-labelledby`, `aria-describedby`, `aria-controls`, `aria-owns`, `for`, `list`) resolve by document-scoped ID lookup. A consumer in the light tree passing `aria-labelledby="my-id"` to a Sunfish Web Component cannot reach an element whose ID lives inside the component's shadow root — the ID is scoped to the shadow root, not to the document. This is a platform limitation, not a Sunfish design choice.

### Three workarounds, in preference order

1. **Reflective ARIA (preferred for host-element attributes).** Authors set ARIA state on the **host element** via [ARIAMixin](https://www.w3.org/TR/wai-aria-1.2/#ARIAMixin) properties (`el.ariaLabel`, `el.ariaExpanded`, `el.role`, …). The component reads those properties internally and mirrors them onto shadow-tree nodes where needed. Consumers therefore set ARIA as if the component were a built-in.
2. **ElementInternals for form association.** Form-associated Custom Elements (per ADR 0017) call `attachInternals()` and use `ElementInternals.ariaLabel`, `ElementInternals.role`, `ElementInternals.setFormValue(...)`, etc. This is the path for components that participate in `<form>` validation and labelling — `SunfishInput`, `SunfishSelect`, `SunfishCheckbox`.
3. **Expose ARIA via a typed property.** When a component genuinely needs an external label target (a dialog titled by arbitrary light-DOM content), expose a string property (e.g., `heading="…"`) instead of an IDREF. The component renders the string into a shadow-tree element it owns and labels itself internally. This is the pattern the [Dialog example contract](#accessibility) above uses.

Avoid the anti-pattern of trying to forward `aria-labelledby` by copying the referenced element's text content into the shadow tree — it stale-drifts when the external element changes and breaks screen-reader pronunciation for dynamic content.

### Declarative Shadow DOM (DSD)

Sunfish plans to ship DSD for server-rendered component hydration once the stable `shadowroot` attribute reaches wide browser support (Safari 16.4+, Chrome/Edge 111+, Firefox 123+). DSD does **not** change any of the accessibility rules above — the hydrated root behaves exactly like an imperatively-attached open root. Authors who gate a feature on DSD availability must not also gate its accessibility contract.

### Manual testing note

The manual screen-reader matrix (§Manual audit workflow) runs against the final open-shadow-root output; no separate "shadow-tree pass" is required. Reviewers verify that the specific Shadow DOM rules (reflective ARIA set, ElementInternals used for forms, no cross-root IDREF leakage) are honored in the contract update shipped with the PR.

## Automated testing

Accessibility automation uses **[axe-core](https://github.com/dequelabs/axe-core)** (Deque's open-source engine, MPL-2.0), wired into the two test harnesses Sunfish already operates per [testing-strategy.md](../engineering/testing-strategy.md):

### Web Components (once `packages/ui-components-web` ships per ADR 0017)

- **Runner:** [Web Test Runner](https://modern-web.dev/docs/test-runner/overview/) with the Playwright launcher for real browser rendering.
- **Engine:** `@axe-core/playwright` (`AxeBuilder`) run against each component's mounted fixture.
- **Shadow DOM:** axe-core traverses open shadow roots natively; tests can also target deep-nested components via `axe.run({ fromShadowDom: ['sunfish-grid', '.header', '#cell-3'] })`.
- **Rule set:** `AxeBuilder.withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice'])` — every component fixture runs the full AA rule set.

### Blazor wrappers

- **Unit layer:** bUnit renders the component; the rendered markup is serialized and fed to `@axe-core/playwright` (via a small Playwright harness project) for a real browser-rendered audit. Blazor's rendered output is plain HTML + Web Component hosts, so axe's DOM analysis applies directly.
- **Integration layer:** existing bUnit + Testcontainers flows add an accessibility assertion per interactive-component fixture.

### CI fail gate

- **Threshold:** any axe-core violation of `impact` ≥ **`moderate`** fails the build. Valid axe impact values are `minor`, `moderate`, `serious`, `critical` (per axe-core rule schema); `minor` findings are reported but non-blocking and logged in the PR comment stream.
- **Integration point:** the accessibility job runs in `ci.yml` as a required status check per [ci-quality-gates.md](../engineering/ci-quality-gates.md). It joins the existing "build + test + CodeQL + docs" required-checks set.
- **Runtime budget:** the a11y job must fit inside the 10-minute p50 / 15-minute p95 speed target from ci-quality-gates.md. If the suite exceeds that, split into a per-adapter matrix rather than relax the gate.
- **Concurrency:** same `cancel-in-progress: true` pattern as the other PR workflows.

### Baseline-drift policy

A pre-existing violation may not block a release, but neither may it rot silently. Known issues are logged in `_shared/engineering/a11y-baseline.md` (P2 follow-up) with the ADR 0014 parity-exception shape:

- **Component / surface affected.**
- **Violation ID** (axe rule id, e.g., `color-contrast`) and **impact level**.
- **Reason** for the exception (e.g., "Material provider's default focus ring fails 3:1 on disabled state — upstream issue open").
- **Owner** and **target** date.
- **Logged** date.

The CI gate reads the baseline file and lets listed violations through. An unknown violation — one not in the baseline — fails the build. An exception past its target date surfaces in monthly roadmap review alongside stale adapter-parity exceptions. Adding a new baseline entry is a reviewable PR, not a silent suppression.

## Manual audit workflow

Automation catches the bulk; it does not catch everything. Manual audit is required in six situations:

1. A new major component lands (anything with its own family entry in [component-principles.md §3](component-principles.md)).
2. A new variant or mode of an existing component (e.g., a virtualized `SunfishDataGrid` renderer).
3. A new provider theme is added or an existing provider undergoes a visual refresh.
4. A new bundle's first user-facing surface ships.
5. A new accelerator host ships (Bridge successor, mobile PWA shell).
6. Regressions reported against a shipped surface — full manual pass before the fix merges.

The manual audit has six passes. Each pass produces a dated note appended to the component's or bundle's audit log.

### 1. Keyboard-only pass

Unplug the mouse. Every action reachable? Focus order sensible? No dead-end traps outside modals? Tab takes you out of the shell into the browser chrome eventually (no infinite app-only trap)?

### 2. Screen-reader pass

| Platform | Screen reader | When required |
|---|---|---|
| Windows | **NVDA** (free, [nvaccess.org](https://www.nvaccess.org/)) + Firefox & Chrome | Always. Canonical Windows desktop reader for OSS testing. |
| Windows | **JAWS** (Freedom Scientific, commercial) + Chrome | Required for enterprise-targeted bundles (school-district, medical-office) where JAWS is the customer's baseline. |
| Windows | **Narrator** (built-in) + Edge | Smoke-only; not a primary target. |
| macOS | **VoiceOver** + Safari | Always. |
| iOS | **VoiceOver** + Safari | Mobile PWA surfaces in bundles flagged `"surface": "mobile"`. |
| Android | **TalkBack** + Chrome | Same — any bundle with a mobile PWA surface. |

The matrix reflects the [WebAIM Screen Reader User Survey](https://webaim.org/projects/screenreadersurvey10/) patterns: NVDA + JAWS dominate desktop; VoiceOver + TalkBack dominate mobile. Testing against both engines per platform catches semantics that one tool papers over.

### 3. Color-contrast pass

Automated via axe in CI; the manual pass uses [Colour Contrast Analyser (TPGi)](https://www.tpgi.com/color-contrast-checker/) or browser devtools to verify tokens that axe can't always compute (gradient text on a gradient backdrop, translucent overlays on unpredictable page content). Run against every provider theme, light and dark.

### 4. Focus-trap pass

For every overlay (dialog, drawer, menu, popover with `aria-modal`), verify:
- Focus moves into the overlay on open.
- Tab cycles inside; Shift+Tab cycles the other way.
- Esc dismisses and restores focus to the trigger.
- Outside-click dismiss (if enabled) also restores focus.
- Nested overlays (dialog-within-dialog) stack focus traps correctly.

### 5. Reduced-motion pass

Enable the OS preference (Windows: Settings → Accessibility → Visual Effects → Animation effects off; macOS: System Settings → Accessibility → Display → Reduce motion). Every animating component degrades gracefully. Spinners still spin (state indicator); fades and slide-ins are disabled.

### 6. Zoom / reflow pass

Zoom the browser to 200%. No horizontal scroll on narrow content. No content clipped. Focused elements remain visible (WCAG 2.4.11). Test at 320 CSS-px viewport width (per [WCAG 1.4.10 Reflow](https://www.w3.org/TR/WCAG22/#reflow)) — the narrowest mobile breakpoint Sunfish targets.

## Provider-theme audits

Provider themes are independent surfaces for accessibility purposes. A component that passes in FluentUI can fail in Material because the tokens resolve differently. Every provider theme runs a narrower audit on a tighter cadence:

- **Color contrast** — full axe scan + manual spot-check on every token pair (text on surface, primary on bg, danger on surface, focus ring on every background).
- **Reduced motion** — confirm the provider's transition tokens respect `prefers-reduced-motion` (motion tokens should resolve to `0s` under the media query, not "fade a little faster").
- **Focus-ring visibility** — the `--sf-focus-ring-*` tokens must be distinguishable on every background in the provider, light and dark.
- **Target size** — the provider may override component density. The override must not break the 24×24 floor.

Cadence: every provider-theme PR triggers the provider audit for the components it touches. A major provider refresh (Bootstrap 5 → 6 upgrade, a Material version bump) triggers a full re-audit across every component the provider styles.

## Accessibility statement and conformance claims

Sunfish publishes a **platform-wide accessibility statement** at `apps/docs/explanation/accessibility.md` (Diátaxis explanation mode per [documentation-framework.md](../product/documentation-framework.md)). The statement:

- Names WCAG 2.2 AA as the baseline.
- Links to this document for the operational detail.
- Lists known gaps (from the baseline-drift file) in user-readable form.
- Names the commercial audit offering (below).
- Provides the accessibility feedback channel (GitHub issue template `accessibility-report.yml`, plus a private-disclosure email for customers under commercial terms).

**Per-bundle VPAT / ACR.** Bundles sold to customers with formal attestation needs (school districts under Section 508, EU public-sector customers under EN 301 549) publish a **Voluntary Product Accessibility Template (VPAT)** producing an **Accessibility Conformance Report (ACR)**. Template: the [ITI VPAT 2.5 INT](https://www.itic.org/policy/accessibility/vpat) — the standard industry form with WCAG 2.2, Section 508 (Revised), EN 301 549, and AODA columns. Each bundle's `conformance/` folder holds the current ACR; updates on every minor release.

## Reviewer checklist (pull_request_template.md)

The checklist below is copied into `.github/pull_request_template.md` when the parity-review consolidation lands per ADR 0014 follow-up. Reviewers tick every applicable box on user-facing PRs. A PR that changes a component, provider, or user-facing surface cannot merge until the checklist is complete.

```md
### Accessibility (required for user-facing changes)

- [ ] Component's Accessibility contract section updated (role, keyboard map, focus, SR, motion, contrast, target size, Shadow DOM).
- [ ] WAI-ARIA APG pattern named in the contract (or deviation justified).
- [ ] axe-core CI job green; no new violations at impact ≥ moderate.
- [ ] If a new baseline entry was added, it carries owner + target date.
- [ ] Keyboard-only pass performed (for new or changed interactive components).
- [ ] Screen-reader pass performed on the required matrix (NVDA + VoiceOver minimum; JAWS/TalkBack per bundle tier).
- [ ] Focus-trap verified for overlays introduced or modified.
- [ ] Reduced-motion honored for any animation introduced.
- [ ] Color-contrast verified in every provider theme touched, light + dark.
- [ ] Target size ≥ 24×24 (desktop) / ≥ 44×44 (mobile bundle surfaces).
- [ ] Shadow DOM: ARIA references resolved inside the component's root or via reflective ARIA.
```

## Commercial audit and remediation offering

Per [vision.md](../product/vision.md) §Business model, Sunfish offers commercial accessibility services for customers needing formal attestation beyond the OSS baseline:

- **Audit.** Full manual pass by a certified accessibility specialist (IAAP CPACC / WAS). Delivers a gap analysis, a populated ACR/VPAT, and a prioritized remediation plan.
- **Remediation.** Targeted engineering against a customer's deployed bundle or custom surfaces to close identified gaps.
- **Ongoing attestation.** Annual re-audit against the shipping version of the bundle; pinned ACR updates on each release.

This is an upsell layered onto the OSS platform, not a gate on OSS accessibility. The OSS distribution meets WCAG 2.2 AA on its own merits; the paid service is formal third-party attestation for customers with statutory disclosure obligations.

## Follow-ups and tooling gaps

| Item | Priority | Trigger |
|---|---|---|
| axe-core CI job wired into `ci.yml` | **P1** | Once `packages/ui-components-web` ships (ADR 0017 migration). Until then, bUnit-side axe integration lands first as an interim gate. |
| `_shared/engineering/a11y-baseline.md` with initial entries | **P1** | Ships alongside the first axe CI run; migrates pre-existing violations into the baseline with target dates. |
| Per-component Accessibility section backfill in `apps/docs/reference/components/` | **P1** | Blocking before 1.0; in flight as part of [documentation-framework.md](../product/documentation-framework.md) migration. |
| VPAT / ACR template in `apps/docs/reference/conformance/` | **P2** | Before the first bundle sale that requires formal attestation (expected: school-district pilot). |
| Screen-reader test-environment documentation in `apps/docs/how-to/testing/` | **P2** | How to install NVDA, configure JAWS for trial use, enable VoiceOver verbosity, run TalkBack on Android emulator. |
| Scoped-registry + ARIA interaction tests (ADR 0017 edge) | **P3** | When scoped registries are used in a component that exposes cross-root labelling. |
| AAA-aspiration tracker | **P3** | A per-component table noting which AAA criteria are met; surfaces as a reference page. Not blocking; useful for enterprise RFP responses. |
| Lighthouse accessibility audit in `apps/docs` build | **P3** | A secondary signal beyond axe; caught via the Chrome DevTools MCP harness already used for design-QC screenshots. |

## Cross-references

- [component-principles.md](component-principles.md) §7 — accessibility as a component contract (this document operationalizes that principle).
- [tokens-guidelines.md](tokens-guidelines.md) — `--sf-color-*` and `--sf-motion-*` tokens that provider themes resolve to contrast-compliant and motion-respectful values.
- [documentation-framework.md](../product/documentation-framework.md) — where the accessibility statement and per-component a11y contracts live (Diátaxis explanation + reference modes).
- [adapter-parity.md](../engineering/adapter-parity.md) — parity-exception shape reused by the a11y baseline-drift file.
- [ci-quality-gates.md](../engineering/ci-quality-gates.md) — required-status-check doctrine that the a11y CI job joins.
- [testing-strategy.md](../engineering/testing-strategy.md) — xUnit + bUnit + Testcontainers harness the axe job integrates with.
- [vision.md §Pillar 5](../product/vision.md) — the product commitment this document operationalizes.
- [ADR 0014](../../docs/adrs/0014-adapter-parity-policy.md) — parity rule that accessibility contracts are verified against.
- [ADR 0017](../../docs/adrs/0017-web-components-lit-technical-basis.md) — Lit / Web Components authoring basis that frames the Shadow DOM accessibility rules.
- [WCAG 2.2 (W3C)](https://www.w3.org/TR/WCAG22/) — the normative standard.
- [WAI-ARIA 1.2 (W3C)](https://www.w3.org/TR/wai-aria-1.2/) — the role/attribute vocabulary components document.
- [WAI-ARIA 1.2 Authoring Practices](https://www.w3.org/WAI/ARIA/apg/) — pattern library components cite in their contracts.
- [axe-core](https://github.com/dequelabs/axe-core) — the accessibility engine the CI gate runs.
- [WebAIM Screen Reader User Survey](https://webaim.org/projects/screenreadersurvey10/) — basis for the screen-reader test matrix.
- [ITI VPAT 2.5 INT](https://www.itic.org/policy/accessibility/vpat) — template used for Sunfish ACRs.

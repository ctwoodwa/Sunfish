# Global-First UX — Phase 1 Week 1 Tooling Pilot

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Validate the three critical tool choices from the Global-First UX spec (Storybook a11y harness, ICU4N wrapper, Weblate + MADLAD-400) with Week 1 pilots before cascading across the repository; deliver the Week 1 go/no-go gate outcome.

**Architecture:** Three parallel research/pilot tracks converge at end-of-Week-1. Each track produces a concrete artifact (memo, ADR, pilot code). The week ends with a binary gate: proceed to Phase 1 Weeks 2-6 or pivot to named fallbacks.

**Tech Stack:** Node.js 20+, Storybook 8.x + `@storybook/addon-a11y`, `@axe-core/playwright`, Web Test Runner + Playwright, ICU4N (.NET 8), xUnit, MADLAD-400-3B-MT via llama.cpp (GGUF), Weblate (Docker self-hosted).

**Scope boundary:** This plan covers ONLY Phase 1 Week 1 (5 business days). Subsequent plans (Loc-Infra cascade Plan 2, Translator-Assist Plan 3, A11y Foundation Plan 4, CI Gates Plan 5, Phase 2 Cascade Plan 6) are deferred until Week 1 results unlock them per the spec's go/no-go gate.

**Parent spec:** [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../specs/2026-04-24-global-first-ux-design.md)

---

## File Structure (Week 1 deliverables)

```
waves/
  global-ux/
    status.md                                       ← Resume Protocol per spec Section 1
    decisions.md                                    ← Rollback log (created empty; populated on first pivot)

docs/adrs/
  0034-a11y-harness-per-adapter.md                  ← NEW ADR
  0035-global-domain-types-as-separate-wave.md      ← NEW ADR

icm/01_discovery/output/
  icu4n-health-check-2026-04-25.md                  ← Research memo
  weblate-vs-crowdin-2026-04-25.md                  ← Research memo
  xliff-tool-ecosystem-2026-04-26.md                ← Research memo

packages/ui-core/
  package.json                                      ← NEW (pnpm workspace entry for Storybook)
  .storybook/main.ts                                ← Storybook config
  .storybook/preview.ts                             ← Storybook preview config + a11y config
  src/components/button/sunfish-button.ts           ← Stub Web Component
  src/components/button/sunfish-button.stories.ts   ← Pilot story with contract
  src/components/dialog/sunfish-dialog.ts           ← Stub Web Component
  src/components/dialog/sunfish-dialog.stories.ts   ← Pilot story
  src/components/syncstate/sunfish-syncstate-indicator.ts    ← Stub Web Component
  src/components/syncstate/sunfish-syncstate-indicator.stories.ts ← Pilot story

packages/foundation/
  Localization/
    ISunfishLocalizer.cs                            ← ICU4N wrapper interface
    SunfishLocalizer.cs                             ← ICU4N wrapper implementation
  Localization.Tests/
    SunfishLocalizerIcuTests.cs                     ← Three smoke tests
```

---

## Task 1: Scaffold wave tracking directory

**Files:**
- Create: `waves/global-ux/status.md`
- Create: `waves/global-ux/decisions.md`
- Create: `waves/global-ux/README.md`

**Why:** Resume Protocol per spec Section 1. Before any work starts, these files must exist so every agent who touches the sprint updates them.

- [ ] **Step 1: Create the directory and README**

Create `waves/global-ux/README.md`:

```markdown
# Global-First UX Wave

Implementation of the Global-First UX mandate. See the spec:
[`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../../docs/superpowers/specs/2026-04-24-global-first-ux-design.md).

## Files in this directory

- `status.md` — current phase, week, active work, blockers, handoff context. Updated at the end of every agent work session.
- `decisions.md` — append-only log of rollback pivots, tool-choice changes, and material scope shifts. Created empty; populated only when a decision is made.

## Rules

- Every agent who touches this wave updates `status.md` before ending their session.
- Pivots from the spec are recorded in `decisions.md` with ISO date, triggering condition, and chosen alternative.
```

- [ ] **Step 2: Create the status file**

Create `waves/global-ux/status.md`:

```markdown
# Global-First UX — Wave Status

**Updated:** 2026-04-24 (plan scaffolded, Week 1 not started)
**Current phase:** Phase 1 Week 1 (Tooling Pilot) — not yet started
**Current focus:** Awaiting Week 1 kickoff

## Completed this week
- (none yet)

## In progress
- (none yet)

## Blocked
- (none)

## Next agent handoff context

Starting Phase 1 Week 1 per plan `docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-week-1-plan.md`.
First three tasks run in parallel as research memos (ICU4N health, Weblate vs Crowdin, XLIFF tooling).
No external dependencies yet.
```

- [ ] **Step 3: Create the decisions file**

Create `waves/global-ux/decisions.md`:

```markdown
# Global-First UX — Decisions Log

Append-only. New entries at the top. Older entries at the bottom.

---

*(No decisions recorded yet. Populated when rollback criteria trigger.)*
```

- [ ] **Step 4: Commit**

```bash
git add waves/global-ux/
git commit -m "chore(global-ux): scaffold wave tracking directory

Add waves/global-ux/ with README, status.md, and decisions.md per the
Resume Protocol specified in the Global-First UX design spec.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 2: ICU4N health check research memo

**Files:**
- Create: `icm/01_discovery/output/icu4n-health-check-2026-04-25.md`

**Why:** Spec Section 3A requires this as a Week 0 research triage output before committing to ICU4N.

- [ ] **Step 1: Check ICU4N repo activity**

Run:
```bash
gh api repos/NightOwl888/ICU4N --jq '{stars: .stargazers_count, open_issues: .open_issues_count, updated_at: .updated_at, default_branch: .default_branch}'
gh api repos/NightOwl888/ICU4N/releases --jq '.[0:5] | map({tag: .tag_name, published: .published_at})'
gh api repos/NightOwl888/ICU4N/commits --jq '.[0:10] | map({date: .commit.author.date, msg: .commit.message | split("\n")[0]})'
```

- [ ] **Step 2: Check CLDR version lag**

Run:
```bash
gh api repos/NightOwl888/ICU4N/releases --jq '.[0].body' | head -100
curl -s https://api.github.com/repos/unicode-org/cldr/releases/latest | jq '.tag_name'
```

- [ ] **Step 3: Write the memo**

Create `icm/01_discovery/output/icu4n-health-check-2026-04-25.md` with sections:
- Repository activity (stars, last release, last commit, open issues)
- CLDR version lag (ICU4N current CLDR version vs. upstream CLDR release)
- Test coverage and CI status (if visible in repo)
- Binary size (check release artifacts for DLL size)
- Verdict: GO / PIVOT TO FALLBACK / FURTHER RESEARCH
- If PIVOT: which fallback (OrchardCore.Localization ICU fork OR custom pattern-matcher)

Use the data from Steps 1-2. No placeholder text — fill in actual numbers.

- [ ] **Step 4: Commit**

```bash
git add icm/01_discovery/output/icu4n-health-check-2026-04-25.md
git commit -m "docs(global-ux): ICU4N health check memo for Week 0 triage

Research output per Global-First UX spec Section 3A Week 0 triage gate:
repo activity, CLDR version lag, test coverage, binary size. Verdict
feeds the Week 1 go/no-go gate on ICU4N wrapper adoption.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 3: Weblate vs Crowdin research memo

**Files:**
- Create: `icm/01_discovery/output/weblate-vs-crowdin-2026-04-25.md`

**Why:** Spec Section 3B says this memo is a Week 0 blocker before Phase 1 Day 1. Default pick if unresolved is Weblate self-hosted with AGPL concession noted for legal review.

- [ ] **Step 1: Write the comparison memo**

Create `icm/01_discovery/output/weblate-vs-crowdin-2026-04-25.md` covering these five dimensions per the spec:

1. **AGPL network-service obligations** — If Sunfish commercializes translation services (per `vision.md`) and Weblate (AGPL) runs as the backend, does that trigger AGPL network-service clause §13 obligations? Requires legal review citation or flagged as open legal question.

2. **Translator UX** — XLIFF 2.0 native support, glossary integration, workflow maturity. Weblate's and Crowdin's docs on these specifically.

3. **MADLAD integration** — Both support ML translation suggestions. Compare the integration surfaces: Weblate's machine translation backends API vs. Crowdin's AI-suggestion system.

4. **Cost** — Weblate self-hosted operational cost (Docker, backups, upgrades) vs. Crowdin SaaS per-seat pricing at Sunfish's projected translator count (estimate: 12 coordinators + ~30 contributors).

5. **Operational burden** — who runs the instance, upgrade cadence, backup strategy, DR plan.

Deliver a **recommendation** with named fallback. Use WebSearch + reading official docs (no guessing).

- [ ] **Step 2: Commit**

```bash
git add icm/01_discovery/output/weblate-vs-crowdin-2026-04-25.md
git commit -m "docs(global-ux): Weblate vs Crowdin translator-platform memo

Research output per Global-First UX spec Section 3B Week 0 triage gate
(platform decision must land before Phase 1 Day 1). Five-dimension
comparison: AGPL obligations, translator UX, MADLAD integration, cost,
operational burden. Feeds translator-assist tool-choice decision.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 4: XLIFF 2.0 vs 1.2 tool ecosystem survey

**Files:**
- Create: `icm/01_discovery/output/xliff-tool-ecosystem-2026-04-26.md`

**Why:** Spec Section 3A lists this as a Week 0 research triage. Informs the custom-MSBuild-task vs Multilingual-App-Toolkit decision.

- [ ] **Step 1: Survey available tools**

Check for XLIFF tooling in .NET ecosystem:
- Microsoft [Multilingual App Toolkit](https://learn.microsoft.com/windows/apps/design/globalizing/use-mat) — XLIFF 1.2 only
- [OrchardCore Localization](https://docs.orchardcore.net/en/main/reference/modules/Localization/) — uses PO files, not XLIFF
- NuGet search: `xliff`, `resx-to-xliff`, `xliff-tool`

Document what exists, what XLIFF version each supports, and which are maintained.

- [ ] **Step 2: Write the memo**

Create `icm/01_discovery/output/xliff-tool-ecosystem-2026-04-26.md` with sections:
- Existing tool landscape (named tools with last-maintained dates)
- XLIFF 1.2 vs 2.0 coverage in existing tools
- Build vs adopt recommendation for Sunfish
- If build: estimated effort for custom MSBuild task (likely 1 week)
- If adopt: which existing tool + conversion step

- [ ] **Step 3: Commit**

```bash
git add icm/01_discovery/output/xliff-tool-ecosystem-2026-04-26.md
git commit -m "docs(global-ux): XLIFF 2.0 tool ecosystem survey

Research output per Global-First UX spec Section 3A Week 0 triage:
evaluates Multilingual App Toolkit vs custom MSBuild task for
.resx <-> XLIFF round-tripping. Feeds XLIFF-pipeline build-vs-adopt
decision.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 5: Draft ADR 0034 — A11y Harness per Adapter

**Files:**
- Create: `docs/adrs/0034-a11y-harness-per-adapter.md`

**Why:** Spec Section 7 requires this ADR authored Week 1 Day 3 and accepted before contract work begins.

- [ ] **Step 1: Read existing ADR format**

Read the most recent ADR to match style:

```bash
cat docs/adrs/0033-browser-shell-render-model-and-trust-posture.md | head -60
```

- [ ] **Step 2: Draft the ADR**

Create `docs/adrs/0034-a11y-harness-per-adapter.md` with:

- **Status:** Proposed
- **Date:** 2026-04-27 (Week 1 Day 3)
- **Context:** The Global-First UX spec establishes per-component a11y contracts executed via the Storybook a11y harness. `ui-core` is Web Components per ADR 0017; `ui-adapters-react` consumes stories via `@storybook/react`; `ui-adapters-blazor` requires a different harness because bUnit renders to HTML strings, not a live browser.
- **Decision:** Three distinct harnesses per adapter, one contract expressed once:
  - `ui-core`: Storybook + Web Test Runner + Playwright + `@axe-core/playwright`
  - `ui-adapters-react`: Storybook for React + Vitest + Playwright + `@axe-core/playwright`
  - `ui-adapters-blazor`: bUnit + new bUnit-to-axe bridge project that serializes bUnit output into a Playwright harness
- **Consequences:** Three CI matrix entries. One contract (in the component's Storybook story `parameters.a11y.sunfish` block) consumed by all three.
- **Alternatives considered:** Single harness (rejected — no unified tool covers all three rendering targets). Skipping Blazor a11y gate (rejected — violates the first-class mandate).
- **Related ADRs:** 0017 (Web Components/Lit), 0030 (React adapter scaffolding).

- [ ] **Step 3: Commit**

```bash
git add docs/adrs/0034-a11y-harness-per-adapter.md
git commit -m "docs(adrs): ADR 0034 - A11y harness per adapter (Proposed)

Establishes Storybook-based accessibility harnesses per adapter (Web
Components / React / Blazor) to execute the Global-First UX spec's
per-component accessibility contract once across three render targets.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 6: Draft ADR 0035 — Global-Domain-Types as Separate Wave

**Files:**
- Create: `docs/adrs/0035-global-domain-types-as-separate-wave.md`

**Why:** Spec Section 3C explicitly excludes `PersonalName`, `Money`, `Address`, `NodaTime` migrations from the Global-First UX sprint. This ADR formalizes the exclusion.

- [ ] **Step 1: Draft the ADR**

Create `docs/adrs/0035-global-domain-types-as-separate-wave.md` with:

- **Status:** Proposed
- **Date:** 2026-04-27 (Week 1 Day 3)
- **Context:** During Global-First UX brainstorming, eleven improvements to localization infrastructure were surfaced. Four of them (`PersonalName` value object, `Money` value object, `Address` templates, `NodaTime` migration) are domain-modeling concerns, not localization infrastructure. Universal-planning review of the spec's Section 3 flagged their inclusion as a timeline-fantasy anti-pattern (~30+ person-weeks presented as ~1 week).
- **Decision:** Split into a separate wave with its own ADR(s), owner, and timeline. Global-First UX ships Phase 1 + Phase 2 without them. When the separate wave lands, the already-wired `IStringLocalizer<T>` consumes the new value objects transparently — no UX-layer rework.
- **Consequences:**
  - Global-First UX Phase 1 scope achievable in 6–8 weeks as spec'd.
  - Domain-type migration has room for its own decomposition, migration plan, and breaking-schema-change policy.
  - Until the separate wave lands, `DateTime` remains in domain code; `decimal` remains for money; `FirstName/LastName` remains in entity models. This is explicit technical debt tracked to the separate wave.
- **Alternatives considered:** Inline domain-type migration into Phase 1 (rejected — timeline fantasy). Indefinite deferral (rejected — the spec calls these first-class for a reason; they're deferred, not abandoned).
- **Related ADRs:** 0015 (module-entity registration), 0018 (governance).

- [ ] **Step 2: Commit**

```bash
git add docs/adrs/0035-global-domain-types-as-separate-wave.md
git commit -m "docs(adrs): ADR 0035 - Global-Domain-Types as separate wave (Proposed)

Formalizes the exclusion of PersonalName/Money/Address value objects and
NodaTime migration from the Global-First UX sprint per spec Section 3C.
These are domain-modeling concerns with ~30 person-week scope that gets
its own wave, ADR, and timeline.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 7: Review and accept both ADRs

**Files:**
- Modify: `docs/adrs/0034-a11y-harness-per-adapter.md` (status line)
- Modify: `docs/adrs/0035-global-domain-types-as-separate-wave.md` (status line)

**Why:** Spec Section 7 and Section 3C require these ADRs accepted before dependent work starts.

- [ ] **Step 1: Flip status Proposed → Accepted on ADR 0034**

Use the Edit tool to change the `**Status:**` line from `Proposed` to `Accepted` in the ADR file.

- [ ] **Step 2: Flip status Proposed → Accepted on ADR 0035**

Same change in the second ADR file.

- [ ] **Step 3: Commit**

```bash
git add docs/adrs/0034-a11y-harness-per-adapter.md docs/adrs/0035-global-domain-types-as-separate-wave.md
git commit -m "docs(adrs): ADRs 0034 + 0035 Proposed to Accepted

Both ADRs reviewed and accepted per Week 1 Day 3 milestone of the
Global-First UX Phase 1 plan.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 8: Set up pnpm workspace for ui-core

**Files:**
- Check: `pnpm-workspace.yaml` at repo root
- Create: `packages/ui-core/package.json`

**Why:** Spec Section 7 uses Storybook. ui-core needs a Node.js package layer alongside its framework-agnostic contracts. Check if a workspace already exists; create if not.

- [ ] **Step 1: Check for existing workspace config**

```bash
test -f pnpm-workspace.yaml && echo "EXISTS" || echo "MISSING"
cat pnpm-workspace.yaml 2>/dev/null
```

If MISSING, proceed to Step 2. If EXISTS, verify `packages/ui-core` is in the workspace globs; add if missing and commit.

- [ ] **Step 2: Create pnpm workspace config (only if MISSING)**

Create `pnpm-workspace.yaml`:

```yaml
packages:
  - 'packages/ui-core'
  - 'packages/ui-adapters-react'
  - 'tooling/*'
  - 'apps/kitchen-sink'
```

- [ ] **Step 3: Create `packages/ui-core/package.json`**

```json
{
  "name": "@sunfish/ui-core",
  "version": "0.1.0-alpha",
  "private": true,
  "description": "Framework-agnostic Web Components for Sunfish. Accessibility-first, RTL-safe, globally tested.",
  "type": "module",
  "scripts": {
    "storybook": "storybook dev -p 6006",
    "build-storybook": "storybook build",
    "test:a11y": "test-storybook --config-dir .storybook"
  },
  "devDependencies": {
    "@storybook/addon-a11y": "^8.3.0",
    "@storybook/addon-essentials": "^8.3.0",
    "@storybook/test": "^8.3.0",
    "@storybook/test-runner": "^0.19.0",
    "@storybook/web-components-vite": "^8.3.0",
    "@axe-core/playwright": "^4.10.0",
    "@web/test-runner": "^0.19.0",
    "@web/test-runner-playwright": "^0.11.0",
    "lit": "^3.2.0",
    "playwright": "^1.48.0",
    "storybook": "^8.3.0",
    "typescript": "^5.6.0"
  }
}
```

- [ ] **Step 4: Install dependencies**

```bash
pnpm install --filter @sunfish/ui-core
```

Expected: installs Storybook, Playwright, axe-core, Lit, TypeScript. Verify no errors; expect 150-250 packages installed.

- [ ] **Step 5: Commit**

```bash
git add pnpm-workspace.yaml packages/ui-core/package.json packages/ui-core/pnpm-lock.yaml
git commit -m "chore(ui-core): scaffold pnpm workspace for Storybook

Adds packages/ui-core/package.json with Storybook 8.x, Playwright, and
@axe-core/playwright dependencies per the Global-First UX spec Section 7.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 9: Configure Storybook for ui-core

**Files:**
- Create: `packages/ui-core/.storybook/main.ts`
- Create: `packages/ui-core/.storybook/preview.ts`
- Create: `packages/ui-core/tsconfig.json`

**Why:** Storybook needs config before stories can render. This wires @storybook/addon-a11y and the web-components-vite framework.

- [ ] **Step 1: Write `packages/ui-core/.storybook/main.ts`**

```typescript
import type { StorybookConfig } from '@storybook/web-components-vite';

const config: StorybookConfig = {
  stories: ['../src/**/*.stories.@(ts|tsx)'],
  addons: [
    '@storybook/addon-essentials',
    '@storybook/addon-a11y',
  ],
  framework: {
    name: '@storybook/web-components-vite',
    options: {},
  },
  typescript: { check: false },
};

export default config;
```

- [ ] **Step 2: Write `packages/ui-core/.storybook/preview.ts`**

```typescript
import type { Preview } from '@storybook/web-components';

const preview: Preview = {
  parameters: {
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'aria-valid-attr-value', enabled: true },
        ],
      },
      options: {
        runOnly: {
          type: 'tag',
          values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice'],
        },
      },
    },
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    globalTypes: {
      direction: {
        name: 'Direction',
        description: 'Layout direction',
        defaultValue: 'ltr',
        toolbar: {
          icon: 'globe',
          items: [
            { value: 'ltr', title: 'LTR (en-US)' },
            { value: 'rtl', title: 'RTL (ar-SA)' },
          ],
        },
      },
    },
  },
  decorators: [
    (story, context) => {
      document.documentElement.setAttribute('dir', context.globals.direction ?? 'ltr');
      return story();
    },
  ],
};

export default preview;
```

- [ ] **Step 3: Write `packages/ui-core/tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "experimentalDecorators": true,
    "useDefineForClassFields": false,
    "lib": ["ES2022", "DOM", "DOM.Iterable"]
  },
  "include": ["src/**/*.ts", ".storybook/**/*.ts"]
}
```

- [ ] **Step 4: Verify Storybook config validates**

```bash
cd packages/ui-core && pnpm dlx tsc --noEmit
```

Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add packages/ui-core/.storybook/ packages/ui-core/tsconfig.json
git commit -m "chore(ui-core): Storybook 8 config with a11y addon + RTL toggle

Storybook main.ts + preview.ts + tsconfig.json. Wires
@storybook/addon-a11y with WCAG 2.2 AA rule set; adds RTL/LTR toolbar
toggle via direction globalType; installs the decorator that flips
html[dir]. Verified type-checks clean.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 10: Build `sunfish-button` stub + story

**Files:**
- Create: `packages/ui-core/src/components/button/sunfish-button.ts`
- Create: `packages/ui-core/src/components/button/sunfish-button.stories.ts`

**Why:** First pilot component. Simplest surface for validating the Storybook + a11y harness end-to-end.

- [ ] **Step 1: Write the component source**

```typescript
// packages/ui-core/src/components/button/sunfish-button.ts
import { LitElement, html, css } from 'lit';
import { customElement, property } from 'lit/decorators.js';

@customElement('sunfish-button')
export class SunfishButton extends LitElement {
  static styles = css`
    :host { display: inline-block; }
    button {
      font: inherit;
      padding-inline: var(--sf-space-4, 16px);
      padding-block: var(--sf-space-2, 8px);
      min-block-size: 24px;
      min-inline-size: 24px;
      background: var(--sf-color-primary, #2563eb);
      color: var(--sf-color-on-primary, #ffffff);
      border: 2px solid transparent;
      border-radius: var(--sf-radius-md, 4px);
      cursor: pointer;
    }
    button:focus-visible {
      outline: 3px solid var(--sf-color-focus-ring, #2563eb);
      outline-offset: 2px;
    }
    button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
  `;

  @property({ type: String }) label = '';
  @property({ type: Boolean }) disabled = false;

  render() {
    return html`
      <button ?disabled=${this.disabled} aria-label=${this.label}>
        <slot>${this.label}</slot>
      </button>
    `;
  }
}
```

- [ ] **Step 2: Write the story with contract**

```typescript
// packages/ui-core/src/components/button/sunfish-button.stories.ts
import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import './sunfish-button.js';

const meta: Meta = {
  title: 'Core/Button',
  component: 'sunfish-button',
  parameters: {
    a11y: {
      sunfish: {
        wcag22Conformant: ['1.3.1', '1.4.3', '1.4.11', '2.1.1', '2.4.7', '2.5.8', '4.1.2'],
        ariaPattern: 'https://www.w3.org/WAI/ARIA/apg/patterns/button/',
        keyboardMap: [
          { keys: ['Enter'], action: 'activate' },
          { keys: ['Space'], action: 'activate' },
        ],
        focus: { initial: 'self', trap: false, restore: null },
        screenReaderAudit: {
          'nvda-2026.1/firefox-126': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
          'voiceover-macos15/safari-17': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
        },
        contrast: { bodyTextMinRatio: 4.5, borderMinRatio: 3.0, apcaLcNonTextMin: 45 },
        targetSize: { desktop: 24, tablet: 32, mobile: 44 },
        shadowDom: { mode: 'open', crossRootAriaStrategy: 'reflective-aria' },
        composedOf: [],
        directionalIcons: [],
      },
    },
  },
  argTypes: {
    label: { control: 'text' },
    disabled: { control: 'boolean' },
  },
};

export default meta;
type Story = StoryObj;

export const Default: Story = {
  args: { label: 'Submit', disabled: false },
  render: (args) => html`<sunfish-button label=${args.label} ?disabled=${args.disabled}></sunfish-button>`,
};

export const Disabled: Story = {
  args: { label: 'Submit', disabled: true },
  render: (args) => html`<sunfish-button label=${args.label} ?disabled=${args.disabled}></sunfish-button>`,
};
```

- [ ] **Step 3: Start Storybook and verify it renders**

```bash
cd packages/ui-core && pnpm storybook
```

Expected: Storybook opens on localhost:6006; the "Core/Button" entry exists; the "Default" story renders a blue button with "Submit" text; the "Accessibility" tab shows axe-core running with 0 violations.

Stop the dev server with Ctrl+C once verified.

- [ ] **Step 4: Commit**

```bash
git add packages/ui-core/src/components/button/
git commit -m "feat(ui-core): pilot sunfish-button component with a11y contract

First pilot component for the Global-First UX Storybook harness. Uses
CSS logical properties (padding-inline, etc.) per spec Section 2.
Declares the full a11y contract in parameters.a11y.sunfish per Section 7.
Verified rendering in Storybook dev mode; axe-core zero violations.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 11: Build `sunfish-dialog` stub + story

**Files:**
- Create: `packages/ui-core/src/components/dialog/sunfish-dialog.ts`
- Create: `packages/ui-core/src/components/dialog/sunfish-dialog.stories.ts`

**Why:** Second pilot component. Tests the harness on a more complex component (focus trap, composition, aria-modal).

- [ ] **Step 1: Write the component source**

```typescript
// packages/ui-core/src/components/dialog/sunfish-dialog.ts
import { LitElement, html, css } from 'lit';
import { customElement, property } from 'lit/decorators.js';

@customElement('sunfish-dialog')
export class SunfishDialog extends LitElement {
  static styles = css`
    :host { display: contents; }
    .backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.5);
      display: none;
      align-items: center;
      justify-content: center;
      z-index: 100;
    }
    :host([open]) .backdrop { display: flex; }
    .dialog {
      background: var(--sf-color-surface, #ffffff);
      color: var(--sf-color-on-surface, #0f172a);
      padding: var(--sf-space-6, 24px);
      border-radius: var(--sf-radius-lg, 8px);
      max-inline-size: min(90vw, 32rem);
      box-shadow: 0 10px 25px rgba(0, 0, 0, 0.15);
    }
    h2 { margin-block-start: 0; }
  `;

  @property({ type: String }) heading = '';
  @property({ type: Boolean, reflect: true }) open = false;

  render() {
    return html`
      <div class="backdrop" @click=${this._onBackdropClick}>
        <div class="dialog" role="dialog" aria-modal="true" aria-labelledby="dialog-title" @click=${this._stopPropagation}>
          <h2 id="dialog-title">${this.heading}</h2>
          <slot></slot>
        </div>
      </div>
    `;
  }

  private _onBackdropClick() {
    this.open = false;
    this.dispatchEvent(new CustomEvent('close'));
  }

  private _stopPropagation(e: Event) {
    e.stopPropagation();
  }

  connectedCallback() {
    super.connectedCallback();
    document.addEventListener('keydown', this._onKeydown);
  }
  disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener('keydown', this._onKeydown);
  }
  private _onKeydown = (e: KeyboardEvent) => {
    if (e.key === 'Escape' && this.open) {
      this.open = false;
      this.dispatchEvent(new CustomEvent('close'));
    }
  };
}
```

- [ ] **Step 2: Write the story with contract**

```typescript
// packages/ui-core/src/components/dialog/sunfish-dialog.stories.ts
import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import './sunfish-dialog.js';
import '../button/sunfish-button.js';

const meta: Meta = {
  title: 'Core/Dialog',
  component: 'sunfish-dialog',
  parameters: {
    a11y: {
      sunfish: {
        wcag22Conformant: ['1.3.1', '1.4.3', '2.1.1', '2.1.2', '2.4.3', '2.4.7', '2.4.11', '2.5.8', '4.1.2'],
        ariaPattern: 'https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/',
        keyboardMap: [
          { keys: ['Escape'], action: 'close' },
          { keys: ['Tab'], action: 'cycle-forward-in-trap' },
          { keys: ['Shift+Tab'], action: 'cycle-backward-in-trap' },
        ],
        focus: { initial: 'first-focusable-child', trap: true, restore: 'trigger' },
        screenReaderAudit: {
          'nvda-2026.1/firefox-126': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
          'voiceover-macos15/safari-17': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
        },
        contrast: { bodyTextMinRatio: 4.5, borderMinRatio: 3.0, apcaLcNonTextMin: 45 },
        targetSize: { desktop: 24, tablet: 32, mobile: 44 },
        shadowDom: { mode: 'open', crossRootAriaStrategy: 'reflective-aria' },
        composedOf: ['sunfish-button'],
        directionalIcons: [],
      },
    },
  },
};

export default meta;
type Story = StoryObj;

export const Default: Story = {
  args: { heading: 'Confirm deletion', open: true },
  render: (args) => html`
    <sunfish-dialog heading=${args.heading} ?open=${args.open}>
      <p>This action cannot be undone.</p>
      <sunfish-button label="Cancel"></sunfish-button>
      <sunfish-button label="Delete"></sunfish-button>
    </sunfish-dialog>
  `,
};
```

- [ ] **Step 3: Verify in Storybook**

```bash
cd packages/ui-core && pnpm storybook
```

Expected: "Core/Dialog" entry appears; "Default" story renders the dialog with a backdrop, heading, body text, and two buttons. Accessibility panel shows zero violations.

- [ ] **Step 4: Commit**

```bash
git add packages/ui-core/src/components/dialog/
git commit -m "feat(ui-core): pilot sunfish-dialog component with a11y contract

Second pilot component. Exercises focus trap, aria-modal, composedOf
(dialog contains buttons), and Escape-to-close keyboard pattern. All
per the WAI-ARIA APG Dialog (Modal) pattern.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 12: Build `sunfish-syncstate-indicator` stub + story

**Files:**
- Create: `packages/ui-core/src/components/syncstate/sunfish-syncstate-indicator.ts`
- Create: `packages/ui-core/src/components/syncstate/sunfish-syncstate-indicator.stories.ts`

**Why:** Third pilot component. Tests the multimodal encoding from Section 5 (color + shape + text + ARIA). This is the highest-risk component since it must validate the specific icons and palette from the P0 closure. SVG icons are inlined as Lit templates for safety — no `innerHTML` usage.

- [ ] **Step 1: Write the component source**

```typescript
// packages/ui-core/src/components/syncstate/sunfish-syncstate-indicator.ts
import { LitElement, html, css, svg, type TemplateResult } from 'lit';
import { customElement, property } from 'lit/decorators.js';

export type SyncState = 'healthy' | 'stale' | 'offline' | 'conflict' | 'quarantine';

// Icons rendered via Lit's safe svg template; no innerHTML.
// Geometries per spec Section 5 P0.1 closure (Material icon names in comments).
function iconFor(state: SyncState): TemplateResult {
  switch (state) {
    case 'healthy': // check_circle
      return svg`<svg viewBox="0 0 24 24" aria-hidden="true">
        <path fill="currentColor" d="M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"/>
      </svg>`;
    case 'stale': // schedule
      return svg`<svg viewBox="0 0 24 24" aria-hidden="true">
        <path fill="currentColor" d="M12 2a10 10 0 100 20 10 10 0 000-20zm.5 5H11v6l5.2 3.2.8-1.3-4.5-2.7z"/>
      </svg>`;
    case 'offline': // cloud_off
      return svg`<svg viewBox="0 0 24 24" aria-hidden="true">
        <path fill="currentColor" d="M19.35 10.04A7.49 7.49 0 0012 4c-1.48 0-2.85.43-4.01 1.17l1.46 1.46A5.5 5.5 0 0117.5 12a5.5 5.5 0 01-5.5 5.5c-.3 0-.58-.03-.86-.08L2.8 9.1A9.01 9.01 0 002 12c0 3.87 3.13 7 7 7h10c2.76 0 5-2.24 5-5s-2.24-5-5-5zM1 4.27l2.28 2.28A8.95 8.95 0 001 12c0 4.97 4.03 9 9 9h8.73l2 2L22.73 22 2.27 3z"/>
      </svg>`;
    case 'conflict': // call_split
      return svg`<svg viewBox="0 0 24 24" aria-hidden="true">
        <path fill="currentColor" d="M14 4l5 5-5 5v-3H9.5l-3 3-1.4-1.4L7.6 10 5 7.4 6.4 6l3 3H14V6z"/>
      </svg>`;
    case 'quarantine': // do_not_disturb_on
      return svg`<svg viewBox="0 0 24 24" aria-hidden="true">
        <circle cx="12" cy="12" r="10" fill="none" stroke="currentColor" stroke-width="2"/>
        <path fill="currentColor" d="M5 11h14v2H5z"/>
      </svg>`;
  }
}

const LABELS_SHORT: Record<SyncState, string> = {
  healthy: 'Synced',
  stale: 'Stale',
  offline: 'Offline',
  conflict: 'Conflict',
  quarantine: 'Held',
};

const LABELS_LONG: Record<SyncState, string> = {
  healthy: 'Synced with all peers',
  stale: 'Last synced earlier',
  offline: 'Offline — saved locally',
  conflict: 'Review required — two versions diverged',
  quarantine: "Can't sync — open diagnostics",
};

@customElement('sunfish-syncstate-indicator')
export class SunfishSyncstateIndicator extends LitElement {
  static styles = css`
    :host { display: inline-flex; align-items: center; gap: var(--sf-space-2, 8px); font: inherit; }
    :host([state="healthy"])    { color: var(--sf-syncstate-healthy-bg, #27ae60); }
    :host([state="stale"])      { color: var(--sf-syncstate-stale-bg, #3498db); }
    :host([state="offline"])    { color: var(--sf-syncstate-offline-bg, #7f8c8d); }
    :host([state="conflict"])   { color: var(--sf-syncstate-conflict-bg, #e67e22); }
    :host([state="quarantine"]) { color: var(--sf-syncstate-quarantine-bg, #c0392b); }
    svg { inline-size: 20px; block-size: 20px; flex-shrink: 0; }
    .label {
      max-inline-size: var(--sf-syncstate-label-max, 28ch);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    :host([form="compact"]) .label { --sf-syncstate-label-max: 10ch; }
  `;

  @property({ type: String, reflect: true }) state: SyncState = 'healthy';
  @property({ type: String, reflect: true }) form: 'compact' | 'standard' = 'standard';

  render() {
    const labelText = this.form === 'compact' ? LABELS_SHORT[this.state] : LABELS_LONG[this.state];
    const role = (this.state === 'conflict' || this.state === 'quarantine') ? 'alert' : 'status';
    return html`
      <span role=${role} aria-atomic="true" aria-label=${LABELS_LONG[this.state]}>
        ${iconFor(this.state)}
        <span class="label">${labelText}</span>
      </span>
    `;
  }
}
```

- [ ] **Step 2: Write the story with contract**

```typescript
// packages/ui-core/src/components/syncstate/sunfish-syncstate-indicator.stories.ts
import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import './sunfish-syncstate-indicator.js';

const meta: Meta = {
  title: 'Core/SyncStateIndicator',
  component: 'sunfish-syncstate-indicator',
  parameters: {
    a11y: {
      sunfish: {
        wcag22Conformant: ['1.3.1', '1.4.3', '1.4.11', '4.1.2', '4.1.3'],
        ariaPattern: 'https://www.w3.org/WAI/ARIA/apg/practices/live-regions/',
        keyboardMap: [],
        focus: { initial: 'none', trap: false, restore: null },
        screenReaderAudit: {
          'nvda-2026.1/firefox-126': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
          'voiceover-macos15/safari-17': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
        },
        contrast: { bodyTextMinRatio: 4.5, borderMinRatio: 3.0, apcaLcNonTextMin: 45 },
        targetSize: { desktop: 24, tablet: 32, mobile: 44 },
        shadowDom: { mode: 'open', crossRootAriaStrategy: 'reflective-aria' },
        composedOf: [],
        directionalIcons: ['conflict'],
      },
    },
  },
};

export default meta;
type Story = StoryObj;

export const AllStates: Story = {
  render: () => html`
    <div style="display: flex; flex-direction: column; gap: 12px; padding: 16px;">
      <sunfish-syncstate-indicator state="healthy"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="stale"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="offline"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="conflict"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="quarantine"></sunfish-syncstate-indicator>
    </div>
  `,
};

export const Compact: Story = {
  render: () => html`
    <div style="display: flex; gap: 16px; padding: 16px;">
      <sunfish-syncstate-indicator state="healthy" form="compact"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="stale" form="compact"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="conflict" form="compact"></sunfish-syncstate-indicator>
    </div>
  `,
};
```

- [ ] **Step 3: Verify in Storybook under RTL**

```bash
cd packages/ui-core && pnpm storybook
```

In the browser:
1. Select "Core/SyncStateIndicator" → "All States"
2. Verify all 5 states render with distinct icons + colors + text
3. Switch direction toolbar to "RTL (ar-SA)" — verify layout flips, icons mirror as declared (only conflict should mirror)
4. Open Accessibility panel — verify zero violations at moderate+ impact

- [ ] **Step 4: Commit**

```bash
git add packages/ui-core/src/components/syncstate/
git commit -m "feat(ui-core): pilot sunfish-syncstate-indicator with multimodal encoding

Third pilot component. Implements the SyncState multimodal encoding from
spec Section 5: five states (healthy/stale/offline/conflict/quarantine),
each with color + shape + text label + ARIA role (status vs alert).
SVG icons rendered via Lit svg template (no innerHTML).
Verified rendering + axe-core clean in both LTR and RTL directions.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 13: Measure CI runtime per component

**Files:**
- Create: `waves/global-ux/week-1-runtime-measurement.md`

**Why:** Spec Section 7 requires this measurement to validate CI runtime budget before cascading.

- [ ] **Step 1: Build Storybook static**

```bash
cd packages/ui-core && time pnpm build-storybook
```

Record the wall-clock time.

- [ ] **Step 2: Run the a11y test-runner and time it**

```bash
cd packages/ui-core && time pnpm test:a11y 2>&1 | tee /tmp/a11y-runtime.log
```

Record:
- Total wall-clock time
- Number of stories tested
- Per-story time (total / count)

- [ ] **Step 3: Extrapolate to full ui-core (estimated 40 components)**

Calculate: `per_story_time * 40 * 3 (themes) * 2 (light/dark) * 2 (LTR/RTL) * 3 (CVD simulations)`.

- [ ] **Step 4: Write the measurement memo**

Create `waves/global-ux/week-1-runtime-measurement.md`:

```markdown
# Week 1 A11y Runtime Measurement

**Date:** 2026-04-28
**Measured against:** 3 pilot stories (button, dialog, syncstate-indicator) × 7 story variants

## Raw numbers
- Build Storybook static: [TIME] seconds
- Run test-runner (7 story variants): [TIME] seconds
- Per-story time: [TIME / 7] seconds

## Extrapolation to full ui-core
Estimated 40 components × 3 themes × 2 light/dark × 2 LTR/RTL × 3 CVD = 1,440 scenarios
Projected CI time: [per-story * 1440 / 4 shards] seconds per shard

## Verdict
[GREEN — fits <15 min p95 budget / YELLOW — requires shard expansion / RED — requires parallelization redesign]
```

Fill in actual numbers from Steps 1-3.

- [ ] **Step 5: Commit**

```bash
git add waves/global-ux/week-1-runtime-measurement.md
git commit -m "docs(global-ux): Week 1 a11y runtime measurement

Per-story a11y test runtime measured on 3 pilot components. Extrapolates
to full ui-core projection. Feeds the Week 1 go/no-go gate criteria
from spec Section 7 (runtime must fit <15 min p95 budget or trigger
matrix parallelization plan).

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 14: Scaffold foundation/Localization + ICU4N wrapper

**Files:**
- Create: `packages/foundation/Localization/ISunfishLocalizer.cs`
- Create: `packages/foundation/Localization/SunfishLocalizer.cs`
- Modify: `packages/foundation/Sunfish.Foundation.csproj` (add ICU4N PackageReference)

**Why:** Spec Section 3A Week 1 pilot — ICU4N wrapper smoke test requires the wrapper to exist first.

- [ ] **Step 1: Add ICU4N to Directory.Packages.props**

Open `Directory.Packages.props` and add:

```xml
<PackageVersion Include="ICU4N" Version="60.1.0-alpha.437" />
```

Check the [ICU4N NuGet page](https://www.nuget.org/packages/ICU4N) for the current stable or beta version matching .NET 8 before commit. Pin the exact version.

- [ ] **Step 2: Add PackageReference to Sunfish.Foundation.csproj**

```xml
<ItemGroup>
  <PackageReference Include="ICU4N" />
</ItemGroup>
```

- [ ] **Step 3: Write `ISunfishLocalizer.cs`**

```csharp
// packages/foundation/Localization/ISunfishLocalizer.cs
using Microsoft.Extensions.Localization;

namespace Sunfish.Foundation.Localization;

/// <summary>
/// ICU-aware localizer wrapping <see cref="IStringLocalizer{T}"/>.
/// Supports CLDR plural rules (Arabic six-form, Japanese/Chinese zero-form),
/// gender variants, and number/date skeletons via ICU MessageFormat.
/// </summary>
public interface ISunfishLocalizer<T>
{
    /// <summary>Simple key lookup; equivalent to IStringLocalizer behavior.</summary>
    string Get(string key);

    /// <summary>Formatted key lookup using ICU MessageFormat pattern.</summary>
    /// <param name="key">Resource key.</param>
    /// <param name="args">Named arguments for the ICU pattern (count, name, etc.).</param>
    string Format(string key, object args);

    /// <summary>Plural-form key lookup. Shortcut for common {count, plural, ...} pattern.</summary>
    string Plural(string key, long count, object? additionalArgs = null);
}
```

- [ ] **Step 4: Write `SunfishLocalizer.cs`**

```csharp
// packages/foundation/Localization/SunfishLocalizer.cs
using System.Globalization;
using System.Reflection;
using ICU4N.Text;
using Microsoft.Extensions.Localization;

namespace Sunfish.Foundation.Localization;

public sealed class SunfishLocalizer<T> : ISunfishLocalizer<T>
{
    private readonly IStringLocalizer<T> _inner;
    private readonly CultureInfo _culture;

    public SunfishLocalizer(IStringLocalizer<T> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _culture = CultureInfo.CurrentUICulture;
    }

    public string Get(string key) => _inner[key].Value;

    public string Format(string key, object args)
    {
        var pattern = _inner[key].Value;
        var messageFormat = new MessageFormat(pattern, ToIcuLocale(_culture));
        var dict = ObjectToDictionary(args);
        return messageFormat.Format(dict);
    }

    public string Plural(string key, long count, object? additionalArgs = null)
    {
        var pattern = _inner[key].Value;
        var messageFormat = new MessageFormat(pattern, ToIcuLocale(_culture));
        var dict = ObjectToDictionary(additionalArgs ?? new { });
        dict["count"] = count;
        return messageFormat.Format(dict);
    }

    private static IDictionary<string, object> ObjectToDictionary(object args)
    {
        var dict = new Dictionary<string, object>();
        foreach (var prop in args.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(args);
            if (value is not null) dict[prop.Name] = value;
        }
        return dict;
    }

    private static ICU4N.Util.ULocale ToIcuLocale(CultureInfo culture) =>
        new(culture.Name.Replace('-', '_'));
}
```

- [ ] **Step 5: Build foundation**

```bash
cd packages/foundation && dotnet build
```

Expected: builds clean; ICU4N dependency restored.

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props packages/foundation/Sunfish.Foundation.csproj packages/foundation/Localization/
git commit -m "feat(foundation): pilot ISunfishLocalizer + ICU4N wrapper

Adds ICU4N package reference and the ISunfishLocalizer<T> wrapper
around IStringLocalizer<T>. Supports CLDR plural rules, gender variants,
and ICU MessageFormat patterns. Feeds the Week 1 smoke-test pilot for
spec Section 3A.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 15: Write the three ICU smoke tests

**Files:**
- Create: `packages/foundation/tests/Localization/SunfishLocalizerIcuTests.cs`
- Create: `packages/foundation/tests/Localization/TestResource.cs`
- Create: `packages/foundation/tests/Resources/TestResource.resx`
- Create: `packages/foundation/tests/Resources/TestResource.ar-SA.resx`
- Create: `packages/foundation/tests/Resources/TestResource.ja.resx`

**Why:** Spec Section 3A binary gate: "ICU4N pilot passes 3 smoke tests (en simple, ar plural-6, ja zero-form)."

- [ ] **Step 1: Write the test resource files**

Create `packages/foundation/tests/Resources/TestResource.resx` (en-US source):

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="greeting"><value>Hello, world</value><comment>Simple greeting; no args.</comment></data>
  <data name="inbox.unread"><value>{count, plural, =0 {No messages} one {1 message} other {# messages}}</value><comment>Inbox count pluralization.</comment></data>
</root>
```

Create `packages/foundation/tests/Resources/TestResource.ar-SA.resx` (Arabic — six plural forms):

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="greeting"><value>مرحبا بالعالم</value><comment>Simple greeting; no args.</comment></data>
  <data name="inbox.unread"><value>{count, plural, zero {لا رسائل} one {رسالة واحدة} two {رسالتان} few {# رسائل} many {# رسالة} other {# رسالة}}</value><comment>Inbox count; Arabic six-form plural.</comment></data>
</root>
```

Create `packages/foundation/tests/Resources/TestResource.ja.resx` (Japanese — zero-form):

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="greeting"><value>こんにちは世界</value><comment>Simple greeting; no args.</comment></data>
  <data name="inbox.unread"><value>{count, plural, other {# 件のメッセージ}}</value><comment>Inbox count; Japanese zero-form (no plural distinction).</comment></data>
</root>
```

- [ ] **Step 2: Write the test marker type**

```csharp
// packages/foundation/tests/Localization/TestResource.cs
namespace Sunfish.Foundation.Tests.Localization;

/// <summary>Marker type for IStringLocalizer&lt;TestResource&gt; in smoke tests.</summary>
public class TestResource { }
```

- [ ] **Step 3: Write the smoke tests**

```csharp
// packages/foundation/tests/Localization/SunfishLocalizerIcuTests.cs
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Sunfish.Foundation.Localization;
using Xunit;

namespace Sunfish.Foundation.Tests.Localization;

public class SunfishLocalizerIcuTests
{
    private static ISunfishLocalizer<TestResource> CreateLocalizer(string culture)
    {
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        var services = new ServiceCollection();
        services.AddLocalization(opt => opt.ResourcesPath = "Resources");
        var provider = services.BuildServiceProvider();
        var inner = provider.GetRequiredService<IStringLocalizer<TestResource>>();
        return new SunfishLocalizer<TestResource>(inner);
    }

    [Fact]
    public void English_SimpleString_ReturnsSourceValue()
    {
        var loc = CreateLocalizer("en-US");
        Assert.Equal("Hello, world", loc.Get("greeting"));
    }

    [Fact]
    public void Arabic_PluralSix_ReturnsCorrectForms()
    {
        var loc = CreateLocalizer("ar-SA");
        Assert.Equal("لا رسائل", loc.Plural("inbox.unread", 0));
        Assert.Equal("رسالة واحدة", loc.Plural("inbox.unread", 1));
        Assert.Equal("رسالتان", loc.Plural("inbox.unread", 2));
        Assert.Equal("3 رسائل", loc.Plural("inbox.unread", 3));
        Assert.Equal("11 رسالة", loc.Plural("inbox.unread", 11));
        Assert.Equal("100 رسالة", loc.Plural("inbox.unread", 100));
    }

    [Fact]
    public void Japanese_ZeroForm_ReturnsSingleFormForAllCounts()
    {
        var loc = CreateLocalizer("ja");
        Assert.Equal("0 件のメッセージ", loc.Plural("inbox.unread", 0));
        Assert.Equal("1 件のメッセージ", loc.Plural("inbox.unread", 1));
        Assert.Equal("5 件のメッセージ", loc.Plural("inbox.unread", 5));
    }
}
```

- [ ] **Step 4: Run the tests**

```bash
cd packages/foundation/tests && dotnet test --filter "FullyQualifiedName~SunfishLocalizerIcuTests"
```

Expected output: `Passed! - Failed: 0, Passed: 3, Skipped: 0`

- [ ] **Step 5: If tests fail**

If any test fails, do NOT attempt to fix the wrapper. Instead:
1. Document the failure mode in `waves/global-ux/decisions.md`
2. Evaluate fallback per spec Section 3A: `OrchardCore.Localization` ICU fork or custom pattern-matcher
3. This is the Week 1 rollback trigger on ICU4N — the go/no-go gate may pivot

- [ ] **Step 6: Commit (only if all 3 pass)**

```bash
git add packages/foundation/tests/Localization/ packages/foundation/tests/Resources/
git commit -m "test(foundation): ICU4N wrapper smoke tests - en+ar+ja

Three smoke tests for Week 1 Section 3A pilot: English simple string,
Arabic six-form plural, Japanese zero-form. All three green; ICU4N
wrapper validated for Phase 1 cascade.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Task 16: Week 1 go/no-go gate decision

**Files:**
- Modify: `waves/global-ux/status.md` (end-of-week update)
- Modify (if no-go): `waves/global-ux/decisions.md`

**Why:** Spec Section 1 rollback criterion. This is the binary decision point.

- [ ] **Step 1: Assess each gate criterion**

Go through the spec's rollback triggers and record pass/fail for each:

| Gate | Pass criteria | Evidence |
|---|---|---|
| ICU4N wrapper | Tasks 14-15 — 3 smoke tests green | `dotnet test` output |
| Storybook harness | Tasks 9-12 — 3 components render + axe clean | Storybook manual check + a11y tab |
| Shadow-DOM traversal | `@axe-core/playwright` reaches shadow roots | Test-runner output from Task 13 |
| CI runtime | `<15 sec` per component projected total | Task 13 measurement memo |
| Weblate legal | No blocking AGPL concern (Task 3 memo) | Research memo verdict |

- [ ] **Step 2: Write the end-of-Week-1 status update**

Open `waves/global-ux/status.md` and replace contents with:

```markdown
# Global-First UX — Wave Status

**Updated:** 2026-05-02 end of Phase 1 Week 1
**Current phase:** Phase 1 Week 1 (Tooling Pilot) — COMPLETE
**Current focus:** Week 1 go/no-go gate decision

## Completed this week
- waves/global-ux/ directory scaffolded with status + decisions
- 3 research memos (ICU4N health, Weblate vs Crowdin, XLIFF tooling)
- ADR 0034 (A11y Harness per Adapter) — Accepted
- ADR 0035 (Global-Domain-Types as separate wave) — Accepted
- pnpm workspace + Storybook 8 config in packages/ui-core
- 3 pilot components (button, dialog, syncstate-indicator) with full a11y contracts
- CI runtime measurement memo
- ICU4N wrapper + 3 smoke tests (en/ar/ja) passing

## Go/No-Go assessment
| Gate | Status | Notes |
|---|---|---|
| ICU4N wrapper | [PASS/FAIL] | [reference evidence] |
| Storybook a11y harness | [PASS/FAIL] | [reference evidence] |
| Shadow-DOM traversal | [PASS/FAIL] | [reference evidence] |
| CI runtime budget | [PASS/FAIL] | [reference evidence] |
| Weblate legal review | [PASS/FAIL] | [reference evidence] |

## Verdict
[GO — proceed to Phase 1 Weeks 2-4 (Plan 2 to be authored)]
OR
[NO-GO — pivot per decisions.md entry YYYY-MM-DD]

## Next agent handoff context
[If GO:] Begin Plan 2 (Loc-Infra cascade) and Plan 3 (Translator-Assist core) in parallel.
[If NO-GO:] Re-plan Week 1 with pivoted tool choices per decisions.md.
```

Fill in the `[PASS/FAIL]` values from actual evidence.

- [ ] **Step 3: If any gate failed, append to decisions.md**

If any gate failed, open `waves/global-ux/decisions.md` and prepend:

```markdown
## 2026-05-02 — Week 1 pivot triggered

**Triggering gate:** [named gate that failed]
**Evidence:** [specific failure]
**Chosen alternative:** [fallback from spec Section 3A/3B/7]
**Impact on timeline:** [added weeks / changed tool / scope cut]

---

```

- [ ] **Step 4: Commit the week-end update**

```bash
git add waves/global-ux/status.md waves/global-ux/decisions.md
git commit -m "docs(global-ux): Phase 1 Week 1 complete - [GO or NO-GO] on gate

[If GO] Week 1 tooling pilot complete. All five gate criteria passed
per waves/global-ux/status.md. Proceeding to Phase 1 Weeks 2-4 planning.

[If NO-GO] Week 1 tooling pilot complete. Gate [X] failed; pivot to
[fallback] recorded in decisions.md. Re-planning Week 1 with revised
tool choice.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

---

## Success Criteria

Phase 1 Week 1 is complete when ALL of:

- ☐ `waves/global-ux/status.md`, `decisions.md`, `README.md` exist and are maintained
- ☐ Three research memos committed to `icm/01_discovery/output/` — all three have a verdict (not pending)
- ☐ ADR 0034 and ADR 0035 are Accepted
- ☐ `packages/ui-core/` has Storybook running with three pilot components
- ☐ `pnpm test:a11y` passes with zero moderate+ violations on the three components in both LTR and RTL
- ☐ CI runtime projection documented in `waves/global-ux/week-1-runtime-measurement.md`
- ☐ `packages/foundation/Localization/SunfishLocalizer.cs` exists with ICU4N wrapper
- ☐ Three ICU smoke tests (en/ar/ja) green per `dotnet test`
- ☐ End-of-Week-1 status update committed with binary GO/NO-GO verdict

## FAILED conditions (triggers Week 1 re-plan, not Phase 1 abort)

If ANY of the following occurs, the Week 1 plan itself is revised — not Phase 1:
- ICU4N wrapper cannot pass the 3 smoke tests (likely a wrapper architecture issue, not a Phase 1 scope issue)
- Storybook `@axe-core/playwright` cannot traverse open shadow-DOM (tooling choice wrong)
- CI runtime projection exceeds 30 minutes (parallelization needed before cascade)

Pivots documented in `waves/global-ux/decisions.md`; Week 1 plan is re-issued with revised tool choices.

---

## Verification

### Automated
- `dotnet test` passes ICU smoke tests (Task 15)
- `pnpm test:a11y` passes Storybook a11y tests (Task 13)
- `pnpm dlx tsc --noEmit` validates Storybook config (Task 9)

### Manual
- Storybook running at `localhost:6006` renders all 3 pilot components (Tasks 10-12)
- RTL direction toolbar flips layout correctly (Task 12 step 3)
- Accessibility tab shows zero violations at moderate+ impact (Task 12 step 3)

### Ongoing observability
- `waves/global-ux/status.md` kept current; every agent updates before session end
- `waves/global-ux/decisions.md` captures any pivot with date + evidence

---

## Handoff

After Week 1 passes the go/no-go gate, Plan 2 (Loc-Infra cascade + XLIFF pipeline) will be authored covering Phase 1 Weeks 2–4. Plans 3, 4, 5, 6 follow in sequence. If Week 1 fails the gate, this plan is re-issued with pivoted tool choices.

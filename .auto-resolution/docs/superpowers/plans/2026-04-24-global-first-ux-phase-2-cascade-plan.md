# Global-First UX — Phase 2 Cascade

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the Phase 1 localization and accessibility infrastructure across the full application surface (kitchen-sink, Bridge, Anchor, docs) in all 12 target locales, with human translators producing real translated content, screen-reader audits refreshed per surface, and platform-specific a11y validated (Narrator, VoiceOver, TalkBack, NVDA) before declaring the global-first UX mandate delivered.

**Architecture:** Phase 2 is app-surface-oriented, not week-oriented. Five sequential-but-overlapping phases (6A–6E). Phase 6A (kitchen-sink + blocks-* demos) is the rehearsal; Phase 6B (Bridge) and Phase 6C (Anchor) deliver the two canonical deployment shapes per paper §20.7; Phase 6D (docs) is the externally-facing knowledge base; Phase 6E is exit validation. Translator batches (paid linguists via Weblate queue) begin at 6A start and run continuously through 6D.

**Tech stack:** Inherited from Phases 1 (Plans 1–5) — SmartFormat.NET 3.6.1, `ISunfishLocalizer<T>`, XLIFF 2.0 MSBuild task, self-hosted Weblate 5.17.1, MADLAD-400-3B-MT backend, Storybook 8 + `@storybook/addon-a11y`, `@axe-core/playwright`, bUnit-to-axe bridge (`ui-adapters-blazor.A11y`). Phase 2 adds: translator-vendor tooling (Smartling-compat XLIFF export if vendor route chosen), NVDA 2026.1 + JAWS 2024 + VoiceOver (macOS 15 + iOS 17) + Narrator (Win11 26200) + TalkBack (Android 15), Accessibility Insights for Web (MS) for Bridge browser-shell audits, WAI-ARIA Authoring Practices 1.2 as the reference pattern library.

**Scope boundary:** This plan covers Phase 2 ONLY (post-exit-gate of Phase 1). It does NOT cover:
- Any Phase 1 infrastructure — Plans 1–5 must have landed and their CI gate must be green on `main` before Phase 6A begins.
- Domain-type migration (`PersonalName`, `Money`, `Address`, `NodaTime`) — that is ADR 0035's separate wave. If `blocks-accounting` needs currency display during Phase 6A, it uses a string-template workaround documented as tech debt.
- New component authoring in `ui-core` / adapters — Phase 2 localizes and a11y-audits what exists at Phase 2 entry. New components land through the Forward Gate (Plan 5), not through Plan 6.
- Post-v1 locale expansion (Korean `bake-in` → `complete`, new locale adds) — post-Phase-2 maintenance, tracked in `waves/global-ux/post-phase-2-roadmap.md`.

**Parent spec:** [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../specs/2026-04-24-global-first-ux-design.md) §1 (Phase 2 Cascade), §3A, §3B, §4, §7, §8 (Forward Gate + exit criteria).
**Predecessor plans:**
- [Plan 1 — Week 1 Tooling Pilot](./2026-04-24-global-first-ux-phase-1-week-1-plan.md) (complete, GO 2026-04-24)
- [Plan 2 — Weeks 2-4 Loc-Infra Cascade](./2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md)
- Plan 3 — Translator-Assist Core (assumed authored; MADLAD CLI + Weblate workflow)
- Plan 4 — A11y Foundation Cascade (ADR 0034 harnesses landed)
- Plan 5 — CI Gates (§8 Forward Gate live on `main`)

---

## Phase-gate precondition (blocking)

**Phase 6A cannot begin until ALL of the following are true:**

- [ ] Plan 5's `.github/workflows/global-ux-gate.yml` has been merged to `main` and is configured as a required status check on the protected branch.
- [ ] Plan 5's CI has been green for ≥ 5 consecutive business days on `main` after merge (validates flakiness is handled, not just "green once").
- [ ] The Phase 1 exit-gate checklist in the spec §8 "Binary gates for Section 8 completion" is fully ticked in `waves/global-ux/status.md`.
- [ ] `locales.json` lists the 12 locales and `i18n/coordinators.md` names a human coordinator for each of the 9 `complete`-tier locales (Plan 2 deliverable).
- [ ] The self-hosted Weblate instance has been operational for ≥ 2 weeks with no P0 outages logged in `infra/weblate/ops-log.md`.
- [ ] A translator vendor contract OR community-translator roster covering all 9 `complete`-tier locales is in writing (purchase order referenced, or signed contributor agreements linked in `i18n/coordinators.md`).

If any precondition fails, block Phase 6A and re-plan the gap in a revised Plan 5.1 — do not "start small" on the cascade before the gate is real. The spec's rationale (Section 8 "Phase 1 exit gate"): without the CI gate, Phase 2 becomes an unenforceable convention.

---

## Context & Why

Phase 1 built the machinery: every new component authored goes through the Forward Gate; every new string lands in a `.resx` with a `<comment>`; every package has a Storybook story with a `parameters.a11y.sunfish` contract; Weblate is running; translators can log in. Phase 2 is the expensive part — actually applying all of that across ~6 application surfaces × 12 locales × multiple flows per surface, commissioning human translators to produce the real content, and running real screen-reader audits on real application flows (not isolated components).

Phase 2 is where the mandate "non-English language support is native, not bolted on" becomes observable from a user's perspective. Before Phase 2, a translator can approve a string in Weblate but no end-user has ever rendered it in an end-user flow. Before Phase 2, a blind user running TalkBack on the Anchor MAUI shell has had no real test. Phase 2 closes both gaps.

---

## Success Criteria

### PASSED — Phase 2 complete; global-first UX mandate delivered

- **Kitchen-sink (6A):** All pages localized through `ISunfishLocalizer<T>`; zero hardcoded user-facing strings (enforced by `SUNFISH_I18N_002` analyzer as Error, per Plan 5). Every component demoed with LTR+RTL toggle and light+dark toggle; axe-core clean at impact ≥ moderate across every combination. All 15 `blocks-*` demo pages localized.
- **Bridge (6B):** Login, tenant workspace selector, browser shell (per ADR 0033), ProblemDetailsFactory errors, all user-facing copy localized. RTL verified on `ar-SA`. Screen-reader audit on 3 core flows (login, tenant-switch, record-list-render) by the a11y lead, logged into each relevant story's `screenReaderAudit` block for NVDA-2026.1/Firefox-126 and VoiceOver-macos15/Safari-17.
- **Anchor (6C):** Workspace switcher (per ADR 0032), team view, onboarding flow localized. MAUI/Blazor-Hybrid surfaces verified with Narrator (Win11), VoiceOver (macOS), and TalkBack (Android) for the 3 core flows. Platform-specific a11y findings logged and resolved or entered into `_shared/engineering/a11y-baseline.md` with target dates.
- **Docs (6D):** `apps/docs` authoritative content in en-US localized into all 12 locales via the same XLIFF → Weblate pipeline used for code. Per-locale search indexing verified. RTL code-sample rendering verified. Nav item ordering respects locale conventions.
- **Translation volume:** ≥ 95% of `complete`-tier locales meet their `completenessFloor` in `locales.json`; ≥ 40% for each `bake-in` locale (he-IL, fa-IR, ko) with 100% layout coverage per RTL snapshot tests.
- **Forward Gate permanent:** Every PR from Phase 2 onward passes the gate without baseline-drift exceptions added at > 2/week average.
- **Telemetry clean:** `i18n.missing_key` telemetry event rate < 0.1% of render events in staging traffic for 1 week before exit declaration.

### FAILED — triggers a re-scope, not a Phase 2 abort

- One app surface stalls (e.g., Anchor's TalkBack audit surfaces a MAUI-Blazor-Hybrid live-region bug that needs an upstream fix) — declare that surface's Phase 2 partial, ship what's green, book the rest as post-Phase-2 maintenance in `waves/global-ux/post-phase-2-roadmap.md`.
- Translator volume exceeds budget — negotiate scope cut with BDFL: ship 9 `complete` locales fully, 3 `bake-in` locales with reduced floor (30% instead of 40%).
- `blocks-*` demo pages prove to have no natural string-seam in one package family (e.g., charts with dynamic axis labels) — descope to Plan 7 (post-Phase-2) for that family only.
- Docs site search indexing breaks per-locale — ship en-US search authoritative, per-locale docs rendered but only English search at Phase-2 exit; track per-locale search as post-Phase-2.

### Kill trigger (12-week timeout)

If Phase 6A has not declared complete by **12 calendar weeks after Plan 5's CI green date**, escalate to BDFL for scope review. Named options are:
(a) Cut blocks-* demo cascade to half the packages (7 of 14) and defer rest to post-Phase-2.
(b) Cut Docs cascade entirely; docs remain en-US-only at v1, translated docs become v1.1 wave.
(c) Cut `bake-in` locales to 2 (he-IL, fa-IR) and defer Korean to v1.1.
(d) Abort Phase 2, ship v1 with Phase 1 complete but no Phase 2 cascade — fall back to "infrastructure shipped, content coming soon" messaging. Only considered if timeline blows out beyond 14 weeks.

Timeout starts from Plan 5 CI-green date, not Phase 2 kickoff, to avoid kickoff-delay-gaming the clock.

---

## Assumptions & Validation

| Assumption | VALIDATE BY | IMPACT IF WRONG |
|---|---|---|
| Plan 5 CI gate is stable enough to block bad PRs without blocking good PRs | Task 6A.0 — 5-business-day green-streak audit on `main`; false-positive rate < 1/week | Plans 5 gets a 5.1 sub-plan to tune the gate before Phase 2 starts |
| Translator vendor (or community roster) can sustain ~30k–60k words/week throughput across 9 `complete`-tier locales | Task 6A.1 — Weblate throughput measurement during 6A pilot (2 weeks); extrapolate to full Phase 2 volume | Budget overrun; re-scope via kill-trigger option (a), (b), or (c) |
| MAUI / Blazor-Hybrid live-region behaviour under TalkBack matches browser behaviour | Task 6C.4 — Anchor TalkBack audit on Android 15 reference device | If live-regions don't announce, need platform-specific announcer (MAUI `ISemanticScreenReader` adapter); scope into 6C |
| Docs site can consume XLIFF → `.md` translations via the same Weblate pipeline as `.resx` | Task 6D.1 — spike with one docs page, one target locale, round-trip through Weblate | If docs format mismatches, fallback is Weblate's native Markdown-file-format support (separate Weblate Component); +2 day slip |
| All 14 confirmed `blocks-*` packages (plus any 15th in flight) have natural string seams | Task 6A.2 — inventory walk; any package without a seam gets a custom localization hook authored as part of that block's cascade | One package family skipped from 6A; re-plan that family separately |
| Axe-core and `@axe-core/playwright` shadow-DOM handling covers Bridge's browser shell per ADR 0033 | Task 6B.3 — spike with browser-shell top-nav axe audit | If shell renders into isolated iframes / shadow roots the axe can't traverse, need `@axe-core/reporter-earl` workaround; +3 day slip |
| Hot-reload from Plan 2 Task 4.1 works on Anchor's MAUI-Blazor-Hybrid surface | Task 6C.1 — Anchor dev-mode `.resx` edit test | If hot-reload requires a Blazor Server circuit not present in Anchor, scope hot-reload to Bridge/kitchen-sink only; doc as Anchor-dev limitation |
| Weblate can handle 12 locales × 6 surfaces × weighted ~8k strings (~575k total unit-translations) | Task 6A.1 load test during Phase 6A pilot; Weblate memory, Postgres connection pool, git-sync latency under projected volume | Weblate VM sizing upgrade (4→8 GB) or DB-tier upgrade; ~$20–40/month added cost |

---

## Translator-Volume Budget

Estimated translation volume and cost, computed at Phase 2 entry. Revise at Phase 6A end based on actual measurements.

### Word-count estimate (per surface, en-US authoritative)

| Surface | Est. user-facing strings | Avg. words/string | Source words (en-US) |
|---|---:|---:|---:|
| `apps/kitchen-sink` | 800 | 5 | 4,000 |
| `blocks-*` demo pages (14 confirmed + 1 in flight = 15 packages × ~50 strings) | 750 | 5 | 3,750 |
| `accelerators/bridge` (login, tenant UI, shell, errors, problem-details) | 1,200 | 6 | 7,200 |
| `accelerators/anchor` (workspace switcher, team view, onboarding, MAUI system messages) | 900 | 6 | 5,400 |
| `apps/docs` (MDX pages: getting-started, guides, API reference prose, 12 ADR summaries) | ~7,500 page-units | 100 | 750,000 |
| `ui-core` + adapter-embedded copy | 400 | 4 | 1,600 |
| **Total source (en-US)** | | | **~772,000 words** |

### Translation volume (× 11 non-source locales)

772,000 × 11 = **~8.5M translated words** for full-complete coverage. However:
- `bake-in` tier (he-IL, fa-IR, ko) cap at 40% coverage → each contributes 0.4 × 772k = 308k words ≈ 924k combined.
- `complete` tier (8 locales × 100%, floor 95%) → 8 × 772k = 6.176M words.
- **Realistic Phase 2 target:** ~7.1M translated words.

### Cost estimate

**Vendor rate (commercial market reference, 2026):**
- Tier 1 (de, fr, ja, es-419, pt-BR, zh-Hans): $0.13–$0.18/word
- Tier 2 (ar-SA, hi): $0.15–$0.22/word (lower supply, higher rate)
- Tier 3 (he-IL, fa-IR, ko `bake-in`): $0.18–$0.25/word

**Blended average:** $0.17/word.

**Estimated total:** 7.1M × $0.17 = **~$1.2M USD** for full vendor-route Phase 2.

**Machine-translation-assisted path (MADLAD-400 pre-draft + human review):**
- Review-only rates average 40–55% of full-translation rates.
- Estimated total with MADLAD pre-drafting: 7.1M × $0.17 × 0.5 = **~$600k USD**.

**Community-plus-vendor hybrid (preferred baseline):**
- Tier-1 commercial locales: vendor route, MADLAD pre-draft + review: $0.17 × 0.5 × 6.176M = ~$525k.
- Tier-2/Tier-3 + `bake-in`: community coordinators + vendor spot-check on legal-sensitive strings: $50k total.
- **Baseline budget:** **~$575k USD** for Phase 2 translation.

**Budget-line recommendation:** allocate $700k (includes ~20% contingency for re-work, glossary churn, and RC-gate polish rounds). If BDFL's finance envelope is lower, trigger kill-trigger option (b) or (c) at Phase 6A/6D boundary — do not attempt to do a global-first launch on a sub-$300k translation budget.

### Calendar-time estimate

- Vendor throughput: standard industry rate is 2,000–2,500 words/day/translator.
- Per-locale throughput target: 1 dedicated translator + 1 reviewer @ 2,000 words/day = ~10,000 words/week/locale.
- To hit 7.1M words across 11 locales in parallel = ~6,500 words/week/locale → achievable with ~0.7 FTE/locale.
- **Calendar target:** 8–10 weeks of sustained translation work, overlapping Phases 6A–6D.

---

## File Structure (Phase 2 deliverables)

```
apps/kitchen-sink/
  Resources/SharedResource.resx                     ← Expanded from Plan 2 sentinel to full copy
  Resources/SharedResource.{11 locales}.resx        ← Populated via Weblate
  Pages/**/*.razor                                  ← @inject IStringLocalizer<T> across every page
  wwwroot/css/kitchen-sink.css                      ← RTL-safe via Plan 2 logical-props cascade
  tests/LocalizationCoverage.Tests.csproj           ← Asserts no hardcoded user-facing strings

accelerators/bridge/
  Pages/Login.razor                                 ← Localized
  Pages/TenantWorkspace.razor                       ← Localized
  Components/BrowserShell/**.razor                  ← Localized per ADR 0033 render model
  Problem/SunfishProblemDetailsFactory.cs            ← Already registered in Plan 2; surface strings localized
  Resources/SharedResource.{12 locales}.resx        ← Populated
  tests/E2E/LocalizedFlows.cs                        ← Playwright E2E for login flow in en-US + ar-SA + ja
  tests/A11y/CoreFlowsAudit.cs                       ← Screen-reader audit harness per ADR 0034

accelerators/anchor/
  Views/WorkspaceSwitcher.razor                     ← Localized per ADR 0032
  Views/TeamView.razor                              ← Localized
  Views/Onboarding/**.razor                         ← Localized
  Platforms/Windows/NarratorAdapter.cs              ← MAUI-specific platform a11y bridge (if needed per 6C.4)
  Platforms/MacCatalyst/VoiceOverAdapter.cs         ← Same for macOS
  Platforms/Android/TalkBackAdapter.cs              ← Same for Android
  Resources/SharedResource.{12 locales}.resx        ← Populated
  tests/A11y/PlatformScreenReaderTests/              ← Platform-gated tests per OS

apps/docs/
  docs.config.ts                                    ← Locale-aware i18n config (Docusaurus/VitePress/whatever is in use)
  i18n/                                             ← Per-locale translated page roots
    ar-SA/, de/, es-419/, fr/, hi/, ja/, pt-BR/, zh-Hans/, he-IL/, fa-IR/, ko/
  src/theme/SearchBar.tsx                           ← Locale-scoped search indexing
  src/components/CodeSample.tsx                     ← RTL-safe syntax highlighting
  tests/docs-i18n-roundtrip.test.ts                  ← Spike + regression for MDX round-trip

packages/blocks-{accounting,assets,businesscases,forms,inspections,leases,maintenance,rent-collection,scheduling,subscriptions,tasks,tax-reporting,tenant-admin,workflow}/
  Resources/SharedResource.{12 locales}.resx        ← Populated from sentinel (Plan 2 Task 3.5 delivered the sentinels)
  Demo/**.razor                                     ← @inject IStringLocalizer<T> across kitchen-sink demo pages

ui-adapters-blazor.A11y/                            ← Plan 4 delivered; Phase 2 consumes for Anchor + Bridge Razor audits
ui-adapters-react.A11y/                             ← Plan 4 delivered; Phase 2 N/A for Bridge at v1 (React surface minimal)

waves/global-ux/
  phase-2-entry-report.md                            ← Task 6A.0 deliverable
  phase-6a-kitchen-sink-cascade-report.md            ← Task 6A.5 deliverable
  phase-6b-bridge-cascade-report.md                  ← Task 6B.5 deliverable
  phase-6c-anchor-cascade-report.md                  ← Task 6C.5 deliverable
  phase-6c-platform-sr-audit-report.md               ← Task 6C.4 deliverable
  phase-6d-docs-cascade-report.md                    ← Task 6D.5 deliverable
  phase-6e-exit-report.md                            ← Task 6E.3 deliverable
  post-phase-2-roadmap.md                            ← Task 6E.2 deliverable
  translator-throughput-log.md                       ← Continuously updated; source for budget reconciliation
```

---

## Phase 6A — kitchen-sink + blocks-* demo cascade

**Rationale:** Kitchen-sink is the component playground. Every change here is low-trust-impact (no production users), high-feedback (subagents can iterate quickly), and it's the rehearsal surface for the patterns that land in Bridge and Anchor. If a pattern breaks in kitchen-sink it's cheap to fix; if it first breaks in Bridge it's expensive.

### Task 6A.0: Phase-2 entry verification

**Files:**
- Create: `waves/global-ux/phase-2-entry-report.md`

**Why:** Encodes the phase-gate preconditions listed at the top of this plan into an auditable artifact. Prevents "close-enough" entry.

- [ ] **Step 1:** Capture evidence for each of the 6 preconditions: link to Plan 5 merge commit, 5-business-day green-streak CI logs, §8 binary-gate checklist file, `i18n/coordinators.md`, Weblate ops log, translator contract reference.
- [ ] **Step 2:** Write the entry report. Each precondition is PASS or BLOCKED. No DEFERRED allowed — the gate is binary.
- [ ] **Step 3:** Commit (`docs(global-ux): Phase 2 entry verification — all preconditions PASS`). If any BLOCKED, do not advance; open an issue tagged `phase-2-blocked` and pause.

### Task 6A.1: Translator throughput pilot (2 weeks)

**Files:**
- Create: `waves/global-ux/translator-throughput-log.md` (updated weekly through Phase 2)

**Why:** The cost and calendar-time estimates above are projections. This task produces the real measurement to reconcile against budget.

- [ ] **Step 1:** Engage the translator vendor (or first-tier community coordinators) on kitchen-sink `apps/kitchen-sink/Resources/SharedResource.resx` content in 3 locales: `de`, `ja`, `ar-SA`. These three cover Germanic-expansion, CJK-compression, and RTL — the three hardest dimensions.
- [ ] **Step 2:** Measure: words/day/translator, time-from-source-change-to-approved-translation, glossary-drift incidents, machine-pre-draft acceptance rate (how often is the MADLAD suggestion accepted unchanged vs. edited vs. rejected).
- [ ] **Step 3:** After 2 weeks, update the budget table in this plan with real numbers. If measured throughput < 60% of estimate, escalate to BDFL per kill-trigger.

### Task 6A.2: kitchen-sink page-inventory + string-seam walk

**Files:**
- Create: `waves/global-ux/phase-6a-kitchen-sink-inventory.md`

**Why:** Cascade works if every user-facing string has a natural `IStringLocalizer<T>` seam. This task makes the absence of a seam visible before cascade writes broken code.

- [ ] **Step 1:** Enumerate every page / component / dialog / toast / error message in `apps/kitchen-sink/`. Expect 80–120 distinct surfaces.
- [ ] **Step 2:** For each surface, record: page name, string count (rough), seam kind (Razor @inject, code-behind service, static constant, third-party component passthrough). Flag any with seam kind "none — hardcoded".
- [ ] **Step 3:** Commit (`docs(global-ux): Phase 6A kitchen-sink inventory`).

### Task 6A.3: kitchen-sink cascade (subagent-driven)

**Files:**
- Modify: every `apps/kitchen-sink/Pages/**/*.razor` + `apps/kitchen-sink/Components/**/*.razor`
- Modify: `apps/kitchen-sink/Resources/SharedResource.resx` (add entries as encountered)
- Modify: `apps/kitchen-sink/Resources/SharedResource.{11 locales}.resx` (auto-populated by sunfish-translate; reviewed in Weblate)

**Why:** This is the expensive part of Phase 6A. Dispatch 4 subagents, one per page-cluster (Components, Pages/Features, Pages/Demos, Pages/Docs-embed). Reviewer-agent gates per cluster. Same pattern as Plan 2 Task 3.5.

- [ ] **Step 1:** Dispatch 4 subagents with path-scoped commit mandate. Each subagent's deliverable: one commit per page-cluster with `@inject IStringLocalizer<SharedResource> Loc` added, all user-facing strings externalized to `.resx`, `<comment>` attached to every new `<data>` entry.
- [ ] **Step 2:** After each cluster commit, run the reviewer-agent (spec-compliance + code-quality check). Block next dispatch until reviewer passes.
- [ ] **Step 3:** Run `SUNFISH_I18N_002 (NoHardcodedStrings)` analyzer from Plan 5 as Error severity on the kitchen-sink project. Any surviving hardcoded string either goes into `.resx` or gets `[UnlocalizedOk]` with justification.
- [ ] **Step 4:** Pipe the new en-US keys to Weblate via the XLIFF export MSBuild task. Translators begin work; track in `translator-throughput-log.md`.

### Task 6A.4: blocks-* demo-page cascade (15 packages)

**Files:**
- Modify: per-package `Demo/*.razor` under each of the 14 confirmed + 1 in-flight `blocks-*` package
- Modify: per-package `Resources/SharedResource.resx` (expand Plan 2 sentinels to full copy)
- Modify: per-package `Resources/SharedResource.{11 locales}.resx` (populated via Weblate)

**Why:** Plan 2 scaffolded `.resx` sentinels in every `blocks-*`. Phase 6A Task 6A.4 fills them with real demo-page copy. Each block's demo page is ~30–50 user-facing strings.

- [ ] **Step 1:** Dispatch 5 subagents in two waves (same cluster split as Plan 2 Task 3.5): Wave 1 = `blocks-finance-ish` (accounting, tax-reporting, rent-collection, subscriptions) + `blocks-crm-ish` (businesscases, forms, leases, tenant-admin, workflow, tasks); Wave 2 = `blocks-ops` (assets, inspections, maintenance, scheduling).
- [ ] **Step 2:** Each subagent: expand the sentinel `.resx` with the demo-page copy for its cluster's packages; wire `@inject IStringLocalizer<T>` in each demo page; commit per-cluster path-scoped.
- [ ] **Step 3:** Reviewer-agent gates each cluster. Dispatch Wave 2 only after Wave 1 is reviewer-approved.
- [ ] **Step 4:** Any package with "seam kind: none" from the Task 6A.2 inventory gets custom hook added (usually a `SunfishLoc.Get(...)` helper service) — scope-creep guard: if a package needs more than 1 day to add the hook, descope it and book into post-Phase-2.

### Task 6A.5: Phase 6A exit report

**Files:**
- Create: `waves/global-ux/phase-6a-kitchen-sink-cascade-report.md`

- [ ] **Step 1:** Score against Phase 6A success criteria: kitchen-sink pages localized (count), blocks-* packages cascaded (count), analyzer errors remaining (count), translator throughput measured (words/day), budget reconciliation.
- [ ] **Step 2:** Verdict: PROCEED to Phase 6B OR trigger kill-trigger option (a) if blocks-* cascade stalled.
- [ ] **Step 3:** Commit.

---

## Phase 6B — Bridge (Zone C Hybrid SaaS shell)

**Rationale:** Bridge is the hosted-node-as-SaaS accelerator (paper §20.7; ADR 0031). Localization here is user-facing to tenants: login → tenant-selector → browser shell → record-class surfaces. Screen-reader audits here are browser-based (NVDA, VoiceOver, JAWS), not platform-specific.

### Task 6B.1: Bridge inventory + string-seam walk

**Files:**
- Create: `waves/global-ux/phase-6b-bridge-inventory.md`

- [ ] **Step 1:** Enumerate user-facing surfaces: Login, TenantSelector, BrowserShell (per ADR 0033 render model — includes top nav, sidebar, record pane), Settings, Account, ProblemDetails error rendering, email templates (welcome email, password reset).
- [ ] **Step 2:** Record per-surface string counts, seam kinds, and a11y contract status (has-story / missing-story / partial).
- [ ] **Step 3:** Email templates: flag separately — they go through the same `.resx` pipeline but render server-side in the user's culture at send time, not at build time. Plan 5's analyzer does NOT cover email templates; add a separate test harness.

### Task 6B.2: Bridge cascade

**Files:**
- Modify: `accelerators/bridge/Pages/Login.razor`, `Pages/TenantWorkspace.razor`, `Components/BrowserShell/**.razor`, `Components/Settings/**.razor`, `Problem/SunfishProblemDetailsFactory.cs` surface strings, `Infrastructure/Email/Templates/**.cshtml` (email templates)
- Modify: `accelerators/bridge/Resources/SharedResource.{12 locales}.resx`

- [ ] **Step 1:** Dispatch 3 subagents: (a) auth+tenant (Login, TenantWorkspace, TenantSelector); (b) browser-shell (BrowserShell components per ADR 0033); (c) settings+errors (Settings, Account, ProblemDetails, email templates).
- [ ] **Step 2:** Each subagent: inject `IStringLocalizer<SharedResource>`, externalize all user-facing strings, commit path-scoped. Reviewer-agent between.
- [ ] **Step 3:** ProblemDetailsFactory: Plan 2 Task 4.2 already registered the factory. Phase 6B ensures the catch-all error copy ("An unexpected error occurred", "Please try again", "Contact support") is sourced from `.resx` and has `<comment>` translator notes explaining the use context.
- [ ] **Step 4:** Email templates: use the `ISunfishLocalizer<T>` in the email-render service; test that en-US and ar-SA and ja produce correct direction and content.

### Task 6B.3: Bridge RTL + zoom + reflow validation

**Files:**
- Create: `accelerators/bridge/tests/E2E/LocalizedFlows.cs` (Playwright E2E)
- Modify: `accelerators/bridge/tests/A11y/**` with RTL-specific cases

- [ ] **Step 1:** Playwright E2E: login → tenant-select → record-list under `en-US`, `ar-SA`, `ja`. Assert: layout-bearing elements don't overflow at 100% zoom, 200% zoom, 400% zoom; no horizontal scroll under `ar-SA`.
- [ ] **Step 2:** ADR 0033 browser-shell spike: axe-core spike-test against the shell top-nav under `@axe-core/playwright`. If the shell renders into isolated iframes/shadow roots the axe can't traverse, apply the `@axe-core/reporter-earl` workaround per the assumption table.
- [ ] **Step 3:** Commit.

### Task 6B.4: Bridge screen-reader audit (3 core flows × 2 SR/browser pairs)

**Files:**
- Modify: Bridge stories — populate `screenReaderAudit` blocks in the relevant component stories
- Create: `waves/global-ux/phase-6b-sr-audit-log.md`

**Why:** Spec §7 requires audit entries with auditor + date + pass state, refreshable per 12-month policy. This is the real audit, done on real running flows.

- [ ] **Step 1:** Flows: (1) first-time login (en-US), (2) tenant-switch (ar-SA), (3) record-list-render (ja). Each flow executed twice: once with NVDA-2026.1 + Firefox-126, once with VoiceOver-macos15 + Safari-17. Auditor is the a11y lead (named in `i18n/coordinators.md` Phase 1 deliverable).
- [ ] **Step 2:** Log findings in `phase-6b-sr-audit-log.md`. Each finding: severity, description, remediation, target date. Blocking findings (broken focus order, missing labels) resolved in-plan; non-blocking findings entered into `_shared/engineering/a11y-baseline.md`.
- [ ] **Step 3:** Update each relevant component story's `screenReaderAudit` block: `{ verified: '2026-XX-XX', auditor: '@a11y-lead', pass: true }` for each of the 2 SR/browser pairs.

### Task 6B.5: Phase 6B exit report

**Files:**
- Create: `waves/global-ux/phase-6b-bridge-cascade-report.md`

- [ ] Score against Phase 6B success criteria; verdict; commit.

---

## Phase 6C — Anchor (Zone A local-first desktop)

**Rationale:** Anchor is the local-first desktop implementation (paper §20.7; ADR 0032 multi-team v2). It is MAUI/Blazor-Hybrid — different platform a11y surfaces than Bridge's pure-browser shell. Phase 6C has unique risk around platform screen readers (Narrator, VoiceOver, TalkBack) that browser-only audits miss.

### Task 6C.1: Anchor inventory + MAUI hot-reload validation

**Files:**
- Create: `waves/global-ux/phase-6c-anchor-inventory.md`

- [ ] **Step 1:** Enumerate user-facing Anchor surfaces: WorkspaceSwitcher (per ADR 0032 v2 workspace switching), TeamView, Onboarding flow, Settings, local-first sync-state surfaces, diagnostics/quarantine surfaces.
- [ ] **Step 2:** Spike: validate Plan 2 Task 4.1 hot-reload on MAUI-Blazor-Hybrid. MAUI's Blazor host does not use Blazor Server circuits the same way. If hot-reload doesn't work, document as Anchor-dev limitation; developers rebuild for `.resx` changes during Phase 6C — acceptable, Anchor dev is less frequent than Bridge dev.
- [ ] **Step 3:** Seam-walk per package (as Phase 6A.2).

### Task 6C.2: Anchor cascade

**Files:**
- Modify: `accelerators/anchor/Views/**` Razor files
- Modify: `accelerators/anchor/Resources/SharedResource.{12 locales}.resx`

- [ ] **Step 1:** Dispatch 2 subagents: (a) WorkspaceSwitcher + TeamView; (b) Onboarding + Settings + sync-state surfaces + diagnostics.
- [ ] **Step 2:** Each subagent: inject `IStringLocalizer<SharedResource>`, externalize, commit path-scoped.
- [ ] **Step 3:** MAUI system messages (permissions dialogs, OS-level notifications): these pass through platform APIs, not `.resx`. Document as platform-native-localization (OS handles via resource bundles) — Anchor just ships the correct key into the platform API.

### Task 6C.3: Anchor RTL layout validation

**Files:**
- Create: `accelerators/anchor/tests/LayoutTests/RtlLayoutTests.cs`

- [ ] **Step 1:** MAUI-Blazor-Hybrid under `dir="rtl"` — does the Blazor-Hybrid WebView respect `html[dir]`? Yes on Windows/macOS WebView2 and iOS WKWebView; TalkBack/Android WebView requires explicit setting.
- [ ] **Step 2:** Test: launch Anchor with `CultureInfo.CurrentUICulture = ar-SA`; capture layout screenshots on each platform; compare to baseline.

### Task 6C.4: Anchor platform-screen-reader audit (3 flows × 3 platforms)

**Files:**
- Create: `waves/global-ux/phase-6c-platform-sr-audit-report.md`
- Create (as needed): `accelerators/anchor/Platforms/Windows/NarratorAdapter.cs`, `Platforms/MacCatalyst/VoiceOverAdapter.cs`, `Platforms/Android/TalkBackAdapter.cs`

**Why:** This is the highest-risk Phase 6C task. Browser-axe coverage does not catch MAUI platform a11y gaps. The Blazor-Hybrid WebView layer sometimes fails to forward live-region announcements to the native platform screen reader, so the user sees the UI update but hears nothing.

- [ ] **Step 1:** Test flows on 3 reference devices: (a) Win11 26200 + Narrator; (b) macOS 15 + VoiceOver; (c) Android 15 Pixel reference + TalkBack. Flows: workspace-switch, team-view-load, onboarding-step-advance.
- [ ] **Step 2:** Log each platform's behaviour per flow: does the live region announce? Is focus order correct when moving between native and WebView layers? Are labels read in the right culture?
- [ ] **Step 3:** If Blazor-Hybrid WebView fails to forward live-regions: author `{Platform}Adapter.cs` bridging via MAUI's `ISemanticScreenReader` — scope the adapter into this plan, not post-Phase-2. Each adapter is ~200 LOC.
- [ ] **Step 4:** Update each relevant story's `screenReaderAudit` block with the platform-specific audit entries: `{ verified: '2026-XX-XX', auditor: '@a11y-lead', pass: true, platform: 'narrator-win11-26200' }` etc.

### Task 6C.5: Phase 6C exit report

**Files:**
- Create: `waves/global-ux/phase-6c-anchor-cascade-report.md`

- [ ] Score; verdict; commit.

---

## Phase 6D — Docs cascade

**Rationale:** The docs site is authoritative en-US. Making it available in 12 locales is the final user-facing polish. Docs-specific concerns that don't apply to code localization: search indexing per locale, code-sample RTL rendering, navigation item ordering (e.g., "Getting Started" appears first in LTR, last in RTL reading order).

### Task 6D.1: Docs XLIFF round-trip spike

**Files:**
- Create: `apps/docs/src/test/i18n-roundtrip.test.ts`
- Create: `waves/global-ux/phase-6d-spike-memo.md`

**Why:** Validate the XLIFF → Markdown assumption before committing docs to the Weblate pipeline.

- [ ] **Step 1:** Pick one docs page (recommend `apps/docs/content/getting-started.md`). Export to XLIFF via a custom script or Weblate's Markdown format handler; import to Weblate; translate one paragraph to `ja`; push back; re-render `apps/docs` with `?locale=ja` URL parameter.
- [ ] **Step 2:** If MDX components (React components embedded in docs) round-trip cleanly, proceed. If not, fallback to Weblate's native Markdown-file-format handler as a separate Weblate Component.
- [ ] **Step 3:** Write the spike memo; commit.

### Task 6D.2: Docs site locale-aware config

**Files:**
- Modify: `apps/docs/docs.config.ts` (or equivalent — Docusaurus/VitePress/Astro)
- Create: `apps/docs/i18n/{locale}/` for each of the 11 non-source locales

- [ ] **Step 1:** Configure docs framework's built-in i18n (Docusaurus has `i18n` config; VitePress has `locales`). Default en-US; 11 additional locales.
- [ ] **Step 2:** Locale-aware URL structure: `/ar-SA/getting-started/` etc. Fall-through to en-US if a page missing in locale.
- [ ] **Step 3:** Locale toggle UI in docs navigation.

### Task 6D.3: Docs content translation (batch)

**Files:**
- Populate: `apps/docs/i18n/{locale}/content/**` across 11 locales via Weblate

- [ ] **Step 1:** Hand off to translator vendor; track throughput in `translator-throughput-log.md`.
- [ ] **Step 2:** Glossary enforcement per Plan 2 Task 3.3. "Sunfish," "Anchor," "Bridge," "Block" remain untranslated; architecture and product vocabulary consistent across docs pages.
- [ ] **Step 3:** Docs translation is the bulk word count (~750k en-US source; ~8M total translated). Expect this phase to span 6–8 weeks.

### Task 6D.4: Docs-specific polish

**Files:**
- Modify: `apps/docs/src/theme/SearchBar.tsx` (locale-scoped Algolia / Pagefind index)
- Modify: `apps/docs/src/components/CodeSample.tsx` (RTL-safe highlighting)
- Modify: `apps/docs/src/theme/Nav.tsx` (nav item ordering under RTL)

- [ ] **Step 1:** Search: per-locale index. Recommend Pagefind (runs at build time, no external SaaS). Each locale has its own index shard; query routes based on current locale.
- [ ] **Step 2:** Code samples: code itself remains LTR regardless of page direction (code grammar is LTR even in Arabic docs). Wrap `<pre><code>` in `dir="ltr"` explicitly.
- [ ] **Step 3:** Nav ordering: verify the "Getting Started" → "Guides" → "API Reference" → "ADRs" order reads naturally in RTL — right-to-left in the sidebar. CSS `flex-direction` on the nav container with logical-property values handles this automatically (Plan 2 sweep); verify no hard-coded `flex-direction: row` remains.

### Task 6D.5: Phase 6D exit report

**Files:**
- Create: `waves/global-ux/phase-6d-docs-cascade-report.md`

- [ ] Score; verdict; commit.

---

## Phase 6E — Exit validation

### Task 6E.1: End-to-end regression across all 12 locales

**Files:**
- Create: `waves/global-ux/phase-6e-regression-matrix.md`

- [ ] **Step 1:** Matrix test: for each locale, execute 3 scenarios (Bridge login, Anchor workspace-switch, Docs getting-started) and capture: layout OK, strings translated to `completenessFloor`, no `i18n.missing_key` telemetry spikes, axe-core clean.
- [ ] **Step 2:** 12 × 3 = 36 scenarios. Any failure triggers in-plan fix (not post-Phase-2 deferral) unless it's a kill-trigger category.
- [ ] **Step 3:** Telemetry validation: stage Bridge for 1 week with synthetic traffic across all 12 locales; `i18n.missing_key` rate < 0.1% of render events.

### Task 6E.2: Post-Phase-2 roadmap

**Files:**
- Create: `waves/global-ux/post-phase-2-roadmap.md`

- [ ] **Step 1:** Enumerate everything descoped from Phase 2: deferred blocks-* packages, `bake-in` tier locales below floor, docs search gaps, any MAUI platform a11y findings parked in `_shared/engineering/a11y-baseline.md` with target dates.
- [ ] **Step 2:** Group by owner. Assign to post-Phase-2 waves (v1.1 translation expansion; v1.2 Korean `complete` promotion; etc.).
- [ ] **Step 3:** Commit.

### Task 6E.3: Phase 2 exit report + GO/NO-GO on v1 release readiness

**Files:**
- Create: `waves/global-ux/phase-6e-exit-report.md`
- Modify: `waves/global-ux/status.md` (Phase 2 complete)

**Why:** This is the binary phase-end gate. A PASS here means the Global-First UX mandate is delivered; Sunfish is ready to declare v1 candidate-release on the UX-layer mandate.

- [ ] **Step 1:** Compile all Phase 6A–6D exit reports into one.
- [ ] **Step 2:** Score against the Phase 2 exit criteria enumerated in Success Criteria above.
- [ ] **Step 3:** Verdict:
  - **PASSED** → Phase 2 complete; global-first UX mandate delivered. Forward Gate remains permanent; post-Phase-2 roadmap is active.
  - **PARTIAL** → Name specific deferrals; attach to post-Phase-2 roadmap; still ship v1 with documented gaps.
  - **FAILED** → Trigger kill-trigger option (a)/(b)/(c)/(d); escalate to BDFL.
- [ ] **Step 4:** Commit.

---

## Verification

### Automated

- `dotnet build -warnaserror` green on all cascaded surfaces with `SUNFISH_I18N_{001,002,003,004}` at Error
- Storybook test-runner green with zero axe-core violations at impact ≥ moderate across kitchen-sink, blocks-* demos, Bridge, Anchor stories
- Playwright E2E green for Bridge login + tenant-switch flow under en-US, ar-SA, ja
- `tooling/locale-completeness-check/` green per Plan 5 CI gate for every cascaded package
- Docs i18n round-trip regression test green
- Telemetry dashboard: `i18n.missing_key` rate < 0.1% over rolling 7-day window

### Manual

- Kitchen-sink walkthrough under `ar-SA` — every page renders RTL; no horizontal scroll; no English leaks in user-facing copy; light/dark toggle preserved.
- Bridge E2E walkthrough under 3 locales by 3 different reviewers (native speaker per locale).
- Anchor platform-SR audit on 3 reference devices (Win11+Narrator, macOS15+VoiceOver, Android15+TalkBack), auditor-signed.
- Docs site visual check under 3 RTL/CJK locales.

### Ongoing observability

- `i18n.missing_key` telemetry continuous monitoring post-Phase-2; alert threshold: > 0.5% for > 4 hours.
- Translator throughput continuously logged in `translator-throughput-log.md`.
- Per-release CVD emulation regression from Plan 5 infrastructure.
- Quarterly SR matrix re-audit per `accessibility.md` baseline-drift policy — audit entries older than 12 months fail CI until refreshed.

---

## Conditional sections

### Rollback Strategy

- **kitchen-sink cascade stall (Phase 6A):** Reduce to ~50 highest-traffic surfaces; defer remainder; proceed to 6B. Accept that kitchen-sink is not 100% cascade-complete at Phase-2 exit.
- **Bridge browser-shell axe-core untraversable (Phase 6B):** Apply `@axe-core/reporter-earl` workaround or mount the shell into a test-only Playwright context with shadow DOM flattened; +3 day slip; if still untraversable, accept shell-level audits as manual-only at v1 and CI-automated in v1.1.
- **Anchor platform-SR gap (Phase 6C):** If one platform (likely Android TalkBack under Blazor-Hybrid WebView) cannot be resolved with the platform-adapter pattern, ship Anchor with that platform marked "preview" for v1 and full a11y in v1.x. Document prominently in Anchor release notes.
- **Docs translation budget overrun (Phase 6D):** Ship en-US authoritative docs + partial translations (Tier-1 locales only: de, fr, ja, es-419, pt-BR, zh-Hans). Defer Tier-2 (ar-SA, hi) and `bake-in` locale docs to v1.1.
- **CI runtime exceeds 20-minute p95 under cascade load:** Expand matrix shards (4→8→16) per Plan 5 remediation path; never relax thresholds.

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Translator-vendor throughput undershoots projection by > 40% | Medium | High (schedule slip 4+ weeks) | Task 6A.1 pilot measurement; kill-trigger (b) or (c) on miss |
| MAUI-Blazor-Hybrid platform SR gaps (especially Android TalkBack) require custom adapters | Medium-High | Medium | Task 6C.4 explicit platform adapters; budget 2 person-weeks per adapter |
| `blocks-*` demo page has no clean string seam (e.g., chart axis labels) | Medium | Low-Medium | Task 6A.2 inventory catches; descope that package to post-Phase-2 if > 1 day fix |
| Docs MDX components don't round-trip through XLIFF cleanly | Medium | Medium | Task 6D.1 spike; fallback to Markdown-format Weblate Component |
| Weblate instance saturates under 12-locale × ~770k source-word load | Low-Medium | Medium | VM-size upgrade (4→8 GB); Postgres tier upgrade; Task 6A.1 captures this |
| Screen-reader audit finding reveals a foundational contract bug (requires ui-core fix, not Phase 2 cosmetic) | Low | High | Route to ui-core via Plan 5's Forward Gate; pause that story's Phase 2 localization until ui-core fix lands |
| Translation quality fails native-speaker review at RC gate | Medium | High | Glossary enforcement (Plan 2 Task 3.3); vendor SLA includes review rounds; budget contingency covers 1 additional review pass per locale |
| Cultural-appropriateness finding on an icon or color in a specific locale | Low-Medium | Medium | CVD + cultural review as part of native-speaker-review contract; swap icon/color per locale via CSS custom property override (mechanism exists per Plan 1 `sunfish-syncstate-indicator`) |

### Dependencies & Blockers

- **Depends on:** Plan 5 CI green on `main` for 5 business days ✅ (precondition) + all Phase 1 exit-gate criteria ✅
- **Depends on:** ADR 0031 (Bridge Zone-C model), ADR 0032 (Anchor v2 multi-team), ADR 0033 (browser-shell render model), ADR 0034 (a11y harness per adapter), ADR 0035 (domain-type wave boundary) — all Accepted at Phase 2 start
- **Depends on:** Translator vendor contract OR community-coordinator roster signed
- **Blocks:** v1 release candidate gate on global-first UX mandate (marketing narrative)
- **Blocks:** Post-Phase-2 waves (Korean `complete` promotion, domain-type migration activation, new-locale adds)
- **External:** MADLAD-400 weights on HuggingFace (pinned per Plan 2 Task 2.5); Weblate 5.17.1 Docker image (pinned); translator-vendor SLAs

### Delegation & Team Strategy

- **Phase 6A (kitchen-sink + blocks-*):** Subagent fleet — 4 subagents for kitchen-sink clusters, 5 subagents for blocks-* in 2 waves. Reviewer-agent serial between cluster commits. Highest parallelism phase.
- **Phase 6B (Bridge):** Smaller fleet — 3 subagents (auth+tenant, browser-shell, settings+errors). Reviewer-agent stricter here; Bridge is closer to production than kitchen-sink.
- **Phase 6C (Anchor):** Solo-by-Claude preferred — MAUI platform adapters need careful cross-platform reasoning; subagents can easily produce divergent platform code. Dispatch exactly 2 subagents only for the non-platform cascade (Razor Views), then foreground Claude handles platform-specific adapter work.
- **Phase 6D (Docs):** Translator-vendor-led, not agent-led. Subagents handle config and polish tasks only (Tasks 6D.2, 6D.4). Content flow is vendor → Weblate → merge.
- **Phase 6E (exit validation):** Solo-by-Claude for the regression matrix and exit report. Native-speaker reviewers (non-Claude) handle the manual 12-locale walkthrough.

### Incremental Delivery

- **End of Phase 6A (~3 weeks in):** kitchen-sink fully localized and screen-reader-audited for core flows; blocks-* demo pages localized; translator throughput validated; go/no-go for remaining Phase 2.
- **End of Phase 6B (~5 weeks in):** Bridge ready for staged rollout to a small set of early-access tenants under `ar-SA` + `ja` + `de` locales. "Soft launch" of the mandate.
- **End of Phase 6C (~7 weeks in):** Anchor ready for staged rollout to desktop early-access users with all 3 platform screen readers validated.
- **End of Phase 6D (~9 weeks in):** Docs site live in 12 locales; marketing narrative ("first global-first Sunfish release") substantiated.
- **End of Phase 6E (~10 weeks in):** v1-RC gate can declare the global-first UX mandate delivered. Post-Phase-2 roadmap active for v1.1 and beyond.

### User Validation

- **Native-speaker review per `complete` locale:** each of 8 locales gets at least one native-speaker reviewer doing a 2-hour walkthrough of Bridge + Anchor core flows. Findings logged; blocking findings fixed in-plan.
- **Disability-community reviewer per core flow:** spec §Mandate lists six disability groups. For Bridge Phase 6B, recruit at least one reviewer from each of: low-vision, blindness (screen-reader dependent), motor/dexterity (keyboard-only), cognitive/neurodivergent. Their findings are first-class inputs, not checkbox-compliance.
- **Cultural-appropriateness spot-check:** for `ar-SA`, `hi`, `ja`, `zh-Hans` — native reviewer spot-checks 20 random user-facing strings for cultural tone/formality (not just linguistic correctness).

### Post-Completion Plan

- Forward Gate remains permanent in `pull_request_template.md` + required status checks on `main`. Every PR after Phase 2 continues to enforce the contract; no regression.
- Quarterly SR matrix re-audit cycle starts at Phase 2 exit date. Calendar event created in team ops calendar.
- Per-release CVD regression, baseline-drift report, `locales.json` review are continuous.
- Post-Phase-2 waves (domain-type migration per ADR 0035, Korean `complete` promotion, locale expansion beyond 12) get their own ADRs, intake, and plans via ICM.
- `waves/global-ux/` kept as the enduring artifact tree for the mandate — do not delete; it is the auditable record of how the mandate shipped.

### Budget & Resources

- **Translator cost:** ~$575k baseline; $700k with contingency. Vendor-led Tier 1 + community-led Tier 2/3 `bake-in`. Revise at Phase 6A end per real throughput.
- **Weblate ops:** $20–40/month incremental if VM upgrade needed (4→8 GB Hetzner CX32).
- **Screen-reader audit staffing:** a11y lead (in-team); 3 native SR-dependent reviewers ~$5k total for the 3-flow × 3-platform audits in Phase 6C + 3-flow × 2-SR/browser audits in Phase 6B.
- **Cultural-appropriateness reviewers:** ~$3k for 4 locales × 2 hours × rate.
- **Contingency for re-work / glossary churn / RC polish rounds:** ~$140k (included in $700k).
- **Total Phase 2 envelope (cash):** ~$745k.
- **Calendar:** 6–10 weeks (baseline 8 weeks; 10-week stretch if all risks fire; 6 weeks only if translator throughput overshoots projection).

### Reference Library

- [Phase 1 Week 1 Plan 1](./2026-04-24-global-first-ux-phase-1-week-1-plan.md)
- [Phase 1 Weeks 2-4 Plan 2](./2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md)
- Plan 3 (Translator-Assist Core) — landed pre-Phase-2
- Plan 4 (A11y Foundation Cascade) — landed pre-Phase-2
- Plan 5 (CI Gates + Forward Gate) — landed pre-Phase-2
- Parent spec [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../specs/2026-04-24-global-first-ux-design.md)
- [ADR 0031 — Bridge Zone-C Hybrid Multi-Tenant SaaS](../../adrs/0031-bridge-hybrid-multi-tenant-saas.md)
- [ADR 0032 — Anchor v2 multi-team workspace switching](../../adrs/0032-multi-team-anchor-workspace-switching.md)
- [ADR 0033 — Browser-shell render model and trust posture](../../adrs/0033-browser-shell-render-model-and-trust-posture.md)
- [ADR 0034 — A11y harness per adapter](../../adrs/0034-a11y-harness-per-adapter.md)
- [ADR 0035 — Global-domain-types as separate wave](../../adrs/0035-global-domain-types-as-separate-wave.md)
- [`_shared/design/internationalization.md`](../../../_shared/design/internationalization.md)
- [`_shared/design/accessibility.md`](../../../_shared/design/accessibility.md)
- [`_shared/product/local-node-architecture-paper.md`](../../../_shared/product/local-node-architecture-paper.md) §20.7 (Accelerator zones)
- WAI-ARIA Authoring Practices 1.2: https://www.w3.org/WAI/ARIA/apg/
- WCAG 2.2 AA: https://www.w3.org/TR/WCAG22/
- Weblate Markdown format: https://docs.weblate.org/en/latest/formats/markdown.html
- Pagefind (locale-aware search for docs): https://pagefind.app/
- MAUI `ISemanticScreenReader` reference: https://learn.microsoft.com/en-us/dotnet/maui/user-interface/accessibility

### Learning & Knowledge Capture

- Append to `waves/global-ux/decisions.md` on each material pivot (translator-vendor change; platform-adapter scope increase; docs MDX → Markdown fallback).
- End-of-Phase-6A retrospective: what surprised us about translator throughput vs. estimate? What should Phase 6B tighten?
- End-of-Phase-6C retrospective: what should a future Anchor-like MAUI project budget for platform-SR work on day 1?
- Phase-2-exit retrospective (in `phase-6e-exit-report.md`): what 3 lessons would we carry into v1.x and v2 locale expansion? Document in `_shared/engineering/coding-standards.md` where the lesson touches cross-repo patterns.

### Replanning Triggers

- **Translator throughput < 60% of estimate at end of Task 6A.1:** Pause Phase 6A before the main cascade; re-plan with kill-trigger option (a), (b), or (c).
- **Phase 6A stalls > 3 weeks:** Invoke kill-trigger option (a) — reduce blocks-* cascade to 7 packages; proceed to 6B with partial blocks-* coverage.
- **Phase 6C platform-SR gap blocks one platform:** If a fix is > 2 weeks of engineering, ship that platform as "preview" quality for v1; re-plan in v1.x.
- **Phase 6D translation budget exceeds $700k:** Trigger option (b); docs ship English-authoritative with Tier-1 locales only.
- **Any phase's CI runtime p95 exceeds 20 minutes:** Expand CI matrix shards per Plan 5 remediation; never relax the threshold.
- **Native-speaker review at Phase 6E surfaces a systemic translation-quality issue:** Pause exit; budget a review-pass round ($50k from contingency); re-run exit gate. Do not ship with degraded translation quality to hit a date.

### Completion Gate

Phase 2 is DONE when ALL of:
- Phase 6A, 6B, 6C, 6D, 6E exit reports committed with PASS verdicts
- `waves/global-ux/status.md` updated with Phase 2 complete
- Phase 2 exit criteria in Success Criteria section all ticked
- Post-Phase-2 roadmap published
- Forward Gate remains green for 2 weeks post-Phase-2 declaration (observability window — catches any regression from the exit push itself)

---

## Cold Start Test

A fresh agent walking into this plan should be able to execute Task 6A.0 without further context by:
1. Reading this plan.
2. Reading Plan 2 for the cascade-subagent pattern and path-scoped commit convention.
3. Reading the parent spec §1 and §7 for Phase 2 scope and Forward Gate definition.
4. Reading ADRs 0031–0035 for accelerator architecture, adapter harnesses, and wave boundaries.
5. Reading `waves/global-ux/decisions.md` for cumulative tool-choice history.

No additional context should be required. If any step requires out-of-band knowledge not in one of those five sources, that is a plan-hygiene bug — file an issue and update this plan before executing.

---

## Handoff

After Phase 2 exit PASSES, v1 release candidate gate can cite the global-first UX mandate as delivered. Post-Phase-2 maintenance (quarterly SR re-audit, per-release CVD regression, baseline-drift review) continues indefinitely under the Forward Gate's permanent status. Post-Phase-2 roadmap (`waves/global-ux/post-phase-2-roadmap.md`) seeds v1.1 and v1.2 waves for locale expansion, `bake-in` → `complete` promotions, and the ADR-0035 domain-type migration.

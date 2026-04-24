# Global-First UX — Design Spec

**Status:** Draft — pending user review
**Date:** 2026-04-24
**Owner:** Chris Wood (@chriswood)
**Scope:** Sunfish v1 Phase 1 foundation + Phase 2 cascade; Forward Gate permanent
**Related ICM request:** `icm/00_intake/output/book-gap-analysis-intake-2026-04-24.md`

---

## Mandate

Sunfish's Pillar 5 (Inclusive by default) — from [`_shared/product/vision.md`](../../../_shared/product/vision.md) — is operationalized as two non-negotiable platform commitments at v1:

1. **Users with disabilities are first-class citizens of the UX.** All six disability groups (low-vision/blindness, motor/dexterity, cognitive/neurodivergent, deaf/hard-of-hearing, color vision deficiency, vestibular/photosensitive) get intentional design patterns from the first component, not compliance passes. WCAG 2.2 AA is the floor; specific criteria target AAA per `accessibility.md`.
2. **Non-English language support is native, not bolted on.** Twelve locales ship at v1 across LTR + RTL + CJK + Devanagari script families. Translation is human-reviewed; machine drafts are never auto-published. Cultural and linguistic correctness is evidenced, not asserted.

This spec operationalizes both commitments through infrastructure, code conventions, component contracts, and CI gates — not documentation alone.

---

## Prerequisites and references

| Document | Role |
|---|---|
| [`_shared/design/internationalization.md`](../../../_shared/design/internationalization.md) | Existing i18n spec this builds on (accepted 2026-04-19) |
| [`_shared/design/accessibility.md`](../../../_shared/design/accessibility.md) | Existing a11y spec this builds on (accepted 2026-04-20) |
| [`_shared/product/vision.md`](../../../_shared/product/vision.md) §Pillar 5 | Mandate origin |
| [`_shared/product/local-node-architecture-paper.md`](../../../_shared/product/local-node-architecture-paper.md) | Foundational architecture paper |
| Book *The Inverted Stack* (`C:\Projects\the-inverted-stack\chapters`) — ch01 deployment scenarios, ch20 sync UX | Mandate reinforcement; India/MENA/LATAM deployment alignment |
| [`icm/07_review/output/global-ux-design/syncstate-encoding-council-review-2026-04-24.md`](../../../icm/07_review/output/global-ux-design/syncstate-encoding-council-review-2026-04-24.md) | Adversarial council review output |
| [`icm/07_review/output/global-ux-design/p0-closure-2026-04-24.md`](../../../icm/07_review/output/global-ux-design/p0-closure-2026-04-24.md) | P0 blocking-item closure evidence |

---

## 1. Overall Structure

Two phases plus a permanent gate. This section describes the sequencing; Sections 2–8 describe the content.

### Phase 1 — Foundation (6–8 weeks)

Phase 1 establishes patterns and infrastructure in the foundation layer, then exits before any cascade work.

**Packages touched in Phase 1 (enumerated):**

| Package | Role |
|---|---|
| `packages/foundation` | `SyncState` enum refinement; `MotionPreference`; `ISunfishUserPreferences` extension |
| `packages/foundation-localfirst` | Composition-root wiring for `IStringLocalizer` |
| `packages/ui-core` | CSS logical properties sweep; Storybook harness; 8-point a11y contracts; multimodal `SyncStateIndicator`; all Phase 1 component contracts |
| `packages/kernel-runtime` | `IStringLocalizer<T>` consumption for error messages |
| `packages/analyzers` | Five Roslyn analyzers (Section 8) |
| `tooling/sunfish-translate/` | **NEW** — MADLAD-400 CLI |
| `tooling/theme-validator/` | **NEW** — CSS palette gate |
| `tooling/a11y-audit-runner/` | **NEW** — Storybook + axe orchestrator |
| `tooling/css-logical-props-lint/` | **NEW** — physical-direction CSS ban |
| `tooling/locale-completeness-check/` | **NEW** — enforces `locales.json` floors |
| `apps/kitchen-sink` | Storybook integration + Phase 1 demo surface |
| `accelerators/anchor` | `RequestLocalizationMiddleware` equivalent in MAUI composition root |
| `accelerators/bridge` | `RequestLocalizationMiddleware` wired; per-tenant locale resolution |

**Phase 1 explicitly does NOT touch:**
- Any `blocks-*` package (cascade in Phase 2)
- `ui-adapters-blazor` beyond composition root (cascade in Phase 2)
- `ui-adapters-react` (deferred cascade in Phase 2, already minimal)
- Federation, ingestion, compat packages
- Any domain modeling (`PersonalName`, `Money`, `Address`, `NodaTime` — see Section 3C)

### Phase 2 — Cascade (6–10 weeks)

Apply Phase 1 patterns to the remaining 53 packages. Parallelizable via dispatched agents. Driven by the Forward Gate — every PR enforces the Phase 1 contract.

### Forward Gate (permanent)

No component merges to `main` without:
- [ ] `IStringLocalizer<T>` injected; all user-visible strings externalized to `.resx`
- [ ] Storybook story with `parameters.a11y.sunfish` contract section current
- [ ] `axe-core` zero violations at impact ≥ moderate (per Section 7)
- [ ] RTL snapshot passing for layout-bearing components
- [ ] Reduced motion honored for any declared animations

Enforced in `.github/workflows/global-ux-gate.yml` as required status checks (Section 8). The gate is the mechanism; conventions are not enforced.

### Rollback criterion (end of Phase 1 Week 1 go/no-go)

After the 3-component pilot (`sunfish-button`, `sunfish-dialog`, `sunfish-syncstate-indicator`) — per the Week 1 breakdown in Section 7 — a go/no-go decision is made at the Week 1/Week 2 boundary. Triggers rollback if any of:
- SmartFormat.NET wrapper fails 3 smoke tests (en simple, ar plural-6, ja single-form)
- Storybook harness cannot traverse open shadow-DOM with `@axe-core/playwright`
- `axe-core` runtime per component exceeds 15 seconds
- Weblate AGPL legal review returns blocking concern

Rollback path: pause Phase 1, evaluate fallback tool choices (OrchardCore ICU fork; alternative to Storybook; Crowdin instead of Weblate), restart from decision point. Document pivot in `waves/global-ux/decisions.md`.

### Resume Protocol

`waves/global-ux/status.md` maintained throughout the sprint:
- Current phase + week
- Completed this week
- In progress (with owner)
- Blocked (with description)
- Next-agent handoff context

Updated at end of each work session. Enables agent handoffs without context loss.

### Post-Phase-2 maintenance

After cascade complete, ongoing regression prevention:
- Forward Gate permanent in `pull_request_template.md` + CI required checks
- Quarterly re-audit of SR matrix per `accessibility.md` baseline-drift policy
- Per-release: glossary/TMX review per locale coordinator
- Per-release: CVD simulation regression on all provider themes via Chrome DevTools MCP
- Annual: full WCAG 2.2 AA conformance review

---

## 2. CSS Logical Properties

Every physical directional CSS property in `ui-core` and `foundation` is replaced with its logical equivalent. This is the RTL foundation; everything else builds on it.

### Replacement table

| Physical (removed) | Logical (target) |
|---|---|
| `padding-left` / `padding-right` | `padding-inline-start` / `padding-inline-end` |
| `margin-left` / `margin-right` | `margin-inline-start` / `margin-inline-end` |
| `border-left` / `border-right` | `border-inline-start` / `border-inline-end` |
| `left` / `right` (in `position`) | `inset-inline-start` / `inset-inline-end` |
| `text-align: left` / `right` | `text-align: start` / `end` |
| `float: left` / `right` | `float: inline-start` / `inline-end` |
| `border-top-left-radius` / `-right-radius` | `border-start-start-radius` / `-end-radius` |
| `border-bottom-left-radius` / `-right-radius` | `border-end-start-radius` / `-end-radius` |

When `html[dir="rtl"]` is set (via the locale resolution chain when active locale is Arabic, Hebrew, or Farsi), the browser mirrors logical properties automatically. No per-component overrides needed.

### Edge cases that don't map to logical properties

| Edge case | Handling |
|---|---|
| `background-position: left 8px top 4px` | CSS custom property `var(--inset-inline-start-offset)` or documented `[dir="rtl"]` override in allowlist |
| `transform-origin: left center` | `transform-origin: var(--origin-inline-start) center` via CSS custom property |
| Icon SVG paths with directional geometry | `directionalIcons` array in component's Storybook story (Section 7); mirrored via `scaleX(-1)` under `:dir(rtl)` |
| `background-image: linear-gradient(to right, ...)` | `linear-gradient(to inline-end, ...)` where supported (Chrome 99+); otherwise flip under `:dir(rtl)` |

### Icon mirror manifest (from Section 5 P0 closure)

| Icon | Library | Mirror under RTL |
|---|---|---|
| `check_circle` (Healthy) | Material | no |
| `schedule` (Stale) | Material | **no** — clocks never mirror |
| `cloud_off` (Offline) | Material | no — symmetric |
| `call_split` (ConflictPending) | Material | **yes** — directional flow |
| `do_not_disturb_on` (Quarantine) | Material | no — symmetric |

### Binary gates for Section 2

- ☐ `tooling/css-logical-props-lint/` runs against `packages/ui-core` and `packages/foundation*` with zero violations and zero allowlist entries
- ☐ Every Phase 1 component story includes an `ar-SA` RTL snapshot
- ☐ Chrome DevTools MCP emulates RTL on kitchen-sink; zero horizontal scrolling at 100% zoom in any provider theme

### Tooling

`tooling/css-logical-props-lint/` — Node.js script scanning `_shared/design/tokens/**/*.css`, `packages/*/src/**/*.{css,scss}`, `packages/*/components/**/*.razor.css` for banned physical-direction regexes. Exits non-zero on match. Allowlist at `tooling/css-logical-props-lint/allowlist.txt` requires justification comment per entry.

### Browser support

CSS logical properties are fully supported in all Sunfish-targeted browsers (Chrome 69+, Firefox 41+, Safari 12.1+, Edge 79+). Logical border-radius (`border-start-start-radius`) requires Chrome 89+, Firefox 66+, Safari 15+. No polyfill needed at Phase 1.

---

## 3. Localization Infrastructure

Split into three workstreams. **3C is excluded from this sprint** — it is a domain-modeling initiative that runs separately.

### 3A — Loc-Infra (Phase 1, 4 weeks)

`IStringLocalizer<T>` wiring, SmartFormat.NET wrapper (pivoted from ICU4N 2026-04-25), XLIFF 2.0 build pipeline, translator-comments analyzer, error localization, dev hot-reload.

**Per-package layout:**

```
packages/<pkg>/
├── Resources/
│   ├── SharedResource.cs         ← marker type for IStringLocalizer<SharedResource>
│   ├── SharedResource.resx       ← en-US source
│   └── SharedResource.ar-SA.resx ← added as translations arrive (via Weblate + MADLAD)
└── <pkg>.csproj                  ← <NeutralLanguage>en-US</NeutralLanguage>
```

`SharedResource.cs` is an empty marker class — the generic type parameter for `IStringLocalizer<T>`.

**Startup registration** (Bridge + Anchor + local-node-host):

```csharp
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supported = LocalesJson.LoadSupportedCultures();
    options.DefaultRequestCulture = new("en-US");
    options.SupportedCultures = supported;
    options.SupportedUICultures = supported;
});
builder.Services.AddControllersWithViews().AddDataAnnotationsLocalization();
app.UseRequestLocalization();
```

**Component injection:**

```csharp
@inject IStringLocalizer<SharedResource> Loc
<button @onclick="Save">@Loc["form.save"]</button>
```

Stable dotted keys — never English-text-as-key. Editing en-US copy edits the value; translations stay attached.

**CLDR plural rules via SmartFormat.NET** *(revised 2026-04-25; see [`waves/global-ux/decisions.md`](../../../waves/global-ux/decisions.md))*

Custom wrapper `ISunfishLocalizer` built on [SmartFormat.NET](https://github.com/axuno/SmartFormat) (MIT) for CLDR plural rules (Arabic six-form, Japanese/Chinese zero-form). The original design specified [ICU4N](https://github.com/NightOwl888/ICU4N); the Week-0 triage memo ([`icm/01_discovery/output/icu4n-health-check-2026-04-25.md`](../../../icm/01_discovery/output/icu4n-health-check-2026-04-25.md)) found ICU4N bundles ICU 60 / CLDR 32 (Oct 2017) versus upstream CLDR 48.2 (Mar 2026) — 16 major versions behind, with Arabic/Hindi plural-rule refinements materially stale. SmartFormat.NET tracks CLDR independently and supports ICU-style `{count:plural:...}` syntax. Number / date / currency formatting delegate to .NET 8+ `System.Globalization` in ICU mode (bundles ICU 74+).

Public contract (`Get` / `Format` / `Plural`) is implementation-independent so a future swap back to a healthy ICU4N successor does not ripple into callers.

```csharp
@SunfishLoc.Plural("inbox.unread", count, args: new { count })
```

**XLIFF 2.0 build pipeline:**

Custom MSBuild task (`Sunfish.Tooling.LocalizationXliff`) converts `.resx` ↔ XLIFF 2.0. Translators work in XLIFF; build artifacts are `.resx` and satellite assemblies. One interchange format, aligned with the JS-side `@lit/localize` XLIFF pipeline.

**Server-side error localization:**

`ProblemDetailsFactory` resolves `title` and `detail` through `IStringLocalizer<T>` using request culture. Domain exceptions carry a `string MessageKey` field; exception-wrapping middleware renders the key through the localizer. Business-logic exceptions thrown deep in aggregates reach the user in the user's locale.

**Dev hot-reload:**

Custom `IStringLocalizerFactory` in `Debug` builds uses `FileSystemWatcher` on `Resources/*.resx`; changes propagate within 3 seconds. Blazor Server only at v1; WASM deferred.

**Tool choices + fallbacks:**

| Tool | Chosen | Fallback-1 | Fallback-2 |
|---|---|---|---|
| Plural / message formatter | SmartFormat.NET (MIT, CLDR-current) | `OrchardCore.Localization` ICU fork | Custom pattern-matcher (CLDR JSON + hand-rolled plural rules). **Originally ICU4N; pivoted 2026-04-25 — see decisions.md.** |
| XLIFF version | 2.0 | 1.2 (broader tool support) | PO files (OrchardCore pattern) |
| XLIFF tool | Custom MSBuild task | [Multilingual App Toolkit](https://learn.microsoft.com/windows/apps/design/globalizing/use-mat) (1.2 with conversion step) | Hand-edited XLIFF |

**Week 0 research triage (before Phase 1 Day 1):**
- ICU4N maintenance health check (last 6 months of commits; CLDR version lag) — **DONE 2026-04-25; verdict PIVOT to SmartFormat.NET.**
- XLIFF 2.0 vs 1.2 tool ecosystem survey — **DONE 2026-04-26; verdict BUILD custom MSBuild task (~5.5 days).**

**Binary gates for 3A:**
- ☐ `grep -r 'AddLocalization()' | wc -l` returns ≥ 3 (Bridge, Anchor, local-node-host)
- ☐ Every `packages/*` has `Resources/SharedResource.resx` with at least one entry
- ☐ `dotnet build` passes with `.resx` analyzer enforcing `<comment>` field
- ☐ XLIFF round-trip: `.resx` → XLIFF 2.0 → `.resx` is byte-identical
- ☑ SmartFormat.NET pilot passes 3 smoke tests (en simple, ar plural-6, ja single-form) — **DONE 2026-04-24; 3/3 green in 80 ms. See [`packages/foundation/tests/Localization/SunfishLocalizerSmartFormatTests.cs`](../../../packages/foundation/tests/Localization/SunfishLocalizerSmartFormatTests.cs).**
- ☐ `ProblemDetailsFactory` returns localized title+detail in en-US, ar-SA, ja
- ☐ Hot-reload: `.resx` edit reflects in kitchen-sink demo within 3 seconds (Blazor Server)

### 3B — Translator-Assist (Phase 1 + Phase 2)

MADLAD-400 pre-publish translation drafts, Weblate deployment, extraction automation, glossary enforcement.

**Architecture — translation model is dev-time and CI only, never in shipped binaries:**

```
┌─ PRE-PUBLISH (dev + CI only) ──────────────────────────────┐
│  en-US.resx → sunfish-translate (MADLAD-400) → draft.xliff  │
│                                                    │         │
│                                                    ▼         │
│                                          Weblate import      │
│                                                    │         │
│                                                    ▼         │
│                                 Human translator reviews     │
│                                 + glossary enforcement       │
│                                 + approves                   │
│                                                    │         │
│                                                    ▼         │
│                                          ar-SA.resx          │
└─────────────────────────────────────────────────── │ ────────┘
                                                    ▼
┌─ BUILD (MSBuild) ──────────────────────────────────────────┐
│  ar-SA.resx → satellite assembly ar-SA/Sunfish.UI.*.dll     │
└──────────────────────────────────────────────────── │ ──────┘
                                                    ▼
┌─ RUNTIME (shipped to users) ────────────────────────────────┐
│  IStringLocalizer<T> → reads satellite assembly             │
│  NO MODEL. NO GPU. NO LLM.                                  │
└─────────────────────────────────────────────────────────────┘
```

**Tool pick: MADLAD-400**

- **License:** Apache 2.0 (model) + ODC-BY (dataset) — fully commercial-friendly
- **Coverage:** 419 languages including all Sunfish v1 targets
- **Architecture:** T5-based; mature inference tooling
- **Sizes:** 3B-MT (dev workstation; 8–12 GB VRAM or CPU via GGUF); 7B-MT (CI/Sunfish-hosted; 16–24 GB)
- **Rejected alternatives:** NLLB-200 (CC-BY-NC 4.0 — noncommercial; disqualifying), Aya Expanse (CC-BY-NC), SeamlessM4T v1 (CC-BY-NC). SeamlessM4T v2 is MIT but speech-heavy; deferred.

**Translation platform pick: Weblate (self-hosted, AGPL)**

Week 0 memo required before Phase 1 Day 1: AGPL network-service implications if translation-as-a-service commercializes; translator UX; MADLAD integration; cost to self-host vs SaaS; operational burden. Default pick if memo unresolved: Weblate self-hosted with AGPL concession documented.

**Fallbacks:** Crowdin (SaaS with data-sovereignty caveat); direct PR workflow with `.resx` files if no platform.

**Workflow:**

1. Developer adds `@Loc["form.save"]`; source generator adds key to en-US `.resx`
2. CI nightly job runs `sunfish-translate` against all supported locales for new keys
3. Output XLIFF marks generated segments `state="needs-review"` — never `state="final"`
4. Weblate imports XLIFF as machine-translation suggestions
5. Human translator (language coordinator per `i18n/coordinators.md`) reviews, edits, approves
6. Approved XLIFF flows back to `.resx` via MSBuild task
7. `NoDraftInCompleteLocale` Roslyn analyzer fails CI if any `.resx` in a `complete`-tier locale contains `state="needs-review"` segments

**Glossary enforcement:**

`i18n/glossary/<locale>.tbx` (TermBase eXchange) locks domain terms before MADLAD inference. "Tenant" in Sunfish means SaaS customer, not renter; glossary enforces the distinction.

**Extraction automation (Phase 2):**

MSBuild task `Sunfish.Tooling.LocalizationExtractor` scans component sources for `@Loc["..."]` and `_loc["..."]` calls; emits key stubs into `.resx` with `<comment>` scaffolding.

**Binary gates for 3B (Phase 1):**
- ☐ `sunfish-translate --source en-US --target ar-SA --input <resx>` produces valid XLIFF 2.0 with `state="needs-review"`
- ☐ MADLAD-400-3B-MT runs locally in < 15 seconds per 100 segments on reference dev machine (16GB RAM, no GPU)
- ☐ CI nightly job creates PR with draft translations when new keys are added
- ☐ Weblate reachable at `i18n.sunfish.internal`, git-synced to `i18n/weblate-staging` branch
- ☐ `NoDraftInCompleteLocale` fires CI failure on test `.resx` with draft segment in en-US

### 3C — Global-Domain-Types

**OUT OF SCOPE for this spec.**

`PersonalName`, `Money`, `Address` value objects; `NodaTime` migration. Runs in its own wave with its own ADR (**ADR 0035 — Global-Domain-Types as separate wave**, to be written before Phase 1 begins). Rationale: domain-modeling concerns with 10–16 week timeline, breaking schema changes, incompatible with a 6-week infrastructure sprint.

When 3C ships, the already-wired `IStringLocalizer<T>` consumes the new value objects transparently — no UX-layer rework.

---

## 4. `locales.json` — Authoritative Locale List

Lives at repo root as `i18n/locales.json`. The single source of truth for supported locales.

### Shape

```jsonc
{
  "source": "en-US",
  "locales": [
    {
      "tag": "en-US",
      "name": "English (United States)",
      "nativeName": "English (United States)",
      "direction": "ltr",
      "coordinator": "@chriswood",
      "status": "complete",
      "completenessFloor": 100,
      "calendar": "gregory",
      "numberingSystem": "latn",
      "firstDayOfWeek": "sunday",
      "tenantOverrideAllowed": true,
      "userOverrideAllowed": true,
      "rationale": "source locale"
    },
    { "tag": "es-419",  "direction": "ltr", "status": "complete", "completenessFloor": 95, "calendar": "gregory",          "numberingSystem": "latn",    "rationale": "commercial tier-1 — LATAM flagship market" },
    { "tag": "pt-BR",   "direction": "ltr", "status": "complete", "completenessFloor": 95, "calendar": "gregory",          "numberingSystem": "latn",    "rationale": "commercial tier-1 — Brazil" },
    { "tag": "fr",      "direction": "ltr", "status": "complete", "completenessFloor": 95, "calendar": "gregory",          "numberingSystem": "latn",    "rationale": "commercial tier-1 — EU + Africa + Canada" },
    { "tag": "de",      "direction": "ltr", "status": "complete", "completenessFloor": 95, "calendar": "gregory",          "numberingSystem": "latn",    "rationale": "commercial tier-1 — DACH enterprise" },
    { "tag": "ja",      "direction": "ltr", "status": "complete", "completenessFloor": 95, "calendar": "japanese",         "numberingSystem": "latn",    "rationale": "commercial tier-1 — Japan enterprise" },
    { "tag": "zh-Hans", "direction": "ltr", "status": "complete", "completenessFloor": 95, "calendar": "gregory",          "numberingSystem": "latn",    "rationale": "commercial tier-1 — China" },
    { "tag": "ar-SA",   "direction": "rtl", "status": "complete", "completenessFloor": 95, "calendar": "islamic-umalqura", "numberingSystem": "arab",    "rationale": "commercial tier-1 — MENA + RTL proof locale" },
    { "tag": "hi",      "direction": "ltr", "status": "complete", "completenessFloor": 95, "calendar": "gregory",          "numberingSystem": "latn",    "rationale": "mission-aligned tier-1 per book primary deployment market (India)" },
    { "tag": "he-IL",   "direction": "rtl", "status": "bake-in",  "completenessFloor": 40, "calendar": "hebrew",           "numberingSystem": "latn",    "rationale": "bake-in tier — second RTL script + Hebrew calendar" },
    { "tag": "fa-IR",   "direction": "rtl", "status": "bake-in",  "completenessFloor": 40, "calendar": "persian",          "numberingSystem": "arabext", "rationale": "bake-in tier — RTL + Persian calendar" },
    { "tag": "ko",      "direction": "ltr", "status": "bake-in",  "completenessFloor": 40, "calendar": "gregory",          "numberingSystem": "latn",    "rationale": "bake-in tier — CJK completeness; Korean enterprise reachable in v1.x" }
  ]
}
```

### Coordinator field rules

One of three allowed values, enforced by `tooling/locale-completeness-check/`:

- `"@<github-handle>"` — named individual owner
- `"vendor:<name>"` — commercial service engagement
- `"community:open-recruiting"` — no named owner; open recruitment ticket. **Allowed for `bake-in` tier only; prohibited for `complete` tier.**

All Phase 1 coordinators named before Phase 1 Day 1 in `i18n/coordinators.md`.

### Status tier rules

- `complete` — 100% coverage expected; floor 95% (acknowledges translation drift risk during cascade)
- `bake-in` — 40% floor; 100% layout coverage required (validated via RTL snapshot tests for RTL locales)

Pure stubs are not acceptable. A mandate that calls global reach first-class does not ship stubs.

### Tenant and user override semantics

```jsonc
"tenantOverrideAllowed": true,      // can tenant default to this locale?
"userOverrideAllowed": true,         // can individual user choose this locale?
```

Rules:
- Tenants may default to any locale with `tenantOverrideAllowed: true` — even `bake-in` tier, with a UI warning at tenant configuration ("This locale is partial; some strings will appear in English")
- Individual users may always choose any locale, regardless of `tenantOverrideAllowed`
- An `en-US` fallback is always provided at render time for missing keys; telemetry event `i18n.missing_key` fires

Phase 1 default: both overrides set to `true` on all 12 locales.

### Calendar field scope

Display-time only. Storage is UTC (via `DateTimeOffset` at v1; `NodaTime Instant` when 3C wave lands). Input is ISO 8601 via native browser controls. Display is rendered via `IntlDateTimeFormat` (JS) and `CultureInfo` (.NET) honoring the locale's `calendar`. Users in `ar-SA` see Islamic (Umm al-Qura) dates; `fa-IR` see Persian; `ja` see Japanese imperial-era. Underlying stored value is always Gregorian UTC.

### Resolution chain

From [`internationalization.md`](../../../_shared/design/internationalization.md), confirmed:

1. `?culture=` query override (test/demo only)
2. Per-user preference from profile
3. Per-tenant default from `TenantMetadata`
4. `Accept-Language` header
5. Platform default (`en-US`)

Applied via `RequestLocalizationMiddleware` in Bridge; composition-root equivalent in Anchor.

### CI enforcement (`tooling/locale-completeness-check/`)

- Any `complete`-tier locale below its `completenessFloor` → build fails
- Any `bake-in`-tier locale below its `completenessFloor` → build fails
- Any RTL-direction locale failing layout snapshot tests → build fails, regardless of string completeness

### Locale list mutation policy

- Adding a locale = `sunfish-feature-change` ICM pipeline
- Lowering `completenessFloor` = `sunfish-feature-change` (user-visible commitment weakened)
- Raising `completenessFloor`, naming coordinator, or flipping override flag = `sunfish-docs-change`
- Removing a locale = `sunfish-api-change` (breaking; must include migration guidance)

---

## 5. SyncState Multimodal Encoding

Every sync state carries four independent signals: **Color + Shape + Text Label + ARIA**. If any one signal is unavailable (color-blindness, screen reader, high contrast, low vision), the other three still communicate state.

### The five states

| State | Light color | Dark color | Icon | Text (en-US) | ARIA |
|---|---|---|---|---|---|
| Healthy | `#27ae60` | `#2ecc71` | `check_circle` | "Synced" / "Synced with all peers" | `role="status"` |
| Stale | `#3498db` | `#5dade2` | `schedule` | "2h ago" / "Last synced 2 hours ago" | `role="status"` |
| Offline | `#7f8c8d` | `#95a5a6` | `cloud_off` | "Offline" / "Offline — saved locally" | `role="status"` |
| ConflictPending | `#e67e22` | `#f39c12` | `call_split` | "Conflict" / "Review required — two versions diverged" | `role="alert"` |
| Quarantine | `#c0392b` | `#ff6b6b` | `do_not_disturb_on` | "Held" / "Can't sync — open diagnostics" | `role="alert"` |

### CVD evidence (P0.5 closure)

ΔE2000 computed between all pairs. Current palette has all pairs ≥ ΔE2000 20 except Healthy×Stale at 20.11 and 17.30 (dark), all above the 11.0 "unmistakably different" threshold. APCA Lc for non-text UI ≥ 45 on both light and dark backgrounds. Methodology: CIEDE2000 over sRGB→Linear→XYZ→Lab (D65); APCA simplified formula.

CVD simulator: Chrome DevTools Rendering panel "Emulate vision deficiencies" at "severe." CI invocation via Chrome DevTools MCP's `emulate` command against each provider theme's kitchen-sink page; screenshots diffed against baseline.

### Icon choices (P0.1 closure — research-backed)

Each replacement icon is grounded in precedent from globally-deployed products (Google Workspace, Microsoft 365, GitHub, Lucide community standard).

- **Quarantine:** `do_not_disturb_on` replaces padlock. Padlock reads "secure/encrypted" globally; circle-with-bar reads "held/blocked." Precedent: OneDrive blocked-file badge. ISO 7001-adjacent. Symmetric — no RTL mirror.
- **Offline:** `cloud_off` replaces disconnected-Wi-Fi. Wi-Fi glyph encodes Wi-Fi-era assumption; cloud-with-slash is bearer-agnostic. Precedent: Google's own offline-design guide uses `cloud_off`. Symmetric.
- **ConflictPending:** `call_split` replaces warning triangle. Warning triangle is ISO 7010 hazard-safety symbol; over-weights affect for routine merge conflict. Diverge-arrows encode "two paths forked — pick one." Precedent: GitHub merge-conflict UI, Lucide community `git-merge-conflict`. RTL mirror: yes (directional flow).

### Text label overflow policy (P0.2 closure)

Two label tiers per state:
- **Short** — ≤ 10 grapheme clusters en-US, compact form
- **Long** — ≤ 30 grapheme clusters en-US, standard form

**Per-locale width budget multipliers** (derived from [CLDR character properties](https://cldr.unicode.org/translation/characters) and Microsoft localization width guidelines):

| Form | Budget (en-US baseline) | de ×1.4 | ar ×1.2 | hi ×1.1 |
|---|---|---|---|---|
| Compact | `max-inline-size: 10ch` | 14ch | 12ch | 11ch |
| Standard | `max-inline-size: 28ch` | 40ch | 34ch | 31ch |
| Mobile | `max-inline-size: 16ch` | 22ch | 19ch | 18ch |

**Overflow handling:**
1. Text truncates with `text-overflow: ellipsis`
2. Full text always available via `aria-label` (screen readers get unabridged)
3. Reveal-on-hover-or-focus expands label to full width in overlay (2-line max)
4. `title` attribute carries full text (mouse-hover tooltip fallback)

### Live-region coalescing (P0.3 closure)

**Document-level singleton.** Two live regions owned by app root (Light DOM, not per-component shadow DOM):

```html
<body>
  <div id="sf-syncstate-announcer-polite"    role="status" aria-atomic="true" class="sr-only"></div>
  <div id="sf-syncstate-announcer-assertive" role="alert"  aria-atomic="true" class="sr-only"></div>
  <!-- app content -->
</body>
```

**Single `role` per region, no redundant `aria-live`.** `role="status"` implies polite; `role="alert"` implies assertive. Redundant pairing has caused NVDA duplicate-announcement bugs historically.

**Coalescing rules:**
- Window: 500ms from first transition
- Individual announcement when batch size ≤ 3
- Aggregate when > 3: "3 records now stale" / "12 records need review"
- Priority split: assertive bypasses polite queue
- Rate limit: max 1 assertive per second per region; excess batched
- Burst kill-switch: > 50 transitions in 10 seconds emits one aggregate "Multiple records changed — open diagnostics"; logs `syncstate.announcer.burst_suppressed` telemetry

**Aggregate message templates** (localized via ICU MessageFormat for ar/ja/ko pluralization):

```
syncstate.announcement.individual.healthy = "Record {recordName} synced"
syncstate.announcement.individual.stale = "Record {recordName} is stale"
syncstate.announcement.aggregate.stale = "{count, plural, one {# record} other {# records}} now stale"
syncstate.announcement.aggregate.conflict = "{count, plural, one {# record} other {# records}} need review"
syncstate.announcement.aggregate.mixed = "Multiple records changed — open activity feed"
```

### CSS custom-property theme contract

```css
:root {
  --sf-syncstate-healthy-bg:    #27ae60;  --sf-syncstate-healthy-fg:    #ffffff;
  --sf-syncstate-stale-bg:      #3498db;  --sf-syncstate-stale-fg:      #ffffff;
  --sf-syncstate-offline-bg:    #7f8c8d;  --sf-syncstate-offline-fg:    #ffffff;
  --sf-syncstate-conflict-bg:   #e67e22;  --sf-syncstate-conflict-fg:   #ffffff;
  --sf-syncstate-quarantine-bg: #c0392b;  --sf-syncstate-quarantine-fg: #ffffff;
}
:root[data-theme="dark"] {
  --sf-syncstate-healthy-bg:    #2ecc71;
  --sf-syncstate-stale-bg:      #5dade2;
  --sf-syncstate-offline-bg:    #95a5a6;
  --sf-syncstate-conflict-bg:   #f39c12;
  --sf-syncstate-quarantine-bg: #ff6b6b;
  --sf-syncstate-healthy-fg:    #0d1117;
  --sf-syncstate-stale-fg:      #0d1117;
  --sf-syncstate-offline-fg:    #0d1117;
  --sf-syncstate-conflict-fg:   #0d1117;
  --sf-syncstate-quarantine-fg: #0d1117;
}
```

`tooling/theme-validator/` loads any override theme and recomputes ΔE2000 + APCA Lc. Fails CI on ΔE2000 < 11.0 or Lc < 45.0. Prevents silent a11y regressions from brand-color overrides.

### Component API

`SunfishSyncStateIndicator` in `ui-core`. Consumers cannot opt out of text label or ARIA — not exposed as optional props. No icon-only variant exists in the library.

### Tracked follow-ups (P1 from council review)

Non-blocking, tracked for v1.0-RC closeout:
- Specify click-target destination for each non-Healthy state (P1.2)
- `SyncStateTransitionEvent` telemetry shape + OpenTelemetry exporter (P1.5)
- Justification for 5-state taxonomy vs. industry-standard 3-state (P1.8)
- WCAG 2.2 AAA targets per criterion (P1.7)

---

## 6. Reduced Motion as Sunfish User Preference

Honor `@media (prefers-reduced-motion: reduce)` **and** surface as explicit per-user preference in Sunfish settings.

### User preference model

`ISunfishUserPreferences` gains `MotionPreference`:

```csharp
public enum MotionPreference
{
    System = 0,    // follow OS prefers-reduced-motion
    Reduced = 1,   // always reduced (overrides OS)
    Full = 2       // always full (explicit opt-in, overrides OS)
}
```

**Default: `System`.** Respects OS setting without Sunfish imposing a decision.

### Application mechanism

`ReducedMotionMiddleware` in Bridge and composition-root wiring in Anchor apply the attribute:

```html
<html data-motion="reduced">   <!-- user set to Reduced -->
<html data-motion="full">       <!-- user set to Full -->
<html>                          <!-- System; CSS media query drives -->
```

### CSS — both signals trigger reduction

```css
.sunfish-panel__enter {
  transition: opacity 0.2s var(--sf-easing), transform 0.2s var(--sf-easing);
}

@media (prefers-reduced-motion: reduce) {
  html:not([data-motion="full"]) .sunfish-panel__enter {
    transition: none;
    animation: none;
  }
}

html[data-motion="reduced"] .sunfish-panel__enter {
  transition: none;
  animation: none;
}
```

The `html:not([data-motion="full"])` guard lets a user explicitly opt back in when their OS setting is reduce.

### Suppressed vs preserved

| Suppressed | Preserved |
|---|---|
| Decorative fades, slides, scale transitions | State-indicating animation (spinner still spins) |
| Parallax | Essential motion (video playback on user action) |
| Auto-play video/GIF/animated backgrounds | Focus transitions (become instant, not removed) |
| Non-essential rotation | Information-conveying animation (chart renders, progress bars) |
| Marquees, auto-scrolling banners | — |

### Settings UI

`SunfishUserPreferencesPanel` gains "Motion" section:
- Follow system setting (default)
- Reduced motion
- Full motion (not recommended if you experience motion sensitivity)

Labels localized. The "not recommended" caveat on Full is deliberate — defaults and language nudge toward accessibility.

### Testing

- Every component declaring animation has a test setting `data-motion="reduced"` on host; asserts no `transition`/`animation` resolves
- `prefers-reduced-motion: reduce` emulation via Chrome DevTools MCP covers OS-level path
- Kitchen-sink demo ships a settings switcher for reviewer verification

---

## 7. Accessibility Contracts via Storybook

Adopt [Storybook](https://storybook.js.org/) with `@storybook/addon-a11y` as the per-component accessibility contract harness. Two artifacts per component; no bespoke tooling invented.

### Two artifacts per component

```
packages/ui-core/src/components/dialog/
├── sunfish-dialog.ts              ← Web Component source
└── sunfish-dialog.stories.ts       ← stories + a11y contract (single source of truth)
```

Stories encode the contract executably. Docs and machine-readable contract data are derived from stories at build time, not authored.

### Contract shape in stories

```typescript
export default {
  title: 'Dialog',
  component: 'sunfish-dialog',
  parameters: {
    a11y: {
      config: { rules: [{ id: 'color-contrast', enabled: true }] },
      sunfish: {
        wcag22Conformant: ['1.3.1', '1.4.3', '2.1.1', '2.4.3', '2.4.11', '2.5.8', '4.1.2'],
        ariaPattern: 'https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/',
        keyboardMap: [
          { keys: ['Escape'], action: 'close' },
          { keys: ['Tab'], action: 'cycle-forward-in-trap' }
        ],
        focus: { initial: 'first-focusable-child', trap: true, restore: 'trigger' },
        screenReaderAudit: {
          'nvda-2026.1/firefox-126':        { verified: '2026-04-20', auditor: '@a11y-lead', pass: true },
          'jaws-2024/chrome-125':           { verified: '2026-04-20', auditor: '@a11y-lead', pass: true },
          'voiceover-macos15/safari-17':    { verified: '2026-04-20', auditor: '@a11y-lead', pass: true }
        },
        contrast: { bodyTextMinRatio: 4.5, borderMinRatio: 3.0, apcaLcNonTextMin: 45 },
        targetSize: { desktop: 24, tablet: 32, mobile: 44 },
        shadowDom: { mode: 'open', crossRootAriaStrategy: 'reflective-aria' },
        composedOf: ['sunfish-button', 'sunfish-icon'],
        directionalIcons: []
      }
    }
  }
};

export const Default = {
  args: { heading: 'Confirm', open: true },
  play: async ({ canvasElement }) => {
    const dialog = canvasElement.querySelector('sunfish-dialog');
    await expect(dialog).toHaveAttribute('role', 'dialog');
    await expect(dialog).toHaveAttribute('aria-modal', 'true');
    await expectFocusTrapped(dialog);
    await expectKeyboardMap(dialog, { Escape: 'close' });
  }
};
```

### Per-adapter harness decisions

**ADR 0034 — A11y Harness per Adapter** (authored Phase 1 Week 1; accepted before contract work begins):

| Layer | Harness | Runner | axe integration |
|---|---|---|---|
| `ui-core` (Web Components per ADR 0017) | Storybook + `@storybook/addon-a11y` | Web Test Runner + Playwright | `@axe-core/playwright` |
| `ui-adapters-react` | Same stories via `@storybook/react` | Vitest + Playwright | `@axe-core/playwright` |
| `ui-adapters-blazor` | bUnit + new bUnit-to-axe bridge (to be built; serializes bUnit-rendered markup into a Playwright harness for real-browser axe pass) | xUnit (.NET) | Bridge → Playwright for real-browser axe pass |

### Binary runtime checks

- `story.run()` completes without thrown assertion
- `axe.run(canvasElement)` returns zero violations at impact ≥ moderate
- `canvasElement.getBoundingClientRect()` width and height ≥ declared `targetSize[surface]`
- Focus trap: Tab from last focusable returns to first within N frames
- `composedOf` components each have their own story; Storybook composition-viewer traverses

### Screen-reader audit provenance

Replaced aspirational `"tested-passing"` with audit-logged entries. CI check: any entry older than 12 months fails the build until re-verified. Forces refresh on real hardware/version pairs.

### Composition handling

`composedOf: ['sunfish-button', 'sunfish-icon']` declared in composite story. Runtime axe-core traverses composed tree. Composite story doesn't duplicate Button's contract — it references. Button's contract change flows through; composite's axe-run catches any new violations.

### Target size tiered

```typescript
targetSize: { desktop: 24, tablet: 32, mobile: 44 }
```

Per-surface rendering consulted via component `surface` prop at mount.

### Phase 1 Week 1 tooling sub-wave

- **Day 1–2:** Survey Storybook a11y addon; confirm `@axe-core/playwright`, shadow-DOM traversal, RTL emulation, CVD emulation coverage; identify gaps
- **Day 3:** ADR 0034 drafted, reviewed, accepted
- **Day 4–5:** Pilot implementation on 3 components (`sunfish-button`, `sunfish-dialog`, `sunfish-syncstate-indicator`). Measure CI runtime cost per mount.
- **End of Week 1:** Go/no-go decision on cascading to rest of `ui-core`

Named owner: a11y program lead. This is the Cold Start Test pass — after Week 1, any contributor adds a new component by copying the three pilot examples.

### CI runtime budget guard

Week 1 pilot measures axe-core runtime per story across 3 provider themes × light/dark × LTR/RTL × 3 CVD simulations = 36 scenarios per component. Target: < 15 seconds per component total. If projected total for `ui-core` exceeds the 10-minute p50 CI budget from `ci-quality-gates.md`, cascade adds matrix-parallelization before proceeding.

### Forward Gate checklist update

Added to `pull_request_template.md`:

```md
- [ ] Storybook story updated; `parameters.a11y.sunfish` contract section current
- [ ] `story.run()` passes in Storybook test-runner
- [ ] `axe-core` zero violations at impact ≥ moderate
- [ ] Screen-reader audit entry within 12 months for at least two SR/browser pairs
- [ ] `composedOf` links valid; composite axe-run passes
```

---

## 8. CI Gates + Analyzer Package

Section 8 consolidates enforcement into named CI jobs and Roslyn analyzers. This is what makes the Forward Gate real.

### The Five Roslyn Analyzers (`Sunfish.Analyzers.*` packages)

Two new sub-packages under the existing `packages/analyzers/` family (siblings to `Sunfish.Analyzers.CompatVendorUsings`):

- `packages/analyzers/i18n/Sunfish.Analyzers.I18n.csproj` — hosts the four I18N analyzers
- `packages/analyzers/accessibility/Sunfish.Analyzers.Accessibility.csproj` — hosts the A11Y analyzer

| ID | Name | Package | Severity | Purpose |
|---|---|---|---|---|
| `SUNFISH_I18N_001` | `ResourceRequiresComment` | `Sunfish.Analyzers.I18n` | Error | `.resx` entries missing `<comment>` — mandatory translator context |
| `SUNFISH_I18N_002` | `NoHardcodedStrings` | `Sunfish.Analyzers.I18n` | Error | Razor component contains user-facing string literal not externalized. `[UnlocalizedOk]` allowlist with justification |
| `SUNFISH_I18N_003` | `NoDraftInCompleteLocale` | `Sunfish.Analyzers.I18n` | Error | Any `.resx` in `complete`-tier locale contains `state="needs-review"` or `state="new"` |
| `SUNFISH_I18N_004` | `NoResourceManagerDirectUsage` | `Sunfish.Analyzers.I18n` | Error | `System.Resources.ResourceManager` direct access — forces all paths through `IStringLocalizer<T>` |
| `SUNFISH_A11Y_001` | `ComponentMissingStory` | `Sunfish.Analyzers.Accessibility` | Warning → Error (Phase 2 exit) | Component file without sibling `*.stories.ts` |

**Package plumbing:** both sub-packages auto-included via `Directory.Packages.props` central package management; rules published to `.editorconfig` at repo root. Naming follows existing `Sunfish.Analyzers.<Sub>` convention (cf. `Sunfish.Analyzers.CompatVendorUsings`).

### Node.js Tooling

| Tool | Purpose | Runtime budget |
|---|---|---|
| `tooling/css-logical-props-lint/` | Physical-direction CSS ban per Section 2 | < 30 seconds full repo |
| `tooling/theme-validator/` | ΔE2000 + APCA matrix check on theme overrides | < 30 seconds per theme |
| `tooling/a11y-audit-runner/` | Storybook test-runner + `@axe-core/playwright` orchestration | < 15 minutes Phase 1; < 20 minutes Phase 2 (with matrix shard expansion) |
| `tooling/locale-completeness-check/` | Enforces `completenessFloor` per `locales.json` | < 2 minutes |

### GitHub Actions workflow

`.github/workflows/global-ux-gate.yml` wires all jobs as required status checks:

```yaml
name: Global-First UX Gate
on: [pull_request, push]

jobs:
  analyzers:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet build --configuration Release -warnaserror

  css-logical-props:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
      - run: node tooling/css-logical-props-lint/index.js

  theme-validator:
    runs-on: ubuntu-latest
    if: contains(github.event.pull_request.changed_files, '_shared/design/tokens/')
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
      - run: node tooling/theme-validator/index.js

  a11y-audit:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        shard: [1, 2, 3, 4]
    timeout-minutes: 20
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
      - run: pnpm install
      - run: pnpm build-storybook
      - run: node tooling/a11y-audit-runner/index.js --shard ${{ matrix.shard }} --total-shards 4

  locale-completeness:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
      - run: node tooling/locale-completeness-check/index.js
```

All five jobs are **required status checks** on the protected `main` branch.

### Baseline-drift policy

Three baseline files track documented exceptions with target dates:

| File | Tracks |
|---|---|
| `_shared/engineering/a11y-baseline.md` | axe-core violations (existing per `accessibility.md`) |
| `_shared/engineering/i18n-baseline.md` | Roslyn analyzer warnings deferred |
| `_shared/engineering/css-logical-allowlist.md` | Third-party CSS exceptions |

Entry format:
```
- **Violation ID / Rule:** SUNFISH_I18N_002 (or axe rule id)
- **Component / Surface:** packages/ui-adapters-blazor/Components/...
- **Reason:** Third-party vendor markup imported verbatim
- **Owner:** @chriswood
- **Target date:** 2026-07-01
- **Logged date:** 2026-05-01
```

CI reads the baseline; allowlisted entries pass. Entry past target date surfaces in monthly roadmap review. Unknown violation fails the build.

### Runtime budget

Targets from `ci-quality-gates.md`:
- p50 < 10 minutes
- p95 < 15 minutes
- Hard timeout 20 minutes per matrix shard

Remedy on breach: matrix shard expansion (4 → 8 → 16), never threshold relaxation.

### Failure modes and fallbacks

| Failure mode | Detection | Fallback |
|---|---|---|
| axe-core breaks on major version update | PR CI fails unexpectedly across all components | Pin version; upgrade on dedicated branch with baseline-drift entries |
| Storybook test-runner flakes | Intermittent failures on unchanged code | Playwright retry with exponential backoff; 3 retries then hard fail + auto-issue |
| Chrome DevTools CVD emulation drift | Screenshots diff without palette change | Pin Chromium version in Playwright; upgrade with visual regression review |
| Weblate sync disrupts `locales.json` | Unintended completeness changes | `locales.json` authoritative; sync writes to `.resx` only |
| Roslyn analyzer false positive | Legitimate code flagged | `[SuppressMessage]` with justification; tracked in `i18n-baseline.md` |

### Binary gates for Section 8 completion

- ☐ All 5 Roslyn analyzers published in `Sunfish.Analyzers` NuGet; rules in `.editorconfig`
- ☐ All 5 tooling directories exist with executable entrypoints
- ☐ `.github/workflows/global-ux-gate.yml` live on `main`; required status checks configured
- ☐ All 3 baseline files exist with headers (no entries required)
- ☐ `pull_request_template.md` lists the Section 7 Forward Gate checklist
- ☐ Runtime p50 measured at Phase 1 Week 2 pilot; within budget or parallelization plan in place

### Phase 1 exit gate

Phase 2 cascade cannot begin until Section 8 CI workflow is live on `main` with all gates passing on the Phase 1 surface. Rationale: without the gate, Phase 2 becomes an unenforceable convention. The gate is the mechanism that keeps cascaded components compliant.

### Ordering summary

1. **Phase 1 Week 1:** Storybook harness pilot (Section 7) + SmartFormat.NET wrapper pilot (Section 3A, pivoted from ICU4N) in parallel. End-of-week go/no-go gate per Section 1. **DONE 2026-04-24 — GO verdict.**
2. **Phase 1 Weeks 2–4:** Section 3A Loc-Infra cascade to `foundation-*` + `ui-core`; XLIFF pipeline; error localization; Section 3B Translator-Assist Phase 1 core (MADLAD CLI, Weblate stand-up, `NoDraftInCompleteLocale` analyzer)
3. **Phase 1 Weeks 3–6** (parallel with steps 2–4): Sections 2 (CSS logical properties sweep), 5 (SyncState multimodal), 6 (reduced motion) across `ui-core`
4. **Phase 1 Week 6 (exit gate):** Section 8 CI workflow live on `main` with all gates passing on Phase 1 surface
5. **Phase 2 begins only after Phase 1 exit gate clears**

---

## Success Criteria (overall)

Phase 1 exits when ALL of:

- ☐ Sections 2, 3A, 3B (Phase 1 core), 5, 6, 7, 8 binary gates each passing
- ☐ `locales.json` deployed with 12 locales; all 9 `complete`-tier coordinators named in `i18n/coordinators.md`
- ☐ `ui-core` + `foundation*` packages pass all 5 Phase 1 CI gates
- ☐ Kitchen-sink demo renders at `ar-SA` (RTL) and `ja` without layout regression
- ☐ `waves/global-ux/status.md` maintained through entire Phase 1

Phase 2 exits when:

- ☐ All 53 remaining packages pass Forward Gate
- ☐ `ui-adapters-blazor` and every `blocks-*` have Storybook stories with complete `parameters.a11y.sunfish` blocks
- ☐ `SUNFISH_A11Y_001` severity upgraded to Error without triggering cascade-blocking noise
- ☐ CVD emulation regression green across all 3 provider themes

### FAILED conditions (rollback triggers)

Project rolls back and pauses for redesign if any of:
- Phase 1 Week 2 cascade: SmartFormat.NET wrapper or Storybook harness proves unworkable at scale (Week 1 pilots passed; Week 2+ cascade risk remains)
- Phase 1 Week 4: Weblate legal review concludes commercialization incompatibility
- Phase 2: projected total Phase 1+2 timeline exceeds 20 weeks
- At any time: CI runtime p95 exceeds 20 minutes and parallelization cannot close the gap

Rollback path documented in `waves/global-ux/decisions.md`.

---

## Verification

### Automated

- 5 Roslyn analyzers (`Sunfish.Analyzers`)
- 5 Node.js tooling scripts
- Storybook test-runner with `@axe-core/playwright`
- Chrome DevTools MCP CVD emulation
- RTL snapshot tests via Storybook global direction parameter
- `locale-completeness-check` per PR

### Manual

- Per-component accessibility audit per `accessibility.md` manual workflow (keyboard-only, SR, contrast, focus-trap, reduced-motion, zoom/reflow)
- Native-speaker review per language coordinator at v1.x
- Quarterly SR matrix re-audit

### Ongoing observability

- `i18n.missing_key` telemetry event on render-time fallback
- `syncstate.announcer.burst_suppressed` telemetry on kill-switch activation
- Per-release CVD regression screenshot diff

---

## Appendices

### Appendix A — Council review and P0 closure

Full council review (15 personas: 5 default council + 6 universal-planning Stage 1.5 + 4 domain specialists) at [`icm/07_review/output/global-ux-design/syncstate-encoding-council-review-2026-04-24.md`](../../../icm/07_review/output/global-ux-design/syncstate-encoding-council-review-2026-04-24.md). Verdict: BLOCK (5.5/10) until P0 closure.

P0 closure evidence at [`icm/07_review/output/global-ux-design/p0-closure-2026-04-24.md`](../../../icm/07_review/output/global-ux-design/p0-closure-2026-04-24.md). All 5 P0 items resolved:
- P0.1: Icon replacements research-backed (OneDrive, Google Design, GitHub/Lucide precedent)
- P0.2: Text-overflow policy with per-locale width multipliers
- P0.3: Document-level live-region singleton with 500ms coalescing
- P0.4: Hindi promoted from `bake-in` to `complete`
- P0.5: ΔE2000 computed, palette revised (Stale amber → blue), dark mode published

### Appendix B — Universal-Planning anti-pattern status

Each design section passed the universal-planning anti-pattern scan (21 patterns). Final status:

| Section | Anti-patterns firing | Status |
|---|---|---|
| 1 — Overall Structure | 0 of 21 | GREEN |
| 2 — CSS Logical Properties | 0 of 21 | GREEN |
| 3 — Localization Infrastructure | 1 of 21 (premature precision, mitigated by Week 0 research triage) | GREEN |
| 4 — `locales.json` | 0 of 21 | GREEN |
| 5 — SyncState Multimodal Encoding | 0 of 21 (P0s closed; P1s tracked) | GREEN |
| 6 — Reduced Motion | 0 of 21 | GREEN |
| 7 — Accessibility Contracts | 0 of 21 (Storybook-based revision) | GREEN |
| 8 — CI Gates + Analyzer Package | 0 of 21 | GREEN |

### Appendix C — Glossary

- **APCA Lc** — Accessible Perceptual Contrast Algorithm; successor to WCAG 2.x contrast ratio. Measures perceived contrast for non-text UI.
- **BCP-47** — IETF standard for language tags (`en-US`, `ar-SA`, `zh-Hans`).
- **Bake-in tier** — A locale with 40%+ string coverage and 100% layout coverage; ships before full translation to validate infrastructure.
- **Complete tier** — A locale with 95%+ string coverage; production-ready.
- **ΔE2000** — CIEDE2000 color difference formula; perceptual distance between two colors in CIE Lab color space.
- **Forward Gate** — The CI-enforced set of requirements every new component must pass before merging to `main`.
- **ICU MessageFormat** — CLDR-standard pattern syntax for pluralization, gender, and number formatting across locales.
- **MADLAD-400** — Google/AllenAI Apache 2.0 translation model covering 419 languages; used for pre-publish translation drafts.
- **Storybook** — Component development environment with the `@storybook/addon-a11y` integration used as the accessibility contract harness.
- **Weblate** — AGPL self-hostable translation platform used for human translator workflow.
- **XLIFF 2.0** — XML Localization Interchange File Format; translator-native interchange format.

---

## Handoff

This spec is ready for user review. After approval, implementation planning proceeds via the `writing-plans` skill per the brainstorming flow.

**Next artifacts expected:**
- ADR 0034 — A11y Harness per Adapter
- ADR 0035 — Global-Domain-Types as separate wave
- `waves/global-ux/status.md` — resume protocol file
- `waves/global-ux/decisions.md` — rollback log (created on first pivot)
- Implementation plan at `docs/superpowers/plans/2026-04-NN-global-first-ux-plan.md`

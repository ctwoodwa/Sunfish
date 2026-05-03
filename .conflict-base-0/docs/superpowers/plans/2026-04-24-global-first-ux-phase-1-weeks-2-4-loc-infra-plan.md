# Global-First UX — Phase 1 Weeks 2-4 Loc-Infra Cascade

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cascade the Week-1 SmartFormat wrapper pilot across all user-facing packages, build the XLIFF 2.0 MSBuild round-trip task, stand up a self-hosted Weblate instance wired to the repo, and land the 12-locale `.resx` skeletons so translators can start work at the Week-4 gate.

**Architecture:** Three parallel workstreams in Week 2 converge at Week 3 entry. Week 3 runs the cascade across ~20 user-facing packages. Week 4 adds integration polish (hot-reload, ProblemDetailsFactory localization, translator-comments analyzer) and validates end-to-end with Arabic RTL flowing through build → Weblate → translator → publish.

**Tech stack:** .NET 11 preview, SmartFormat.NET 3.6.1, `Microsoft.Extensions.Localization`, custom MSBuild task on `Microsoft.Build.Framework` + `Microsoft.Build.Utilities.Core`, Weblate 5.17.1 self-hosted via Docker Compose, `System.Xml.Linq` for RESX/XLIFF manipulation, Roslyn analyzer SDK for the translator-comments analyzer, llama.cpp server mode with MADLAD-400-3B-MT GGUF for MT suggestions (wired via OpenAI-compat backend, not shipped in binaries).

**Scope boundary:** This plan covers Phase 1 Weeks 2-4 ONLY (15 business days). It does NOT cover:
- Phase 2 cascade into `apps/bridge/`, `apps/anchor/`, `apps/kitchen-sink/` end-user flows — that is Plan 6.
- Actual translation work (paid translators, volunteer recruitment, glossary seeding). Weblate stack is ready; translator onboarding is a separate owner.
- The `ui-adapters-blazor.A11y` bridge project — that is Plan 4 (A11y Foundation cascade), running in parallel but separately-owned.

**Parent spec:** [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../specs/2026-04-24-global-first-ux-design.md) §3A, §3B
**Predecessor plan:** [`2026-04-24-global-first-ux-phase-1-week-1-plan.md`](./2026-04-24-global-first-ux-phase-1-week-1-plan.md) (complete — GO verdict 2026-04-24)

---

## Context & Why

Week 1 validated the SmartFormat wrapper on one foundation package with three smoke tests. The Week-1 go/no-go gate passed, unlocking cascade. Weeks 2-4 turn the pilot into production infrastructure: every user-facing string flows through `ISunfishLocalizer<T>`; every resource round-trips through XLIFF 2.0; translators have a running Weblate instance pointing at the repo.

Three parallel Week 2 workstreams (XLIFF task build, Weblate stack, resx scaffolder) converge at Week 3 start. Cascade is the expensive Week 3 work. Week 4 adds the polish that makes the infra developer-friendly (hot-reload, localized server errors, analyzer warnings for missing `<comment>` translator notes).

---

## Success Criteria

### PASSED — proceed to Plans 4-5 finalization

- All ~20 user-facing packages have `Resources/SharedResource.resx` with at least one localized entry.
- `ISunfishLocalizer<T>` is DI-registered in the three app composition roots (Bridge, Anchor, local-node-host).
- `dotnet build -t:SunfishExportXliff` produces one `.xlf` file per (resx × locale), and `SunfishImportXliff` re-imports byte-identical output for untouched inputs.
- Weblate self-hosted instance at a named internal URL is up; the repo `localization/xliff/` directory is a Weblate remote; Arabic `ar-SA` locale is populated with ≥1 translator-authored entry round-tripping into a `.resx` satellite assembly.
- `ProblemDetailsFactory.CreateProblemDetails` returns localized `title` and `detail` in en-US, ar-SA, ja for at least one smoke path (tested in Bridge integration tests).
- Hot-reload: `.resx` edit in `Debug` build of kitchen-sink reflects in running Storybook within 3 seconds.
- Translator-comments analyzer (`SunfishLocAnalyzer`) emits a warning when `.resx` `<data>` is missing `<comment>`.

### FAILED — triggers a Week-4 re-plan (not Phase 1 abort)

- XLIFF MSBuild task cannot round-trip one of the 12 locales byte-identically (likely an encoding or ordering bug; diagnose root cause, do not ship the task).
- Weblate Docker stack cannot run on the target VM sizing (4 GB RAM; 2 vCPU) — escalate to managed-Postgres tier or fallback to Crowdin Business per Task 3 memo.
- Cascade cannot complete within Week 3 because a package family (e.g., `federation-*`) has no clean string-formatting seam — document as deferred-cascade, scoped to Plan 6.
- Translator-comments analyzer false-positive rate >5% on existing internal-only `.resx` — tune or disable until tuning lands.

### Kill trigger (30-day timeout)

If Plan 2 has not landed all success criteria by **2026-05-24** (30 days from Week-1 GO), escalate to BDFL for scope cut: named options are (a) reduce cascade to blocks-* only, defer foundation packages; (b) use Crowdin Business tier in place of self-hosted Weblate; (c) defer XLIFF task to Plan 6 and use PO-file fallback in Weeks 2-4.

---

## Assumptions & Validation

| Assumption | VALIDATE BY | IMPACT IF WRONG |
|---|---|---|
| SmartFormat.NET's `{count:plural:...}` syntax covers all 12 target-locale CLDR plural rules | Task 3.1 — CLDR round-trip test for each locale before cascade begins | ~2 day slip for custom plural-rule patches; fallback is explicit `=0`/`=1` selectors |
| Weblate 5.17.1 self-hosted runs on 4 GB RAM / 2 vCPU target VM | Task 2.2 — load test with 20 concurrent translator sessions against synthetic glossary | Upgrade to 8 GB / 4 vCPU; adds ~$20/month operational cost |
| XLIFF 2.0 → `.resx` round-trip is deterministic (byte-identical for unchanged inputs) | Task 1.4 — round-trip property test over random `.resx` fixtures in all 12 target locales | If non-deterministic, introduces churn in git diffs; need to normalize XML encoding before comparison |
| MADLAD-400-3B-MT GGUF runs on developer hardware (inference, not training) | Task 2.5 — measure inference time on a 4-core Apple M2 / equivalent | If >10s per short string, unusable for on-demand translator suggestions; fallback is Weblate's DeepL backend |
| Translator-comments analyzer doesn't false-positive on internal-only `.resx` files | Task 4.3 — run analyzer across existing `packages/*/Resources/` after Week-3 cascade | If >5% false-positive, mute or narrow rule until tuning lands in Plan 4 |
| Hot-reload via `FileSystemWatcher` works under Blazor Server's SignalR circuit in dev | Task 4.5 — manual test in kitchen-sink dev mode | If stale state leaks across circuits, scope hot-reload to full-page reloads only (disable circuit-live refresh) |

---

## File Structure (Weeks 2-4 deliverables)

```
tooling/
  Sunfish.Tooling.LocalizationXliff/
    Sunfish.Tooling.LocalizationXliff.csproj  ← MSBuild task assembly
    SunfishResxToXliffTask.cs                  ← Export direction
    SunfishXliffToResxTask.cs                  ← Import direction
    ResxFile.cs                                ← RESX reader/writer
    Xliff20File.cs                             ← XLIFF 2.0 reader/writer
    build/Sunfish.Tooling.LocalizationXliff.targets
    tests/
      RoundTripTests.cs                        ← Property + fixture tests
      TwelveLocaleTests.cs                     ← All 12 locales exercised

  Sunfish.Tooling.LocAnalyzer/
    Sunfish.Tooling.LocAnalyzer.csproj
    ResourceRequiresCommentAnalyzer.cs         ← SUNFISH_I18N_001 diagnostic (per spec §8)
    NoHardcodedStringsAnalyzer.cs              ← SUNFISH_I18N_002 diagnostic

infra/
  weblate/
    docker-compose.yml
    .env.example
    README.md                                  ← Ops runbook

localization/
  glossary/
    sunfish-glossary.tbx                       ← TermBase eXchange (translator-facing)
  source/
    (per-package Resources/ folders linked)
  xliff/
    (populated by MSBuild export)

packages/foundation/Localization/
  SunfishLocalizerFactory.cs                   ← Hot-reload aware (Debug only)
  SunfishProblemDetailsFactory.cs              ← Localized Problem Details

packages/{blocks-*, ui-core, ui-adapters-react, ui-adapters-blazor}/
  Resources/
    SharedResource.resx                        ← At minimum one string per package

accelerators/{anchor,bridge}/
  Program.cs                                   ← AddSunfishLocalization() wired

waves/global-ux/
  week-2-xliff-roundtrip-report.md             ← Task 1.5 output
  week-2-weblate-ops-runbook.md                ← Task 2.3 output
  week-3-cascade-coverage-report.md            ← Task 3.4 output
  week-4-integration-report.md                 ← Task 5.1 output
```

---

## Week 2 — Infrastructure (three parallel workstreams)

### Workstream A: XLIFF 2.0 MSBuild task

#### Task 1.1: Scaffold the tooling project

**Files:**
- Create: `tooling/Sunfish.Tooling.LocalizationXliff/Sunfish.Tooling.LocalizationXliff.csproj`
- Create: `tooling/Sunfish.Tooling.LocalizationXliff/build/Sunfish.Tooling.LocalizationXliff.targets`

**Why:** MSBuild custom-task assembly needs distinct .csproj from consumers. The `build/` subfolder + `.targets` is auto-imported by NuGet pack convention.

- [ ] **Step 1:** csproj targets `netstandard2.0` (MSBuild 17 + Visual Studio host compat). PackageReferences: `Microsoft.Build.Framework`, `Microsoft.Build.Utilities.Core`.
- [ ] **Step 2:** `.targets` file declares `<UsingTask AssemblyFile="$(MSBuildThisFileDirectory)..\lib\netstandard2.0\Sunfish.Tooling.LocalizationXliff.dll" TaskName="SunfishResxToXliffTask" />` etc.
- [ ] **Step 3:** Commit. Path-scoped `git add tooling/Sunfish.Tooling.LocalizationXliff/` only.

#### Task 1.2: RESX reader/writer

**Files:**
- Create: `tooling/Sunfish.Tooling.LocalizationXliff/ResxFile.cs`

- [ ] **Step 1:** Parse `.resx` as `XDocument`. Extract `<data name="...">` elements with `<value>` and optional `<comment>`.
- [ ] **Step 2:** Serialize back; preserve unknown elements (metadata headers) verbatim.
- [ ] **Step 3:** Unit tests: round-trip fixture `.resx` files from `packages/foundation/tests/Resources/fixtures/`.

#### Task 1.3: XLIFF 2.0 reader/writer

**Files:**
- Create: `tooling/Sunfish.Tooling.LocalizationXliff/Xliff20File.cs`

- [ ] **Step 1:** Serialize XLIFF 2.0 with OASIS namespace `urn:oasis:names:tc:xliff:document:2.0`. Units keyed by RESX `name` (preserves round-trip without heuristic matching).
- [ ] **Step 2:** Preserve `<target state="translated">` / `state="final"` on export — never overwrite translator-approved text.
- [ ] **Step 3:** Obsolete-unit retention: units present in XLIFF but missing from source RESX get `state="obsolete"`, retained 90 days (configurable).

#### Task 1.4: Round-trip property tests

**Files:**
- Create: `tooling/Sunfish.Tooling.LocalizationXliff/tests/RoundTripTests.cs`
- Create: `tooling/Sunfish.Tooling.LocalizationXliff/tests/TwelveLocaleTests.cs`

**Why:** The export→import round-trip must be byte-identical for unchanged inputs, or git diffs churn on every build.

- [ ] **Step 1:** FsCheck property: for any valid RESX, `Import(Export(resx)) == resx` after XML normalization.
- [ ] **Step 2:** Twelve-locale fixture test — hand-authored `.resx` in en, ar-SA, hi, zh-Hans, zh-Hant, ja, ko, ru, pt-BR, es-LA, fr, de; assert round-trip preserves diacritics, RTL marks, zero-width joiners, emoji.
- [ ] **Step 3:** Run: `dotnet test tooling/Sunfish.Tooling.LocalizationXliff/tests/ -v normal`. All tests green before committing.

#### Task 1.5: Round-trip report

**Files:**
- Create: `waves/global-ux/week-2-xliff-roundtrip-report.md`

- [ ] Document: byte-identical round-trip confirmed for all 12 locales. Any exceptions documented with root cause + fix.

---

### Workstream B: Weblate Docker stack

#### Task 2.1: Docker Compose stack

**Files:**
- Create: `infra/weblate/docker-compose.yml`
- Create: `infra/weblate/.env.example`

- [ ] **Step 1:** Compose services: `weblate`, `postgres`, `valkey` (Redis-compatible), `cache` (optional). Images pinned to exact `weblate/weblate:5.17.1-*` tags.
- [ ] **Step 2:** `.env.example` lists all required vars: `WEBLATE_SITE_DOMAIN`, `WEBLATE_ADMIN_*`, `POSTGRES_PASSWORD`, `REDIS_PASSWORD`, `WEBLATE_SECRET_KEY`. Real `.env` gitignored.
- [ ] **Step 3:** Volume mounts: `data/weblate` and `data/postgres` on the host filesystem; backup-friendly.

#### Task 2.2: Weblate load test

**Files:**
- Create: `waves/global-ux/week-2-weblate-load-test-log.md`

**Why:** Validate the 4 GB RAM / 2 vCPU VM sizing assumption before committing ops budget.

- [ ] **Step 1:** Spin up stack on a 4 GB / 2 vCPU VM (Hetzner CX22 or DigitalOcean Basic).
- [ ] **Step 2:** Use `weblate-bench` or hand-rolled curl harness to simulate 20 concurrent translator sessions hitting `/api/translations/`. Measure p50, p95, p99 latency; memory use; Postgres connection count.
- [ ] **Step 3:** Record go/no-go on the sizing assumption. If memory > 3.5 GB under load, upgrade to 8 GB and document added cost.

#### Task 2.3: Operations runbook

**Files:**
- Create: `infra/weblate/README.md`
- Create: `waves/global-ux/week-2-weblate-ops-runbook.md`

- [ ] Cover: initial bring-up, upgrade procedure, daily backup (BorgBackup to S3-compat), disaster-recovery rehearsal cadence (monthly restore-from-backup test), CVE patching cadence, log-forwarding to the team's observability stack.

#### Task 2.4: Repo-as-Weblate-remote integration

**Files:**
- Modify: `infra/weblate/README.md` (§Repo-integration section)

- [ ] **Step 1:** Weblate's `Component` configured with git remote `https://github.com/<sunfish-repo>` and resource file path `localization/xliff/*.xlf`.
- [ ] **Step 2:** Git-hook callback pointed at Weblate's webhook URL so repo pushes trigger Weblate re-import.
- [ ] **Step 3:** Test: push a new `.xlf` file to a test branch; verify it appears in Weblate within 60 s.

#### Task 2.5: MADLAD-400 MT backend wiring

**Files:**
- Create: `infra/weblate/mt-backends.md`

- [ ] **Step 1:** Stand up `llama.cpp server --model madlad400-3b-mt.Q4_K_M.gguf --port 8080 --host 127.0.0.1` on the Weblate host.
- [ ] **Step 2:** Weblate `MT_SERVICES = ["weblate.machinery.openai.OpenAITranslation"]`; `MT_OPENAI_BASE_URL = "http://127.0.0.1:8080/v1"`; `MT_OPENAI_MODEL = "madlad400-3b-mt"`.
- [ ] **Step 3:** Smoke test: request MT suggestion for `"Save"` en→ar, en→ja. Record latency and quality (qualitative); compare against Weblate's free DeepL backend as sanity check.

---

### Workstream C: RESX scaffolder

#### Task 3.1: RESX scaffolding script

**Files:**
- Create: `tooling/scaffolding-cli/src/commands/LocalizationScaffolder.cs`

- [ ] **Step 1:** Command `sunfish-cli loc scaffold --package <name>` creates `packages/<name>/Resources/SharedResource.resx` with an empty root plus one sentinel entry (`<data name="_package"><value>&lt;package-name&gt;</value></data>`) so the file is non-empty for IStringLocalizer discovery.
- [ ] **Step 2:** Also emits the 11 empty locale satellites (`SharedResource.ar-SA.resx`, etc.) so Weblate sees all 12 target locales from day 1.
- [ ] **Step 3:** Idempotent — re-running overwrites nothing; only fills gaps.

#### Task 3.2: 12-locale CLDR plural-rule verification

**Files:**
- Create: `tooling/Sunfish.Tooling.LocalizationXliff/tests/CldrPluralTests.cs`

**Why:** Before cascade starts, prove SmartFormat handles all 12 CLDR plural-rule families.

- [ ] **Step 1:** For each locale: construct a pattern `{count:plural:<cldr-forms-joined-by-|>}`; assert the correct form is selected for 3 representative counts.
- [ ] **Step 2:** Twelve-locale matrix: en, ar-SA, hi, zh-Hans, zh-Hant, ja, ko, ru, pt-BR, es-LA, fr, de. CLDR families covered: English (2-form), Arabic (6-form), Russian (3-form), Japanese+Chinese+Korean (1-form), Hindi+Portuguese+Spanish (2-form), French (2-form-with-0-as-1), German (2-form).
- [ ] **Step 3:** Any failure is a Week-2 rollback trigger — cascade blocked until resolved.

#### Task 3.3: Glossary seed

**Files:**
- Create: `localization/glossary/sunfish-glossary.tbx`

- [ ] **Step 1:** Seed TermBase eXchange file with core terms: `Sunfish` (do-not-translate), `Anchor`, `Bridge`, `CRDT`, `Block` (domain sense, capitalize), `Sync`, `Offline`, `Conflict`, `Quarantine` (sync sense, not medical).
- [ ] **Step 2:** Import into Weblate via admin UI. Verify terms surface in translator editor glossary pane.

---

## Week 3 — Cascade

### Task 3.4: Inventory user-facing packages

**Files:**
- Create: `waves/global-ux/week-3-cascade-inventory.md`

- [ ] **Step 1:** Enumerate packages that produce user-facing copy. Initial set: `ui-core`, `ui-adapters-react`, `ui-adapters-blazor`, all `blocks-*` (14 confirmed + 1 in flight per Plan 6 inventory), `accelerators/anchor`, `accelerators/bridge`, `apps/kitchen-sink`. Total estimate: ~20 packages.
- [ ] **Step 2:** For each, identify the string-formatting seam: `@inject IStringLocalizer<T>` in Razor / JSX, or `SunfishLoc.Get(...)` call sites in code-behind / services.
- [ ] **Step 3:** Table rows with: package, locale seam kind, estimated string count, owner agent (for subagent-driven cascade).

### Task 3.5: Cascade wrapper injection

**Files:**
- Modify: each of ~20 packages — add `Resources/SharedResource.resx`, wire `ISunfishLocalizer<T>` registration in each package's DI or composition file.

**Why:** This is the expensive Week 3 work. Each package gets the wrapper injected and one pilot string localized end-to-end.

- [ ] **Step 1:** Dispatch one subagent per package-cluster (suggest ~5 clusters of ~4 packages each: `blocks-finance-ish` = accounting+tax-reporting+rent-collection+subscriptions; `blocks-ops` = assets+inspections+maintenance+scheduling; `blocks-crm-ish` = businesscases+forms+leases+tenant-admin+workflow+tasks; `ui-and-adapters`; `accelerators-and-apps`).
- [ ] **Step 2:** Each subagent's deliverable per package: `Resources/SharedResource.resx` scaffolded, at least one pilot string localized with translator comment, DI registration added, package's primary entry-point file updated to inject `ISunfishLocalizer<T>`.
- [ ] **Step 3:** One commit per package-cluster. Path-scoped `git add packages/<cluster-roots>/`. Subagents MUST NOT run `git add .` or batch unrelated files.
- [ ] **Step 4:** Reviewer agent (spec-compliance + code-quality, serial) gates each cluster commit before the next cluster dispatches.

### Task 3.6: Cascade coverage report

**Files:**
- Create: `waves/global-ux/week-3-cascade-coverage-report.md`

- [ ] Report: packages covered, packages deferred (with reason), binary-gate score against the spec's `grep -r 'AddLocalization()'` ≥ 3 criterion + the `Every packages/* has Resources/SharedResource.resx` criterion.

---

## Week 4 — Polish + end-to-end validation

### Task 4.1: Hot-reload `IStringLocalizerFactory`

**Files:**
- Create: `packages/foundation/Localization/SunfishLocalizerFactory.cs`

**Why:** Spec Section 3A requires Debug-build hot-reload of `.resx` changes within 3 seconds.

- [ ] **Step 1:** Implement `IStringLocalizerFactory` wrapping the default factory. In `Debug` builds, attach a `FileSystemWatcher` to each `Resources/` directory. On file change, invalidate the cached `IStringLocalizer<T>` for the affected type.
- [ ] **Step 2:** Thread-safety: a `SemaphoreSlim` per-type-marker guards the cache swap.
- [ ] **Step 3:** Release builds delegate to the default factory with no watcher overhead. Verified via `#if DEBUG` conditional + assembly-level attribute check.
- [ ] **Step 4:** Manual test in kitchen-sink: edit `Resources/SharedResource.resx`, observe UI reflects change within 3 seconds without page reload (Blazor Server circuits).

### Task 4.2: `ProblemDetailsFactory` localization

**Files:**
- Create: `packages/foundation/Localization/SunfishProblemDetailsFactory.cs`

- [ ] **Step 1:** Subclass `Microsoft.AspNetCore.Mvc.ProblemDetailsFactory`. Override `CreateProblemDetails` to resolve `title` and `detail` through `ISunfishLocalizer<SharedResource>` using the ambient request's `CultureInfo.CurrentUICulture`.
- [ ] **Step 2:** Register in Bridge and Anchor composition roots via `services.AddSingleton<ProblemDetailsFactory, SunfishProblemDetailsFactory>()`.
- [ ] **Step 3:** Integration test: Bridge endpoint returning a 404 should return localized `title`/`detail` for en-US, ar-SA, ja based on `Accept-Language`.

### Task 4.3: Translator-comments analyzer

**Files:**
- Create: `tooling/Sunfish.Tooling.LocAnalyzer/MissingCommentAnalyzer.cs`

- [ ] **Step 1:** Roslyn analyzer reads `.resx` files (via `AdditionalFiles` MSBuild metadata). For each `<data>` missing `<comment>` or with empty comment, emits `SUNFISH_I18N_001` warning (matches spec §8 diagnostic table).
- [ ] **Step 2:** Severity: Warning (not Error) — the spec allows missing comments during initial cascade but flags them for translator quality.
- [ ] **Step 3:** Diagnostic code `SUNFISH_I18N_001` registered in `docs/diagnostic-codes.md`; cross-reference spec §8 diagnostic table.
- [ ] **Step 4:** False-positive check: run analyzer across Week-3 cascade output. If >5% of `<data>` elements flagged, re-tune the rule (possibly narrow to `packages/blocks-*/Resources/` only, exempt `compat-*` / internal-only).

### Task 4.4: End-to-end Arabic validation

**Files:**
- Create: `waves/global-ux/week-4-arabic-e2e-report.md`

**Why:** Prove the whole pipeline works for one hard locale before Week-4 gate.

- [ ] **Step 1:** Author three Arabic strings in `packages/blocks-tasks/Resources/SharedResource.ar-SA.resx` (simple, formatted, plural).
- [ ] **Step 2:** Run `dotnet build -t:SunfishExportXliff`; verify three XLIFF files reach Weblate within 60 s.
- [ ] **Step 3:** In Weblate UI, mark one entry as `approved`; push back to repo.
- [ ] **Step 4:** Run `dotnet build`; verify the approved entry reaches the running kitchen-sink UI under `ar-SA` culture, RTL layout correct (tested via Storybook direction toggle).
- [ ] **Step 5:** Document any friction, including sub-3-second hot-reload under Arabic.

### Task 4.5: Integration report + go/no-go for Plan 5 entry

**Files:**
- Create: `waves/global-ux/week-4-integration-report.md`
- Modify: `waves/global-ux/status.md` (end-of-Weeks-2-4 update)

- [ ] **Step 1:** Compile the week's deliverables: coverage report + XLIFF round-trip report + Weblate ops runbook + Arabic E2E report.
- [ ] **Step 2:** Score against Plan 2 success criteria table; record each as PASS / FAIL / DEFERRED with evidence link.
- [ ] **Step 3:** Binary verdict: PROCEED to Plan 5 (CI Gates) OR RE-PLAN weeks 2-4 with named fallback.

---

## Verification

### Automated

- `dotnet test tooling/Sunfish.Tooling.LocalizationXliff/tests/` — all round-trip + 12-locale tests green
- `dotnet test tooling/Sunfish.Tooling.LocAnalyzer.Tests/` — analyzer unit tests green
- `dotnet test packages/foundation/tests/Localization/` — existing SmartFormat smoke tests remain green
- Bridge integration test suite — `ProblemDetailsFactory` localization assertions green
- Build-time: `Sunfish.Tooling.LocAnalyzer` `SUNFISH_I18N_001` diagnostic emits on any test RESX missing `<comment>`

### Manual

- Kitchen-sink hot-reload demo: edit `.resx`, watch UI update in <3 s.
- Weblate UI visual check: glossary populated; all 12 locales listed; Arabic RTL editor works; MADLAD-400 suggestions surface with <10 s latency.
- Arabic E2E walkthrough: author → XLIFF → Weblate → approve → satellite assembly → UI rendered RTL with correct plural form.

### Ongoing Observability

- Weblate instance availability monitor (uptime ping every 60 s; alert after 3 failures).
- MADLAD MT backend latency metric (p95 < 10 s or fallback to DeepL).
- Post-Week-4: GitHub Action that fails the PR if `Resources/*.resx` is added without a matching culture-specific stub for the 12 target locales.

---

## Conditional sections

### Rollback Strategy

- **XLIFF task failure:** Fall back to `.resx` → PO-file via `gettext`; Weblate supports PO natively. Defer XLIFF 2.0 to Plan 6. Timeline cost: –3 days on Week 2.
- **Weblate self-host failure:** Switch to Crowdin Business ($175–450+/mo) per Task 3 memo. Timeline cost: +1 day on Week 2 (migration); recurring $175+/mo ongoing.
- **Cascade stuck on a package family:** Document as deferred-cascade; Plan 6 picks it up. Week 3 declared partial-success if ≥ 15 of the ~20 packages land.
- **Hot-reload brittleness:** Scope hot-reload to full-page reload only (disable circuit-live refresh). Degraded DX but unblocks Week 4.

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| CLDR plural-rule coverage gap in SmartFormat for one of 12 locales | Low | High (cascade-blocking) | Task 3.2 validation before cascade starts |
| Weblate Docker stack I/O-bound on cheap VM | Medium | Medium | Task 2.2 load test; budget for upgrade if needed |
| XLIFF round-trip non-determinism due to XML whitespace | Medium | Medium | Normalization pass in `ResxFile.Write`; Task 1.4 property tests guard |
| Week-3 subagent-driven cascade over-reaches commits (prior-session GitButler bug recurs) | Medium | High (pollutes branch) | Path-scoped `git add` mandatory in every subagent prompt; reviewer-agent gate per cluster |
| Translator-comments analyzer noisy → devs disable it | Low-Medium | Low | Task 4.3 false-positive calibration step; warn-not-error severity |

### Dependencies & Blockers

- **Depends on:** Plan 1 complete (Tasks 14-15 SmartFormat wrapper landed) ✅
- **Blocks:** Plan 5 (CI Gates) — CI cannot gate localization quality until the cascade lands
- **Blocks:** Plan 6 Phase 2 cascade — app-level localization depends on all block packages being wrapper-ready
- **External dependency:** Weblate 5.17.1 Docker image (public; unlikely blocker). MADLAD-400-3B-MT GGUF from HuggingFace (weight ~1.8 GB, one-time download).

### Delegation & Team Strategy

- **Solo-by-agent for Weeks 2 (infra):** 3 subagents, one per workstream (A/B/C), dispatch in parallel. Each has a narrow brief and path-scoped commit mandate.
- **Subagent-fleet for Week 3 (cascade):** 5 subagents, one per package-cluster, dispatched in two waves (not all 5 at once — reviewer-agent can't scale to 5 simultaneous clusters). Reviewer-agent runs serial between cluster commits.
- **Solo-by-Claude for Week 4 (polish + integration):** Hot-reload, ProblemDetailsFactory, analyzer work needs careful state-machine + Roslyn reasoning; better done in foreground Claude context than subagents.

### Incremental Delivery

- **End of Week 2:** XLIFF task + Weblate stack + scaffolder usable in isolation (Sunfish devs can scaffold + round-trip in one package manually).
- **End of Week 3:** All ~20 packages are wrapper-ready; one pilot string per package localized.
- **End of Week 4:** End-to-end Arabic flow works; hot-reload running; server errors localized.

### Reference Library

- Week-1 [spec](../specs/2026-04-24-global-first-ux-design.md) §3A, §3B
- Week-1 [Plan 1](./2026-04-24-global-first-ux-phase-1-week-1-plan.md)
- Week-0 [ICU4N memo](../../../icm/01_discovery/output/icu4n-health-check-2026-04-25.md)
- Week-0 [Weblate vs Crowdin memo](../../../icm/01_discovery/output/weblate-vs-crowdin-2026-04-25.md)
- Week-0 [XLIFF tool survey](../../../icm/01_discovery/output/xliff-tool-ecosystem-2026-04-26.md)
- [ADR 0034](../../adrs/0034-a11y-harness-per-adapter.md) (parallel-plan context)
- [ADR 0035](../../adrs/0035-global-domain-types-as-separate-wave.md) (scope boundary)
- [decisions.md](../../../waves/global-ux/decisions.md) (ICU4N → SmartFormat pivot)
- SmartFormat.NET PluralLocalizationFormatter: https://github.com/axuno/SmartFormat
- Weblate XLIFF 2.0: https://docs.weblate.org/en/latest/formats/xliff2.html
- Weblate MT backends: https://docs.weblate.org/en/latest/admin/machine.html
- OASIS XLIFF 2.0 spec: http://docs.oasis-open.org/xliff/xliff-core/v2.0/xliff-core-v2.0-os.html
- CLDR plural rules: https://www.unicode.org/cldr/charts/48/supplemental/language_plural_rules.html

### Learning & Knowledge Capture

- Document in `waves/global-ux/decisions.md` on any new tool-choice pivot (e.g., if Weblate fallback to Crowdin triggers).
- End-of-Week-4 retrospective in `waves/global-ux/week-4-integration-report.md`: what surprised us, what cost more than expected, what should change for Plan 3 (Translator-Assist) and Plan 4 (A11y Foundation cascade) to avoid hitting the same surprises.

### Replanning Triggers

- Week-2 workstream slips > 3 days: re-sequence to land XLIFF task first (highest-risk), defer Weblate load test or MT backend to Week 3.
- Week-3 cascade hits < 15 of the ~20 target packages by Friday: declare Week-3 partial; cut analyzer + ProblemDetailsFactory to Plan 5; scope Week-4 to Arabic E2E validation only.
- Any gate-blocking failure: append to `decisions.md` with named pivot; re-author a revised Plan 2 with adjusted scope.

---

## Cold Start Test

A fresh agent walking into this plan should be able to execute Task 1.1 without further context by:
1. Reading this plan.
2. Reading Plan 1 for the commit-style and subagent-driven pattern.
3. Reading `waves/global-ux/decisions.md` for the ICU4N→SmartFormat pivot context.
4. Reading the three Week-0 memos for the tool-choice rationales.

No additional context should be required. If any step requires out-of-band knowledge not in one of those four documents, that is a plan-hygiene bug — file an issue and update this plan before executing.

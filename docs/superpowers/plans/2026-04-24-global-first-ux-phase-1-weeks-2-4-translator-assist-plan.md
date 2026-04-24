# Global-First UX — Phase 1 Weeks 2-4 Translator-Assist

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the translator-facing workflow around the Weblate instance that Plan 2 deploys — automated extraction from `.resx` into XLIFF drafts, MADLAD-400 pre-publish translation-draft generation, glossary enforcement as a commit gate, ICU placeholder preservation, translator recruitment, and post-edit quality heuristics — so that by the Week-4 gate, a human translator can open Weblate, see MADLAD drafts, accept or edit them under glossary protection, and have the result flow through to satellite assemblies with CI quality gates green.

**Architecture:** Runs parallel to Plan 2 (Loc-Infra) and Plan 4 (A11y). Plan 3 is the *people-and-quality* plan; Plan 2 is the *pipes*. Week 2 builds the extraction hook + placeholder validator + glossary check. Week 3 wires MADLAD as a pre-publish CI step (generating drafts into XLIFF) and lands the glossary-enforcement plugin on the Weblate side. Week 4 recruits translators, onboards them, and validates post-edit quality heuristics against real MADLAD output.

**Tech stack:** .NET 11 preview, Python 3.12 (Weblate plugins are Python), llama.cpp server mode with MADLAD-400-3B-MT GGUF (dev workstation) and 7B-MT (CI, Sunfish-hosted), Husky.NET pre-commit hook framework, `System.Xml.Linq` for XLIFF manipulation, Weblate `weblate.checks.Check` extension points, TermBase eXchange (TBX) v3, OpenAI-compatible REST API bridge (llama.cpp → Weblate `MT_OPENAI_BASE_URL`).

**Scope boundary:** This plan covers Phase 1 Weeks 2-4 ONLY (15 business days), running parallel to Plans 2 and 4. It does NOT cover:
- The Weblate Docker stack itself — that is **Plan 2 Workstream B (Tasks 2.1-2.4)**. Plan 3 assumes a running Weblate instance by end of Week 2.
- MADLAD MT backend wiring into Weblate's OpenAI-compat endpoint — that is **Plan 2 Task 2.5**. Plan 3 builds the *pre-publish draft generator* on top of that wiring.
- The XLIFF 2.0 round-trip MSBuild task — that is **Plan 2 Workstream A (Tasks 1.1-1.5)**. Plan 3's extraction hook writes into the XLIFF files that Plan 2's task reads/writes.
- Wrapper cascade across packages — that is **Plan 2 Workstream C + Week 3 Cascade**.
- Accessibility harness work — that is **Plan 4**.
- Phase 2 translator-assist (full nightly CI loop, 7B model on dedicated CI host, multi-language coordinator workflow) — that is Plan 6.

**Parent spec:** [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../specs/2026-04-24-global-first-ux-design.md) §3B
**Parallel plans:** [Plan 2 Loc-Infra](./2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md), Plan 4 A11y (pending)
**Predecessor plan:** [Plan 1 Week-1 Tooling Pilot](./2026-04-24-global-first-ux-phase-1-week-1-plan.md) (complete — GO verdict 2026-04-24)
**Week-0 memo:** [Weblate vs Crowdin triage](../../../icm/01_discovery/output/weblate-vs-crowdin-2026-04-25.md) (AGPL §13 caveat is unresolved — see Assumptions)

---

## Context & Why

Plan 2 delivers the translation *infrastructure* — Weblate, XLIFF round-trip, wrapper cascade — but a running Weblate with no drafts, no glossary enforcement, and no translators is an empty room. Plan 3 fills the room.

The spec's §3B architecture is explicit: MADLAD-400 runs at dev/CI time only, never in shipped binaries. That constraint drives the shape of Plan 3 — every translation-quality lever lives in the pre-publish pipeline (extraction hook, MADLAD draft, glossary check, placeholder validator, post-edit heuristic flagger) and none of it ships to end users. The runtime surface is just `IStringLocalizer<T>` reading a satellite assembly that CI assembled from approved translator output.

Three concerns drive the plan's phasing:

1. **Extraction automation must land before MADLAD wiring**, because MADLAD operates on XLIFF units and needs something to populate those units. Week 2.
2. **Glossary enforcement must land before translators touch the system**, because a translator who mis-translates "Sunfish" once, gets the commit rejected, and re-reads the glossary costs five minutes; a translator who mis-translates it 50 times across four locales before anyone notices costs a week of rework. Week 2-3.
3. **Translator recruitment lags the tooling by one week**, because recruiting into a broken system wastes the translator's first impression. Week 4.

---

## Success Criteria

### PASSED — proceed to Plan 5 (CI Gates) finalization

- Pre-commit hook `sunfish-loc extract` runs on every `.resx` edit and produces a matching `.xlf` draft with `{key, source, comment}` units in the correct XLIFF 2.0 shape; hook exits non-zero if extraction fails.
- MADLAD-400-3B-MT draft-generation CI step runs in <15 seconds per 100 segments on the reference dev machine (16 GB RAM, no GPU) and emits `state="needs-review"` on every generated segment — never `state="final"` or `state="translated"`.
- Custom Weblate check plugin `sunfish_glossary_enforcement` is loaded in the running Weblate instance and blocks approval of any segment that (a) translates "Sunfish" to anything other than "Sunfish" or (b) uses lowercase "block" where the source is the domain-sense "Block". Verified with two fixture segments hitting the check.
- Custom Weblate check plugin `sunfish_placeholder_preservation` validates that ICU-style `{count, plural, =0 {…} one {…} other {…}}` placeholders survive round-trip unchanged; blocks approval if any placeholder is dropped, renamed, or its brace structure broken. Verified with three fixture segments across en→ar, en→ja, en→ru.
- Translator recruitment runbook committed at `docs/i18n/translator-recruitment.md` with: target translator count per locale (2-3), recruitment channels (LinkedIn, ProZ, Weblate volunteer network, open-source contributor outreach), onboarding checklist, glossary walkthrough, payment tiers for paid locales vs volunteer acknowledgment for community locales.
- Post-edit quality heuristic script `tooling/Sunfish.Tooling.LocQuality/PostEditFlagger.cs` flags suspicious translator outputs (length ratio anomaly, placeholder count mismatch, glossary miss, high edit-distance from MADLAD draft with no translator comment explaining the divergence) for second-pass review. Verified against 10 fixture translations with 3 seeded "suspicious" cases.
- MADLAD quality-gate CI check (`madlad-smoke.yml` GitHub Action) runs on PRs that touch `localization/xliff/*.xlf` and fails if (a) MADLAD emits a segment with `state="final"` (bug), (b) MADLAD output contains a known-bad pattern from the glossary enforcement ruleset, or (c) MADLAD 95th-percentile latency exceeds 15s/100 segments on the CI runner.

### FAILED — triggers a Week-4 re-plan (not Phase 1 abort)

- MADLAD-400-3B-MT inference time exceeds 15s/100 segments on the reference dev machine — fallback is Weblate's built-in DeepL backend (paid) for the 4 paid locales and no-MT for the 8 volunteer locales.
- Weblate custom check plugin architecture does not support Sunfish's glossary rule complexity (e.g., capitalization-sensitive matching within morphologically-inflected translations in Russian) — fallback is CI-side enforcement only, losing the in-editor warning for translators.
- ICU placeholder preservation validator has >10% false-positive rate on hand-authored fixture translations (possibly due to SmartFormat dialect differences from strict ICU MessageFormat) — narrow the validator scope, document the gap, and cover in Plan 5.
- Translator recruitment yields <1 qualified translator per locale for any of the 4 paid locales (en, es-LA, fr, de) — escalate to paid agency for that locale or defer that locale to Phase 2.

### Kill trigger (30-day timeout)

If Plan 3 has not landed all success criteria by **2026-05-24** (30 days from Week-1 GO), escalate to BDFL for scope cut: named options are (a) defer MADLAD CI wiring to Plan 6 and use "Weblate's built-in DeepL for paid locales, human-from-scratch for volunteer locales" as a stopgap; (b) defer glossary enforcement plugin to Plan 5 and use CI-only enforcement in Weeks 2-4; (c) defer translator recruitment to Phase 2 and use the project team for Arabic E2E validation only.

---

## Assumptions & Validation

| Assumption | VALIDATE BY | IMPACT IF WRONG |
|---|---|---|
| MADLAD-400-3B-MT produces publishable-quality drafts (translator edit <20% of tokens on average) across the 12 target locales | Task 3.4 — run MADLAD against 50 hand-curated en-US source strings per locale, measure translator edit distance after first pass | If quality is poor (>40% edit distance), MADLAD is a net slowdown for translators; fallback to human-from-scratch workflow. Cost: ~20% translator-hour increase for volunteer locales. |
| Weblate's `weblate.checks.Check` extension points permit a custom plugin with access to glossary entries and full source/target text | Task 2.2 — build a minimal stub check that reads glossary, register it, verify it fires on a fixture translation | If the extension surface is too narrow, fallback is CI-side-only enforcement via a GitHub Action on PR merge; translators lose in-editor feedback. |
| Husky.NET pre-commit hook works reliably on Windows, macOS, and Linux dev machines across the team | Task 1.3 — dry-run on all three platforms | If flaky on one platform, mark that platform "run `sunfish-loc extract` manually before commit" and file a follow-up; don't block cascade. |
| ICU placeholder syntax `{count, plural, =0 {…} one {…} other {…}}` used by SmartFormat is a subset of strict ICU MessageFormat that can be parsed with a regex-based validator (no full AST required) | Task 1.5 — fuzz-test the validator against 500 randomly-generated SmartFormat patterns | If regex is insufficient, port a minimal ICU parser from `OrchardCore.Localization.Core` (MIT) or pivot to full ICU4N parsing. Cost: ~2 days. |
| AGPL §13 does not trigger for Sunfish's internal use of Weblate in Phase 1 (no external third-party access) | Task 4.1 — confirm with legal counsel that Phase 1 deployment is internal-only | If counsel is unavailable before Week 4, defer translator onboarding to Phase 2 and keep Weblate behind VPN-only access. Cost: ~1 week slip on translator recruitment. |
| MADLAD-400-3B-MT GGUF Q4_K_M quantization preserves translation quality acceptably vs Q8_0 or full FP16 | Task 3.4 — quality-compare Q4_K_M vs Q8_0 on the 50-string fixture | If Q4_K_M is materially worse, bump to Q8_0 (2x memory, ~1.3x slower); still well within the 15s/100-segment budget. |
| Post-edit quality heuristics have a signal-to-noise ratio >3:1 (useful flags outnumber false positives) | Task 4.3 — dogfood against 3 rounds of real translator output | If SNR <2:1, devs/translators ignore the flagger; tune rules or narrow scope until SNR lands. |

---

## File Structure (Weeks 2-4 deliverables)

```
tooling/
  Sunfish.Tooling.LocExtraction/
    Sunfish.Tooling.LocExtraction.csproj    ← Pre-commit extractor
    ResxToXliffDraftExtractor.cs            ← Writes XLIFF drafts from .resx
    PlaceholderValidator.cs                  ← ICU placeholder preservation
    IcuPlaceholderRegex.cs                   ← Regex + test fixtures
    Program.cs                               ← `sunfish-loc extract` CLI entry
    tests/
      ExtractionTests.cs
      PlaceholderValidatorTests.cs
      PlaceholderFuzzTests.cs                ← 500-pattern fuzz test

  Sunfish.Tooling.LocQuality/
    Sunfish.Tooling.LocQuality.csproj
    PostEditFlagger.cs                       ← Post-edit heuristics
    MadladDraftGenerator.cs                  ← CI-side MADLAD driver
    LengthRatioHeuristic.cs
    EditDistanceHeuristic.cs
    GlossaryMissHeuristic.cs
    tests/
      PostEditFlaggerTests.cs                ← 10 fixture translations, 3 seeded suspicious

infra/
  weblate/
    plugins/
      sunfish_glossary_enforcement/
        __init__.py
        check.py                             ← weblate.checks.Check subclass
        glossary_loader.py                   ← Reads localization/glossary/*.tbx
        README.md                            ← Plugin install + config
      sunfish_placeholder_preservation/
        __init__.py
        check.py
        placeholder_regex.py                 ← Python port of IcuPlaceholderRegex
        README.md
    Dockerfile.plugins                       ← Derived image with plugins baked in

localization/
  glossary/
    sunfish-glossary.tbx                     ← (extended by Task 2.1 — new terms added)
    enforcement-rules.yaml                   ← Capitalization, do-not-translate, domain-sense rules

.husky/
  pre-commit                                 ← Calls `sunfish-loc extract` on staged .resx

.github/workflows/
  madlad-smoke.yml                           ← MADLAD quality-gate CI check
  loc-quality.yml                            ← Post-edit flagger CI check

docs/i18n/
  translator-recruitment.md                  ← Runbook: who, how many per locale, onboarding
  coordinators.md                            ← (extended) per-locale translator roster
  post-edit-review-guide.md                  ← Translator-facing guide to the flagger

waves/global-ux/
  week-2-extraction-hook-report.md           ← Task 1.6 output
  week-3-madlad-quality-report.md            ← Task 3.5 output
  week-4-translator-onboarding-report.md     ← Task 4.4 output
```

---

## Week 2 — Extraction hook + placeholder validator + glossary rules

### Task 1.1: Scaffold the `Sunfish.Tooling.LocExtraction` CLI

**Files:**
- Create: `tooling/Sunfish.Tooling.LocExtraction/Sunfish.Tooling.LocExtraction.csproj`
- Create: `tooling/Sunfish.Tooling.LocExtraction/Program.cs`

**Why:** The pre-commit hook needs a single command to invoke. Keeping it as a standalone CLI (not an MSBuild task) means it can run outside a build context — critical for pre-commit speed.

- [ ] **Step 1:** csproj targets `net11.0` (matching repo baseline). `OutputType` is `Exe`. `RootNamespace` is `Sunfish.Tooling.LocExtraction`. Pack as a local tool (`PackAsTool=true`, `ToolCommandName=sunfish-loc`).
- [ ] **Step 2:** `Program.cs` dispatches subcommands: `extract`, `validate-placeholders`, `--help`. Use `System.CommandLine` (already in Sunfish's baseline).
- [ ] **Step 3:** Commit. Path-scoped `git add tooling/Sunfish.Tooling.LocExtraction/` only.

### Task 1.2: RESX → XLIFF draft extractor

**Files:**
- Create: `tooling/Sunfish.Tooling.LocExtraction/ResxToXliffDraftExtractor.cs`

**Why:** The pre-commit hook reads the staged `.resx` file, extracts each `<data>` element as `{key, source, comment}`, and writes a draft `.xlf` that Plan 2's XLIFF task will later reconcile into the canonical `localization/xliff/` tree.

- [ ] **Step 1:** For each `<data name="..."><value>...</value><comment>...</comment></data>` in the RESX, emit an XLIFF 2.0 `<unit id="<name>">` with `<source>` + `<notes><note category="translator-comment">`. Units are sorted deterministically by `id` to keep diffs stable.
- [ ] **Step 2:** When the same `<unit id>` already exists in the target `.xlf` with `state="translated"` or `state="final"`, preserve it verbatim and only update `<source>` + `<note>`. Never overwrite translator work.
- [ ] **Step 3:** When a `<data>` is deleted from the RESX, mark the corresponding `<unit>` in the XLIFF as `state="obsolete"` (per Plan 2 Task 1.3's 90-day retention rule).
- [ ] **Step 4:** Unit tests: round-trip fixture RESX files including obsolete-unit handling.

### Task 1.3: Husky.NET pre-commit wiring

**Files:**
- Create: `.husky/pre-commit`
- Modify: `Directory.Build.props` (add `Husky.Net` `PackageReference`)

**Why:** The hook must run automatically on every `git commit` that touches a staged `.resx`. Husky.NET is the repo standard for .NET pre-commit hooks.

- [ ] **Step 1:** Install Husky.NET per the repo's existing pattern. Hook script checks `git diff --cached --name-only --diff-filter=AM` for `.resx` files. If any, run `dotnet sunfish-loc extract --staged`.
- [ ] **Step 2:** Exit non-zero on extraction failure. Block the commit. Print a clear error with the offending file path.
- [ ] **Step 3:** Dry-run the hook on Windows, macOS (dev-container), and Linux (CI runner). Record any platform-specific friction in `waves/global-ux/week-2-extraction-hook-report.md`.
- [ ] **Step 4:** Document the bypass (`git commit --no-verify`) as a last-resort escape hatch with a warning that CI will re-run extraction and fail if output differs.

### Task 1.4: ICU placeholder preservation validator (C# side)

**Files:**
- Create: `tooling/Sunfish.Tooling.LocExtraction/PlaceholderValidator.cs`
- Create: `tooling/Sunfish.Tooling.LocExtraction/IcuPlaceholderRegex.cs`

**Why:** An Arabic translator who drops the `{count}` placeholder from a plural form silently breaks the render at runtime. CI must catch this before the XLIFF flows back to `.resx`.

- [ ] **Step 1:** Build the ICU placeholder regex. Must match `{name}`, `{name, plural, ...}`, `{name, select, ...}`, `{name, number, ...}`, `{name, date, ...}`. Handles nested braces up to 3 levels (ICU spec allows arbitrary nesting but Sunfish's usage caps at 3).
- [ ] **Step 2:** `Validate(source, target)` extracts placeholders from both and checks set-equality on placeholder names (not values — translated plural branches are expected to differ).
- [ ] **Step 3:** Return a structured `ValidationResult` with `Severity` (`Error`/`Warning`), `DroppedPlaceholders`, `AddedPlaceholders`, `BracesMismatch`.

### Task 1.5: Placeholder validator fuzz tests

**Files:**
- Create: `tooling/Sunfish.Tooling.LocExtraction/tests/PlaceholderFuzzTests.cs`

**Why:** The regex must tolerate the full SmartFormat dialect Sunfish uses in production. Fuzz-testing against 500 generated patterns catches edge cases that hand-written fixtures miss.

- [ ] **Step 1:** FsCheck generator produces valid SmartFormat patterns with 0-5 placeholders, depth 1-3, mixed `plural`/`select`/`number`/`date` types. 500 iterations.
- [ ] **Step 2:** For each pattern: assert `ExtractPlaceholders(pattern)` returns the known-good set.
- [ ] **Step 3:** Negative fuzz: corrupt 100 valid patterns (drop a brace, rename a placeholder) and assert `Validate(original, corrupted)` returns a non-empty `ValidationResult` with matching `DroppedPlaceholders`/`BracesMismatch`.
- [ ] **Step 4:** If false-positive rate on the positive fuzz exceeds 1% or false-negative rate on the negative fuzz exceeds 1%, the regex is wrong; port `OrchardCore.Localization.Core` ICU parser instead (per Assumption row 4).

### Task 1.6: Week-2 extraction-hook report

**Files:**
- Create: `waves/global-ux/week-2-extraction-hook-report.md`

- [ ] Document: extraction hook installed and firing on staged `.resx`; fuzz-tested placeholder validator green; per-platform friction notes; commit-bypass escape hatch documented.

### Task 1.7: Glossary enforcement rules

**Files:**
- Create: `localization/glossary/enforcement-rules.yaml`
- Modify: `localization/glossary/sunfish-glossary.tbx` (add terms seeded by Plan 2 Task 3.3)

**Why:** Glossary entries in TBX are translator-facing suggestions. Enforcement rules are machine-readable gates: "Sunfish" is do-not-translate (DNT); "Block" in the domain sense must preserve capitalization; "Anchor" and "Bridge" are DNT when referring to the accelerator products.

- [ ] **Step 1:** `enforcement-rules.yaml` schema:
  ```yaml
  rules:
    - term: Sunfish
      type: do-not-translate
      applies-to: [all-locales]
      severity: error
    - term: Block
      type: preserve-capitalization
      applies-to: [all-locales]
      context: domain-sense  # matched via comment or key-prefix
      severity: error
    - term: Anchor
      type: do-not-translate
      applies-to: [all-locales]
      context: accelerator-name  # matched via comment or key-prefix
      severity: warning
    # ...
  ```
- [ ] **Step 2:** Seed ~20 rules covering: product names (Sunfish, Anchor, Bridge), domain terms (Block, Sync, Conflict, Quarantine, CRDT), technical DNT (OAuth, HTTP, JSON, SQL), branded UX terms (Kitchen Sink, Ledger).
- [ ] **Step 3:** Validate the YAML against a JSON schema in `enforcement-rules.schema.json` so typos in rule-type fail fast.

---

## Week 3 — MADLAD draft generation + Weblate check plugins

### Task 2.1: Scaffold `Sunfish.Tooling.LocQuality` CLI

**Files:**
- Create: `tooling/Sunfish.Tooling.LocQuality/Sunfish.Tooling.LocQuality.csproj`
- Create: `tooling/Sunfish.Tooling.LocQuality/Program.cs`

**Why:** MADLAD driver + post-edit flagger share a CLI. Keeping both under one tool means one install step for CI and dev machines.

- [ ] **Step 1:** csproj targets `net11.0`, `PackAsTool=true`, `ToolCommandName=sunfish-loc-quality`. Subcommands: `generate-drafts`, `flag-post-edit`, `--help`.
- [ ] **Step 2:** Shared HTTP client for llama.cpp OpenAI-compat endpoint. Configurable via env var `SUNFISH_MADLAD_BASE_URL` (default `http://localhost:8080/v1`, matching Plan 2 Task 2.5 default).

### Task 2.2: Weblate glossary-enforcement check plugin

**Files:**
- Create: `infra/weblate/plugins/sunfish_glossary_enforcement/__init__.py`
- Create: `infra/weblate/plugins/sunfish_glossary_enforcement/check.py`
- Create: `infra/weblate/plugins/sunfish_glossary_enforcement/glossary_loader.py`
- Create: `infra/weblate/plugins/sunfish_glossary_enforcement/README.md`
- Modify: `infra/weblate/Dockerfile.plugins` (bake plugin into derived image)

**Why:** In-editor glossary enforcement is the highest-signal feedback loop for translators. Catching "Sunfish → سنفيش" in the editor (not in CI after the commit) saves a full round-trip.

- [ ] **Step 1:** Read Weblate's check-plugin extension surface. Weblate's `weblate.checks.Check` base class exposes `check_target_unit(sources, targets, unit)` returning a bool. Plugin registers via `settings.py` `CHECK_LIST = [..., "sunfish_glossary_enforcement.check.GlossaryEnforcementCheck"]`.
- [ ] **Step 2:** Plugin loads `localization/glossary/enforcement-rules.yaml` at check-instance init (cached, reloaded on SIGHUP).
- [ ] **Step 3:** For each rule: if `type: do-not-translate`, assert `term` appears literally in target; if `type: preserve-capitalization`, assert capitalization of `term` matches source.
- [ ] **Step 4:** Return `True` (flag as failing check) if any rule violated; Weblate's UI then surfaces the failure inline and blocks the translator's "Approve" action until resolved or explicitly dismissed with a reviewer's comment.
- [ ] **Step 5:** Bake into `Dockerfile.plugins`: `COPY plugins/ /app/data/python/weblate/plugins/`. Derived image used in place of upstream Weblate in `docker-compose.yml`.
- [ ] **Step 6:** Smoke test: in the running Weblate, edit an en→ar translation changing "Sunfish" to "سنفيش"; confirm the check fires inline, blocks approval, and surfaces the rule name + enforcement-rules.yaml reference in the UI.

### Task 2.3: Weblate placeholder-preservation check plugin

**Files:**
- Create: `infra/weblate/plugins/sunfish_placeholder_preservation/__init__.py`
- Create: `infra/weblate/plugins/sunfish_placeholder_preservation/check.py`
- Create: `infra/weblate/plugins/sunfish_placeholder_preservation/placeholder_regex.py`
- Create: `infra/weblate/plugins/sunfish_placeholder_preservation/README.md`

**Why:** The C# validator (Task 1.4) catches placeholder drops at commit time. The Python port catches them inline in the editor — same logic, different host.

- [ ] **Step 1:** Port the ICU placeholder regex from `IcuPlaceholderRegex.cs` to Python. Validate parity: 50 fixture patterns produce identical placeholder sets from both implementations.
- [ ] **Step 2:** Check fires when `ExtractPlaceholders(source) != ExtractPlaceholders(target)`. Message includes the specific dropped/added placeholder name for translator clarity.
- [ ] **Step 3:** Smoke test: in Weblate, author an ar-SA translation that drops `{count}` from a plural form; confirm the check fires inline and surfaces "Missing placeholder: {count}".

### Task 2.4: MADLAD draft generator (`generate-drafts` subcommand)

**Files:**
- Create: `tooling/Sunfish.Tooling.LocQuality/MadladDraftGenerator.cs`

**Why:** When a new key enters `en-US.resx`, a CI nightly job should generate MADLAD drafts for all 12 target locales and push them as `state="needs-review"` into the XLIFF tree. This is the core translator-assist feature.

- [ ] **Step 1:** Command `sunfish-loc-quality generate-drafts --source en-US --target ar-SA --input <xlf>` reads all `<unit>` elements with empty `<target>` or missing `<target>`.
- [ ] **Step 2:** For each unit: POST to llama.cpp's `/v1/chat/completions` with a system prompt that enforces: "Translate from English to {target-locale}. Preserve all {…} placeholders verbatim. Preserve all capitalization of DNT terms per glossary. Return only the translation, no commentary."
- [ ] **Step 3:** Write the draft back into the XLIFF as `<target state="needs-review">...`. Never `state="translated"` or `state="final"` — a human must approve in Weblate.
- [ ] **Step 4:** Append `<note category="mt-metadata">generated-by:madlad-400-3b-mt; model-hash:<sha>; draft-date:<iso>; latency-ms:<n></note>` for later quality-gate inspection.
- [ ] **Step 5:** Rate-limit: batch 10 segments per llama.cpp call to amortize prompt overhead; target <15s per 100 segments on the reference dev machine.

### Task 2.5: MADLAD quality-gate CI check

**Files:**
- Create: `.github/workflows/madlad-smoke.yml`

**Why:** The CI check protects the repo from MADLAD regressions — a new model checkpoint that emits `state="final"` by accident, or a latency regression that blows past the 15s budget, should fail PRs before merge.

- [ ] **Step 1:** GitHub Action triggers on PRs touching `localization/xliff/*.xlf`. Boots the llama.cpp container on the runner (pulls cached GGUF from runner's Docker volume).
- [ ] **Step 2:** Runs `sunfish-loc-quality generate-drafts` against a 100-segment fixture. Measures wall-clock latency.
- [ ] **Step 3:** Asserts: (a) no segment emitted with `state="final"` or `state="translated"`; (b) every segment has `mt-metadata` note; (c) p95 latency <15s per 100 segments; (d) no output matches a known-bad pattern from `enforcement-rules.yaml` (e.g., no segment outputs "سنفيش" as a translation of "Sunfish").
- [ ] **Step 4:** Fail the action on any assertion miss. Attach the offending XLIFF as a workflow artifact for debug.
- [ ] **Step 5:** Cache the MADLAD GGUF on the runner (size ~1.8 GB); first-run cold boot is ~3 min, warm runs ~10s.

### Task 2.6: Week-3 MADLAD quality report

**Files:**
- Create: `waves/global-ux/week-3-madlad-quality-report.md`

- [ ] Document: MADLAD CI check green; 50-segment fixture quality comparison (Q4_K_M vs Q8_0); Weblate check plugins loaded and smoke-tested; known-good and known-bad patterns seeded.

---

## Week 4 — Translator recruitment + post-edit quality + Arabic E2E

### Task 3.1: Translator recruitment runbook

**Files:**
- Create: `docs/i18n/translator-recruitment.md`

**Why:** The success criteria require 2-3 qualified translators per locale. This runbook defines "qualified," where to find them, what onboarding looks like, and how they're paid (or acknowledged as volunteers).

- [ ] **Step 1:** Define per-locale target: 2-3 translators per locale = 24-36 people across 12 locales. Tiered:
  - **Paid tier (4 locales):** en-US (source), es-LA, fr, de — these are high-traffic locales where translation quality directly affects commercial paid-accelerator adopters. Budget: $0.08-0.15/word or $50-100/hour per contract translator.
  - **Volunteer tier (8 locales):** ar-SA, hi, zh-Hans, zh-Hant, ja, ko, ru, pt-BR — community-contributed. Acknowledgment: credited in `docs/i18n/coordinators.md`; Weblate-earned badge; annual thank-you swag.
- [ ] **Step 2:** Recruitment channels per tier:
  - **Paid:** ProZ.com job post, LinkedIn recruiter outreach, referrals from the Marilo network (CTW's prior project).
  - **Volunteer:** Weblate's "Hosted Weblate" volunteer network, OSS contributor outreach (post in r/opensource, Hacker News "Ask HN", Fediverse localization groups), Sunfish's own GitHub README call-to-action.
- [ ] **Step 3:** Qualification criteria:
  - Native fluency in target locale (self-attested + verified with 50-word translation sample).
  - Familiarity with software-translation conventions (placeholder preservation, terse UI copy, glossary-driven DNT terms).
  - For paid: professional references or prior credited software-translation work.
- [ ] **Step 4:** Onboarding checklist:
  - [ ] Weblate account created, added to locale team.
  - [ ] Walked through glossary (`docs/i18n/post-edit-review-guide.md`).
  - [ ] Completed 10-string practice set; reviewed by locale coordinator.
  - [ ] Signed translator agreement (paid tier) or credit-acknowledgment (volunteer tier).
  - [ ] First real commit merged within 1 week of onboarding (activation metric).
- [ ] **Step 5:** Payment logistics for paid tier: Wise or PayPal Business for international contractors; monthly invoicing; 1099/W-8BEN paperwork handled pre-first-payment.

### Task 3.2: Post-edit quality heuristic flagger

**Files:**
- Create: `tooling/Sunfish.Tooling.LocQuality/PostEditFlagger.cs`
- Create: `tooling/Sunfish.Tooling.LocQuality/LengthRatioHeuristic.cs`
- Create: `tooling/Sunfish.Tooling.LocQuality/EditDistanceHeuristic.cs`
- Create: `tooling/Sunfish.Tooling.LocQuality/GlossaryMissHeuristic.cs`

**Why:** After a human edits a MADLAD draft, some edits are suspicious — too long, too short, too different from the draft with no comment explaining why. Flag these for second-pass review before merge; don't block, just flag.

- [ ] **Step 1:** `LengthRatioHeuristic` — flag if target/source token-count ratio is outside per-locale expected band (e.g., en→de ratio 1.0-1.3; en→zh ratio 0.3-0.6). Bands seeded from CLDR length tables.
- [ ] **Step 2:** `EditDistanceHeuristic` — flag if Levenshtein distance between MADLAD draft and human-edited target is >60% of target length *and* no `<note category="translator-comment">` explains the divergence. Rationale: if a translator completely rewrites MADLAD, that's either genuine draft rejection (useful signal) or a glossary miss (bug).
- [ ] **Step 3:** `GlossaryMissHeuristic` — run the same logic as the Weblate plugin (Task 2.2) but server-side on the final XLIFF; catches any translator who dismissed the inline check warning.
- [ ] **Step 4:** `PostEditFlagger.Flag(xlf)` runs all three heuristics, writes a structured report to `waves/global-ux/post-edit-review-queue.json` for coordinator triage.
- [ ] **Step 5:** Tests: 10 fixture translations; 3 seeded suspicious (one length-ratio, one edit-distance, one glossary-miss); assert flagger catches all 3 and flags none of the 7 clean ones.

### Task 3.3: Post-edit flagger CI check

**Files:**
- Create: `.github/workflows/loc-quality.yml`

- [ ] **Step 1:** GitHub Action triggers on PRs touching `localization/xliff/*.xlf`. Runs `sunfish-loc-quality flag-post-edit`.
- [ ] **Step 2:** Posts a PR comment summarizing flags (not blocking — advisory only in Phase 1). "Found N segments needing second-pass review: …"
- [ ] **Step 3:** Writes the review queue JSON as a workflow artifact for coordinator triage.
- [ ] **Step 4:** Phase 2 (out of scope for this plan) will escalate to blocking after SNR tuning.

### Task 3.4: Translator-facing post-edit review guide

**Files:**
- Create: `docs/i18n/post-edit-review-guide.md`

**Why:** Translators need a short, readable guide to the heuristics so flags aren't confusing. "Why is the flagger complaining about my length ratio? Is that a bug?"

- [ ] Cover: what each heuristic flags; how to respond (edit, or add a `<note category="translator-comment">` with reason); what "second-pass review" means operationally; who the locale coordinator is and how to escalate.
- [ ] Length: ~3 pages, with per-locale examples for length ratios and per-domain examples for edit-distance divergence.

### Task 3.5: Arabic E2E validation with real translator output

**Files:**
- Create: `waves/global-ux/week-4-translator-onboarding-report.md`

**Why:** Prove end-to-end that the plan works with a real translator, not just synthetic fixtures.

- [ ] **Step 1:** Onboard the first Arabic volunteer (per Task 3.1 recruitment). Walk them through the onboarding checklist end-to-end.
- [ ] **Step 2:** They translate 10 strings in Weblate. Glossary check fires at least once; placeholder check fires at least once (seed an `inbox.unread` plural for this). Each is resolved.
- [ ] **Step 3:** Their approved translations flow back to `.resx` via Plan 2's XLIFF import task. The kitchen-sink UI renders the Arabic strings under `ar-SA` culture.
- [ ] **Step 4:** Run post-edit flagger on their output. Record flag count; triage with the translator to calibrate SNR.
- [ ] **Step 5:** Document: time-to-first-commit metric, friction points, glossary gaps discovered, any plugin bugs found.

### Task 3.6: Week-4 go/no-go report + Plan 5 entry gate

**Files:**
- Modify: `waves/global-ux/status.md` (end-of-Plan-3 update)

- [ ] **Step 1:** Score each success criterion from the Success Criteria table; record PASS/FAIL/DEFERRED with evidence link.
- [ ] **Step 2:** Binary verdict: PROCEED to Plan 5 (CI Gates) OR RE-PLAN this plan's scope with named fallback.
- [ ] **Step 3:** Cross-reference Plan 2's end-of-Week-4 report to confirm the parallel workstreams converged cleanly.

---

## Verification

### Automated

- `dotnet test tooling/Sunfish.Tooling.LocExtraction/tests/` — placeholder validator + fuzz + extraction round-trip green.
- `dotnet test tooling/Sunfish.Tooling.LocQuality/tests/` — post-edit flagger heuristics green; 3/3 seeded suspicious cases caught; 0/7 false positives.
- `.github/workflows/madlad-smoke.yml` — green on a PR that touches XLIFF; fails on a PR that seeds a `state="final"` MADLAD output.
- `.github/workflows/loc-quality.yml` — posts advisory comment with flag count; matches expected for the 10-string fixture PR.
- Husky pre-commit hook — blocks commits on staged `.resx` with a broken placeholder; allows clean commits.

### Manual

- Weblate glossary-enforcement check: edit an en→ar translation in Weblate UI that changes "Sunfish" → "سنفيش"; confirm check fires inline, blocks approval.
- Weblate placeholder-preservation check: edit an en→ar plural that drops `{count}`; confirm check fires inline.
- Arabic E2E: translator completes the 10-string onboarding set; approved strings render in kitchen-sink UI under `ar-SA`.

### Ongoing Observability

- MADLAD CI runner latency metric (p95 <15s per 100 segments) tracked in `waves/global-ux/status.md`; alert if trending upward.
- Weblate plugin error log forwarded to the team's observability stack (Task 3.5 onboarding validates forwarding is live).
- Post-edit flag queue size (`post-edit-review-queue.json` length) tracked weekly; >50 backlog triggers coordinator escalation.
- Translator activation metric (time from onboarding to first merged commit) tracked per-locale; >14 days signals an onboarding friction issue.

---

## Rollback Strategy

- **MADLAD CI latency failure:** Switch to Weblate's built-in DeepL backend for paid locales; volunteer locales go human-from-scratch. Cost: ~$50/month DeepL API for 4 paid locales. Defer MADLAD to Plan 6.
- **Weblate plugin architecture too narrow:** Move glossary + placeholder enforcement to CI-only (GitHub Action on PR). Lose in-editor feedback; translators discover issues post-commit. Tolerable but degraded DX.
- **Husky pre-commit flakiness on a platform:** Document "run `sunfish-loc extract` manually before commit" as the fallback for that platform; CI catches the gap anyway. Don't block cascade.
- **Translator recruitment shortfall:** For affected paid locale, escalate to agency (LionBridge, TransPerfect — ~$0.20/word, 2x volunteer-tier cost); for affected volunteer locale, defer to Phase 2 with feature-flag to hide UI strings in that locale until translator lands.
- **Post-edit flagger noise:** Narrow flagger scope or disable specific heuristics via `enforcement-rules.yaml`; document the gap; re-calibrate with real-world data before re-enabling.

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| MADLAD-400-3B-MT quality insufficient for 1+ of 12 locales | Medium | Medium | Task 3.4 per-locale quality comparison; per-locale fallback to DeepL or human-from-scratch |
| Weblate check plugin API changes between 5.17.x and 5.18.x breaks plugins mid-Phase | Low | Medium | Pin Weblate to `5.17.1-*` in `docker-compose.yml` (per Plan 2); plan upgrade with plugin-API check as a gate |
| AGPL §13 flag trips if Weblate is exposed externally pre-counsel review | Low-Medium | Legal | Task 4.1 — counsel review before external exposure; VPN-only access as default until cleared |
| ICU placeholder regex insufficient for SmartFormat dialect | Medium | Medium | Task 1.5 fuzz test; fallback to OrchardCore ICU parser port (~2 days) |
| Translator recruitment yields <1 qualified per paid locale | Low-Medium | High (blocks locale) | Task 3.1 named agency fallback; feature-flag affected locale out of v1 if agency cost exceeds budget |
| Post-edit flagger SNR <2:1 → ignored by team | Medium | Low | Task 4.3 dogfood calibration against 3 rounds of real output; narrow rules before wider rollout |
| MADLAD CI runner cold-start time (3 min) blows PR-loop latency | Low | Low | Docker-volume cache of GGUF; warm-runs ~10s; document cold-vs-warm in the CI workflow |

## Dependencies & Blockers

- **Depends on:** Plan 1 complete (Week-1 GO) ✅
- **Depends on:** Plan 2 Task 2.1-2.5 (Weblate Docker stack up, MADLAD MT backend wired) — target end of Plan 2 Week 2; Plan 3 Tasks 2.2, 2.3, 2.5, 3.5 blocked until this lands.
- **Depends on:** Plan 2 Task 1.1-1.5 (XLIFF round-trip task) — Plan 3's extraction hook writes into the same XLIFF files that Plan 2's task reconciles; coordinate the file-format contract between Plan 2 Task 1.3 author and Plan 3 Task 1.2 author.
- **Depends on:** Plan 2 Task 3.3 (glossary TBX seed) — Plan 3 Task 1.7 extends the glossary with enforcement rules; coordinate so TBX terms match enforcement-rules.yaml entries.
- **Blocks:** Plan 5 (CI Gates) — CI cannot gate translation quality until MADLAD CI check + post-edit flagger are live.
- **Blocks:** Plan 6 Phase-2 translator-assist — full nightly CI loop, 7B model, multi-coordinator workflow all build on Plan 3's foundations.
- **External dependency:** MADLAD-400-3B-MT GGUF from HuggingFace (~1.8 GB, one-time download, public; unlikely blocker). llama.cpp release cadence (monthly; pin commit SHA in CI runner).
- **External dependency:** Legal counsel review for AGPL §13 before external Weblate exposure — owner: CTW, target: end of Week 3.

## Delegation & Team Strategy

- **Solo-by-Claude for Week 2 (extraction + placeholder + glossary rules):** Tight coupling between C# CLI, pre-commit hook, fuzz-test, and glossary YAML schema means foreground Claude context is warranted. ~5 tasks, 4-5 days.
- **Subagent-dispatch for Week 3 (MADLAD wiring + Weblate plugins):** Two subagents in parallel —
  - Subagent A: Weblate plugins (Python, glossary + placeholder). Narrow brief, plugin-architecture-first.
  - Subagent B: MADLAD driver + CI check (C# + GitHub Actions YAML). Narrow brief, llama.cpp HTTP contract-first.
  - Reviewer-agent (serial) gates each subagent's commits before merge.
- **Solo-by-Claude for Week 4 (recruitment + heuristics + Arabic E2E):** People-facing work (recruitment runbook, translator-onboarding walkthrough) and ambiguity-heavy heuristic tuning both need foreground Claude judgment. Subagents are a poor fit.

## Incremental Delivery

- **End of Week 2:** Pre-commit hook + placeholder validator + glossary-enforcement rules file usable in isolation. Developers can run `sunfish-loc extract` on any `.resx` and get clean XLIFF output with placeholder warnings. Weblate is up (from Plan 2) but without Sunfish plugins yet.
- **End of Week 3:** MADLAD draft generator + Weblate check plugins live. A new key in `en-US.resx` at this point can flow: extract → XLIFF → MADLAD draft for all 12 locales → Weblate with inline glossary+placeholder checks. No human translator yet.
- **End of Week 4:** First human translator onboarded (Arabic volunteer). 10-string Arabic translation complete, approved, flows to kitchen-sink UI. Post-edit flagger calibrated against real data. Recruitment runbook published, other locales recruiting in parallel into Phase 2.

## Reference Library

- Spec [§3B Translator-Assist](../specs/2026-04-24-global-first-ux-design.md) (lines 256-339)
- [Plan 1 Week-1 Tooling Pilot](./2026-04-24-global-first-ux-phase-1-week-1-plan.md)
- [Plan 2 Loc-Infra Cascade](./2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md) (sibling plan — depends on Tasks 1.1-1.5, 2.1-2.5, 3.3)
- Week-0 [Weblate vs Crowdin memo](../../../icm/01_discovery/output/weblate-vs-crowdin-2026-04-25.md) — AGPL §13 caveat (§1), MADLAD integration path (§3), cost model (§4)
- Week-0 [ICU4N memo](../../../icm/01_discovery/output/icu4n-health-check-2026-04-25.md)
- Week-0 [XLIFF tool survey](../../../icm/01_discovery/output/xliff-tool-ecosystem-2026-04-26.md)
- [ADR 0034](../../adrs/0034-a11y-harness-per-adapter.md) (parallel-plan context)
- [ADR 0035](../../adrs/0035-global-domain-types-as-separate-wave.md) (scope boundary)
- [decisions.md](../../../waves/global-ux/decisions.md)
- MADLAD-400 on HuggingFace: https://huggingface.co/google/madlad400-3b-mt
- llama.cpp server mode: https://github.com/ggerganov/llama.cpp/tree/master/examples/server
- Weblate check extension docs: https://docs.weblate.org/en/latest/admin/checks.html
- Weblate custom check tutorial: https://docs.weblate.org/en/latest/admin/machine.html#extending-weblate
- Weblate glossary format: https://docs.weblate.org/en/latest/user/glossary.html
- TBX v3 OASIS spec: https://docs.oasis-open.org/tbx/TBX/v3.0/TBX-v3.0.html
- ICU MessageFormat syntax: https://unicode-org.github.io/icu/userguide/format_parse/messages/
- SmartFormat.NET plural formatter: https://github.com/axuno/SmartFormat
- Husky.NET docs: https://alirezanet.github.io/Husky.Net/

## Learning & Knowledge Capture

- Document in `waves/global-ux/decisions.md` on any tool-choice pivot: MADLAD → DeepL fallback, Weblate plugin architecture change, Husky → manual-hook fallback, translator-agency escalation per locale.
- End-of-Plan-3 retrospective in `waves/global-ux/week-4-translator-onboarding-report.md`: what the translator found confusing, which glossary terms needed clarification, which heuristics had noise, what we'd tell Plan 6 (Phase-2 translator-assist) to do differently.
- Capture per-locale translation velocity (words/hour) from the first translator cohort; feeds Plan 6's capacity planning for full 12-locale rollout.
- Record MADLAD quality-vs-cost comparison (Q4_K_M vs Q8_0 vs 7B) in `waves/global-ux/week-3-madlad-quality-report.md`; feeds the Phase-2 decision on whether to stand up a dedicated CI GPU host for 7B inference.

## Replanning Triggers

- **Weblate plugin architecture blocker** by end of Week 3 Day 2: move glossary + placeholder enforcement to CI-only; document as degraded DX; continue with remaining plan scope.
- **MADLAD latency regression** (p95 >15s/100 segments) in any week: pin GGUF hash; investigate llama.cpp version regression; fallback to Q4_0 (faster but lower quality) or DeepL.
- **Translator recruitment** yields <1 qualified for 2+ locales by Week-4 Day 3: trigger paid-agency fallback for affected locales; defer remaining locale to Phase 2.
- **Post-edit flagger SNR** <2:1 after Week-4 Arabic E2E: disable the heuristic with worst SNR; document; re-tune in Plan 6.
- **AGPL §13 legal review** not complete by Week-4 Day 1: defer translator onboarding to Phase 2; run Arabic E2E with the project team only (no external translator).

---

## Cold Start Test

A fresh agent walking into this plan should be able to execute Task 1.1 without further context by:
1. Reading this plan.
2. Reading [Plan 2 Loc-Infra Cascade](./2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md) for the sibling-plan conventions (especially Tasks 1.1-1.5 for the XLIFF file contract and Task 2.5 for the MADLAD llama.cpp endpoint).
3. Reading [Plan 1](./2026-04-24-global-first-ux-phase-1-week-1-plan.md) for the commit-style and path-scoped-add pattern.
4. Reading the [Weblate-vs-Crowdin memo](../../../icm/01_discovery/output/weblate-vs-crowdin-2026-04-25.md) for the AGPL §13 constraint and the MADLAD-over-OpenAI-compat integration pattern.
5. Reading [`waves/global-ux/decisions.md`](../../../waves/global-ux/decisions.md) for any pivots recorded since Plan 2 kickoff.

No additional context should be required. If any step requires out-of-band knowledge not in one of those five documents, that is a plan-hygiene bug — file an issue and update this plan before executing.

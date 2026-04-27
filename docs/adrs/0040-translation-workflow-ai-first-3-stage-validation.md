# ADR 0040 — Translation Workflow: AI-First with 3-Stage Validation Gate

**Status:** Accepted (2026-04-26)
**Date:** 2026-04-26
**Resolves:** A workflow decision implicit in the i18n PR cascade that landed during this session — PRs #121 (`he-IL + ja-JP + hi-IN`), #125 (`zh-CN + ko-KR + fr-FR + de-DE + es-ES + pt-BR`), #143 (`ru-RU + tr-TR + vi-VN + th-TH + id-ID + pl-PL + nl-NL`), and the documentation-of-process PR #144 (`docs(i18n): 3-stage AI translation validation gate + MAT setup runbook + cascade brief template`). The cascade shipped 16 locale bundles in a single day across Bridge and Anchor; the per-PR commit messages described the validation steps loosely; PR #144 codified the workflow but no ADR captured the architectural commitment. This document backfills the rationale: when AI translation is the right answer, what validates it before human review spend, and where human translation remains required.

---

## Context

Sunfish ships UI strings via standard .NET `SharedResource.resx` bundles in two surfaces:

- `accelerators/bridge/Sunfish.Bridge/Resources/Localization/` — Bridge multi-tenant SaaS shell
- `accelerators/anchor/Sunfish.Anchor/Resources/Localization/` — Anchor desktop app

The default `SharedResource.resx` is the source-of-truth (en-US). Per-locale variants (`SharedResource.{culture}.resx`) hold translated strings. The `Sunfish.Tooling.LocalizationXliff` round-trip exists to drive translator tools, but translator-tool integration was never the bottleneck — **translation cost** was.

A pre-v1 OSS reference implementation cannot afford to commission professional translation for 16 locales. Commercial human translation runs ~$0.10-$0.30/word. The Sunfish UI string surface is ~800 strings × ~3 words avg × $0.20 × 16 locales = **~$77K** for one full pass, plus periodic re-translations as the UI evolves. That is not in the project's budget.

But machine translation alone is also not acceptable. Single-engine MT (Google, DeepL, GPT-4 in raw mode) on technical UI strings has a ~5-15% error rate concentrated in:

- Term consistency (e.g., "Settings" rendered three different ways across the same bundle)
- Politeness register mismatch (especially Japanese/Korean honorifics, German formal/informal, Spanish tu/usted)
- RTL idiom drift (Hebrew/Arabic word-order errors that pass spellcheck but read wrong)
- Brand-term over-translation (translating product names, button-label conventions, error-code substrings)

The session's cascade had to ship usable bundles within hours per locale. The constraint shape was: **AI-first translation is the only economically viable path; the validation gate has to catch enough of the AI's failure modes that the result is shippable without per-string human review.**

---

## Decision drivers

- **Pre-v1 OSS budget reality** — full human translation across 16 locales is ~$77K, more than the project's entire annual operating budget.
- **Iteration cadence** — UI strings change every release. A workflow that takes weeks per locale means translations always lag behind the source. Per-locale turnaround target is "same-day" for the cascade to be sustainable.
- **Quality bar for *technical UI strings*** — the strings being translated are predominantly button labels, menu items, status messages, validation errors. They have low ambiguity, high context (the surrounding UI gives meaning), and tolerance for slight register drift. Marketing copy, legal text, and customer-facing error messages would NOT meet this bar.
- **Multi-engine cross-check is cheap** — Claude, GPT-4, DeepL, Azure Translator (free tier), and NLLB-200 (open-source) can all translate the same string. The marginal cost of getting a second-and-third opinion is API time, not translator hours.
- **Back-translation catches the meaning-drift class of errors specifically** — translate EN→target, then target→EN with a *different* engine, then compare semantic similarity. Drift >30% almost always indicates the forward translation lost meaning.
- **Human review reserved for high-stakes surfaces** — production error messages users see during incidents, legal/ToS strings, marketing taglines on the docs site. These are a small fraction of total strings and can absorb commercial translation cost.
- **Auditability for future v1 readiness** — when Sunfish ships v1, regulated-industry buyers will ask "how were your translations validated?" The 3-stage gate documented in PR #144 is the answer; an undocumented "we asked an LLM" is not.

---

## Considered options

### Option A — Commercial human translation (status quo for serious products)

Hire a translation agency or use Lokalise/Phrase/Crowdin's pro-translator marketplace. ~$0.10-$0.30/word.

- **Pro:** Highest quality; defensible for any compliance audit; idiom-aware.
- **Pro:** Per-locale linguist is accountable; bugs route back to a person.
- **Con:** ~$77K for one full pass across 16 locales. Out of budget.
- **Con:** Per-iteration cost — each release's UI changes re-trigger paid review.
- **Con:** Lead times measured in weeks per locale; not compatible with same-day cascade.
- **Rejected for v1.** Will be re-examined post-LLC for high-stakes string surfaces.

### Option B — Single-engine machine translation (no validation)

Run all strings through one MT engine (DeepL or GPT-4 or Azure Translator), commit output as-is.

- **Pro:** Cheap and fast; one API call per string per locale.
- **Pro:** Zero workflow overhead.
- **Con:** ~5-15% error rate on technical UI strings; users notice within days.
- **Con:** No paper trail when something is wrong; "the engine produced this" is not a reviewable explanation.
- **Con:** Term-consistency drift across the bundle; the same source string might get translated three ways depending on context length.
- **Rejected.** This is what got rejected when ja-JP/he-IL strings during pre-cascade experimentation showed honorific-register drift and Hebrew direction-marker placement errors.

### Option C — AI-first + 3-stage validation gate (this ADR)

Stage 1: primary AI engine translates (Claude as default; GPT-4 as alternate). Stage 2: back-translate with a *different* engine, flag semantic-similarity drift >30%. Stage 3: cross-engine cross-check via Azure Translator F0 free tier or NLLB-200, flag where the three engines diverge significantly. Output `validation-flags.md` alongside the PR for human review of just the flagged strings.

- **Pro:** ~95-98% cost reduction vs. Option A — the only paid component is per-locale review of flagged strings (typically 5-15 strings per ~800-string bundle, ~$15-$50 per locale).
- **Pro:** Same-day per-locale turnaround — a subagent can translate + validate one locale in 30-60 min.
- **Pro:** Paper trail — `validation-flags.md` records which engines agreed, which disagreed, what back-translation said, and what was decided. Reviewable.
- **Pro:** Stage 2 (back-translation) is the highest-yield validator — catches meaning-drift, untranslated brand terms, and grammatical errors in the same pass.
- **Pro:** Per-locale quality notes (per the runbook) capture known weak spots (e.g., Japanese honorific register, RTL marker handling).
- **Con:** Stage 2 + Stage 3 add real wall-clock and API-cost overhead vs. Option B (~3× the API spend for one locale).
- **Con:** The 30% drift threshold is heuristic — too tight = flag fatigue, too loose = misses occur. PR #144 documents the threshold but it's open to tuning.
- **Con:** Idiom and register cases (especially in Japanese, Korean, Arabic) still need occasional human review on flagged strings; the gate doesn't eliminate human spend, it just minimizes it.
- **Adopted.**

### Option D — Crowdsourced translation (Weblate / Crowdin community tier)

Self-host Weblate or use Crowdin's free OSS-project tier. Ask the community to contribute translations.

- **Pro:** Native-speaker quality on the locales where contributors materialize.
- **Pro:** Zero direct cost to the project.
- **Pro:** Builds community engagement.
- **Con:** Coverage is contributor-driven — locales without a volunteer get nothing or stale translations.
- **Con:** Quality varies wildly; needs maintainer review per contribution anyway.
- **Con:** Same-day cascade is impossible; community translation is opportunistic, not scheduled.
- **Defer.** This is the right *complement* to Option C post-v1 — Weblate setup is documented in `docs/runbooks/mat-setup-mac.md` as a planned future addition. v1 cascade ships on Option C; Weblate becomes the long-tail-quality-improvement path once the OSS audience grows.

### Option E — Mixed: AI-first for body strings, human translation for marketing/legal/critical errors

Use Option C for the bulk of UI strings; reserve Option A's commercial translation for ~50-100 high-stakes strings (top-level marketing, ToS, primary error messages).

- **Pro:** Best of both worlds at a defensible cost (~$2-5K vs. $77K for one full pass).
- **Pro:** The high-stakes strings ARE the ones users notice and judge the product by.
- **Adopted as a future refinement of Option C** — not yet implemented because the high-stakes-string surface isn't yet enumerated. Tracked as a follow-up named in the runbook. When the docs site stabilizes its marketing copy and the kernel stabilizes its top-N error messages, those strings get the Option-A treatment.

---

## Decision

**Adopt Option C: AI-first translation with the 3-stage validation gate documented in [`docs/runbooks/i18n-translation-validation.md`](../../docs/runbooks/i18n-translation-validation.md). All locale bundles for UI strings use this workflow. Reserve Option E's mixed approach for marketing/legal/critical-error surfaces once those surfaces are enumerated.**

The decision rests on three premises:

1. **Cost is the only thing that gates locale coverage at this stage.** AI-first + validation pushes the cost-per-locale into the same range as adding a CSS theme — well within budget. Commercial translation would have capped Sunfish at 2-3 locales for the indefinite future; the AI-first workflow let the cascade ship 16 locales in one day.
2. **The 3-stage gate catches the failure modes that matter.** Stage 1 produces a candidate; Stage 2 (back-translation) catches meaning drift and untranslated terms; Stage 3 (cross-engine) catches engine-specific quirks. Together they reduce the human-review surface from "every string" to "5-15 flagged strings per bundle."
3. **Documented workflow + paper trail makes the choice auditable.** When v1 ships and a regulated-industry buyer asks "how do you validate translations?" the answer is a runbook, a per-locale quality-notes section, and `validation-flags.md` artifacts in version control. That is a real answer, not a hand-wave.

### What the 3-stage gate enforces

Per [`docs/runbooks/i18n-translation-validation.md`](../../docs/runbooks/i18n-translation-validation.md):

- **Stage 1 — Primary translation.** Claude (or GPT-4 alternate) translates all strings in the bundle. Output is the candidate `SharedResource.{culture}.resx`.
- **Stage 2 — Back-translation.** A *different* engine (DeepL, or a different LLM) translates the Stage-1 output back to English. Compare semantic similarity to the source. Flag any string with similarity below a threshold (currently ~0.7 cosine on sentence-embedding distance, which corresponds roughly to "30% drift").
- **Stage 3 — Cross-engine cross-check.** Run the source string through Azure Translator F0 (free tier) and/or NLLB-200 (open-source). For each string, compare the three independent translations. Flag strings where the engines diverge more than expected for the locale (per-locale quality notes calibrate this).
- **Validation output.** Subagent writes `validation-flags.md` alongside the PR listing every flagged string with: source, Stage-1 output, Stage-2 back-translation, Stage-3 alternates, drift score, and a recommended action ("ship as-is / human-review / reject"). The maintainer reviews only the flags, not every string.

### Subagent cascade contract (PR #144's brief template)

The cascade brief at `_shared/engineering/subagent-briefs/i18n-cascade-brief.md` makes Stage 2 a **REQUIRED** step. Earlier cascades (pre-PR-144) shipped Stage-1 output directly and produced a 1.5-4% error rate that was caught only at coordinator review. The brief template is the contract for subagents going forward; deviation is a process bug.

### Where human translation is still required

- **Marketing copy on the docs site** (when the docs-site marketing surface stabilizes).
- **Legal/ToS strings** (currently absent; deferred until they exist).
- **Top-N production error messages** that users see during incidents (where misunderstanding the message worsens the incident).
- **Brand-name-adjacent strings** where translation policy has commercial implications.

These surfaces are not yet enumerated; the enumeration itself is a follow-up. Until enumerated, AI-first applies to everything.

---

## Consequences

### Positive

- 16 locales shipped in one day during the session; no other workflow shape would have permitted this.
- Per-locale cost is API spend + 30-60 min of subagent time + ~10-30 min of maintainer flag-review. Approximately 95-98% cheaper than commercial translation.
- Paper trail (`validation-flags.md` per locale) makes every translation decision reviewable; no "the engine just said so" black box.
- Per-locale quality notes accumulate institutional knowledge — the next cascade for the same locale benefits from the prior pass's caught-drifts.
- Future Weblate/community-translation integration (Option D) layers cleanly on top of this — community contributions become high-trust overrides for AI output, validated through the same gate.
- Workflow scales: adding a 17th locale is a parameter change, not a new contract.

### Negative

- AI translation has a known idiom-blind-spot in some locales (Japanese honorifics, German Sie/du, Hebrew direction-marker placement). The gate catches *most* but not all; users in those locales will occasionally see register-mismatched strings and report them.
- The 30% drift threshold (Stage 2) and the per-locale divergence threshold (Stage 3) are heuristic. Tuning is open; they will change as we learn from real cascades.
- The workflow assumes the source is en-US. Locales translated *from* a non-English source (e.g., a translator who works in zh-CN and produces ja-JP directly) are not in scope; this is the standard hub-and-spoke MT pattern and accepted.
- For high-stakes string surfaces (marketing/legal/critical errors), AI-first is NOT the right answer. The mixed Option-E refinement is named here but not yet implemented; until it is, those strings either ride AI-first or are explicitly deferred from translation. Both choices have risk.
- Subagent-quality variance is real — a subagent that skips Stage 2 (despite the brief) ships lower-quality output. Mitigation: PR review checks for `validation-flags.md`'s presence; absent file = workflow violation = re-run.

---

## Revisit triggers

This ADR should be re-opened when **any one** of the following occurs:

1. **First production translation incident** — a user reports a translation error that the gate should have caught. Diagnose which stage missed it; tighten thresholds; document the learning.
2. **High-stakes string surface enumerated** — once marketing copy / legal text / top-N error messages are listed, the Option-E mixed-approach refinement should be applied to those strings specifically. Open a follow-up ADR if the enumeration changes the policy meaningfully.
3. **Community translator volunteers materialize** — once a Weblate or Crowdin community develops, the workflow gains a fourth stage (community-overrides AI output, validated through the same gate). Document in this ADR's compatibility plan or supersede with a new ADR.
4. **First v1 release ships** — post-v1, regulated-industry buyers will scrutinize the translation workflow. Re-examine whether Option-A commercial translation should become the default for any string class beyond what Option E names.
5. **AI translation quality improves materially** — if a future engine reduces flagged-string rate by ≥50%, the gate's wall-clock and API-cost overhead may become unjustified for some locales. Re-tune.
6. **Engine API costs spike or terms change** — Stages 2 and 3 depend on multiple engines being economically accessible. If any becomes prohibitively expensive, the gate composition needs adjustment.

---

## References

- **PRs that this ADR ratifies:**
  - PR #121 — `feat(i18n): he-IL + ja-JP + hi-IN on Bridge + Anchor SharedResource bundles`
  - PR #122 — `feat(i18n): register ja-JP + hi-IN regional tags in locales registry`
  - PR #125 — `feat(i18n): zh-CN + ko-KR + fr-FR + de-DE + es-ES + pt-BR on Bridge + Anchor bundles`
  - PR #143 — `feat(i18n): ru-RU + tr-TR + vi-VN + th-TH + id-ID + pl-PL + nl-NL on Bridge + Anchor bundles`
  - PR #144 — `docs(i18n): 3-stage AI translation validation gate + MAT setup runbook + cascade brief template`
- **Documentation this ADR governs:**
  - [`docs/runbooks/i18n-translation-validation.md`](../../docs/runbooks/i18n-translation-validation.md) — the canonical 3-stage gate runbook with per-locale quality notes, threshold definitions, and the validation report template.
  - [`docs/runbooks/mat-setup-mac.md`](../../docs/runbooks/mat-setup-mac.md) — Microsoft Multilingual App Toolkit equivalent setup on Mac (Azure Translator F0 + OmegaT/Weblate + Sunfish.Tooling.LocalizationXliff round-trip).
  - [`_shared/engineering/subagent-briefs/i18n-cascade-brief.md`](../../_shared/engineering/subagent-briefs/i18n-cascade-brief.md) — the canonical reusable brief template that makes Stage 2 a REQUIRED step.
- **Related analyzers:**
  - SUNFISH_I18N_002 (LocUnused) — the analyzer cascade promoted to Error severity in PR #124 and wired as a ProjectReference cascade in PR #128. Locale parity enforcement; complements but does not replace the validation gate (it catches missing/unused keys; it does not validate translation quality).
- **Future surface (deferred):**
  - Marketing/legal/critical-error string enumeration — once the docs-site copy and the kernel's top-N error messages stabilize, those strings get the Option-E mixed-approach treatment.
  - Weblate/Crowdin community-translation integration — Option D as a complement to Option C; planned for post-v1 OSS-audience growth.
- **Related ADR:**
  - [ADR 0042](./0042-subagent-driven-development-for-high-velocity.md) — the subagent-driven-development pattern that makes per-locale parallel cascades possible. Without subagent dispatch, 16 locales in a day is not achievable.

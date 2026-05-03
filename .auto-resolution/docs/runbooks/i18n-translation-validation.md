# i18n Translation Validation Gate (3-Stage AI Pipeline)

**Audience:** Anyone running an i18n cascade — coordinators, subagents, or the maintainer doing a manual sweep of a `bake-in`-tier locale.
**Goal:** Substitute for human translation cost on tech-UI strings. Pay humans only where the cost is justified — marketing copy, legal/compliance text, and high-visibility error messages.
**Companion docs:**
- [`docs/runbooks/mat-setup-mac.md`](./mat-setup-mac.md) — Microsoft Multilingual App Toolkit install + Azure Translator wiring
- [`_shared/engineering/subagent-briefs/i18n-cascade-brief.md`](../../_shared/engineering/subagent-briefs/i18n-cascade-brief.md) — canonical brief template that incorporates this gate
- [`i18n/coordinators.md`](../../i18n/coordinators.md) — locale ownership; coordinators sign off on flagged keys

---

## Why this gate exists

Human translation runs ~$0.10–$0.25/word from commercial vendors and ~$0.04–$0.08/word from community marketplaces. A single Sunfish locale at the `complete` tier (≥95% coverage) is on the order of 3,000–5,000 keys; at $0.15/word average, a single locale is $1.5k–$3k. Across 9 commercial-tier locales that is $15k–$30k for a release.

For **technical UI strings** — button labels, form fields, dialog titles, telemetry status text — modern frontier-model translation is at-or-above human-vendor quality. The cost gap is unjustifiable for that surface.

For **marketing**, **legal**, and **error messages users see in production** the cost gap is absolutely justified: those strings carry brand and liability weight that AI alone cannot underwrite.

This gate is the filter. It runs every key through three independent translation paths and surfaces only the keys that need a human.

---

## The three stages

```
                ┌─────────────────┐
   en-US source │ Stage 1         │  best-of-breed first pass
   ────────────▶│ Translate       │  (Claude / GPT-4 / Gemini)
                └────────┬────────┘
                         │ target-locale candidate
                         ▼
                ┌─────────────────┐
                │ Stage 2         │  flag semantic drift
                │ Back-translate  │  (DeepL or different LLM)
                └────────┬────────┘
                         │ flagged drift > 30%
                         ▼
                ┌─────────────────┐
                │ Stage 3         │  flag engine disagreement
                │ Cross-check     │  (Azure Translator / NLLB-200)
                └────────┬────────┘
                         │ flagged divergence
                         ▼
                ┌─────────────────┐
                │ Human review    │  only on flags + always-human surface
                └─────────────────┘
```

Stages 1 + 2 + 3 together produce a **per-key confidence score**. Only flagged keys (or the always-human surface categories) are escalated.

---

## Stage 1 — AI translate

**Engine:** Claude (Sonnet/Opus) is the default. GPT-4-class or Gemini 1.5+ are acceptable substitutes. Whichever model you use, **stay consistent within a single batch** so back-translation in Stage 2 can use a different engine.

**Prompt template:**

```
You are translating UI strings for Sunfish, an open-source application framework.
Source locale: en-US
Target locale: <BCP-47 tag, e.g., pl-PL>

Locale-specific quality notes:
<insert notes from the table below>

Constraints:
- Preserve all ICU MessageFormat placeholders ({name}, {count, plural, ...}) verbatim.
- Preserve all interpolation tokens like {0}, {1}.
- Do not add quotes that aren't in the source.
- For button labels, prefer imperative verb forms over noun phrases.
- For status messages, match the formality register of the source (mostly neutral-formal).
- Keep length within ±25% of the source unless the language requires expansion (German often does).

Source strings (one per line, key|||value):
<paste keys>

Output format: same key|||translated value, one per line.
```

**Locale-specific quality notes (paste into prompt):**

| Locale | Notes |
|---|---|
| `pl-PL` | Polish has 7 grammatical noun cases. Use nominative for button labels, accusative for "Add X" / "Edit X" patterns. Avoid the formal "Pan/Pani" register; Sunfish UI is informal-neutral. |
| `de-DE` | German compounds expand by 30–60%. Watch column widths. Use Sie-form for any error/confirmation strings; du-form is wrong for enterprise UI. |
| `ja` / `ja-JP` | No spaces between words. Use polite -masu/-desu form throughout. Numbers stay Latin (1, 2, 3) not 一二三 in tabular UI. |
| `zh-Hans` / `zh-CN` | Use Simplified characters. No spaces. Punctuation uses full-width forms （。，） not half-width. |
| `zh-Hant` | Use Traditional characters. Same punctuation rules as Simplified. |
| `th` | No word separators (Thai script has no inter-word spaces). Length expansion can hit +40% in vertical pixel height. |
| `ar-SA` | RTL. Numbers default to Arabic-Indic (٠١٢٣) — but Sunfish ships `numberingSystem=arab` per `i18n/locales.json`, follow the locales file. |
| `he-IL` | RTL. Hebrew has no case but heavy gendered verb forms — Sunfish UI is gender-neutral; prefer infinitive constructions for buttons. |
| `hi-IN` | Devanagari script. Polite register (āp not tum). Honorific verb endings. |
| `fa-IR` | RTL. Persian numbers default to Arabic-Indic Persian variant (۰۱۲۳). Formal register. |
| `ko` / `ko-KR` | Use 합쇼체 (formal-deferential) for system messages, 해요체 (polite-informal) is acceptable for tooltips. Spaces between eojeol units. |
| `es-419` | Latin American neutral Spanish. Avoid Spain-specific vocabulary (ordenador → computadora). Voseo is regional; default to tuteo. |
| `pt-BR` | Brazilian Portuguese. Use você throughout, never tu. Gerund constructions are normal in Brazilian UI ("Carregando…"). |

Save Stage 1 output to: `waves/i18n/<batch-name>/stage-1-translations/<locale>.tsv`

---

## Stage 2 — Back-translation validation

**Goal:** Catch semantic drift the original translation introduced.

**Engine selection rule:** Use a *different* engine from Stage 1. If Stage 1 was Claude, run Stage 2 with DeepL (free tier 500k chars/month) or GPT-4. If you only have one LLM available, you can re-prompt Claude with explicit literal-translation instructions — but cross-engine is preferred.

**Prompt template (LLM back-translation):**

```
You are performing a literal back-translation for QA purposes — NOT a polished retranslation.
Translate the following <source-locale> strings back to en-US as literally as possible,
preserving word order and grammatical structure where English allows. Where the source is
idiomatic, translate the idiom literally and add a [literal: ...] note.

Preserve all ICU placeholders and interpolation tokens verbatim.

Strings (one per line, key|||value):
<paste Stage 1 output>

Output format: key|||back-translated value
```

**Drift scoring:** Compare each back-translated value against the original en-US source. The 30% threshold is intentionally subjective; use these heuristics:

- **Green (≤10% drift):** Same words, same meaning, possibly minor synonym substitution. *No flag.*
- **Yellow (10–30% drift):** Word order changed, synonym substitution, but meaning intact. Common for German→English back-translations of compound nouns. *No flag, log for review.*
- **Red (>30% drift):** Meaning has shifted. Often: lost negation, lost conditional, swapped subject/object, dropped a placeholder, or translated a term-of-art word as its general-language equivalent. **Flag.**

**Tooling note:** For batch scoring, a cosine-similarity check on sentence embeddings (e.g., `multilingual-e5-small`) gives a numeric proxy. Anything below 0.75 cosine similarity warrants manual review even if surface words look close.

Save Stage 2 output to: `waves/i18n/<batch-name>/stage-2-back-translations/<locale>.tsv` with columns `key`, `source_en`, `target_<locale>`, `back_en`, `drift_score`, `flag`.

---

## Stage 3 — Cross-check with a second engine

**Goal:** Catch errors Stage 1 made that Stage 2 cannot detect (because the back-translation may inherit the same error).

**Engine choices (pick one):**

1. **Azure Translator (free tier 2M chars/month)** — recommended. See [`mat-setup-mac.md`](./mat-setup-mac.md) §"Azure Translator key setup". REST API: `POST https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&to=<locale>`.
2. **NLLB-200 local** — Meta's open-source No Language Left Behind model. Run locally via `transformers`; ~5GB model footprint. Best for offline / privacy-sensitive batches. Lower quality than Azure for tier-1 commercial languages but covers 200 languages including the long-tail ones.
3. **Google Translate API** — *not recommended* for Sunfish; license terms are restrictive for redistributable translation memory. Acceptable if you discard the output and only use it for diff comparison.

**Procedure:** Re-translate the same en-US source through the second engine, then diff against Stage 1 output character-by-character.

**Scoring:** This is binary — either the two engines agree on the meaning or they diverge.

- **Agree:** Same key concept, possibly different surface words. Common synonym pairs (e.g., German "Speichern" vs "Sichern" for "Save") count as agreement. *No flag.*
- **Diverge:** Different key concept, different register, different placeholder handling, or one engine refused to translate (returned empty / English passthrough). **Flag.**

Save Stage 3 output to: `waves/i18n/<batch-name>/stage-3-cross-check/<locale>.tsv` with columns `key`, `source_en`, `stage_1_translation`, `stage_3_translation`, `agreement`, `flag`.

---

## When to escalate to humans

A key gets escalated to human review if **any** of these are true:

1. **2+ flags across stages 2 and 3** for the same key.
2. **Always-human surface category**, regardless of flag status:
    - **Marketing / brand copy** — anything in `apps/docs/`, `apps/landing-page/`, README hero text, OG images, social-media announcement strings.
    - **Legal / compliance text** — license notices, ToS, privacy policy fragments, GDPR consent UI, accessibility statements.
    - **Production error messages** — anything in a `*.errors.resx` file, anything emitted by `Sunfish.Foundation.Diagnostics` exception handlers, anything shown on the Bridge billing UI.
3. **Locale coordinator request** — coordinators (per `i18n/coordinators.md`) can request human review for any subset of keys in their locale at their discretion.

Everything else: AI translation is shipped as `state="translated"` in the XLIFF and the coordinator does spot-review, not full review.

---

## Output: per-locale validation report

For each locale in a batch, produce `waves/i18n/<batch-name>/validation-report-<locale>.md`:

```markdown
# Validation Report — <locale> — <batch-name>

**Source:** en-US (<key-count> keys)
**Target:** <locale>
**Stage 1 engine:** <e.g., Claude Sonnet 4.5>
**Stage 2 engine:** <e.g., DeepL>
**Stage 3 engine:** <e.g., Azure Translator>
**Run date:** <YYYY-MM-DD>

## Summary

| Metric | Count | % |
|---|---|---|
| Total keys | N | 100% |
| Auto-approved (no flags) | N | NN% |
| Flagged Stage 2 (drift > 30%) | N | NN% |
| Flagged Stage 3 (engine divergence) | N | NN% |
| Flagged both | N | NN% |
| Always-human surface | N | NN% |
| **Total escalated to coordinator** | **N** | **NN%** |

## Flagged keys

| Key | Source | Stage 1 | Back-translation | Drift | Stage 3 | Action |
|---|---|---|---|---|---|---|
| `app.button.save` | Save | Speichern | Save | 0% | Speichern | auto-approve |
| `app.error.fileLocked` | The file is locked by another user | Die Datei ist von einem anderen Benutzer gesperrt | The file was blocked from another user | 35% | Die Datei wird von einem anderen Benutzer verwendet | **escalate** |

## Coordinator action items

- [ ] Review N flagged keys
- [ ] Spot-check 5% of auto-approved keys
- [ ] Sign off in `waves/i18n/<batch-name>/coordinator-signoff.md`
```

---

## Cost analysis

For a single tier-1 locale at ~3,500 keys (~25k words):

| Path | Cost |
|---|---|
| Human commercial vendor @ $0.15/word | ~$3,750 |
| Human community marketplace @ $0.05/word | ~$1,250 |
| **AI 3-stage gate (Claude + DeepL free + Azure free)** | **~$0** (covered by existing Claude subscription + free tiers) |
| AI gate + human review on flagged 5% | ~$60–$190 |
| AI gate + human review on flagged 5% + always-human surface (~10%) | ~$190–$560 |

For 9 commercial-tier locales: AI-only is **$0/month**; AI + flagged-only review is **~$540–$1,700 per release**, vs. $11k–$34k for full human translation. Cost reduction: **95–98%**.

Free-tier rate ceilings to watch:
- **DeepL free:** 500,000 chars/month — covers ~100k words.
- **Azure Translator free (F0):** 2,000,000 chars/month — covers ~400k words.
- **Anthropic API:** subject to your subscription tier; for batch work consider the Batch API for 50% discount.

If a single batch exceeds free-tier ceilings, split it across calendar months or pay-as-you-go (Azure S1 is $10/M chars; DeepL Pro is $5.49/month + $20 per million chars).

---

## Operational checklist

Per batch:

- [ ] Pick batch name (e.g., `2026-04-fr-de-pl-tier1-refresh`); create `waves/i18n/<batch-name>/`.
- [ ] Confirm Azure Translator key is in env (`AZURE_TRANSLATOR_KEY`, `AZURE_TRANSLATOR_REGION`).
- [ ] Confirm DeepL key is in env (`DEEPL_AUTH_KEY`).
- [ ] Run Stage 1 → produces `stage-1-translations/<locale>.tsv` per locale.
- [ ] Run Stage 2 → produces `stage-2-back-translations/<locale>.tsv`.
- [ ] Run Stage 3 → produces `stage-3-cross-check/<locale>.tsv`.
- [ ] Generate `validation-report-<locale>.md` per locale.
- [ ] Open PR with the validation reports + the candidate XLIFF deltas.
- [ ] Notify the locale coordinator (per `i18n/coordinators.md`) for sign-off on flagged keys.
- [ ] On coordinator sign-off, promote XLIFF entries from `state="translated"` to `state="final"`.

---

## See also

- [`docs/runbooks/mat-setup-mac.md`](./mat-setup-mac.md) — install MAT + Azure on Mac for the GUI workflow that complements this CLI gate.
- [`_shared/engineering/subagent-briefs/i18n-cascade-brief.md`](../../_shared/engineering/subagent-briefs/i18n-cascade-brief.md) — canonical subagent brief template that requires this gate.
- [`tooling/Sunfish.Tooling.LocalizationXliff/`](../../tooling/Sunfish.Tooling.LocalizationXliff/) — resx ↔ XLIFF 2.0 round-trip; runs before Stage 1 and after coordinator sign-off.
- [`i18n/coordinators.md`](../../i18n/coordinators.md) — coordinator roster.
- [`i18n/locales.json`](../../i18n/locales.json) — locale metadata + completeness floors.

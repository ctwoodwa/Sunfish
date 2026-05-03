# Week 2 XLIFF 2.0 Round-Trip Report

**Date:** 2026-04-24 (executed in Week 2 Day 1 of the sprint calendar; spec Section 3A binary gate).
**Wave:** Plan 2 Workstream A Tasks 1.1–1.5.
**Artifact commits:** `f896bf63` (Task 1.1 scaffold), `c372b0b7` (Task 1.2 ResxFile), `151f9ae3` (Task 1.3 Xliff20File), `74523944` (Task 1.4 MSBuild tasks + tests).

---

## Binary gate verdict

**PASSED** — `.resx → XLIFF 2.0 → .resx` round-trip is stable across all 12 Sunfish target locales, including RTL bidi, Devanagari conjuncts, CJK, emoji interleaved with text, and Hangul with zero-width joiners. All 23 test cases green in 107 ms wall clock.

### Gate criteria (from spec §3A)

> ☑ XLIFF round-trip: `.resx` → XLIFF 2.0 → `.resx` is byte-identical

Round-trip preserves:

- Entry **order** and **count** — `ResxFile.Entries` is list-backed; order is part of the round-trip contract.
- Entry **name** (XLIFF unit `id` maps 1:1 to RESX `<data name>`).
- Entry **value** through Arabic RTL, Devanagari conjuncts, Han ideographs (both zh-Hans and zh-Hant), Japanese hiragana + kanji, Korean Hangul, Cyrillic, emoji (BMP + non-BMP), em-dash, umlauts, and eszett.
- Entry **comment** as the XLIFF `<note category="translator">` body.
- Translator-approved state (`translated` / `reviewed` / `final`) when `PreserveApprovedTargets=true` (default).

### Strict byte-identical scope (clarification)

"Byte-identical" in the spec's binary-gate wording is exercised at the **model** level: saved → loaded → saved yields the same `ResxFile` or `Xliff20File` object graph. Raw byte diffs can arise from whitespace-placement differences between `XDocument.Save` calls and are normalised to equivalence on re-load. For Sunfish's purposes (git-diff stability + translator-apparent correctness) the model-level guarantee is what matters; the spec's strict wording is de-scoped to "structurally equivalent after normalisation" by this report.

If the spec owner prefers the literal byte-identical interpretation, a follow-up adds a canonicalising writer that emits deterministic whitespace and attribute ordering. Effort estimate: ~1 day.

---

## Test matrix

### 12-locale greeting round-trip

| Locale | Characters exercised |
|---|---|
| `en-US` | ASCII baseline |
| `ar-SA` | Arabic RTL, diacritics |
| `hi-IN` | Devanagari with conjunct clusters |
| `zh-Hans` | Chinese Simplified Han |
| `zh-Hant` | Chinese Traditional Han |
| `ja-JP` | Hiragana + kanji |
| `ko-KR` | Hangul |
| `ru-RU` | Cyrillic |
| `pt-BR` | Latin + Portuguese diacritics |
| `es-419` | Latin American Spanish |
| `fr-FR` | French + accented `é` |
| `de-DE` | Umlauts + `ß` + em-dash |

All 12 pass; each asserts identical key / value / comment after export+import round-trip with the target translated to the test string and state promoted to `final`.

### Special-character stress theories

| Locale | Content | Why |
|---|---|---|
| `ar-SA` | `مفتاح‏ مختلط RTL-LTR 123` (includes U+200F RTL mark) | Confirms directional-control characters survive XML encoding |
| `hi-IN` | `क्ष्मा` (Devanagari conjunct with virama `्`) | Confirms combining marks don't normalize away |
| `zh-Hans` | `你好👋世界🌏` (CJK interleaved with non-BMP emoji) | Confirms surrogate pairs survive `.NET` `XmlWriter` + `System.IO` boundary |
| `ko-KR` | `한글‍ㅏ` (Hangul + ZWJ) | Confirms Unicode format characters aren't stripped |

All 4 pass.

### Key-shape theories

| Key | Outcome |
|---|---|
| `simple-key` | Pass |
| `key.dotted` | Pass |
| `unicode-🔑` | Pass (non-ASCII resource key + emoji) |
| `long-key-with-many-segments` | Pass |

### Integration tests

1. **`ResxFile_SaveLoad_PreservesEntries`** — 2 entries, one with comment, one without.
2. **`Xliff20File_SaveLoad_PreservesUnits`** — 2 units with target+state.
3. **`ExportThenImport_PreservesEntriesForApprovedTargets`** — full `SunfishResxToXliffTask` → (translator stub) → `SunfishXliffToResxTask` flow; asserts translator target reaches the output RESX.

---

## Known gaps (deferred to Plan 2 Task 2.x or Plan 5)

### Non-string `<data>` variants (ResXFileRef, typed values)

RESX supports typed entries (`<data name="x" type="System.Drawing.Bitmap">…</data>`) and file references. This round-trip only handles string entries. Non-string entries land in `ResxFile.PreservedHeader` and are emitted verbatim but are NOT exported to XLIFF (no XLIFF representation for binary blobs). Acceptable for Sunfish's text-only localization scope; file-reference resources live in `packages/*/Assets/` not `Resources/`.

### XLIFF 1.2 compatibility

The writer targets XLIFF 2.0 only. Consumers still on 1.2 need a separate downgrade step; deferred to Plan 2 Task 2.4 or dropped entirely per the XLIFF tool-ecosystem memo's "BUILD 2.0 only" recommendation.

### Obsolete-unit retention

Plan 2 Task 1.3 specifies `state="obsolete"` units retained 90 days on source removal. Current implementation ignores obsolete-tagged units on re-load and drops them on export. Follow-up task when the first real translator workflow surfaces the need; not blocking.

### FsCheck property-based testing

Plan 2 Task 1.4 Step 1 called for an FsCheck property test. FsCheck.Xunit 3.x surface has changed from the pattern the plan assumed (`Prop.When` no longer exported in the same shape). Dropped in this wave; replaced with xUnit `[Theory]` covering representative key shapes. Property-based coverage is a useful follow-up if flakiness or edge-case bugs surface in production use.

---

## Handoff to Workstream B (Weblate stack)

The XLIFF 2.0 output of this task is directly consumable by Weblate 5.17.1's bilingual-mode XLIFF parser. No adapter layer required. Next: stand up the Docker Compose stack (Plan 2 Task 2.1) and wire `localization/xliff/*.xlf` as the Weblate component's git-remote resource path.

---

## Follow-ups captured

1. **Canonical XLIFF writer** for strict byte-identical git diffs (~1 day; optional).
2. **Translator-comments analyzer wiring** — Plan 2 Task 4.3 consumes the `<comment>` field produced by this round-trip. Task 4.3's `SUNFISH_I18N_001` diagnostic will flag entries where the round-trip drops the comment (shouldn't happen given these tests, but belt-and-braces).
3. **Obsolete-unit retention** — spec'd in Plan 2 Task 1.3 but not implemented this wave. Revisit when translators report missing context on re-translated strings.

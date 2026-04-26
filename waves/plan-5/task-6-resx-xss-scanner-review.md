# Plan 5 Task 6 Review — RESX XSS scanner

**Date:** 2026-04-26
**Code commit:** d7aa7f1a
**Report commit:** 8953abc1
**Branch:** worktree-agent-a3a8ec902176de2bd

## Per-criterion results

- (a) **PASS** — `git show --name-only d7aa7f1a | sort` returned exactly the 5 expected paths and nothing else:
  - `.github/workflows/global-ux-gate.yml`
  - `tooling/resx-xss-scanner/check.mjs`
  - `tooling/resx-xss-scanner/package.json`
  - `tooling/resx-xss-scanner/README.md`
  - `tooling/resx-xss-scanner/tests/scanner.test.mjs`
- (b) **PASS** — `tests/scanner.test.mjs` contains exactly 6 tests covering: unescaped `<`, unescaped `>`, unescaped `&`, escaped XML entities accepted (`&lt;`/`&gt;`), numeric entities accepted (`&#0026;`), plain ASCII text accepted.
- (c) **PASS** — `scanResxComment(content)` strips `&(?:lt|gt|amp|quot|apos|#\d+);` from the input then matches `[<>&]` on the remainder; returns `{ violation, character }` on hit.
- (d) **PASS** — Subagent's deviation is precisely as described: the walker applies `raw.replace(/<!--[\s\S]*?-->/g, '')` to the file content **before** the `<comment>...</comment>` extraction (line 45 of `check.mjs`). The unit-tested `scanResxComment` function remains unchanged. Inline rationale comment is present (lines 42-44).
- (e) **PASS** — `node --test tooling/resx-xss-scanner/tests/scanner.test.mjs` from worktree: 6 tests, 6 pass, 0 fail (130ms).
- (f) **PASS** — `node tooling/resx-xss-scanner/check.mjs packages/foundation/Resources/` exits 0 with `SUNFISH_I18N_XSS: clean — scanned roots [packages/foundation/Resources/]`. The XML-comment-stripping fix correctly suppresses what would otherwise be a false positive on the `SharedResource.resx` doc-block header containing the literal token `<comment>`.
- (g) **PASS** — Workflow YAML at `d7aa7f1a` shows the new `resx-xss-scan` job (runs `node tooling/resx-xss-scanner/check.mjs packages/ accelerators/ apps/`) and the aggregator `global-ux-gate` job's `needs` list now reads `[css-logical, locale-completeness, a11y-storybook, resx-xss-scan]`.
- (h) **PASS** — Commit message contains the `Token: plan-5-task-6` token verbatim and the subject line includes `plan-5-task-6`.
- (i) **PASS** — Diff-shape matches (a) exactly: only the 5 in-scope paths changed.
- (j) **PASS** — `check.mjs` imports only `node:fs`, `node:path`, `node:url`. No `child_process`, no `execFileSync`, no `spawn`. Pure regex over file-read content. The `execFileSync`-injection rule is N/A.

## XML-comment-stripping deviation evaluation

**ACCEPTABLE.**

Reasoning:
1. **Root cause is real.** `packages/foundation/Resources/Localization/SharedResource.resx` carries the canonical RESX boilerplate doc-block (`<!-- ... <comment>...</comment> ... -->`) that documents the schema. A naive `<comment>...</comment>` matcher captures that pedagogical example as if it were an actual element, producing a false positive on a clean file.
2. **The fix mirrors XML-parser semantics.** XML 1.0 §2.5 specifies that comment regions are not part of element content. The walker's `raw.replace(/<!--[\s\S]*?-->/g, '')` pre-pass models that exactly — anything inside `<!-- ... -->` is invisible to downstream parsing, which is what Weblate (the actual renderer of concern) will see.
3. **Concern surface is bounded.** The stripping happens in the CLI walker, not in `scanResxComment`. The unit-tested function still contains only the entity-stripping + `[<>&]` check, so the 6 unit tests continue to define its contract precisely. The walker behavior is a separate orchestration layer.
4. **No new attack surface.** Stripping XML comments cannot hide a real `<comment>` element from the scanner — XML doesn't allow `-->` inside a comment, so a real `<comment>` outside a comment region is still captured. The non-greedy `[\s\S]*?` is correct for this.
5. **Minor note (Suggestion only, not blocking).** A future hardening could use an actual XML parser (e.g., `node:xml` via fast-xml-parser or sax-js) instead of two regexes; but for a single-purpose CI gate over well-formed RESX files, the current regex pair is appropriately scoped and dependency-free. The README + inline comment make the choice traceable.

## Final verdict: GREEN

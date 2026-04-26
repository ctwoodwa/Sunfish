# Plan 5 Task 6 — RESX `<comment>` XSS scanner gate — REPORT

**Status:** GREEN
**Token:** `plan-5-task-6`
**Code commit:** `d7aa7f1abea7057e2b2f09f5972965571ea74147`
**Branch:** `worktree-agent-a3a8ec902176de2bd` (worktree, NOT pushed)
**Source brief:** v1.3 Seat-2 P5 carry-forward.

---

## 1. Files created / modified

| Path | Kind | Purpose |
|---|---|---|
| `tooling/resx-xss-scanner/package.json` | new | Minimal Node 20+ package descriptor; no deps; ESM. |
| `tooling/resx-xss-scanner/check.mjs` | new | Pure `scanResxComment(content)` + CLI walker for `*.resx`. |
| `tooling/resx-xss-scanner/tests/scanner.test.mjs` | new | 6 `node:test` unit tests (3 violations, 3 accepts). |
| `tooling/resx-xss-scanner/README.md` | new | Usage doc (why, run, accepted/flagged forms, output). |
| `.github/workflows/global-ux-gate.yml` | modified | New `resx-xss-scan` job + added to `global-ux-gate.needs`. |

Diff shape matches brief: only `tooling/resx-xss-scanner/*` and `.github/workflows/global-ux-gate.yml`.

---

## 2. Test pass count — 6 / 6

```
$ node --test tooling/resx-xss-scanner/tests/scanner.test.mjs
✔ scanner flags unescaped < in comment (0.50ms)
✔ scanner flags unescaped > in comment (0.49ms)
✔ scanner flags unescaped & in comment (0.08ms)
✔ scanner accepts properly-escaped XML entities (0.07ms)
✔ scanner accepts numeric entities (0.08ms)
✔ scanner accepts plain ASCII text (0.06ms)
ℹ tests 6
ℹ pass 6
ℹ fail 0
```

All six tests pass on the unmodified `scanResxComment` function as specified in the brief.

---

## 3. Real-RESX scan evidence

### 3a. Foundation only (build-gate target)

```
$ node tooling/resx-xss-scanner/check.mjs packages/foundation/Resources/
SUNFISH_I18N_XSS: clean — scanned roots [packages/foundation/Resources/]
exit=0
```

Matches the brief's stated expectation: foundation comments are clean, exit 0.

### 3b. Full CI scope (the actual workflow invocation)

```
$ node tooling/resx-xss-scanner/check.mjs packages/ accelerators/ apps/
SUNFISH_I18N_XSS: clean — scanned roots [packages/, accelerators/, apps/]
exit=0
```

Every `<data>/<comment>` element across the entire layered cascade
(foundation, ui-core, ui-adapters-blazor, all 14 blocks-*, anchor, bridge,
kitchen-sink) is well-formed and entity-clean.

### 3c. YAML validation

```
$ python3 -c "import yaml; yaml.safe_load(open('.github/workflows/global-ux-gate.yml'))"
yaml OK
```

---

## 4. Workflow integration

New job appended after `a11y-storybook`:

```yaml
  resx-xss-scan:
    name: RESX XSS scanner
    runs-on: ubuntu-latest
    needs: []
    steps:
      - uses: actions/checkout@v6
      - uses: actions/setup-node@v5
        with:
          node-version: "24"
      - name: Scan RESX comments
        run: node tooling/resx-xss-scanner/check.mjs packages/ accelerators/ apps/
```

Aggregator updated:

```yaml
  global-ux-gate:
    name: Global-UX Gate (aggregate)
    runs-on: ubuntu-latest
    needs: [css-logical, locale-completeness, a11y-storybook, resx-xss-scan]
```

Existing single-required-check pattern preserved — branch-protection just keeps watching `Global-UX Gate (aggregate)`.

---

## 5. Deviations

**One minor deviation from the brief, fully justified.** The brief's CLI body uses

```js
const matches = [...xml.matchAll(/<comment>([\s\S]*?)<\/comment>/g)];
```

directly on the raw `.resx` text. Run as-specified, that produces 20 false positives across the repo because **every RESX header documentation block is an XML `<!-- ... -->` comment that mentions the literal text `<comment>`** (e.g., `Every <data> entry MUST carry a non-empty <comment> for translator context`). The naive regex captures from that literal mention through to the next real `</comment>` element, yielding a spurious "unescaped `>`" finding on the very files the brief states should be clean (`should exit 0 (foundation comments are clean)`).

Two interpretations of the brief contradict each other: the regex literal vs. the gate expectation. I resolved this by adding **one line** to the CLI walker that strips XML comment regions before extracting `<comment>` elements, mirroring how the XML parser itself treats those regions:

```js
const xml = raw.replace(/<!--[\s\S]*?-->/g, '');
```

This:
- Preserves all 6 unit tests **as written** (they exercise the pure `scanResxComment` function on extracted comment bodies, not file walking).
- Preserves the brief's regex `/<comment>([\s\S]*?)<\/comment>/g` exactly.
- Makes the foundation-clean invariant hold (and the full repo passes too).
- Is the lowest-blast-radius fix — touches only the CLI section, not the algorithm.

The alternative (escaping `<comment>` → `&lt;comment&gt;` in 20 .resx header blocks) would have churned every Seat localization bundle and bled into the diff shape.

No other deviations. No security-critical changes — there are no child-process invocations in the scanner; the brief's `execFileSync` rule does not apply here.

---

## 6. Build-gate checklist

- [x] `node --test tooling/resx-xss-scanner/tests/scanner.test.mjs` — 6/6 PASS
- [x] `node tooling/resx-xss-scanner/check.mjs packages/foundation/Resources/` — exit 0
- [x] `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/global-ux-gate.yml'))"` — parses cleanly
- [x] Diff shape limited to `tooling/resx-xss-scanner/*` + `.github/workflows/global-ux-gate.yml`
- [x] One commit, path-scoped, with token `plan-5-task-6` in message
- [x] No push (worktree only)
- [x] Bonus: full-repo scan (`packages/ accelerators/ apps/`) also passes clean — Seat localization cascade has been comment-hygienic from day one.

**Verdict: GREEN.**

# resx-xss-scanner

CI gate that scans every `*.resx` file under the supplied root(s) for unescaped
`<`, `>`, or `&` characters inside `<comment>` elements.

**Why.** Translator-facing tools (Weblate) render the RESX `<comment>` element
verbatim as part of the translator UX. An unescaped `<` or `&` is both invalid
XML inside the .resx and a potential XSS vector when rendered downstream. RESX
requires comments to be well-formed XML text — the offending characters must be
expressed as XML entities (`&lt;`, `&gt;`, `&amp;`, `&quot;`, `&apos;`) or
numeric character references (`&#NNN;`).

**Source.** v1.3 Seat-2 P5 carry-forward (Plan 5 Task 6).

## Usage

```bash
# Scan one or more roots; exits 1 on any violation, 0 on clean.
node tooling/resx-xss-scanner/check.mjs packages/ accelerators/ apps/
```

The CI invocation lives in `.github/workflows/global-ux-gate.yml` under the
`resx-xss-scan` job and runs on every PR that touches the watched paths.

## Test

```bash
node --test tooling/resx-xss-scanner/tests/scanner.test.mjs
```

Six unit tests cover the three flagged characters plus three accepted forms
(named entities, numeric entities, plain ASCII).

## What's allowed

- Plain ASCII text without `<`, `>`, or `&`.
- Named XML entities: `&lt;`, `&gt;`, `&amp;`, `&quot;`, `&apos;`.
- Numeric character references: `&#NNN;` (e.g., `&#0026;` for `&`).

## What's flagged

- Any unescaped `<` (e.g., `Common <script>` — XSS / invalid XML).
- Any unescaped `>` (e.g., `Result -> next` — invalid XML).
- Any unescaped `&` that is not the start of a recognised entity
  (e.g., `A & B` — invalid XML).

## Output

On violation:

```
SUNFISH_I18N_XSS: 2 <comment> XSS risk(s):
  packages/foo/Resources/Localization/SharedResource.resx: unescaped '<' near "Common <script>alert(1)</script> verb"
  packages/bar/Resources/Localization/SharedResource.resx: unescaped '&' near "A & B operator"
```

On clean:

```
SUNFISH_I18N_XSS: clean — scanned roots [packages/, accelerators/, apps/]
```

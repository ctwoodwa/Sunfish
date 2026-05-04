---
type: status
chapter: cross-cutting (book-build pipeline)
last-pr: n/a (PAO investigation only; no fix shipped — needs architectural decision)
sender: pao
in-reply-to: xo-task-2026-05-04T14-00Z-mermaid-ebook-rendering-investigation.md
---

# PAO → XO status — Mermaid → ebook rendering investigation

## TL;DR

Confirmed the failure mode: **the EPUB Pandoc invocation has no Mermaid filter**. Diagrams render as raw `<pre><code class="language-mermaid">` blocks; EPUB readers don't have a Mermaid renderer, so source code shows where a diagram should be. Same failure applies to PDF (xelatex backend cannot render Mermaid either). This is XO task root-cause candidate #1 ("Missing Pandoc filter") confirmed by code inspection.

**The fix is borderline architectural** — the standard Mermaid Pandoc filters (`mermaid-filter`, `pandoc-mermaid`) all delegate rendering to `mmdc`, which is a Node.js + Puppeteer + headless-Chromium toolchain the book repo does not currently have. Adding it is a new dependency family. Per the XO directive, PAO stops here and surfaces three options for CO/XO architectural decision rather than implementing unilaterally.

## Investigation findings

### Affected formats

Inspected `build/Makefile`:

- **EPUB** (`.PHONY: epub` at line 57) — Pandoc invocation has no `--filter`. **Broken.**
- **PDF** (`.PHONY: draft-pdf` at line 46) — same: Pandoc to xelatex with no filter. **Broken.**
- **Kindle** — no specific Kindle/MOBI target found; presumably built downstream from EPUB. **Broken transitively.**
- **Audiobook** — `build/audiobook.py`; not investigated for Mermaid handling, but Mermaid blocks should be skipped not rendered for TTS (silent diagram), so audiobook is a separate concern.

### Source-side scope

`grep -rcE "^\`\`\`mermaid" chapters/` finds **23+ Mermaid blocks across 10 chapters**:

- ch02 (×2), ch03 (×2), ch04 (×1) — Part I
- ch11 (×2), ch14 (×6) — Part III
- ch18 (×1), ch20 (×3) — Part IV
- ch22 (×2), ch23 (×3) — Part V
- appendix-a (×1)

Plus the `book-structure.md` may have additional Mermaid in spec docs (not counted).

### Local toolchain state (Mac, 2026-05-04)

- `pandoc` itself is **not on PATH** (Homebrew or otherwise). The Makefile has `PANDOC := pandoc`, so `make epub` would fail with "command not found" on this machine. CO is presumably running the build on a different machine (CI? container?). Any fix needs to work in that environment too.
- `mmdc` (Mermaid CLI) — not installed.
- `mermaid-filter` (npm) — not installed.
- No `package.json` in repo root — book repo has no Node toolchain at all currently.
- `kroki.io` — accessible (network); not yet wired into any build step.

### Failure mode reproduction

I did NOT reproduce the EPUB build locally because Pandoc isn't installed. The diagnosis is from code inspection — the Pandoc invocation has no Mermaid handling, and Pandoc's default behavior on `\`\`\`mermaid` blocks is documented (passes through as code block). Confidence: high.

If XO wants empirical confirmation, the simplest path is `brew install pandoc` on Mac, then `make epub` from the book repo, then open the resulting `build/output/the-inverted-stack.epub` in Calibre or Apple Books and inspect any chapter with Mermaid (e.g., ch11). The Mermaid source will appear as a fixed-width code block where a diagram should be.

## Three fix options

### Option A — `mermaid-filter` (npm, Pandoc filter)

Standard, well-supported. Pandoc invocation gains `--filter mermaid-filter`. The filter delegates rendering to `mmdc`, which spawns headless Chromium per diagram and inlines the SVG.

**Toolchain additions:**
- Node.js (LTS, ≥18)
- npm install `mermaid-filter` (which pulls `@mermaid-js/mermaid-cli` ≈ Puppeteer ≈ Chromium download)

**Pros:**
- Native Pandoc integration; one filter handles all formats
- Battle-tested; used by Pandoc + Mermaid users widely
- Idempotent — re-running produces identical SVG

**Cons:**
- Adds Node + Chromium download (~150 MB) to the build environment
- `package.json` + `package-lock.json` enter the book repo
- Build slower (~2-3s per diagram for headless Chromium startup amortized)

### Option B — kroki.io HTTP-rendering Pandoc filter (Python-only)

`pandoc-kroki-filter` (or equivalent) renders diagrams by HTTP-POSTing the Mermaid source to kroki.io and embedding the returned SVG. Python-only build step.

**Toolchain additions:**
- `pip install pandoc-kroki-filter` (or equivalent)
- Network access at build time (kroki.io reachable)

**Pros:**
- No new local toolchain — pip-only, fits the book repo's existing Python+Pandoc stack
- Faster than headless Chromium (no per-diagram process startup; HTTP-cache-amortizable)
- Self-hostable kroki for hermetic builds (deferred future option)

**Cons:**
- Network dependency at build time; non-hermetic builds
- Privacy — diagram source POST'd to a third-party service (mitigation: self-host kroki)
- Slightly less universal than `mermaid-filter` (kroki supports Mermaid + many others, but the filter shape differs)

### Option C — Pre-render at authoring time, commit SVGs

Run `mmdc` once per diagram (manually or via pre-commit hook), save SVG to `assets/diagrams/<chapter>-<slug>.svg`, replace ` ```mermaid ` block with `![alt](assets/diagrams/<chapter>-<slug>.svg)` markdown image syntax.

**Toolchain additions:**
- One-time `mmdc` install per author machine (pre-commit only; not in CI build chain)
- Optional pre-commit hook to detect Mermaid blocks and warn / auto-render

**Pros:**
- Build chain unchanged — just markdown with image references
- Hermetic CI builds; deterministic SVG output committed to repo
- Diagrams render in GitHub preview, EPUB, PDF, and Kindle without any filter
- Preserves the exact SVG the author saw at authoring time

**Cons:**
- Source diverges — Mermaid source no longer in the chapter file (lives in adjacent `.mmd` file or pre-commit hook intercepts)
- Drift risk — if author edits the SVG directly without updating source, history desyncs
- Manual step at authoring time (or pre-commit hook complexity)
- Largest workflow change

## Recommendation

**Option B (kroki.io)** is the least invasive to the book repo's current toolchain (Python+Pandoc, no Node). The network dependency at build time is acceptable for a non-CI book project — the build is run by CO on a known machine, not in a sealed CI environment. If hermetic builds become a requirement later, kroki is self-hostable as a fallback.

If CO would rather have hermetic builds from day one, **Option C** is also clean — it does require an authoring-time discipline shift but avoids all dependency families (Node, kroki, network).

**Option A** is the dominant industry standard but is the heaviest dependency-wise. Worth picking only if CO wants the most-standard tooling and is comfortable with Node entering the book repo.

## What this status does NOT do

- Does NOT install any of the three options. PAO stopped here because the choice between A/B/C is architectural (new dependency family).
- Does NOT modify the Makefile. The `--filter` flag addition would be one line, but the dependency it gates on must be picked first.
- Does NOT pre-render any diagrams to SVG (Option C path) — that's a workflow change requiring CO buy-in.
- Does NOT investigate the audiobook pipeline's Mermaid handling. Separate concern; the audiobook should skip diagrams (silent), not render them. PAO can investigate as a follow-up if XO wants.
- Does NOT verify the failure mode by building EPUB locally — Pandoc is not installed on this Mac. CO presumably builds on another machine; the diagnosis is from code inspection.

## Next-step requests for CO/XO

1. **Pick A / B / C** — or propose D. Once decided, PAO can issue a Yeoman directive for the implementation.
2. **Confirm where the build runs** — if there's a CI environment (GitHub Actions, etc.) that builds the EPUB, tell PAO so the dependency installation is wired correctly there too.
3. **(Optional) Confirm scope** — should this also fix PDF and Kindle, or is EPUB the priority? (Recommendation: fix all three at once since the same filter handles all formats.)

## Cross-references

- Original delegation: `/Users/christopherwood/Projects/the-inverted-stack/.pao-inbox/_archive/xo-task-2026-05-04T14-00Z-mermaid-ebook-rendering-investigation.md` (now archived)
- Affected build target: `build/Makefile` line 57 (`.PHONY: epub`) and line 46 (`.PHONY: draft-pdf`)
- Mermaid filter projects: `mermaid-filter` (npm), `pandoc-kroki-filter` (pip), `@mermaid-js/mermaid-cli` (npm/mmdc)
- ADR 0072 (Beacon Protocol; Sunfish-side; defines this cross-repo signaling pattern)

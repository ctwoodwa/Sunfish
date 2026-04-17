# Phase 8: Documentation Migration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate Marilo's documentation corpus (107 component-spec directories, 59 reusable content templates, the DocFX toolchain, and all long-form articles covering getting-started, theming, accessibility, common features, security, testing, globalization, and troubleshooting) to the Sunfish repository under `apps/docs/`. Wire DocFX to generate API reference from `packages/` XML comments, enable built-in Lucene search, publish the site to GitHub Pages via a reusable workflow, and leave zero broken links, zero rogue `Marilo`/`MariloX` references, and a clean `dotnet docfx` build.

**Architecture:** `apps/docs/` is the documentation application — sibling of `apps/kitchen-sink/`. DocFX stays as the toolchain (Microsoft's official C# doc generator; same tool Marilo used; natively reads the XML doc comments Sunfish is already emitting via `<GenerateDocumentationFile>true` in `Directory.Build.props`). The docs site has two content sources: hand-authored markdown in `apps/docs/articles/` + `apps/docs/component-specs/`, and auto-generated API reference pulled from `packages/foundation`, `packages/ui-core`, and `packages/ui-adapters-blazor` csproj files. The component-spec markdown is the single source of truth for per-component behavior docs — it is never duplicated into the csproj tree.

**Tech Stack:** DocFX 2.78.x (latest stable as of 2026-04), .NET 10, YAML (toc), Markdown (content), GitHub Actions (deploy), GitHub Pages (host)

---

## Scope

### In Scope

- Migrate `C:/Projects/Marilo/docs/component-specs/` → `apps/docs/component-specs/` (107 spec directories including `README.md` + `component-mapping.json`)
- Migrate `C:/Projects/Marilo/docs/_contentTemplates/` → `apps/docs/_contentTemplates/` (59 `.md` template files across 26 category folders)
- Migrate `C:/Projects/Marilo/docfx/` tooling → `apps/docs/` (docfx.json, toc.yml, index.md, articles/, images/, logo.svg)
- Migrate long-form articles from `C:/Projects/Marilo/docfx/articles/` → `apps/docs/articles/` (getting-started, theming, accessibility, common-features, security, testing, globalization, troubleshooting — plus `components/` overview pages)
- Sed-style rename pass across all migrated `.md` + `.yml` files: `Marilo` → `Sunfish`, `MariloX` → `SunfishX`, `marilo-` → `sunfish-` (in CSS class references, asset paths)
- Update `docfx.json` project references from `../src/Marilo.*` to `../../packages/*` (foundation, ui-core, ui-adapters-blazor)
- Update `docfx.json` metadata (`_appTitle`, `_appName`, `_appFooter`), search flag, and `TargetFramework: net10.0`
- Add GitHub Pages deploy workflow `.github/workflows/docs.yml`
- Add `apps/docs/README.md` explaining local preview + edit workflow
- Link from root `README.md` to the published docs site
- Local build verification (`dotnet docfx`) with zero broken-link warnings

### Out of Scope

- Rewriting spec content (prose quality improvements are a separate pipeline)
- Adding new articles Marilo didn't have
- Moving icon browser tooling (`marilo-icon-browser.js`, `marilo-icons.json`) — handled in Phase 3 icons track
- Switching toolchains (Docusaurus/Starlight/Jekyll) — see D-DOCFX
- Live demo embedding (kitchen-sink link stub only; deeper integration is post-Phase 7)
- Docs translations / i18n
- Custom DocFX template development (use `default` + `modern` built-ins, same as Marilo)

---

## Key Decisions

**D-DOCFX:** Keep DocFX as the documentation toolchain.
- Marilo already invested in DocFX — migration cost is near-zero.
- Sunfish's `Directory.Build.props` already sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. DocFX natively consumes these XML files to build API reference.
- Avoids adding a JS/Node build dependency (Docusaurus, Starlight) to a .NET-first repository.
- Built-in Lucene search is sufficient for Sunfish's current scale.
- Pin DocFX version to `2.78.3` via `dotnet tool manifest` (`.config/dotnet-tools.json`) so CI and local builds stay deterministic.
- Rejected alternatives: Docusaurus (requires npm), Astro+Starlight (new toolchain, unfamiliar to .NET contributors), Jekyll (Ruby dependency, no C# XML-doc integration).

**D-LOCATION:** Docs app lives at `apps/docs/`.
- Mirrors `apps/kitchen-sink/` — both are applications that consume Sunfish packages.
- Same depth allows `apps/docs/` and `apps/kitchen-sink/` to share future infrastructure (e.g., a shared `apps/_shared/` for demo chrome) without special-casing paths.
- This path is explicitly called out as the docs destination in the master migration plan.

**D-DOC-SOURCE:** Component specs in `apps/docs/component-specs/` are the **authoritative prose spec** for each component. API parameter/event names render from XML doc comments in the `packages/*` source. There is no third source of truth. A spec may reference an API-generated page via DocFX's `xref` links (e.g., `<xref:Sunfish.Components.Blazor.SunfishButton>`). Do not paste parameter tables into spec markdown — let DocFX render them from XML.

**D-BRAND-REPLACEMENT:** A deterministic rename pass runs across every migrated `.md` and `.yml`:
- `Marilo` → `Sunfish` (prose)
- `MariloX` → `SunfishX` (code snippets, prefab component names)
- `marilo-` → `sunfish-` (CSS class references like `.marilo-button`, asset paths like `_content/Marilo.Providers.FluentUI/...`)
- `MariloInc` → `SunfishContributors` (author/publisher metadata)
- `Marilo.com` / `www.marilo.com` → `sunfish.dev` (placeholder domain; confirm before publishing)
- `slug:` cross-refs are renamed only where the target slug also changes (preserve slugs that still resolve)
- Preserve intentional historical references — any line containing `origin: marilo` or `rebrand from Marilo` is skipped. The plan task list shows the exact grep-filter guard.

**D-CONTENT-TEMPLATES:** The 59 files under `_contentTemplates/` are DocFX "shared include" fragments referenced via `[!include[...]]` macros in spec articles. They are migrated as-is with the D-BRAND-REPLACEMENT pass applied. DocFX resolves their paths relative to `docfx.json`, so after migration `apps/docs/_contentTemplates/` must sit alongside `apps/docs/docfx.json`.

**D-API-GENERATION:** Enable DocFX's C# metadata pipeline. The `metadata` section in `docfx.json` targets:
- `../../packages/foundation/Sunfish.Foundation.csproj`
- `../../packages/ui-core/Sunfish.UICore.csproj`
- `../../packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj`

Output goes to `apps/docs/api/` (same convention Marilo used). `includePrivateMembers: false`. Set `properties.TargetFramework: net10.0` so DocFX restores with the right TFM.

**D-SEARCH:** Enable DocFX's built-in Lucene search (`_enableSearch: true` in `globalMetadata`). No external dependency (Algolia, etc.).

**D-DEPLOYMENT:** Publish to GitHub Pages via a dedicated `docs.yml` workflow.
- Trigger: push to `main` touching `apps/docs/**`, `packages/**/*.cs`, `packages/**/*.csproj`, or the workflow itself.
- Build runs `dotnet docfx apps/docs/docfx.json`.
- Deploy uses `actions/deploy-pages@v4` against the built `apps/docs/_site/`.
- Custom domain: defer. Start with `https://<org>.github.io/sunfish/`.
- Branch protection on `main` is unchanged — the workflow's `pages: write` + `id-token: write` permissions are scoped to the workflow only.

---

## File Structure (After Phase 8)

```
apps/docs/
  docfx.json                 ← references ../../packages/*.csproj
  toc.yml                    ← top-level navigation
  index.md                   ← landing page
  logo.svg                   ← Sunfish logo (placeholder until branding ready)
  README.md                  ← how to edit + preview docs
  articles/                  ← 8 long-form subtrees
    getting-started/ (overview, installation, first-component, provider-selection, web-app)
    theming/         (overview, providers, custom-provider, dark-mode, runtime-switching, token-reference)
    accessibility/   (overview, compliance)
    common-features/ (data-binding, dimensions, icons, input-validation, keyboard-navigation, loading-indicators)
    components/      (category landing pages: alert, button, card, dialog, icon, search-box, select, tabs, text-field, tooltip)
    globalization/   (overview, rtl-support)
    security/        (overview, csp)
    testing/         (bunit-testing)
    troubleshooting/ (general-issues, js-errors)
  component-specs/           ← 107 spec dirs + README.md + component-mapping.json + toc.yml
    button/          (overview, appearance, disabled-button, events, icons, styling, type, accessibility/)
    grid/            (overview, columns/, editing/, data-binding, events, filter/, selection/, toolbar, ...)
    … (105 more dirs copied verbatim; see Marilo /docs/component-specs/README.md)
  _contentTemplates/         ← 59 `.md` fragments across 26 category folders
  images/                    ← shared article images
  api/                       ← generated; gitignored
  _site/                     ← built output; gitignored
  obj/                       ← docfx metadata cache; gitignored

.config/dotnet-tools.json    ← pins docfx 2.78.3
.github/workflows/docs.yml   ← build + deploy to GitHub Pages
.gitignore                   ← adds apps/docs/_site/, apps/docs/obj/, apps/docs/api/
README.md                    ← add link to docs site
```

**Counts (sourced from Marilo):**
- Component spec directories: 107 (plus `README.md`, `component-mapping.json`, `toc.yml`)
- Content template subdirectories: 26, containing 59 `.md` files
- DocFX article subtrees: 8 (`getting-started`, `theming`, `accessibility`, `common-features`, `components`, `globalization`, `security`, `testing`, `troubleshooting`)

---

## Task 1: Branch and scaffold

**Files:**
- Create: `apps/docs/` directory
- Create: `.config/dotnet-tools.json`
- Modify: `.gitignore`

- [ ] **Step 1: Create branch**

```bash
cd "C:/Projects/Sunfish"
git switch -c feat/migration-phase8-docs
mkdir -p apps/docs
```

- [ ] **Step 2: Pin DocFX as a local .NET tool**

Create `.config/dotnet-tools.json` (or amend if it exists):

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "docfx": {
      "version": "2.78.3",
      "commands": [ "docfx" ]
    }
  }
}
```

Then:

```bash
cd "C:/Projects/Sunfish"
dotnet tool restore
dotnet docfx --version
```

Expected: prints `2.78.3+...`. This guarantees CI and developer machines use the same DocFX.

- [ ] **Step 3: Add docs-generated artifacts to .gitignore**

Append to `.gitignore`:

```gitignore
# Docs (generated)
apps/docs/_site/
apps/docs/api/
apps/docs/obj/
```

- [ ] **Step 4: Commit scaffolding**

```bash
but stage ".config/dotnet-tools.json" "feat/migration-phase8-docs"
but stage ".gitignore" "feat/migration-phase8-docs"
but commit -m "chore(docs): scaffold apps/docs, pin docfx 2.78.3 via dotnet tool manifest" "feat/migration-phase8-docs"
```

---

## Task 2: Copy component specs

**Files:**
- Copy: `C:/Projects/Marilo/docs/component-specs/**` → `apps/docs/component-specs/**`

- [ ] **Step 1: Copy the tree verbatim**

```bash
cp -r "C:/Projects/Marilo/docs/component-specs/." \
       "C:/Projects/Sunfish/apps/docs/component-specs/"

# Sanity: count spec dirs + README + JSON
ls "C:/Projects/Sunfish/apps/docs/component-specs/" | wc -l
```

Expected: 109 entries (107 spec dirs + `README.md` + `component-mapping.json`; `toc.yml` is the 110th entry if present at root — treat as informational).

- [ ] **Step 2: Apply D-BRAND-REPLACEMENT across the spec tree**

Do not use a naive blanket sed. Use this targeted pass that preserves the "origin" and "rebrand from Marilo" escape hatches:

```bash
cd "C:/Projects/Sunfish/apps/docs/component-specs"

# Files to transform: .md and .yml only
find . -type f \( -name "*.md" -o -name "*.yml" -o -name "*.json" \) | while read -r f; do
  # Skip files explicitly tagged with origin: marilo
  if grep -qE '^origin:\s*marilo\s*$' "$f"; then
    echo "SKIP (origin tag): $f"
    continue
  fi
  # The four substitutions, in order
  sed -i \
    -e 's/MariloX/SunfishX/g' \
    -e 's/MariloInc/SunfishContributors/g' \
    -e 's/Marilo\.com/sunfish.dev/g' \
    -e 's/www\.marilo\.com/sunfish.dev/g' \
    -e 's/marilo-/sunfish-/g' \
    -e 's/Marilo/Sunfish/g' \
    "$f"
done
```

**Important:** The replacements run in order — `MariloX`/`MariloInc`/`Marilo.com`/`www.marilo.com`/`marilo-` are more specific than bare `Marilo`, so they must be substituted first. The final bare-`Marilo` substitution cleans up anything that remained.

- [ ] **Step 3: Verify no Marilo references leaked through**

```bash
cd "C:/Projects/Sunfish/apps/docs/component-specs"
grep -rn "Marilo" . | grep -v "rebrand from Marilo" | grep -v "origin: marilo" | head -20
```

Expected: empty (or only intentional "rebrand from Marilo" explanatory text).

- [ ] **Step 4: Commit spec migration**

```bash
but stage "apps/docs/component-specs/" "feat/migration-phase8-docs"
but commit -m "docs(migration): copy 107 component specs from Marilo with Sunfish rename pass" "feat/migration-phase8-docs"
```

---

## Task 3: Copy content templates

**Files:**
- Copy: `C:/Projects/Marilo/docs/_contentTemplates/**` → `apps/docs/_contentTemplates/**`

- [ ] **Step 1: Copy the templates verbatim**

```bash
cp -r "C:/Projects/Marilo/docs/_contentTemplates/." \
       "C:/Projects/Sunfish/apps/docs/_contentTemplates/"

find "C:/Projects/Sunfish/apps/docs/_contentTemplates/" -name "*.md" | wc -l
```

Expected: 59 files.

- [ ] **Step 2: Apply the same D-BRAND-REPLACEMENT pass**

```bash
cd "C:/Projects/Sunfish/apps/docs/_contentTemplates"

find . -type f -name "*.md" | while read -r f; do
  sed -i \
    -e 's/MariloX/SunfishX/g' \
    -e 's/MariloInc/SunfishContributors/g' \
    -e 's/Marilo\.com/sunfish.dev/g' \
    -e 's/www\.marilo\.com/sunfish.dev/g' \
    -e 's/marilo-/sunfish-/g' \
    -e 's/Marilo/Sunfish/g' \
    "$f"
done

grep -rn "Marilo" . | head -10
```

Expected: empty output from the grep.

- [ ] **Step 3: Commit template migration**

```bash
but stage "apps/docs/_contentTemplates/" "feat/migration-phase8-docs"
but commit -m "docs(migration): copy 59 content templates with Sunfish rename pass" "feat/migration-phase8-docs"
```

---

## Task 4: Copy articles, index, toc, images

**Files:**
- Copy: `C:/Projects/Marilo/docfx/articles/**` → `apps/docs/articles/**`
- Copy: `C:/Projects/Marilo/docfx/images/**` → `apps/docs/images/**`
- Copy: `C:/Projects/Marilo/docfx/index.md` → `apps/docs/index.md`
- Copy: `C:/Projects/Marilo/docfx/toc.yml` → `apps/docs/toc.yml`
- Copy: `C:/Projects/Marilo/docfx/logo.svg` → `apps/docs/logo.svg` (placeholder)

- [ ] **Step 1: Copy articles + top-level files**

```bash
cp -r "C:/Projects/Marilo/docfx/articles/." "C:/Projects/Sunfish/apps/docs/articles/"
cp    "C:/Projects/Marilo/docfx/index.md"   "C:/Projects/Sunfish/apps/docs/index.md"
cp    "C:/Projects/Marilo/docfx/toc.yml"    "C:/Projects/Sunfish/apps/docs/toc.yml"
cp    "C:/Projects/Marilo/docfx/logo.svg"   "C:/Projects/Sunfish/apps/docs/logo.svg"
cp -r "C:/Projects/Marilo/docfx/images/."   "C:/Projects/Sunfish/apps/docs/images/" 2>/dev/null || true
```

- [ ] **Step 2: Apply D-BRAND-REPLACEMENT across articles + top files**

```bash
cd "C:/Projects/Sunfish/apps/docs"

find articles index.md toc.yml -type f \( -name "*.md" -o -name "*.yml" \) | while read -r f; do
  sed -i \
    -e 's/MariloX/SunfishX/g' \
    -e 's/MariloInc/SunfishContributors/g' \
    -e 's/Marilo\.com/sunfish.dev/g' \
    -e 's/www\.marilo\.com/sunfish.dev/g' \
    -e 's/marilo-/sunfish-/g' \
    -e 's/Marilo/Sunfish/g' \
    "$f"
done
```

- [ ] **Step 3: Update `toc.yml` component-specs path**

Marilo's `toc.yml` references `../docs/component-specs/toc.yml` because DocFX lived in `docfx/` and specs lived in `docs/`. In Sunfish they are siblings under `apps/docs/`. Edit `apps/docs/toc.yml`:

```yaml
- name: Component Specs
  href: component-specs/toc.yml
```

(replaces the Marilo `../docs/component-specs/toc.yml` line)

Also update the Components href — Marilo pointed to a localhost URL for live demos (`https://localhost:5301/components`). Replace with the kitchen-sink published URL or a relative path to the Sunfish kitchen-sink once deployed. For now:

```yaml
- name: Components
  href: articles/components/
```

- [ ] **Step 4: Verify no Marilo references remain in articles**

```bash
cd "C:/Projects/Sunfish/apps/docs"
grep -rn "Marilo" articles/ index.md toc.yml | head -20
```

Expected: empty.

- [ ] **Step 5: Commit articles migration**

```bash
but stage "apps/docs/articles/" "feat/migration-phase8-docs"
but stage "apps/docs/index.md" "feat/migration-phase8-docs"
but stage "apps/docs/toc.yml" "feat/migration-phase8-docs"
but stage "apps/docs/logo.svg" "feat/migration-phase8-docs"
but stage "apps/docs/images/" "feat/migration-phase8-docs"
but commit -m "docs(migration): copy DocFX articles, index, toc, images with Sunfish rename pass" "feat/migration-phase8-docs"
```

---

## Task 5: Write Sunfish docfx.json

**Files:**
- Create: `apps/docs/docfx.json`

- [ ] **Step 1: Author the Sunfish docfx.json**

Create `apps/docs/docfx.json`:

```json
{
  "metadata": [
    {
      "src": [
        {
          "files": [
            "foundation/Sunfish.Foundation.csproj",
            "ui-core/Sunfish.UICore.csproj",
            "ui-adapters-blazor/Sunfish.Components.Blazor.csproj"
          ],
          "src": "../../packages"
        }
      ],
      "dest": "api",
      "includePrivateMembers": false,
      "properties": {
        "TargetFramework": "net10.0"
      }
    }
  ],
  "build": {
    "content": [
      {
        "files": ["api/**.yml", "api/index.md"]
      },
      {
        "files": [
          "articles/**/**.md",
          "articles/**/toc.yml",
          "toc.yml",
          "*.md"
        ]
      },
      {
        "files": [
          "component-specs/**/**.md",
          "component-specs/**/toc.yml",
          "component-specs/toc.yml"
        ]
      }
    ],
    "resource": [
      {
        "files": ["images/**", "logo.svg"]
      },
      {
        "files": ["component-specs/**/images/**"]
      }
    ],
    "output": "_site",
    "template": ["default", "modern"],
    "globalMetadata": {
      "_appTitle": "Sunfish Documentation",
      "_appName": "Sunfish",
      "_appFooter": "Sunfish &mdash; MIT License",
      "_enableSearch": true,
      "_disableContribution": false,
      "_gitContribute": {
        "repo": "https://github.com/your-org/sunfish",
        "branch": "main"
      }
    }
  }
}
```

Notes on the shape change vs. Marilo's docfx.json:
- `metadata.src.src` changes from `../src` (Marilo) to `../../packages` (Sunfish) — docs now live one level deeper under `apps/docs/`.
- Four Marilo csprojs (`Marilo.Core`, `Marilo.Components`, `Marilo.Icons`, `Marilo.Providers.FluentUI`) collapse to three Sunfish csprojs (icons + providers merged into ui-adapters-blazor for Phase 3; revisit if icons or providers split later).
- `build.content` for component-specs drops the `"src": "../docs"` override because specs now live alongside `docfx.json`.
- Resources for `component-specs/**/images/**` drop the `"src": "../docs"` override for the same reason.
- `globalMetadata._gitContribute` enables the "Edit on GitHub" link on every page.

- [ ] **Step 2: Commit the config**

```bash
but stage "apps/docs/docfx.json" "feat/migration-phase8-docs"
but commit -m "docs: add Sunfish docfx.json with metadata referencing packages/*" "feat/migration-phase8-docs"
```

---

## Task 6: First local build + link audit

**Files:**
- Uses: `apps/docs/docfx.json`

- [ ] **Step 1: Build the site locally**

```bash
cd "C:/Projects/Sunfish"
dotnet docfx apps/docs/docfx.json --warningsAsErrors
```

Expected (first pass will likely have some warnings):
- Zero build errors.
- API metadata extracts from all three packages (check `apps/docs/api/` for generated `.yml` files including `Sunfish.Foundation.Services.SunfishBuilder.yml`, `Sunfish.UICore.Contracts.ISunfishCssProvider.yml`, `Sunfish.Components.Blazor.Base.SunfishComponentBase.yml`).
- Generated site appears in `apps/docs/_site/`.

If warnings appear (likely: broken `slug:` cross-refs, missing image files, dead `[!include]` paths), note them in a scratch file and proceed to Step 2 before committing.

- [ ] **Step 2: Fix the high-signal warnings**

Common warnings and fixes:

| Warning class | Root cause | Fix |
|---|---|---|
| "Invalid file link: `../docs/component-specs/...`" | Stale Marilo-era path in article TOC | Update `apps/docs/articles/**/toc.yml` to drop `../docs/` prefix |
| "Include file not found: `_contentTemplates/...`" | Path resolution broke during migration | Ensure `_contentTemplates/` sits at `apps/docs/_contentTemplates/` (sibling of docfx.json) |
| "Cannot resolve xref: `MariloBuilder`" | Sed missed a `Marilo` reference in a spec | Grep the file, rename explicitly |
| "Image file not found: `images/button-basic.png`" | Image lives under `component-specs/button/images/` not top-level | Either move image up or adjust reference |
| "Duplicate uid `getting-started-installation`" | Frontmatter `uid` not updated during rename | Search `uid:` in frontmatter, prefix with `sunfish-` where needed |

Fix each class with one small commit so the fix is reviewable.

- [ ] **Step 3: Re-run the build with warnings-as-errors**

```bash
cd "C:/Projects/Sunfish"
rm -rf apps/docs/_site apps/docs/api apps/docs/obj
dotnet docfx apps/docs/docfx.json --warningsAsErrors
```

Expected: 0 errors, 0 warnings. If warnings remain, iterate on Step 2 until clean.

- [ ] **Step 4: Smoke-test the generated site**

Serve locally:

```bash
cd "C:/Projects/Sunfish"
dotnet docfx apps/docs/docfx.json --serve --port 8080
```

Open `http://localhost:8080/` in a browser. Verify:
- Landing page renders and shows "Sunfish Documentation".
- Top nav has: Getting Started, Components, Theming, Accessibility, Common Features, Security, Testing, Globalization, Troubleshooting, Component Specs, API Reference.
- Clicking "API Reference" shows `Sunfish.Foundation`, `Sunfish.UICore`, `Sunfish.Components.Blazor` namespaces.
- Clicking any component spec (e.g., Button) renders its article.
- The search box (top right) returns hits for "button", "theme", "grid".
- Zero `Marilo` or `MariloX` appears anywhere in rendered HTML (`curl http://localhost:8080/index.html | grep -i marilo` → empty).

- [ ] **Step 5: Commit any fixes from Step 2**

```bash
but stage "apps/docs/" "feat/migration-phase8-docs"
but commit -m "docs: fix docfx warnings from first migration build" "feat/migration-phase8-docs"
```

(If no fixes were needed, skip this commit.)

---

## Task 7: Add GitHub Pages deploy workflow

**Files:**
- Create: `.github/workflows/docs.yml`

- [ ] **Step 1: Create the workflow**

Create `.github/workflows/docs.yml`:

```yaml
name: Docs

on:
  push:
    branches: [ main ]
    paths:
      - 'apps/docs/**'
      - 'packages/**/*.cs'
      - 'packages/**/*.csproj'
      - '.config/dotnet-tools.json'
      - '.github/workflows/docs.yml'
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: pages
  cancel-in-progress: true

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore tools
        run: dotnet tool restore

      - name: Restore packages
        run: dotnet restore Sunfish.slnx

      - name: Build docs
        run: dotnet docfx apps/docs/docfx.json --warningsAsErrors

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: apps/docs/_site

  deploy:
    needs: build
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

Key points:
- Uses the pinned DocFX from `.config/dotnet-tools.json` (via `dotnet tool restore`).
- `--warningsAsErrors` gates deployment on a clean build.
- `actions/upload-pages-artifact@v3` + `actions/deploy-pages@v4` is the modern GitHub Pages flow (no `gh-pages` branch required).
- `concurrency: pages` prevents two deploys racing.

- [ ] **Step 2: Enable GitHub Pages in the repo settings**

Manual step (document in PR description):
1. Repo Settings → Pages → Source: **GitHub Actions**.
2. First successful run of the workflow will publish to `https://<org>.github.io/sunfish/`.

- [ ] **Step 3: Commit the workflow**

```bash
but stage ".github/workflows/docs.yml" "feat/migration-phase8-docs"
but commit -m "ci(docs): add GitHub Pages deploy workflow for apps/docs" "feat/migration-phase8-docs"
```

---

## Task 8: Author apps/docs/README.md

**Files:**
- Create: `apps/docs/README.md`

- [ ] **Step 1: Write the README**

Create `apps/docs/README.md` with sections covering:
- **Structure** — `articles/` (guides), `component-specs/` (authoritative API), `_contentTemplates/` (includes), `images/`, `docfx.json`, `toc.yml`, `index.md`
- **Preview docs locally** — `dotnet tool restore` then `dotnet docfx apps/docs/docfx.json --serve --port 8080` (rebuilds on change)
- **Warnings-as-errors build (what CI runs)** — `dotnet docfx apps/docs/docfx.json --warningsAsErrors`
- **Editing workflow** — edit markdown → preview → warnings-as-errors → commit → CI deploys on merge to `main`
- **API reference generation** — `docfx.json` pulls XML from `../../packages/foundation/`, `../../packages/ui-core/`, `../../packages/ui-adapters-blazor/` csprojs; keep XML doc comments on all public members (already gated by `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in `Directory.Build.props`)

- [ ] **Step 2: Link the docs site from the root README**

Open `C:/Projects/Sunfish/README.md`. Add under the project intro (placement-appropriate for the existing content):

```markdown
## Documentation

- **Site:** https://<org>.github.io/sunfish/
- **Edit docs:** [apps/docs/README.md](apps/docs/README.md)
```

- [ ] **Step 3: Commit**

```bash
but stage "apps/docs/README.md" "feat/migration-phase8-docs"
but stage "README.md" "feat/migration-phase8-docs"
but commit -m "docs: add apps/docs/README and link from root README" "feat/migration-phase8-docs"
```

---

## Task 9: Final full-repo verification

**Files:**
- Verifies: everything from Tasks 1–8

- [ ] **Step 1: Clean rebuild**

```bash
cd "C:/Projects/Sunfish"
rm -rf apps/docs/_site apps/docs/api apps/docs/obj
dotnet tool restore
dotnet restore Sunfish.slnx
dotnet build Sunfish.slnx
dotnet docfx apps/docs/docfx.json --warningsAsErrors
```

Expected: 0 errors, 0 warnings throughout. Site generates at `apps/docs/_site/`.

- [ ] **Step 2: Grep for residual Marilo references across the whole docs tree**

```bash
cd "C:/Projects/Sunfish/apps/docs"
grep -rniE "marilo(?!-phase-origin)" . \
  --include="*.md" --include="*.yml" --include="*.json" \
  | grep -v "rebrand from Marilo" \
  | grep -v "_site/" \
  | grep -v "api/" \
  | grep -v "obj/"
```

Expected: empty.

- [ ] **Step 3: Grep rendered HTML for residual Marilo references**

```bash
grep -rniE "marilo" "C:/Projects/Sunfish/apps/docs/_site/" | head -20
```

Expected: empty (or exclusively "rebrand from Marilo" intentional strings).

- [ ] **Step 4: Verify API reference populated**

```bash
ls "C:/Projects/Sunfish/apps/docs/api/" | head -20
```

Expected: lists of `Sunfish.Foundation.*.yml`, `Sunfish.UICore.*.yml`, `Sunfish.Components.Blazor.*.yml`. Specifically look for:
- `Sunfish.Foundation.Services.SunfishBuilder.yml` (from Phase 1)
- `Sunfish.UICore.Contracts.ISunfishCssProvider.yml` (from Phase 2)
- `Sunfish.Components.Blazor.Base.SunfishComponentBase.yml` (from Phase 3a)

- [ ] **Step 5: Push the branch**

```bash
git push origin feat/migration-phase8-docs
```

- [ ] **Step 6: Trigger the workflow manually to verify CI**

Via the GitHub UI: Actions → Docs → Run workflow → main.

Expected: workflow succeeds on the first try (if it fails, fix warnings locally with `--warningsAsErrors` — never bypass CI's gate).

---

## Self-Review Checklist

- [ ] `apps/docs/docfx.json` references `../../packages/foundation/Sunfish.Foundation.csproj`, `../../packages/ui-core/Sunfish.UICore.csproj`, `../../packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj`
- [ ] `.config/dotnet-tools.json` pins DocFX to `2.78.3`
- [ ] `dotnet docfx apps/docs/docfx.json --warningsAsErrors` returns 0 errors, 0 warnings
- [ ] `apps/docs/_site/index.html` renders a page titled "Sunfish Documentation" — no "Marilo" string in rendered output
- [ ] `apps/docs/api/` contains generated YAML for `SunfishBuilder`, `ISunfishCssProvider`, `SunfishComponentBase`
- [ ] Built-in Lucene search works (the search input in the top nav returns hits for "button", "theme", "grid")
- [ ] All 107 component-spec directories copied from Marilo exist under `apps/docs/component-specs/`
- [ ] All 59 content templates copied from Marilo exist under `apps/docs/_contentTemplates/`
- [ ] `apps/docs/articles/` contains all 8 Marilo article subtrees (getting-started, theming, accessibility, common-features, components, globalization, security, testing, troubleshooting)
- [ ] No `Marilo` / `MariloX` / `marilo-` / `MariloInc` / `Marilo.com` / `www.marilo.com` references remain in `apps/docs/` source (grep returns empty, excluding intentional "rebrand from Marilo" explanatory strings)
- [ ] `apps/docs/README.md` documents the local preview command and the CI gate
- [ ] Root `README.md` links to the docs site and to `apps/docs/README.md`
- [ ] `.github/workflows/docs.yml` triggers on `apps/docs/**` or `packages/**/*.cs` changes, builds with `--warningsAsErrors`, and deploys via `actions/deploy-pages@v4`
- [ ] `.gitignore` excludes `apps/docs/_site/`, `apps/docs/api/`, `apps/docs/obj/`
- [ ] `dotnet build Sunfish.slnx` still passes (docs migration did not affect the solution)
- [ ] All existing tests still pass (`dotnet test Sunfish.slnx` → same count as end of Phase 3a plus any new additions)

---

## How to Preview Docs Locally

Once Phase 8 is merged, any contributor can preview the docs with three commands:

```bash
# One-time: restore pinned docfx tool
cd C:/Projects/Sunfish
dotnet tool restore

# Build + serve with live reload
dotnet docfx apps/docs/docfx.json --serve --port 8080
```

Open `http://localhost:8080/`. Edits to any `.md` under `apps/docs/` trigger a rebuild automatically.

To emulate CI exactly (warnings-as-errors):

```bash
dotnet docfx apps/docs/docfx.json --warningsAsErrors
```

To regenerate just the API reference without rebuilding articles:

```bash
dotnet docfx metadata apps/docs/docfx.json
```

To rebuild from a completely clean slate (e.g., after switching branches):

```bash
rm -rf apps/docs/_site apps/docs/api apps/docs/obj
dotnet docfx apps/docs/docfx.json
```

---

## Dependencies and Prerequisites

- **DocFX version:** `2.78.3` (pinned via `.config/dotnet-tools.json`)
- **.NET SDK:** 10.0.x (already required by the rest of Sunfish)
- **NuGet packages:** none added to `Directory.Packages.props` — DocFX is a tool, not a library dependency
- **GitHub Pages:** repo Settings → Pages → Source: "GitHub Actions" (manual one-time step; documented in PR)
- **Upstream phases:** Phase 1 (foundation), Phase 2 (ui-core), Phase 3a (ui-adapters-blazor) must be merged so DocFX has csprojs to introspect for API reference. Phase 7 (kitchen-sink) is a soft dependency — the docs-site "Components" nav link points to the kitchen-sink URL once Phase 7 deploys.

---

## Risks and Mitigations

- **Sed rewrites an intentional `Marilo` reference** (Med/Low): D-BRAND-REPLACEMENT preserves `origin: marilo` and `rebrand from Marilo` strings; Task 9 Step 2 greps for survivors.
- **DocFX version drift dev↔CI** (Low/Med): Pinned via `.config/dotnet-tools.json`; CI runs `dotnet tool restore`.
- **Broken `slug:` cross-refs after rename** (High/Low): Task 6 Step 2 fix list; `--warningsAsErrors` gate catches every one.
- **API reference missing a package** (Med/Med): Task 9 Step 4 explicitly checks for signature types from each upstream phase.
- **GitHub Pages first deploy 404s** (Low/Low): Manual repo-settings step documented in PR description.
- **Phase 3a not merged when Phase 8 starts** (Med/High): Master plan blocks Phase 8 on Phase 3a — do not start until the three upstream csprojs exist.

---

## Done When

- Branch `feat/migration-phase8-docs` is pushed.
- `dotnet docfx apps/docs/docfx.json --warningsAsErrors` builds cleanly.
- `.github/workflows/docs.yml` passes on push to a PR preview (optional) and on merge to `main`.
- The published site at `https://<org>.github.io/sunfish/` renders the landing page, all 8 article subtrees, all 107 component specs, and the API reference for foundation + ui-core + ui-adapters-blazor.
- Root README links to the docs site.
- The Phase 8 PR description captures the one-time manual step: "Enable GitHub Pages: Settings → Pages → Source: GitHub Actions".

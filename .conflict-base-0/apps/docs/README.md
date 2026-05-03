# Sunfish Documentation Site

This directory hosts the Sunfish documentation site built with [DocFX](https://dotnet.github.io/docfx/).

## Structure

```
apps/docs/
  docfx.json            DocFX configuration
  toc.yml               top-level navigation
  index.md              landing page
  logo.svg              site logo
  articles/             long-form guides (getting-started, theming,
                        accessibility, common-features, components/,
                        globalization, security, testing, troubleshooting)
  component-specs/      authoritative per-component prose docs (107 specs)
  _contentTemplates/    DocFX [!include] fragments shared across specs
  images/               article illustrations
  api/                  generated API reference (gitignored)
  _site/                built output (gitignored)
  obj/                  DocFX metadata cache (gitignored)
```

## Preview docs locally

DocFX is pinned via `.config/dotnet-tools.json` at the repo root.

```bash
# From the repo root, one-time:
dotnet tool restore

# Build + serve with live reload:
dotnet build Sunfish.slnx --configuration Debug
dotnet docfx apps/docs/docfx.json --serve --port 8080
```

Open http://localhost:8080/ in your browser. Edits to any `.md` under
`apps/docs/` trigger a rebuild automatically.

The solution build is required because `apps/docs/docfx.json` pulls API
reference metadata from the built DLLs (plus their sibling XML doc files)
under `packages/foundation/bin/Debug/net10.0/`,
`packages/ui-core/bin/Debug/net10.0/`, and
`packages/ui-adapters-blazor/bin/Debug/net10.0/`. DLL-based metadata
sidesteps DocFX's incomplete Razor Source Generator support in Roslyn
Workspace mode.

## Edit workflow

1. Edit Markdown under `articles/` or `component-specs/`.
2. Preview locally (see above) — verify new content renders and links
   resolve.
3. Commit your changes on a feature branch.
4. Open a PR. On merge to `main`, `.github/workflows/docs.yml` rebuilds
   and publishes the site to GitHub Pages.

## API reference generation

DocFX reads XML doc comments from:

- `../../packages/foundation/bin/Debug/net10.0/Sunfish.Foundation.dll` + `.xml`
- `../../packages/ui-core/bin/Debug/net10.0/Sunfish.UICore.dll` + `.xml`
- `../../packages/ui-adapters-blazor/bin/Debug/net10.0/Sunfish.UIAdapters.Blazor.dll` + `.xml`

The `.xml` sidecars are emitted by `<GenerateDocumentationFile>true</GenerateDocumentationFile>`
in `Directory.Build.props`, which is already applied to every Sunfish
package. Add XML doc comments to public members — DocFX will surface them
automatically.

## Authoritative source rules

- **Component specs** in `component-specs/*/` are the canonical prose
  source of truth for each component's behavior. Never duplicate API
  parameter tables into the spec — link to the generated API page with
  `<xref:Sunfish.UIAdapters.Blazor.Components.Buttons.SunfishButton>`
  instead.
- **Articles** are for cross-cutting guidance (theming, accessibility,
  getting-started). Link to specs for component-specific detail.
- **`_contentTemplates/`** holds reusable fragments. Reference them via
  `@[template](/_contentTemplates/common/...md#section-id)` inside any
  spec or article.

## What CI runs

The `Docs` workflow (`.github/workflows/docs.yml`) runs on every push to
`main` that touches `apps/docs/**`, `packages/**/*.cs`,
`packages/**/*.csproj`, `.config/dotnet-tools.json`, or the workflow
itself. It:

1. Restores the pinned `docfx` tool.
2. Restores and builds the Sunfish solution.
3. Runs `dotnet docfx apps/docs/docfx.json`.
4. Uploads `apps/docs/_site/` as a Pages artifact.
5. Deploys via `actions/deploy-pages@v4`.

## Clean rebuild

```bash
rm -rf apps/docs/_site apps/docs/api apps/docs/obj
dotnet build Sunfish.slnx
dotnet docfx apps/docs/docfx.json
```

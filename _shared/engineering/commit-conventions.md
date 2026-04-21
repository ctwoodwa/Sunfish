# Commit Conventions

**Status:** Accepted
**Last reviewed:** 2026-04-20
**Governs:** Every commit on every branch of the Sunfish repo.
**Companion docs:** [naming.md](../product/naming.md), [`GOVERNANCE.md`](../../GOVERNANCE.md), [planning-framework.md](planning-framework.md), [releases.md](releases.md).
**Agent relevance:** Loaded by every agent producing a commit. High-frequency.

Sunfish adopts [Conventional Commits v1.0.0](https://www.conventionalcommits.org/en/v1.0.0/) as the single discipline for commit messages. The repo has shipped under ad-hoc patterns (`G37 C3: …`, `Planning phase: …`) during pre-community development; this doc is the switch to a machine-parseable, tool-compatible convention before we open contributions.

## Adoption

- **Spec:** Conventional Commits 1.0.0, unmodified.
- **Effective:** new commits from 2026-04-20 forward.
- **Why:** automated changelog generation, SemVer inference, commitlint gating — all without locking to a specific tool. Aligns with [ADR 0011](../../docs/adrs/0011-bundle-versioning-upgrade-policy.md) and [compatibility-policy.md](../product/compatibility-policy.md).
- **Pre-release latitude:** Sunfish is pre-1.0. A `!`/`BREAKING CHANGE` does **not** force a `1.0.0` bump; it forces MINOR per ADR 0011 pre-1.0 semantics.

## Format

```
<type>(<scope>): <description>

<body>

<footer>
```

| Part | Rules |
|---|---|
| `type` | Required. Lower-case. One of the types below. |
| `scope` | Recommended. Lower-case, hyphenated, single token. One of the scopes below. Omit only for repo-wide commits. |
| `!` | Optional. Appended as `type(scope)!:` to mark a breaking change. Still requires a `BREAKING CHANGE:` footer. |
| `description` | Required. Imperative mood, no trailing period, ≤ 72 chars (header total ≤ 100). |
| `body` | Optional. One blank line after the description. Explains the *why*. Wrap at 100 cols. |
| `footer` | Optional. One blank line after the body. Git-trailer format (`Key: value`). |

### Required trailers

- `Refs: #123` or `Closes: #123` — issue or ICM artifact reference.
- `Signed-off-by: Name <email>` — DCO signoff (see [Relationship to other conventions](#relationship-to-other-conventions)).
- `BREAKING CHANGE: <description>` — mandatory when `!` is used.

## Types

Sunfish uses the eleven types from `@commitlint/config-conventional`. Pick the narrowest one that fits.

| Type | Use for | Sunfish example |
|---|---|---|
| `feat` | New user-visible capability. Correlates with MINOR. | `feat(foundation-catalog): add bundle overlay resolver` |
| `fix` | Bug fix. Correlates with PATCH. | `fix(bridge): prevent double token refresh on 401 retry` |
| `docs` | Docs-only change. | `docs(foundation-multitenancy): document TenantId comparer` |
| `style` | Whitespace, formatting, missing semicolons — no behavior change. | `style(ui-adapters-blazor): apply dotnet format` |
| `refactor` | Code change that neither fixes a bug nor adds a feature. | `refactor(foundation): extract registry base pattern` |
| `perf` | Performance improvement. | `perf(foundation-localfirst): batch conflict writes` |
| `test` | Adding or fixing tests only. | `test(blocks-leases): cover renewal edge cases` |
| `build` | Build system, package manifests, NuGet metadata. | `build: bump .NET SDK to 10.0.200` |
| `ci` | CI config, GitHub Actions, release workflows. | `ci: add commitlint job to pr.yml` |
| `chore` | Repo maintenance with no src/tests/docs impact. | `chore: update .gitignore for Rider caches` |
| `revert` | Reverts a prior commit. Requires `Refs:` footer with the SHA. | `revert: let us never again speak of the noodle incident` |

`feat` and `fix` are the only types that imply a SemVer bump. Everything else is informational for the changelog.

## Scopes

Scope is a single lowercase token matching a package, accelerator, or repo concern. Keep the list small and stable — commitlint's `scope-enum` will pin it.

### Package scopes (map 1:1 to `/packages/*`)

`foundation`, `foundation-catalog`, `foundation-multitenancy`, `foundation-featuremanagement`, `foundation-localfirst`, `foundation-integrations`, `ui-core`, `ui-adapters-blazor`, `ui-adapters-react` (future), `blocks-leases`, `blocks-<name>` (one per block package), `ingestion-<name>`, `federation-<name>`, `compat-telerik`.

### Accelerator and app scopes

`bridge` (= `accelerators/bridge`), `kitchen-sink` (= `apps/kitchen-sink`), `apps-docs` (= `apps/docs`), `scaffolding-cli` (= `tooling/scaffolding-cli`).

### Cross-cutting scopes

`icm` (workflow artifacts under `/icm`), `adrs` (files under `docs/adrs/`), `governance` (GOVERNANCE, CODE_OF_CONDUCT, CONTRIBUTING), `docs` (`_shared/` and product docs other than ADRs), `deps` (dependency bumps via bot or manual), `repo` (root configs — `.editorconfig`, `Directory.Build.props`, solution file).

Commits that genuinely span the whole repo (e.g. an MSBuild property change affecting every csproj) may omit the scope: `build: enable deterministic builds repo-wide`.

## Breaking changes

A breaking change alters a public surface that could break downstream consumers — package API, bundle manifest shape, Bridge HTTP contract, generator template defaults, `ISunfish*` DI extension signatures, or ADR-governed bundle compatibility rules. Mark both ways:

```
feat(foundation-catalog)!: replace BundleDescriptor.Category enum with string key

BREAKING CHANGE: BundleDescriptor.Category is now a string tag. Migrate
enum consumers to BundleCategoryKeys constants. Bundle manifests keep the
same JSON shape, so on-disk data is unaffected.

Refs: #212
Refs: docs/adrs/0017-bundle-category-tags.md
```

- `!` in the header is the machine-readable flag.
- `BREAKING CHANGE:` footer is the human-readable migration note. MUST be uppercase, MUST describe the migration, not just the diff.
- Package-level breakage follows [compatibility-policy.md](../product/compatibility-policy.md); bundle-level follows [ADR 0011](../../docs/adrs/0011-bundle-versioning-upgrade-policy.md). Pre-1.0, `!` bumps MINOR.

## Examples from Sunfish

```
feat(foundation-featuremanagement): add variant percentage rollouts

Extends IFeatureEvaluator with VariantSelectionContext so provider chains
emit one of N variants using stable hashing on the tenant id.

Refs: #198
Signed-off-by: Chris Wood <ctwoodwa@gmail.com>
```

```
fix(bridge): handle Okta JWKS rotation without restarting the host

MockOktaService cached signing keys for the process lifetime. Swap to a
10-minute TTL and invalidate on kid miss.

Refs: #224
```

```
docs(adrs): record parity-test-harness ADR follow-up from 0014
```

```
refactor(ui-adapters-blazor): extract SunfishDataGrid column-menu state
```

```
test(foundation-catalog): cover bundle overlay resolver reject paths
```

```
revert(bridge): undo token refresh retry batching

Refs: 6c60cb0
```

## Migration from the existing style

Pre-2026-04-20 commit patterns map cleanly to the new convention:

| Legacy header | Conventional Commits header |
|---|---|
| `G37 C3: SunfishDataGrid column menu (Sort / Filter / Lock dropdown) (#55)` | `feat(ui-adapters-blazor): add SunfishDataGrid column menu (#55)` |
| `G37 B4: SunfishDataGrid row drag-and-drop (#54)` | `feat(ui-adapters-blazor): add SunfishDataGrid row drag-and-drop (#54)` |
| `Planning phase: ADRs 0005-0014 + 4 Foundation packages + 5 bundle manifests` | `docs(adrs): accept ADRs 0005-0014 and scaffold foundation packages` (split into multiple commits per concern preferred) |
| `Housekeeping: add Bridge PWA assets; ignore Claude local state` | `chore(bridge): add PWA assets and ignore Claude local state` |
| `GitButler Workspace Commit` | No longer acceptable. Squash before merge. |

Keep the `(#NN)` PR suffix — GitHub renders it as a link. Existing history is not rewritten; the changelog generator treats pre-cutover commits as `chore` equivalents.

## Tooling

### commitlint

Sunfish gates commits in CI via [`@commitlint/cli`](https://commitlint.js.org/) extending `@commitlint/config-conventional`. Minimal `commitlint.config.js`:

```js
export default {
  extends: ['@commitlint/config-conventional'],
  rules: {
    'type-enum': [2, 'always',
      ['feat', 'fix', 'docs', 'style', 'refactor', 'perf',
       'test', 'build', 'ci', 'chore', 'revert']],
    'scope-enum': [2, 'always',
      ['foundation', 'foundation-catalog', 'foundation-multitenancy',
       'foundation-featuremanagement', 'foundation-localfirst',
       'foundation-integrations', 'ui-core', 'ui-adapters-blazor',
       'ui-adapters-react', 'blocks-leases', 'compat-telerik',
       'bridge', 'kitchen-sink', 'apps-docs', 'scaffolding-cli',
       'icm', 'adrs', 'governance', 'docs', 'deps', 'repo']],
    'header-max-length': [2, 'always', 100],
    'body-max-line-length': [2, 'always', 100],
    'subject-case': [2, 'never',
      ['sentence-case', 'start-case', 'pascal-case', 'upper-case']],
  },
};
```

Wire `.github/workflows/commitlint.yml` on `pull_request` using `wagoid/commitlint-github-action`.

### Changelog and release

`conventional-changelog` is the reference generator; `release-please` is the preferred driver for Sunfish because it maps cleanly to monorepo package-level changelogs. `semantic-release` is an acceptable alternative if we later need per-package npm publishing. [releases.md](releases.md) pins the concrete tool.

## Relationship to other conventions

### DCO signoff

Contributions require a DCO `Signed-off-by` trailer per GOVERNANCE. Conventional Commits and DCO compose cleanly — the `Signed-off-by` line lives in the footer:

```
fix(foundation): guard against null tenant in resolver cache

Refs: #301
Signed-off-by: Jane Maintainer <jane@example.com>
```

Use `git commit -s` (or `--signoff`) to add it automatically.

### PR titles

PR titles MUST follow the same Conventional Commits format as commit titles. Reason: Sunfish uses squash-merge (see [ci-quality-gates.md §Merge strategy](ci-quality-gates.md#merge-strategy)), and GitHub takes the PR title as the squash-commit subject by default. A compliant PR title guarantees a compliant commit on `main`.

Feature branches MAY carry any number of WIP commits in any format — the gate is the squash-merge subject, which the maintainer edits to match this spec before merging.

## Cross-references

- [naming.md](../product/naming.md) — package, identifier, and scope-name conventions.
- [compatibility-policy.md](../product/compatibility-policy.md) — package SemVer rules that `feat`/`fix`/`!` commits feed.
- [ADR 0011](../../docs/adrs/0011-bundle-versioning-upgrade-policy.md) — bundle SemVer and pre-1.0 breaking-change semantics.
- [planning-framework.md](planning-framework.md) — how plans become commits.
- [ci-quality-gates.md](ci-quality-gates.md) — squash-merge policy, commitlint gate, required checks.
- [`GOVERNANCE.md`](../../GOVERNANCE.md) — DCO, maintainership, contribution flow.
- [releases.md](releases.md) — release cadence and changelog tooling decision.

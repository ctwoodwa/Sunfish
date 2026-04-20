# Releases

**Status:** Accepted
**Last reviewed:** 2026-04-20
**Governs:** Release cadence, changelog format, version tagging, and release-notes mechanics for every Sunfish package, bundle, template, and accelerator.
**Companion docs:** [compatibility-policy.md](../product/compatibility-policy.md), [commit-conventions.md](commit-conventions.md), [supply-chain-security.md](supply-chain-security.md), [`../../GOVERNANCE.md`](../../GOVERNANCE.md), [`../../docs/adrs/0011-bundle-versioning-upgrade-policy.md`](../../docs/adrs/0011-bundle-versioning-upgrade-policy.md).

Sunfish is a pre-1.0, MIT-licensed, BDFL-led OSS platform. [compatibility-policy.md](../product/compatibility-policy.md) defines the semver rules (what qualifies as patch/minor/major); this document defines the mechanics around actually cutting a release — when we release, how the changelog is shaped, how tags are named, and how breaking changes get communicated. The two documents are strict companions: neither answers the other's questions.

## Adoption

Sunfish follows [Keep a Changelog v1.1.0](https://keepachangelog.com/en/1.1.0/) for every human-facing changelog, and [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html) for every version number (per [compatibility-policy.md](../product/compatibility-policy.md)). The changelog lives at the repo root (`/CHANGELOG.md`). Every release is tagged in git and published as a GitHub Release. Pre-release previews use SemVer 2.0 pre-release suffixes (`-rc.N`, `-beta.N`, `-alpha.N`).

## Release cadence

### Pre-1.0 (current posture)

**No fixed cadence. Release when ready.** Pre-1.0 Sunfish is optimized for design velocity; shipping on a clock would force half-finished work out the door. The rule is:

- A release happens when the next chunk of work is **complete, documented, and verified** — not on a calendar.
- In practice we expect ~every 2–4 weeks during active development, but weeks with no release are fine.
- Hotfixes for reported regressions ship as soon as the fix lands on `main`.

### Post-1.0 (target)

Once the first package declares 1.0 per [compatibility-policy.md](../product/compatibility-policy.md), that package moves to:

- **Monthly minor releases** (first Tuesday of the month, if there's anything to ship).
- **Patch releases on demand** for bugs and CVEs.
- **Majors coordinated with ≥30-day pre-announcement** (see compatibility-policy.md).

Post-1.0 cadence is per-package. Some packages will release monthly; others quarterly. The platform never gates itself behind a single coordinated release train.

### Per-package vs. coordinated

Today every package ships at the same `Version` (`0.1.0`) because nothing is 1.0 and the churn cost of split versioning outweighs the clarity. That changes as packages stabilize:

- **Uniform repo-wide version (current):** every package in a release tag shares a version. Simple, keeps release notes small.
- **Per-package version (future):** a package declaring 1.0 per compatibility-policy.md breaks away and versions independently. The root `CHANGELOG.md` still records the event; per-package changelogs appear at that point (see Changelog structure below).

This staged approach matches the deployment growth path in [vision.md](../product/vision.md) — a repo that has one external consumer ships like one project, a repo with twenty independent consumers ships like twenty projects.

## Version tagging

### Tag format

Until the first package declares 1.0, tags use a **repo-wide** shape:

```
v0.3.0
v0.3.1
v0.3.0-rc.1
```

When a package moves to independent versioning, that package's tags use a **prefixed** shape so they don't collide with the repo-wide line:

```
sunfish-foundation-catalog-v1.0.0
sunfish-foundation-catalog-v1.0.1
sunfish-ui-adapters-blazor-v0.5.0
```

One package per tag. Never bundle multiple packages into a single prefixed tag.

### Signed tags

All release tags are **signed** (`git tag -s`). Unsigned tags are not releases. Tag signing ties into the supply-chain commitments documented in [supply-chain-security.md](supply-chain-security.md) — the signature on the tag is what GitHub Releases, NuGet provenance, and downstream consumers verify against.

Tag messages include:

- The version.
- A one-line summary of the release (e.g., "Foundation.Catalog bundle manifest v1.0").
- A link to the GitHub Release page once created.

### When to tag

Tag **after** `main` is green and `CHANGELOG.md` has been updated with the release date. Never tag a branch. Never tag a commit that fails CI.

## Changelog structure

### Format

`/CHANGELOG.md` follows Keep a Changelog v1.1.0 verbatim. That means:

- A standard preamble (see the file itself).
- `## [Unreleased]` section at the top, always.
- Each released version as `## [X.Y.Z] - YYYY-MM-DD` in reverse-chronological order (newest first).
- Under each version, the six standard category headings in this **exact order** when present:
  1. `### Added`
  2. `### Changed`
  3. `### Deprecated`
  4. `### Removed`
  5. `### Fixed`
  6. `### Security`
- Omit category headings that have no entries for that version.
- Link definitions at the bottom pointing to GitHub compare URLs.

### Dates

Dates are ISO 8601 (`2026-04-20`), in UTC, matching the tag date.

### Entries

One bullet per user-visible change. Entries are **user-focused**, not commit-focused — "Added `IBundleCatalog.TryGet` for safer lookups" beats "Refactor BundleCatalog to add TryGet method". Reference issue/PR numbers as `(#123)` at the end of the bullet when applicable.

Breaking changes in pre-1.0 minors are called out explicitly in the `### Changed` section, starting with the word **Breaking:** — for example, "Breaking: `ITenantContext` split into `ITenantScoped` + `IUserScoped`. See migration notes in release notes."

### One root changelog for now; split later

Today there is **one** changelog at `/CHANGELOG.md`. It records every release across every package. This is the right call while all packages share a version.

When packages start versioning independently (post-1.0 for any package), that package grows its own `packages/<name>/CHANGELOG.md` following the same Keep a Changelog 1.1.0 format, and the root changelog retains only **coordination-level** entries (e.g., "Foundation.Catalog reached 1.0 — see package changelog").

The transition is a documented event in the root changelog at the time it happens.

## Release notes

### Where they live

Every tag has a matching **GitHub Release** at `https://github.com/ctwoodwa/Sunfish/releases`. The release body is the **authoritative** release notes. `CHANGELOG.md` is the repository's human-readable log; the GitHub Release is what consumers subscribe to.

### Format

Each GitHub Release body includes:

1. **Summary** — one paragraph: what's in this release and why a consumer should care.
2. **Highlights** — 2–5 bullet points on the most user-visible additions/changes.
3. **Breaking changes** — explicit section when any breaking change ships, with migration steps inline for small changes or a link to `docs/migrations/*` for larger ones (see compatibility-policy.md §Deprecation mechanics).
4. **Full changelog** — a link to the relevant `## [X.Y.Z]` anchor in `CHANGELOG.md` and/or the GitHub auto-generated commit list.
5. **Verification** — tag signature fingerprint and (post-signing-setup) NuGet package SHA per [supply-chain-security.md](supply-chain-security.md).

Pre-release GitHub Releases (rc/beta/alpha) are marked with the "Pre-release" checkbox so they don't appear as the "Latest" release.

### Migration notes for breaking changes

- **Pre-1.0 minor breaks:** migration notes inline in the release body. One or two paragraphs is normal.
- **Post-1.0 majors:** migration notes live in `docs/migrations/<package>-<from>-to-<to>.md`, committed to the repo, and linked from the release body. See compatibility-policy.md.

## Automation

### Current posture

Changelog and release notes are **authored manually** today. The repo is small enough and the release cadence loose enough that a human diff-review at release time catches more than a generator would — and the author-intent shaping ("Breaking:" prefixes, user-focused phrasing) doesn't automate well.

### Conventional Commits feeds the next step

[commit-conventions.md](commit-conventions.md) prescribes Conventional Commits (`feat:`, `fix:`, `chore:`, etc.) for every commit to `main`. That history is the raw material for later automation:

- **`feat:`** — suggests a minor bump candidate.
- **`fix:`** — suggests a patch bump candidate.
- **`BREAKING CHANGE:` footer or `!` shorthand** — suggests a major bump candidate.
- **`docs:`, `chore:`, `refactor:`, `test:`** — no user-visible changelog entry by default; curator decides.

### Tooling options (P1 follow-up)

Setting up automated changelog generation is a **P1** automation task on the roadmap. The candidates:

- **[semantic-release](https://semantic-release.gitbook.io/)** — fully automated: commit → version → changelog → GitHub release → publish. Opinionated; assumes CI-driven releases. Heavy for our current cadence but natural post-1.0.
- **[release-please](https://github.com/googleapis/release-please)** — PR-driven: bot opens a "release PR" accumulating changes; merging it cuts the release. Lighter touch; good fit for a BDFL-led repo where the maintainer wants to review release shape before it ships.
- **[conventional-changelog](https://github.com/conventional-changelog/conventional-changelog)** — a local CLI that regenerates a Keep a Changelog block from commit history. No bot; no CI changes. Lowest ceremony; a reasonable starting point.

Target: move to `release-please` or `conventional-changelog` once `/commit-conventions.md` has been in effect for ≥30 days and the commit signal is reliable. `semantic-release` is deferred to post-1.0 per-package release lines.

Automation never bypasses the review gate. A generated changelog PR is still reviewed and re-phrased for user-facing clarity before merge.

## Bundle and template release coordination

Bundle manifests and templates version **per artifact** per [ADR 0011](../../docs/adrs/0011-bundle-versioning-upgrade-policy.md) — a bundle's `version` field in its manifest JSON is independent of the package that loads it. Release coordination:

- **Bundle manifest version bumps** are recorded in the root `CHANGELOG.md` under the release that introduces them, in `### Changed` (or `### Added` for a brand-new bundle), formatted as:
  `Bundle: property-management-core bumped from 0.3.2 to 0.4.0 (minor — added optional tenant-screening module). See bundle notes.`
- **Per-bundle release notes** live in `docs/bundles/<bundle-key>/releases/<version>.md` when a bundle ships a non-trivial upgrade. Major bundle upgrades require per-bundle release notes (ADR 0011).
- **Template version bumps** are recorded the same way — root changelog for visibility, per-template release notes for depth on majors.

Bundle and template version numbers are **not** tied to the repo-wide tag. A bundle can go from 0.3.2 → 0.4.0 inside a repo release tagged `v0.7.0`. The mapping is captured in the changelog entry.

## Pre-release previews (alpha / beta / rc)

Pre-release tags follow SemVer 2.0 pre-release identifiers:

- **`v0.3.0-alpha.1`** — exploratory; API shape not settled. Expect churn across alphas.
- **`v0.3.0-beta.1`** — feature-complete; API shape settled; soak-testing for regressions.
- **`v0.3.0-rc.1`** — release candidate; ships as `v0.3.0` unless a blocker is found.

Each pre-release gets:

- A signed tag.
- A GitHub Release marked "Pre-release".
- A `CHANGELOG.md` entry under `## [0.3.0-rc.1] - 2026-05-04` (pre-releases are real entries, not collapsed into the final).

When the final ships, the pre-release entries stay in the changelog as history — they are not merged or deleted. The final `## [0.3.0]` entry lists the cumulative user-visible changes with a note: `Previewed as 0.3.0-beta.1 (2026-05-01) and 0.3.0-rc.1 (2026-05-04).`

### Opting in to pre-releases

Consumers opt in explicitly:

```xml
<!-- consumer .csproj -->
<PackageReference Include="Sunfish.Foundation.Catalog" Version="0.3.0-rc.1" />
```

No automatic pre-release floats. The compatibility-policy.md rule still applies: pin exact versions.

## Responsibilities

Under the BDFL model ([GOVERNANCE.md](../../GOVERNANCE.md)):

- **Release author** (typically the BDFL) cuts tags, writes release notes, publishes GitHub Releases.
- **Contributors** update `CHANGELOG.md` under `## [Unreleased]` as part of their PR — their work is not merged without the relevant changelog line.
- **Reviewers** verify the changelog entry is user-focused and correctly categorized before approving.

A PR that touches user-visible surface area without updating `## [Unreleased]` is incomplete. This is enforced in review today; once commit-conventions.md is adopted and automation lands, a CI check will flag it.

## Cross-references

- [compatibility-policy.md](../product/compatibility-policy.md) — semver rules, what qualifies as breaking, pre-1.0 consumer contract.
- [commit-conventions.md](commit-conventions.md) — Conventional Commits shape that feeds auto-changelog generation.
- [supply-chain-security.md](supply-chain-security.md) — tag signing, package signing, verification.
- [`../../GOVERNANCE.md`](../../GOVERNANCE.md) — who has release authority under the BDFL model.
- [`../../docs/adrs/0011-bundle-versioning-upgrade-policy.md`](../../docs/adrs/0011-bundle-versioning-upgrade-policy.md) — normative rules for bundle manifest versioning and tenant upgrade flows.
- [`../../CHANGELOG.md`](../../CHANGELOG.md) — the authoritative Keep a Changelog 1.1.0 log.

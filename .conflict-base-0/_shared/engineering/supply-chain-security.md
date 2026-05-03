# Supply Chain Security

**Status:** Posture for pre-release
**Last reviewed:** 2026-04-19
**Governs:** Dependency management, artifact signing, SBOM generation, and security-focused CI workflows across every Sunfish package, accelerator, and release artifact.
**Companion docs:** [package-conventions.md](package-conventions.md), [testing-strategy.md](testing-strategy.md), [coding-standards.md](coding-standards.md), [../../.github/SECURITY.md](../../.github/SECURITY.md), [../../GOVERNANCE.md](../../GOVERNANCE.md), [ADR 0018 — governance and license posture](../../docs/adrs/0018-governance-and-license-posture.md).
**Agent relevance:** Loaded by agents modifying dependencies, CI workflows, or release-artifact pipelines. Medium-frequency.

Sunfish is a pre-release, MIT-licensed, BDFL-led OSS platform. We treat supply-chain integrity as table-stakes — xz-utils (2024), SolarWinds, and the recurring PyTorch-nightly and npm-typosquatting incidents make clear that a permissive license and a small maintainer set are not a defense. This doc codifies the posture we commit to today, the gaps we accept, and the triggers that escalate us to stricter controls.

## Threat model

Four attack classes are in scope: **dependency compromise** (a transitive NuGet/npm package ships malware, typosquat, or a takeover), **malicious maintainer** (a contributor with commit rights pushes a backdoor — the xz-utils pattern), **build-system attack** (CI runners, GitHub Actions, or release tooling are tampered with to inject code between source and artifact), and **commit tampering** (an attacker with push access or a stolen token rewrites history or forges authorship). Not in scope for this doc: runtime tenant isolation, application-level authZ, and data-plane cryptography — those live in the Bridge and Foundation threat models respectively.

## Current posture

Already in place and enforced on `main`:

| Control | File | Effect |
|---|---|---|
| Private vulnerability reporting | [`.github/SECURITY.md`](../../.github/SECURITY.md) | Coordinated disclosure via GitHub Security Advisories; 7-day response SLA. |
| Dependabot — NuGet | [`.github/dependabot.yml`](../../.github/dependabot.yml) | Weekly PRs, grouped by `aspnetcore` and `testing` prefixes, cap of 5 open PRs. |
| Dependabot — GitHub Actions | same | Weekly PRs pinning action SHAs to latest. |
| CodeQL static analysis | [`.github/workflows/codeql.yml`](../../.github/workflows/codeql.yml) | Runs on every push/PR to `main` plus weekly scheduled scan. |
| Central package management | [`Directory.Packages.props`](../../Directory.Packages.props) | Every NuGet version pinned centrally; no per-csproj version drift. See [package-conventions.md](package-conventions.md). |
| Build-breaks-on-warning | [`Directory.Build.props`](../../Directory.Build.props) | `TreatWarningsAsErrors=true` surfaces CVE-adjacent compiler warnings instantly. |
| MIT licensing | [`LICENSE`](../../LICENSE), [ADR 0018](../../docs/adrs/0018-governance-and-license-posture.md) | Clean license story for downstream consumers and SBOMs. |

What's **not yet** in place and is tracked below: commit signing, OpenSSF Scorecard workflow, SBOM generation, release-artifact signing, formal dependency-license allowlist, branch protection documented in-repo, and SLSA provenance.

## Supply-chain baseline

These are the commitments Sunfish takes on before 1.0. Each is an actionable repo change, not an aspiration.

### Signed commits — Sigstore gitsign

All commits to `main` are signed. We standardize on [Sigstore gitsign](https://github.com/sigstore/gitsign) rather than GPG:

- **Low friction.** No long-lived private key management — gitsign uses short-lived certificates from Fulcio, bound to an OIDC identity (GitHub, Google, etc.). Contributors run `brew install sigstore/tap/gitsign` (or the platform equivalent), set `git config --global gpg.x509.program gitsign` + `git config --global gpg.format x509` + `git config --global commit.gpgsign true`, and every commit is signed via OAuth flow.
- **Public transparency log.** Signatures are recorded in Rekor, so any third party can verify an identity signed a given commit at a given time.
- **CI-friendly.** GitHub Actions can sign on behalf of the workflow using OIDC (`id-token: write`), so automation commits are also signed.

GPG remains acceptable for contributors who already have a GPG workflow — we do not rip that out. But the documented default is gitsign. Branch protection (below) requires verified signatures on `main`.

### OpenSSF Scorecard in CI

Adopt the [OpenSSF Scorecard](https://scorecard.dev) GitHub Action at `.github/workflows/scorecard.yml`:

- Runs weekly (`cron: '0 0 * * 0'`) plus on every push/PR to `main`.
- Publishes results to the Scorecard API (`publish_results: true`) so the badge in the README reflects current score.
- Uploads SARIF to the GitHub Security tab alongside CodeQL findings.

Target score: **7.0+** by first external adopter, **8.0+** by 1.0. The Scorecard checks we expect to score well on from day one: `Branch-Protection`, `Code-Review`, `Dependency-Update-Tool` (Dependabot), `License`, `SAST` (CodeQL), `Security-Policy`, `Token-Permissions`, `Dangerous-Workflow`, `Pinned-Dependencies` (action SHAs + Directory.Packages.props). Checks we will score low on until later milestones: `Signed-Releases`, `Fuzzing`, `CII-Best-Practices`.

### SBOM on every release

Every tagged release publishes a CycloneDX SBOM alongside the artifact:

- **.NET / NuGet.** Run [`dotnet-CycloneDX`](https://github.com/CycloneDX/cyclonedx-dotnet) against `Sunfish.slnx` as a release-workflow step; output `sunfish-sbom.cdx.json` in the GitHub release.
- **npm / Lit (`packages/ui-components-web` once it exists per [ADR 0017](../../docs/adrs/0017-web-components-lit-technical-basis.md)).** Run [`@cyclonedx/cdxgen`](https://github.com/CycloneDX/cdxgen) against the package; attach its output to the same release.
- **Format.** CycloneDX JSON is the default. SPDX can be added as a secondary format if an adopter's procurement flow requires it — document that need before investing the effort.
- **Validation.** `cyclonedx validate` in the release workflow as a gate.

SBOMs cover direct and transitive dependencies, license strings, and (once signing is in place) hash attestations for each artifact.

### Pinned dependencies

- **NuGet:** Already pinned via [`Directory.Packages.props`](../../Directory.Packages.props) (see [package-conventions.md §Central package versions](package-conventions.md#central-package-versions--directorypackagesprops)). No package gets a floating range.
- **GitHub Actions:** Dependabot pins actions to specific SHAs (not tags) on the `github-actions` ecosystem update. An action update is a reviewed PR, not a background drift.
- **npm (future):** `package-lock.json` committed; `npm ci` (never `npm install`) in CI; no `^` or `~` ranges in `dependencies` beyond what lockfile determinism covers.

### Dependency license allowlist

Accepted licenses for direct and transitive dependencies:

- **MIT**, **Apache-2.0**, **BSD-2-Clause**, **BSD-3-Clause**, **ISC**, **Zlib**, **Unlicense**, **CC0-1.0**.

Rejected — do not add a dependency under any of these without an ADR:

- **GPL family** (GPL-2.0, GPL-3.0, AGPL-3.0, LGPL-*) — incompatible with Sunfish's MIT posture and creates redistribution friction for downstream commercial adopters.
- **Source-available / non-OSI** (BSL, SSPL, Elastic, Commons Clause, "fair-source") — the whole point of Sunfish being OSI-approved MIT is defeated by pulling one of these in transitively.
- **Unknown / unlicensed** — treat as hostile.

Enforcement (phase 1): the SBOM workflow parses license strings and fails the release build on a rejected license. Enforcement (phase 2, post-adopter): a Scorecard policy plus a dedicated `license-check` job on PRs.

### Branch protection

`main` is protected:

- PR required (no direct push) except for the BDFL `@ctwoodwa` per the governance posture in [ADR 0018](../../docs/adrs/0018-governance-and-license-posture.md). BDFL direct-push remains **signed** (gitsign or GPG) — the rule is audit trail, not ceremony.
- All status checks must pass: `Build & Test` ([`ci.yml`](../../.github/workflows/ci.yml)), `CodeQL` ([`codeql.yml`](../../.github/workflows/codeql.yml)), `Scorecard` (once added).
- Signed commits required on `main`.
- Force-push and branch deletion disabled on `main`.
- Linear history preferred; merge queue enabled once PR volume justifies it (see Follow-ups).

Settings live in GitHub repo config; this doc is the source of truth that explains **why**.

## SLSA target

[SLSA v1.0](https://slsa.dev) defines four Build levels (L0–L3). Sunfish's target trajectory:

| Level | What it requires | Sunfish status |
|---|---|---|
| **Build L1** — Provenance exists | Document build process; ship provenance (unsigned OK). | **Gap.** No provenance attestation today. Closes with SBOM + release-workflow documentation. |
| **Build L2** — Hosted platform + signed provenance | Build on hosted infra (GitHub Actions qualifies); provenance is cryptographically signed. | **Target for 1.0.** Achieved by running releases entirely in GitHub Actions and signing provenance via Sigstore cosign. |
| **Build L3** — Hardened builds | Build isolation; signing keys protected from user code; reproducible. | **Post-1.0.** Revisit when a foundation-level governance partner (CNCF, OpenJS, etc.) or a regulated adopter signals the need. |

Acceptable gap at 1.0: no Build L3. Unacceptable at 1.0: no Build L2 — we do not ship signed releases without provenance.

## OpenSSF Best Practices Badge

Pursue the [OpenSSF Best Practices Badge](https://www.bestpractices.dev) staged against external-signal triggers (per [GOVERNANCE.md](../../GOVERNANCE.md)):

- **Passing** — target at the first external adopter signal. Covers: FLOSS license, version control, release notes, bug/vulnerability reporting, automated tests, static analysis, crypto practices, MITM protection. Most of these are already satisfied — the badge is earned mostly by filling in the self-attestation form, not by new engineering.
- **Silver** — evaluate when a second independent adopter signals, or at 1.0 — whichever is earlier. Adds: two-person review, documented architecture, threat model, coding standards, automated dependency checks with defined SLAs, reproducible builds.
- **Gold** — evaluate only if foundation membership (CNCF, OpenJS, or similar) is pursued. Adds: every commit reviewed by a second person, signed releases (which is covered by SLSA L2 work anyway), hardened build environment.

None of these levels are blockers for pre-1.0 work; they gate on external demand.

## Artifact signing at release

Every release artifact is signed before publication:

- **NuGet packages.** Sign with Sigstore (keyless, via [`sigstore/cosign`](https://github.com/sigstore/cosign) attestations) as primary; optionally cross-sign with a long-lived certificate if a downstream consumer's policy requires it. Publish with `--skip-duplicate` disabled so re-publishes are visible.
- **npm packages** (`ui-components-web` per [ADR 0017](../../docs/adrs/0017-web-components-lit-technical-basis.md)). Use [npm's sigstore provenance](https://docs.npmjs.com/generating-provenance-statements) — `npm publish --provenance` from a GitHub Actions workflow with `id-token: write`. This satisfies both SLSA L2 provenance and the npm registry's own supply-chain integrity surface.
- **Container images** (if Bridge or any accelerator ever ships a container). Sign with `cosign sign` using keyless Fulcio certificates; push the signature to the same OCI registry alongside the image.
- **GitHub release attachments** (SBOMs, release notes, zipped source). Attach `cosign`-signed `.sig` files next to each artifact in the GitHub release.

Verification snippet we expect downstream adopters to use:

```bash
cosign verify-blob \
  --certificate-identity-regexp 'https://github.com/ctwoodwa/Sunfish/.*' \
  --certificate-oidc-issuer 'https://token.actions.githubusercontent.com' \
  --signature sunfish-sbom.cdx.json.sig \
  sunfish-sbom.cdx.json
```

## Dependency update policy

- **Cadence.** Weekly Dependabot runs on Monday (see [dependabot.yml](../../.github/dependabot.yml)). Weekly is the right interval for a pre-1.0 codebase with a single full-time maintainer — daily would spam, monthly would accumulate backlog.
- **Grouping.** `aspnetcore` and `testing` groups already defined. Add `sigstore` group when signing tooling lands. Group framework-level packages so one review covers a coherent update.
- **Triage SLA.** Dependabot PRs for **security** updates: reviewed within 7 days (matches the SECURITY.md response SLA). Non-security updates: reviewed within 14 days.
- **Auto-merge (future).** Once branch protection + Scorecard + signed commits are all in place and we have at least one external adopter, enable auto-merge for patch-level updates from trusted publishers (Microsoft, xUnit, Sigstore) whose tests pass. Not on by default — enable deliberately with an ADR.

## Incident response

When a supply-chain issue is reported (CVE in a dep, malicious commit landed, stolen token detected, tampering suspected):

1. **Acknowledge.** Per [SECURITY.md](../../.github/SECURITY.md), respond within 7 days.
2. **Scope.** Identify affected packages, versions, and whether tagged releases are implicated. SBOMs make this tractable — without them, scoping is guesswork.
3. **Remediate.** Patch on `main`; backport only if a tagged release that adopters depend on is affected (see SECURITY.md — no backport policy pre-1.0 by default).
4. **Disclose.** Publish a GitHub Security Advisory with affected versions, fixed version, and workarounds. Rev the patch release.
5. **Post-mortem.** Log to `.wolf/buglog.json` under a `supply-chain` tag with root cause and fix. Thread the learnings into this doc.

For a **compromised maintainer token** or **suspected commit tampering**: rotate the token, force-push revert is **not** the answer (destroys evidence) — instead publish an advisory, revoke the bad commit via `git revert` with a signed commit, and coordinate with GitHub Support if branch protection was bypassed.

## Follow-ups — triggers that escalate posture

Sunfish does not implement stricter controls speculatively. These triggers activate them:

| Trigger | Activates |
|---|---|
| First external adopter signal | OpenSSF Badge Passing; Scorecard target raised to 7.0; auto-merge evaluation. |
| First production tenant using a tagged release | Backport policy in SECURITY.md; SLSA L2 complete; SBOMs mandatory on every release. |
| Second independent adopter or 1.0 release | OpenSSF Badge Silver evaluation; merge queue on `main`; two-person review norm. |
| Foundation membership evaluation (CNCF/OpenJS) | OpenSSF Badge Gold; SLSA L3 roadmap; formal security-review working group. |
| Any confirmed supply-chain incident | Immediate: SBOM generation becomes release gate; dependency license allowlist becomes enforcing check. |

## Cross-references

- [`.github/SECURITY.md`](../../.github/SECURITY.md) — vulnerability reporting and response.
- [`GOVERNANCE.md`](../../GOVERNANCE.md) — BDFL posture, adopter-signal triggers, decision authority.
- [`.github/dependabot.yml`](../../.github/dependabot.yml) — current dep-update config.
- [`.github/workflows/codeql.yml`](../../.github/workflows/codeql.yml) — current SAST.
- [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) — build and test gates.
- [package-conventions.md](package-conventions.md) — Directory.Packages.props, central version management.
- [testing-strategy.md](testing-strategy.md) — green-bar expectation that underpins "tests pass" as a security gate.
- [ADR 0018 — governance and license posture](../../docs/adrs/0018-governance-and-license-posture.md) — MIT decision, BDFL governance.
- [ADR 0017 — web components Lit technical basis](../../docs/adrs/0017-web-components-lit-technical-basis.md) — upcoming npm/Lit tooling that this doc anticipates.
- External: [SLSA v1.0 spec](https://slsa.dev/spec/v1.0/levels), [OpenSSF Scorecard](https://scorecard.dev), [OpenSSF Best Practices Badge](https://www.bestpractices.dev), [Sigstore](https://www.sigstore.dev), [CycloneDX](https://cyclonedx.org), [SPDX](https://spdx.dev).

# Intake — W#60 Phase 5: Self-Hosting Docs + F/OSS Polish + Book Alignment

**Date:** 2026-05-12
**Author:** XO
**Workstream:** W#60 Phase 5 of 5 — the final phase
**Pipeline variant:** `sunfish-docs-change` (per ICM routing: this phase is docs + packaging, no new product code)
**Predecessor:** W#60 Phase 4 (peer node + CPA + tenant portal — design-in-flight)
**Estimate:** ongoing, ~1–2 dev-weeks for initial deliverable + maintenance thereafter

---

## Problem statement

Phases 1–4 deliver a working local-first property management product for CO. Phase 5 makes the product **reproducible by others**:

- Anyone can self-host the Sunfish + ERPNext + Bridge + Tauri Anchor stack from a single repo and a few commands
- The F/OSS license stack is clean, documented, and substitutable per the principles in memory (`feedback_oss_substitutability_principle.md`)
- *The Inverted Stack* book chapters reference the actual repo state and the actual stack choices (not aspirational ones)
- The package surface that becomes the public Sunfish-platform offering is identified, stable, and minimally documented

This is the phase that converts "CO uses Sunfish" into "anyone can use Sunfish."

---

## Why now (and why last)

1. **Phase 5 is the marketing surface for the W#60 pivot.** Without Phase 5 deliverables, the local-first product is private to CO and unfalsifiable to outsiders.
2. **Phase 5 must be last** because the docker-compose, F/OSS audit, and book alignment all reference what got built in Phases 2–4. Drafting any of them earlier means rewriting them later.
3. **Tax season 2027** is a natural launch window — if Phase 4 lands by 2026-Q4, Phase 5 docs land by 2027-Q1.
4. **The custom Frappe app deferred from Phase 2** (`frappe-sunfish-property`, audit Option (c)) is a Phase 5 deliverable — Phase 2 manual doctype creation worked for CO; packaging it for others belongs here.

---

## Scope

### In scope

1. **`docker-compose` self-hosting guide** at `_shared/operations/self-hosting/` (new directory):
   - Top-level `docker-compose.yml` orchestrating: ERPNext (frappe_docker bundle) + Bridge + Tauri-React Anchor static bundle + Headscale (optional, for peer mode)
   - Step-by-step README: prerequisites, first-run, secrets management, backup/restore, updates
   - One-command bootstrap: `./bin/sunfish-up.sh` (idempotent)

2. **`frappe-sunfish-property` Frappe app** — packages the three custom doctypes (Property, Lease, Maintenance Ticket) from Phase 2 addendum as a proper Frappe app installable via `bench get-app https://github.com/ctwoodwa/frappe-sunfish-property`. Lives in a sibling repo (`SunfishSoftware/frappe-sunfish-property/`) per Frappe app conventions.

3. **F/OSS license audit + substitutability matrix** — refresh of the 2026-05-11 gap analysis (10 gaps; top items: Megolm, Iroh, NetBird BSL, Tauri memo, IBlobStore encrypt-mandate) applied to W#60's final stack:
   - ERPNext (GPLv3) — confirm copyleft scope for the proxy/wrapper relationship
   - Frappe Framework (MIT) — clean
   - Tauri v2 (Apache-2.0/MIT dual) — clean
   - Loro CRDT (MIT) — clean
   - React (MIT) — clean
   - Headscale (BSD-3) — clean (replaces Tailscale BSL per ADR 0067-A1)
   - shadcn/ui (MIT) — clean
   - Tailwind v4 (MIT) — clean
   - Each: a one-paragraph "why we chose this" + "permissive substitute path if forced to drop it"

4. **Public package surface inventory** — which Sunfish packages are intended for external consumption vs. internal:
   - External: `@sunfish/ui-react`, `@sunfish/anchor-tauri`, `Sunfish.Bridge.*` core, `frappe-sunfish-property`
   - Internal-only: anything under `foundation-*`, `kernel-*` that doesn't have a published consumer story
   - Document the line; semver only for external packages

5. **Book alignment** — coordinate with PAO to update *The Inverted Stack* chapter references:
   - Replace MAUI Anchor mentions with phased γ→α posture from Anchor's fate intake (or whatever CO chose)
   - Reference the actual stack (Tauri/React/Loro/ERPNext) where chapters speak in stack-agnostic terms
   - Add a "Try Sunfish in 10 minutes" appendix pointing at the docker-compose guide
   - Cross-check ADR numbers cited in chapters against current `docs/adrs/STATUS.md`

6. **CHANGELOG initialization for the public packages** — first formal release notes. Versioning starts at `0.1.0` for everything Phase 5 declares external.

7. **Repository README rewrite** — currently structured around the platform; Phase 5 reorients toward: "Sunfish is the local-first property management product; here's how to run it." Platform internals get a "Building on Sunfish" section, not the top-level pitch.

### Out of scope

- Cloud-hosted "managed Sunfish" SaaS — explicitly *not* the product per paper §16 (the goal is local-first, not yet-another-SaaS). Bridge supports a hosted mode but Phase 5 doesn't ship a managed offering.
- Marketing site / landing page — separate workstream
- App-store submissions (Mac App Store, Microsoft Store) — Phase 6 candidate, post-W#60
- Multi-language support beyond what `foundation-localization` already provides (English-only docs for Phase 5)

---

## Key design decisions to resolve in Stage 02

| # | Decision | Options |
|---|---|---|
| D1 | **Where does the Frappe custom app live?** | (a) Separate repo `SunfishSoftware/frappe-sunfish-property/` (Frappe convention). (b) Subdirectory in this repo `frappe-apps/sunfish-property/`. (a) matches Frappe ecosystem expectations; (b) keeps everything in one repo. Recommend (a). |
| D2 | **docker-compose: does Bridge run in a container or natively?** | (a) Bridge in container alongside ERPNext (standard). (b) Bridge native on host, ERPNext in container (more dev-friendly). Recommend (a) for self-hosters, (b) for development docs. |
| D3 | **Headscale optional vs included** | (a) Always installed (even single-user setups). (b) Optional add-on for multi-actor mode. Recommend (b) — single-user is the simplest setup. |
| D4 | **License of the SunfishSoftware/Sunfish repo itself** | Currently public; needs a `LICENSE` file declaration. ERPNext consumer relationship suggests either GPLv3-compatible or a clean proxy-API boundary that avoids the linking question entirely. Recommend MIT for Sunfish-authored code + a `NOTICES.md` documenting the proxy relationship to GPL'd ERPNext. Legal review advisable before final pick. |
| D5 | **Versioning scheme for external packages** | (a) Strict semver. (b) Calver (year.month.iteration). Frappe ecosystem uses something like (b); npm ecosystem expects (a). Recommend (a) for the npm + NuGet packages; (b) optional for the Frappe app to match Frappe community norms. |
| D6 | **Backup/restore story** | (a) ERPNext built-in `bench backup` + filesystem-level Anchor backup. (b) Custom Sunfish `sunfish backup` CLI. (a) is correct for Phase 5; (b) is a Phase 6 polish item. |
| D7 | **Tauri code-signing for public releases** | (a) Dev-signed (free, browser warns on download). (b) Apple Developer ($99/y) + Microsoft EV cert ($250/y). For "anyone can try Sunfish," dev-signed is acceptable for v0.1; revisit at v0.5. |

---

## Open research questions

1. **GPLv3 copyleft boundaries** for Sunfish's proxy relationship to ERPNext. Cleanest stance: Sunfish doesn't link or include ERPNext code; it communicates over HTTP/JSON. That should keep Sunfish's own code outside GPLv3's reach. Confirm with legal counsel before declaring license.
2. **Frappe app installation in `frappe_docker`** — `bench get-app` works in a developer-mode Frappe install, but in the production `frappe_docker` setup, custom apps are baked into a custom Docker image. Phase 5 needs a documented "how to build your own frappe-sunfish-property image" workflow.
3. **Bridge's MockOktaService in self-hosted mode** — single-user self-hosters don't need Okta. Phase 5 should ship a "no-auth dev mode" or "local-cookie mode" for the simplest setup. Inherits the Phase 2 G4 auth scheme work.
4. **Self-hosted vs hosted Bridge** — Phase 5 supports self-hosted Bridge. Hosted Bridge (for users who want to skip Docker entirely) is a separate offering. Phase 5 names this distinction; doesn't build the hosted side.
5. **What goes in the "Try Sunfish in 10 minutes" walkthrough?** Probably: docker-compose up → log in → see sample 1-property dataset → record a rent payment → see the GL. Needs a seeded sample dataset (Phase 5 deliverable).

---

## Acceptance criteria (Phase 5 PASS — also = W#60 complete)

1. **A new user can:**
   ```bash
   git clone https://github.com/ctwoodwa/Sunfish
   cd Sunfish/_shared/operations/self-hosting
   ./bin/sunfish-up.sh
   open http://localhost:8080
   ```
   And see a working Sunfish + ERPNext + sample data within 10 minutes on a fresh Mac or Linux box.

2. **The license stack** is documented in `_shared/operations/LICENSES.md` with: each dependency, its license, why we picked it, the substitute path if forced to drop it.

3. **`@sunfish/ui-react` 0.1.0 is published to npm** (or a private registry if not opening to npm yet). Tauri Anchor consumes it.

4. **`frappe-sunfish-property` 0.1.0 is published** to a public GitHub org. Tagged release.

5. **The Inverted Stack book** has at least one chapter referencing the actual W#60 stack as built (not aspirational language). PAO sign-off required.

6. **CHANGELOG.md** entries cover Phase 5's externalizations.

7. **Repository README** has a top section: "Run Sunfish locally in 10 minutes" with a working link to the docker-compose guide.

**FAILED triggers:**
- GPLv3 legal interpretation forces Sunfish into GPL — re-evaluate W#60 architecture (likely means moving ERPNext interaction into a network-only boundary or considering a fork to AGPL-compatible). Major rework.
- Tauri public-release signing costs become a blocker for the F/OSS distribution — fall back to dev-signed releases with prominent install warning.
- docker-compose orchestration becomes unstable on common host configurations (Windows WSL2, Apple Silicon) — split into per-OS guides; potentially drop docker-compose for a per-OS native installer.

---

## Stage routing

| Stage | Action |
|---|---|
| 00 Intake (this doc) | ✅ authored |
| 01 Discovery | needed — GPLv3 legal scope research; Frappe custom-app build pipeline; backup tooling state |
| 02 Architecture | needed — resolve D1–D7 above; ADR (likely `0087-sunfish-public-distribution.md`) |
| 03 Package design | external package list firms up; semver / calver pick |
| 04 Scaffolding | docker-compose skeleton; frappe-sunfish-property app skeleton |
| 05 Implementation plan | docs-pipeline-variant skips Stages 2–3; goes intake → 05 directly |
| 06 Build | sunfish-PM (docs and packaging) + PAO (book alignment) |
| 07 Review | docs review + legal review + PAO book sign-off |
| 08 Release | Sunfish v0.1.0 public — the W#60 outcome |

---

## Predecessors and successors

**Predecessors:**
- W#60 Phases 2–4 — all at "built"
- Anchor's fate decision (XO recommends γ-then-α; CO decision needed)
- ADR 0067-A1 (Headscale substitution) — Accepted
- Memory: 2026-05-11 F/OSS gap analysis — input to the Phase 5 audit

**Successors:**
- App-store submissions (Phase 6 candidate)
- Marketing site / landing page (separate workstream)
- Sunfish v0.2+ semver iterations driven by user feedback
- Hosted-Bridge offering for users who don't want to self-host (separate offering, not part of W#60)

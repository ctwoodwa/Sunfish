# Sunfish ICM Pipeline

The Integrated Change Management (ICM) pipeline is Sunfish's filesystem-based orchestration system for
staging work through deliberate phases, ensuring quality, clarity, and traceability.

## Pipeline Structure

Sunfish ICM follows a **default 9-stage pipeline** that all work flows through:

| Stage | Purpose |
|---|---|
| 00_intake | Classify the request and select a pipeline variant |
| 01_discovery | Research scope, affected packages, and constraints |
| 02_architecture | Design decisions, contracts, and cross-package impacts |
| 03_package-design | Per-package API surface and implementation boundaries |
| 04_scaffolding | Generator/template changes if tooling is affected |
| 05_implementation-plan | Ordered tasks, owners, and acceptance criteria |
| 06_build | Code implementation in /packages, /apps, /tooling |
| 07_review | Quality gates: API, docs, tests, compat-telerik policy |
| 08_release | Changelog, versioning, publish, post-release updates |

Each stage:
- Has a `CONTEXT.md` that defines purpose, inputs, outputs, and exit criteria
- Maintains `references/` for stable supporting material
- Maintains `output/` as the handoff point to the next stage
- Ends with a review gate before advancing

## Pipeline Variants

ICM includes **reusable pipeline variants** under `/icm/pipelines/` for recurring work types:

- **sunfish-feature-change** — New features, blocks, enhancements spanning packages
- **sunfish-api-change** — Public contracts, breaking changes, adapter interfaces
- **sunfish-scaffolding** — Generator/CLI/template changes
- **sunfish-docs-change** — Docs site, kitchen-sink demos, usage guides, API docs
- **sunfish-quality-control** — Review gates, audits, release readiness checks
- **sunfish-test-expansion** — Coverage, regression, parity, scenario matrices
- **sunfish-gap-analysis** — Missing capabilities, adapter parity, docs gaps

Pipeline variants are **routing overlays** — they do not duplicate the main stage tree. Instead, each
variant specifies:
- Which default stages are emphasized or de-emphasized
- Typical output expectations
- Work type-specific entry heuristics
- Affected Sunfish areas

When starting work, choose a pipeline variant in stage 00_intake. The variant's `routing.md` then
guides how to navigate the default stages.

## Fast-track is the default

Sunfish is a solo, pre-community project (see [`_shared/product/vision.md` §Project Mode](../_shared/product/vision.md)). The full 9-stage pipeline is the *maximum* ceremony available, not the expected baseline. For most work, **fast-track is the default**:

- **Default fast-track path:** `00_intake` → `05_implementation-plan` → `06_build`. Collapse `01`–`04` into the intake note when the scope fits in a paragraph.
- **When to expand:** Add `02_architecture` when the change needs an ADR; add `03_package-design` when a new public API surface is introduced; add `04_scaffolding` only for generator/template work.
- **Always:** Create an intake note (even one paragraph) so the change is traceable. Always run `07_review` against the diff; always pick a `08_release` posture (changelog line, version bump if applicable).

Picking the fast-track path is a choice made in `00_intake` and recorded there. Expanding back to the full pipeline is always allowed — stage gates are a tool, not a tax.

## Terminology

In this repository:
- **pipeline** = The numbered ICM flow (00_intake through 08_release, or a pipeline variant overlay)
- **pipeline variant** = A reusable routing overlay for recurring work types
- **workspace** = VS Code only — not used for ICM concepts in this repo

## Core Principles

1. **Filesystem-based orchestration** — All workflow artifacts live in `/icm/`; real code stays in `/packages/`, `/apps/`, `/tooling/`
2. **Framework-agnostic first** — Contracts in `foundation/` and `ui-core/` take precedence; adapters follow
3. **Adapter parity** — All framework adapters (Blazor, React) should have equal feature coverage
4. **compat-telerik is policy-driven** — Treat as constrained; do not default to it as an abstraction target
5. **Docs and demos are deliverables** — User-facing changes must include kitchen-sink updates and docs
6. **Stage outputs are review gates** — Always review a stage's output folder before advancing

## Tool Boundaries

ICM is one of three tools that work together in this repository. Each has a distinct lane.

| Tool | Role |
|---|---|
| **ICM** | Pipeline/process — stage flow, routing, deliverables, review gates |
| **OpenWolf** | Repo memory — anatomy index, bug log, learned conventions, context middleware |
| **Serena** | Semantic code tooling — symbol discovery, navigation, targeted code edits |

**ICM's lane:** Manage the lifecycle of a change. Define what needs to happen, in what order, and with what review gates. Store design decisions, implementation plans, and release artifacts in stage `output/` folders.

**Not ICM's lane:** Persistent repo memory (OpenWolf), symbol-level code navigation (Serena), or code implementation (that lives in `/packages/`, `/apps/`, `/tooling/`).

When working through ICM stages, use Serena for code exploration and OpenWolf for context — but do not reproduce their outputs inside ICM artifacts.

## Directory Layout

```
icm/
  CONTEXT.md                  (this file)
  _config/
    routing.md                (classification and variant selection)
    stage-map.md              (stage reference table)
    deliverable-templates.md  (artifact templates)
  
  00_intake/                  (classify request, choose variant)
  01_discovery/               (research scope, packages, constraints)
  02_architecture/            (design, ADRs, cross-package contracts)
  03_package-design/          (per-package APIs, types, boundaries)
  04_scaffolding/             (generator/template work)
  05_implementation-plan/     (ordered tasks, acceptance criteria)
  06_build/                   (code implementation)
  07_review/                  (quality gates, approval)
  08_release/                 (changelog, publish, post-release)
  
  pipelines/
    sunfish-feature-change/
    sunfish-api-change/
    sunfish-scaffolding/
    sunfish-docs-change/
    sunfish-quality-control/
    sunfish-test-expansion/
    sunfish-gap-analysis/
```

Each stage and variant has its own `CONTEXT.md` or `README.md` describing how to work within it.

## Getting Started

1. **Start in stage 00_intake** — describe the request and classify it
2. **Choose a pipeline variant** — use `/icm/_config/routing.md` to decide
3. **Follow the variant's routing** — read `/icm/pipelines/[variant]/routing.md`
4. **Progress through stages** — each stage's `CONTEXT.md` explains next steps
5. **Review before advancing** — always check the current stage's output folder

## Real Code Lives Elsewhere

ICM is **workflow orchestration only**. Implementation happens in:
- `/packages/` — Framework-agnostic and adapter implementations
- `/apps/` — Demo and documentation apps
- `/tooling/` — CLI and generator tools

Do not move implementation code into `/icm/`. Reference it, yes. Manage it, no.

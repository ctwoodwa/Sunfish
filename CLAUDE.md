# Claude Instructions for Sunfish

## Overview

Sunfish is a framework-agnostic suite of open-source and commercial building blocks that helps
scaffold, prototype, and ship real-world applications with interchangeable UI and domain components.

This document explains how to work with Claude on Sunfish, including how to use the Integrated
Change Management (ICM) pipeline system.

### Foundational paper (read first)

The repo implements the architecture specified in
[`_shared/product/local-node-architecture-paper.md`](./_shared/product/local-node-architecture-paper.md)
— *Inverting the SaaS Paradigm: A Local-Node Architecture for Collaborative Software* (Version 10.0,
April 2026). Every structural choice in the codebase traces back to the decisions described there:
the kernel/plugin split, the UI-kernel four-tier layering (Foundation / Framework-Agnostic / Blocks
/ Compat-and-Adapter), the CP/AP per-record-class position, event-sourced ledger, schema epoch
coordination, the managed-relay sustainability model, and the compat-vendor-adapter pattern. When
in doubt about whether a change belongs in the repo, consult the paper — it defines the "why" that
makes the directory structure legible.

**Accelerator zone mapping per paper §20.7:** Anchor (`accelerators/anchor/`) is the Zone A
local-first desktop implementation. Bridge (`accelerators/bridge/`) is the Zone C Hybrid
implementation — hosted-node-as-SaaS with per-tenant data-plane isolation. These are the two
canonical deployment shapes; any future accelerator inherits from one. See
[ADR 0031](./docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) for Bridge's Zone-C model and
[ADR 0032](./docs/adrs/0032-multi-team-anchor-workspace-switching.md) for Anchor's v2
multi-team shape.

---

## Effort + Model Policy

The project sets **`effortLevel: xhigh`** as the default in `.claude/settings.json`
(overrides the Claude API default of `high`). Per the canonical Anthropic
guidance, `xhigh` is "the recommended starting point for coding and
agentic work" on Opus 4.7 and matches Sunfish's typical session shape:
multi-package refactors, ICM stage transitions, paper-alignment waves,
30+ minute build sessions.

Three things to know:

1. **`xhigh` is Opus 4.7-only.** If you `/model sonnet`, also set
   `/effort medium` explicitly — that's the canonical Sonnet 4.6 default
   and avoids unexpected latency.
2. **Subagents default to `low`.** When dispatching an `Agent`, expect
   `low` effort unless the role is design/review (then `xhigh`).
3. **Don't bake `max` into anything.** `max` causes overthinking on
   structured-output tasks per the canonical guidance — reserve it for
   the rare stuck case where evals show measurable headroom over `xhigh`.

Full rubric (per work type, per ICM stage, per subagent role, per
hypothetical project-local agent) lives in
[`.claude/rules/effort-policy.md`](.claude/rules/effort-policy.md).
Canonical reference: <https://platform.claude.com/docs/en/build-with-claude/effort>

---

## Tool Boundaries

Sunfish uses three complementary tools for AI-assisted development. Each has a distinct responsibility.
Do not duplicate one system's role inside another.

| Tool | Responsibility | Owns |
|---|---|---|
| **ICM** | Pipeline/process orchestration | Stage artifacts, routing, deliverables, workflow handoffs |
| **OpenWolf** | Repo memory and context middleware | `.wolf/anatomy.md`, `.wolf/cerebrum.md`, `.wolf/buglog.json`, token-aware context enrichment |
| **Serena** | Semantic code tooling | Symbol discovery, semantic navigation, precise code edits via LSP |

### When to Use Each Tool

**Use ICM when:**
- Managing the lifecycle of a change (intake → release)
- Deciding which stages to run or skip
- Creating or reviewing workflow artifacts (design docs, ADRs, implementation plans)
- Tracking review gates and approvals

**Use OpenWolf when:**
- Checking what files exist and their token cost (`.wolf/anatomy.md`)
- Looking up known bugs and fixes (`.wolf/buglog.json`)
- Reading or updating learned preferences and conventions (`.wolf/cerebrum.md`)
- Building or refreshing project context at the start of a session

**Use Serena when:**
- Navigating symbols, classes, functions, or types by name
- Finding all callers or references to a symbol
- Making precise, symbol-targeted code edits
- Exploring package structure semantically without reading whole files

### What Not to Do

- Do not store code navigation results in ICM artifacts — that is Serena's job
- Do not duplicate OpenWolf memory concepts (anatomy, buglog, cerebrum) in ICM stage notes
- Do not use ICM for ad-hoc context enrichment — use OpenWolf hooks for that
- ICM may reference Serena and OpenWolf operationally, but does not replace either

---

## Multi-Session Coordination

This repository is worked on by **three Claude sessions** that share the file system but cannot talk to each other directly:

| Session | Role | Default behavior |
|---|---|---|
| **research session** | ADRs, intakes, design decisions, retrofit plans, conventions, gap analyses, **+ cross-project PM** | Analyze & recommend. Doesn't write production code by default. **Synthesizes cross-project status on demand** when the user asks "what's the status?" — pulls from this repo's `icm/_state/active-workstreams.md` + `gh pr list` AND from `/Users/christopherwood/Projects/the-inverted-stack/` (chapter ICM stages, book-update-loop iterations, audiobook pipeline). |
| **sunfish-PM session** | Production code, scaffolds, PRs, CI fixes, dependency updates | Implements specs from research. Doesn't make architectural decisions. |
| **book-writing session** | The Inverted Stack manuscript | Writes/edits chapters at `/Users/christopherwood/Projects/the-inverted-stack/`. Doesn't touch Sunfish code or ADRs. Runs the book-update-loop (chapter ICM stages tracked as GitHub labels per book CLAUDE.md). |

Sessions coordinate via repo artifacts + auto-memory only. There is no chat channel between them. The research session's PM coverage is **on-demand synthesis, not a maintained dashboard** — status decays fast; on-the-fly is more honest.

### Canonical state files

- **`icm/_state/MASTER-PLAN.md`** — stable; the three goals (Business MVP, Component Library, Book), done conditions per goal, velocity baseline, estimated MVP date. Updated when goal definition or velocity baseline materially shifts; not per-PR.
- **`icm/_state/active-workstreams.md`** — dynamic; what's in flight, who owns it, what state it's in. Read at session start. Update on state change.
- **`icm/_state/handoffs/`** — per-workstream hand-off specs (research → sunfish-PM).

### Status format (executive summary on demand)

When the user asks "where are we?" / "status?" / "what's next?", produce a brief executive summary in this format (target ~250-400 words):

```
**Where we are:** 2-3 sentences naming the most-advanced workstream + the most-blocked workstream.

**Blockers needing your attention:** bulleted list, max 5, each <2 lines.
- Severity: [crit / high / med / low]
- One-line decision needed
- Where the detail lives (file path)

**What's next (priority order):**
1. Most-urgent unblock-or-decide item
2. Next sunfish-PM hand-off
3. Next book milestone
4. Next research deliverable

**Velocity vs MVP:** 1-2 sentences. PRs/day recent, percent-done estimate, on-track-or-not vs the MASTER-PLAN.md estimate.
```

Don't pad. Don't include dependabot/bot PRs in the in-flight count. Don't repeat MASTER-PLAN.md content; reference it.

Status vocabulary: `design-in-flight` (research working; do not build), `ready-to-build` (hand-off file exists; sunfish-PM may implement), `building`, `built`, `held`, `blocked`, `superseded`.

### Pre-build checklist (sunfish-PM session)

**Before any code change beyond a one-line fix, check:**

1. **`icm/_state/active-workstreams.md`** — find the row for the workstream you're about to touch. It must say `ready-to-build`. If `design-in-flight` or `held`, STOP and write a memory note asking research to clarify before proceeding.
2. **The relevant intake / ADR / architecture artifact's `Status:` line** — must match `ready-to-build` (or be silent on status, which means workstream-level status from the ledger applies).
3. **`icm/_state/handoffs/<workstream>.md`** — for `ready-to-build` workstreams, a hand-off file describes exactly what to build, file-by-file, with acceptance criteria.
4. **`gh pr list --state open`** — look for in-flight PRs touching the same package, especially ones with auto-merge enabled.
5. **`but status` (or `git log --oneline -10 --all`)** — verify no parallel-session work has landed since the hand-off was authored.

If ANY of those signal "design-in-flight" / "blocked" / unexpected state, stop. Write a memory entry naming the workstream, what you observed, and what you need clarified. Do not proceed.

### State transitions (research session)

When widening or revising an intake mid-flight: immediately set its `Status:` to `design-in-flight`, update the matching row in `icm/_state/active-workstreams.md`, and (if a hand-off existed) revoke or update it.

When a design is final and ready for implementation: write a hand-off in `icm/_state/handoffs/<workstream>.md`, flip the ledger row to `ready-to-build`, and (optionally) write a project memory pointing at the hand-off so sunfish-PM finds it via auto-memory at session start.

### Memory-side coordination

Project memories under `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/` are auto-loaded at every session's start. Use them for:

- Cross-session announcements ("research has revised X; see hand-off Y")
- State summaries that future sessions need ("Phase 1 progress per gap")
- Hand-off pointers when the repo artifact alone isn't discoverable enough

Don't duplicate hand-off content into memory; point at the repo file.

### When parallel-session work surprises you

Per `feedback_verify_pr_state_at_session_start.md`: at session start (especially after `/compact`), batch-run `git log --all`, `git status`, `gh pr list`, `but status`, and tail `.wolf/memory.md` before acting on anything that says "pending." Compacted summaries are point-in-time snapshots; the repo + ledger are the ground truth.

---

## Sunfish ICM Pipeline System

Sunfish uses a filesystem-based orchestration system called ICM (Integrated Change Management) to
manage work through deliberate stages, ensuring quality, clarity, and traceability.

### What is /icm?

`/icm` is the Sunfish ICM pipeline — a staging system for work that flows through 9 numbered stages:

- **00_intake** — Classify and scope the request
- **01_discovery** — Research dependencies and impact
- **02_architecture** — Design the solution
- **03_package-design** — Define per-package APIs
- **04_scaffolding** — Build generators/templates (if needed)
- **05_implementation-plan** — Create task list
- **06_build** — Implement code
- **07_review** — Quality gates and approval
- **08_release** — Publish and announce

Each stage has a `CONTEXT.md` file explaining its purpose, inputs, outputs, and exit criteria.
See [`/icm/CONTEXT.md`](icm/CONTEXT.md) for complete overview.

### What are Pipeline Variants?

Under `/icm/pipelines/`, Sunfish maintains **7 reusable pipeline variant overlays** for recurring
work types:

1. **sunfish-feature-change** — New features, blocks, enhancements
2. **sunfish-api-change** — Breaking changes, public contracts
3. **sunfish-scaffolding** — Generator/CLI/template changes
4. **sunfish-docs-change** — Docs site, examples, API docs
5. **sunfish-quality-control** — Audits, review gates, consistency checks
6. **sunfish-test-expansion** — Test coverage, regression, parity
7. **sunfish-gap-analysis** — Missing capabilities, parity gaps

Pipeline variants are **NOT separate stage trees**. They are **routing overlays** that guide how
work moves through the default 9 stages.

Each variant includes:
- `README.md` — When to use this variant, key responsibilities
- `routing.md` — How to navigate the default stages
- `deliverables.md` — Standard outputs expected at each stage

### Terminology

**Important:** In this repository, terminology is strict:

- **pipeline** = The numbered ICM flow (00_intake through 08_release, or a variant routing)
- **pipeline variant** = A reusable overlay for recurring work types (lives in /icm/pipelines/)
- **workspace** = VS Code only — NEVER used for ICM concepts in this repo

If documentation mentions "workspace" in an ICM context, replace it with "pipeline".

---

## How to Use Sunfish's ICM

### When Starting a Request

1. **Start in stage 00_intake** (unless reusing a prior output)
   - Describe the request
   - Identify affected Sunfish packages
   - Choose a pipeline variant using [`/icm/_config/routing.md`](icm/_config/routing.md)
   - Create `00_intake/output/intake-note.md`

2. **Use the variant's routing guide** (e.g., `pipelines/sunfish-feature-change/routing.md`)
   - Each variant explains how to navigate the 9 stages
   - Which stages to emphasize
   - Which stages to skip

3. **Follow the stage guidance** (e.g., `01_discovery/CONTEXT.md`)
   - Each stage has a CONTEXT.md explaining purpose, process, outputs, and exit criteria
   - Each stage also has expected deliverables (see variant's `deliverables.md`)

4. **Review before advancing**
   - Each stage's `output/` folder is a review gate
   - Don't advance until the output is reviewed and approved

### Key Files

| File | Purpose |
|---|---|
| `/icm/CONTEXT.md` | Overview of the ICM pipeline system |
| `/icm/_config/routing.md` | How to classify requests and choose pipeline variants |
| `/icm/_config/stage-map.md` | Quick reference table of all stages |
| `/icm/_config/deliverable-templates.md` | Standard artifact templates |
| `/icm/[00-08]_*/CONTEXT.md` | Purpose, inputs, process, outputs, exit criteria for each stage |
| `/icm/pipelines/[variant]/README.md` | When to use this variant, key responsibilities |
| `/icm/pipelines/[variant]/routing.md` | How to navigate default stages for this variant |
| `/icm/pipelines/[variant]/deliverables.md` | Standard outputs for this variant |

---

## Real Code vs. Workflow Artifacts

### ICM is Workflow Only

The `/icm` directory contains workflow orchestration artifacts only:
- Stage context files
- Design decisions and ADRs
- Implementation plans
- Audit reports
- Release checklists

Do NOT put implementation code in `/icm`. ICM is not a code folder.

### Real Code Lives Here

- `/packages/` — Framework-agnostic and adapter implementations
- `/apps/` — Demo and documentation applications
- `/tooling/` — Developer tools (CLI, generators, build tools)
- `/_shared/` — Project documentation and standards

When working through ICM stages, reference these locations but don't move code into `/icm`.

---

## Sunfish-Specific Considerations

### Package Architecture

Sunfish has a layered package structure:

```
foundation (no dependencies)
  ↓ (depends on)
ui-core (framework-agnostic contracts)
  ↓ (depend on)
ui-adapters-blazor (Blazor implementation)
ui-adapters-react (React implementation)
  ↓ (depend on)
blocks-* (composition layer)
  ↓ (depend on)
apps/kitchen-sink (playground/demo)
apps/docs (documentation)
  ↓
tooling/scaffolding-cli (generator tool)

compat-telerik (compatibility shim, depends on foundation + ui-core + ui-adapters-blazor)
```

### Framework-Agnostic Design Principle

When designing features:
1. **Define the contract in foundation/ui-core** (framework-agnostic types, interfaces)
2. **Then implement in adapters** (Blazor and React, framework-specific)
3. **Compose in blocks** (using adapters)
4. **Don't let adapters drive the design** — contracts should be framework-agnostic

### Adapter Parity

- All features available in all adapters (Blazor + React) unless explicitly approved otherwise
- Write parity tests to verify equivalence
- Document any intentional differences and get sign-off

### compat-telerik Policy

- compat-telerik provides a Telerik-compatible surface over Sunfish
- It is NOT the source of truth; ui-core and adapters are
- compat-telerik changes are policy-gated (expect to justify them in review)
- If a Sunfish feature can't map to Telerik, that's OK (but document it)

### User-Facing Changes

If your change is user-facing:
- **kitchen-sink demo is mandatory** (stage 06 deliverable)
- **apps/docs updates are mandatory** (stage 06 deliverable)
- **JSDoc/XML comments on all public APIs** (stage 06 deliverable)
- **Changelog entry with user-focused description** (stage 08 deliverable)

---

## Pipeline Variant Decision Tree

Choose a pipeline variant using this heuristic:

**Is it a new feature, block, or demo?**
→ **sunfish-feature-change**

**Is it a breaking API change?**
→ **sunfish-api-change**

**Is it a generator, CLI, or template change?**
→ **sunfish-scaffolding**

**Is it docs, examples, or kitchen-sink?**
→ **sunfish-docs-change**

**Is it an audit, review gate, or consistency check?**
→ **sunfish-quality-control**

**Is it test coverage, regression, or parity testing?**
→ **sunfish-test-expansion**

**Is it finding or scoping a missing capability?**
→ **sunfish-gap-analysis**

See `/icm/_config/routing.md` for detailed heuristics and examples.

---

## Common Workflows

### Add a New Feature (Feature Block, Component, etc.)

1. Intake: Describe the feature, identify affected packages
2. Choose: **sunfish-feature-change** variant
3. Follow: `/icm/pipelines/sunfish-feature-change/routing.md`
4. Output: New code in packages/, new demo in kitchen-sink, new docs in apps/docs
5. Release: MINOR version bump

### Make a Breaking API Change

1. Intake: Clearly state what is breaking and why
2. Choose: **sunfish-api-change** variant
3. Follow: `/icm/pipelines/sunfish-api-change/routing.md`
4. Output: Updated APIs, migration guide, updated all consumers
5. Release: MAJOR version bump, migration guide in release notes

### Update Docs/Examples

1. Intake: Describe what docs need updating
2. Choose: **sunfish-docs-change** variant
3. Follow: `/icm/pipelines/sunfish-docs-change/routing.md`
4. Output: Updated markdown files, verified examples
5. Release: Publish docs site updates (may be independent of code release)

### Improve Test Coverage

1. Intake: Identify coverage gaps and target coverage %
2. Choose: **sunfish-test-expansion** variant
3. Follow: `/icm/pipelines/sunfish-test-expansion/routing.md`
4. Output: New test files, improved coverage metrics
5. Release: Merge to main (no version bump needed if no code changes)

---

## Accelerating or Skipping Stages

### When You Can Skip Stages

- **docs-change:** Skip architecture and package-design (go straight from intake to implementation-plan)
- **quality-control:** Skip scaffolding (go straight from implementation-plan to build)
- **test-expansion:** Skip architecture and package-design (scope is clear)

### When You Can Fast-Track

- **Trivial bug fixes:** Go from intake straight to 06_build (get 00_intake approval first)
- **Reusing prior discovery:** If you have a discovery document from related work, reference it in
  intake and go straight to architecture

Always document acceleration decisions in the current stage's notes.

---

## Review Gates and Approvals

Each stage is a review gate. Before advancing:

- ✓ Current stage's output is complete
- ✓ Output artifact(s) have been reviewed and approved
- ✓ Any blockers from prior stages are resolved

If a stage is blocked or has issues:
- Document the issue clearly
- Return to relevant earlier stage
- Fix the issue
- Re-review

This is normal and expected. Catching issues early is better than discovering them late.

---

## Questions?

For questions about the ICM pipeline:
- See `/icm/CONTEXT.md` for pipeline overview
- See the specific stage CONTEXT.md (e.g., `/icm/06_build/CONTEXT.md`)
- See the variant README and routing guide
- See `/icm/_config/routing.md` for request classification

For questions about Sunfish architecture or package structure:
- See `/_shared/product/architecture-principles.md`
- See individual package README files (e.g., `/packages/foundation/README.md`)
- See `/packages/ui-core/README.md` for component contracts
- See `/_shared/engineering/coding-standards.md` for style guidelines

---

## Summary

The Sunfish ICM pipeline is a structured, stage-gated approach to managing work. It emphasizes:
- **Clarity** — every stage has clear inputs, outputs, and exit criteria
- **Quality** — review gates before advancing
- **Traceability** — all decisions and plans are documented
- **Sunfish-specific guidance** — package architecture, adapter parity, user-facing requirements

Use the pipeline variant routing guides to navigate the default 9 stages based on the type of work.
Always document assumptions and decisions at each stage.

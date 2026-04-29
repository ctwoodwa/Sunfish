# Claude Instructions for Sunfish

Sunfish is a framework-agnostic suite of building blocks (open-source + commercial) for scaffolding, prototyping, and shipping real applications with interchangeable UI and domain components. It is the reference implementation for *The Inverted Stack*.

## Foundational paper (read first)

Architecture is specified in [`_shared/product/local-node-architecture-paper.md`](./_shared/product/local-node-architecture-paper.md) — *Inverting the SaaS Paradigm*, v10.0 April 2026. Every structural choice (kernel/plugin split, four-tier UI layering, CP/AP per-record-class, event-sourced ledger, schema epochs, managed-relay sustainability, compat-vendor-adapter pattern) traces back to it.

**Accelerator zones (paper §20.7):** `accelerators/anchor/` is Zone A local-first desktop ([ADR 0032](./docs/adrs/0032-multi-team-anchor-workspace-switching.md)); `accelerators/bridge/` is Zone C hybrid hosted-node-as-SaaS ([ADR 0031](./docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md)). Future accelerators inherit from one.

---

## Effort + Model Policy

`.claude/settings.json` sets `effortLevel: xhigh` (overrides API default `high`). Canonical for Opus 4.7 multi-package agentic work.

1. **`xhigh` is Opus 4.7-only.** On `/model sonnet`, set `/effort medium` explicitly (canonical Sonnet 4.6 default).
2. **Subagents default to `low`** unless role is design/review (`xhigh`).
3. **Don't bake `max` into anything.** Reserve for stuck cases with measured headroom over `xhigh`.

Full per-work-type / per-stage / per-subagent rubric: [`.claude/rules/effort-policy.md`](.claude/rules/effort-policy.md). Canonical: <https://platform.claude.com/docs/en/build-with-claude/effort>.

---

## Tool Boundaries

| Tool | Responsibility | Owns |
|---|---|---|
| **ICM** | Pipeline/process orchestration | Stage artifacts, routing, deliverables, hand-offs |
| **OpenWolf** | Repo memory + context middleware | `.wolf/anatomy.md`, `.wolf/cerebrum.md`, `.wolf/buglog.json` |
| **Serena** | Semantic code tooling | Symbol discovery, semantic navigation, LSP-precise edits |

Don't duplicate one system's role inside another. ICM tracks lifecycle of changes; OpenWolf is repo memory; Serena is for code navigation/edits. Consult `.wolf/anatomy.md` before reading project files; `.wolf/buglog.json` before fixing bugs (see [`.claude/rules/openwolf.md`](.claude/rules/openwolf.md)).

---

## Multi-Session Coordination

Three Claude sessions share the file system but cannot talk directly:

| Session | Role | Default behavior |
|---|---|---|
| **research** | ADRs, intakes, design, retrofits, conventions, gap analyses, **+ cross-project PM** | Analyze + recommend. No production code by default. **Synthesizes cross-project status on demand** from this repo + `/Users/christopherwood/Projects/the-inverted-stack/`. |
| **sunfish-PM** | Production code, scaffolds, PRs, CI fixes, deps | Implements specs from research. Doesn't make architectural decisions. |
| **book-writing** | The Inverted Stack manuscript | Writes/edits chapters at `/Users/christopherwood/Projects/the-inverted-stack/`. Doesn't touch Sunfish code/ADRs. |

Sessions coordinate via repo artifacts + auto-memory. No chat between them. Research's PM coverage is **on-demand synthesis, not a maintained dashboard**.

### Canonical state files

- **`icm/_state/MASTER-PLAN.md`** — stable; goals + done-conditions + velocity baseline. Updated only when goals/baseline shift.
- **`icm/_state/active-workstreams.md`** — dynamic; in-flight workstreams + owner + state. Read at session start; update on state change.
- **`icm/_state/handoffs/`** — per-workstream hand-off specs (research → sunfish-PM).

### Status format (executive summary on demand)

When user asks "where are we?" / "status?" / "what's next?", produce ~250-400 words:

```
**Where we are:** 2-3 sentences naming most-advanced + most-blocked workstreams.
**Blockers needing your attention:** ≤5 bullets, each <2 lines: [crit/high/med/low] + decision needed + file path.
**What's next (priority order):** 1) urgent unblock; 2) next sunfish-PM hand-off; 3) next book milestone; 4) next research deliverable.
**Velocity vs MVP:** 1-2 sentences. PRs/day, % done, on-track vs MASTER-PLAN.md estimate.
```

Don't pad. Don't include dependabot/bot PRs. Don't repeat MASTER-PLAN.md content.

Status vocabulary: `design-in-flight` / `ready-to-build` / `building` / `built` / `held` / `blocked` / `superseded`.

### Pre-build checklist (sunfish-PM)

Before any code change beyond a one-line fix:

1. **`active-workstreams.md`** — find the row. Must say `ready-to-build`. If `design-in-flight`/`held`, STOP + memory note to research.
2. **Intake / ADR `Status:` line** — must match `ready-to-build` (or be silent — ledger applies).
3. **`icm/_state/handoffs/<workstream>.md`** — describes what to build, file-by-file, with acceptance criteria.
4. **`gh pr list --state open`** — look for in-flight PRs touching the same package; especially auto-merge-enabled.
5. **`but status` (or `git log --all --oneline -10`)** — verify no parallel-session work landed since hand-off was authored.

Any unexpected state → STOP, memory note naming workstream + observation + needed clarification. Do not proceed.

### State transitions (research)

- **Widening/revising mid-flight:** set intake `Status: design-in-flight`, update ledger row, revoke/update existing hand-off.
- **Design final:** write hand-off in `handoffs/<workstream>.md`, flip ledger row to `ready-to-build`, optionally write a project memory pointing at the hand-off.

### Memory-side coordination

Project memories under `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/` auto-load at session start. Use for: cross-session announcements, state summaries future sessions need, hand-off pointers. Don't duplicate hand-off content; point at the repo file.

### When parallel-session work surprises you

Per `feedback_verify_pr_state_at_session_start.md`: at session start (especially after `/compact`), batch-run `git log --all`, `git status`, `gh pr list`, `but status`, and tail `.wolf/memory.md` before acting on anything marked "pending." Compacted summaries are point-in-time; the repo + ledger are ground truth.

### Fallback work order (sunfish-PM, when priority queue is dry)

Priority queue = `active-workstreams.md` rows `ready-to-build` with a hand-off file. When dry, sunfish-PM does **not idle** — falls through:

| Rung | Work |
|---|---|
| 1 | **Dependabot PR cleanup** — auto-merge per `project_pre_release_latest_first_policy`; skip CI failures + pinned packages. |
| 2 | **Build hygiene** — fix new warnings/deprecations/analyzer findings; flag design-judgment items to research. |
| 3 | **Style-audit P0** per `icm/07_review/output/style-audits/TIER-4-RE-AUDIT.md`. |
| 4 | **Test coverage gap-fill** — tests for existing public surface; no behavior changes. |
| 5 | **Doc improvements** — XML docs, README gaps, `apps/docs/blocks/<block>.md` stubs. |
| 6 | **Idle:** write `cob-idle-*.md` to research-inbox (see below) THEN `ScheduleWakeup 1800s`. Re-poll priority queue at wake. |

Rules: commits use `chore(fallback):` / `fix(build):` / `test(coverage):` / `docs:`. Design-question halts → `cob-question-*.md` (see below). Re-check priority queue after every merge; priority always wins. Cap concurrent fallback PRs at 3. **Research commitment:** maintain queue depth 2–3 `ready-to-build` rows; pre-write next 1–2 hand-offs after every PR merges. **XO scans `research-inbox/` every loop iteration.**

### Live signaling to XO — `research-inbox/`

Filesystem inbox at `icm/_state/research-inbox/` for sunfish-PM (COB) and book-writing (Yeoman) → research (XO) signals. Survives session restarts (committed to git); active beacons in root, resolved in `_archive/`. **File naming:** `{sender}-{type}-YYYY-MM-DDTHH-MMZ-{slug}.md` where `sender ∈ {cob, yeoman}` and `type ∈ {idle, question, resumed}`. Body: 3-line YAML frontmatter (`type`, `workstream-or-chapter`, `last-pr`) + ≤2 lines context + ≤2 lines "what would unblock me."

**COB writes** (`cob-*`): rung-6 idle → `cob-idle-*.md` + `ScheduleWakeup 1800s`. Design-ambiguity halt → `cob-question-*.md` + halt the workstream + ledger row note. **Yeoman writes** (`yeoman-*`, cross-repo `cd /Users/christopherwood/Projects/Sunfish && git ...`): factual question that gates a chapter (ADR cross-ref status, workstream timing, architectural detail) → `yeoman-question-*.md` + pause that chapter section. **XO reads each loop iteration:** `ls icm/_state/research-inbox/*.md 2>/dev/null`; non-empty → priority above ADR cadence; resolve via hand-off/answer/ledger update, then `git mv` to `_archive/` in the same PR. **Trim:** weekly `chore(housekeeping): prune research-inbox archive` deletes `_archive/*.md` >30 days old; active beacons >7 days unanswered → XO escalates to CO.

---

## Sunfish ICM Pipeline

ICM (Integrated Change Management) is filesystem-based stage orchestration. 9 stages: **00_intake** → **01_discovery** → **02_architecture** → **03_package-design** → **04_scaffolding** → **05_implementation-plan** → **06_build** → **07_review** → **08_release**. Each stage has `CONTEXT.md` (purpose / inputs / outputs / exit criteria). See [`/icm/CONTEXT.md`](icm/CONTEXT.md).

### Pipeline variants (routing overlays — NOT separate stage trees)

7 reusable overlays under `/icm/pipelines/` for recurring work types: **sunfish-feature-change** (new features/blocks), **sunfish-api-change** (breaking changes), **sunfish-scaffolding** (generators/CLI/templates), **sunfish-docs-change** (docs/examples), **sunfish-quality-control** (audits/review-gates), **sunfish-test-expansion** (coverage/parity), **sunfish-gap-analysis** (missing capabilities).

Each variant has `README.md` + `routing.md` + `deliverables.md`. Variants guide HOW work moves through the 9 default stages; they don't replace them.

### Terminology (strict)

- **pipeline** = the numbered ICM flow (or a variant routing through it)
- **pipeline variant** = reusable overlay in `/icm/pipelines/`
- **workspace** = VS Code only — NEVER an ICM concept here

### Variant decision tree

New feature/block/demo → **feature-change** · Breaking API change → **api-change** · Generator/CLI/template → **scaffolding** · Docs/examples/kitchen-sink → **docs-change** · Audit/review-gate/consistency → **quality-control** · Coverage/regression/parity → **test-expansion** · Find/scope missing capability → **gap-analysis**. See [`/icm/_config/routing.md`](icm/_config/routing.md) for examples.

### Skip / fast-track

- **docs-change** skips architecture + package-design (intake → impl-plan).
- **quality-control** skips scaffolding.
- **test-expansion** skips architecture + package-design.
- **Trivial bug fix** can go intake → 06_build (with intake approval).
- **Reusing prior discovery** can go intake → architecture (reference the doc).

Document acceleration decisions in the current stage's notes.

---

## Sunfish architecture

```
foundation (no dependencies)
  ↓
ui-core (framework-agnostic contracts)
  ↓
ui-adapters-blazor / ui-adapters-react
  ↓
blocks-* (composition layer)
  ↓
apps/kitchen-sink (demo) / apps/docs (documentation)
  ↓
tooling/scaffolding-cli (generator)

compat-telerik (depends on foundation + ui-core + ui-adapters-blazor)
```

**Framework-agnostic principle:** define the contract in foundation/ui-core first; then implement in adapters; then compose in blocks. Adapters don't drive contracts.

**Adapter parity:** all features in all adapters (Blazor + React) unless explicitly approved. Parity tests verify; document intentional differences.

**compat-telerik:** Telerik-compatible surface over Sunfish; NOT the source of truth. Changes are policy-gated. If a Sunfish feature can't map to Telerik, that's OK — document it.

**User-facing changes (Stage 06 deliverables):** kitchen-sink demo + apps/docs update + JSDoc/XML on public APIs. Stage 08: changelog entry with user-focused description.

---

## Real code vs. workflow artifacts

`/icm/` is workflow only — context files, ADRs, plans, audits, release checklists. No implementation code.

Code lives in `/packages/` (framework-agnostic + adapters), `/apps/` (demo + docs), `/tooling/` (CLI/generators), `/_shared/` (project docs + standards).

---

## Key files

| File | Purpose |
|---|---|
| [`/icm/CONTEXT.md`](icm/CONTEXT.md) | ICM pipeline overview |
| [`/icm/_config/routing.md`](icm/_config/routing.md) | Request classification + variant choice |
| [`/icm/_config/stage-map.md`](icm/_config/stage-map.md) | Quick stage reference |
| [`/icm/_config/deliverable-templates.md`](icm/_config/deliverable-templates.md) | Standard artifact templates |
| `/icm/[NN]_*/CONTEXT.md` | Per-stage purpose/inputs/outputs/exit-criteria |
| `/icm/pipelines/[variant]/{README,routing,deliverables}.md` | Per-variant guidance |
| [`/_shared/product/architecture-principles.md`](./_shared/product/architecture-principles.md) | Architecture principles |
| [`/_shared/engineering/coding-standards.md`](./_shared/engineering/coding-standards.md) | Style guidelines |
| [`/packages/foundation/README.md`](./packages/foundation/README.md), [`/packages/ui-core/README.md`](./packages/ui-core/README.md) | Per-package contracts |

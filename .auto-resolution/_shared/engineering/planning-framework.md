# Planning Framework

**Status:** Accepted
**Last reviewed:** 2026-04-20
**Governs:** How plans are produced at every ICM stage and for every significant Sunfish contribution.
**Companion docs:** [`icm/CONTEXT.md`](../../icm/CONTEXT.md), [`../../GOVERNANCE.md`](../../GOVERNANCE.md), [`docs/adrs/`](../../docs/adrs/), [`.claude/rules/universal-planning.md`](../../.claude/rules/universal-planning.md).
**Agent relevance:** Loaded by agents producing ICM-stage plans or multi-phase proposals. High-frequency when planning; skip for trivial fixes.

## Adoption

Sunfish adopts the **[Universal Planning Framework (UPF)](https://github.com/primeline-ai/universal-planning-framework)** by Primeline AI as its plan-quality discipline. UPF is a three-stage thinking process (Discovery → Planning → Meta-Validation) plus an optional adversarial hardening pass, with 21 anti-pattern checks, built on the **Decompose-Suspend-Validate (DSV)** principle — "challenge your interpretation before committing to it." It's distributed as a Claude Code rule and ships alongside Sunfish at [`.claude/rules/universal-planning.md`](../../.claude/rules/universal-planning.md) under its original MIT license, unmodified.

UPF was chosen because Sunfish is AI-native by commitment (vision Pillar 4), and UPF is specifically built for AI-assisted planning with adversarial hardening, Cold Start Tests, and anti-pattern detection that fit how Claude Code and similar assistants work. The framework is domain-agnostic and applies equally to code, bundle design, accelerator build-outs, documentation, and roadmap decisions.

## Three frameworks, one stack

Three complementary frameworks operate at different altitudes in Sunfish. They layer cleanly rather than conflict:

| Framework | Altitude | What it governs |
|---|---|---|
| **Open Decision Framework (ODF)** — Red Hat | Governance | *Who* decides, *how inclusively*, with what transparency. Four phases (Ideation → Planning & Research → Design/Development/Testing → Launch) mapped to Sunfish's decision types. See [`GOVERNANCE.md`](../../GOVERNANCE.md). |
| **Universal Planning Framework (UPF)** — Primeline AI | Plan quality | *How a plan itself is made rigorous.* Three stages (Discovery → Plan → Meta-Validation) with an optional Stage 1.5 Autonomous Hardening pass, 21 anti-pattern checks, and a C/B/A quality rubric. |
| **Integrated Change Management (ICM)** — Sunfish | Workflow | *What stages work passes through* in the Sunfish repo: `00_intake → 01_discovery → 02_architecture → 03_package-design → 04_scaffolding → 05_implementation-plan → 06_build → 07_review → 08_release`. |

In one sentence: **ODF says how decisions are made, UPF says how plans are made well, ICM says what stages a plan passes through.** A plan for a new Sunfish feature uses all three — ODF for inclusion, UPF for rigor, ICM for orchestration.

## Mapping UPF stages to ICM stages

Most ICM stages produce planning artifacts; UPF describes what a good artifact looks like at that stage.

| UPF stage | ICM stages it informs | What UPF contributes |
|---|---|---|
| **Stage 0 — Discovery & Sparring** | `00_intake`, `01_discovery` | The 12 Stage-0 checks (Existing Work, Feasibility, Better Alternatives / AHA Effect, Factual Verification, Official Docs, ROI, Updates, Best Practices, Deep Research, Competitive, Constraints, People Risk) become the structure of the discovery write-up. |
| **Stage 1 — The Plan** | `02_architecture`, `03_package-design`, `05_implementation-plan` | Every plan artifact has 5 CORE sections (Context & Why, Success Criteria with FAILED conditions, Assumptions & Validation, Phases with binary gates, Verification) plus any applicable CONDITIONAL sections. Architecture ADRs, package-design docs, and implementation plans each fill the template at the right altitude. |
| **Stage 1.5 — Autonomous Hardening** | Pre-merge review between `05_implementation-plan` and `06_build`, and before any ADR merges | Plans and ADRs pass through six adversarial perspectives (Outside Observer, Pessimistic Risk Assessor, Pedantic Lawyer, Skeptical Implementer, The Manager, Devil's Advocate) before they're ratified. Hardening Log is preserved in the PR. |
| **Stage 2 — Meta-Validation** | `07_review` | The 7 meta-checks — Delegation Strategy, Research Needs, Review Gates, 21-pattern Anti-Pattern Scan, Cold Start Test, Plan Hygiene, Discovery Consolidation — are the review gate. Plans that fail Stage 2 get blocked, not merged. |

## When UPF activates

Per the UPF rule itself: **use UPF for** new features, architecture changes touching 3+ files, multi-phase projects (3+ phases or >2 hours), and anything with external dependencies. In Sunfish, that covers essentially every ADR, every new `blocks-*` module, every bundle manifest beyond the first, every Bridge-accelerator feature, every provider adapter, every migration (including the ADR 0017 Web Components migration).

**Skip UPF when ALL apply:** single file, no dependencies, <50 lines, no migration, no user-facing change, rollback is `git revert`. In practice this means typo fixes, doc-link corrections, tests for existing behavior, and similar maintenance.

## The critical sections in practice

Of the UPF framework, the three practices with the highest impact for Sunfish specifically:

### 1. The AHA Effect (Stage 0 check 0.9)

"Is there a fundamentally simpler approach we're missing?" For Sunfish, this check has repeatedly caught would-be reinventions (CRDT work that Automerge already handles, custom manifest schemas that JSON Schema already covers, compat shims that upstream already ships). The principle is baked into the vision doc's "consolidation over invention" commitment — AHA is how that commitment shows up in a specific planning session.

### 2. FAILED conditions (Stage 1 CORE section 2)

Every plan defines **not just when it succeeds but when it should be killed**. No FAILED condition = zombie project (anti-pattern #11). For Sunfish bundles and accelerators where the "ship it because we've invested 6 months" trap is real, FAILED conditions are the single most valuable Stage-1 discipline.

### 3. Cold Start Test (Stage 2 check 5)

"Can a fresh Claude Code session execute this plan tonight, without access to the current conversation?" If the answer is no, the plan is underspecified — regardless of how reasonable it looks to the author. For a platform built AI-native, the Cold Start Test is the strongest forcing function against context-dependent plans.

## How this fits with ODF

When `GOVERNANCE.md` ships (per the last governance proposal), it will adopt ODF's four phases and name the mechanisms for each. UPF sits *inside* ODF's Planning-and-Research phase: ODF says the problem must be shared transparently and external input invited; UPF says the plan produced during that phase must meet the quality rubric and pass the anti-pattern scan before ratification.

The two frameworks don't compete — they answer different questions:

- ODF: *"Have the right people had input on this decision?"*
- UPF: *"Is the plan itself rigorous enough to execute?"*

## Practical mechanics

- **Where UPF lives in the repo:** [`.claude/rules/universal-planning.md`](../../.claude/rules/universal-planning.md). The rule loads automatically for Claude Code sessions in the Sunfish repo.
- **Commands** (`/plan-new`, `/plan-refine`, `/plan-review`, `/interview-plan`): available by installing the full UPF kit. The minimal install shipped with Sunfish covers the rule only. To add commands, run UPF's full-install steps:
  ```bash
  git clone https://github.com/primeline-ai/universal-planning-framework /tmp/upf
  cp -r /tmp/upf/.claude/commands/* .claude/commands/
  cp -r /tmp/upf/.claude/agents/* .claude/agents/
  ```
- **Updating:** when UPF releases a new version, resync the rule file from upstream. Sunfish-specific adaptations stay in this document, not in the rule file.

## Attribution

- **Universal Planning Framework** is © Primeline AI, MIT-licensed, available at <https://github.com/primeline-ai/universal-planning-framework>. Sunfish incorporates it unmodified under the MIT terms.
- **Open Decision Framework** is © Red Hat, Inc., Creative Commons licensed. See <https://github.com/red-hat-people-team/open-decision-framework>.
- **Integrated Change Management (ICM)** is Sunfish-specific; see [`icm/CONTEXT.md`](../../icm/CONTEXT.md).

## Cross-references

- [`.claude/rules/universal-planning.md`](../../.claude/rules/universal-planning.md) — the UPF rule, loaded by Claude Code in this repo.
- [`icm/CONTEXT.md`](../../icm/CONTEXT.md) — Sunfish's workflow stages that UPF informs.
- [`GOVERNANCE.md`](../../GOVERNANCE.md) (repo root) — ODF adoption for decision-making governance.
- [roadmap-tracker.md](../product/roadmap-tracker.md) — major-decision cadence where UPF Stage 0 + Stage 1.5 show up at quarterly review.
- [architecture-principles.md](../product/architecture-principles.md) — the platform commitments plans must honor.

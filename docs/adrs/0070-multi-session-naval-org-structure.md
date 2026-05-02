---
id: 70
title: Multi-Session Naval-Org Structure (CO/XO/COB/PAO/Yeoman)
status: Proposed
date: 2026-05-01
tier: process
concern:
  - governance
  - dev-experience
  - operations
composes: []
extends:
  - 18
  - 42
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0070 — Multi-Session Naval-Org Structure (CO/XO/COB/PAO/Yeoman)

**Status:** Proposed
**Date:** 2026-05-01
**Author:** XO (research session)
**Pipeline variant:** `sunfish-quality-control` (process-tier ADR; no production code; codifies
existing operating protocol)
**Council posture:** standard — no council dispatch required for process-tier ADR that codifies
existing runtime-verified behavior

**Resolves:** Stage 5 quarterly snapshot (PR #487) identified "process tier" as a documentation
gap; this ADR formalizes the multi-session coordination model that has been operating informally
since 2026-04, moving it from CLAUDE.md prose into the architectural decision trail.

---

## Context

Sunfish has been running a multi-session AI-assisted development protocol since approximately
2026-04. The protocol divides work across up to four concurrent Claude Code sessions, each with a
distinct role, scope, and filesystem-based communication channel. Work product from one session
reaches another through committed repo artifacts, not through direct chat. This design was
intentional: Claude Code sessions cannot share a live context, but they can share a git repository.

Until this ADR, the protocol lived only in [`CLAUDE.md`](../../CLAUDE.md) §"Multi-Session
Coordination" as operational guidance. It was never written down as a decision — with rationale,
alternatives considered, consequences, and revisit triggers. The protocol has now shipped enough
work (multiple cohort cycles, cross-session unblock chains, >65 merged ADRs, hundreds of PRs) to
merit a formal decision record.

The motivating pressures that produced this structure:

1. **Solo-maintainer throughput.** A single Claude Code session is bounded by context window,
   token budget, and session continuity. Splitting roles across sessions multiplies
   throughput without multiplying the human's cognitive load proportionally.
2. **Separation of concerns.** Research/design work (ADRs, intakes, gap analyses) and
   production-code work (PRs, scaffolds, CI fixes) require different thinking postures and
   different risk tolerances. Conflating them in one session produces context pollution.
3. **Cross-project coordination.** The Sunfish codebase and *The Inverted Stack* book are
   maintained in parallel. Keeping book-side editorial and technical-writing sessions
   subordinate to the same command structure prevents the two projects from diverging.
4. **Context-loss resilience.** Claude Code sessions compact and restart. A session that loses
   context to `/compact` or restart needs a reliable recovery path — the filesystem (git
   history, state files, project memory) is persistent where the session context is not.

This ADR does not propose a new structure. It records the decision to adopt the structure that
already operates, codifies its contracts, and establishes the revisit triggers that would prompt
a redesign.

---

## Decision drivers

1. **Scaling beyond single-session work.** Sequential development in one session cannot sustain
   the velocity required to close the Sunfish MVP backlog while simultaneously developing the
   companion book. Parallel sessions with scoped roles address this.
2. **Clean hand-offs between design and build.** The biggest risk in AI-assisted development is
   starting implementation before the design is stable. A hard role boundary (XO authors
   hand-offs; COB implements hand-offs) enforces this gate at the process level.
3. **Resilience to `/compact` events.** Project memories, state files, and the ICM pipeline
   artifacts all survive session restarts. The protocol is designed so that any session can
   recover full context from the filesystem alone, without needing the originating session's
   context.
4. **Cohort batting average.** As of 2026-04-30, the multi-eye review enabled by the
   multi-session structure — XO authors + council subagents review before COB builds —
   produced a 19-of-19 substrate amendment resolution rate: every substrate ADR that went
   through council review before merge caught real defects. The pre-merge council pattern
   emerged directly from the multi-session structure: XO can dispatch adversarial review
   sessions before flipping a workstream to `ready-to-build`.
5. **Cross-project dependency management.** PAO (book editor) needs Sunfish architecture
   decisions to stay current with the codebase. The chain CO → XO → PAO → Yeoman provides a
   clear escalation path: PAO files a `pao-question-*.md` beacon when a book-side question
   requires Sunfish architecture input that the book + Sunfish docs cannot answer.
6. **Accountability at every tier.** Each role has a named scope, owns a named artifact set,
   and cannot cross into another role's domain without escalation. This prevents the
   "everything is everyone's job" failure mode common in solo-maintainer projects.

---

## Considered options

### Option A — Single long-running session (status quo ante)

One Claude Code session handles research, design, implementation, review, and book-side
editorial.

- **Pro:** Simplest coordination model; no cross-session artifact protocol.
- **Pro:** No hand-off friction; the context is always in scope.
- **Con:** Context window and token budget cap throughput. One session cannot maintain
  Sunfish design quality AND ship production PRs AND manage book development simultaneously.
- **Con:** Research-quality work (ADRs, gap analyses, discovery docs) and
  production-code work (package implementations, CI fixes) have different risk/quality
  profiles. Conflating them degrades both.
- **Con:** A single session that loses context (via `/compact` or restart) loses everything;
  no structural recovery path other than manual re-prompting.
- **Rejected** as the default. Still appropriate for short bursts where one session type
  is the whole task (e.g., a pure architecture session with no implementation work in flight).

### Option B — Unstructured multi-session (ad-hoc)

Multiple sessions are opened as needed, with no formal role definitions, no hand-off protocol,
no state files.

- **Pro:** Lower overhead than a formal protocol; natural emergence.
- **Con:** Sessions collide. Without formal role separation, two sessions can make
  conflicting decisions (or implement before design is stable) without any signal.
- **Con:** No recovery path on restart. An ad-hoc session has no way to know what the
  other sessions have done since it last ran.
- **Con:** Cross-project (Sunfish + book) coordination fails silently. No signal path
  from book-side questions to architecture-side answers.
- **Rejected.** The absence of a formal protocol was observed before the naval-org structure
  was adopted; the collision failures it produces are the reason the structure was built.

### Option C — Naval-org structure with filesystem-as-IPC (this ADR) [RECOMMENDED]

Named roles (CO / XO / COB / PAO / Yeoman) with scoped ownership, artifact contracts,
and a committed filesystem as the inter-session communication channel.

- **Pro:** Role separation enforces the research/build gate. COB cannot implement
  something XO has not finished designing; the ledger row must say `ready-to-build`.
- **Pro:** All coordination state is in the repo. Session restarts recover fully from
  `git log`, `gh pr list`, `active-workstreams.md`, and project memory files.
- **Pro:** The research-inbox beacon pattern gives sub-XO sessions a signal path without
  requiring live session interaction.
- **Pro:** The fallback work order (6-rung ladder) keeps COB productive even when the
  priority queue is dry, and keeps XO informed of COB's state.
- **Pro:** Multi-eye review (XO dispatches council sessions; COB builds against reviewed
  specs) has produced measurable quality improvements: 19-of-19 substrate amendments
  needed and caught council-side before merge as of 2026-04-30.
- **Con:** Artifact-writing overhead. Hand-offs, ledger rows, beacon files, and memory
  files are process work, not product work. The cost is real.
- **Con:** Coordination lag. A COB beacon that XO does not process in the same loop
  iteration can leave COB blocked for hours. Mitigated by the 30-min fallback sleep
  and priority-queue re-poll pattern.
- **Con:** Context asymmetry. XO may author a hand-off based on the repo state at
  authoring time; by the time COB starts implementation, parallel merges may have
  changed the baseline. Mitigated by the pre-build checklist (verify `git log`, `but
  status`, `gh pr list` before acting on any hand-off).
- **Adopted.**

---

## Decision

**Adopt Option C: the naval-org structure with filesystem-as-IPC is the canonical
multi-session coordination model for Sunfish.**

The structure has been operating since 2026-04 and is codified here. CLAUDE.md remains the
authoritative operational manual (session instructions, detailed rules, status format);
this ADR is the architectural decision record and rationale trail.

### §1 — Command structure

```
CO (Chris Wood, BDFL)
└── XO (research session — this repo)
    ├── COB (sunfish-PM session — this repo)
    └── PAO (book editor session — book repo)
        └── Yeoman (book technical-writer session — book repo)
```

Four concurrent Claude Code sessions, each mapped to one role. Sessions share the Sunfish
repository filesystem and (via worktree + filesystem cross-writes) the book repository. They
cannot communicate directly via chat or API; all coordination is through committed git artifacts
and project memory files.

### §2 — Role definitions and scope ownership

| Role | Scope | What it owns | What it does NOT own |
|---|---|---|---|
| **CO** | Authority | All final decisions; architecture | Daily execution; any session role |
| **XO** | Research + cross-project PM | ADRs, intakes, design, retrofits, conventions, gap analyses; cross-project PM on demand | Production code; editorial direction |
| **COB** | Production code | Packages, scaffolds, PRs, CI fixes, deps; implements specs from XO | Architecture decisions; book editorial |
| **PAO** | Book editorial | Clarity, structure, voice, market positioning of *The Inverted Stack*; manages Yeoman | Sunfish architecture decisions; code |
| **Yeoman** | Book technical writing | Chapter drafts, revisions, audiobook pipeline; reports to PAO | Sunfish code/ADRs; architectural calls |

XO acts on delegated authority from CO. Business-impact decisions escalate as recommendation +
default rather than a blocking question. XO's PM coverage is on-demand synthesis, not a
maintained dashboard.

PAO is the cross-repo funnel for all book-side questions. Yeoman routes through PAO; direct
Yeoman → XO signals (using `yeoman-*` beacons) are a PAO-offline fallback only, and are flagged
`PAO-bypass` in the beacon body.

### §3 — Filesystem-as-IPC: the core pattern

Sessions coordinate through committed filesystem artifacts. There is no live IPC, no shared
memory, and no direct session-to-session chat. The filesystem is the message bus.

**What this means in practice:**

- XO finishes a hand-off → commits it → COB reads it at session start or polling interval.
- COB finishes a build → updates ledger row → XO reads it on next loop iteration.
- COB hits a design question → writes a beacon file → commits it → XO reads it at next
  loop scan, resolves it via hand-off or ledger update, archives the beacon.

The filesystem-as-IPC pattern has one critical requirement: all coordination state MUST be
committed (not just present on disk). Unstaged artifacts are invisible to other sessions.
The protocol mandates that ledger updates, hand-offs, and beacon files be committed in the
same PR as the work they describe.

**Recovery from `/compact` and session restart.** Any session that loses context recovers via:

```bash
git log --all --oneline -20          # recent commits
git status                           # current worktree state
gh pr list --state open              # open PRs (COB's in-flight work)
but status                           # GitButler workspace state
tail -n 40 .wolf/memory.md           # recent memory entries
cat icm/_state/active-workstreams.md # ledger state
ls icm/_state/research-inbox/        # pending beacons
```

This batch suffices to reconstruct session context without relying on the previous session's
compacted summary.

### §4 — Canonical state files

Three canonical files define the shared session state:

**`icm/_state/MASTER-PLAN.md`** — stable; goals + done-conditions + velocity baseline. Updated
only when goals or the velocity baseline shift. Not a task list; not a status dashboard.

**`icm/_state/active-workstreams.md`** — dynamic ledger; in-flight workstreams + owner + state.
Every session reads it at session start. State transitions are the primary coordination signal
between XO (who changes `design-in-flight` → `ready-to-build`) and COB (who changes
`ready-to-build` → `building` → `built`).

**`icm/_state/handoffs/<workstream>.md`** — per-workstream hand-off spec from XO to COB. Describes
what to build, file-by-file, with acceptance criteria. COB must not begin implementation until
a hand-off file exists AND the ledger row says `ready-to-build`. Hand-offs are authoritative;
if a hand-off is revoked (workstream returns to `design-in-flight`), COB stops and the hand-off
file is archived or updated.

### §5 — Workstream lifecycle vocabulary

The following status values appear in `active-workstreams.md`. All sessions use these terms
consistently; no synonyms.

| Status | Meaning | Who sets it |
|---|---|---|
| `design-in-flight` | XO is still working on the spec. **COB: do not implement.** | XO |
| `ready-to-build` | Spec is final; a hand-off file exists in `handoffs/`. COB may implement. | XO |
| `building` | COB is implementing. Other sessions: no parallel PRs on this scope. | COB |
| `built` | Implementation complete (committed/merged). | COB |
| `held` | Paused pending external decision (CO, third-party, or another workstream). | XO or CO |
| `blocked` | Depends on a workstream not yet resolved. Link the dependency. | XO |
| `superseded` | Replaced by another workstream. Link the replacement. | XO |

XO's pre-`ready-to-build` checklist for state transitions:
1. **Widen/revise mid-flight:** set intake `Status: design-in-flight`, update ledger row,
   revoke or update any existing hand-off. COB receives implicit stop signal via ledger.
2. **Design final:** write hand-off in `handoffs/<workstream>.md`; flip ledger row to
   `ready-to-build`; optionally write a project memory pointing at the hand-off.

COB's pre-build checklist (applies to every code change beyond a one-line fix):
1. Ledger row must say `ready-to-build`. If `design-in-flight` or `held`, STOP + write
   a `cob-question-*.md` beacon noting the unexpected state.
2. Intake or ADR `Status:` line must match `ready-to-build` (or be silent; ledger applies).
3. Hand-off file in `handoffs/<workstream>.md` must exist.
4. `gh pr list --state open` — check for in-flight PRs touching the same package, especially
   auto-merge-enabled ones.
5. `but status` or `git log --all --oneline -10` — verify no parallel-session work landed
   since the hand-off was authored.

### §6 — Live signaling: the research-inbox beacon protocol

The filesystem inbox at `icm/_state/research-inbox/` is the sub-XO sessions' signal path
to XO. Beacons survive session restarts because they are committed to git. Active beacons
live in the inbox root; resolved beacons are archived to `_archive/` in the same PR as the
resolution.

**File naming convention:**

```
{sender}-{type}-YYYY-MM-DDTHH-MMZ-{slug}.md
```

Where:
- `sender ∈ {cob, pao, yeoman}`
- `type ∈ {idle, question, resumed}`
- `slug` is a brief lowercase-hyphenated description

**Beacon body format** (strictly minimal):

```yaml
---
type: idle | question | resumed
workstream-or-chapter: <identifier>
last-pr: <PR number or "none">
---
<≤2 lines of context>
<≤2 lines of "what would unblock me">
```

**Who writes beacons:**

- **COB** writes `cob-*`: rung-6 idle → `cob-idle-*.md` + `ScheduleWakeup 1800s`;
  design-ambiguity halt → `cob-question-*.md` + halt the workstream + ledger row note.
- **PAO** writes `pao-*`: book-side question needing Sunfish architecture/ADR/workstream
  input that cannot be resolved from book + Sunfish docs alone → `pao-question-*.md`. PAO
  is the cross-repo funnel; Yeoman's questions go to PAO first via the book-local
  `.pao-inbox/` (Tier 1).
- **Yeoman** writes `yeoman-*` only as PAO-bypass fallback when PAO is offline AND a
  critical question cannot wait. Flag `PAO-bypass` in the beacon body.

**XO response protocol:**

- XO scans `icm/_state/research-inbox/` on every loop iteration:
  ```bash
  ls icm/_state/research-inbox/*.md 2>/dev/null
  ```
- Non-empty inbox → priority above ADR cadence. Resolve via hand-off update, ledger row
  change, or answer note. Archive the beacon (`git mv`) in the same PR.
- **Response SLA:** active beacons > 7 days unanswered → XO escalates to CO.
- **Archive policy:** weekly `chore(housekeeping): prune research-inbox archive` deletes
  `_archive/*.md` older than 30 days.

**Precedent from operation:** The W#18 Vendors unblock chain (PRs #325, #330, #333) was
triggered by COB beacons hitting design ambiguities in the encrypted-field substrate. XO
processed the beacon, authored 5 substrate ADR amendments (ADR 0046-A2/A3/A4/A5 + W#32),
flipped W#18 to `ready-to-build`, and the 8-phase build shipped cleanly on the strengthened
substrate. Total XO-to-COB cycle time: one session iteration.

### §7 — COB fallback work order (when priority queue is dry)

The priority queue is `active-workstreams.md` rows in `ready-to-build` state with a hand-off
file. When the queue is empty, COB does not idle. The fallback ladder:

| Rung | Work | Commit prefix |
|---|---|---|
| 1 | **Dependabot PR cleanup** — auto-merge per `project_pre_release_latest_first_policy`; skip CI failures + pinned packages | `chore(deps):` |
| 2 | **Build hygiene** — fix new warnings, deprecations, analyzer findings; flag design-judgment items to XO | `fix(build):` |
| 3 | **Style-audit P0** per `icm/07_review/output/style-audits/TIER-4-RE-AUDIT.md` | `chore(fallback):` |
| 4 | **Test coverage gap-fill** — tests for existing public surface; no behavior changes | `test(coverage):` |
| 5 | **Doc improvements** — XML docs, README gaps, `apps/docs/blocks/<block>.md` stubs | `docs:` |
| 6 | **Idle** — write `cob-idle-*.md` beacon + `ScheduleWakeup 1800s` | — |

Rules:
- Re-check priority queue after every merge; priority always wins over fallback.
- Cap concurrent fallback PRs at 3.
- Design-question halts at any rung → `cob-question-*.md` beacon + halt.
- XO commitment: maintain queue depth of 2–3 `ready-to-build` rows; pre-write next 1–2
  hand-offs after every PR merges.

### §8 — Pre-merge multi-eye review (council subagent pattern)

The multi-session structure enables a review posture that single-session development cannot
match: XO dispatches adversarial review subagents (the "council") before flipping a workstream
to `ready-to-build`, not after COB ships a PR.

Council dispatch is standard for:
- Every substrate ADR (foundation, kernel, cross-cutting contracts)
- Every `api-change` pipeline variant
- Any ADR where downstream consumers > 3

**Cohort evidence (2026-04-29 to 2026-04-30):** 19 substrate ADRs went through pre-merge
council review. All 19 needed at least one amendment based on council findings. Post-council
merged ADRs have produced zero post-acceptance amendments driven by symbol drift or structural
citation errors. The multi-session structure is the mechanism that makes this economical: XO
can dispatch a 5-subagent council batch while COB works on something else, then merge the
council fixes before COB ever starts implementing.

**ADR 0028-A7 (spinoff via W#43)** is a concrete example: council review found a structural
citation error in ADR 0028-A6 (a `required: true` field attributed to the wrong type);
XO filed a corrective amendment (A7) before COB built anything. The PR for A7 (referenced as
PR #480 in the W#43 spinoff) shipped cleanly.

---

## Consequences

### Positive

- **Role clarity eliminates the "who owns this decision?" ambiguity** that is endemic to
  solo-maintainer projects where the human wears every hat. The naval-org structure names
  the hat explicitly per task type.
- **Filesystem-as-IPC is crash-safe.** Lost session context is a recoverable event, not a
  catastrophe. The recovery batch (`git log`, `gh pr list`, `active-workstreams.md`,
  project memory) suffices to resume from any failure state.
- **Pre-merge council quality gate** has produced a 19-of-19 amendment catch rate on
  substrate ADRs, enabling higher implementation velocity with lower defect rate.
- **Cross-project coordination** (Sunfish + book) runs through a single XO funnel; the book
  side cannot accidentally drive architecture decisions, and the Sunfish side stays
  synchronized with the book narrative.
- **Fallback work order** keeps COB productive during research-queue dry spells, turning
  queue gaps into compounding quality improvements (deps, hygiene, tests, docs).

### Negative

- **Artifact-writing overhead.** Every state transition requires a ledger update, and every
  workstream requires a hand-off file. For a one-person project, this is real overhead. The
  payoff is only justified when session count > 1 and context-loss frequency is high.
- **Coordination lag.** XO and COB are never running simultaneously under normal operating
  conditions (one session active at a time). A beacon may wait hours for a response. The
  fallback ladder and 30-minute sleep mitigate this but don't eliminate the lag.
- **Rigid scope can overshoot.** A COB that discovers a design flaw mid-implementation must
  stop and write a beacon rather than fixing the design itself. This is the correct behavior,
  but it feels expensive when the fix would have taken five minutes. The constraint is
  intentional: mid-implementation design changes have historically been the source of the
  most expensive rework.
- **Structure scales only to ~4 active roles.** Adding a fifth session without a clear role
  definition produces the Option B (unstructured) failure mode inside the structure.

### Security and trust impact

No production trust model change. This ADR codifies a coordination protocol, not a capability
boundary or signature surface. The research-inbox beacon files are committed markdown; they
carry no secrets. The state files are git-tracked; they have the same trust posture as the
rest of the repo.

---

## Open questions

**OQ-1: Scaling beyond 4 sessions.** The current structure is sized for 4 active roles (XO,
COB, PAO, Yeoman). If Sunfish grows to involve external contributors who want a dedicated
session role (e.g., a security-review session), the command structure needs a new tier or a
lateral expansion. Deferred until the trigger fires: "3+ sustained external contributors"
(see ADR 0018 §1 transition triggers).

**OQ-2: Automated beacon processing.** Today XO reads the inbox manually on loop iteration.
A future enhancement could have a harness-level hook that fires on beacon commit. Not
designed; deferred pending evidence that manual processing is the bottleneck.

**OQ-3: PAO cross-repo worktree pattern.** PAO writes beacons to the Sunfish repo via a
worktree checkout from the book repo session. This works but is fragile — a PAO session
that does not have the Sunfish worktree set up cannot signal XO. A more robust pattern
(e.g., a shared signal file path agreed at the OS level) is deferred until the fragility
actually causes a dropped beacon.

---

## Revisit triggers

1. **3+ sustained external contributors join.** The structure assumes one human (CO) and
   a small number of AI sessions. External contributors need a defined role; the current
   structure does not have an "external contributor" tier. Open a follow-up ADR amendment.
2. **COB beacon queue exceeds 7 days unanswered.** This is already an XO-to-CO escalation
   trigger per the beacon SLA. If it happens more than once in a 30-day window, the
   coordination lag is a structural problem, not a one-off. Re-examine loop discipline and
   session wake frequency.
3. **Cross-session collision causes a production incident.** If two sessions produce
   conflicting commits that break `main` in a way `git revert` doesn't cleanly fix, the
   collision-avoidance mechanisms (pre-build checklist, ledger, beacon protocol) have
   failed. Diagnose the gap and amend this ADR.
4. **CLAUDE.md multi-session section diverges from this ADR.** CLAUDE.md is the operational
   manual (session-start instructions, status format, commit-type rules). This ADR is the
   decision record. If they contradict, CLAUDE.md wins for operational behavior; open an
   amendment here to realign the decision record.
5. **New AI session harness capability (live multi-session IPC, shared context).** If
   Claude Code or an equivalent harness gains the ability to share live context across
   sessions, the filesystem-as-IPC rationale changes materially. Re-evaluate whether
   the artifact protocol is still the right answer.

---

## Compatibility plan

No production code change. No package surface change. No migration. This ADR ratifies an
existing operating protocol; the only artifact produced is this document and the updated
ADR index.

---

## Implementation checklist

- [x] ADR file `docs/adrs/0070-multi-session-naval-org-structure.md` authored (this file)
- [ ] ADR index (`docs/adrs/README.md`) updated — add row for ADR 0070 in the index table
- [ ] ADR projection files (`INDEX.md`, `STATUS.md`, `GRAPH.md`) regenerated via
      `python3 tools/adr-projections/project.py --check-only` (must report 0 errors)
- [ ] PR opened with title `docs(adrs): 0070 — Multi-Session Naval-Org Structure
      (CO/XO/COB/PAO/Yeoman)`; auto-merge **NOT** enabled (process-tier ADR; CO review
      before merge)

---

## References

### Predecessor and sister ADRs

- [ADR 0018](./0018-governance-and-license-posture.md) — Governance Model (BDFL + ICM +
  UPF + ODF framework stack). ADR 0070 sits inside the ICM process tier defined there.
- [ADR 0042](./0042-subagent-driven-development-for-high-velocity.md) — Subagent-Driven
  Development. ADR 0070 defines the coordination layer *around* which subagent dispatch
  (ADR 0042) operates. XO dispatches council subagents; COB dispatches build subagents.
- [ADR 0043](./0043-unified-threat-model-public-oss-chain-of-permissiveness.md) — Unified
  Threat Model. Process ADRs (0070) compose with the threat model: beacon files and state
  files must not contain secrets; the research-inbox pattern is consistent with the
  public-OSS trust posture.

### Canonical operational documents

- [`CLAUDE.md`](../../CLAUDE.md) §"Multi-Session Coordination" — the authoritative
  operational manual. ADR 0070 is the decision record; CLAUDE.md is the session-instruction
  layer. When they differ in detail, CLAUDE.md governs session behavior; amend this ADR to
  realign.
- [`icm/_state/MASTER-PLAN.md`](../../icm/_state/MASTER-PLAN.md) — stable goals +
  velocity baseline.
- [`icm/_state/active-workstreams.md`](../../icm/_state/active-workstreams.md) — dynamic
  workstream ledger.
- [`icm/_state/handoffs/`](../../icm/_state/handoffs/) — per-workstream hand-off specs.
- [`icm/_state/research-inbox/`](../../icm/_state/research-inbox/) — beacon inbox.

### User memory (cross-session learning)

The following project memory files document the cohort lessons that directly shaped this ADR:

- `project_research_session_is_cto_role.md` — naval command structure adoption
  (2026-04-29); XO/COB/PAO/Yeoman canonical roles.
- `feedback_loop_discipline.md` — loop continuity; ScheduleWakeup convention; rung-6
  fallthrough per session type.
- `feedback_git_discipline.md` — worktree hygiene; pre-build verification batch; no
  destructive ops in loop.
- `feedback_verify_pr_state_at_session_start.md` — the session-start batch command pattern
  that enables `/compact`-recovery.
- `feedback_council_can_miss_spot_check_negative_existence.md` — the three failure modes
  of council subagents (false-negative, false-positive, structural-citation error) that
  motivated the pre-merge council posture.
- `feedback_use_inbox_not_status_reports.md` — beacon-channel discipline; COB signals
  idle via inbox, not CO chat.

### Operational precedents cited in this ADR

- **W#18 unblock chain (PRs #325, #330, #333):** COB beacons triggered XO substrate
  ADR amendment cycle (ADR 0046-A2/A3/A4/A5 + W#32); W#18 flipped to `ready-to-build`
  after amendments; 8-phase build shipped cleanly.
- **ADR 0028-A7 spinoff (W#43 / PR #480):** Council review found structural citation error
  in ADR 0028-A6; XO filed corrective A7 before COB started implementing.
  The multi-session council-before-build posture prevented a mid-implementation rework cycle.

---

## Pre-acceptance audit (§A0 self-audit)

- [x] **AHA pass.** Three options considered (single session, unstructured multi-session,
  naval-org). The naval-org was not the first idea; unstructured multi-session was observed
  and rejected based on collision failures. *(Anti-pattern #10: checked.)*
- [x] **FAILED conditions / kill triggers.** Named in Revisit triggers §1–5 and OQ-3.
  The structure is reversed if: external contributors exceed the structure's capacity
  (trigger 1); coordination lag becomes chronic (trigger 2); a collision causes a
  production incident (trigger 3); or a new harness capability makes filesystem-as-IPC
  obsolete (trigger 5). *(Anti-pattern #11: checked.)*
- [x] **Rollback strategy.** This ADR codifies existing behavior; "rollback" means
  reverting to CLAUDE.md-only guidance with no formal decision record. The operational
  protocol continues regardless of ADR status. The ADR can be `Superseded` if the
  structure changes materially; the doc itself has no runtime effect. *(Anti-pattern #4:
  checked.)*
- [x] **Confidence level.** HIGH. The protocol has been operating since 2026-04 across
  multiple session cohorts, with measurable outcomes (19-of-19 council catch rate,
  multi-hundred PR count). There are no speculative claims here — every assertion cites
  observed behavior. *(Anti-pattern #13: checked.)*
- [x] **Cited-symbol verification.** This is a process-tier ADR with no `Sunfish.*`
  type symbols. All citations are file paths, PR numbers, ADR numbers, and CLAUDE.md
  section names. File paths verified to exist; PR numbers are historical references.
  Cross-ADR claims (ADR 0018 §1 transition triggers, ADR 0042 subagent dispatch) verified
  against the cited ADR files. *(Anti-pattern #21: checked.)*
- [x] **Anti-pattern scan.** Critical APs 1, 3, 9, 11, 12, 21 checked. AP-1 (unvalidated
  assumptions): the protocol described here is observed behavior, not assumption. AP-3
  (vague success criteria): not applicable to a process ADR (no build phase). AP-9
  (skipping Stage 0): three options considered (§"Considered options"). AP-11 (zombie
  project): revisit triggers named. AP-12 (timeline fantasy): no implementation timeline
  claimed. AP-21 (hallucinated symbols): no code symbols cited. *(Checked.)*
- [x] **Revisit triggers.** Five named in §"Revisit triggers"; each has a concrete
  condition, not a calendar. *(Anti-pattern #11 again: checked.)*
- [x] **Cold Start Test.** The implementation checklist (4 steps) is executable by a
  fresh session from this ADR alone. No ADR-author clarification needed. *(Stage 2
  Check 5: checked.)*
- [x] **Sources cited.** Every factual claim has a reference: protocol behavior traced
  to CLAUDE.md; batting averages traced to user memory files and specific PRs/ADRs;
  precedent examples cite PR numbers. *(Anti-pattern #21 part 2: checked.)*

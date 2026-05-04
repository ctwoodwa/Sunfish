---
id: 72
title: Research-Inbox Beacon Protocol (Cross-Session Signaling)
status: Proposed
date: 2026-05-01
tier: process
concern:
  - governance
  - dev-experience
  - operations
composes:
  - 70
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0072 — Research-Inbox Beacon Protocol (Cross-Session Signaling)

**Status:** Proposed
**Date:** 2026-05-01
**Authors:** XO research session
**Pipeline variant:** `sunfish-quality-control`
**Consumer scope:** all Sunfish sessions (XO, COB, PAO, Yeoman)

---

## Context

Sunfish is operated by four Claude sessions running concurrently on a shared
filesystem: **XO** (research/design), **COB** (production implementation),
**PAO** (book editor), and **Yeoman** (book technical writer). These sessions
cannot communicate directly — each runs in an isolated Claude context with no
shared message bus, no inter-session API, and no real-time notification path.
They share the Sunfish git repository (and the book repository via a filesystem
symlink) as their only coordination medium.

ADR 0070 establishes the naval command structure that defines these roles and
their authority boundaries. Its §6 ("Live signaling: the research-inbox beacon
protocol") provides an operational specification of the mechanism — file
location, who writes what, the XO scan loop, archive cadence, escalation SLA,
and spam mitigation. ADR 0070 §6 also names two open questions explicitly:
**OQ-2** (automated beacon processing — currently human-driven on every loop
iteration) and **OQ-3** (PAO cross-repo worktree fragility — flagged as
deferred until the fragility actually causes a dropped beacon). ADR 0070 is
therefore not under-spec'd; it is a working operational specification with two
named gaps.

This ADR exists not to fill a deferred slot but to specify the protocol more
rigorously and to audit the as-built behavior against the spec. The protocol
has accumulated 13 archived beacons across two senders since 2026-04-29; that
empirical record exposes schema ambiguities, race conditions, and procedural
gaps that ADR 0070 §6's prose pass did not anticipate (notably: schema-key
divergence between `cob-*` and `pao-*` beacons, branch-cleanup omissions in
the PAO worktree procedure, and concurrent-writer race conditions on the
`_archive/` move). This ADR formalizes the schema as actually practiced,
documents the procedural gaps, and adds a §8 Concurrency section addressing
the race conditions identified pre-merge by canonical Opus council. ADR 0070
§6 remains the primary cross-reference for the policy framing; this ADR
supersedes its prose pass as the authoritative specification of the
mechanism's surface area.

The concrete problem the protocol solves: COB finishes a workstream and has no
`ready-to-build` row in the priority queue. Without a signaling channel, COB
either (a) polls `active-workstreams.md` on every iteration — which is coarse and
doesn't let COB describe *why* it's blocked or *what would unblock it* — or
(b) writes a status update somewhere in the repo and hopes XO notices during its
next pass over all state files. Neither is reliable. PAO encounters an analogous
problem: it needs to ask XO architecture questions without leaving notes scattered
across ADR files, CLAUDE.md, or chat (which survives only for one session).
Yeoman needs a tiered escalation path.

The existing practice — evidenced by the `icm/_state/research-inbox/_archive/`
directory accumulating beacons since 2026-04-29 — has already demonstrated that
the protocol works. The archive contains 13 resolved beacons from two senders
(`cob`, `pao`) across multiple session-days. Those beacons unblocked:

- W#31 taxonomy substrate (COB idled 2026-04-29T20-42Z after shipping PRs
  #258+#263; XO queued three follow-on workstreams in response; COB resumed
  within one loop iteration)
- W#18 vendor onboarding substrate (multiple `cob-question-*` beacons from
  2026-04-29/30 surfaced encrypted-field design questions; XO escalated to
  ADR 0046-A2/A3/A4/A5 chain; PRs #325/#326/#329/#330/#331/#333/#335/
  #337/#338 shipped as the complete unblock — 9 PRs across two days driven
  by a sequence of question beacons, not ad-hoc interruption)
- Book/Sunfish cross-repo incident recovery (PAO's `pao-incident-2026-04-30T07-35Z`
  beacon surfaced a destructive-git-operation incident; XO synthesized the
  cross-program implications and produced three new memory rules without
  requiring CO intervention)

The protocol has earned formalization. This ADR specifies it precisely, documents
the rationale for its key design choices, and establishes the archive and
escalation policies as explicit commitments rather than informal convention.

---

## Decision drivers

- **Filesystem-as-IPC is the only reliable coordination medium.** Sessions cannot
  call each other; cannot share a message queue; cannot rely on process signals.
  The git repository is always present, always readable, and survives session
  restarts. Any signaling mechanism must be buildable on top of git-tracked files.
- **Coarse polling (scan all state files) is not sufficient.** `active-workstreams.md`
  and `MASTER-PLAN.md` express planned state; they do not express session-local
  runtime events (COB blocked on a design question; PAO needs an architecture
  clarification; Yeoman encountered a book-Sunfish inconsistency). XO needs a
  single scan path that surfaces all live signals without reading the full state
  tree.
- **Signals must survive session restarts.** Claude sessions compact or restart
  without warning. A signal dropped because it lived only in session context is
  a lost coordination event. Committed files are the only durable medium.
- **Beacons must carry enough context for XO to act without follow-up.** A signal
  that says "COB is idle" but omits last workstream, last PR, and the unblock ask
  forces XO to reconstruct that context from state files. Structured body schema is
  not overhead; it is the affordance that makes scanning tractable.
- **Cross-repo signaling is a real requirement.** PAO operates from the book
  repository (`/Users/christopherwood/Projects/the-inverted-stack`). Its
  architecture questions cannot wait for a Sunfish-side session to notice a commit
  in the book repo. The protocol must handle beacons from outside the Sunfish
  working tree via a well-specified worktree procedure.
- **Archive and pruning are first-class requirements.** A protocol that accumulates
  resolved beacons indefinitely creates noise. An escalation policy for beacons
  that age past their useful life is required to prevent stale signals from
  misleading future sessions.
- **Spam mitigation must not depend on access control.** All in-tree sessions
  can write to `icm/_state/research-inbox/`. Access control is not practical for
  Claude sessions on a shared filesystem. Mitigation must rely on naming convention
  enforcement, CI lint, and the pruning policy.

---

## Considered options

### Option A — Chat layer between sessions

Each sub-XO session appends a message to a chat-log file (e.g.,
`icm/_state/chat.md`) which XO reads at each iteration.

**Pro:** Familiar; minimal schema discipline.
**Con:** Chat-log grows unboundedly; no structured lifecycle; XO cannot distinguish
actionable from resolved entries at a glance. Multiple concurrent writers cause merge
conflicts; rebase history for a chat log is noisy.
**Verdict: Rejected.** Unstructured append-only log does not scale to multiple
concurrent senders.

### Option B — State-file polling with per-session status flags

Each session maintains a status section in `active-workstreams.md` or a per-session
status file (e.g., `icm/_state/cob-status.md`). XO scans all status files on each
iteration to detect idle/blocked/question states.

**Pro:** No new directory; no naming convention required.
**Con:** XO must scan N per-session files on every iteration even when there is
nothing to report — the common case. No lifecycle (active vs. resolved); XO cannot
distinguish acted-on status flags from new ones. Body schema undefined; signals have
no consistent shape for programmatic scanning.
**Verdict: Rejected.** Coarseness and lack of lifecycle management are disqualifying.
Option B describes the state before the inbox protocol was introduced; the scattered
COB status notes across `active-workstreams.md` and CLAUDE.md are the evidence.

### Option C — Dedicated inbox directory with structured filenames [RECOMMENDED]

A single scan-path directory (`icm/_state/research-inbox/`) holds active beacon
files. Naming convention encodes sender, type, timestamp, and slug. Body schema
is structured YAML frontmatter + prose. Resolved beacons are moved to `_archive/`
in the same PR that acts on them. Archive is pruned periodically.

**Pro:** Single scan path — `ls icm/_state/research-inbox/*.md` gives the full
active signal set at O(1) filesystem cost. Empty directory = nothing to do.
**Pro:** Structured filenames support sender filtering and type-based routing
without parsing file bodies. XO can filter to `cob-question-*` vs `pao-*` at
the shell level.
**Pro:** Lifecycle is explicit. Active = present in root. Resolved = moved to
`_archive/`. No ambiguity; no stale signals in the scan path.
**Pro:** YAML frontmatter enables future tooling (e.g., a CI lint that validates
beacon shape; a dashboard script that counts open signals by sender).
**Pro:** Cross-repo writes are handled by a well-specified worktree procedure
(sender `cd`s to the Sunfish repo via a temporary worktree; writes the file;
commits and pushes). The protocol is file-system-level; the git transport is
standard.
**Pro:** Proven in practice. The `_archive/` directory contains 13 resolved
beacons from the 2026-04-29/30 period; every one of them contributed to an
unblock or a governance improvement. The pattern works; this ADR formalizes it.
**Verdict: Adopted.** See Decision section for full protocol specification.

---

## Decision

**Adopt Option C: the Research-Inbox Beacon Protocol.**

`icm/_state/research-inbox/` is the canonical cross-session signaling path for
sub-XO sessions → XO. The protocol is specified below.

---

### §1 Inbox location and lifecycle

**Active beacons:** `icm/_state/research-inbox/<filename>.md`

**Resolved beacons:** `icm/_state/research-inbox/_archive/<filename>.md`
(moved by XO in the same commit that acts on the beacon)

**Scan command (XO, every loop iteration):**
```
ls icm/_state/research-inbox/*.md 2>/dev/null
```
Non-empty output → at least one active beacon; process before proceeding to
ADR cadence or other XO work.

---

### §2 File naming convention

```
{sender}-{type}-YYYY-MM-DDTHH-MMZ-{slug}.md
```

**`sender`** — one of:

| Value | Who writes | Context |
|---|---|---|
| `cob` | COB (production implementation session) | Sunfish repo |
| `pao` | PAO (book editor session) | Book repo, via worktree |
| `yeoman` | Yeoman (book technical writer) | Book repo, PAO-bypass only |
| `routine` | Scheduled/automated process | CI or ScheduleWakeup signal |

**`type`** — one of:

| Value | When to use |
|---|---|
| `idle` | Sender's priority queue is empty; requesting new work |
| `question` | Sender is blocked on a design or architecture decision |
| `resumed` | Sender is back online after an absence; acknowledging re-entry |
| `status` | Informational signal not requiring XO action |
| `maintenance` | Reporting a completed housekeeping or maintenance run |

**Timestamp format:** ISO-8601 UTC, second precision, dashes for colons:
`2026-05-01T14-30-22Z`. This is the moment the beacon is written, not the
moment the triggering event occurred. (The 13 archive beacons use the
earlier minute-precision form `YYYY-MM-DDTHH-MMZ`; that form is grandfathered
for the existing archive but is not canonical for new beacons. Second
precision is required for new beacons to prevent filename collisions across
concurrent writers — see §8.)

**Slug:** 2-5 hyphen-separated lowercase tokens summarizing the beacon content.
Tokens may be plain words or alphanumeric codes (e.g., workstream IDs like `w28`,
phase codes like `p5c4`, or chapter IDs like `ch22`).
Examples: `w31-built-queue-dry`, `w18-encrypted-field-design`,
`w19-p3-prereqs`, `p5c4-w20-substrate-adaptation`.

**Full filename examples:**
```
cob-idle-2026-05-01T14-30Z-queue-dry.md
cob-question-2026-04-30T03-58Z-w28-p5c4-capability-verifier.md
pao-question-2026-05-02T09-00Z-dynamic-forms-api-question.md
yeoman-status-2026-05-03T11-00Z-ch22-draft-complete.md
```

---

### §3 Body schema

The body schema formalizes the practice observed across the 13 archived
beacons. The schema deliberately accepts the per-sender key divergence
(`workstream` for `cob-*` beacons, `chapter` for `pao-*` beacons) and the
optional metadata keys that have appeared organically (`filed-by`, `filed-at`,
`severity`, `sender`, `date`, `from`, `to`). This is the **formalize-practice**
path: the spec describes what beacons actually look like, not a tighter
schema that would invalidate the archive. A subsequent ADR amendment may
tighten the schema once the operational record justifies the migration cost.

**Skeleton:**

```
---
type: <type>
<sender-specific work key>: <value>
last-pr: <last PR merged or opened by this sender; "none" if no PR>
[optional metadata keys per sender type]
---

<≤2 lines of context: what was just shipped / what is blocked / why this beacon>

<≤2 lines of "what would unblock me" / what XO action is requested>
```

**Required keys (all senders): 3.** Every beacon MUST carry these three keys:

| Key | Value |
|---|---|
| `type` | beacon type (see enum below) |
| `<work key>` | sender-specific (`workstream` or `chapter`; see table) |
| `last-pr` | last PR merged or opened by this sender; `none` or `n/a` if no PR |

**Sender-specific work key:**

| Sender | Work key | Accepted values |
|---|---|---|
| `cob` | `workstream` | numeric workstream ID (e.g., `31`, `28`), `multi` (cross-workstream), or `none` (no specific workstream) |
| `pao` | `chapter` | chapter ID (e.g., `Ch22`), `process / cross-cutting (no specific chapter)`, or `n/a (program-level)` |
| `yeoman` | `chapter` | chapter ID; PAO-bypass beacons MUST also include the literal text "PAO-bypass" in the context block |
| `routine` | `workstream` | reserved for future use; use `none` when no workstream context |

**Optional keys (any sender):**

| Key | When to use |
|---|---|
| `filed-by` | Sender role for clarity (e.g., `COB`, `PAO`) — used by 4 of 13 archive beacons |
| `filed-at` | Timestamp duplicating filename timestamp — useful when filename truncation is suspected |
| `sender` | Alternative to `filed-by`; used by 1 archive beacon |
| `date` | ISO-8601 form of the timestamp (e.g., `2026-04-30T07:35Z`) — used by 2 archive beacons |
| `from` / `to` | For PAO `resumed` beacons documenting cross-program re-entry |
| `severity` | For incident-class beacons; values `low` / `medium` / `high` / `critical` |

**Total key count range:** 3-7 keys per beacon. Empirically: 3 of 13 archive
beacons use the minimum 3 keys; 5 of 13 use 5 keys; 1 uses 6; 1 uses 7. Any
key not enumerated above MUST be approved via ADR amendment before use.

**`type` enum (current archive + spec extensions):**

| Value | When used |
|---|---|
| `idle` | sender priority queue empty; short form (used by `cob-*` beacons) |
| `question` | sender blocked on a design decision; short form |
| `resumed` | sender back online; short form |
| `cob-idle` | sender-prefixed long form (functional equivalent of `idle`); used in 1 of 3 cob-idle beacons |
| `cob-question` | sender-prefixed long form (equivalent of `question`); used in 4 of 8 cob-question beacons |
| `pao-incident` | PAO incident report (extended-context allowed); 1 archive beacon |
| `pao-resumed` | PAO re-entry; 1 archive beacon |
| `status` | informational signal not requiring XO action (reserved; spec extension) |
| `maintenance` | completed housekeeping or maintenance run (reserved; spec extension) |

Both short forms (`idle`, `question`, `resumed`) and sender-prefixed long
forms (`cob-idle`, `cob-question`, `pao-incident`, `pao-resumed`) are
canonical. The sender-prefixed form gives filename-grep symmetry with the
filename's `{sender}-{type}-...` prefix; the short form is more concise.
Senders MAY use either; XO MUST recognize both.

**Body rules:**

- Context block: target ≤2 lines, ≤120 characters each. Facts only; no narrative.
- Unblock block: target ≤2 lines, ≤120 characters each.
- **`type: question` (or `cob-question`) beacons MUST contain exactly one
  concrete ask.** Compound asks (two unrelated questions in one beacon) MUST
  be split into separate `question` beacon files. Beacon proliferation from
  over-splitting is a named negative consequence; batching genuinely related
  sub-questions into one ask is acceptable.
- Beacons with `type: status`, `pao-incident`, or other extended-context
  classifications MAY exceed the prose targets when documenting an incident
  or multi-phase decision; the body MUST justify the deviation in its first
  line (e.g., "Extended-context beacon: incident report covering [scope].").
  All other types (`idle`, `question`, `resumed`, `cob-idle`, `cob-question`,
  `pao-resumed`, `maintenance`) MUST conform to the 2-line target.
- The `pao-incident-2026-04-30T07-35Z-destructive-action-reset-hard.md`
  beacon in the archive is the canonical extended-context example. Its
  `type: pao-incident` and 6-key frontmatter are spec-conformant under this
  schema.

---

### §4 Who writes what

**COB writes `cob-*` beacons:**

- **`cob-idle-*.md`**: When the priority queue (i.e., `active-workstreams.md`
  rows with `ready-to-build` status and a hand-off file) is empty at rung-6 of
  the fallback work order. Write the beacon, then `ScheduleWakeup 1800s`. Do not
  write multi-paragraph status reports to CO; the beacon is the signal.
- **`cob-question-*.md`**: When a design-ambiguity halt occurs during Stage 06
  build — a decision is needed that COB cannot make from the hand-off spec alone.
  Write the beacon, halt the workstream, and add a note to the ledger row in
  `active-workstreams.md`. Do not proceed or guess; wait for XO response.

**PAO writes `pao-*` beacons:**

PAO operates from the book repository. To write a beacon to the Sunfish inbox,
PAO adds a Sunfish worktree from the book repo shell. The procedure captures
the timestamp into a shell variable so the worktree-creation and cleanup steps
agree on the same branch name (a naive double-invocation of `date` can cross a
minute boundary at the 59-second mark and produce different branch names);
the `-u` flag forces UTC so the trailing `Z` in the filename is honest:

```bash
TS=$(date -u +%Y%m%dT%H%MZ)
WT=/tmp/sunfish-pao-signal-wt
BRANCH=pao/signal-$TS
git -C /Users/christopherwood/Projects/Sunfish worktree add "$WT" -b "$BRANCH" origin/main
# Write the beacon file
# Commit + push + open PR (with --auto-merge per pre-merge council canonical)
git -C /Users/christopherwood/Projects/Sunfish worktree remove "$WT"
git -C /Users/christopherwood/Projects/Sunfish branch -d "$BRANCH"   # after PR has merged
```

**Both cleanup steps are required.** `git worktree remove` deletes the
worktree directory but leaves the local branch `pao/signal-<timestamp>`
intact in the Sunfish repo's branch list. `git branch -d` deletes that local
branch reference. Without both, every PAO signal accumulates a dangling local
branch that complicates `git branch -a` and `gh pr list --state open` output.
Run `git branch -d` only **after** the PR has merged (or after confirming the
branch is no longer needed); attempting `-d` on an unmerged branch fails
safely. Use `-D` (force) only when the PR was closed without merge and the
branch is intentionally being abandoned.

**Known fragility (inherits ADR 0070 OQ-3).** ADR 0070 §6 OQ-3 flagged the
PAO cross-repo worktree pattern as fragile and "deferred until the fragility
actually causes a dropped beacon." This ADR codifies the procedure but does
not resolve OQ-3; the failure modes (mid-procedure session crash leaving a
half-written worktree; `origin/main` advancing during the worktree's life
producing a beacon based on stale state) remain. If a beacon is ever
demonstrably dropped, the resolution path is to revisit OQ-3 and consider an
in-tree alternative (e.g., a book-repo-local script that pushes via Sunfish's
HTTPS remote without a worktree).

PAO writes beacons when it encounters a question requiring Sunfish architecture,
ADR, or workstream context that the PAO cannot resolve from the book + Sunfish
docs alone. PAO is the **cross-repo funnel**: Yeoman's book-side questions go to
the book-local `.pao-inbox/` (Tier 1) first; PAO escalates to this inbox only
when the question has a genuine Sunfish-architecture dimension.

**Yeoman writes `yeoman-*` beacons:**

Yeoman writes to this inbox **only** as a PAO-bypass fallback when PAO is
confirmed offline AND a critical Sunfish question cannot wait. Beacons written
in this mode must include the text "PAO-bypass" in the context block. Yeoman's
normal questions go to the book-local `.pao-inbox/`, not here.

**Routine/automated writes `routine-*` beacons:**

Reserved for CI jobs or ScheduleWakeup-triggered processes that need to surface
completion signals or anomalies to XO. Not yet in use; reserved for future
tooling.

---

### §5 XO processing protocol

**Every loop iteration:** run `ls icm/_state/research-inbox/*.md 2>/dev/null`.

- **Empty:** continue to ADR cadence / normal XO work.
- **Non-empty:** process all active beacons **before** other XO work.

**Processing a beacon:**

1. Read the beacon file.
2. Classify: `idle` → queue a hand-off (or acknowledge if queue is being
   built); `question` → answer + update ledger/hand-off + resume workstream;
   `status/maintenance` → acknowledge + archive; `resumed` → update memory.
3. Write the response (hand-off update, ADR amendment stub, memory note, or
   inline answer in ledger) as part of the same or a concurrent PR.
4. `git mv` the beacon to `_archive/` in the same commit that delivers the
   response. Active beacon + response PR merge simultaneously.

**Escalation to CO:** if a beacon has been active for >7 days without XO
response (measured from the timestamp in the filename), XO escalates to CO
in the same turn it processes the beacon. The escalation note should name
the beacon, its age, and the XO judgment about why it was not resolved (e.g.,
"blocked on CO architectural decision" vs. "XO processing delay").

---

### §6 Archive and pruning policy

**Archive:** `icm/_state/research-inbox/_archive/`

Resolved beacons are moved here (not deleted) so that the resolution history
is queryable. The archive is searched when diagnosing recurring failure modes
(e.g., "has COB asked about encrypted-field design before?").

**Pruning:** `chore(housekeeping): prune research-inbox archive` runs weekly
(or on-demand when the archive exceeds ~20 files). The pruning rule:

```
Archive files with a filename timestamp older than 30 days from today → delete.
```

This is a hard prune (git rm), not a soft archive. After 30 days, beacons have
no operational value; their lessons should have migrated to memory files, ADR
amendments, or `.wolf/cerebrum.md`. Pruning keeps the archive navigable.

**Never prune active beacons.** Files in the root (not `_archive/`) are never
deleted by automated pruning. Escalation to CO is the correct path for aged
active beacons.

---

### §7 Trust model and spam mitigation

Any session with write access to the Sunfish repo can create a beacon. There is
no access-control gate. Mitigation layers:

1. **Naming convention enforcement.** Beacons with malformed filenames (wrong
   sender prefix, invalid type, wrong timestamp format) are ignored by XO's
   processing logic (`ls *.md` returns all `.md` files; XO applies the naming
   convention filter before acting; malformed files are flagged in the loop
   iteration log and XO writes a memory note). A CI lint step (see Implementation
   checklist) validates filenames at PR merge time and flags violations before
   they reach the scan path.
2. **PR-gated writes.** Beacons written from feature-branch PRs are reviewed
   before landing on `main`. The PR diff makes the beacon content visible;
   no surprise files appear without a PR record.
3. **Pruning.** Spam beacons accumulating in `_archive/` are pruned after
   30 days.
4. **XO discretion.** XO is not obligated to act on every beacon. A beacon
   classified as `status/maintenance` that requires no XO action is archived
   immediately with a note. A beacon from an unknown sender (e.g., a file
   prefixed `unknown-`) is treated as out-of-scope and archived with a "not
   actionable" note.

---

### §8 Concurrency and uniqueness

The protocol assumes XO is the sole writer that performs the `git mv beacon
_archive/` step (§5 step 4). It does **not** assume single-writer semantics
across the inbox as a whole — sub-XO senders can write new beacons while XO
is processing existing ones, and PAO can open a PR from a worktree while XO
opens a parallel archive PR. This section names the race conditions and
specifies the rules that prevent them from resurrecting archived beacons or
producing silent collisions.

**Filename precision.** Beacons MUST use second-precision timestamps:
`YYYY-MM-DDTHH-MM-SSZ` (e.g., `2026-05-04T14-30-22Z`). The earlier
minute-precision form (`YYYY-MM-DDTHH-MMZ`) is grandfathered for the 13
archive beacons but is no longer canonical for new beacons. Second precision
makes filename collisions vanishingly unlikely across concurrent writers; the
prior minute precision had a real collision risk when multiple sessions
fired within a 60-second window.

**Filename collision tiebreaker.** If two senders generate identical
filenames despite second precision (e.g., both fire at the exact same wall
clock second), the second writer to push appends a 4-character random
hexadecimal suffix to the slug: `...slug-a3f7.md`. The first writer's PR
keeps the un-suffixed name. The PR-review gate makes this resolution visible.

**Archive-vs-root precedence rule.** If a beacon's filename appears in
`_archive/` AND ALSO in the inbox root (e.g., after a rebase that introduces
a copy of a previously-archived beacon), the **root-version is the canonical
state** and the archive entry is the obsolete pre-archive snapshot. Readers
(including XO scan logic) MUST NEVER resurrect a beacon by copying it from
`_archive/` back to the root. The archive is read-only with respect to the
active inbox.

**Rebase-introduces-archived-beacon rule.** A rebase that re-introduces a
beacon file already-archived on `origin/main` is an error condition. The
canonical resolution is to delete the rebased copy from the rebased branch
(`git rm icm/_state/research-inbox/<filename>`) before pushing. The
archived version on `origin/main` remains the authoritative resolution
record. This most often arises when a sub-XO PR was authored against an
older `origin/main` and is rebased after XO archived an unrelated beacon
in the same iteration; the rebase-resolution step is mechanical.

**Archive-during-write race.** If XO archives a beacon that a sub-XO sender
is in the process of writing (sub-XO PR in flight against an older `origin/main`):

1. XO's archive PR lands first; the beacon moves from root to `_archive/`.
2. Sub-XO's PR rebases on the new `origin/main`. The rebase reveals that the
   beacon now lives in `_archive/`, not in root.
3. Sub-XO's PR MUST treat the absence of the beacon from root as "already
   archived" — i.e., the signal has been received and resolved. Sub-XO MUST
   NOT re-introduce the beacon to the inbox root. If sub-XO's PR contains
   only the beacon and no other changes, sub-XO closes the PR without merge.
   If sub-XO's PR contains additional changes, sub-XO drops the beacon-add
   from the PR and proceeds with the remaining changes.

**XO single-writer constraint on `_archive/`.** XO MUST NOT batch multiple
`git mv` operations against the same `_archive/` directory across PRs that
are in flight simultaneously. Each archive operation is one PR; PRs are
serialized by `gh pr merge --auto --squash` against the auto-merge queue.
This prevents merge conflicts from interleaved archive operations.

**Detection (Open Question).** A CI lint job that greps for filename
collisions across `icm/_state/research-inbox/*.md` and
`icm/_state/research-inbox/_archive/*.md` would catch the rebase-introduces-
archived-beacon failure mode automatically. Implementation deferred to OQ-4
(below); the rule is enforceable manually until then.

---

## Consequences

### Positive

- **Single scan path, low overhead.** `ls icm/_state/research-inbox/*.md` is
  the complete XO signal check. No parsing of multi-thousand-word state files to
  determine if there is a signal.
- **Lifecycle clarity.** Active = in root. Resolved = in `_archive/`. XO never
  processes the same beacon twice unless it intentionally re-opens the archive.
- **Proven pattern.** 13 beacons resolved across 2 session-days before formalization;
  every one contributed to a concrete unblock or governance improvement.
- **Cross-repo coordination without a shared message bus.** PAO can signal XO
  from the book repo using a standard worktree procedure; no book-repo-side
  tooling required.
- **Structured schema enables future tooling.** A CI lint that validates beacon
  frontmatter, a dashboard script that counts open signals by type, or an
  automated escalation check are all implementable from the naming convention +
  frontmatter without schema migration.
- **Audit trail.** The `_archive/` directory preserves the full resolution
  history; recurring patterns (e.g., COB repeatedly blocked on the same class
  of design question) surface through archive review.

### Negative

- **One more file to write.** COB, PAO, and Yeoman must remember to write a
  beacon rather than chat-interrupting or writing ad-hoc notes. The marginal
  cost is low (the file template is short) but it is non-zero cognitive overhead
  for session participants.
- **Resolution latency is bounded by XO loop frequency.** If XO is not actively
  looping (e.g., CO has halted the session), beacons accumulate without response.
  The 7-day escalation threshold provides a backstop but does not eliminate latency.
- **Beacon proliferation risk.** A sender that writes a beacon per micro-question
  (rather than batching related questions) degrades the inbox into a chat log.
  The "≤2 lines" body constraint and the "one concrete ask" rule mitigate this;
  XO should flag over-fine-grained beacons in the archive note.

### Trust impact

The beacon files themselves contain no sensitive data — they reference workstream
IDs, PR numbers, and architecture question descriptions, all of which are already
in the public repo. The protocol does not create a new data-sensitivity boundary.

The write-access model (any session can write a beacon) is consistent with the
existing repo access model for all in-tree sessions. PR-gated writes provide the
same review gate as all other file changes.

---

## Compatibility plan

The `icm/_state/research-inbox/` directory and `_archive/` subdirectory exist
today. The 13 beacons currently in `_archive/` pre-date this ADR and were written
under the informal convention; they conform to the naming scheme and body schema
specified here. No migration is required.

The `type` enum in §2 adds two new values (`status`, `maintenance`) beyond what
was in informal use (`idle`, `question`, `resumed`). Existing beacons in the
archive are unaffected; new beacons should use the full enum.

---

## Implementation checklist

- [ ] Verify `icm/_state/research-inbox/` directory exists (it does; no action
  required)
- [ ] Verify `icm/_state/research-inbox/_archive/` directory exists (it does;
  no action required)
- [ ] Add this ADR to `docs/adrs/INDEX.md` under the `process` tier
- [ ] Add this ADR to `docs/adrs/README.md` index table
- [ ] Add this ADR to `docs/adrs/STATUS.md` (Proposed status)
- [ ] Add `CLAUDE.md` cross-reference: update §"Live signaling to XO —
  `research-inbox/`" to cite ADR 0072 as the protocol specification
- [ ] Add a CI lint step that validates beacon filenames match the naming
  convention regex; report violations as warnings (non-blocking initially;
  upgrade to blocking when the scan path is hardened per §7 layer 1)

---

## Open questions

1. **`routine` sender.** The spec reserves `routine` for CI/ScheduleWakeup
   signals. No concrete use case exists yet. If a future CI job needs to surface
   an anomaly to XO (e.g., "dependabot backlog exceeds 10 PRs"), the `routine`
   sender type is the correct path. Activate when the first use case arises.
2. **Beacon body schema extension.** The 3-key frontmatter schema is intentionally
   minimal. If a sender type needs additional context fields (e.g., PAO beacons
   benefiting from a `chapter-status` field), propose via ADR amendment rather
   than adding keys ad-hoc.
3. **Automated escalation.** The 7-day threshold is currently a human-enforced
   policy (XO checks beacon age during each iteration). A CI job that comments
   on old beacons or opens a GitHub issue would harden the escalation path.
   Low priority; the policy is sufficient for current session frequency.
4. **Filename-collision CI lint (§8 detection).** A CI job that greps for
   filename collisions across `icm/_state/research-inbox/*.md` and
   `icm/_state/research-inbox/_archive/*.md` would catch the
   rebase-introduces-archived-beacon failure mode automatically. The §8
   archive-vs-root precedence rule is enforceable manually until then; CI
   automation upgrades enforcement from human-attentive to mechanical.
   Trigger to implement: first occurrence of a beacon being silently
   resurrected, or beacon volume exceeding 5 active beacons (whichever is
   sooner).

---

## Revisit triggers

This ADR should be re-evaluated when **any one** of the following occurs:

1. **A third contributor joins.** The protocol assumes three sub-XO senders
   (COB, PAO, Yeoman). A fourth sender requires evaluating whether the naming
   convention extends cleanly or needs a new sender prefix.
2. **Session-to-session communication primitives become available.** If a future
   Claude Code release enables direct inter-session messaging (e.g., a shared
   context object or an IPC API), re-evaluate whether the filesystem inbox
   remains the right IPC mechanism or whether it should become a thin adapter
   over the native primitive.
3. **Beacon volume exceeds 10 active beacons routinely.** High beacon volume
   suggests either (a) XO loop frequency is too low (non-protocol issue) or
   (b) the protocol is being used for micro-communication that should be
   session-local. Diagnose and either raise the loop frequency or tighten the
   usage policy.
4. **ADR 0070 is superseded.** The naval command structure defined in ADR 0070
   determines who the valid senders are. If the org structure changes (e.g.,
   COB and XO merge into a single session, or a new accelerator introduces a
   fifth session role), update the sender enum and PAO cross-repo procedure.
5. **Book repo moves.** The PAO cross-repo worktree procedure hard-codes
   `/Users/christopherwood/Projects/the-inverted-stack`. If the book repo
   moves, update §4 accordingly.

---

## References

### Predecessor and sibling ADRs

- [ADR 0042](./0042-subagent-driven-development-for-high-velocity.md) — subagent
  dispatch pattern. The research-inbox protocol extends the coordination model:
  ADR 0042 covers how the controller orchestrates parallel subagents; ADR 0072
  covers how persistent sessions (not transient subagents) signal the XO across
  iteration boundaries.
- [ADR 0043](./0043-unified-threat-model-public-oss-chain-of-permissiveness.md)
  — trust model for the repo. The beacon protocol's trust impact section (§7)
  is consistent with the chain-of-permissiveness analysis; beacons do not
  introduce new trust boundaries.
- **ADR 0070** (merged via PR #489) — naval command structure. ADR 0072 composes
  ADR 0070: 0070 names the protocol exists; 0072 specifies it. The
  `composes: [70]` frontmatter field records this dependency.

### Canonical protocol specification

- [`CLAUDE.md`](../../CLAUDE.md) §"Live signaling to XO — `research-inbox/`"
  — the operational prose that preceded this ADR; this ADR supersedes the prose
  as the authoritative specification.

### Operational artifacts

- `icm/_state/research-inbox/` — active beacon directory (the subject of this ADR)
- `icm/_state/research-inbox/_archive/` — resolved beacon directory; currently
  contains 13 resolved beacons from the 2026-04-29/30 period including the PAO
  incident report that demonstrated the protocol's cross-repo utility

### Unblock chains driven by beacons (evidence of protocol value)

- **W#31 taxonomy substrate unblock (2026-04-29):** `cob-idle-2026-04-29T20-42Z-
  31-built-queue-dry.md` signaled XO at rung 6; XO queued three follow-on
  workstreams; COB resumed within one loop iteration.
- **W#18 vendor substrate unblock (2026-04-29/30):** sequence of `cob-question-*`
  beacons surfaced encrypted-field design questions; XO escalated to ADR
  0046-A2/A3/A4/A5 chain; PRs #325/#326/#329/#330/#331/#333/#335/#337/#338
  (9 PRs, two days) shipped as the complete unblock.
- **PAO incident recovery (2026-04-30):** `pao-incident-2026-04-30T07-35Z-
  destructive-action-reset-hard.md` surfaced cross-program implications of a
  destructive git operation; XO synthesized three new memory rules without
  requiring CO intervention.

### Related process ADRs (sibling candidates, NOT authored here)

- Hand-off template ADR — specifying `icm/_state/handoffs/<workstream>.md`
  format. Separate dispatch per the XO authoring queue.
- Session startup protocol ADR — specifying the batch of git/gh/worktree state
  checks XO and COB run at session start (per `CLAUDE.md` §"When parallel-session
  work surprises you"). Separate dispatch.

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Two alternatives considered (Option A: chat log; Option B:
  state-file polling). Both rejected with documented rationale. Option C is not
  the first idea — the problem with unstructured logging was the starting
  observation, and the dedicated-inbox approach was the third iteration.
- [x] **FAILED conditions / kill triggers.** Named: if beacon volume exceeds 10
  active beacons routinely, the protocol is being misused and should be tightened
  or replaced. If inter-session communication primitives become available natively,
  re-evaluate whether this protocol remains the right mechanism.
- [x] **Rollback strategy.** The protocol requires no code changes and no
  infrastructure. Rolling back = stop writing beacons and remove the inbox
  directory convention from CLAUDE.md. No migration required; resolved beacons
  remain in `_archive/` for historical reference.
- [x] **Confidence level.** HIGH. The protocol has been in operational use since
  2026-04-29 with 13 resolved beacons demonstrating its value. The specification
  here formalizes observed behavior rather than proposing untested design.
- [x] **Cited-symbol verification.** No `Sunfish.*` code symbols are cited in
  this ADR. All cited artifacts are filesystem paths, PR numbers, and ADR
  references, all of which have been verified to exist.
- [x] **Anti-pattern scan.** AP-1 (unvalidated assumptions): the "13 beacons"
  claim is grounded in `_archive/` directory contents verified before writing.
  AP-3 (vague phases): implementation checklist uses observable/verifiable items.
  AP-9 (skipping Stage 0): two alternatives were considered and rejected.
  AP-11 (zombie project): three named kill triggers. AP-12 (timeline fantasy):
  no timelines asserted. AP-21 (assumed facts without sources): PR citations and
  archive references grounded in inspected files.
- [x] **Revisit triggers.** Five named conditions (§"Revisit triggers").
- [x] **Cold Start Test.** The protocol spec (§1-§7) is self-contained. A
  session that has never seen a beacon can write and process one from this ADR
  alone. The body schema, file naming, scan command, and archive procedure are
  all specified without requiring external context.
- [x] **Sources cited.** All load-bearing claims have references: PR numbers
  are cited for the unblock chains; `_archive/` file names are cited for the
  beacon examples; CLAUDE.md section reference is cited for the predecessor
  prose specification.

# Anti-Pattern Sweep — Batch 5

**Date:** 2026-04-28
**Auditor:** Subagent (automated overnight run)
**Reference:** `.claude/rules/universal-planning.md` §21 Anti-Patterns
**ADRs audited:** 0032, 0042, 0048

---

## ADR 0032 — Multi-Team Anchor (Slack-Style Workspace Switching)

**Decision:** Adopt single-process per-team `TeamContext` scoping as the default multi-team isolation model, with a compliance-tier process-per-team escape hatch reserved for future implementation.

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| AP-1 | Unvalidated assumptions | Partial | Minor | Annotate |
| AP-4 | No rollback strategy | Partial | Minor | Annotate |
| AP-11 | Zombie project / no kill criteria | Partial | Minor | Annotate |

**AP-1 detail:** The ADR assumes the `ResourceGovernor` cap of `MaxActiveRoundsPerTick = 2` will keep RAM/CPU bounded on 8GB laptops with 4–8 background teams. This is stated as design intent, not a validated benchmark. No protocol for what happens if real-world telemetry proves the cap insufficient. Recommend annotating with a revisit trigger tied to beta telemetry.

**AP-4 detail:** The compatibility plan (v1 → v2 migration) is described as "non-destructive" with a `legacy-backup/` folder retained "for one minor version cycle." No explicit rollback path is defined if the migration step fails mid-flight (e.g., a corrupted `legacy-backup/` on a write-limited device). The ADR defers this to implementation but does not flag it as a required deliverable. Recommend annotating with a note that the migration must include an abort-and-revert path as an implementation precondition.

**AP-11 detail:** The Option-B compliance-tier escape hatch is explicitly noted as "not part of v2 MVP" with no kill trigger: *"This escape hatch is not part of v2 MVP but the APIs are designed so Option B can layer in later."* There is no criterion for when this deferred promise is either fulfilled or formally abandoned. Recommend adding a revisit trigger (e.g., "if no compliance-tier user requests Option B before v3, close the promise with a superseding note").

**Overall grade: annotation-only**

---

## ADR 0042 — Subagent-Driven Development for High-Velocity Sessions

**Decision:** Establish parallel background subagent dispatch (each on its own worktree, each completing as a single PR with auto-merge) as the default execution model for parallelizable task shapes, with a formal contract governing when it is and is not appropriate.

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| AP-7 | Delegation without contracts | Partial | Minor | Annotate |
| AP-8 | Blind delegation trust | Partial | Minor | Annotate |
| AP-13 | Confidence without evidence | Partial | Minor | Annotate |

**AP-7 detail:** The ADR defines a six-condition contract for subagent dispatch, which is strong. However, the contract does not specify what the controller must do when a subagent exceeds its self-cap or produces a PR that is within CI-green but substantively wrong (e.g., plausible-looking but convention-violating output). The brief template section lists elements but does not mandate a checklist or handoff format the controller uses to verify output before enabling auto-merge. The contract is directionally correct but leaves "controller reviews PR" as an implied step, not an explicit gate.

**AP-8 detail:** *"Auto-merge + CI gate means subagent failures don't land. The risk surface is 'merged-but-wrong' not 'caused damage on the way.'"* The ADR acknowledges "merged-but-wrong" as a real risk in the same sentence that uses CI-green as a safety net. This is partially blind trust — CI cannot catch semantic incorrectness, convention violations, or blast-radius surprises that don't break tests. The ADR's own failure-mode table lists "Blast-radius surprise" with the fix being "add diff-shape clause to every brief" — meaning the safety depends entirely on brief discipline, which the ADR concedes is a "load-bearing skill." Recommend annotating that the safety model requires a post-merge sampling review habit, not just CI-green.

**AP-13 detail:** *"~30 PRs landed in the 2026-04-26 session vs. an estimated 3-5 with sequential development — a ~6-10× throughput improvement."* The 3-5 sequential baseline is an assertion, not a measured datum from an actual sequential session of comparable scope. The throughput multiplier range (6-10×) is derived from this unvalidated baseline. This does not weaken the ADR's core decision, but the numbers are asserted as evidence for the ROI case. Recommend a brief annotation acknowledging the baseline is a working estimate, not a controlled experiment.

**Overall grade: annotation-only**

---

## ADR 0048 — Anchor Multi-Backend MAUI (Win/Mac/iOS/Android native; Linux/WASM via Avalonia)

**Decision:** Adopt a multi-backend MAUI strategy for Phase 2: native MAUI on Win/Mac/iOS/Android; MAUI Avalonia backend on Linux; WebAssembly exploratory for Phase 3+. Phase 1 unchanged (Win64 only).

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| AP-1 | Unvalidated assumptions | Partial | Minor | Annotate |
| AP-21 | Assumed facts without sources | Partial | Minor | Annotate |

**AP-1 detail:** The ADR assumes Razor/BlazorWebView renders correctly through the MAUI Avalonia Preview 1 backend and that the cross-OS gossip round (AnchorSyncHostedService + AnchorCrdtDeltaBridge) works on Linux. Both are explicitly deferred to a spike: *"The spike validates MAUI Avalonia Preview 1 on Ubuntu 22.04 LTS, smoke-tests the AnchorSyncHostedService and AnchorCrdtDeltaBridge across a Win64↔Linux gossip round."* The ADR correctly gates the decision on spike results, so this is partial rather than a full hit. The annotation needed: the spike's exit criterion ("ship/wait recommendation") should be documented as a blocking gate before Phase 2 Linux work begins.

**AP-21 detail:** Two facts are cited without direct source links: (1) *"MAUI Avalonia Preview 1 released 2026-03-16"* and (2) the partnership announcement date *"2025-11-11."* The References section cites `<https://avaloniaui.net/Blog>` for both — a blog index, not a permalink to either article. If the blog archive reorganizes, both citations become unverifiable. The Avalonia partnership claim is a load-bearing decision driver; a permalink or a snapshot citation would make this more durable.

**Overall grade: annotation-only**

---

## Summary

| ADR | Title (short) | Grade |
|---|---|---|
| 0032 | Multi-Team Anchor Workspace Switching | annotation-only |
| 0042 | Subagent-Driven Development | annotation-only |
| 0048 | Anchor Multi-Backend MAUI | annotation-only |

No ADR in this batch requires structural amendment. All three carry minor annotation-level findings: unvalidated assumptions, one partially blind delegation trust acknowledgment, and one citation durability gap. Recommended action: add brief inline annotations (<!-- audit note --> or a dedicated "Audit notes" section) to each ADR addressing the flagged items. No ADR text deletions or decision reversals warranted.

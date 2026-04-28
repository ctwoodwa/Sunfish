# Active Workstreams Ledger

Canonical "what's in flight, who owns it, what state it's in" for cross-session coordination between the **research session** (ADRs, intakes, design decisions), **sunfish-PM session** (production code, PRs), and **book-writing session** (the-inverted-stack manuscript).

**All sessions read this at session start. Update on state change. Do not implement anything not listed as `ready-to-build`.**

---

## Status vocabulary

| Status | Meaning |
|---|---|
| `design-in-flight` | Research session is still working on the spec. **sunfish-PM: do not implement.** |
| `ready-to-build` | Spec is final; sunfish-PM may implement. A hand-off file in `handoffs/` describes the work. |
| `building` | sunfish-PM is implementing. Other sessions: do not open parallel PRs on the same scope. |
| `built` | Implementation complete (committed/merged). Watch for follow-up retrofits. |
| `held` | Paused pending external decision (user, third-party, or another workstream). |
| `blocked` | Depends on a workstream not yet resolved (link the dependency). |
| `superseded` | Replaced by another workstream (link the replacement). |

---

## Current state (last updated 2026-04-28)

| # | Workstream | Status | Owner (current phase) | Reference | Notes |
|---|---|---|---|---|---|
| 1 | Multi-tenancy type surface convention | `design-in-flight` | research | `icm/00_intake/output/tenant-id-sentinel-pattern-intake-2026-04-28.md` | Stage 00 widened intake; Stage 01 Discovery next. **Blocks Tier 2 retrofit (AuditQuery → TenantSelection) on the now-merged kernel-audit package.** |
| 2 | Kernel-audit Tier 1 retrofit | `built` (merged) | sunfish-PM | https://github.com/ctwoodwa/Sunfish/pull/198 (merged 2026-04-28 16:30Z) | AttestingSignature pair shape + IAuditTrail XML doc fix shipped. Tier 2 (`AuditQuery.TenantId → TenantSelection`) remains blocked on workstream #1's M2. |
| 3 | Kernel-audit scaffold (PR #190) | `built` (merged) | sunfish-PM | https://github.com/ctwoodwa/Sunfish/pull/190 (merged 2026-04-28 08:43Z) | Tier 1 drift now resolved by workstream #2 (PR #198). Only Tier 2 (`AuditQuery → TenantSelection`) remains, blocked on workstream #1's M2. |
| 4 | Kernel-event-bus test-flake fix (PR #191) | `built` (merged) | research session (exception turn) | https://github.com/ctwoodwa/Sunfish/pull/191 (merged 2026-04-28 08:26Z) | InMemoryEventBus + InMemoryEventBusTests deterministic readiness wait. Closes bug-268 in `.wolf/buglog.json`. |
| 5 | Phase 2 commercial MVP | `design-in-flight` | research | `icm/00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md` | Stage 00 intake. ADR 0049 accepted (built); ADR 0051 (Payments), 0052 (Outbound msg) not yet drafted. |
| 6 | Foundation-audit / kernel-audit relationship | `held` (Stage 00 stub) | research | `icm/00_intake/output/foundation-audit-vs-kernel-audit-relationship-intake-2026-04-28.md` | Held until a revisit trigger fires (see intake's "Revisit triggers" section). |
| 7 | Phase 1 G7 conformance scan | `blocked` | research (when G6 closes) | `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_business_mvp_phase_1_progress.md` | Blocked on G6 trustee orchestration completion. |
| 8 | Phase 1 G6 trustee orchestration | `building` (sunfish-PM) | sunfish-PM | PRs #178, #185 (merged); follow-up unspecified | Per Phase 1 progress memory; verify state via `gh pr list`. |
| 9 | ADR template self-audit checklist (Phase 1 of audit work) | `built` (PR #193, auto-merging) | research session (exception turn) | PR #193 — branch `chore/adr-template-self-audit` | Adds `docs/adrs/_template.md` with 5-min pre-acceptance self-audit. Forward-looking; lands when CI greens. |
| 10 | ADR Tier 2 anti-pattern sweep (15 ADRs) | `built` | research session (5 sonnet subagents) | `icm/07_review/output/adr-audits/anti-pattern-sweep-batch-{1..5}.md` | All 5 batches complete. 3 needs-amendment (0009, 0017, 0029); 12 annotation-only. Consolidated findings in `CONSOLIDATED-HUMAN-REVIEW.md`. |
| 11 | ADR Tier 1 full UPF audit (8 ADRs) | `built` | research session (8 opus subagents) | `icm/07_review/output/adr-audits/<NNNN>-upf-audit.md` (8 files) | All 8 audits complete. 1 A- (0043), 6 B/B-, 0 critical-fail. **4 critical-severity findings** identified (0008, 0013, 0028, 0046) — see `CONSOLIDATED-HUMAN-REVIEW.md` §1. |
| 12 | ADR audit consolidation + commit | `built` (PR #194, auto-merging) | research session | PR #194 — `chore/adr-audit-overnight-results` | Audit findings + ledger update bundled. Lands when CI greens. |
| 13 | Platform features brainstorm (layered startup / visibility modes / support delegation / sensitivity classification) | `brainstorm` | research session | `icm/00_intake/output/platform-features-brainstorm-2026-04-28.md` | 4-idea cluster captured. Held at Stage 00 until a forcing function promotes one to Stage 01 Discovery. Sequencing: 4 (sensitivity classification) is the foundation; 1 (layered startup) independent; 2+3 build on 4. |
| 14 | ADR 0013 provider-neutrality enforcement gate (Roslyn analyzer + BannedSymbols) | `built` (merged) | sunfish-PM | https://github.com/ctwoodwa/Sunfish/pull/196 (merged 2026-04-28 14:35Z) | Resolves audit finding C-1. Phase-2 `providers-*` scaffolds can now proceed with mechanical vendor-isolation gate active. SUNFISH_PROVNEUT_001 + RS0030 (BannedApiAnalyzers) auto-attached. |
| 15 | Foundation.Recovery package split (ADR 0046 + 0049 reconciliation) | `ready-to-build` | sunfish-PM | `icm/_state/handoffs/adr-0046-recovery-package-split.md` | UPF-graded A. User approved option C "split by concern" 2026-04-28 with quality-over-cost framing. ~2-3 days sunfish-PM + ~1 hr research review of Phase 1 inventory. api-change pipeline. Resolves audit finding C-2. |

---

## How to use this ledger

### research session (this session)

- Updates rows when designs progress, freeze, or unfreeze.
- Authoritative author of `design-in-flight` ↔ `ready-to-build` transitions.
- Writes the hand-off file in `handoffs/` when transitioning a row to `ready-to-build`.
- Updates the "Last updated" timestamp + signs off ("research session — YYYY-MM-DD").

### sunfish-PM session

- **Before any code change beyond a one-line fix:** verify the relevant row says `ready-to-build` AND a hand-off file exists in `handoffs/`.
- During implementation: update row to `building` (with PR link if available).
- After completion: update row to `built` with the merged PR / commit reference.
- If unsure whether work is wanted: write a memory note asking the research session, do not proceed.

### book-writing session

- When a book chapter cross-references a Sunfish ADR or intake, check this ledger for that workstream's status.
- If `design-in-flight`: mark the cross-reference as draft / pending in the manuscript.
- If `built`: cross-reference is stable.

### Maintenance

- Stale `built` rows can be pruned in a periodic housekeeping pass (>7 days old, no follow-ups).
- Rows superseded by a different workstream link the replacement; keep the row at `superseded` for one cycle so anyone mid-work sees the redirect.

---

## Last updated

**2026-04-28** — research session: ledger created (coordination protocol bootstrap)
**2026-04-28 (later)** — research session: workstreams #3 (PR #190) and #4 (PR #191) flipped to `built` (merged); workstream #2 hand-off branch strategy updated to "follow-up PR from main" since PR #190 merged without the retrofit.
**2026-04-28 (later still)** — sunfish-PM session: workstream #14 flipped to `built` — PR #196 merged 14:35Z. ADR 0013 audit finding C-1 in `CONSOLIDATED-HUMAN-REVIEW.md` § 1 is resolved.
**2026-04-28 (later still still)** — sunfish-PM session: workstream #2 flipped to `built` — PR #198 merged 16:30Z. Workstream #3 row updated to note Tier 1 drift resolved; only Tier 2 remains blocked on workstream #1's M2.

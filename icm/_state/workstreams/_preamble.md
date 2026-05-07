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

## Current state (last updated 2026-05-06 — W#59 Crew Comms Anchor MVP hand-off authored; CO directive top priority this week)

| # | Workstream | Status | Owner (current phase) | Reference | Notes |
|---|---|---|---|---|---|

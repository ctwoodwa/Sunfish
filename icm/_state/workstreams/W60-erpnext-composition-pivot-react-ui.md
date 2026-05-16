---
sort_order: 69
number: 60
slug: erpnext-composition-pivot-react-ui
title: "**ERPNext Composition Pivot — React UI + local-first sync layer** (5-phase; `sunfish-feature-change` pipeline) — CO UPF plan approved 2026-05-11; ERPNext (GPLv3, self-hosted) as property/accounting engine; Sunfish as local-first sync + offline + React UI + tenant comms layer over it"
status: "building"
status_cell: "`building` — **P2 BUILT 2026-05-13** (PRs #731+#732+#751+#752+#757+#758); **P3 IMPLEMENTATION COMPLETE 2026-05-14** (PR #812 Tauri shell + PR #836 SQLite cache + PR #839 Loro CRDT write queue; ADR 0086 Accepted PR #784 2026-05-13); **P3 gate remaining: CO Surface Pro PASS** — dev-win producing ARM artifact for CO validation; **P4 owner: dev (dev-win)** — hand-off `w60-collaboration-phase4-stage06-handoff.md`; gated on CO Surface Pro PASS signal; **P5 PR 1 (`@sunfish/contracts`) INDEPENDENT + immediately buildable** — hand-off `w60-reporting-contracts-phase5-stage06-handoff.md`"
owner: "dev"
owner_cell: "dev (dev-win; P4 Tauri ARM + Stronghold + Collaboration)"
reference_cell: "P2: `icm/_state/handoffs/w60-erpnext-react-ui-phase2-stage06-handoff.md`; P3: `icm/_state/handoffs/w60-tauri-offline-phase3-stage06-handoff.md`; P4: `icm/_state/handoffs/w60-collaboration-phase4-stage06-handoff.md`; P5: `icm/_state/handoffs/w60-reporting-contracts-phase5-stage06-handoff.md`; UPF: `~/.claude/plans/noble-crunching-hopper.md`"
---

## Notes

**CO UPF plan approved 2026-05-11.** Architecture: ERPNext (GPLv3, self-hosted Docker) as primary accounting + property engine; Sunfish as local-first sync + offline + React UI + tenant comms layer over it.

**Phase 1 (CO action, ~1 week):** ERPNext self-hosted via docker-compose; property management module configured (6 properties + leases); core accounting loop validated; accountant remote access tested. PASS = CO can log in on Surface Pro, create a lease, collect rent, see it in ledger. **No COB work until Phase 1 PASS is signalled.**

**Phase 2 (COB, ~6 dev-weeks, GATED on Phase 1 PASS):** React 19 + TypeScript + Vite + Tailwind + shadcn/ui skeleton; ASP.NET Core proxy layer between React and ERPNext API; 6 screens (properties, leases, rent, accounting, crew-comms, maintenance); `@sunfish/ui-react` npm package; role routing (CO/accountant/CPA/tenant). Hand-off to be authored by XO once Phase 1 PASS confirmed.

**Phase 3 (~3 dev-weeks, GATED on ADR 0086 Accepted):** Tauri v2 on Surface Pro; offline SQLite cache; write queue → ERPNext sync on reconnect; Loro CRDT for AP-class data. Hand-off authored 2026-05-13. Council review of ADR 0086 dispatched 2026-05-13. PR #737 must merge (status: Accepted) before COB starts.

**Phase 4 (~4 dev-weeks):** Accountant peer node (Headscale Tier 2, read/write, bidirectional sync); CPA read-only Bridge account; tenant magic-link portal; bank CSV import.

**Phase 5 (ongoing):** `docker-compose` self-hosting guide; F/OSS polish; book alignment.

**Fate of existing .NET work:**
- `blocks-crew-comms` (2,291 LOC) — KEPT, primary differentiator (ERPNext has no chat)
- `kernel-sync`, `kernel-crdt`, `kernel-lease` — KEPT, central to local-first value
- `blocks-accounting` (979 LOC) → becomes ERPNext API adapter
- `blocks-leases` (1,107 LOC) → becomes ERPNext API adapter
- `blocks-forms` (32 LOC stub) → may be superseded by ERPNext native forms

**Collaborator model:**

| Collaborator | Access | Sync |
|---|---|---|
| Accountant | Read/write blocks-accounting (reconciliation) | Peer node — Headscale Tier 2, bidirectional |
| CPA | Read-only accounting + tax reporting | Bridge read-only account or snapshot export |
| Tenants | Read own lease + messages | Magic-link portal on Bridge; no install |

**FAILED triggers:** React velocity slower than Blazor; OpenAPI→TypeScript client maintenance > hand-authored types; Tauri fails on ARM Surface Pro (fallback: browser PWA); Phase 2 exceeds 4 calendar months.

**Numbering note:** W#46–W#59 registered in session memory 2026-05-06–2026-05-11 are in `icm/_state/workstreams/` on `main` but were absent from the working tree at the time this file was authored. W#60 skips ahead to avoid collision with those source files.

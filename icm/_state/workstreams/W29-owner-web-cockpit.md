---
sort_order: 28
number: 29
slug: owner-web-cockpit
title: "Owner Web Cockpit (cluster module)"
status: "ready-to-build"
status_cell: "`ready-to-build` — hand-off authored 2026-05-15; Phase 1 covers Properties + Equipment + Inspections + Leases + Work Orders + Vendors + Dashboard (all built modules); Receipts/Messaging deferred to Phase 2; multi-actor permissions matrix resolved (OQ1)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/00_intake/output/property-owner-cockpit-intake-2026-04-28.md` + `icm/_state/handoffs/property-owner-cockpit-stage06-handoff.md`"
---

## Notes

Anchor + Bridge cockpit views consuming all cluster modules. Multi-actor permissions matrix resolves cluster OQ1.

**Phase 1 (2026-05-15 hand-off):** Cockpit shell + Properties + Equipment + Inspections + Leases + Work Orders + Vendors + Dashboard. 6 PRs; distributed view pattern (no new blocks-* package; views in accelerators). OQ-OC1 through OQ-OC4 resolved in hand-off. **Receipts + Messaging deferred** pending W#26 (ADR 0055) + W#20 completion.

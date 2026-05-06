---
sort_order: 33
number: 37
slug: tenant-security-policy-atlas-surface-promoted-from-w-34-foll
title: "**Tenant Security Policy + Atlas surface** (`sunfish-feature-change` pipeline) — promoted from W#34 follow-on"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0068 **Proposed** 2026-05-05 via PR #584 — CO acceptance flip pending; Stage 06 hand-off authored 2026-05-05; sunfish-PM may begin Phase 1 when W#46 Phase 1 clears gate H1 AND ADR 0068 reaches Status: Accepted)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/tenant-security-policy-stage06-handoff.md` + `docs/adrs/0068-tenant-security-policy.md` (PR #584 merged)"
---

## Notes

XO should start any legal-not-required scope-cut authoring from the canonical
pre-legal research prompt at `_shared/engineering/pre-legal-research-prompt.md`.
Pedantic-Lawyer perspective applies during §A0 self-audit + cohort council;
general-counsel engagement is the final-mile gate before Status: Accepted.

**Hand-off ready 2026-05-05.** ADR 0068 **Proposed** (CO acceptance flip pending; verify `status:` field before building); extended pre-merge council complete
(adversarial + security-engineering + WCAG/a11y + Pedantic Lawyer). New package:
`foundation-security-policy` (5 sub-domains: MFA enrollment / device attestation / audit
retention / key rotation / recovery contacts). §GC.1 general-counsel note travels with
every PR. **Phase 1 gate: W#46 Phase 1** (foundation-ship-common + ShipRole on origin/main)
— ALL Phase 1 types reference ShipRole. Phase 2 gate: ADR 0066-A1 + W#53 Phase 1a
(IAtlasProvider<T> in ui-core). ~14–20h sunfish-PM / 2 phases / ~4–5 PRs. Pre-merge council
canonical per ADR 0069 D1. Security-engineering subagent mandatory for Phase 1. WCAG/a11y
subagent mandatory for Phase 2.

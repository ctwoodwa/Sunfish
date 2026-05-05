---
sort_order: 42
number: 39
slug: foundation-missionspace-regulatory-substrate-phase-1
title: "Foundation.MissionSpace.Regulatory substrate Phase 1 (ADR 0064 + A1 contract surface)"
status: "built"
status_cell: "`built` (5/5 phases shipped 2026-05-01: P1 #467 scaffold + 16 types + 10 audit constants; P2 #469 composite probe + rule engine + residency enforcer; P3 #470 sanctions + AdvisoryOnly + Bridge HTTP 451 middleware; P4 #471 audit emission + dedup wiring W#32 both-or-neither; P5 DI extension + apps/docs page + ledger flip)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/foundation-mission-space-regulatory-stage06-handoff.md` + `docs/adrs/0064-runtime-regulatory-policy-evaluation.md` (post-A1; landed via PR #415 + post-A1 council fixes)"
---

## Notes

**Built 2026-05-01 — 99 / 99 tests green; halt-conditions clean; reader-caution propagated to README + apps/docs + DI XML doc.** Phase 3 rule-content authoring HALTS on legal-counsel engagement letter for InScope regimes. ADR 0064 + A1 Accepted on origin/main; council batting average 18-of-18; A1 absorbed all 4 Required council recommendations (GDPR Article 25 anchor; legal-advice disclaimer; sanctions opt-out path; Bridge data-residency middleware code-path). Substrate-only Phase 1 ships `Sunfish.Foundation.MissionSpace.Regulatory` package + ~16 types (`JurisdictionProbe` + `IPolicyEvaluator` + `IDataResidencyEnforcer` + `ISanctionsScreener` + 6 enums + 8 records) + composite-confidence probe with tie-breaker (user-declaration > tenant-config > IP-geo per A1.15) + Bridge `DataResidencyEnforcerMiddleware` with HTTP 451 RFC 7725 (per A1.4) + `ScreeningPolicy.AdvisoryOnly` opt-out (per A1.3) + 10 new `AuditEventType` constants (per A1.7 + A1.3 + A1.12.5) + canonical JSON Schema document (per A1.14) + DI extension + apps/docs page WITH reader-caution disclaimer (per A1.2 affirmative legal-advice framing). **Reader caution carried forward:** Sunfish does not provide legal advice; substrate not a substitute for counsel; Phase 1 substrate-only deployments NOT regulatory-compliant by virtue of substrate alone. Phase 3 rule-content authoring HALT-CONDITIONS on legal-counsel engagement letter for InScope regimes. **8 halt-conditions named** (reader-caution propagation; MinimumSpec cross-package availability; empty rule-content silent-pass behavior is intentional; Bridge middleware namespace; HTTP 451 Retry-After semantic; EuAiActTier placeholder Phase 1 usage; force-enable composition with ADR 0062; legal-counsel engagement letter for Phase 3). Substrate-only; consumer wiring (ADR 0057 + 0060 Phase 4 cross-cutting refactor; W#22 Phase 6 compliance half) is separate workstreams. Phase 2 (Sunfish.Regulatory.Jurisdictions taxonomy charter) + Phase 3 (per-regime rule content) + Phase 4 (cross-cutting refactor) + Phase 5 (commercial productization) are separate work products gated on Phase 1 + legal-counsel engagement.

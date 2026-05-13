---
sort_order: 49
number: 46
slug: shared-design-system-load-bearing-w-35-ship-architecture-fol
title: "**Shared Design System** (ADR 0077; `sunfish-feature-change` pipeline) — load-bearing W#35 Ship Architecture follow-on; sequences first per W#35 §9.2"
status: "built"
status_cell: "`built` (Phase 1 PR #622 ✓; Phase 2a PR #639 ✓; Phase 3 PR #645 ✓; Phase 1b PR #680 ✓; Phase 2b PR #745 ✓; Phase 4 PR #747 ✓; Phase 5 PR #749 ✓; Phase 6 — duplicate DefaultConformanceRegistry fixed PR #750 ✓; ledger flipped 2026-05-13)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/shared-design-system-stage06-handoff.md` + `icm/_state/handoffs/shared-design-system-permres-cache-invalidation-addendum.md` (Phase 1b follow-up: halt-C subscribe-before-load) + `docs/adrs/0077-shared-design-system.md` (PR #543 merged) + `packages/foundation-ship-common/` (P1 merged)"
---

## Notes

**XO priority recommendation 2026-05-06:** highest-leverage next pickup for COB.
Phase 2 onwards unblocks the W#35 Ship Architecture cascade — W#49/W#50/W#51/W#52/W#54/W#55
all halt-gated on this workstream's design-token + UICore-primitives surface landing on
origin/main. ADR 0077 Status flip shipped via PR #608 (Proposed → Accepted) clears the
pre-build checklist blocker. Parallel-safe with W#53 (Helm + Identity Atlas Phase 2) since
substrate concerns don't overlap (Ship Architecture vs Wayfinder Helm/Atlas).

**Phase 1 merged 2026-05-06 via PR #622.** New `Sunfish.Foundation.Ship.Common` package
shipped with the closed `ShipRole` taxonomy (11 values) + `IPermissionResolver` +
`DefaultPermissionResolver` + 9 `ShipAction` static-readonly fields + `PermissionDecision`
(Granted/Denied) + 8-value `DenialReason` + 6-value `RemediationKind` + per-tenant 60s TTL
cache (with cache-stampede protection) + `(ActorId, ShipLocation)` 1-min sliding-window
denial rate limit (default N=10) + 2 new `AuditEventType` constants (`PermissionDenied`,
`PermissionDenialRateExceeded`). Pre-merge council 1 Critical (cross-tenant authority bleed
fixed via tenantId on `ResolveAsync`) + 5 Major (cache stampede; 6 `CallToActionLabel`
contract violations; MissionEnvelope gate pass-through) + 2 Minor (capability action
fallthrough; sentence-cased `ReasonDisplay`). 25/25 tests pass. **Cohort batting average
updated: 28-of-32 substrate amendments needed council fixes.**

**Phase 1 Phase-2 follow-up TODOs in code** (none gating P2):
- `IOnWatchProbe` for live OOD/EOOW lookup (step 1)
- Promotion-target wiring through `ResolveAsync` parameters (step 0b — `CheckPromotionGuard`
  static helper available for caller pipelines today)
- `ITenantSecurityPolicy` for step 7 (W#37 / ADR 0068)
- Subscribe-before-load cache invalidation (**halt-condition C — CLEARED 2026-05-06**;
  `IStandingOrderEventStream` shipped W#57 PR #662; spec at
  `shared-design-system-permres-cache-invalidation-addendum.md`; ship as Phase 1b
  follow-up PR BEFORE Phase 4)
- Audit-loud `Granted` emission for `AuditLoudActions`
- `TenantId.System` swap for current `TenantId.Default` fallback (ADR 0084)

**Phase 1 unblocks:** W#50 (Engine Room Phase 1), W#55 (Ship's Office Phase 1) FULLY;
W#54 (Sick Bay Phase 1 H1 cleared; H2 still gated on W#53 P1; H3 on ADR 0068 Accepted).
W#51 (Quarterdeck) + W#52 (Tactical) gate on W#46 Phase 3 (`ILiveAnnouncer` + `IFocusTrap`).

**Phase 2a merged 2026-05-06 via PR #639.** `foundation-design-tokens` package scaffold +
baseline `tokens.json` W3C Design Tokens catalog (10 namespace groups: SurfaceColors /
TextColors / StateColors / RoleBandColors / Typography / Space / Radius / Elevation / Motion /
TargetSize). Codegen pipeline (Phase 2b) still pending.

**Phase 3 merged 2026-05-06 via PR #645.** `ILiveAnnouncer` + `IFocusTrap` + `LiveRegionPoliteness`
+ `IFormControlContract` + `IFirstAidContract` + `IAccessibilityAuditor` etc. — 18 types across 3
namespace groups (`UICore.Primitives` / `UICore.FirstAid` / `UICore.Conformance`). **Clears W#51
Phase 3a + W#52 Phase 3a gates** (confirmed 2026-05-06).

**Remaining phases:**

- **Phase 2b** (~3h): codegen pipeline — `tokens.json` → C# const records + CSS custom properties
  + Markdown reference table + WCAG 1.4.3/1.4.11 contrast CI gate + CVD ΔE2000 audit. Fully
  specified in hand-off. Design-engineering subagent council mandatory.
- **Phase 4** (~8h): adapter implementations (Blazor + React + MAUI Win/Mac concrete
  primitives) + a11y harness extension + CI gates. WCAG/a11y subagent mandatory.
- **Phase 5** (~3h): `apps/docs` + `AddSunfishSharedDesignSystem()` meta-extension + cross-link.
- **Phase 6** (~30min): ledger flip + close W#35 follow-on row.

**Pre-merge council canonical per ADR 0069 D1 for every phase.** Critical dependency:
`IStandingOrderEventStream` not yet built (ADR 0065-A1 spec-only); Phase 1 ships with TTL
cache (halt-condition C in hand-off).

**Hard prerequisite for ALL downstream W#35 cohort ADRs** (Quarterdeck / Engine Room /
Tactical / Sick Bay / Ship's Office / OOD-Watch ✓ already built).

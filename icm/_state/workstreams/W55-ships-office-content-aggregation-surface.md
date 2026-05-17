---
sort_order: 58
number: 55
slug: ships-office-content-aggregation-surface
title: "**Ship's Office Content Aggregation Surface + Scribe Role** (ADR 0083; W#35 Ship Architecture follow-on #7 — FINAL cohort ADR; `sunfish-feature-change` pipeline)"
status: "built"
status_cell: "`built` — all 6 phases shipped 2026-05-13/16 (PRs #624+#711+#753+#756+#759+#762+#787+#828 + Phase 5 PR #945); 5 Razor components + DynamicTemplate kind + 62 tests; ADR 0055 Accepted (PR #916); IFormSchemaStore local stub per xo-ruling-T02-43Z; pipeline closed (W#35 cohort COMPLETE)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/ships-office-stage06-handoff.md` + `docs/adrs/0083-ships-office-content-aggregation.md` (PR #591 merged) + `packages/foundation-ships-office/` (P1 merged) + `icm/00_intake/output/2026-05-01_ships-office-content-aggregation-intake.md`"
---

## Notes

**Phase 1 merged 2026-05-06 via PR #624.** New `Sunfish.Foundation.ShipsOffice`
package shipped: 8 data-model types + 4 contract interfaces
(`IShipsOfficeDataProvider`, `IShipsOfficeCommandService`,
`IContentEditorSurface`); `PublishOutcome` enum (added per Major SI-1:
explicit success/rejection enum prevents the silent-rejection foot-gun on
`PublishAsync`); 6 new `AuditEventType` constants + 4 new `ShipAction`
constants (with `ActionMinimumDeck` + `MapToCapabilityAction` extended in
`DefaultPermissionResolver`); `AddSunfishShipsOffice()` DI extension +
`ShipsOfficeOptions` (FallbackPollingInterval=60s; SnapshotPageSize=500;
RequireSecondActorPublish=false).

**Pre-merge council** (standard 4-perspective adversarial; Opus + xhigh)
returned MECHANICAL-AMENDMENTS-ONLY: 2 Major (PublishAsync silent-rejection +
comment-only-contract gap) + 4 Minor (latency-posture + SubscribeChangesAsync
semantics + ContentEditorResult invariant + PageToken cross-tenant safety +
SnapshotPageSize DoS guidance). All applied pre-merge. **Cohort batting
average updated: 29-of-33** substrate amendments needed council fixes.

**B-1 architectural rule honored**: `foundation-ships-office` does NOT depend
on `Sunfish.UICore`. `IDocumentDiffService` lives in `blocks-ships-office`
(Phase 2). DateTimeOffset over NodaTime per cohort precedent.

**18/18 tests pass** in `foundation-ships-office`; **25/25 tests pass** in
`foundation-ship-common` (existing W#46 P1 suite green; `ActionMinimumDeck`
cardinality test updated to assert the 4 new entries; net cardinality = 13).

**Phase 1 Phase-2 follow-up TODOs** (none gating P2):
- `SUNFISH_SHIPSOFFICE_PERM001` Roslyn analyzer (Phase 2)
- `IShipsOfficeDataProvider` + `IShipsOfficeCommandService` reference impls
  in `blocks-ships-office` (Phase 2)
- `IDocumentDiffService` (declared in `blocks-ships-office` per B-1)
- `NoopContentEditorSurface` read-only stub (Phase 2; Open Q2)
- `ShipsOfficeSnapshot.WasTruncated` field for partial-snapshot-on-timeout
  posture (Phase 2)
- `ContentEditorResult.Saved()` / `Cancelled()` factory methods (Phase 2 —
  illegal-state guard per Minor SI-3)
- Role-minimum enforcement on the 4 new ShipActions (Scribe / XO+) — gated
  on W#37 / `ITenantSecurityPolicy`

**Remaining phases:**

- **Phase 2** (~6h): reference impl + permission gating + analyzer; pre-merge
  security-engineering subagent mandatory.
- **Phase 3** (~5h): `blocks-ships-office` Blazor UI; pre-merge WCAG/a11y
  subagent mandatory; halt H2 (`DiffPreviewView`) gates real diff impl
  (Phase 1 stub OK); halt H3 (`ISearchAsYouType`) gates Phase 3 search bar.
- **Phase 4** (~3h): React adapter parity per ADR 0014.
- **Phase 5** (~3h): CONDITIONAL on H4 (ADR 0055 Accepted); skip if not
  cleared.
- **Phase 6** (~30min): ledger flip + close cohort follow-on row.

**W#35 Ship Architecture cohort substrate complete on origin/main:**
W#46 P1 ✓ (foundation-ship-common) + W#49 P1+P2+P2-amend+P3 ✓
(foundation-wayfinder OOD watch) + W#55 P1 ✓ (foundation-ships-office;
this PR). Phases 2+ for each cohort workstream remain.

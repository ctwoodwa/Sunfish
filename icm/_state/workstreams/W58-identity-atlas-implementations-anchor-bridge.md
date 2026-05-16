---
sort_order: 61
number: 58
slug: identity-atlas-implementations-anchor-bridge
title: "**Identity Atlas Implementations** (ADR 0066 §Phase 3; W#53 Phase 3-deferred; `sunfish-feature-change` pipeline) — Anchor + Bridge `IIdentityAtlasSurface` concrete implementations + five WCAG-conformant identity pages"
status: "built"
status_cell: "`built` — all 4 phases shipped 2026-05-13 (PRs #763+#764+#767+#795+#799); 5 Anchor Blazor + 5 Bridge Blazor + 5 React TSX identity pages + IDiffPreview + WCAG docs; ledger close PR #799; pipeline closed"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/identity-atlas-implementations-stage06-handoff.md` + `docs/adrs/0066-helm-composition-and-identity-atlas-surface.md` §Phase 3 + `packages/ui-core/Wayfinder/Identity/` (contracts on origin/main)"
---

## Notes

**W#53 Phase 3-deferred.** ADR 0066 §Phase 3 specifies concrete
`IIdentityAtlasSurface` implementations for the Anchor and Bridge
accelerators. Explicitly NOT part of W#53 per the W#53 hand-off
("COB should NOT begin Phase 3 without a dedicated hand-off file").

**Contracts on origin/main (W#53 P1a):**
- `IIdentityAtlasSurface` — 5-method read-only surface (GetProfile / GetKeyRotation
  / GetRecoveryContacts / GetHistoricalKeys / GetActiveTeamOverview)
- `IdentityProfileEditViewModel` + `KeyRotationViewModel` + `RecoveryContactsViewModel`
  + `HistoricalKeysBrowseViewModel` + `ActiveTeamOverviewViewModel` + supporting records
  (all in `packages/ui-core/Wayfinder/Identity/ViewModels.cs`)

**Implementation is READ-ONLY.** `IIdentityAtlasSurface` implementations MUST be
projection-only — no mutations, no audit emission, no StandingOrder issuance. Writes
continue to flow through `IStandingOrderIssuer` + capability graph + recovery coordinator.

**Build target:** 4 phases / ~27h / ~6 PRs.
- **Phase 1** (~10h / 2 PRs): `AnchorIdentityAtlasSurface` + 5 Anchor Blazor pages
- **Phase 2** (~8h / 2 PRs): `BridgeIdentityAtlasSurface` + 5 Bridge Blazor pages
- **Phase 3** (~6h / 1 PR): Bridge React adapter parity (5 React components) + WCAG audit
- **Phase 4** (~3h / 1 PR): Diff-preview wiring + `apps/docs/wcag/identity-atlas.md` + ledger

**H1 gate:** W#53 Phase 2 complete (React adapter PR 2d must land on origin/main before
Phase 1 build begins — the Identity Atlas is navigated from the Helm; Helm Phase 2
must ship all 6 canonical widgets + adapter parity).

**H4 (partial):** ADR 0046-A1 (`HistoricalKeysProjection`) is Proposed. Phase 1 ships
`HistoricalKeysBrowseViewModel` with `Keys = []` + `KeyCount` placeholder (count from
existing `foundation-recovery` surface); full key-history list ships in a Phase 1b
follow-up when ADR 0046-A1 Accepted.

**Pre-merge council canonical for every UI-bearing phase.** WCAG/a11y subagent mandatory
for Phases 1/2/3. Security-engineering subagent for Phase 2 (Bridge multi-tenant boundary).

---
sort_order: 56
number: 47
slug: w-42-follow-on-anchor-maui-concrete-per-adapter-ui-surface-f
title: "**W#42 follow-on — Anchor MAUI `ISystemRequirementsRenderer`** (`sunfish-feature-change` pipeline) — concrete per-adapter UI surface for the W#42 Wayfinder substrate; mounts ADR 0063-A1.1 `SystemRequirementsResult` onto Anchor's MAUI Blazor Hybrid shell"
status: "built"
status_cell: "`built` — Phase 1 PR #765 merged; Phase 2 PR #768 merged; Phase 3 PR #769 merged; **Phase 4 PR #770 auto-merge enabled 2026-05-13** (AddAnchorSystemRequirementsRenderer DI ext + 9 a11y harness tests + wcag.md per-adapter conformance section; WCAG council PASS-WITH-AMENDMENTS D1/D2/C1/C2 applied + 4-perspective PASS-WITH-AMENDMENTS C1/C2 applied); Phase 5 ledger close done"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/foundation-wayfinder-anchor-maui-renderer-stage06-handoff.md` + `docs/adrs/0063-mission-space-requirements.md` (substrate spec) + `docs/adrs/0065-wayfinder-system-and-standing-order-contract.md` §Decision §7 (WCAG mandate) + `docs/adrs/0048-anchor-multi-backend-maui.md` (multi-backend) + `docs/adrs/0032-multi-team-anchor-workspace-switching.md` (active-team scoping)"
---

## Notes

**Hand-off ready 2026-05-04.** First per-adapter renderer hand-off in the W#42 follow-on chain (Anchor MAUI; Bridge React + iOS SwiftUI + Android-native still queued as future hand-offs). Implementation: 13–18h sunfish-PM / 5 PRs / 5 phases. **Phase 1:** `PreInstallFullPage` Razor page + `SystemRequirementsDimensionRow` component + 26 localization keys + 6 unit tests (~4–5h). **Phase 2:** `PostInstallInlineExplanation` mode + `AnchorMauiSystemRequirementsRenderer` + `AnchorMauiSystemRequirementsSurface` + 4 unit tests (~2–3h). **Phase 3:** `PostInstallRegressionBanner` + `SystemRequirementsRegressionObserver` (`IMissionEnvelopeObserver`) + `aria-live="assertive"` + 5 unit tests (~2h). **Phase 4:** `AddAnchorSystemRequirementsRenderer` DI extension + per-platform native-a11y (UIA / NSAccessibility on Win + MacCatalyst Phase-1 RIDs; iOS/Android deferred per ADR 0048-A1) + 3 a11y harness tests via `Sunfish.UIAdapters.Blazor.A11y` + WCAG 2.2 AA + EN 301 549 v3.2.1 baseline append in `apps/docs/foundation/wayfinder/wcag.md` (~3–4h). **Phase 5:** ledger flip + memory + close (~30min). **7 halt-conditions named** including (a) substrate prereq verification on origin/main, (b) ledger-row sanity check, (c) WCAG/a11y subagent pre-merge canonical per ADR 0065 §7 mandate (non-negotiable for UI-bearing phases), (d) MAUI-version compatibility, (e) Win+MacCatalyst-only Phase-4 scope, (f) audit-double-emission discipline (renderer MUST NOT emit audit; resolver does), (g) `IMinimumSpecResolver` scoping must respect `IActiveTeamAccessor`. Pre-merge council canonical (4-perspective + WCAG/a11y subagent before EVERY UI-bearing phase; cohort batting average 22-of-22).

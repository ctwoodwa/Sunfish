# Intake — Anchor Cross-OS Strategy (Phase 2 Prep)

**Date:** 2026-04-27
**Requestor:** Chris Wood
**Request:** Establish Anchor's path to Win/Mac/Linux/iOS/Android coverage now that Avalonia's MAUI backend is in preview, so Phase 1's Win64-only stance (ADR 0044) has a deliberate Phase 2 follow-on rather than an open-ended wait.

## Problem Statement

ADR 0044 ships Anchor on Win64 only for Phase 1, deferring Mac/Linux/iOS/Android until ".NET MAUI 10 stabilizes" — an unbounded wait on Microsoft's release cadence. The Sunfish business pitch (paper §1, ADR 0006/0031, *The Inverted Stack* Ch1) requires cross-OS for credibility: a local-first node that runs only on Windows is a contradiction in marketing terms even if the kernel is technically portable. Phase 2 can't ship to a real customer fleet without at least Win + Mac + Linux desktop coverage.

Two events since ADR 0044 change the analysis:

1. **November 11, 2025** — Avalonia + Microsoft announced a partnership: *"Avalonia is bringing .NET MAUI to Linux and WebAssembly, delivering on the community's most requested features since MAUI launched."*
2. **March 16, 2026** — **MAUI Avalonia Preview 1** released. Same MAUI codebase, swappable native backend per platform.

The MAUI 10 Mono runtime preview that blocks Mac/iOS/Android (per ADR 0044) and the MAUI Avalonia backend that unblocks Linux are **independent fixes on independent timelines**. ADR 0044's "wait for MAUI 10 to stabilize" was correct as a one-fix model; with the partnership, Linux is now decoupled from the Mono fix and can ship sooner via the Avalonia backend.

This intake scopes the work to: (a) confirm the multi-backend MAUI strategy as Sunfish's Phase 2 cross-OS path, (b) timebox a spike to validate MAUI Avalonia Preview 1 on Linux, and (c) document the re-enablement triggers for Mac/iOS/Android via the existing scaffolded csproj conditions.

## Affected Areas

- `accelerators/anchor/` — primary code site; csproj `<TargetFrameworks>` extension + new backend NuGet reference
- `docs/adrs/0044-anchor-windows-only-phase-1.md` — extended (not superseded) by ADR 0048
- `docs/adrs/0048-anchor-multi-backend-maui.md` — new (this intake's deliverable, drafted alongside)
- `apps/local-node-host/` — already cross-OS; no change but worth documenting as the Linux/Mac fallback path during the preview window
- `_shared/product/business-mvp-roadmap.md` (or equivalent) — Phase 2 milestones may reference this intake

## Selected Pipeline Variant

- [ ] sunfish-feature-change
- [ ] sunfish-api-change
- [ ] sunfish-scaffolding
- [ ] sunfish-docs-change
- [ ] sunfish-quality-control
- [ ] sunfish-test-expansion
- [ ] sunfish-gap-analysis

**Selected: hybrid `sunfish-docs-change` (for the ADR + intake deliverables) followed by `sunfish-feature-change` (for the post-Phase-1 spike).** The strategy decision lands as docs first; the implementation spike follows once Phase 1 closes (G7 conformance baseline scan complete).

## Dependencies and Constraints

### Hard dependencies

- **Phase 1 must close first.** G5 (backup orchestration), G6 (recovery flow), G7 (conformance baseline scan) come before any cross-OS work begins. Phase 1 is a wire-protocol foundation deliverable; cross-OS is a productization concern. Mixing them risks both.
- **MAUI Avalonia Preview 1 is preview, not GA.** Preview-1-to-GA timeline is typically 6-12 months; ship-on-preview risk applies. Aligns with Sunfish's existing **pre-release latest-first policy** (.NET 11 preview, MAUI 10 preview, EFCore preview, Aspire preview already in production posture).
- **No Anchor consumers exist yet.** Phase 1 proves the architecture; no tenants depend on Anchor builds. Cross-OS strategy decisions made now have zero migration cost.

### Soft dependencies

- **G4 follow-up wiring** (ManagedRelayPeerDiscovery → Anchor MauiProgram) lands in Phase 1. The Linux backend spike must verify this wiring runs identically on Linux — but that's a smoke test, not a redesign.
- **Sunfish.UIAdapters.Blazor** must render unchanged via the Avalonia backend. This is a research deliverable in the spike — almost certainly true (the partnership announcement specifically calls out Razor/Blazor compatibility) but needs verification.

### Constraints

- **No Phase 1 budget.** This intake produces docs only (ADR 0048 + this intake). The 2-3 day implementation spike is a Phase 2 prep deliverable that follows G7.
- **No language fragmentation.** Sunfish stays .NET-first. Tauri (Rust shell), Electron (Node.js), and similar stack-swap options are not in scope. ADR 0044 already documented this exclusion; ADR 0048 inherits it.
- **No code duplication.** A parallel `accelerators/anchor-photino/` codebase (an option I considered earlier in the strategy discussion) is rejected. The MAUI Avalonia backend is additive within the existing `accelerators/anchor/`.

## Open Questions Resolved by ADR 0048

- **Q1 — Cross-OS path for Linux:** MAUI Avalonia backend (preview, but on a partnership-backed roadmap). Decision in ADR 0048.
- **Q2 — Cross-OS path for Mac/iOS/Android:** Stay on native MAUI; re-enable when MAUI 10 Mono runtime preview lands stable packages (existing ADR 0044 trigger). No change.
- **Q3 — Should Anchor migrate off MAUI entirely?** No. The partnership announcement validates MAUI as the cross-platform .NET-first path. Photino, Tauri, Avalonia-direct, Uno Platform are all rejected as primary paths in ADR 0048.
- **Q4 — When does the spike run?** Phase 2 prep, after Phase 1 G7 closes. Not interleaved with G5/G6/G7.
- **Q5 — Production-readiness criterion for MAUI Avalonia Preview 1?** Spike acceptance criteria: (a) Razor + BlazorWebView render correctly on Ubuntu 22.04 LTS, (b) the AnchorSyncHostedService gossip daemon round completes successfully on Linux against a Win64 peer, (c) the AnchorCrdtDeltaBridge applies a CRDT delta inbound from a Win64 peer. If all three pass, ship Linux on preview alongside Win.

## Spike Scope (for Phase 2 prep, post-G7)

**Estimated effort: 2-3 working days.**

1. **Day 1 — Backend swap mechanics.** Add Linux TargetFramework + MAUI Avalonia preview NuGet reference to `Sunfish.Anchor.csproj`. Resolve any package conflicts. Build cleanly on Ubuntu 22.04 LTS. Exit criterion: clean `dotnet build` on Linux.

2. **Day 2 — UI smoke test.** Launch Anchor on Linux, navigate to the team-switcher page, confirm Sunfish.UIAdapters.Blazor components render (status bar, dialog provider, theme service). Exit criterion: visual parity screenshot vs. Win64 reference.

3. **Day 3 — Wire-protocol smoke test.** Run AnchorSyncHostedService on Linux. Run a second Anchor on Win64 (LAN mDNS discovery). Confirm: HELLO completes, GOSSIP_PING exchange succeeds, DELTA_STREAM with a CRDT mutation propagates Win→Linux. Exit criterion: 2-node cross-OS round completes successfully.

**Deliverables:**
- A `spike-report-anchor-linux-2026-XX-XX.md` in `icm/01_discovery/output/`
- A small PR adding the Linux TargetFramework to `Sunfish.Anchor.csproj` (gated behind a build-time flag if the preview is too unstable for default builds)
- A decision: ship Linux on preview now, or hold for MAUI Avalonia Preview 2 / RC.

## Re-enablement Triggers (Mac/iOS/Android)

Per ADR 0044, the existing csproj has `<TargetFrameworks>` lines commented out for `net11.0-android`, `net11.0-ios`, `net11.0-maccatalyst`. ADR 0048 inherits these triggers unchanged. Re-enable when:

- MAUI 10 GA published with Mono runtime packages for Android/iOS/MacCatalyst, OR
- MAUI 11 reaches RC

No spike needed for these — uncomment the lines, build, ship.

## Next Steps

1. Land ADR 0048 (drafted alongside this intake) — same PR.
2. Continue Phase 1: G5 (backup) → G6 (recovery) → G7 (conformance scan).
3. After G7 closes, schedule the 2-3 day Linux spike as the first Phase 2 prep deliverable.
4. After the spike returns data, decide whether to ship Linux on MAUI Avalonia preview or wait for GA.

Proceed to **08_release** for ADR 0048 (no design or build stages — this is a docs-only deliverable). The implementation spike runs as a separate ICM cycle in Phase 2 prep, starting with its own intake.

## Acceptance Criteria for This Intake

- [x] ADR 0048 drafted in `docs/adrs/0048-anchor-multi-backend-maui.md`
- [x] ADR README index updated
- [x] This intake committed under `icm/00_intake/output/`
- [ ] PR opened with auto-merge (CI gates the merge)
- [ ] Memory entry updated noting "Anchor cross-OS = multi-backend MAUI per ADR 0048"

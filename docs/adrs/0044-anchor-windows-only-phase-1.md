# ADR 0044 — Anchor ships Windows-only for Business MVP Phase 1

**Status:** Accepted (2026-04-26)
**Date:** 2026-04-26
**Resolves:** Open Question Q2 + Decision D1 from `icm/01_discovery/output/business-mvp-phase-1-discovery-final-2026-04-26.md`

## Context

The Sunfish Business MVP Plan (`C:/Projects/the-inverted-stack/docs/business-mvp/mvp-plan.md` §10 Phase 1) calls for the Anchor app shell to ship on Windows, macOS, and Linux. The current `accelerators/anchor/Sunfish.Anchor.csproj` is conditionally Windows-only:

```xml
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">net11.0-windows10.0.19041.0</TargetFrameworks>
<TargetFrameworks Condition="!$([MSBuild]::IsOSPlatform('windows'))">net11.0-maccatalyst</TargetFrameworks>
```

with an inline comment explaining: *"MAUI 10 preview: Mono runtime packages for Android/iOS/MacCatalyst are not yet published at the version the workload expects, causing restore failures on this SDK. Multi-targeting is scaffolded but commented; Anchor ships Windows-only until MAUI 10 stabilizes, then re-enables mobile targets."*

This means the `Win/Mac/Linux` deliverable in plan §10 is technically blocked on .NET MAUI 10/11 stabilization — an external dependency on Microsoft's release cadence with no firm ship date.

## Decision drivers

- **Unblocking Phase 1.** Phase 1 is the prerequisite for the four business modules (accounts/vendors/inventory/projects). Every week Phase 1 is delayed compounds across Phases 2-5.
- **Risk register entry already names this.** Plan §12 lists ".NET MAUI cross-platform stability issues" as Medium-likelihood / High-impact, with mitigation: "Track .NET MAUI roadmap; have Tauri fallback evaluated by end of Phase 1."
- **Phase 1 functionality doesn't require cross-platform.** The Phase 1 deliverable ("Anchor opens, syncs with another Anchor over LAN, syncs with Bridge over WAN, key recovery flow works end-to-end") can be demonstrated on a Win-only fleet.
- **Plan §6 demo deployments are Phase 6.** The cross-platform value lives in Phase 6 demo scenarios (restaurant POS, construction, consultancy). Mac/Linux availability there.
- **Tauri pivot is a 1-way door.** The csproj's Razor + MAUI scaffolding, plus the broader .NET ecosystem alignment in book Ch12, makes a Tauri rewrite a major architectural change that should not be undertaken without strong evidence.

## Considered options

### Option A — Ship Windows-only for Phase 1; defer Mac/Linux to a later phase

- **Pro:** unblocks Phase 1 immediately; matches existing csproj's deliberate posture; lowest risk; reversible
- **Con:** plan §10 Win/Mac/Linux deliverable becomes Win-only-then-Mac-Linux; conformance scan baseline runs on Win64 only initially
- **Trigger to revisit:** MAUI 10 GA (Mono runtime packages publish) OR MAUI 11 reaches RC

### Option B — Wait for MAUI 10/11 stabilization before starting Phase 1

- **Pro:** plan §10 deliverable ships as written
- **Con:** unbounded wait on Microsoft's release cadence; blocks Phases 2-5; the risk register already calls this out as High-impact
- **Trigger to revisit:** never selected — defeats the project velocity target

### Option C — Switch Anchor to Tauri (Rust + web UI)

- **Pro:** unblocks cross-platform immediately; Tauri Win/Mac/Linux is mature today
- **Con:** large pivot; book Ch12 names .NET ecosystem; existing MAUI Razor scaffolding partially discarded; new Rust runtime in dependency tree; 1-way door
- **Trigger to revisit:** if MAUI 10 doesn't reach GA by end of Phase 2 AND we need cross-platform sooner than Phase 6

## Decision

**Adopt Option A.** Anchor ships Windows-only for Phase 1. Mac/Linux ship in a later phase once MAUI 10 stabilizes (or MAUI 11 lands at RC).

A parallel Tauri-fallback evaluation runs during Phase 1 as a separate workstream (per plan §12 risk-register mitigation). Its output is a recommendation memo at end of Phase 1, NOT a binding architectural choice.

## Consequences

### Positive

- Phase 1 unblocked immediately
- No architectural pivot
- The Tauri evaluation memo informs Phase 6 (demo deployments) without gating Phase 1
- The csproj's existing condition stays as-is — no code change required

### Negative

- Phase 1 conformance baseline scan runs on Win64 only; Mac/Linux conformance is a deferred milestone
- If a Phase 2-5 module surfaces a cross-platform requirement (unlikely for backend logic, possible for UI behaviors), it gets deferred or worked-around with a Win64-first-then-port pattern
- Plan §10 deliverable wording ("Win/Mac/Linux") needs an asterisk — captured in this ADR

## Revisit triggers

- MAUI 10 GA released with stable Mono runtime packages for Android/iOS/MacCatalyst
- MAUI 11 reaches RC
- Tauri-evaluation memo end-of-Phase-1 strongly favors switching
- A Phase 2-5 module surfaces a hard cross-platform requirement that can't be deferred
- A real customer scenario (one of the three Phase 6 demos) requires Mac or Linux Anchor before Phase 6 ships

## References

- Spec source: `C:/Projects/the-inverted-stack/docs/business-mvp/mvp-plan.md` §10 Phase 1, §12 risk register
- Anchor csproj: `accelerators/anchor/Sunfish.Anchor.csproj` (lines 4-9 MAUI 10 preview comment)
- Phase 1 intake: `icm/00_intake/output/business-mvp-phase-1-foundation-intake-2026-04-26.md`
- Phase 1 final discovery: `icm/01_discovery/output/business-mvp-phase-1-discovery-final-2026-04-26.md`
- Sunfish Phase 1 risk register entry: plan §12 "MAUI cross-platform stability issues"

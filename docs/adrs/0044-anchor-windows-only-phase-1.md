---
id: 44
title: Anchor ships Windows-only for Business MVP Phase 1
status: Accepted
date: 2026-04-26
tier: accelerator
concern:
  - operations
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0044 — Anchor ships Windows-only for Business MVP Phase 1

**Status:** Accepted (2026-04-26), **Amended 2026-04-28** — MacCatalyst build is now functional; "Windows-only" narrows to "Windows is the default Phase 1 deployment target," not a hard build block. See [Amendment 2026-04-28](#amendment-2026-04-28--maccatalyst-build-now-functional) below.
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

---

## Amendment 2026-04-28 — MacCatalyst build now functional

**Status:** Amendment Accepted (2026-04-28)

### What changed

The MAUI 11 preview workload (`maui-maccatalyst` manifest `26.2.11588-net11-p3`) released between this ADR's acceptance and 2026-04-28 published the missing Mono runtime packs for MacCatalyst. Combined with three csproj enhancements and three per-developer host-environment prereqs, Anchor now produces a runnable `.app` bundle on macOS in addition to its Windows build.

The csproj enhancements (in `accelerators/anchor/Sunfish.Anchor.csproj`):

1. `<RuntimeIdentifiers>maccatalyst-x64;maccatalyst-arm64</RuntimeIdentifiers>` conditional on `IsOSPlatform('osx')` — Mac App Store requires both architectures or x64-only, never arm64-only.
2. `<ValidateXcodeVersion>false</ValidateXcodeVersion>` on osx — workload pins Xcode 26.3 exactly; bypass needed for ABI-compatible 26.4.x.
3. `<UseRidGraph>true</UseRidGraph>` on osx + a `_SunfishStripAspNetCoreFromMacCatalystPacks` MSBuild target — `Microsoft.AspNetCore.App` ships no maccatalyst-specific runtime pack (the reference assemblies Blazor Hybrid needs are inlined in MAUI's MacCatalyst runtime pack); the target removes the AspNetCore entry from `UnavailableRuntimePack` and `ResolvedFrameworkReference` so neither `ResolveRuntimePackAssets` (NETSDK1082) nor MAUI's runtime-component manifest reader (MSB4096) fires.

The host-environment prereqs are documented in `docs/dev/anchor-maccatalyst-build-prereqs.md` (Xcode license acceptance, `xcode-select` symlink target, Xamarin `Settings.plist` case canonicalization).

### Why this doesn't fully supersede ADR 0044

This is an amendment, not a supersedure. The original "Windows-only Phase 1" decision was about which fleet the Phase 1 conformance baseline scan, the ICM Stage 06 build environment, and the multi-trustee recovery flow demos run against — and those decisions are still load-bearing:

- **Phase 1 conformance baseline scan (G7):** still runs on Win64 first. Mac conformance follows once Phase 1 is signed off and we have a Mac CI runner provisioned.
- **Stage 06 build for any Anchor-touching ICM pipeline:** still expected to run on a Windows session unless the change is Mac-specific. The MacCatalyst build path is an unblocker for local Mac development, not a swap of the canonical CI target.
- **Phase 6 demo deployments (restaurant POS, construction, consultancy):** still the right phase for cross-platform parity work — the demos are where Mac/Linux first ship to real customers.

### What this DOES change

- **G6 host integration is no longer Windows-gated.** The Phase 1 G6 host integration task — wiring `RecoveryCompleted → ISqlCipherKeyDerivation → RotateKeyAsync` in Anchor and persisting `RecoveryEvents` to the per-tenant audit log — can now be developed and tested locally on a Mac. Stage 06 final review still runs on Windows for the conformance scan, but day-to-day iteration unblocks immediately.
- **Macs become legitimate workstations for Sunfish core engineering.** The previous ADR 0044 implicit posture — "develop on Windows, period, until MAUI 10 stabilizes" — narrows to "Windows is still the default CI/conformance target; macOS is now a fully supported developer workstation."
- **Tauri-fallback evaluation deprioritized.** The risk-register entry that motivated keeping a Tauri pivot warm is materially smaller now. The evaluation memo deliverable from end-of-Phase-1 stands, but its expected output shifts from "should we switch?" to "what's left for Linux."

### Revisit triggers (additions)

- Mac CI runner provisioned → re-evaluate G7 conformance baseline scan plan to include MacCatalyst RID
- Linux build still blocked (no `linux-*` MAUI runtime packs published yet) → Tauri-evaluation memo focuses there

### References (additions)

- Build prereqs: `docs/dev/anchor-maccatalyst-build-prereqs.md`
- csproj changes: `accelerators/anchor/Sunfish.Anchor.csproj` (RuntimeIdentifiers, ValidateXcodeVersion, UseRidGraph, _SunfishStripAspNetCoreFromMacCatalystPacks target)

# ADR 0048 — Anchor multi-backend MAUI: native MAUI for Win/Mac/iOS/Android, MAUI Avalonia for Linux/WebAssembly

**Status:** Accepted (2026-04-27)
**Date:** 2026-04-27
**Extends:** ADR 0044 (Anchor ships Windows-only for Phase 1) — does not supersede; adds Phase 2 cross-OS roadmap that ADR 0044 deferred.
**Resolves:** Cross-OS strategy question raised at the close of Phase 1 G4 (PR #169 — `ManagedRelayPeerDiscovery`).

## Context

ADR 0044 (2026-04-26) shipped Anchor on Win64 for Phase 1 because MAUI 10 preview's Mono runtime packages for Android/iOS/MacCatalyst did not yet publish at the version the workload expected. The deferred Mac/Linux/iOS/Android targets were framed as a single dependency on "MAUI 10 stabilization" — a single Mono runtime fix that, when delivered, would re-enable all four non-Windows platforms simultaneously. That framing was correct as of 2026-04-26.

Two events in late 2025 / early 2026 change the analysis:

1. **2025-11-11** — Avalonia and Microsoft announced a partnership: *"Avalonia is bringing .NET MAUI to Linux and WebAssembly, delivering on the community's most requested features since MAUI launched."* (avaloniaui.net blog)
2. **2026-03-16** — **MAUI Avalonia Preview 1** released. Same MAUI codebase, swappable native backend per platform: native MAUI on Win/Mac/iOS/Android; Avalonia-rendered on Linux and WebAssembly.

The MAUI 10 Mono runtime preview that blocks Mac/iOS/Android (per ADR 0044) and the MAUI Avalonia backend that unblocks Linux are **independent fixes on independent timelines**. Linux no longer needs to wait for the Mono runtime preview to stabilize. The original ADR 0044 framing — "wait for one fix, get four platforms" — is now wrong; the correct framing is "Linux unblocks via Avalonia backend regardless of Mono status; Mac/iOS/Android unblock via Mono regardless of Avalonia status."

This decoupling has a corollary: a Phase 2 cross-OS plan can ship Linux earlier than Mac/iOS/Android by adopting the MAUI Avalonia preview. The marketing-credibility floor for a local-first product is "runs on Win + Mac + Linux desktop." Mac is gated on the Mono fix; Linux is now decoupled. So the path to a credible Phase 2 release is:

- Win: ship via native MAUI (already working in Phase 1)
- Mac: ship via native MAUI when MAUI 10 Mono runtime preview stabilizes
- Linux: ship via MAUI Avalonia backend (preview but on a partnership-backed roadmap)
- iOS / Android: ship via native MAUI when MAUI 10 Mono runtime preview stabilizes
- WebAssembly: optionally ship via MAUI Avalonia backend at a later phase (Phase 3+ exploratory)

Note: this is one MAUI codebase with platform-specific backends, not multiple codebases. The same `MauiProgram.cs`, `App.xaml.cs`, Razor components, Sunfish.UIAdapters.Blazor, DI wiring, hosted services run on every backend. Only the native shell that hosts the BlazorWebView differs per platform.

## Decision drivers

- **Cross-OS is foundational to the local-first pitch.** Paper §1, ADR 0006/0031, and *The Inverted Stack* Ch1 all frame local-first as device-pluralistic. A Win-only Anchor v1 ships, but Anchor v2 must be cross-OS or the architecture pitch fails.
- **Avoid language fragmentation.** Sunfish is .NET-first per the paper and existing ADRs. Tauri (Rust shell), Electron (Node.js), Photino (still .NET but parallel codebase) all introduce maintenance overhead the project doesn't want.
- **Avoid code duplication.** A second `accelerators/anchor-*/` codebase would diverge over time; a backend swap within `accelerators/anchor/` is additive and stays in sync by construction.
- **Avoid UI rewrite.** Razor + BlazorWebView + Sunfish.UIAdapters.Blazor is the existing UI layer. MAUI Avalonia explicitly preserves Razor compatibility per the partnership announcement.
- **Preserve existing scaffolding.** ADR 0044's commented-out `<TargetFrameworks>` lines for android/ios/maccatalyst are still correct; this ADR does not invalidate them.
- **Pre-release latest-first policy already accepts preview risk.** Sunfish runs .NET 11 preview, MAUI 10 preview, EFCore preview, Aspire preview. MAUI Avalonia Preview 1 fits the same posture.

## Considered options

### Option A — Multi-backend MAUI: native MAUI for Win/Mac/iOS/Android + MAUI Avalonia for Linux/WASM

- **Pro:** single codebase; preserves all existing MAUI scaffolding; .NET-first; no UI rewrite; partnership-backed roadmap; WebAssembly future option at zero extra cost
- **Pro:** Linux unblocks independently of Mono fix; Phase 2 ship date no longer hostage to a single external dependency
- **Con:** depends on MAUI Avalonia Preview 1 stabilizing (preview-1 to GA typically 6-12 months); preview-tier risk applies
- **Con:** introduces a third preview dependency in the production Anchor build (alongside .NET 11 preview and MAUI 10 preview)
- **Trigger to revisit:** if MAUI Avalonia is abandoned or its preview cadence stalls without progress

### Option B — Switch Anchor to Photino.Blazor for Linux (parallel codebase)

- **Pro:** Photino v4.0.13 (Jan 2025) is more mature than MAUI Avalonia Preview 1; less preview risk
- **Pro:** stays .NET-first
- **Con:** parallel `accelerators/anchor-photino/` codebase will diverge from the MAUI codebase over time
- **Con:** doesn't help Mac/iOS/Android — Photino is desktop-only, so we'd still need MAUI for mobile
- **Con:** the partnership announcement effectively makes MAUI Avalonia the canonical .NET-first cross-OS path; betting on Photino is betting against the official roadmap
- **Verdict:** rejected. Considered earlier in the strategy discussion; superseded by the MAUI Avalonia option once the partnership status was confirmed.

### Option C — Switch Anchor to Avalonia UI directly (no MAUI)

- **Pro:** Avalonia UI is more mature than MAUI Avalonia (which uses Avalonia under the hood)
- **Con:** requires UI rewrite away from Razor and into Avalonia XAML; loses Sunfish.UIAdapters.Blazor investment
- **Con:** loses MAUI ecosystem (mobile targets, native APIs)
- **Verdict:** rejected. The investment in Razor + BlazorWebView + Sunfish.UIAdapters.Blazor is too large to discard.

### Option D — Switch Anchor to Tauri (Rust shell + Blazor WASM)

- **Pro:** Tauri is mature for Win/Mac/Linux/mobile
- **Con:** language fragmentation; every contributor needs Rust toolchain
- **Con:** large pivot away from .NET-first posture documented in paper Ch12 and existing ADRs
- **Con:** ADR 0044 already considered Tauri and explicitly framed it as a one-way door requiring strong evidence
- **Verdict:** rejected. The MAUI Avalonia partnership eliminates the strongest argument for Tauri (cross-OS .NET-first wasn't possible before; now it is).

### Option E — Wait indefinitely for MAUI 10 Mono fix (status quo per ADR 0044)

- **Pro:** zero work
- **Con:** unbounded wait on Microsoft's release cadence with no committed timeline
- **Con:** ignores the partnership announcement; leaves Linux gated on a fix it doesn't actually need
- **Con:** ADR 0044's "wait for MAUI" is a Phase 1 stance, not a permanent strategy
- **Verdict:** rejected for Phase 2; correct for Phase 1 (this ADR does not change Phase 1 behavior).

## Decision

**Adopt Option A — multi-backend MAUI.**

For Phase 2:
- Native MAUI for Windows (already shipping), macOS, iOS, Android
- MAUI Avalonia backend for Linux
- MAUI Avalonia for WebAssembly is exploratory / Phase 3+

For Phase 1: **no change.** ADR 0044's "Win64 only Phase 1" remains in force. This ADR adds the Phase 2 roadmap; it does not move work into Phase 1.

The Phase 2 prep spike (2-3 days, scoped in `icm/00_intake/output/anchor-cross-os-strategy-intake-2026-04-27.md`) runs after Phase 1 G7 (conformance baseline scan) closes. The spike validates MAUI Avalonia Preview 1 on Ubuntu 22.04 LTS, smoke-tests the AnchorSyncHostedService and AnchorCrdtDeltaBridge across a Win64↔Linux gossip round, and produces a ship/wait recommendation.

## Consequences

### Positive

- Single MAUI codebase covers all five target platforms (Win, Mac, Linux, iOS, Android) plus optional WebAssembly
- Preserves all existing Phase 1 investment: MauiProgram.cs, App.xaml.cs, AnchorSyncHostedService, AnchorCrdtDeltaBridge, Sunfish.UIAdapters.Blazor, kernel-sync, kernel-crdt, kernel-runtime, kernel-security
- ADR 0044's commented-out `<TargetFrameworks>` lines for android/ios/maccatalyst stay valid as the path for those platforms
- Linux ship date decoupled from Mac/iOS/Android ship date
- WebAssembly target opens a Phase 3+ option for browser-based local-first Anchor without further architectural commitment
- Aligns with the existing pre-release latest-first policy (preview tier accepted across the stack until v1)
- Closes a question that would otherwise recur every time MAUI's preview cadence slips ("should we switch off MAUI?" — answer: no, multi-backend is the path)

### Negative

- Anchor's production build will eventually carry three preview dependencies: .NET 11 preview + MAUI 10 preview + MAUI Avalonia Preview 1. Acceptable per existing policy but worth noting.
- Linux release ships on a preview backend until MAUI Avalonia GA. Risk of regressions during preview cadence; mitigated by the spike's smoke test gate (don't ship Linux until cross-OS gossip round verifies).
- The MAUI Avalonia backend introduces a new failure surface specific to Linux (Avalonia rendering bugs, GTK/X11/Wayland edge cases). Win/Mac/iOS/Android paths remain on the native MAUI rendering they always used.
- Two render paths in production (native MAUI on Win/Mac/iOS/Android; Avalonia on Linux) means visual regression testing must cover both. The Sunfish.UIAdapters.Blazor a11y harness already covers per-adapter tests (ADR 0034); extend it to per-backend.

### Neutral

- WebAssembly target is opt-in; no Phase 2 commitment
- The spike's exit criterion is a ship/wait recommendation, not an automatic ship; the team can still defer Linux past Phase 2 if the preview is too unstable

## Revisit triggers

- **MAUI Avalonia Preview 1 → 2 / RC / GA released:** revisit Linux ship readiness criteria
- **MAUI Avalonia partnership stalls or is cancelled:** fall back to evaluating Photino.Blazor (Option B) or wait-and-see
- **MAUI 10 Mono runtime publishes stable packages:** uncomment the existing `<TargetFrameworks>` lines in `Sunfish.Anchor.csproj` and re-enable Mac/iOS/Android per ADR 0044 — no new ADR needed
- **A real Phase 2 customer scenario surfaces a hard "must work today on Linux/Mac" requirement before the spike runs:** escalate to bring the spike forward
- **Spike returns negative result** (Razor/BlazorWebView don't render correctly via Avalonia preview, OR cross-OS gossip round fails): hold Linux release until a later MAUI Avalonia preview; do NOT regress to Photino without strong evidence

## References

- ADR 0044 — Anchor ships Windows-only for Business MVP Phase 1
- ADR 0031 — Bridge as Hybrid Multi-Tenant SaaS (Zone-C)
- ADR 0006 — Bridge Is a Generic SaaS Shell, Not a Vertical App
- ADR 0034 — Accessibility Harness per Adapter (extension target for per-backend a11y testing)
- Avalonia partnership announcement (2025-11-11): <https://avaloniaui.net/Blog>
- MAUI Avalonia Preview 1 release (2026-03-16): <https://avaloniaui.net/Blog>
- Sunfish pre-release latest-first policy: memory entry `project_pre_release_latest_first_policy.md`
- Spike scope: `icm/00_intake/output/anchor-cross-os-strategy-intake-2026-04-27.md`
- Anchor csproj: `accelerators/anchor/Sunfish.Anchor.csproj` (existing ADR-0044 conditional `<TargetFrameworks>`)
- Paper §1, §17.2, §20.7
- *The Inverted Stack* Ch1, Ch12 (.NET-first ecosystem positioning)

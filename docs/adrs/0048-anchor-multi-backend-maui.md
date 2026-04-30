# ADR 0048 — Anchor multi-backend MAUI: native MAUI for Win/Mac/iOS/Android, MAUI Avalonia for Linux/WebAssembly

**Status:** Accepted (2026-04-27; **A1 mobile-scope amendment landed 2026-04-30** — see §"Amendments (post-acceptance)")
**Date:** 2026-04-27 (Accepted) / 2026-04-30 (A1 mobile-scope amendment)
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

---

## Amendments (post-acceptance)

### A1 (REQUIRED) — Mobile scope clarification: Anchor (MAUI iOS) vs Field-Capture App (SwiftUI native) coexist

**Date:** 2026-04-30
**Driver:** Workstream #23 iOS Field-Capture App intake (`icm/00_intake/output/property-ios-field-app-intake-2026-04-28.md` §"In scope" item 1 + §"Explicitly NOT in scope") explicitly rejects MAUI for the Field-Capture App in favor of SwiftUI native. The original ADR specified "Native MAUI for Windows, macOS, **iOS**, Android" — phrasing that, read literally, would conflict with W#23's SwiftUI-native decision. This amendment clarifies scope: **ADR 0048 specifies the multi-backend story for Anchor (the desktop/tablet-class workspace switching app per ADR 0032). The W#23 Field-Capture App is a separate iOS app with different UX requirements; both can ship on iOS using different UI frameworks.**

#### A1.1 — Scope clarification (the carve-out)

ADR 0048's "Native MAUI for ... iOS ..." phrasing applies to **Anchor** specifically — the multi-team workspace switching desktop-class app per ADR 0032. On iOS / iPadOS, Anchor would target iPad as a tablet-class app (large-screen workspace switching, full Sunfish kernel, multi-actor delegation surface, payments / messaging / signatures viewing). MAUI iOS is the right framework for that target — it shares the Anchor codebase across Win/Mac/Linux/iPad and inherits the kernel + adapter stack already proven on Windows.

The W#23 **Field-Capture App** is a distinct iOS app:

| Aspect | Anchor on iPad (per ADR 0048) | Field-Capture App (per W#23) |
|---|---|---|
| Repo path | `accelerators/anchor/` (existing) — new MAUI iOS target | `accelerators/anchor-mobile-ios/` (new family per W#23 intake item 9) |
| UI framework | MAUI Blazor Hybrid (per ADR 0048) | SwiftUI native (per W#23 intake item 1) |
| Sunfish kernel on device | Yes (full kernel; Anchor is the desktop substrate) | No (per ADR 0028-A1; capture-only event queue, no CRDT) |
| Concurrency profile | Multi-actor workspace; CRDT-managed | Single-actor per device; LWW + forward-only-status guards (per ADR 0028-A1+A2) |
| Camera / OCR / PencilKit / Vision / DataScannerViewController / PDFKit | Not the primary use case (Anchor is workspace UI) | Core use case — drives the SwiftUI-native rejection of MAUI |
| Distribution | Same as Anchor desktop (per ADR 0048 distribution roadmap) | TestFlight Phase 2.1; App Store Phase 2.3 (per W#23 intake item 8) |
| Phase | Phase 2 (per ADR 0048 cross-OS roadmap) | Phase 2.1 (per W#23 intake) |

The two apps coexist on the same iPad if a user wants (e.g., owner uses Anchor for workspace switching + uses Field-Capture for inspections).

#### A1.2 — Why MAUI is wrong for Field-Capture

Per W#23 intake §"Explicitly NOT in scope":

> *MAUI iOS — explicitly rejected. SwiftUI native chosen for camera, PencilKit, background URLSession, Vision/DataScannerViewController, and PDFKit. Reusing the Blazor adapter for field UI is a false economy.*

Mechanically:

- **Camera + Vision / DataScannerViewController** — MAUI's camera abstraction is a thin wrapper over native APIs that loses ergonomic affordances (focus modes, capture format selection, depth data). DataScannerViewController for nameplate OCR is an iOS-16+ API with no MAUI equivalent.
- **PencilKit + signature canvas** — MAUI Blazor Hybrid renders Blazor in a WebView; signature canvas accuracy degrades through the WebView boundary (touch-event timing latency; pressure-sensitivity loss).
- **`URLSessionConfiguration.background`** — MAUI's HTTP client abstraction wraps `NSURLSession` but doesn't expose `URLSessionConfiguration.background` settings (per ADR 0028-A1.2.1: `discretionary`, `sessionSendsLaunchEvents`, file-based upload tasks). Field-Capture's offline-first sync model requires direct access.
- **PDFKit** — for receipt / W-9 / lease document rendering. MAUI's PDF support is limited to read-only viewing; W#23 Phase 5 needs annotation + signature embedding.

The MAUI cost would be a Blazor abstraction layer over each of these native APIs, losing fidelity at every boundary. SwiftUI native targets each API directly.

#### A1.3 — Why MAUI is right for Anchor on iPad

Anchor's primary UX is workspace UI (lists, dialogs, forms, dashboards). The native-API surface MAUI poorly maps onto (camera / PencilKit / DataScannerViewController) is NOT Anchor's primary surface. MAUI Blazor Hybrid keeps Anchor's iPad target on the same codebase as Windows / Mac / Linux — single source of truth for the workspace UI, single deployment path, single test surface.

Anchor on iPad would still consume Field-Capture App data (an inspection captured on the Field-Capture App lands at Anchor via the merge boundary; Anchor renders the Inspection list); the two apps integrate via the data substrate, not via shared UI code.

#### A1.4 — Andriod Field-Capture App is post-MVP

Per W#23 intake §"Explicitly NOT in scope": "Android version — Phase 4+; not in Phase 2 scope (BDFL is iOS-only)." If/when Android Field-Capture ships, it gets a similar amendment (Android Studio + Kotlin / Jetpack Compose; same architectural pattern as iOS but different platform).

ADR 0048's "Native MAUI for ... Android ..." applies to **Anchor on Android tablet** only, NOT to a future Field-Capture Android app.

#### A1.5 — Cited-symbol verification (Decision Discipline Rule 6)

This amendment is at the architectural / repo-layout layer. Per the cohort lesson (8-of-8 substrate amendments needing post-acceptance fixes), explicitly verify:

- ADR 0028 (CRDT engine selection) + A1 (mobile reality check) — verified merged on `origin/main` (PR #342, 2026-04-30)
- ADR 0032 (multi-team workspace switching) — verified Accepted on `origin/main`
- W#23 intake (`icm/00_intake/output/property-ios-field-app-intake-2026-04-28.md`) — verified existing on `origin/main`; status `design-in-flight`
- `accelerators/anchor/` — verified existing path on `origin/main`
- `accelerators/anchor-mobile-ios/` — does NOT exist on `origin/main` (introduced by W#23 Stage 06 build per the queued plan)

No new `Sunfish.*` source symbols introduced by A1.

#### A1.6 — Compatibility with the main ADR 0048 decision

This amendment does NOT change the multi-backend MAUI decision for Anchor. Native MAUI for Win/Mac/iOS/Android + MAUI Avalonia for Linux/WASM remains the Anchor strategy. The amendment scopes a **carve-out for the Field-Capture App** as a separately-architected sibling iOS app.

Phase 2 prep spike scope (per ADR 0048's cross-OS-strategy intake reference) is unaffected — the spike validates MAUI Avalonia on Ubuntu, not iOS UI choices.

#### A1.7 — Open questions

- **OQ-A1.1:** does Anchor on iPad share the iOS Keychain entry space with the Field-Capture App, or do they have separate Keychain access groups? **A1 default:** separate access groups; Anchor uses its existing pairing-token surface (per ADR 0032), Field-Capture uses its own per-device install identity (per ADR 0028-A2.3). Cross-app credential sharing TBD pending a Phase 2.2+ multi-app integration ADR.
- **OQ-A1.2:** does the W#23 Field-Capture App require an Android-equivalent ADR (or amendment to this ADR) when Android Field-Capture eventually lands? **A1 default:** yes; same pattern (a sibling amendment OR a new ADR if scope warrants). Out of A1 scope.

#### A1.8 — Pre-acceptance audit

- **AP-1 (unvalidated assumption):** A1.2's MAUI-camera-loses-ergonomics claim is verifiable but time-sensitive — MAUI's native-API abstraction matures across versions. Pass for the 2026-04-30 snapshot; future MAUI releases may close the gap.
- **AP-3 (vague success criteria):** A1.1's coexistence-on-iPad model is concrete (separate repos, separate Keychain access groups, separate distribution). Pass.
- **AP-21 (cited facts):** all cited ADRs verified on `origin/main` (A1.5). Pass.

This amendment is `Accepted` upon merge of the PR introducing it. Per the cohort lesson (8-of-8 substrate ADR amendments needed council fixes), this PR's auto-merge is intentionally disabled until a Stage 1.5 council subagent reviews.

---
id: 48
title: 'Anchor multi-backend MAUI: native MAUI for Win/Mac/iOS/Android, MAUI Avalonia for Linux/WebAssembly'
status: Accepted
date: 2026-04-27
tier: accelerator
concern:
  - operations
  - ui
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0048 — Anchor multi-backend MAUI: native MAUI for Win/Mac/iOS/Android, MAUI Avalonia for Linux/WebAssembly

**Status:** Accepted (2026-04-27; **A1 + A2 mobile-scope amendments landed 2026-04-30** — see §"Amendments (post-acceptance)")
**Date:** 2026-04-27 (Accepted) / 2026-04-30 (A1 mobile-scope amendment / A2 council-fix amendments)
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

### A2 (REQUIRED, mechanical) — A1 council-review fixes

**Driver:** Stage 1.5 council review of A1 (`icm/07_review/output/adr-audits/0048-A1-council-review-2026-04-30.md`, dated 2026-04-30; PR #349) ran pre-merge per cohort discipline. Council found 0 Critical + 3 Major + 4 Minor + 0 Encouraged. All 3 required + 4 encouraged are mechanical (per Decision Discipline Rule 3); A2 applies them all. Cohort batting average updates to **9-of-9 substrate ADR amendments needing post-acceptance amendments after council review**.

#### A2.1 — F1' (Major, AP-21): drop unsupported "per ADR 0032" iPad framing

A1.1 paragraph 2 originally read "Anchor — the multi-team workspace switching desktop-class app per ADR 0032." Council verified ADR 0032 contains no iPad / tablet-class framing — that's A1's own design call, not an inheritance.

**Replace A1.1 paragraph 2 with:**

> ADR 0048's "Native MAUI for ... iOS ..." phrasing applies to **Anchor** specifically — the multi-team workspace switching app per ADR 0032. **A1 extends ADR 0032's framing to a tablet form factor (iPad) not previously named** by either ADR 0032 or ADR 0048; this scope decision is A1's own. On iOS / iPadOS, Anchor would target iPad as a workspace-class app (large-screen workspace switching, full Sunfish kernel, multi-actor delegation surface, payments / messaging / signatures viewing). MAUI iOS is the right framework for that target — it shares the Anchor codebase across Win/Mac/Linux/iPad and inherits the kernel + adapter stack already proven on Windows.

The semantic change is dropping the "per ADR 0032" qualifier on the iPad-target sentence and explicitly acknowledging A1 as the framing extension.

#### A2.2 — F2' (Major, AP-1): explicit Anchor-on-iPad camera scope boundary

A1.3's "Anchor's primary surface is workspace UI not native-API-heavy" claim is true today (Phase 1 Win-only) but unbounded for Phase 2 — Anchor on iPad may want receipt photo capture, document scanning, signature canvas. The carve-out logic ("Field-Capture handles native APIs, Anchor handles workspace UI") needs an explicit boundary statement.

**Insert as new sub-section A1.3.1, immediately after A1.3:**

##### A1.3.1 — Anchor-on-iPad native-iOS-API boundary (per A2.2)

Anchor on iPad uses MAUI's native-API abstractions only for **ambient platform integration**:
- File pickers (open / save dialogs over iCloud Drive, on-device storage)
- Share sheets (export PDFs, share via standard iOS share UI)
- Photo library selection (`MediaPicker.PickPhotoAsync` — selecting a photo already in the iCloud Photos library)
- Standard iOS notification banners (in-app notifications via MAUI's `INotification` abstraction)

Anchor on iPad does **NOT** use MAUI for camera capture, document scanning, signature canvas, or any other capture-flow UX. Those are delegated to the Field-Capture App via the data substrate — Anchor renders the resulting artifacts (e.g., displays a captured receipt photo, displays a captured signature image) but does not capture them.

**Boundary rule:** if a Phase 2+ Anchor-on-iPad scenario surfaces a hard requirement for native-API camera capture (e.g., owner wants to capture a receipt directly in Anchor without launching Field-Capture), that scenario triggers a new intake. The carve-out is NOT automatically invalidated; the new intake decides whether to (a) widen the carve-out (Anchor gains native-API capture for that domain), (b) hold the line (user is directed to use Field-Capture for capture; Anchor remains read-only for those domains), or (c) revisit the SwiftUI-vs-MAUI decision wholesale (per A1 revisit triggers A2.5).

This boundary statement seals the AP-1 finding by codifying the assumption the carve-out logic relies on.

#### A2.3 — F3' (Major, AP-3): extend A1.7 OQs to cover iOS coexistence surface

A1.7 originally listed two OQs (one Keychain-related, one Android-Field-Capture-related). Council found three additional iOS-platform coexistence concerns silent: URL scheme namespacing, push notification entitlements, deep-link routing.

**Replace A1.7 with the expanded 5-item OQ block:**

#### A1.7 — Open questions (revised per A2.3)

- **OQ-A1.1 (Keychain access groups):** does Anchor on iPad share the iOS Keychain entry space with the Field-Capture App, or do they have separate Keychain access groups? **A1 default:** separate access groups. Anchor uses its existing pairing-token surface (per ADR 0032). Field-Capture uses its own per-device install identity (per ADR 0028-A2.3 — `device_id` derived from install Ed25519 public key). Cross-app credential sharing TBD pending a Phase 2.2+ multi-app integration ADR.
- **OQ-A1.2 (URL scheme namespacing):** Anchor uses ApplicationId `dev.sunfish.anchor` (existing in `accelerators/anchor/Sunfish.Anchor.csproj`). Field-Capture proposes `dev.sunfish.field` (sibling under same `dev.sunfish.*` prefix). **A1 default:** separate ApplicationIds + separate URL schemes. Cross-app deep linking deferred to Phase 2.2+ multi-app integration ADR.
- **OQ-A1.3 (push notifications):** **A1 default:** separate APNs entitlement profiles per app. No shared notification surface in Phase 2.1. Field-Capture is offline-first and may not need push at all (per W#23 intake's `URLSessionConfiguration.background` sync pattern + per ADR 0028-A1.2 which specifies no on-device merge). If push becomes a requirement, it triggers a Phase 2.2+ multi-app integration ADR.
- **OQ-A1.4 (deep-link routing between apps):** out of A1 scope. If Field-Capture's Inspection-detail UX wants to open Anchor's signing surface (or Anchor wants to launch Field-Capture for capture), that's a Phase 2.2+ multi-app integration concern. **A1 default:** no cross-app deep linking in Phase 2.1.
- **OQ-A1.5 (Android Field-Capture):** does the W#23 Field-Capture App require an Android-equivalent ADR (or amendment to this ADR) when Android Field-Capture eventually lands? **A1 default:** yes; same pattern (a sibling amendment OR a new ADR if scope warrants). Out of A1 scope. *(was OQ-A1.2 in A1; renumbered to keep the iOS-platform concerns contiguous as A1.7.OQ-A1.1 through OQ-A1.4)*

#### A2.4 — F4' (Encouraged, AP-19): expand A1.5 cited-symbol audit

A1.5 originally listed 5 references but missed two load-bearing ones. Add to A1.5:

- **ADR 0044 (Anchor ships Windows-only for Phase 1)** — verified Accepted on `origin/main`; A1 explicitly preserves Phase 1 Win-only scope unchanged. The Phase 2 cross-OS roadmap (which A1 amends) sits on top of ADR 0044's Phase 1 baseline.
- **`accelerators/anchor/Sunfish.Anchor.csproj`** — verified existing on `origin/main`; commented-out `<TargetFrameworks>` lines for `net11.0-android;net11.0-ios` remain valid as the iOS re-enable path per ADR 0048. **A1 does NOT require uncommenting these** — that's a Phase 2 build action gated on the Phase 2 cross-OS-strategy spike completion.

The csproj entry is load-bearing because A1.1's "Anchor on iPad … MAUI iOS is the right framework" is dependent on ADR 0048's scaffolding-already-exists claim; the csproj is where that scaffolding lives.

#### A2.5 — F5' (Encouraged, AP-11): A1-specific revisit triggers

A1 inherited ADR 0048's revisit triggers (about MAUI Avalonia stabilization), but those don't cover the carve-out's failure modes. Add:

##### A1.9 — Revisit triggers (per A2.5)

Trigger a new intake or amendment if ANY of these fire:

- **MAUI 11+ closes a meaningful subset of A1.2's native-API fidelity gaps** (camera ergonomics, PencilKit pressure-data, `URLSessionConfiguration.background` settings exposure, PDFKit annotation): revisit whether Field-Capture's SwiftUI-native rejection of MAUI is still warranted. (Not an automatic flip; the existing investment in SwiftUI may still win on UX. But the rejection rationale needs re-examination.)
- **Anchor on iPad surfaces a hard native-iOS-API requirement** that the A2.2 boundary statement does not absorb (e.g., Phase 2 product scope adds in-Anchor camera receipt capture as a hard requirement): triggers a new intake to decide whether to widen the carve-out or hold the line.
- **Apple deprecates `DataScannerViewController` / `URLSessionConfiguration.background` / PencilKit pressure-data API**, OR releases a successor API that materially changes A1.2's load-bearing list: triggers a Field-Capture architecture review.
- **Multi-app integration ADR ships** (resolving OQ-A1.2 / OQ-A1.3 / OQ-A1.4 cross-app deep-link / push / Keychain-sharing): A1's "two apps coexist" framing is updated to point at the multi-app integration ADR.

#### A2.6 — F6' (Encouraged, AP-17): resolve W#23 OQ-I1 explicitly

W#23 intake's OQ-I1 asks whether the Field-Capture App lives at `accelerators/anchor-mobile-ios/` or `apps/field/`. A1 implicitly resolved this by naming `accelerators/anchor-mobile-ios/` throughout, but didn't formally close the OQ.

**Add as new sub-section A1.1.1:**

##### A1.1.1 — Resolves W#23 OQ-I1 (per A2.6)

This amendment authoritatively resolves W#23 intake OQ-I1: the Field-Capture App lives at `accelerators/anchor-mobile-ios/`, NOT `apps/field/`. W#23 Stage 02 inherits the resolved path. The `accelerators/` framing is consistent with ADR 0048's accelerator-zone model (Zone-A field-class accelerator for the Field-Capture App).

#### A2.7 — F7' (Encouraged, AP-15): soften A1.1 Anchor-on-iPad feature-set claims

A1.1's table row for Anchor on iPad currently claims "full Sunfish kernel, multi-actor delegation surface, payments / messaging / signatures viewing." Anchor's *exact* iPad feature set is a Phase 2 product call, not an A1 carve-out claim.

**Replace A1.1's table row for "Anchor on iPad" with:**

| Aspect | Anchor on iPad (per ADR 0048) | Field-Capture App (per W#23) |
|---|---|---|
| ... | ... | ... |
| Feature set | **Anchor's existing feature set, scoped per Phase 2 iPad product intake (TBD)**. Phase 1 (Win-only per ADR 0044) ships workspace switching + multi-actor delegation; Phase 2 iPad target inherits whichever subset of Phase-2 Anchor capabilities the iPad product scope names. | Domain capture flows: receipts, assets, inspections, signatures, mileage, work-order responses (per W#23 intake §"In scope" item 4). Single-actor-per-device. |
| ... | ... | ... |

(Other rows of the A1.1 table are unchanged.)

#### A2.8 — Cohort batting average (updated)

**9-of-9 substrate ADR amendments** now needing post-acceptance fixes after council review. A1 here is the 9th. Pattern remains locked-in: pre-merge council on substrate ADR amendments is canonical; cost of skipping = held-state delay (A2-of-0046 paid ~24h).

#### A2.9 — Cited-symbol re-verification (Decision Discipline Rule 6)

Per the cohort lesson, A2 re-runs the cited-symbol audit:

| Symbol / reference | Status |
|---|---|
| ADR 0028 + A1+A2 | ✓ verified merged on `origin/main` (PR #342, 2026-04-30T10:31:33Z) |
| ADR 0032 (multi-team workspace switching) | ✓ verified Accepted (A1.1's "extends to iPad" framing is now explicitly A1's call, not inherited; A2.1 fix) |
| ADR 0044 (Anchor ships Windows-only for Phase 1) | ✓ verified Accepted (added per A2.4) |
| ADR 0054 (electronic signatures; signature canvas reference) | ✓ verified Accepted on `origin/main` |
| W#23 intake | ✓ verified existing |
| `accelerators/anchor/Sunfish.Anchor.csproj` (commented-out iOS / Android `<TargetFrameworks>`) | ✓ verified existing (added per A2.4) |
| `accelerators/anchor-mobile-ios/` | ✓ correctly classified introduced-by-W#23 (does NOT exist on `origin/main`; A2.6 confirms path resolution) |

No new `Sunfish.*` source symbols introduced by A2.

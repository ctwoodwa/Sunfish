# Multi-Platform Host Strategy — Evaluation

**Date:** 2026-04-18
**Status:** Research note informing spec v0.4
**Companion revisions:** `docs/specifications/sunfish-platform-specification.md` v0.4 §2.3, §4.4, Appendix C #5/#6, new Appendix E

---

## Thesis

Sunfish's v0.2 spec assumed two UI rendering targets — Blazor and React — each with its own adapter, with parity tests keeping them in lockstep. v0.4 revises that assumption:

1. **React is dropped.** Blazor MAUI Hybrid covers web + Windows + macOS + iOS + Android from a single component codebase. A second JavaScript-framework adapter is not a strategic wedge.
2. **Mobile and desktop become first-class hosts,** not a hypothetical future. Sunfish ships a `Sunfish.Hosts.*` family alongside the existing Blazor adapter.
3. **Lessons learned from the Iced (Rust) GUI library** inform the shape of `ui-core` extensions — specifically the discipline around pluggable renderers, typed message-driven state machines, and per-widget style records.

Scope of this note: research + recommendation. The actual spec edits live in v0.4 of `sunfish-platform-specification.md` (same PR).

---

## Part 1 — Iced lessons learned

Iced is a Rust GUI library inspired by the Elm architecture, with pluggable rendering backends (`wgpu` for GPU; `tiny-skia` for CPU) and a strict Model-View-Update discipline. The research informing this section comes from `docs.rs/iced/latest/iced/` and the Context7-published snapshot of Iced's current API.

### Six patterns worth adopting

Each pattern is evaluated against Sunfish's shipped Blazor adapter and graded on ROI.

#### L1 — Pluggable rendering-backend discipline [HIGH ROI]

**What Iced does.** Every widget in Iced is generic over its `Renderer`: `Button<Message, Theme, Renderer>` where `Renderer` is the trait that turns geometry + text into pixels. Iced ships two concrete renderers (`wgpu`, `tiny-skia`) and consumers can plug their own.

**Why it matters for Sunfish.** Sunfish has `ISunfishCssClassBuilder` for swapping CSS frameworks (FluentUI, Bootstrap, Material), but that abstraction only covers class-name generation. It doesn't contemplate "render this button as a WinUI native control" or "render this text input as a MAUI native widget." The Blazor MAUI Hybrid story works today because all rendering goes through a WebView — but if a consumer wants pure-native widgets on mobile for performance, they have no extension point.

**Recommended shape.** Introduce `Sunfish.UICore.ISunfishRenderer` as a sibling contract to `ISunfishCssClassBuilder`. Web (DOM) is the default; MAUI-native and Avalonia-native are possible providers. Widget authors write once against `RenderFragment` + `ISunfishRenderer`; consumers switch renderers at DI registration. This is the move that makes Phase 4.5 multi-platform hosts scale without per-platform rewrites.

#### L2 — `Task` and `Subscription` as first-class async primitives [HIGH ROI]

**What Iced does.** `Task<Message>` is fire-and-report (await a future, produce a message). `Subscription<Message>` is long-running (a stream that produces messages continuously until cancelled). Both are returned from the `update` function, so async work is part of the state-machine contract, not a scattered `async void` side channel.

**Why it matters for Sunfish.** Blazor components today mix async by handling `async Task` in event callbacks, running `InvokeAsync`, wiring SignalR subscriptions ad-hoc. There's no unifying abstraction. For workflow components (inspection wizards, multi-step forms, real-time sensor dashboards) the lack of structure shows up as bugs: cancellation isn't propagated, disposal isn't clean, messages arrive out of order.

**Recommended shape.** Ship `Sunfish.Foundation.IClientTask<TMessage>` + `Sunfish.Foundation.IClientSubscription<TMessage>` as contracts, plus `InMemoryDispatcher` + `SignalRDispatcher` defaults. Blazor `EventCallback` stays as the sync path. Opt-in, not mandatory. Particularly valuable for sensor/voice/imagery ingestion UIs where real-time backpressure matters.

#### L3 — Message-driven state machines for complex components [MEDIUM ROI]

**What Iced does.** Every interaction is an explicit variant in a `Message` enum. The `update` function is exhaustive pattern match — the compiler catches missing cases.

**Why it matters for Sunfish.** Blazor components let you mutate `@state` directly from event handlers. Simple components don't need structure; complex ones (wizards, inspection flows, lease-renewal dialogs) benefit from the rigor.

**Recommended shape.** `Sunfish.UICore.Patterns.StateMachineComponent<TState, TMessage>` as an opt-in base component. Not mandatory for all components — the 228 existing components stay unchanged. Use this for new complex flows and for refactoring when a component grows unwieldy. Pairs naturally with L2 (Task/Subscription compose into the Message channel).

#### L4 — Per-widget `Style` struct pattern [MEDIUM ROI]

**What Iced does.** Each widget exposes a `Style` struct with strongly-typed fields: `TextInput::Style { background, border, icon, placeholder, value, selection }`. Themes provide a `Catalog` that returns a `Style` per widget variant.

**Why it matters for Sunfish.** Current widget styling builds CSS class strings. Strings are easy to mistype, impossible to diff cleanly, and fragment when providers (FluentUI vs Bootstrap) disagree on class hierarchies.

**Recommended shape.** Per-widget style records (`SunfishTextInputStyle`, `SunfishButtonStyle`, `SunfishGridStyle`, etc.) alongside the existing class builders. Providers convert style records to their CSS output; future MAUI-native renderer converts the same style records to native attributes. Closes the type-safety gap without forcing immediate migration of the 228 shipped components.

#### L5 — Operation tree-walking for cross-cutting queries [MEDIUM ROI]

**What Iced does.** The `Operation` trait walks the widget tree to answer global questions: "is any text input focused?", "scroll the focused element into view", "collect all validation errors in this form." No component holds references to other components.

**Why it matters for Sunfish.** Blazor has `@ref` + `ElementReference` for reaching into child components, plus `JSInvokable` callbacks. Both are fragmented: managing refs across component trees quickly becomes spaghetti.

**Recommended shape.** `Sunfish.UICore.Operations.ISunfishOperation` + `OperationContext.Walk(rootElement)`. Ship default operations for focus management, scroll-into-view, validation-summary collection. Niche but removes a known pain point.

#### L6 — Unified `Element` coercion target [LOW ROI, LOW COST]

**What Iced does.** `Element<Message, Theme, Renderer>` is THE type every widget converts to via `.into()`. Composition helpers take `Element`, return `Element`. The compiler enforces type compatibility across the tree.

**Why it matters for Sunfish.** Blazor's `RenderFragment` + `IComponent` is a looser contract. For the overwhelmingly common case, this doesn't matter. For typed composition helpers (e.g., a `WrapWithLabel(child)` helper that preserves child's message type), an explicit generic wrapper helps.

**Recommended shape.** `Sunfish.UICore.SunfishElement<TMessage>` as a `RenderFragment` wrapper carrying `TMessage`. Low priority; ship if and when a composition-helper pain point emerges.

### Two patterns to NOT adopt

**Iced's 11-method `Widget` trait.** Blazor's `ComponentBase` already handles layout, lifecycle, and rendering through the framework's own tree diffing. Re-implementing Iced's explicit lifecycle (`tag`, `state`, `diff`, `size`, `layout`, `operate`, `update`, `draw`, `mouse_interaction`, `size_hint`, `children`, `overlay`) inside Blazor is pure cost with no clarity gain.

**GPU-native rendering via `wgpu`.** If a Sunfish view genuinely needs GPU-level rendering (3D asset viewer, real-time drone-imagery composite), the right answer is to drop into pure MAUI / Avalonia for that view — not to port Iced's `wgpu` path into Blazor. Blazor's HTML canvas + WebGL is the ceiling for web; native views fill the gap on mobile/desktop.

---

## Part 2 — Blazor MAUI Hybrid as the multi-platform host strategy

### Why MAUI Hybrid is the right bet

Blazor MAUI Hybrid hosts Blazor components inside a native `WebView` on each platform. The component model is identical across web, desktop, and mobile; the host changes. As of .NET 10:

- **Windows** via WinUI 3 (MSIX-packaged)
- **macOS** via Mac Catalyst
- **iOS** via UIViewController + WKWebView
- **Android** via WebView
- **Web** via Blazor Server or Blazor WASM

The 228 components shipped in `Sunfish.Components.Blazor` run on all five targets without modification. The only per-platform code is the host — a minimal entry point (~50 lines) that configures DI and launches the WebView.

### What MAUI Hybrid covers natively

| Concern | Coverage |
|---|---|
| UI component rendering | Full (WebView everywhere) |
| Navigation | Full (Blazor Router + per-host back-button wiring) |
| Theming / providers | Full (existing FluentUI/Bootstrap/Material providers work) |
| DI / services | Full (standard `IServiceCollection`) |
| Device filesystem | Mostly (via `Microsoft.Maui.Essentials`) |
| Network / HTTP | Full (HttpClient works identically) |
| Offline storage | Partial (needs SQLite or Akavache) |
| Hardware sensors (camera, GPS, accelerometer) | Via `Microsoft.Maui.Essentials` + `CommunityToolkit.Maui` |
| Native UI elements (maps, date pickers) | Partial (blend of Blazor widgets + MAUI interop) |
| Push notifications | Via `Shiny.NET` or `Plugin.Firebase` |
| Biometric auth | Via `Plugin.Fingerprint` or MAUI Essentials |

### What MAUI Hybrid doesn't cover, and OSS gap-fillers

#### Lightweight desktop deployment — **Photino.Blazor**

MAUI's runtime is ~200 MB. For utility apps, admin dashboards, and desktop variants that don't need mobile-style packaging, **Photino.Blazor** is a ~30 MB web-view shell that hosts Blazor natively on Windows/macOS/Linux. Sunfish ships a Photino-flavored variant of `Sunfish.Hosts.Desktop` for deployments where footprint matters.

#### Native component interop — **CommunityToolkit.Maui**

For the ~10% of cases where a native MAUI view beats a Blazor-in-WebView view (map with offline tiles, AR camera preview, rich hardware biometric prompts), `CommunityToolkit.Maui` provides battle-tested native controls that compose into a MAUI Hybrid page alongside the Blazor view.

#### State management — **Fluxor** or **TimeWarp-State**

Redux-pattern state management for Blazor. Both are mature; **Fluxor** is Elm-inspired and pairs naturally with the L3 Message-driven state-machine pattern described above. Ship as an optional pattern for complex flows.

#### Rich data grids / schedulers — **MudBlazor**, **Radzen.Blazor**, **Blazorise**

Current Sunfish DataGrid / Gantt / Scheduler components are solid but basic. For demanding scenarios (pivot tables, advanced filtering, large-dataset virtualization), these OSS libraries go deeper. Sunfish can either wrap their components as providers (same architecture as FluentUI/Bootstrap/Material) or let consumers substitute at DI registration time.

#### Code / rich-text editing — **BlazorMonaco**, **TinyMCE-Blazor**

For schema editing (JSON, YAML), inspector notes with formatting, and voice-transcript review surfaces. Neither reinvents the editor; both are composable into Sunfish components.

#### Desktop hardware APIs — MAUI Essentials + **CommunityToolkit.Maui**

USB, serial, Bluetooth Classic, printer access. Already covered by `Microsoft.Maui.Essentials`; gaps filled by `CommunityToolkit.Maui`.

#### Push notifications — **Shiny.NET** or **Plugin.Firebase**

Cross-platform push infrastructure. Apple Push Notification Service + Firebase Cloud Messaging handled uniformly. Paired with Phase D federation for notification delivery from the Sunfish kernel.

#### Offline-first storage — **SQLite-net-pcl** + Sunfish `IBlobStore` over local filesystem

Entity mutations while offline, synced via Phase D's `SyncEnvelope` replay when network returns. The Phase D air-gap sneakernet scenario already covers this semantically; mobile wiring is an incremental step.

#### Accessibility verification — **Axe-core-blazor**

Automated accessibility regression testing in CI. Critical for the PM-vertical audience (municipal code enforcement, accessibility compliance requirements).

### Explicit non-gap: React is dropped

The v0.2 spec planned a `Sunfish.Adapters.React` parallel to `Sunfish.Components.Blazor`, with parity tests between the two. v0.4 drops this entirely:

- **Maintenance cost of two adapters is 2× per feature.** Every new widget, every bug fix, every accessibility polish happens twice. Parity tests enforce duplication, not reduce it.
- **Blazor MAUI Hybrid covers every platform a React adapter would target.** The only "React unique" scenario is teams that already have React skills — which is a team-hiring question, not an architecture question. Teams with React expertise can wrap Sunfish's HTTP + SignalR surface directly.
- **Rust adapter is also out of scope** (see research note in this same directory, TBD). A portable Rust kernel crate may be valuable for mobile-native + client-side canonical-JSON, but a Rust UI adapter parallel to Blazor is not.

Concrete spec edits:
- Remove `Sunfish.Adapters.React` from §2.3 package architecture diagram.
- Delete parity-test language from §2.3 adapter paragraph.
- Delete Appendix C open question #6 ("React adapter scope and timeline").
- Replace Appendix C open question #5 ("Mobile strategy — MAUI, React Native, Flutter, or PWA?") with a paragraph naming MAUI Blazor Hybrid as the answer and cross-referencing §4.5.

---

## Part 3 — Package architecture revision

### Current (v0.2)

```
foundation
  ↓
ui-core
  ↓
ui-adapters-blazor    ui-adapters-react (planned)
  ↓                      ↓
blocks-*                blocks-* (planned)
  ↓
apps/*, accelerators/*
```

### Proposed (v0.4)

```
foundation
  ↓
ui-core
  ↓
ui-adapters-blazor             (shipped — 228 components)
  ↓
blocks-*                       (shipped — forms, tasks, scheduling, assets)
  ↓
hosts-web                      (existing Blazor Server/WASM host — renamed)
hosts-desktop-maui             (NEW — MAUI Blazor Hybrid for Windows + macOS)
hosts-desktop-photino          (NEW — Photino.Blazor for lightweight desktop)
hosts-mobile-maui              (NEW — MAUI Blazor Hybrid for iOS + Android)
hosts-native-maui              (NEW, OPTIONAL — pure MAUI views for perf-critical surfaces)
```

Key property: `hosts-*` packages are **thin** — each is ~100 lines of platform-specific entry-point code plus DI configuration. All the substantive UI code lives in `ui-adapters-blazor` and `blocks-*`, which is unchanged. Adding a new host doesn't fork the component tree.

### What changes in ui-core to enable this

Three additive contracts, no breaking changes:

- `ISunfishRenderer` (from L1) — renderer abstraction alongside `ISunfishCssClassBuilder`.
- `IClientTask<TMessage>` + `IClientSubscription<TMessage>` (from L2) — async primitives.
- Per-widget `Style` records (from L4) — incremental adoption.

Plus one optional pattern:

- `StateMachineComponent<TState, TMessage>` (from L3) — base component for complex flows.

---

## Part 4 — Phase 4.5 — multi-platform host shipment

Inserts between Phase 4 (PM vertical) and Phase 5 (secondary verticals) in the spec §4 roadmap.

**Duration estimate:** 3–4 months (after Phase 4 completes).

**In scope:**

- `packages/hosts-web/` — rename / re-home the existing Blazor Server + WASM host for explicit topology.
- `packages/hosts-desktop-maui/` — MAUI Blazor Hybrid, Windows + macOS, MSIX + dmg packaging, CI builds.
- `packages/hosts-desktop-photino/` — Photino.Blazor variant for lightweight desktop deployments.
- `packages/hosts-mobile-maui/` — iOS + Android, App Store + Play Store submission pipeline, MAUI Essentials wiring.
- `ui-core` additions per L1–L5 above (`ISunfishRenderer`, `IClientTask`/`IClientSubscription`, per-widget Style records, `StateMachineComponent`, `ISunfishOperation`).
- OSS integration: Fluxor, CommunityToolkit.Maui, Shiny.NET, SQLite-net-pcl, Axe-core-blazor — each with a Sunfish-integration adapter package where needed.
- Offline-first mobile wiring: local `IBlobStore` + entity mutations queued for Phase D `SyncEnvelope` sync on reconnect.
- Accessibility CI: Axe-core regression tests for the 228 components, gated in CI.
- Bridge accelerator: validate the mobile host against the PM inspections + maintenance flows (spec §6.3, §6.4).

**Deliverables:**

- NuGet: `Sunfish.Hosts.Web`, `Sunfish.Hosts.Desktop.Maui`, `Sunfish.Hosts.Desktop.Photino`, `Sunfish.Hosts.Mobile.Maui`, `Sunfish.Hosts.Native.Maui`.
- App Store / Play Store packaging pipelines.
- MSIX + dmg signing pipelines.
- Updated kitchen-sink demo: same app, 5 platforms.
- Docs: per-host getting-started guides + platform-specific gotchas.

**Exit criteria:**

- Kitchen-sink demo builds and runs on all 5 platforms from a single command per platform.
- All 228 Blazor components render correctly on all 5 platforms (visual regression via screenshot diff against web baseline).
- Axe-core accessibility test suite passes on all platforms.
- Bridge accelerator's inspection workflow works end-to-end on an iPad and a Windows laptop against the same federation peer set.
- Offline-first mobile: inspector can create + edit inspection entities while offline; reconnecting syncs via Phase D federation without manual intervention.

---

## Part 5 — Open questions deferred to v0.5+

1. **Avalonia vs Uno Platform vs pure MAUI for `hosts-native-maui`.** Each has tradeoffs: Avalonia is Skia-based with best cross-platform fidelity; Uno can host Blazor (giving yet another Blazor path); pure MAUI is Microsoft-blessed and most discoverable. Phase 4.5 picks pure MAUI initially; Avalonia and Uno stay as evaluation candidates if specific gaps emerge.

2. **Embedded / edge UX.** If Sunfish-ingested sensor data needs on-device UI (handheld scanners, rugged field devices), neither MAUI nor Photino is a good fit. Rust + Iced or similar embedded GUI becomes relevant. Parked for 2027+; revisit if an accelerator requires it.

3. **Real-time collaborative editing on mobile.** Phase D federation delivers the protocol; mobile-specific concerns (intermittent connectivity, battery-aware sync cadence, conflict-resolution UI) are a research track on top of that.

4. **Mobile-specific authentication.** Biometric auth + OAuth on mobile is different from server-side OIDC flows. Bridge accelerator will surface concrete requirements; until then, use MAUI Essentials' authentication APIs as the generic path.

5. **`hosts-web-wasm` AOT compilation** for large apps. .NET 10 WASM AOT is now stable; the Phase 4.5 work should benchmark whether AOT-compiled WASM bundles are small enough for a production PM-vertical deployment.

---

## Part 6 — References

- **Iced docs:** https://docs.rs/iced/latest/iced/
- **Iced architecture notebook:** https://docs.iced.rs/iced/
- **Context7 Iced snapshot:** used during this research; query results included `iced::application(init, update, view).run()`, `Task::perform(future, msg_fn)`, `Subscription::run(stream_builder)`, and the `Widget<Message, Theme, Renderer>` trait shape.
- **.NET MAUI documentation:** https://learn.microsoft.com/dotnet/maui/
- **Blazor Hybrid:** https://learn.microsoft.com/aspnet/core/blazor/hybrid/
- **Photino.Blazor:** https://github.com/tryphotino/photino.Blazor
- **Fluxor:** https://github.com/mrpmorris/Fluxor
- **MudBlazor / Radzen / Blazorise:** https://mudblazor.com, https://blazor.radzen.com, https://blazorise.com
- **CommunityToolkit.Maui:** https://github.com/CommunityToolkit/Maui
- **Shiny.NET:** https://github.com/shinyorg/shiny

---

## Part 7 — Recommendation summary

Adopt in this order of priority:

1. **Immediate (v0.4 spec):** Drop React. Update Appendix C. Document Phase 4.5. This is a PR of docs-only edits; low risk, high alignment value.
2. **ui-core extensions (stage 1):** Add `ISunfishRenderer`, `IClientTask`/`IClientSubscription`, per-widget Style records as additive contracts. No breaking changes; existing 228 components unchanged.
3. **Phase 4.5 execution:** Ship `hosts-desktop-maui`, `hosts-mobile-maui`, `hosts-desktop-photino` as a bundle. Validate against Bridge accelerator.
4. **OSS integrations:** Add Sunfish-integration packages for Fluxor, CommunityToolkit.Maui, Shiny.NET, Axe-core as Phase 4.5 polish.
5. **Deferred:** `hosts-native-maui`, Avalonia/Uno evaluation, embedded-device research.

Do not adopt:
- React adapter (dropped).
- Rust UI adapter (a Rust kernel crate for mobile-native primitives is a separate research track; UI adapter is not the wedge).
- GPU-native rendering in Blazor (if needed, escalate to native views).

---

*Companion: spec v0.4 applies §2.3, §4.4, §4.5 (new), Appendix C #5/#6, Appendix E edits to implement this note's recommendations.*

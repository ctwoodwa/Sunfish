---
id: 33
title: Browser Shell v1 Render Model + Trust Posture
status: Accepted
date: 2026-04-23
tier: accelerator
concern:
  - threat-model
  - ui
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0033 — Browser Shell v1 Render Model + Trust Posture

**Status:** Accepted (2026-04-23)
**Date:** 2026-04-23
**Deciders:** Chris Wood (BDFL)
**Resolves:** The three design-time stop-work items raised by `_shared/product/wave-5.3-decomposition.md` §9 and reiterated in `icm/07_review/output/paper-alignment-audit-2026-04-23-eod.md` §6:

1. Decrypted role-keys in Blazor Server circuit memory vs. the paper's "in-browser" language.
2. Proxy back-pressure on the browser → `bridge-web` → tenant-child WebSocket dual-buffer path.
3. Blazor Server vs. Blazor WASM render-mode selection for the browser shell.

This ADR **amends** ADR 0031's "Browser shell (new)" sub-section (single-paragraph commitment to "new Blazor Server app"). It does not supersede ADR 0031 — the Zone-C Hybrid decision, the three trust levels, and the per-tenant data-plane decision all stand.

---

## Context

ADR 0031 commits Bridge to a Zone-C Hybrid deployment and introduces a "browser shell" at each tenant's subdomain — the only surface through which a hosted-peer-free customer can participate in their team's CRDT state without installing Anchor. The single paragraph reads:

> "New Blazor Server app at each tenant's subdomain. User authenticates, browser fetches wrapped role-key bundle, decrypts role keys into memory, opens WebSocket to the tenant's hosted-node peer, reads/writes via CRDT ops decrypted in-browser. Session keys wiped on tab close / logout. No persistent browser local-node in v1."

The Wave 5.3 decomposition plan then had to operationalize that paragraph. Doing so surfaced three concrete tensions:

**1. "Blazor Server" and "decrypted in-browser" are mutually exclusive in strict reading.** A Blazor Server render mode means UI components execute server-side inside a SignalR *circuit*. If the CRDT-apply + role-key-decrypt work is done "in the circuit," the server process transiently holds the device key and derived role keys in RAM. The paper's §11.2 ciphertext-at-rest invariant ("operator holds only ciphertext") and ADR 0031's own "Relay-only" trust level (operator sees only ciphertext) are — on a literal reading — incompatible with a server-memory custody posture. The decomposition plan flagged this as "Attested-hosted-peer OK; Relay-only needs Option B or a v2 WASM renderer."

**2. Bridge's reverse-proxy is a two-hop WebSocket path.** Browser → `bridge-web` → per-tenant `local-node-host` child → back again. Each hop has its own Kestrel buffer, its own `System.Net.WebSockets.WebSocket` copy-loop, its own back-pressure curve. No production load data exists for this shape. Tail latency (p99) under 30-concurrent-browser-session load is unknown; whether the browser-shell feels "live" or "SignalR over dial-up" depends on that number.

**3. Render-mode selection cascades into both of the above.** If we pick pure Blazor Server, concern #1 is maximal but first paint is fast and server-side observability (ILogger + OpenTelemetry) is effortless. If we pick pure Blazor WASM, concern #1 goes to zero (keys never leave the browser) but first paint slows by several hundred ms (Argon2 WASM + the CRDT engine WASM), app-shell size grows, and server-side observability disappears (the server only sees encrypted frames). Blazor 8+ ships `InteractiveAuto` — a hybrid that renders Server initially, then transparently swaps to WASM once the WASM runtime is ready — which opens a middle ground neither option covers.

The paper's §17.2 framing of "hosted-relay-as-SaaS-node" does not mandate a render mode; it mandates the *invariant* — the operator holds ciphertext-only unless the tenant explicitly grants more. ADR 0031's §"Decisions embedded" #3 reinforces: "Operator NOT in CP quorum by default." The render mode selected here either preserves or violates that posture at the wire.

---

## Decision drivers

- **Paper §11.2 ciphertext-at-rest invariant.** The operator must not read tenant ciphertext. "Ciphertext" here means the key material, not just the transport frames — a server that holds the decryption key while the circuit is alive has effectively held cleartext for that window.
- **Paper §17.2 "hosted-relay-as-SaaS" framing.** The paper explicitly sanctions browser-accessible hosted deployments; it does not dictate key custody beyond the invariant.
- **Ship velocity.** W5.3 is the last Zone-C component before W5.4 (founder/joiner flows) and a usable browser-shell demo. A render-mode choice that adds 2+ weeks must be justified by a real threat, not a theoretical one.
- **Developer experience — Blazor Server is the known quantity.** Sunfish's entire Blazor adapter (`packages/ui-adapters-blazor`) compiles and runs under Server render mode today. A WASM-only posture requires packaging the full adapter + `hash-wasm` Argon2 + `@noble/ed25519` + the `kernel-crdt` runtime + the in-memory CRDT mirror into a Blazor-WASM bundle. That is new engineering — not reuse.
- **Observability.** Server-render circuits emit rich server-side telemetry. WASM-only renders do not. For a v1 product, "did the user's delta stream open and apply?" is a question that needs answering from logs, not support tickets.
- **Reverse-proxy latency budget.** `bridge-web` today is a typical Aspire-hosted Blazor Server app. Adding a WebSocket reverse-proxy — either hand-rolled via `ClientWebSocket` pump or via Yarp — introduces one hop. v1's latency budget is "no worse than 2× a direct connection at p99"; exceeding that forces escalation to a dedicated gateway tier.
- **Trust-level differentiation.** ADR 0031 names three tenant trust levels (Relay-only / Attested hosted peer / No hosted peer). The browser-shell render-mode decision must make explicit which trust levels it is compatible with.
- **Upgrade-path preservation.** Whatever we pick for v1, the paper's wire format is render-mode-agnostic. v2 changes to the render mode must not require a protocol change. This constrains us toward choices that are layering decisions, not protocol decisions.

---

## Considered options

### Stop-work #1 — Key custody posture

**Option 1a — Accept Server-circuit key custody with explicit trust-level caveat.**
Document that the browser shell v1 implies an operator who transiently holds decryption keys in RAM during the circuit lifetime. Tenants who cannot accept this must use Anchor, "No hosted peer," or upgrade to Option B (dedicated deployment).

- *Pro:* fastest ship; reuses existing Blazor Server machinery.
- *Pro:* rich server-side observability.
- *Con:* violates the literal "Relay-only" trust level. "Attested hosted peer" becomes the *minimum* trust level a shared-Bridge subscriber gets — a regression from ADR 0031's default of Relay-only.
- *Con:* XSS or server-side compromise reads cleartext keys.

**Option 1b — Pure WASM renderer.**
Move the entire UI — including CRDT mirror, role-key derivation, decryption — into Blazor WASM. The server circuit sees only ciphertext WebSocket frames and static HTML host.

- *Pro:* preserves "Relay-only" trust level by construction; keys never cross into server RAM.
- *Pro:* XSS still extracts from browser memory, but the server compromise surface shrinks dramatically.
- *Con:* +2 weeks of engineering to port `kernel-crdt` apply + `IBrowserCrdtMirror` to a WASM-runnable form.
- *Con:* app-shell first paint slows by ~300-800ms (WASM download + init).
- *Con:* server-side logs cannot diagnose user issues — "my delta didn't apply" becomes a browser-console support ticket.

**Option 1c — Hybrid: `InteractiveAuto` render mode.**
Blazor 8+ `InteractiveAuto` renders Server initially (first paint fast), then swaps to WASM once the WASM runtime is downloaded + ready. Components are authored once; the render host decides per-component. We route the *key-handling path* (passphrase → Argon2 → device_key → HKDF → Ed25519 signer → challenge-response → WebSocket HELLO signature) into a WASM-only sub-tree, and route the *live UI* (CRDT mirror rendering, dashboard view) into Server.

- *Pro:* `device_key` and derived Ed25519 keys are computed, stored, and used to sign handshake frames *only* in the WASM runtime — the server never sees them.
- *Pro:* Role-key decryption of incoming `DELTA_STREAM` frames can also stay WASM-side; the server receives ciphertext, forwards ciphertext, and logs ciphertext only.
- *Pro:* Live UI rendering (decrypted CRDT snapshot → HTML) stays Server for observability and fast first paint. The decrypted *state* transiently transits server memory while circuit is live, but the *keys* do not.
- *Con:* Trust-level caveat is different-but-still-present: "Operator sees decrypted session state during circuit lifetime, NOT the device key or role keys." This is materially stronger than Option 1a (which has both) but weaker than Option 1b (which has neither).
- *Con:* Two render modes in one app surface is more complex to author, test, and reason about. Mixing `[Inject]`-resolved services across the Server/WASM boundary has sharp edges.
- *Con:* Still requires packaging Argon2 + Ed25519 + role-key HKDF into a Blazor-WASM bundle. Smaller scope than Option 1b (no CRDT engine port), but not zero.

### Stop-work #2 — Reverse-proxy choice

**Option 2a — `bridge-web` as the reverse proxy.**
The existing `bridge-web` project hosts the subdomain middleware (W5.3.A already landed), the cookie auth (W5.3.B), and adds a hand-rolled `TenantWebSocketReverseProxy` that opens a `ClientWebSocket` to the tenant-child `/ws` and pumps bytes opaquely.

- *Pro:* reuses shipped infrastructure; one deployable for v1.
- *Pro:* the proxy sees ciphertext only (if stop-work #1 picks Option 1b or 1c); opaque byte-pumping is a ~150-LOC component.
- *Con:* single-process proxy is the latency bottleneck; p99 under 30-concurrent-session load is unmeasured.
- *Con:* back-pressure on slow consumers risks a cascading stall — one slow browser could stall the bridge-web thread-pool.

**Option 2b — Dedicated Yarp gateway project (`Sunfish.Bridge.Gateway`).**
New `Microsoft.NET.Sdk.Web` project using Microsoft's Yarp reverse-proxy middleware. Gateway terminates TLS + WebSocket, routes by subdomain to tenant-child `/ws`. `bridge-web` shrinks to the SaaS control plane + browser-shell UI host.

- *Pro:* Yarp is production-grade: built-in backpressure handling, connection pooling, Channel-based duplex pumping.
- *Pro:* gateway is horizontally scalable independently of `bridge-web`.
- *Con:* +3-5 days engineering, +1 deployable to operate.
- *Con:* overkill for the 4-client launch posture.

**Option 2c — Direct browser → tenant-child connection (no proxy).**
Each tenant-child Kestrel is directly reachable from the public internet at a per-tenant TLS SNI. Browser opens WSS directly.

- *Pro:* zero proxy latency.
- *Con:* requires a per-tenant TLS cert (or wildcard SAN that includes each tenant slug), DNS SAN per tenant, public network exposure of every tenant-child, per-tenant firewall rule. Operationally untenable at any real scale.
- *Con:* loses the central auth cookie boundary — each tenant-child has to re-implement cookie validation.
- **Rejected** as operationally impractical.

### Stop-work #3 — Render mode (coupled to #1)

**Option 3a — Blazor Server (ADR 0031 literal).**
Pure `AddInteractiveServerRenderMode()`. Entire UI + CRDT mirror + key handling server-side.

- *Pro:* simplest; fastest to ship.
- *Pro:* matches ADR 0031 text verbatim.
- *Con:* forces stop-work #1 to Option 1a (accept server-circuit key custody).

**Option 3b — Blazor WASM pure.**
`AddInteractiveWebAssemblyRenderMode()`. App shell downloads WASM runtime; everything runs in browser.

- *Pro:* forces stop-work #1 to Option 1b (pure-browser keys).
- *Con:* +2 weeks; loses server-side observability; slower first paint.

**Option 3c — `InteractiveAuto` (hybrid).**
`AddInteractiveAutoRenderMode()` on the root; per-component render-mode directives scope the key-handling path to WASM-only.

- *Pro:* enables Option 1c for stop-work #1.
- *Con:* render-mode-per-component adds author-time complexity.

---

## Decision (recommended)

### #1 — **Option 1c (Hybrid: key handling in WASM, live UI in Server).**

The paper's §11.2 invariant governs *key material*, not transient rendered state. The operator holding a decrypted CRDT snapshot in circuit RAM for a few seconds during render is materially different from the operator holding the passphrase-derived device key. The former is observable only by a live, in-process attacker; the latter is reusable, persistent, and enables offline re-decryption of any captured ciphertext.

The hybrid posture honours the invariant where it matters most (keys) and accepts a defensible compromise where it matters less (rendered state). The explicit trust-level statement becomes: **"Operator can see live session state during circuit lifetime; operator cannot see the device key, role keys, or passphrase."** This is stronger than Option 1a and ships in half the time of Option 1b.

### #2 — **Option 2a (`bridge-web` as reverse proxy for v1).**

Ship the hand-rolled `TenantWebSocketReverseProxy` inside `bridge-web`. Gate the v1 release on a load-test in W5.3.C: 30 concurrent browser sessions × 3 tenants × realistic delta-stream traffic. Pass criteria: p99 handshake < 2s, p99 frame latency < 500ms, zero slow-consumer-induced stalls over a 10-minute run.

If the load test fails, escalate to Option 2b (Yarp gateway) under a new ADR 0034. Defer the escalation decision until we have numbers — not before.

### #3 — **Option 3c (`InteractiveAuto`).**

Adopt `InteractiveAuto` render mode on the browser-shell root. Enforce a hard rule: any component that touches `device_key`, role keys, passphrase material, or WebSocket HELLO-signing must be annotated `@rendermode InteractiveWebAssembly` and must not take a server-only `[Inject]` dependency. All other components default to the auto mode.

`PassphraseLoginPage.razor` becomes WASM-exclusive; `BrowserSyncClient` (the WebSocket orchestrator that needs to sign HELLO) becomes WASM-exclusive; `IBrowserDeviceKeyStore` lives only in the WASM DI scope. `DashboardPage.razor` and the CRDT mirror view components render Server. The `IBrowserCrdtMirror` is *populated* in WASM (it holds decrypted state and role keys briefly to apply remote deltas) then published to Server via a JS-interop-bridged snapshot that carries already-applied state (no keys) back to Server for rendering.

---

## Consequences

### Positive

- **Paper §11.2 invariant preserved for the key-handling path.** Device key, role keys, and Ed25519 signers never exist in server-process memory.
- **Fast first paint retained.** The Server-rendered dashboard view lands at the same latency as today's Bridge pages.
- **Observability retained where it's useful.** Server-side logs cover subdomain resolution, cookie auth, WebSocket reverse-proxy handshake, tenant-child health — the operational surface that Ops needs. Key derivation and delta application run in the browser; if those fail, the browser console is the diagnostic surface (documented).
- **Trust-level differentiation becomes explicit and accurate.** The "Relay-only" trust level is compatible with the browser shell v1 provided the user accepts "operator sees live session state in render, not keys." Tenants who need stricter posture choose "No hosted peer" (Anchor only) or Option B (dedicated deployment).
- **Reverse-proxy escalation is deferred with a measurable gate**, not a gut call.
- **ADR 0031's wire protocol is unchanged** — this is a render-mode layering decision, not a protocol change.

### Negative

- **Two render modes in one app surface** raises the authoring bar. Every new component has to decide (or inherit) Server vs. WASM. Mis-annotating a key-handling component fails-open — the key leaks into server memory without a build error. Mitigations: a WASM-only DI container for key types (so a Server component that tries to `[Inject] IBrowserDeviceKeyStore` gets a DI resolution failure at render time); code-review checklist in PLATFORM_ALIGNMENT; explicit tests.
- **WASM bundle ships Argon2 + Ed25519 + HKDF + role-key cache** — ~400KB compressed. First-session latency increases by ~500ms-1s on slow connections. Acceptable for v1; revisit if telemetry says otherwise.
- **CRDT mirror state is on Server for rendering**, which means a live attacker with circuit-memory access reads decrypted session data. This is a deliberate trade-off: rendered state is ephemeral and bucket-scoped, whereas keys would be reusable. Users who can't accept it go Anchor or Option B.
- **The `bridge-web` reverse proxy is single-process** — if load test reveals a bottleneck, ADR 0034 escalates to Yarp, which is extra work. Acceptable because we have a measurable escalation trigger.
- **`InteractiveAuto` + WASM requires a Blazor-WASM SDK project** in `accelerators/bridge/`. Today's tree has none. W5.3.B will add `Sunfish.Bridge.BrowserShell.Client` as a sibling `Microsoft.NET.Sdk.BlazorWebAssembly` project.

### Neutral / upgrade path

- **v2 can tighten to pure WASM without breaking the wire.** If a future regulated-industry tier demands operator cannot see rendered state, the CRDT mirror view can migrate to WASM components one at a time. Same protocol; no tenant-side migration.
- **Option B (dedicated deployment) is unchanged.** Operators of dedicated-deployment customers can pick either shared or dedicated render modes; the per-tenant data-plane isolation is a stronger guarantee than either render-mode choice.

---

## Compatibility plan

### Relation to ADR 0031

- **ADR 0031 is amended, not superseded.** The single paragraph describing "new Blazor Server app" is refined by this ADR. The Zone-C Hybrid decision, the three trust levels (Relay-only / Attested hosted peer / No hosted peer), the per-tenant data-plane isolation, and all other ADR 0031 decisions stand unchanged.
- **Trust-level semantics updated.** The "Relay-only" trust level's meaning gains a sub-note: *"Browser-shell v1 users additionally expose decrypted session state to the operator during render-circuit lifetime. Device keys and role keys remain browser-only."* Anchor users and "No hosted peer" users are unaffected.
- **Update checklist (post-acceptance):** Add a pointer from ADR 0031's "Browser shell (new)" sub-section to this ADR. Update `accelerators/bridge/PLATFORM_ALIGNMENT.md` trust-level table with the new browser-shell row.

### Relation to W5.3 decomposition

- **W5.3.A (subdomain middleware)** — already landed; unaffected.
- **W5.3.B (passphrase auth)** — this ADR pins the render mode for the auth sub-tree to WASM. `PassphraseLoginPage.razor` + `SessionStorageDeviceKeyStore.cs` + `argon2-bridge.js` live in a new Blazor-WASM project (`Sunfish.Bridge.BrowserShell.Client`). Dispatch can proceed once this ADR is Accepted.
- **W5.3.C (WebSocket transport)** — the reverse-proxy half lives in `bridge-web` (Server-side). The transport adapter half (`WebSocketSyncDaemonTransport`) on `local-node-host` is unchanged. Load-test gate added: 30-session × 3-tenant × 10-min run; pass criteria in §"Decision" #2.
- **W5.3.D (ephemeral browser node)** — the CRDT mirror populates in WASM, renders in Server. New scoped-service bridge pattern: a WASM service publishes decrypted read-snapshots to a Server-side `ICrdtReadSnapshotStore`; Server-side components bind to that. Writes flow the other direction: Server UI emits an intent → Server-side op-queue → JS interop publishes to WASM → WASM signs + submits via WebSocket.
- **W5.3.E (project + E2E)** — adds the `Sunfish.Bridge.BrowserShell.Client` WASM project as a sibling to the Server project. AppHost composition extended to serve both from the same tenant subdomain.
- **W5.4 (founder + joiner flows)** — founder bundle generation and QR-paste paths also WASM-side. Unchanged in shape; pinned by this ADR.

### Relation to Option B (dedicated deployment)

Dedicated-deployment customers (Wave 5.5 IaC) inherit the same render-mode stack. Nothing to change there — they get the same browser shell, just on isolated infrastructure.

---

## Open questions (deferred)

1. **Threat model of WASM-side memory.** An XSS injection still reads `device_key` from the WASM heap. What's the browser-memory hardening posture? Deferred to a security-review ADR; CSP (`script-src 'self' 'wasm-unsafe-eval'`) + sub-resource-integrity on all JS + refusal of inline event handlers is the v1 baseline.
2. **Server-side observability for the encrypted WebSocket path.** How do we diagnose "my delta didn't apply" when the server sees ciphertext only? OpenTelemetry counters on frame count + frame size + handshake success/failure are a starting point, but root-cause investigation needs browser-console capture. Decide the browser-telemetry posture (Sentry vs. in-tree logger → POST-back) in a later ADR.
3. **Auto-mode fallback behavior when WASM fails to load.** `InteractiveAuto` falls back to Server if WASM is unavailable (disabled browser, ancient iOS). For the key-handling path this MUST fail closed, not fall back. Implementation must explicitly disable Server fallback for `@rendermode InteractiveWebAssembly` components — verify this behavior against Blazor 8+ docs in W5.3.B.
4. **Render-mode-per-component lint rule.** Should we ship a Roslyn analyzer that flags any Server component taking `[Inject] IBrowser{Device,RoleKey}*`? Useful guard-rail; defer to post-v1 tooling.
5. **Bundle-size budget.** v1 ships ~400KB of WASM. Is there a hard cap? Telemetry-informed; revisit when we have real first-session-latency data.

---

## Implementation checklist

Items below are one-line tasks; W5.3.B through W5.3.E will satisfy them.

- [ ] Create `accelerators/bridge/Sunfish.Bridge.BrowserShell.Client/Sunfish.Bridge.BrowserShell.Client.csproj` — `Microsoft.NET.Sdk.BlazorWebAssembly`.
- [ ] Create `accelerators/bridge/Sunfish.Bridge.BrowserShell/Sunfish.Bridge.BrowserShell.csproj` — `Microsoft.NET.Sdk.Web`; references the Client project.
- [ ] Wire `InteractiveAuto` render mode in `Sunfish.Bridge.BrowserShell/Program.cs`.
- [ ] Author `PassphraseLoginPage.razor` with `@rendermode InteractiveWebAssembly`.
- [ ] Author `IBrowserDeviceKeyStore` in the `.Client` project only; verify a `[Inject]` from a Server component fails DI resolution.
- [ ] Ship `hash-wasm` Argon2 + `@noble/ed25519` JS modules vendored under `.Client/wwwroot/js/`.
- [ ] Implement `TenantWebSocketReverseProxy` in `Sunfish.Bridge` (Server-side); opaque byte-pump over `ClientWebSocket`.
- [ ] Implement `WebSocketSyncDaemonTransport` in `packages/kernel-sync/Protocol/` (server-side; accepts from `bridge-web`).
- [ ] Author `HostedWebSocketEndpoint` on `local-node-host` (parallel to `HostedHealthEndpoint`); share `WebApplication`.
- [ ] Author `IBrowserCrdtMirror` in `.Client` (WASM; holds decrypted state + role keys); author a Server-side `ICrdtReadSnapshotStore` that mirrors read-only snapshots across the interop bridge.
- [ ] Author `BrowserSyncClient` in `.Client` — orchestrates WS open, HELLO signing, delta pump, writes. `@rendermode InteractiveWebAssembly`.
- [ ] Add load-test harness to `tests/Sunfish.Bridge.Tests.Integration/Wave53/ReverseProxyLoadTest.cs`: 30 sessions × 3 tenants × 10 min. Emit p50/p95/p99 handshake + frame latencies.
- [ ] Decision gate at W5.3.C completion: if load test fails thresholds, open ADR 0034 (Yarp gateway escalation); do not ship v1.
- [ ] Update `docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md` "Browser shell (new)" sub-section with a pointer to this ADR.
- [ ] Update `accelerators/bridge/PLATFORM_ALIGNMENT.md` trust-level table: Relay-only + browser-shell row explicitly lists the rendered-state caveat.
- [ ] Update `accelerators/bridge/README.md` — describe the InteractiveAuto posture and the WASM-only key-handling rule.
- [ ] CSP header on the browser-shell response: `default-src 'self'; connect-src 'self' wss://{slug}.sunfish.example.com; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'`.
- [ ] Document in `_shared/product/paper-alignment-plan.md` that W5.3 proceeds under ADR 0033.

---

## References

- [ADR 0031 — Bridge as Hybrid Multi-Tenant SaaS](./0031-bridge-hybrid-multi-tenant-saas.md) — this ADR amends the "Browser shell (new)" sub-section.
- [ADR 0032 — Multi-Team Anchor](./0032-multi-team-anchor-workspace-switching.md) — the Zone-A client-side counterpart; unaffected by this ADR.
- [Wave 5.3 decomposition plan](../../_shared/product/wave-5.3-decomposition.md) — the source of the three stop-works this ADR resolves; §§2.2, 2.3, 6, 9.
- [Paper v12.0 §11.2](../../_shared/product/local-node-architecture-paper.md#112-defense-in-depth) — ciphertext-at-rest invariant.
- [Paper v12.0 §17.2](../../_shared/product/local-node-architecture-paper.md#172-managed-relay-as-sustainable-revenue) — hosted-relay-as-SaaS-node.
- [Paper-alignment audit EOD 2026-04-23](../../icm/07_review/output/paper-alignment-audit-2026-04-23-eod.md) §5, §6, §8 — recommended next dispatch.
- [Aspire 13.2 runtime resource-mutation research](../../_shared/research/aspire-13-runtime-resource-mutation.md) — context for the `bridge-web` reverse-proxy posture.
- [`accelerators/bridge/Sunfish.Bridge/Middleware/TenantSubdomainResolutionMiddleware.cs`](../../accelerators/bridge/Sunfish.Bridge/Middleware/TenantSubdomainResolutionMiddleware.cs) — W5.3.A landed; this ADR builds on it.

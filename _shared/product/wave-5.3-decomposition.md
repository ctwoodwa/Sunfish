# Wave 5.3 — Bridge Browser Shell v1: Decomposition Plan

**Status:** Draft | **Date:** 2026-04-23
**Source:** Plan-agent output, 2026-04-23 dispatch. Mirrors `wave-5.2-decomposition.md` §-structure.

---

## 1. Current State Audit

### 1.1 `accelerators/bridge/Sunfish.Bridge/` today (SaaS posture)

Single `Microsoft.NET.Sdk.Web` project. `Program.cs:34-106` is a classic Blazor Server composition root:

| Concern | Today |
|---|---|
| Project SDK | `Microsoft.NET.Sdk.Web`; companion `Sunfish.Bridge.Client` is `Microsoft.NET.Sdk.Razor` with `FrameworkReference Microsoft.AspNetCore.App`. No Blazor-WASM SDK anywhere in the repo. |
| Render mode | `app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddAdditionalAssemblies(typeof(Sunfish.Bridge.Client._Imports).Assembly)` — pure Blazor Server (circuit-based; no WASM runtime). |
| Auth | `app.UseAuthorization()` wired; `builder.Services.AddAuthorization()` with no policies; no `AddAuthentication()` call in the SaaS branch. `MockOktaService` is an Aspire-registered OIDC mock but no `AddOpenIdConnect` in `Sunfish.Bridge/Program.cs`. |
| Tenant resolution | `ITenantContext` → `DemoTenantContext` (scoped), registered only in `Development`. No subdomain / host-header inspection. |
| SignalR | `app.MapHub<BridgeHub>("/hubs/bridge")` with Redis backplane. **No WebSocket endpoint other than SignalR.** |
| Client (Razor) pages | `Sunfish.Bridge.Client/Pages/`: `Home`, `Board`, `Budget`, `Risk`, `Tasks`, `Team`, `Timeline`, `Account/`, `NotFound`. PM demo surface — unrelated to paper-aligned kernel. |
| CORS | `builder.Services.AddCors()` with **no policies**. |
| wwwroot | Client assets only (app.css, favicons, site.webmanifest). No `js/` loaders for WASM. |

**Implication:** the browser shell v1 is net-new. There is no existing passphrase UI, no WebSocket endpoint other than SignalR, no subdomain middleware, no Argon2 in the browser. `Sunfish.Bridge.Client` exists as a Razor Class Library referenced by bridge-web; 5.3 may extend it or ship a new sibling project.

### 1.2 Wave 5.2 orchestration surfaces already shipped

| File | Role for 5.3 |
|---|---|
| `Sunfish.Bridge/Services/TenantRegistry.cs` | `GetByIdAsync(Guid)`, `GetBySlugAsync(string)`, `TeamPublicKey` column on `TenantRegistration`. 5.3 tenant resolution + auth both consume this. |
| `Sunfish.Bridge/Orchestration/ITenantEndpointRegistry.cs` | `TryGet(Guid, out Uri)` — in-memory mapping tenant → hosted-node `/health` URI. 5.3 WebSocket proxy reuses this to route browser traffic to the right child. |
| `Sunfish.Bridge/Orchestration/ITenantProcessSupervisor.cs` | `StartAsync/PauseAsync/StopAndEraseAsync`, state machine. 5.3 gates subdomain access on `Running`. |
| `Sunfish.Bridge/Orchestration/TenantHealthMonitor.cs` | `/health` poller; publishes `TenantHealthEvent`. 5.3 uses it to fail-fast on unhealthy tenants during handshake. |
| `Sunfish.Bridge.AppHost/Program.cs:63-75` | `bridge-web` project; 5.3 either extends this project OR adds `bridge-browser` as a sibling. Decision §4 below. |

### 1.3 `apps/local-node-host/` hosted-node surface today

| Surface | State |
|---|---|
| `Health/HostedHealthEndpoint.cs` | Wave 5.2.D `IHostedService` wraps a minimal Kestrel `WebApplication`; exposes `GET /health` only. Kestrel instance is already there — **5.3 adds `/ws` on the same `WebApplication` by calling `_app.UseWebSockets() + _app.Map("/ws", ...)` inside a new hosted service paralleling `HostedHealthEndpoint`**. |
| `LocalNodeOptions` | `NodeId`, `TeamId`, `DataDirectory`, `MultiTeam.*`, `HealthPort`. No `BrowserWebSocket.*` sub-section. |
| Sync-daemon transport | `Sunfish.Kernel.Sync.Protocol.UnixSocketSyncDaemonTransport` ships (Wave 2.1 landed, provisional). CBOR envelope + 4-byte length prefix on every frame. **Cannot run over raw TCP from a browser** — browsers cannot open UDS / named pipes; this is the core constraint. |
| Relay | `Sunfish.Bridge/Relay/RelayServer.cs` team-id fan-out via handshake `Granted` stream. Relay speaks the kernel-sync CBOR protocol, not a browser-friendly WebSocket. |

**Critical:** browsers cannot natively speak the sync-daemon's UDS/named-pipe framing. 5.3 needs a new WebSocket-framed variant of the gossip protocol, NOT a reuse of `UnixSocketSyncDaemonTransport`.

### 1.4 Crypto primitives already available

| Primitive | Package | Notes |
|---|---|---|
| Argon2id (server-side) | `packages/foundation-localfirst/Encryption/Argon2idKeyDerivation.cs` | Server-side only, uses Konscious NuGet. Cannot be called from the browser; browser needs a WASM-side Argon2 (argon2-browser, hash-wasm, or a Blazor-WASM package). |
| Keystore abstraction | `packages/foundation-localfirst/Encryption/IKeystore.cs`, `WindowsDpapiKeystore` | OS keystore adapter; irrelevant client-side. |
| HKDF subkey derivation | Wave 6.2 (`packages/kernel-security/Keys/`) | Tenant=team mapping from 5.2 means tenant's team public key is already HKDF-derived on the server side. |

### 1.5 ADR 0031 verbatim locks for 5.3

> "New Blazor Server app at each tenant's subdomain. User authenticates, browser fetches wrapped role-key bundle … decrypts role keys into memory, opens WebSocket to the tenant's hosted-node peer, reads/writes via CRDT ops decrypted in-browser. Session keys wiped on tab close / logout. No persistent browser local-node in v1."
> — ADR 0031, "Browser shell (new)" sub-section

> "Browser key bootstrap: passphrase-derived device key as the default flow, with WebAuthn as an opt-in hardening option and QR-from-phone as the fallback … Passphrase is friction-tolerable for SMB SaaS"
> — ADR 0031, Decisions embedded #2

> "Single domain per tenant (`{tenant}.sunfish.example.com`) for the browser shell; operator admin at `admin.sunfish.example.com`."
> — ADR 0031, Decisions embedded #6

These three statements are load-bearing: any sub-task that would contradict them must be flagged.

---

## 2. Target State per Concern

### 2.1 Subdomain routing

| Concern | Target |
|---|---|
| URL shape | `{tenant-slug}.sunfish.example.com` per ADR 0031 #6. Operator admin at `admin.sunfish.example.com` (out-of-scope for 5.3; called out in §9). |
| Resolution | New middleware `TenantSubdomainResolutionMiddleware` reads `HttpContext.Request.Host.Host`, strips the root zone, treats the left-most label as `slug` (reserved: `admin`, `www`, `auth`). Looks up via `ITenantRegistry.GetBySlugAsync(slug)`; 404/410 if `Pending` / `Cancelled` / missing; 503 if `Suspended`. |
| Context carrier | New `IBrowserTenantContext` (scoped) populated from middleware. Holds `TenantId`, `Slug`, `TrustLevel`, `TeamPublicKey`. Supersedes the SaaS `ITenantContext` for browser-shell request scope only. |
| Endpoint lookup | Once `TenantId` known, resolve hosted-node URI via `ITenantEndpointRegistry.TryGet(TenantId, out uri)`. Null endpoint → 503 "starting". Used both for the WebSocket reverse-proxy and for any in-browser REST metadata calls. |
| Local dev | `.localhost` subdomains resolve without hosts-file edits on modern browsers (Firefox, Chromium). Aspire `bridge-web` listens on an ephemeral port; dev uses `acme.localhost:PORT`. |
| Prod | Wildcard cert `*.sunfish.example.com`; terminated at upstream (Azure Front Door / nginx) — bridge-web sees X-Forwarded-Host. Middleware prefers `X-Forwarded-Host` when `ForwardedHeadersOptions.KnownProxies` matches. |

### 2.2 Passphrase-derived device-key auth

| Step | Location | Notes |
|---|---|---|
| 1. User types passphrase | Browser `<input type=password>` in `PassphraseLoginPage.razor` | Never POSTed to the server. |
| 2. Fetch per-tenant salt | `GET /auth/salt?slug={slug}` (server endpoint; returns constant 16-byte per-tenant salt stored on `TenantRegistration.AuthSalt` — NEW column) | Salt is non-secret; required to make Argon2 deterministic. |
| 3. Argon2id in browser | WASM (argon2-browser or hash-wasm). Params: m=256MiB, t=3, p=1, out=32 bytes. Output = `device_key`. | Runs in a Web Worker to keep the UI thread responsive. |
| 4. Challenge-response | `POST /auth/challenge` → server returns `challenge` (16 random bytes) + `session_id`. Browser signs `challenge` with an **Ed25519 keypair derived from device_key via HKDF("sunfish-browser-auth-v1")**, then `POST /auth/verify { session_id, pubkey, signature }`. Server verifies the signature AND checks `pubkey == TenantRegistration.TeamPublicKey` (the tenant's root team key that Wave 5.4 writes at founder-flow completion). | Server stamps a short-lived browser-session cookie tied to `TenantId`. |
| 5. WebSocket handshake | Browser opens `wss://{slug}.sunfish.example.com/ws`, sending the browser-session cookie. Server's reverse-proxy uses the cookie to resolve tenant and forwards to `ITenantEndpointRegistry` URI. Then at the kernel-sync handshake layer, the hosted-node peer re-validates by requiring the browser to sign the daemon-level HELLO nonce with the same Ed25519 key. | Double-layered: HTTP session + daemon-level signature. |
| 6. Tab-close cleanup | `sessionStorage` (NOT `localStorage`); `beforeunload` handler clears keys; `sessionStorage` is per-tab by design. | Browser memory is best-effort; session cookie is `SameSite=Strict; Secure; HttpOnly; Max-Age=3600`. |

**Enforcement that "key never leaves the browser":** CSP header `connect-src 'self' wss://{slug}.sunfish.example.com; script-src 'self' 'wasm-unsafe-eval'`. Server never exposes an endpoint that accepts raw `device_key` — only signed challenges and signed daemon frames. A code-review gate must reject any `fetch('/auth/...', { body: device_key })` pattern. Threat model note: XSS still extracts memory; CSP + no-inline-script reduces blast radius but does not eliminate it.

### 2.3 Ephemeral in-memory browser node

| Concern | Target |
|---|---|
| Plugin subset | Only `kernel-crdt` (decode/apply remote ops to in-memory state) + `kernel-event-bus` read-model projector. No `kernel-sync` daemon (server-side peer does gossip; browser is a thin terminal on it). No `foundation-localfirst` (no SQLCipher in browser v1). No `kernel-lease` (leases require daemon roundtrip; the hosted-node peer takes leases on behalf of the browser). |
| Session open | Browser sends `HELLO` over WebSocket. Hosted-node responds with `CAPABILITY_NEG` + initial `DELTA_STREAM` of the team's current CRDT state (bounded by bucket subscription). Browser decrypts deltas with role keys derived from `device_key`. |
| Ongoing | Browser subscribes to `GOSSIP_PING` / `DELTA_STREAM` fan-out. UI components render from an in-memory CRDT snapshot. User writes = local CRDT op applied optimistically + `DELTA_STREAM` to hosted node. |
| Tab close | `sessionStorage` keys cleared in `beforeunload`; WebSocket closes; hosted-node peer marks the browser session `gone`. No unsaved deltas persist client-side — any write not ACKed by the hosted node is lost. v1 documents this; v2 (OPFS opt-in) is the path forward (deferred per ADR 0031 open question #1). |
| Storage budget | In-memory cap (per `IStorageBudgetManager` analogue). 5.3 ships a fixed 64 MiB budget; if exceeded, oldest buckets get LRU-evicted and refetched on demand. |

### 2.4 WebSocket protocol surface

| Frame | Direction | Shape |
|---|---|---|
| `HELLO` | Browser → Hosted-node | Same CBOR body as `packages/kernel-sync/Protocol/Messages.cs:MessageTypes.Hello`, transported inside a WebSocket binary frame instead of UDS/length-prefix. |
| `CAPABILITY_NEG` / `ACK` | Hosted-node → Browser | Per spec §3. |
| `DELTA_STREAM` | Bidirectional | Browser receives encrypted bucket deltas; decrypts via in-memory role keys. |
| `GOSSIP_PING` | Hosted-node → Browser | Liveness; browser responds to keep hosted-node aware of session. |
| `ERROR` | Either | Recoverable vs non-recoverable; non-recoverable closes WS. |

**Framing:** one WebSocket binary message = one CBOR envelope. Drops the 4-byte length prefix because WebSocket already frames. Adapter: `packages/kernel-sync/Protocol/WebSocketSyncDaemonTransport.cs` (NEW) — `ISyncDaemonTransport` implementation parallel to `UnixSocketSyncDaemonTransport`, hosted-node side. Browser side: a handwritten JS/Blazor WebSocket client (`Sunfish.Bridge.BrowserShell/wwwroot/js/sync-daemon-ws.js` + a C# Interop wrapper, OR a `System.Net.WebSockets.ClientWebSocket` running inside a Blazor-Server circuit that proxies to the hosted-node child — see §4 decision).

**Relation to Wave 2.1:** Wave 2.1 sync-daemon transport has landed (`UnixSocketSyncDaemonTransport`) per `packages/kernel-sync/Protocol/`. The CBOR envelope + message catalogue are reusable. **5.3 does NOT block on "Wave 2.1 landing"** — the adapter is a new transport alongside, not a replacement. (Corrects the Wave 5.2 §8 stop-work #2 narrative: 2.1 landed for local transports; what 5.3 adds is a browser-friendly transport on the same envelope spec.)

---

## 3. Sub-Task Breakdown (5 agents, DAG)

### DAG shape

```
5.3.A (Subdomain routing + BrowserTenantContext + auth-salt column)
    │
    ├─▶ 5.3.B (Passphrase-derived auth: server endpoints + WASM Argon2 + challenge/verify)
    │       │
    │       └─▶ 5.3.E (Bridge.BrowserShell project + E2E smoke test)
    │
    ├─▶ 5.3.C (WebSocketSyncDaemonTransport + local-node-host /ws endpoint)
    │       │
    │       └─▶ 5.3.D (Ephemeral in-memory browser node: CRDT apply + bucket render)
    │                 │
    │                 └─▶ 5.3.E
    └─────────────────▶ 5.3.E
```

Tiers: 1 = A. 2 = B ‖ C. 3 = D. 4 = E.

### 5.3.A — Subdomain routing + `BrowserTenantContext` + `AuthSalt` column

**Scope:** Middleware + DB-column addition. Unblocks B, C, D by giving every downstream request a resolved `TenantId`.

**Files touched:**
- `Sunfish.Bridge/Middleware/TenantSubdomainResolutionMiddleware.cs` (NEW).
- `Sunfish.Bridge/Middleware/IBrowserTenantContext.cs` + `BrowserTenantContext.cs` (NEW; scoped).
- `Sunfish.Bridge.Data/Entities/TenantRegistration.cs` (EXTEND) — add `byte[] AuthSalt` (16 bytes, unique-per-tenant, generated at `CreateAsync` time). **Requires EF migration** — author in the same sub-task to keep schema-change atomic.
- `Sunfish.Bridge/Services/TenantRegistry.cs` (EXTEND) — populate `AuthSalt = RandomNumberGenerator.GetBytes(16)` in `CreateAsync`.
- `Sunfish.Bridge/Program.cs` (EXTEND) — register middleware in pipeline between `UseHttpsRedirection` and `UseAuthorization`; register `IBrowserTenantContext`.
- `tests/Sunfish.Bridge.Tests.Unit/Middleware/TenantSubdomainResolutionMiddlewareTests.cs` (NEW) — happy path, reserved-slug rejection, suspended-503, cancelled-410, X-Forwarded-Host honoured.

**Effort:** ~6-8 hours.

**Test strategy:** Unit-level middleware tests using `DefaultHttpContext` with synthetic hosts. Integration: extend `Sunfish.Bridge.Tests.Integration/Wave52/ThreeTenantSmokeTest.cs` to assert `GET acme.localhost:{port}/` resolves the `acme` tenant, `GET nonexistent.localhost:{port}/` returns 404.

**Risks:** reserved-slug list drift (`admin`, `www`, `auth`, `api`); X-Forwarded-Host spoofing if `KnownProxies` isn't set — document the deployment requirement.

### 5.3.B — Passphrase-derived auth (server endpoints + client-side Argon2)

**Scope:** `/auth/salt`, `/auth/challenge`, `/auth/verify` server endpoints; WASM-side Argon2 + Ed25519 signature. Depends on 5.3.A for `BrowserTenantContext`.

**Files touched:**
- `Sunfish.Bridge/Auth/PassphraseAuthEndpoints.cs` (NEW) — minimal-API `MapGroup("/auth")` with three endpoints. Validates `challenge` signed by a pubkey matching `TenantRegistration.TeamPublicKey`. Issues `BrowserSessionCookie` (JWT or opaque; signed with data-protection key; contains `TenantId`, `PublicKeyFingerprint`, 1h expiry).
- `Sunfish.Bridge/Auth/BrowserSessionCookieAuthenticationHandler.cs` (NEW) — `AuthenticationHandler<BrowserSessionCookieOptions>` that resolves the cookie into an `AuthenticationTicket` with `NameIdentifier = {TenantId}:{PublicKeyFingerprint}`.
- `Sunfish.Bridge/Program.cs` (EXTEND) — `AddAuthentication().AddScheme<BrowserSessionCookieOptions, BrowserSessionCookieAuthenticationHandler>("browser-shell")`.
- `Sunfish.Bridge.BrowserShell/wwwroot/js/argon2-bridge.js` (NEW) — thin JS loader for `hash-wasm`'s Argon2id; runs inside a dedicated Web Worker (`argon2-worker.js`). `hash-wasm` is preferred over `argon2-browser` because it's MIT-licensed, WASM-first, and actively maintained.
- `Sunfish.Bridge.BrowserShell/wwwroot/js/ed25519-sign.js` (NEW) — uses `@noble/ed25519` (browser-built, pure JS). Signs challenge, returns hex pubkey + signature.
- `Sunfish.Bridge.BrowserShell/Pages/PassphraseLoginPage.razor` (NEW) — UI + `[JSInvokable]` callbacks to orchestrate salt-fetch → Argon2 → sign → verify.
- `Sunfish.Bridge.BrowserShell/Services/IBrowserDeviceKeyStore.cs` + `SessionStorageDeviceKeyStore.cs` (NEW) — scoped service that holds `device_key` and derived keys in-memory for the Blazor Server circuit lifetime. **Never serializes to disk.**
- `tests/Sunfish.Bridge.Tests.Unit/Auth/BrowserSessionCookieTests.cs` (NEW).
- `tests/Sunfish.Bridge.Tests.Integration/Wave53/PassphraseAuthE2ETests.cs` (NEW) — Playwright (already available? if not, WebApplicationFactory + Konscious server-side to simulate client).

**Effort:** ~14-18 hours (WASM integration + cross-runtime signature verification is the risk).

**Risks:** CSP `wasm-unsafe-eval` required for Argon2 WASM — document and audit. Circuit-lifetime storage may leak keys across tabs sharing a circuit if multiple browser tabs reconnect to the same circuit (Blazor Server reconnection semantics); mitigate by scoping the circuit per tab via URL-specific `RenderMode` options.

### 5.3.C — `WebSocketSyncDaemonTransport` + `/ws` endpoint in local-node-host

**Scope:** Extend `apps/local-node-host` to accept browser WebSocket connections on the same Kestrel instance that serves `/health`. New transport is `ISyncDaemonTransport` implementation. Depends on 5.3.A (for tenant-scoped routing) and on Wave 5.2.D's `HostedHealthEndpoint` pattern.

**Files touched:**
- `packages/kernel-sync/Protocol/WebSocketSyncDaemonTransport.cs` (NEW) — server-side; accepts WS, wraps the connection as an `ISyncDaemonTransportConnection`. Per-message binary framing; CBOR envelope reused unchanged.
- `apps/local-node-host/Health/HostedWebSocketEndpoint.cs` (NEW) — parallel to `HostedHealthEndpoint`; shares the same `WebApplication` via a refactor. Maps `app.Map("/ws", ...)` that `UseWebSockets`, upgrades, and hands the `WebSocket` to the transport.
- `apps/local-node-host/Health/HostedHealthEndpoint.cs` (REFACTOR) — extract `WebApplication` construction into `SharedHostedWebApp` so `/health` and `/ws` coexist on one Kestrel.
- `apps/local-node-host/LocalNodeOptions.cs` (EXTEND) — add `BrowserWebSocket.Enabled` (default `true`), `BrowserWebSocket.MaxMessageBytes` (default 4 MiB).
- `apps/local-node-host/Program.cs` (EXTEND) — register `HostedWebSocketEndpoint` as `IHostedService`; wire a new `ISyncDaemonTransport` registered against the browser connections (distinct singleton from the `UnixSocketSyncDaemonTransport` for local peers).
- `Sunfish.Bridge/Proxy/TenantWebSocketReverseProxy.cs` (NEW) — middleware on `bridge-web` that intercepts `/ws` after 5.3.A subdomain resolution + 5.3.B cookie auth; looks up `ITenantEndpointRegistry.TryGet(tenantId, out uri)`; upgrades + proxies the WebSocket (no framing rewrite — opaque byte-pump via `ClientWebSocket`).
- `tests/Sunfish.LocalNodeHost.Tests/HostedWebSocketEndpointTests.cs` (NEW).
- `tests/Sunfish.Bridge.Tests.Integration/Wave53/WebSocketReverseProxyTests.cs` (NEW).

**Effort:** ~14-16 hours.

**Risks:** WebSocket framing + CBOR double-framing footguns (one-WS-message-per-CBOR-envelope is the invariant; violating it breaks parsing). Reverse-proxy back-pressure — browser → bridge-web → tenant child has two hops with independent buffers; stop-work candidate if tail latency is unacceptable.

### 5.3.D — Ephemeral in-memory browser node (CRDT apply + bucket render)

**Scope:** Browser-side consumption of the DELTA_STREAM. Thin — only what's needed to render a minimal read view. Depends on 5.3.C.

**Files touched:**
- `Sunfish.Bridge.BrowserShell/Services/IBrowserCrdtMirror.cs` + `InMemoryBrowserCrdtMirror.cs` (NEW) — scoped; holds decrypted CRDT state per subscribed bucket. `Apply(remoteDelta)` + `LocalWrite(op) → Task<ack>`.
- `Sunfish.Bridge.BrowserShell/Services/IBrowserRoleKeyCache.cs` (NEW) — in-memory cache of decrypted role keys (derived from `device_key` via HKDF); cleared on disconnect.
- `Sunfish.Bridge.BrowserShell/Services/BrowserSyncClient.cs` (NEW) — orchestrates: opens WS (via 5.3.C), sends HELLO, pumps deltas into `IBrowserCrdtMirror`, applies optimistic local writes, handles reconnect (transparent to UI).
- `Sunfish.Bridge.BrowserShell/Pages/DashboardPage.razor` (NEW) — minimal read-only view bound to a single test bucket. Proves the full path end-to-end; richer UI deferred.
- `Sunfish.Bridge.BrowserShell/BeforeUnload.razor` (NEW) — `@inject IJSRuntime`; registers `beforeunload` handler that clears the `IBrowserDeviceKeyStore` and the `IBrowserRoleKeyCache`.
- `tests/Sunfish.Bridge.Tests.Integration/Wave53/EphemeralBrowserNodeTests.cs` (NEW) — spawns one tenant child; opens WS; asserts delta roundtrip + tab-close cleanup (disconnect → hosted-node marks session gone).

**Effort:** ~12-14 hours.

**Risks:** CRDT state grows unboundedly if buckets are large — 64 MiB cap + LRU eviction is v1; real-world workloads may exceed. Flag as Wave 5.4+ follow-up.

### 5.3.E — `Sunfish.Bridge.BrowserShell` project + E2E smoke test

**Scope:** Packaging + exit-criterion test. Wires the new project into AppHost and extends the Wave 5.2 three-tenant smoke test to exercise browser-shell login + delta-stream.

**Files touched:**
- `accelerators/bridge/Sunfish.Bridge.BrowserShell/Sunfish.Bridge.BrowserShell.csproj` (NEW) — `Microsoft.NET.Sdk.Web`; or alternatively `Microsoft.NET.Sdk.Razor` integrated into existing `Sunfish.Bridge` (decision §4). **Recommended: separate project** to keep the paper-aligned browser shell distinct from the legacy PM-demo pages.
- `accelerators/bridge/Sunfish.Bridge.BrowserShell/Program.cs` (NEW) — Blazor Server composition root; references `Sunfish.Bridge.csproj` (for `ITenantRegistry`, `ITenantEndpointRegistry`, middleware).
- `Sunfish.Bridge.AppHost.csproj` (EXTEND) — `ProjectReference` to `Sunfish.Bridge.BrowserShell`.
- `Sunfish.Bridge.AppHost/Program.cs` (EXTEND) — `AddProject<Projects.Sunfish_Bridge_BrowserShell>("bridge-browser")` with `WithReference(postgres)` + `WithEnvironment("Bridge__Orchestration__TenantDataRoot", ...)`. Bound to a distinct ingress port with wildcard host routing.
- `Sunfish.Bridge.BrowserShell.slnx` inclusion (EXTEND `Sunfish.Bridge.slnx`).
- `accelerators/bridge/PLATFORM_ALIGNMENT.md` (EXTEND) — add "Browser shell v1" section.
- `tests/Sunfish.Bridge.Tests.Integration/Wave53/BrowserShellThreeTenantE2E.cs` (NEW) — Create 3 tenants (reusing Wave 5.2 factory); for each, navigate to `{slug}.localhost:{port}/login`, enter passphrase, assert WS handshake completes, assert DELTA_STREAM delivers a known value from the hosted-node child. Then `TabClose()` → assert hosted-node reports session-ended.

**Effort:** ~10-12 hours.

**Risks:** Playwright introduction if not already present — check `tests/` for existing browser-test infrastructure before committing to it.

**Total effort:** ~56-68 agent-hours across 5 dispatchable chunks. Consistent with ADR 0031's 2-3 week estimate.

---

## 4. Subdomain Routing Decision

**Decision: middleware lives in `Sunfish.Bridge` (existing project), NOT a separate gateway project.**

**Rationale:**
- `ITenantRegistry` already lives in `Sunfish.Bridge/Services/`; a gateway project would need a ProjectReference to Bridge OR a duplicated registry interface — strictly worse.
- `bridge-web` already terminates TLS (in prod, behind AFD/nginx) and already has the auth pipeline wired. Adding `TenantSubdomainResolutionMiddleware` + `BrowserSessionCookieAuthenticationHandler` extends that pipeline by ~150 LOC.
- The new `Sunfish.Bridge.BrowserShell` project is a **Razor-pages-hosting sibling** that ProjectReferences `Sunfish.Bridge` for shared services. Separating the projects keeps the paper-aligned browser shell's surface small (no Postgres Wolverine DAB baggage) while reusing the tenant-resolution middleware registered in `Sunfish.Bridge`.
- Wave 5.2's `ITenantEndpointRegistry` (in `Sunfish.Bridge/Orchestration/`) is already the source of truth for "which port/process serves tenant X" — the reverse-proxy code belongs next to it, not in a separate gateway.

**Future upgrade path:** If scale demands a dedicated gateway tier, the middleware + cookie auth extract cleanly into `Sunfish.Bridge.Gateway` (new project) that references `Sunfish.Bridge` only for the types. Wave 5.5 (dedicated deployments, already shipped) does not require this split.

**Which auth context carries the tenant id?** The new `IBrowserTenantContext` (scoped). The existing `ITenantContext` / `DemoTenantContext` from the SaaS posture stays for legacy PM-demo Razor pages and SaaS-control-plane concerns per Wave 5.1's scope-narrowing. Two contexts coexist; the middleware sets only the browser-side one. Downstream code MUST NOT mix them — enforced via code-review guidance in PLATFORM_ALIGNMENT.

---

## 5. Passphrase-Derived Device-Key Auth — Spec

### Flow (numbered; steps 3-5 happen entirely client-side)

1. `GET {slug}.sunfish.example.com/login` → `PassphraseLoginPage.razor`.
2. User types passphrase.
3. JS: `fetch('/auth/salt?slug={slug}') → {salt: hex}`. Server reads `TenantRegistration.AuthSalt`.
4. JS Web Worker: `device_key = Argon2id(passphrase, salt, {m:262144, t:3, p:1, out:32})`. Erase passphrase from memory post-derivation.
5. JS: `(pk, sk) = HKDF-Ed25519(device_key, "sunfish-browser-auth-v1")`.
6. JS: `POST /auth/challenge {slug, pk:hex}` → server returns `{challenge: hex(16), session_id}`.
7. JS: `signature = ed25519.sign(challenge, sk)`.
8. JS: `POST /auth/verify {session_id, signature: hex}` — server checks `ed25519.verify(challenge, signature, pk) AND pk == TenantRegistration.TeamPublicKey`. On success, server sets `browser-session` cookie (SameSite=Strict, Secure, HttpOnly, Max-Age=3600).
9. Client redirects to `/app`; BeforeUnload registers; `device_key` stored in scoped Blazor service instance AND sessionStorage (see below for rationale).
10. WS handshake: `new WebSocket('wss://{slug}.sunfish.example.com/ws')` auto-includes the cookie; bridge-web reverse-proxies to the tenant's hosted-node; hosted-node's WebSocketSyncDaemonTransport receives HELLO signed by the same `sk`.

### Enforcement: "key never leaves the browser"

- **Browser-side:** `device_key` is computed in a Web Worker; never placed in a `fetch()` body. The Worker exposes only `deriveAuthKeypair(device_key, label) → pk` and `sign(challenge, sk) → signature`. Raw `device_key` never crosses into a request.
- **Server-side:** No endpoint accepts a raw Argon2 output or passphrase. `/auth/verify` accepts only `{session_id, signature}`; server logs ingest via structured filters reject any payload containing a 32-byte hex that matches known-key shapes (belt-and-braces).
- **CSP:** `Content-Security-Policy: default-src 'self'; connect-src 'self' wss://{slug}.sunfish.example.com; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'`. `wasm-unsafe-eval` is required by hash-wasm.
- **Audit gate:** lint rule or code-review checklist in `.claude/` prohibits any `fetch('/auth/',` with a `device_key` reference in scope.

### Session storage strategy

| Option | Chosen? | Reason |
|---|---|---|
| `localStorage` | No | Survives tab close (v1 must wipe). |
| `sessionStorage` | Yes (belt) | Scoped per-tab, auto-cleared on tab-close, survives page reload. |
| In-memory only (Blazor scoped service) | Yes (braces) | Cleared on circuit dispose. Belt-and-braces with sessionStorage because Blazor circuit reconnection after transient disconnect needs `device_key` reloaded; sessionStorage is the reload source. |

On `beforeunload`, `BeforeUnload.razor` calls `sessionStorage.removeItem('sunfish.device_key')` + dispatches a custom event the Blazor circuit listens to in order to clear the in-memory copy.

### Tab-close cleanup semantics

- `beforeunload` fires → `sessionStorage.clear()` for the Sunfish keys → WS closes → hosted-node's `OnDisconnected` marks session gone → any unACKed local deltas are **lost** (documented in user-facing docs as "v1 is an ephemeral view; make sure writes ACK before closing the tab").

---

## 6. Ephemeral In-Memory Browser Node

### Plugin subset

| Package | Loaded in browser? | Why |
|---|---|---|
| `kernel-crdt` | Yes (subset) | CRDT op apply + conflict resolution. Must transpile to a browser-consumable form OR run fully on the Blazor Server circuit (Server option: simpler; avoids the WASM port). |
| `kernel-event-bus` | No | Event log is hosted-node-side; browser just consumes projected reads. |
| `foundation-localfirst` | No | No SQLCipher in v1. |
| `kernel-sync` | No | Daemon runs on hosted-node; browser is a transport-client. |
| `kernel-lease` | No | Leases through hosted-node on behalf of browser. |
| `kernel-security` subset | Yes (role-key derive only) | `HKDF(device_key, role_label)` to derive read/write keys for decrypting bucket deltas. |

**Decision on where CRDT apply runs:** *Blazor Server circuit* (not WASM). Rationale: ADR 0031 says "new Blazor Server app" — render mode is Server, not WebAssembly. The in-memory CRDT mirror lives in the server-side circuit's scoped DI, not in the browser memory. This trivially satisfies "no persistent browser local-node" (the server discards state on circuit dispose) AND preserves the "key never leaves the browser" invariant because role keys flow over the circuit's SignalR back from browser memory (not server-side stored). **However** this re-introduces a caveat: the server circuit transiently holds decrypted role keys in memory while the circuit is alive. Document this in the trust-level table as: "Attested hosted peer" trust levels are fine; "Relay-only" trust-level tenants need Option B or a v2 WASM renderer. Stop-work §9.

### Session open (initial sync)

1. WS handshake completes.
2. Browser sends `BucketSubscription` intents (e.g. "subscribe to `team-members`, `buckets:public`").
3. Hosted-node responds with one `DELTA_STREAM` per bucket snapshot, encrypted with the bucket's role key.
4. Browser (server circuit) decrypts via `HKDF(device_key, bucket_role_label)`; applies to `InMemoryBrowserCrdtMirror`.
5. UI renders.

### What gets lost on tab close

- Any `DELTA_STREAM` frame authored by the browser but not ACKed by the hosted-node.
- Cached decrypted bucket data (server circuit disposed).
- Role keys derived during the session (not persisted).

v1 trade-off. v2 path: OPFS-backed persistent CRDT state (ADR 0031 open question #1).

---

## 7. WebSocket Protocol Surface

### Handshake

Browser opens `wss://{slug}.sunfish.example.com/ws` carrying cookie. Reverse-proxy forwards opaque WS to hosted-node. Hosted-node side:

```
Browser → Node:    HELLO { peer_node_id, capabilities, proposed_streams, signature_over_nonce }
Node   → Browser:  CAPABILITY_NEG { agreed_version, agreed_ciphers }
Node   → Browser:  ACK { granted_streams, rejections }
```

Identical body semantics to `packages/kernel-sync/Protocol/Messages.cs`. Only transport swaps: WebSocket binary frames, one-message-per-frame (no length prefix because WS already frames). `packages/kernel-sync/Protocol/WebSocketSyncDaemonTransport.cs` (NEW) implements `ISyncDaemonTransport` consuming `System.Net.WebSockets.WebSocket`.

### Delta-stream framing

```
DELTA_STREAM { bucket_id, epoch_id, vector_clock, encrypted_ops[] }
```

Identical to the kernel-sync delta stream; each op is ciphertext. Browser decrypts in-server-circuit. Fan-out to other browsers for the same tenant is handled by the hosted-node peer (the browser connects only to its tenant's child, never to other browsers directly).

### Gossip between browser-node and hosted-node peer

None. The browser is a **thin terminal on the hosted-node**, not a first-class gossip peer. The hosted-node handles gossip with the relay tier + any Anchor installs. Browser subscribes to a subset of delta streams; hosted-node pushes. This is a deliberate narrowing for v1: it means the browser doesn't need leases, vector-clock merges across peers, or bucket evaluators. v2 opens the question of "browser as first-class peer."

### Does this require Wave 2.1 sync-daemon transport?

**No — Wave 2.1 has already landed** (`UnixSocketSyncDaemonTransport` exists in `packages/kernel-sync/Protocol/`, dated Wave 2.1 provisional per its docstring). 5.3 adds a new `WebSocketSyncDaemonTransport` *alongside* the Wave 2.1 transport on the same `ISyncDaemonTransport` contract + same CBOR envelope. **Not a stop-work.** (This updates the Wave 5.2 §8 stop-work #2 narrative, which was cautious pre-5.2-shipping.)

---

## 8. Anti-Pattern Audit (top 3)

1. **#1 Unvalidated assumptions — HIGH:** "Blazor Server circuit is the right place to hold decrypted role keys." This leaks the paper's ciphertext-at-rest invariant from the client into the server-circuit memory, even transiently. Validate with a security review before 5.3.D dispatches. Alternative: pure WASM renderer where role-key cleartext never leaves the browser — significantly more work (new SDK, CRDT port, auth UI re-plumb). Stop-work §9 #1.
2. **#15 Premature precision — MEDIUM:** 64 MiB in-memory cap, 1 h cookie lifetime, 16-byte challenge, Argon2 params (m=256MiB, t=3). Tune in Wave 5.4 after founder-flow E2E. Cite as "v1 defaults, not contractual" in PLATFORM_ALIGNMENT.
3. **#7 Delegation without contracts — MEDIUM:** Three sub-agents (A, B, C) each own a different piece of what the browser/server interface looks like. `IBrowserTenantContext` shape MUST be pinned in 5.3.A with full PR review before B/C/D dispatch. Cookie schema pinned in 5.3.B. WebSocket framing rules pinned in 5.3.C.

---

## 9. Stop-Work Items (for BDFL review)

1. **"Decrypted role keys in Blazor Server circuit" security posture.** ADR 0031's "Relay-only" trust level says operator holds only ciphertext. A Server-render browser shell transiently holds cleartext in server memory during the circuit. This is arguably NOT the paper's invariant. **Recommend:** accept for v1 with explicit caveat on the trust-level table ("Browser shell v1 implies Attested-hosted-peer trust — operator can see cleartext while circuit is live"). A pure-WASM renderer ships as v2. OR: re-dispatch 5.3 with a WASM-only variant; +2 weeks.
2. **Reverse-proxy back-pressure + tail-latency.** Browser → bridge-web → tenant-child is a two-hop WebSocket. Single-process proxy may bottleneck; if p99 handshake > 2s it's unusable. **Recommend:** benchmark in 5.3.C with three concurrent tenants × 10 browser sessions; if fail, escalate to a dedicated Yarp-based gateway (new project). Wave 5.5 dedicated deployments avoid this hop but cost-inefficient.
3. **SPA-vs-Server render mode decision.** ADR 0031 Text: "new Blazor Server app." This implies Server. But §6 decided to honour that by keeping CRDT apply server-side; the security consequence is stop-work #1. Flag this explicitly for BDFL sign-off: "Keep Server rendering per ADR 0031; accept that cleartext lives in server memory during circuit lifetime. Plan v2 pure-WASM renderer."
4. **Cross-origin / CORS.** Browser shell at `{slug}.sunfish.example.com`; WS same origin; `/auth/*` same origin. No cross-origin concerns in v1. If Wave 5.4 founder QR flow involves Anchor at `localhost` calling into `{slug}.sunfish.example.com`, CORS becomes a concern — flag for 5.4 plan.
5. **Wave 2.1 "is it landed?" check.** YES for local transport (`UnixSocketSyncDaemonTransport`). WebSocket variant is new code in 5.3.C; the envelope spec is reused verbatim. Not a blocker.
6. **`hash-wasm` + `@noble/ed25519` vendoring.** Require offline / air-gap friendly vendoring of these JS modules (licenses, SBOM). Likely fine (both MIT) but must land in 5.3.B.
7. **AuthSalt EF migration.** Adding `TenantRegistration.AuthSalt` requires an EF migration. In environments with existing tenants (none at Wave 5.3 dispatch — 5.2 just landed), backfill must generate per-tenant salts during migration. No production impact.

---

## 10. Summary

**Decomposition shape:** 5 sub-agents in 4-tier DAG. Tier 1: 5.3.A (routing + AuthSalt, ~6-8h). Tier 2: 5.3.B (passphrase auth, ~14-18h) ‖ 5.3.C (WebSocket transport, ~14-16h). Tier 3: 5.3.D (ephemeral browser node, ~12-14h). Tier 4: 5.3.E (BrowserShell project + E2E, ~10-12h).

**DAG:** 5.3.A → (5.3.B ‖ 5.3.C) → 5.3.D → 5.3.E.

**Total effort:** ~56-68 agent-hours. Consistent with ADR 0031's 2-3 week estimate.

**Top 3 stop-work items:** (1) decrypted role keys transit Blazor Server memory — violates strict "Relay-only" trust level; (2) reverse-proxy back-pressure unvalidated; (3) Server-vs-WASM render mode requires BDFL sign-off on the security consequence of #1.

**Wave 2.1 dependency:** NO — sync-daemon transport landed for local transports; 5.3 adds a WebSocket transport alongside, not instead.

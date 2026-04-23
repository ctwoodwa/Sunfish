# Paper-Alignment Audit — 2026-04-23 End-of-Day

**Date:** 2026-04-23 (EOD; third refresh of the day)
**Auditor:** Claude Code (Opus 4.7)
**Prior audit:** [`paper-alignment-audit-2026-04-23-post-wave-6-4.md`](./paper-alignment-audit-2026-04-23-post-wave-6-4.md)
**Source of truth:** [`_shared/product/local-node-architecture-paper.md`](../../../_shared/product/local-node-architecture-paper.md) v12.0
**Wave plan:** [`_shared/product/paper-alignment-plan.md`](../../../_shared/product/paper-alignment-plan.md)
**Commit window:** `c9fb8d2`..`HEAD` (31 commits; window starts at the prior-audit parent `ed51ce7`'s predecessor `c9fb8d2`)

Legend: 🟢 aligned • 🟡 partial • 🔴 missing • ⚠ structural conflict • ⚪ out of scope / deferred

---

## 1. Executive Summary

The single biggest delta today: **Zone-A (Anchor multi-team) and Zone-C (Bridge hybrid SaaS) are both architecturally operational end-to-end** — modulo sync-daemon transport advancement (Wave 2.1 variant for browsers) and the three Wave 5.3 stop-works called out below. Wave 6.3 landed in six sub-commits (A/B/C/D/E.1/E.2/F), converting the Wave-6.1 `TeamContext` scaffold from "compiles, doesn't do anything" into a live composition root where `AddSunfishMultiTeam(DefaultTeamServiceRegistrar.Compose(...))` wires per-team `IEventLog` / `IEncryptedStore` / `IQuarantineQueue` / `IGossipDaemon` / `ILeaseCoordinator` / `IBucketRegistry` / team-scoped `INodeIdentityProvider`; `apps/local-node-host/Program.cs` now invokes this stack directly. Wave 5.2 landed in five sub-commits (A/B/C.1/D/E plus stop-work #1) plus a decomposition plan: `TenantProcessSupervisor` spawns a `local-node-host` child per tenant via `Process.Start`, a `TenantLifecycleCoordinator` drives create/pause/stop/delete, a `TenantHealthMonitor` polls `/health`, a `TenantEndpointRegistry` binds tenant → hosted-node URI, `RabbitMqTenantRegistryEventBus` publishes lifecycle events, and the AppHost composes bridge-web + per-tenant children with a three-tenant smoke test. Wave 5.3.A subdomain middleware + `BrowserTenantContext` + `AuthSalt` column landed (three W5.3 decomposition-plan stop-works outstanding — see §5). Waves 5.5 (IaC: Bicep + Terraform + k8s), 6.6 (`SunfishTeamSwitcher`), 6.7 (Anchor v1→v2 migration), and 6.8 (QR join-additional-team extension) all landed. Three decomposition plans (6.3, 5.2, 5.3) + one research doc (Aspire 13.2 runtime resource mutation — answer: **no**; `WithExplicitStart` is the blessed workaround) shipped. A keystore-backed `IRootSeedProvider` landed plus a Bridge-supervisor `ITenantSeedProvider` HKDF-derives per-tenant seeds from Bridge's install-level root. The MAUI `IHostedService` lifecycle fix unblocks Anchor bootstrap on Windows. The three-tier derivation surface (`ITeamSubkeyDerivation` / `ISqlCipherKeyDerivation` / `IRootSeedProvider`) is stable. The sole remaining not-dispatched scope on the Wave-5/6 spine is W5.3.B/C/D/E + W5.4 (browser-shell passphrase auth, WebSocket transport, ephemeral browser node, founder/joiner flows).

---

## 2. Commit Ledger — `c9fb8d2..HEAD` (31 commits)

| SHA | Subject | Wave / Work-unit |
|---|---|---|
| `7612b88` | docs: align repo narrative with ADRs 0031/0032 (zone A / zone C hybrid) | Pre-wave narrative |
| `2f70ed0` | feat(kernel): Wave 6.1 — TeamContext + per-team HKDF subkey derivation | W6.1, W6.2 |
| `72260e0` | feat(bridge): Wave 5.1 — narrow Bridge to Zone-C control plane | W5.1 |
| `7536ed1` | feat(kernel-runtime): Wave 6.4 — ResourceGovernor concurrent-gossip cap | W6.4 |
| `0f684d9` | fix(bridge): wire real Ed25519 signer into RelayServerTests | W5-bonus |
| `eb0ca73` | feat(kernel-runtime): Wave 6.5 — NotificationAggregator scaffold | W6.5 |
| `ed51ce7` | docs(icm): paper-alignment audit refresh post Waves 5.1/6.1/6.4 | Prior audit |
| `290aa49` | docs(plan): Wave 6.3 decomposition into 6 sub-task DAG | W6.3 plan |
| `375c1c9` | feat(kernel-runtime): Wave 6.3.A — TeamPaths + registrar scaffold | W6.3.A |
| `7333455` | feat(kernel-runtime): Wave 6.3.D — per-team IBucketRegistry via registrar | W6.3.D |
| `86bab64` | feat(kernel): Wave 6.3.B — per-team ledger trio + SQLCipher key derivation | W6.3.B |
| `cbf9ba7` | feat(kernel): Wave 6.3.C — per-team IGossipDaemon + ILeaseCoordinator | W6.3.C |
| `fc16b83` | feat(kernel-runtime): Wave 6.3.E.1 — TeamStoreActivator + registrar sugar | W6.3.E.1 |
| `ccad825` | feat(local-node-host): Wave 6.3.E.2 — compose per-team services in Program | W6.3.E.2 |
| `952a47e` | feat(anchor): Wave 6.3.F — bind shell to TeamContext | W6.3.F |
| `c1d1d13` | fix(anchor): drive IHostedService lifecycle in MauiApp startup | MAUI fix |
| `cf262b0` | docs(plan): Wave 5.2 decomposition — per-tenant data-plane orchestration | W5.2 plan |
| `232bf9b` | feat(bridge): Wave 5.2.A — orchestration contracts scaffold | W5.2.A |
| `8fb8ddf` | feat(bridge): Wave 5.2.B — TenantRegistry lifecycle + event bus publish | W5.2.B |
| `3a482b3` | feat(anchor): Wave 6.7 — v1 → v2 data migration | W6.7 |
| `f2ba653` | feat(bridge,local-node-host): Wave 5.2.D — health endpoint + monitor | W5.2.D |
| `64aef06` | feat(kernel-security): add keystore-backed IRootSeedProvider | W6.7.A (prereq) |
| `5ccdd46` | feat(bridge): Wave 5.2.C.1 — TenantProcessSupervisor via Process.Start | W5.2.C.1 |
| `592c7d2` | feat(bridge): Wave 5.2 stop-work #1 — per-tenant seed derivation | W5.2 stop-work #1 |
| `2cdcc6a` | docs(research): Aspire 13.2 runtime resource-mutation finding | Aspire research |
| `75487e0` | feat(bridge): Wave 5.2.E — AppHost composition + 3-tenant E2E | W5.2.E |
| `2cae957` | feat(ui-adapters-blazor): Wave 6.6 — SunfishTeamSwitcher component | W6.6 |
| `76a216e` | docs(bridge): Wave 5.5 — dedicated-deployment packaging (Option B) | W5.5 |
| `2f53dea` | docs(plan): Wave 5.3 decomposition — browser shell v1 | W5.3 plan |
| `28d2df1` | feat(anchor): Wave 6.8 — join-additional-team QR extension | W6.8 |
| `bb6fad9` | feat(bridge): Wave 5.3.A — subdomain routing + BrowserTenantContext | W5.3.A |

---

## 3. Wave 5 Scorecard (Bridge Zone-C Hybrid, ADR 0031)

| # | Scope | Status | Landed in | Evidence |
|---|---|---|---|---|
| 5.1 | Control-plane scope narrowing + `TenantRegistration` | 🟢 | `72260e0` | `accelerators/bridge/Sunfish.Bridge.Data/Entities/TenantRegistration.cs`; `accelerators/bridge/Sunfish.Bridge/Services/TenantRegistry.cs` (from prior audit) |
| 5.2.A | Orchestration contracts scaffold (8 interfaces + options + 2 enums) | 🟢 | `232bf9b` | `accelerators/bridge/Sunfish.Bridge/Orchestration/` — `ITenantProcessSupervisor.cs`, `ITenantEndpointRegistry.cs`, `ITenantRegistryEventBus.cs`, `ITenantSeedProvider.cs`, `IProcessStarter.cs`, `BridgeOrchestrationOptions.cs`, `TenantProcessState.cs`, `DeleteMode.cs`, `TenantPaths.cs` |
| 5.2.B | `TenantRegistry` lifecycle columns + event-bus publish | 🟢 | `8fb8ddf` | `Sunfish.Bridge.Data/Migrations/20260423_Wave52B_TenantLifecycleColumns.cs`; `ITenantRegistryEventBus` + `RabbitMqTenantRegistryEventBus` |
| 5.2.C.1 | `TenantProcessSupervisor` via `Process.Start` | 🟢 | `5ccdd46` | `accelerators/bridge/Sunfish.Bridge/Orchestration/TenantProcessSupervisor.cs:51`; `TenantLifecycleCoordinator.cs`; `TenantProcessHandle.cs`; `TenantProcessEvent.cs` |
| 5.2.D | `/health` endpoint + `TenantHealthMonitor` | 🟢 | `f2ba653` | `apps/local-node-host/Health/HostedHealthEndpoint.cs` + `LocalNodeHealthCheck.cs`; `accelerators/bridge/Sunfish.Bridge/Orchestration/TenantHealthMonitor.cs`; `TenantHealthEvent.cs` |
| 5.2.E | AppHost composition + 3-tenant E2E smoke test | 🟢 | `75487e0` | `accelerators/bridge/Sunfish.Bridge.AppHost/Program.cs:63-75` (bridge-web `WithEnvironment("Bridge__Orchestration__TenantDataRoot", …)` + `LocalNodeExecutablePath`); 3-tenant integration harness |
| 5.2 stop-work #1 | Per-tenant seed derivation (Bridge HKDF → `LocalNode__RootSeedHex`) | 🟢 | `592c7d2` + `64aef06` | `packages/kernel-security/Keys/KeystoreRootSeedProvider.cs`, `IRootSeedProvider.cs`; `Sunfish.Bridge/Orchestration/TenantSeedProvider.cs`; `apps/local-node-host/Program.cs:58-90` injected-seed branch |
| 5.2 stop-work #2 | Sync-daemon transport bound to per-team endpoint | 🟢 | `cbf9ba7` (W6.3.C) | `packages/kernel-sync/Protocol/UnixSocketSyncDaemonTransport.cs` registered per-team in `DefaultTeamServiceRegistrar.cs:172` via `AddSunfishKernelSync()` |
| 5.2 stop-work #3 | Aspire 13.2 runtime mutation — resolved by research finding | 🟢 (research-resolved) | `2cdcc6a` | `_shared/research/aspire-13-runtime-resource-mutation.md` — verdict: **No**; use `WithExplicitStart` pre-allocated slots |
| 5.3.A | Subdomain routing + `BrowserTenantContext` + `AuthSalt` column | 🟢 | `bb6fad9` | `accelerators/bridge/Sunfish.Bridge/Middleware/TenantSubdomainResolutionMiddleware.cs`, `BrowserTenantContext.cs`, `IBrowserTenantContext.cs`; `Sunfish.Bridge.Data/Migrations/20260423_Wave53A_TenantAuthSalt.cs` |
| 5.3.B | Passphrase auth: `/auth/salt`, `/auth/challenge`, `/auth/verify` + WASM Argon2 | 🔴 | — | Not dispatched; **3 stop-works outstanding (§5)** |
| 5.3.C | `WebSocketSyncDaemonTransport` + `/ws` endpoint in `local-node-host` | 🔴 | — | Not dispatched; `HostedHealthEndpoint` is the refactor-seam |
| 5.3.D | Ephemeral in-memory browser node (CRDT apply + bucket render) | 🔴 | — | Not dispatched; gated on 5.3.C |
| 5.3.E | `Sunfish.Bridge.BrowserShell` project + Playwright E2E | 🔴 | — | Not dispatched; gated on 5.3.B + 5.3.D |
| 5.4 | Founder + joiner flows via browser | 🔴 | — | Gated on 5.3.E |
| 5.5 | Dedicated-deployment IaC (Bicep + Terraform + k8s) | 🟢 | `76a216e` | `accelerators/bridge/deploy/bicep/main.bicep`, `modules/`; `deploy/terraform/main.tf`, `variables.tf`, `outputs.tf`; `deploy/k8s/*.yaml` (9 manifests) |

**Wave 5 progress:** 8 of 13 sub-items 🟢 (5.1, 5.2.A–E including stop-work #1, 5.3.A, 5.5). Only W5.3.B/C/D/E + W5.4 remain. Wave 5 stop-works #2 and #3 are both resolved.

---

## 4. Wave 6 Scorecard (Multi-Team Anchor, ADR 0032)

| # | Scope | Status | Landed in | Evidence |
|---|---|---|---|---|
| 6.1 | `TeamContext` + `ITeamContextFactory` + `IActiveTeamAccessor` | 🟢 | `2f70ed0` | `packages/kernel-runtime/Teams/*.cs` (7→11 files after 6.3) |
| 6.2 | Per-team HKDF subkey derivation + extended `NodeIdentity` | 🟢 | `2f70ed0` | `packages/kernel-security/Keys/TeamSubkeyDerivation.cs`; `packages/kernel-sync/Identity/TeamScopedNodeIdentity.cs` |
| 6.3.A | `TeamPaths` + `TeamServiceRegistrar` scaffold | 🟢 | `375c1c9` | `packages/kernel-runtime/Teams/TeamPaths.cs`, `DefaultTeamServiceRegistrar.cs`, `TeamServiceRegistrar.cs` |
| 6.3.B | Per-team ledger trio (`IEventLog` + `IQuarantineQueue` + `IEncryptedStore`) + `ISqlCipherKeyDerivation` | 🟢 | `86bab64` | `DefaultTeamServiceRegistrar.cs` ledger block; `packages/kernel-security/Keys/SqlCipherKeyDerivation.cs`, `ISqlCipherKeyDerivation.cs` |
| 6.3.C | Per-team `IGossipDaemon` + `ILeaseCoordinator` + `INodeIdentityProvider` + UDS transport | 🟢 | `cbf9ba7` | `DefaultTeamServiceRegistrar.cs:172` `services.AddSunfishKernelSync()`; `:181` `AddSunfishKernelLease(...)` per-team; `UnixSocketSyncDaemonTransport` bound |
| 6.3.D | Per-team `IBucketRegistry` + manifest loader | 🟢 | `7333455` | `DefaultTeamServiceRegistrar.cs:192` `AddSunfishKernelBuckets(...)` |
| 6.3.E.1 | `ITeamStoreActivator` + `AddSunfishDefaultTeamRegistrar` sugar | 🟢 | `fc16b83` | `packages/kernel-runtime/Teams/ITeamStoreActivator.cs`, `TeamStoreActivator.cs` |
| 6.3.E.2 | `local-node-host` composition root + `MultiTeamBootstrapHostedService` | 🟢 | `ccad825` | `apps/local-node-host/Program.cs:109-135`; `apps/local-node-host/MultiTeamBootstrapHostedService.cs` |
| 6.3.F | Anchor shell binds to `TeamContext` | 🟢 | `952a47e` | `accelerators/anchor/` MauiProgram wiring |
| 6.4 | `ResourceGovernor` concurrent-gossip cap | 🟢 | `7536ed1` | `packages/kernel-runtime/Scheduling/ResourceGovernor.cs` (from prior audit) |
| 6.5 | `INotificationAggregator` scaffold | 🟡 | `eb0ca73` | `packages/kernel-runtime/Notifications/NotificationAggregator.cs`; **still no real producers** — gated on sync-daemon transport advancement |
| 6.6 | `SunfishTeamSwitcher` Blazor component | 🟢 | `2cae957` | `packages/ui-adapters-blazor/Components/LocalFirst/SunfishTeamSwitcher.razor` + `.razor.cs` + `.razor.css` + tests |
| 6.7 | Anchor v1 → v2 data migration (`legacy-backup/` + `teams/{legacy_team_id}/`) | 🟢 | `3a482b3` | `accelerators/anchor/Services/AnchorV1MigrationService.cs` |
| 6.8 | QR join-additional-team extension | 🟢 | `28d2df1` | `accelerators/anchor/Services/QrOnboardingService.cs` |

**Wave 6 progress:** 13 of 14 sub-items 🟢, 1 🟡 (6.5 scaffold — producer-wiring deferred per design, gated on sync-daemon transport maturity). Wave 6 is effectively end-to-end complete on the ADR 0032 scope.

---

## 5. Paper Section Deltas (bands that moved)

| Paper § | Concern | Prior | Now | Driver |
|---|---|---|---|---|
| §5.1 (kernel plugin model) | Per-team scoping of kernel services | 🟡 (type exists; not wired) | 🟢 | W6.3.A–F — composition root is live |
| §5.1 | Install-level vs team-level split | 🔴 (undifferentiated) | 🟢 | `Program.cs` comments pin the split explicitly |
| §6.1 (gossip) | Concurrent-gossip cap + per-team daemon | 🟢 (ResourceGovernor type) → 🟢 (wired per-team) | 🟢 | W6.3.C binds `IGossipDaemon` per-team via `AddSunfishKernelSync` |
| §6.3 (leases) | Flease per-team | 🔴 | 🟢 | W6.3.C `AddSunfishKernelLease` in registrar |
| §10.2 (buckets) | Per-team `IBucketRegistry` + manifest scope | 🔴 | 🟢 | W6.3.D |
| §11.2 Layer 1 (encrypted store) | Per-team SQLCipher key + OS keystore root | 🟡 (store exists; keystore backing missing) | 🟢 | W6.3.B `ISqlCipherKeyDerivation` + W6.7.A `IRootSeedProvider` (keystore-backed) |
| §11.3 (role attestation + key distribution) | HKDF subkeys + per-tenant seed injection | 🟢 | 🟢 | W6.2 (prior) + W5.2 stop-work #1 (Bridge HKDFs to child) |
| §13.1 (shell UX) | Multi-team switcher component | 🔴 | 🟢 | W6.6 `SunfishTeamSwitcher` |
| §13.2 (freshness + notifications) | Per-team notification fan-in UI | 🔴 | 🟡 | W6.5 scaffold + W6.6 switcher binds to it (no real producers) |
| §13.4 (QR onboarding) | Join-additional-team variant | 🔴 | 🟢 | W6.8 |
| §13 (migration) | v1 → v2 non-destructive upgrade | 🔴 | 🟢 | W6.7 `AnchorV1MigrationService` w/ `legacy-backup/` |
| §17.2 (hosted-relay-as-SaaS) | Per-tenant data-plane process | 🔴 | 🟢 | W5.2.C.1 `TenantProcessSupervisor` |
| §17.2 | `/health` + lifecycle coordination | 🔴 | 🟢 | W5.2.D + `TenantLifecycleCoordinator` |
| §17.2 | Tenant → endpoint registry | 🔴 | 🟢 | `ITenantEndpointRegistry` + `BridgeRelayAllowlistRefresher` |
| §17.2 | Per-tenant seed independence | 🔴 | 🟢 | W5.2 stop-work #1 HKDF path |
| §17.2 | Subdomain browser-shell routing | 🔴 | 🟡 | W5.3.A middleware landed; B/C/D/E pending |
| §17.2 | Dedicated-deployment (Option B) | 🔴 | 🟢 | W5.5 IaC templates |
| §20.7 (zone mapping) | Zone-A vs Zone-C accelerator binding | 🟢 | 🟢 (both architecturally operational) | entire wave batch |

No regressions detected.

---

## 6. Stop-Work Items Outstanding

Three items from the Wave 5.3 decomposition plan are **design-time stop-works** that need explicit sign-off before W5.3.B can dispatch:

1. **Decrypted role-keys in Blazor Server circuit memory.** Per `wave-5.3-decomposition.md` §2.2 and §2.3, the browser-shell v1 holds `device_key` and derived role-keys in the Blazor Server *circuit* (server process memory, per-tab), not in the browser. The paper says "ephemeral in-memory browser node — session keys wiped on tab close"; the implementation plan puts them server-side. This contradiction needs an explicit ADR-0031 clarification: either (a) accept server-memory key custody as an acceptable threat-model deviation for v1 with documented migration to OPFS-opt-in in v2, or (b) push Argon2 + Ed25519 sign into a Web Worker and keep the circuit key-naive. **Status:** flagged in decomposition plan; not resolved. Blocks W5.3.B.
2. **Proxy back-pressure on browser → bridge-web → tenant-child WebSocket path.** W5.3.C's `TenantWebSocketReverseProxy` has two independent buffers (browser↔bridge-web and bridge-web↔tenant-child). Tail-latency and slow-consumer behavior need a load-test gate before W5.3.C is declared 🟢. `wave-5.3-decomposition.md` §3 5.3.C "Risks" flags this.
3. **Blazor Server render-mode sign-off for the browser shell.** Decomposition plan assumes `AddInteractiveServerRenderMode` (circuit-based) for the v1 shell; ADR 0031 is render-mode-agnostic. A WASM-first posture would move keys off the server entirely but requires argon2 WASM bundle + ed25519 browser-JS bundle shipping from bridge-web. Needs render-mode ADR (could be ADR 0033) before W5.3.B/C commits to server-circuit shape.

**Wave 5.2 leftovers:** None structurally outstanding. Stop-works #2 and #3 from the 5.2 decomposition plan both resolved during the wave itself (sync-daemon transport wired via W6.3.C; Aspire runtime-mutation answered by research doc).

---

## 7. Remaining Gaps (Top 6)

1. **W5.3.B** — passphrase auth endpoints + WASM Argon2 + Ed25519 challenge-sign. **Gated on stop-work #1 and #3 above.** Highest-value next dispatch once signed off.
2. **W5.3.C** — `WebSocketSyncDaemonTransport` (the browser-friendly variant of the sync daemon protocol). Paper §6.2 / §18 sync-daemon transport already exists for UDS (`packages/kernel-sync/Protocol/UnixSocketSyncDaemonTransport.cs` is registered at `DefaultTeamServiceRegistrar.cs:172` via `AddSunfishKernelSync`) — the WebSocket transport is a sibling, not a replacement. **Gated on stop-work #2 load-test agreement.**
3. **W5.3.D** — ephemeral in-memory browser node (CRDT apply + bucket render). Gated on W5.3.C. No prior scaffolding.
4. **W5.3.E** — `Sunfish.Bridge.BrowserShell` project + Playwright E2E smoke. Gated on 5.3.B + 5.3.D.
5. **W5.4** — founder + joiner flows via browser (QR adaptation for browser-first signup). Gated on W5.3.E.
6. **W6.5 real producers** — `IGossipDaemon` / `IConflictInbox` / `IQuarantineQueue` publishing to `ITeamNotificationStream`. Explicitly deferred per prior audit: "gated on sync-daemon transport advancement." Transport itself is now bound per-team (W6.3.C) — real producers are now unblocked by DI but still need domain-event emission code. Follow-up, not a dispatch-blocker.

**Sync-daemon transport check:** `packages/kernel-sync/Protocol/UnixSocketSyncDaemonTransport.cs` exists AND is bound — `packages/kernel-sync/DependencyInjection/ServiceCollectionExtensions.cs:61` registers it as default `ISyncDaemonTransport`, and `DefaultTeamServiceRegistrar.cs:172` invokes `services.AddSunfishKernelSync()` per-team. The transport is in-tree and wired, not "explicitly deferred." The Wave 2.1 "full transport delivery" is not formally declared 🟢 in the plan since gossip/lease production traffic is still minimal in CI; this is the audit's read — raise to plan as a clarifying follow-up.

---

## 8. Recommended Next Dispatch

**First: ADR 0033 (or an ADR-0031 amendment) resolving the three W5.3 stop-works.** This is a 2-4 hour docs dispatch. Until it lands, W5.3.B risks building against a posture that later reviewers reject.

**Second, conditional on stop-work sign-off: W5.3.C first, not W5.3.B.** Rationale:
- W5.3.C is server-side only (`WebSocketSyncDaemonTransport` + `HostedWebSocketEndpoint` on `local-node-host`). Zero browser-side work. The per-tenant endpoint registry is already in place (`ITenantEndpointRegistry`); extending it for WebSocket proxying is a natural extension.
- W5.3.B has the hardest stop-works (render-mode + circuit-memory key custody). Giving it more design time while W5.3.C clears the plumbing is lower-risk.
- W5.3.C landing lets you stand up a Postman/wscat smoke test of the full relay → tenant-child WebSocket path before any browser code exists. Useful diagnostic.

**If W5.3.C dispatches first: parallel-safe second dispatch** = W6.5 producer-wiring (`IGossipDaemon.OnInboundDelta` publishing to `ITeamNotificationStream`). No collision with W5.3.C; both strengthen the kernel-sync surface. Moves W6.5 from 🟡 to 🟢 and unblocks realistic demos of `SunfishTeamSwitcher` badge counts.

**Deferred:** W5.3.B → W5.3.D → W5.3.E → W5.4, sequential, once W5.3.A/C land and the render-mode ADR is accepted.

---

## 9. Cross-References

**ADRs**
- [`docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md`](../../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) — Accepted
- [`docs/adrs/0032-multi-team-anchor-workspace-switching.md`](../../../docs/adrs/0032-multi-team-anchor-workspace-switching.md) — Accepted

**Plans / decomposition**
- [`_shared/product/paper-alignment-plan.md`](../../../_shared/product/paper-alignment-plan.md) — master wave DAG
- [`_shared/product/wave-5.2-decomposition.md`](../../../_shared/product/wave-5.2-decomposition.md) — Bridge orchestration
- [`_shared/product/wave-5.3-decomposition.md`](../../../_shared/product/wave-5.3-decomposition.md) — Bridge browser shell v1
- [`_shared/product/wave-6.3-decomposition.md`](../../../_shared/product/wave-6.3-decomposition.md) — per-team service factory rewiring

**Research**
- [`_shared/research/aspire-13-runtime-resource-mutation.md`](../../../_shared/research/aspire-13-runtime-resource-mutation.md) — Aspire 13.2 runtime resource mutation (verdict: **no**; `WithExplicitStart` is the workaround)

**Code surfaces of record (added/extended this window)**
- `packages/kernel-runtime/Teams/TeamPaths.cs` (W6.3.A)
- `packages/kernel-runtime/Teams/DefaultTeamServiceRegistrar.cs` (W6.3.A–D)
- `packages/kernel-runtime/Teams/ITeamStoreActivator.cs` + `TeamStoreActivator.cs` (W6.3.E.1)
- `packages/kernel-security/Keys/IRootSeedProvider.cs` + `KeystoreRootSeedProvider.cs` (W6.7.A prereq)
- `packages/kernel-security/Keys/ISqlCipherKeyDerivation.cs` + `SqlCipherKeyDerivation.cs` (W6.3.B)
- `packages/ui-adapters-blazor/Components/LocalFirst/SunfishTeamSwitcher.razor{,.cs,.css}` (W6.6)
- `apps/local-node-host/Program.cs` (W6.3.E.2; 138 lines, composition root)
- `apps/local-node-host/MultiTeamBootstrapHostedService.cs` (W6.3.E.2)
- `apps/local-node-host/Health/HostedHealthEndpoint.cs` + `LocalNodeHealthCheck.cs` (W5.2.D)
- `accelerators/bridge/Sunfish.Bridge/Orchestration/*` (W5.2.A–E, 17 files)
- `accelerators/bridge/Sunfish.Bridge/Middleware/TenantSubdomainResolutionMiddleware.cs` + `BrowserTenantContext.cs` + `IBrowserTenantContext.cs` (W5.3.A)
- `accelerators/bridge/Sunfish.Bridge.Data/Migrations/20260423_Wave52B_TenantLifecycleColumns.cs` (W5.2.B)
- `accelerators/bridge/Sunfish.Bridge.Data/Migrations/20260423_Wave53A_TenantAuthSalt.cs` (W5.3.A)
- `accelerators/bridge/Sunfish.Bridge.AppHost/Program.cs` (W5.2.E)
- `accelerators/bridge/deploy/{bicep,terraform,k8s}/*` (W5.5)
- `accelerators/anchor/Services/AnchorV1MigrationService.cs` (W6.7)
- `accelerators/anchor/Services/QrOnboardingService.cs` (W6.8; extended from prior wave)
- `accelerators/anchor/Services/MauiHostedServiceLifetime.cs` (MAUI fix `c1d1d13`)

---

*Snapshot at 2026-04-23 EOD post-`bb6fad9`. Next refresh: after the W5.3 render-mode ADR + W5.3.C land, or when dispatch decisions materially shift the scorecard.*

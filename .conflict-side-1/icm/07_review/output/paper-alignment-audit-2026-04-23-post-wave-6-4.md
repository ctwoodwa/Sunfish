# Paper-Alignment Audit — Post Waves 5.1 / 6.1 / 6.4

**Date:** 2026-04-23 (second refresh of the day)
**Auditor:** Claude Code (Opus 4.7)
**Prior audit:** [`paper-alignment-audit-2026-04-23-refresh.md`](./paper-alignment-audit-2026-04-23-refresh.md)
**Source of truth:** [`_shared/product/local-node-architecture-paper.md`](../../../_shared/product/local-node-architecture-paper.md) v12.0
**Wave plan:** [`_shared/product/paper-alignment-plan.md`](../../../_shared/product/paper-alignment-plan.md)
**Commit window:** `7612b88`..`0f684d9` (plus `eb0ca73` Wave 6.5 scaffold landed after the filename was fixed — reported in §3.5)

Legend: 🟢 aligned • 🟡 partial • 🔴 missing • ⚠ structural conflict • ⚪ out of scope

---

## 1. Executive Summary

The prior refresh audit captured a "Phase-1-kernel-plus-Anchor-end-to-end" baseline. Since then, ADRs 0031 (Bridge Zone-C Hybrid) and 0032 (multi-team Anchor) were accepted and their first implementation waves landed — opening two new workstreams that were not visible in the prior audit at all. **Wave 5.1 narrows Bridge from "authoritative tenant database" to Zone-C control-plane-only, with all team-data entities now `[Obsolete]` and a fresh `TenantRegistration` + `TenantRegistry` surface serving the paper §17.2 ciphertext-at-rest invariant.** **Wave 6.1 stands up `TeamContext` + `ITeamContextFactory` + `IActiveTeamAccessor` + per-team HKDF subkey derivation**, satisfying the cryptographic foundation for Slack-style multi-team Anchor. **Wave 6.4 adds `ResourceGovernor`** to bound concurrent gossip rounds. **Wave 6.5 (NotificationAggregator scaffold) landed in `eb0ca73` after the audit filename was fixed** and is scored below. The repo is still single-tenant-in-practice — no composition root wires `AddSunfishMultiTeam`, no `SunfishTeamSwitcher` exists, and no Anchor v1→v2 migration path is coded. Wave 5.2 (per-tenant data-plane orchestration), Wave 6.3 (per-team factory rewiring of kernel services), Wave 6.6 (switcher UI), and Wave 6.7 (Anchor migration) are the new tall poles.

---

## 2. Wave 6 (Multi-Team Anchor) Scorecard

ADR 0032 eight sub-waves. Status relative to the wave's scope, not to multi-team end-to-end.

| # | Scope | Status | Landed in | Evidence |
|---|---|---|---|---|
| 6.1 | `TeamContext` + `ITeamContextFactory` + `IActiveTeamAccessor` | 🟢 | `2f70ed0` | `packages/kernel-runtime/Teams/*.cs` (7 files); `AddSunfishMultiTeam` in `DependencyInjection/ServiceCollectionExtensions.cs:42` |
| 6.2 | Per-team HKDF subkey derivation + extended NodeIdentity | 🟢 | `2f70ed0` | `packages/kernel-security/Keys/TeamSubkeyDerivation.cs:33`; `packages/kernel-sync/Identity/TeamScopedNodeIdentity.cs`; `IEd25519Signer.GenerateFromSeed` added |
| 6.3 | Per-team service factory rewiring (gossip/lease/eventlog/store/quarantine/buckets) | 🔴 | — | No `TeamServiceRegistrar` callsite outside tests; `DefaultRegistrar` is a no-op (`TeamContextFactory.cs:20`); composition roots still resolve singletons |
| 6.4 | `ResourceGovernor` concurrent-gossip cap | 🟢 | `7536ed1` | `packages/kernel-runtime/Scheduling/ResourceGovernor.cs`; `MaxActiveRoundsPerTick = 2` default (`ResourceGovernorOptions.cs:20`) |
| 6.5 | `INotificationAggregator` + per-team streams + cross-team fan-in | 🟡 | `eb0ca73` | `packages/kernel-runtime/Notifications/NotificationAggregator.cs` — scaffold present; no team-stream producers registered anywhere |
| 6.6 | `SunfishTeamSwitcher` Blazor component | 🔴 | — | No `*TeamSwitcher*` file under `packages/ui-adapters-blazor/` |
| 6.7 | Anchor v1 → v2 migration code | 🔴 | — | No file in `accelerators/anchor/` references `teams/` subdirectory layout or `legacy-backup/` |
| 6.8 | Join-additional-team QR extension | 🔴 | — | `QrOnboardingService` in `accelerators/anchor/` unchanged since `02df46b` |

**Wave 6 progress:** 3 of 8 🟢, 1 🟡 scaffold, 4 🔴. The cryptographic + scheduling foundations are in; the service rewiring that makes them effective is not.

---

## 3. Wave 5 (Bridge Zone-C Hybrid) Scorecard

ADR 0031 five sub-waves.

| # | Scope | Status | Landed in | Evidence |
|---|---|---|---|---|
| 5.1 | Control-plane scope narrowing + TenantRegistration | 🟢 | `72260e0` | `accelerators/bridge/Sunfish.Bridge.Data/Entities/TenantRegistration.cs` (TrustLevel / TenantStatus / SupportContact); `accelerators/bridge/Sunfish.Bridge/Services/TenantRegistry.cs` (GetBySlug / Create / SetTeamPublicKey / UpdateTrustLevel / ListActive); 8 project-domain entities marked `[Obsolete]` in `Entities.cs:27..117` |
| 5.2 | Per-tenant data-plane orchestration (`Bridge.AppHost` spawns `local-node-host`) | 🔴 | — | `Bridge.AppHost` unchanged; no process-supervisor code; `[Obsolete]` removal is gated on this wave |
| 5.3 | Browser shell v1 (Blazor Server per subdomain + passphrase-derived key) | 🔴 | — | No new sub-app under `accelerators/bridge/` |
| 5.4 | Founder + joiner flows via browser | 🔴 | — | Gated on 5.3 |
| 5.5 | Dedicated-deployment packaging (IaC templates) | 🔴 | — | No `accelerators/bridge/deploy/` IaC additions |

**Bonus:** `0f684d9` wires a real `Ed25519Signer` into `RelayServer` (`RelayServer.cs:48, 71, 211`) — fixes the stub-signer baseline the prior audit noted; tests in `accelerators/bridge/tests/.../RelayServerTests.cs` now pass against real crypto.

**Wave 5 progress:** 1 of 5 🟢 (+1 relay-signer fix). 5.1 did the important semantic work (the rest of Wave 5 is orchestration scaffolding on top of it).

---

## 4. Paper §§ Delta (moved bands only)

| Paper § | Concern | Prior | Now | Driver |
|---|---|---|---|---|
| §5.1 (kernel plugin model) | Per-team scoping of kernel services | 🔴 (unaddressed — paper §5.1 is agnostic, but ADR 0032 derives from it) | 🟡 (type exists; not wired) | Wave 6.1 |
| §6.1 (gossip) | Concurrent-gossip cap for multi-team installs | n/a (ADR 0032-only concern) | 🟢 | Wave 6.4 |
| §11.3 (role attestation + key distribution) | Operator cross-team correlation defense | 🔴 (single keypair across everything) | 🟢 (HKDF subkey verified at 1000-team scale — commit message of `2f70ed0`) | Wave 6.2 |
| §13 (UX) | Multi-team UX surface | 🔴 | 🔴 (no switcher; no aggregator UI; no migration) — **unchanged** | — |
| §17.2 (hosted-relay-as-SaaS) | Ciphertext-at-rest invariant at control-plane boundary | 🟡 (ADR 0026 dual-posture; SaaS shell still carried team data) | 🟡 → 🟢 at the data-model layer (control plane holds none; data plane still to come in 5.2) | Wave 5.1 |
| §17.2 | Per-tenant isolation boundary | 🔴 | 🟡 (TrustLevel enum + registration exists; hosted-node-per-tenant doesn't) | Wave 5.1 |
| §20.7 (Zone mapping) | Accelerator → zone binding | 🔴 (undocumented) | 🟢 (Anchor = Zone A, Bridge = Zone C; encoded in `CLAUDE.md`, `docs/adrs/0031`, `0032`, `accelerators/*/README.md`) | `7612b88` |

No regressions detected.

---

## 5. Newly Visible Gaps

Gaps that only surface once Wave 5.1 + 6.1 foundations exist. These were invisible to the prior audit.

### 5.1. `TeamContext` disposal cascade is sound; cache eviction is bounded only by `RemoveAsync`

`TeamContextFactory` caches in `Dictionary<TeamId, TeamContext>` with no LRU, no TTL, no weak references. A user who joins N teams over time holds N live scopes until process exit or an explicit `RemoveAsync` call. `RemoveAsync` exists (`TeamContextFactory.cs:97`) and cascades disposal correctly, but nothing calls it today — no leave-team UX, no GC-on-long-idle policy. **Followup:** Wave 6.7 (Anchor migration) must decide the leave-team semantics; memory growth is O(teams-ever-joined) today.

### 5.2. `ResourceGovernor` is disconnected from `IGossipDaemon`

Wave 6.4 shipped the semaphore-backed governor with a correct `AcquireGossipSlotAsync(teamId, ct)` contract, but `IGossipDaemon` in `packages/kernel-sync/` has no call site invoking it. Cap is enforceable only when Wave 6.3 rewires the gossip daemon through the team factory and the factory injects the governor. **This is Wave 6.3 scope, not a defect.** Worth noting explicitly so that Wave 6.3 dispatch includes the governor-hookup as a first-class deliverable, not a bolt-on.

### 5.3. `TenantRegistration.TrustLevel` has no setter surface

`TenantRegistry.UpdateTrustLevelAsync` exists (`TenantRegistry.cs:154`) but no HTTP endpoint, no admin UI, no CLI tool calls it — only `TenantRegistryTests.cs:146` exercises it. A tenant that signs up in the default `RelayOnly` state has no way to elect `AttestedHostedPeer`. **Followup:** Wave 5.3 browser shell must surface the election at signup; an admin backoffice surface is the earliest it can move.

### 5.4. `AddSunfishMultiTeam` has zero production callers

The DI extension exists (`ServiceCollectionExtensions.cs:42`) but no composition root (`apps/local-node-host`, `accelerators/anchor`, `accelerators/bridge`) invokes it. The entire Wave 6 machinery is type-present but runtime-absent. This is Wave 6.3 scope by design, but it means "Wave 6.1 complete" does not mean "Wave 6 delivers user value yet."

### 5.5. `INotificationAggregator` has no team-stream producers

Wave 6.5 scaffold (`eb0ca73`) ships the aggregator but no `IGossipDaemon` / `IConflictInbox` / `IQuarantineQueue` publishes to `ITeamNotificationStream`. `GetAggregateUnreadCount()` returns 0 in practice until Wave 6.3 wires the producers.

### 5.6. Bridge relay identity is ephemeral per process restart

`RelayServer.cs:48` generates a fresh Ed25519 keypair in the constructor. Fine for stateless Wave-4-era relays but when Wave 5.2's hosted-node-per-tenant runs as a full replicated peer (paper §17.2), a stable relay identity persisted somewhere becomes a real concern (HELLO signature verifiability across relay restarts). **Followup:** decide relay-identity persistence posture during Wave 5.2 design.

### 5.7. `[Obsolete]` Bridge entities still compile and still run the demo

`Entities.cs:18` `#pragma warning disable CS0618` keeps the demo executing during the transition. Wave 5.2 must delete these types + the demo-seeder paths that use them — otherwise "ADR 0031 landed" keeps a parallel non-paper-aligned code path alive indefinitely.

---

## 6. Untouched Gaps From Prior Audit (Top 5, Still Open)

1. **`IKeystore`-backed `INodeIdentityProvider` in `apps/local-node-host`** — DI fallback still generates a fresh keypair per start. Wave 5.2 makes this more painful (per-tenant process lifecycle amplifies it).
2. **`DELTA_STREAM` receive-path rate-limiter invocation** — rate limiter exists in `kernel-sync` but `GossipDaemon.AllowInboundDelta` is uncalled. Prior-audit TODO #2 carries forward.
3. **React adapter full parity** — ADR 0014 still outstanding; switcher adds a new parity item (Wave 6.6 backlog per ADR 0032 line 205).
4. **Deterministic simulation + chaos-test harnesses** (paper §15 levels 4–5) — not implemented.
5. **`compat-telerik` `TelerikGrid.OnRead` gap-closure** — still throws at entry per prior audit item #11.

---

## 7. Recommended Next Wave

**Wave 6.3 — Per-team service factory rewiring.** Rationale:

- It unblocks Wave 6.4 (governor becomes effective), Wave 6.5 (producers publish to per-team streams), Wave 6.6 (switcher has something to switch), and Wave 6.7 (migration has a target layout to migrate to). Four downstream waves are gated on 6.3 alone.
- It's in-kernel work — no UI fan-out, no cross-process orchestration. Fits a single dispatch cleanly.
- It converts the Wave 6.1 + 6.2 type scaffolding from "compiles, doesn't do anything" to "composition roots light up multi-team." Highest leverage per line of code of any candidate.
- Wave 5.2 (Bridge per-tenant orchestration) is a larger, longer-running workstream that benefits from Wave 6.3 being done first (the per-tenant hosted-node process spawned by Wave 5.2 will itself compose via `AddSunfishMultiTeam` — though with N=1 teams per host).

**Parallel-safe second dispatch:** Wave 5.2 design document + `Bridge.AppHost` prototype. It doesn't touch `packages/kernel-runtime/` so it won't collide with 6.3 at the build layer.

**Deferred dispatches:** Wave 6.6 (switcher UI) and Wave 6.7 (Anchor migration) should wait until 6.3 proves the scoping contract works end-to-end inside `local-node-host`.

---

## 8. Test Coverage Snapshot (Deltas Only)

| Area | Prior | Now | Source |
|---|---|---|---|
| `packages/kernel-runtime/tests/` | 20 | 36+ (16 new, TeamContext + ActiveTeam + ResourceGovernor) | `2f70ed0`, `7536ed1` commit messages |
| `packages/kernel-security/tests/` | 29 | 40 (11 new on subkey derivation; 1000-team collision-free probe) | `2f70ed0` |
| `accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/` | 14 | 14 + new `TenantRegistryTests.cs` (slug uniqueness, public-key write-once, TrustLevel update) + fixed `RelayServerTests.cs` | `72260e0`, `0f684d9` |

All previously-passing tests remain passing (no regressions reported in commit messages; `0f684d9` explicitly "restores RelayServer test baseline").

---

## 9. Cross-References

**Commits in this audit window**
- `7612b88` — docs-align narrative with ADRs 0031/0032
- `2f70ed0` — Wave 6.1: TeamContext + HKDF subkey in kernel-runtime/kernel-security
- `72260e0` — Wave 5.1: Bridge Zone-C control-plane narrowing + TenantRegistration
- `7536ed1` — Wave 6.4: ResourceGovernor concurrent-gossip cap
- `0f684d9` — RelayServer Ed25519 signer integration (test-baseline restore)
- `eb0ca73` — Wave 6.5: NotificationAggregator scaffold (landed after audit filename fixed; scored at §2 for completeness)

**ADRs**
- [`docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md`](../../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) — Accepted 2026-04-23
- [`docs/adrs/0032-multi-team-anchor-workspace-switching.md`](../../../docs/adrs/0032-multi-team-anchor-workspace-switching.md) — Accepted 2026-04-23
- [`docs/adrs/0026-bridge-posture.md`](../../../docs/adrs/0026-bridge-posture.md) — Superseded by ADR 0031
- [`docs/adrs/0027-kernel-runtime-split.md`](../../../docs/adrs/0027-kernel-runtime-split.md) — referenced by 0032

**Specs / plans**
- [`_shared/product/local-node-architecture-paper.md`](../../../_shared/product/local-node-architecture-paper.md) — paper v12.0 (§§5, 6, 11, 13, 17.2, 20.7 load-bearing for this audit)
- [`_shared/product/paper-alignment-plan.md`](../../../_shared/product/paper-alignment-plan.md) — wave DAG; Waves 5 + 6 drive this audit
- [`icm/07_review/output/paper-alignment-audit-2026-04-23-refresh.md`](./paper-alignment-audit-2026-04-23-refresh.md) — immediate predecessor

**Code surfaces of record**
- `packages/kernel-runtime/Teams/*` — Wave 6.1
- `packages/kernel-runtime/Scheduling/*` — Wave 6.4
- `packages/kernel-runtime/Notifications/*` — Wave 6.5 scaffold
- `packages/kernel-security/Keys/*` — Wave 6.2
- `packages/kernel-sync/Identity/TeamScopedNodeIdentity.cs` — Wave 6.2 bridge
- `accelerators/bridge/Sunfish.Bridge.Data/Entities/TenantRegistration.cs` — Wave 5.1
- `accelerators/bridge/Sunfish.Bridge/Services/TenantRegistry.cs` — Wave 5.1
- `accelerators/bridge/Sunfish.Bridge/Relay/RelayServer.cs:48,71,211` — relay signer fix

---

*Snapshot at 2026-04-23 post-`eb0ca73`. Next refresh: after Wave 6.3 rewiring lands (expected largest-value next dispatch per §7).*

# Wave 6.3 — Per-Team Service Factory Rewiring: Decomposition Plan

**Status:** Draft | **Date:** 2026-04-23
**Source:** Plan-agent output, 2026-04-23 dispatch.

---

## 1. Current State Audit

All six services are registered as **process-level singletons** today via `TryAddSingleton`. None have per-team awareness. The composition roots call one `Add*` per service, producing one instance for the entire process.

| Service | Package | Registration | Per-team state vector | Consumers |
|---|---|---|---|---|
| `IGossipDaemon` | `packages/kernel-sync/Gossip/` | `services.TryAddSingleton<IGossipDaemon, GossipDaemon>()` via `AddSunfishKernelSync` in `packages/kernel-sync/DependencyInjection/ServiceCollectionExtensions.cs:85`. Co-registers `ISyncDaemonTransport`, `VectorClock`, `IEd25519Signer`, `INodeIdentityProvider`. | Keypair (currently root; must become team-subkey per ADR 0032); `VectorClock` (per-team); known-peers membership (per-team); `ISyncDaemonTransport` endpoint (install-level, but the daemon that speaks over it is team-level). | `LocalNodeWorker` ctor (`apps/local-node-host/LocalNodeWorker.cs:39`); `ILeaseCoordinator` (DI chain in `AddSunfishKernelLease`); `RelayWorker` indirectly. |
| `ILeaseCoordinator` | `packages/kernel-lease/` | `services.TryAddSingleton<ILeaseCoordinator>(sp => new FleaseLeaseCoordinator(...))` in `packages/kernel-lease/DependencyInjection/ServiceCollectionExtensions.cs:48`. Takes `localNodeId` as a direct string arg at registration time. | Node id (currently install-level, must become team-scoped derivation); in-flight lease map (per-team); quorum participants (per-team). | `local-node-host` (indirectly, via plugins that will take leases). |
| `IEventLog` | `packages/kernel-event-bus/` | `services.TryAddSingleton<IEventLog, FileBackedEventLog>()` via `AddSunfishEventLog` in `packages/kernel-event-bus/DependencyInjection/EventBusServiceCollectionExtensions.cs:67`. Directory comes from `EventLogOptions.Directory` (default `%LOCALAPPDATA%\Sunfish\event-log\`). | File directory (per-team); epoch id (per-team); in-memory head pointer. | `EventLogBackedQuarantineQueue` ctor; future projections; `IPostingEngine` ledger log. |
| `IEncryptedStore` | `packages/foundation-localfirst/Encryption/` | `services.AddSingleton<IEncryptedStore>(_ => new SqlCipherEncryptedStore())` in `packages/foundation-localfirst/ServiceCollectionExtensions.cs:74`. Store is returned **unopened** — caller owns `OpenAsync(databasePath, key)`. `EncryptionOptions.DatabasePath` default `%LOCALAPPDATA%/Sunfish/data/sunfish.db`. `EncryptionOptions.KeystoreKeyName` default `"sunfish-primary"`. | DB path (per-team); derived Argon2id key (per-team; keyed by team-subkey-derived material); open SQLite connection (per-team). | `QrOnboardingService.cs:34`; any block plugin that persists to the encrypted KV store. |
| `IQuarantineQueue` | `packages/foundation-localfirst/Quarantine/` | `services.AddSingleton<IQuarantineQueue>(sp => new EventLogBackedQuarantineQueue(sp.GetRequiredService<IEventLog>()))` in `packages/foundation-localfirst/ServiceCollectionExtensions.cs:34`. **Directly coupled to whichever `IEventLog` is in the container.** | Delegates its state to `IEventLog`; logical stream `"foundation-localfirst/quarantine"` (could be reused across teams since the event log itself is per-team). | Offline-write validation paths (future block plugins). |
| `IBucketRegistry` | `packages/kernel-buckets/` | `services.TryAddSingleton<IBucketRegistry, BucketRegistry>()` via `AddSunfishKernelBuckets` in `packages/kernel-buckets/DependencyInjection/ServiceCollectionExtensions.cs:26`. Co-registers `IBucketYamlLoader`, `IBucketFilterEvaluator`, `IBucketStubStore`, `IStorageBudgetManager`. | Loaded bucket manifests (per-team); storage budget counters (per-team); YAML manifest search path (per-team). | Kernel sync delta streaming (Wave 2.5+); block plugin bucket subscription. |

**Composition roots that wire the six today:**
- `apps/local-node-host/Program.cs:40-50` — wires all six via the `Add*` helpers chained on `builder.Services`.
- `accelerators/anchor/MauiProgram.cs:36-42` — wires only `AddSunfishEncryptedStore`, `AddSunfishKernelSecurity`, `AddSunfishKernelRuntime`. Gossip/lease/event-log/quarantine/buckets are **not** wired into the Anchor shell today — the shell expects to talk to a running `local-node-host` over IPC (not yet wired in 6.x). For 6.3, Anchor keeps this posture; the rewiring is in `local-node-host`.
- `accelerators/bridge/Sunfish.Bridge/Program.cs:235-236` — Relay posture wires `AddSunfishKernelSync` + `AddSunfishKernelSecurity` only (tier-3 relay has no tenant authority — it forwards bytes). For 6.3 the Relay posture requires **zero changes** because a relay terminates transport only; the SaaS posture also does not wire these six today.

---

## 2. Target State per Service

Wave 6.1 already landed the integration seam: `TeamServiceRegistrar` (`packages/kernel-runtime/Teams/TeamServiceRegistrar.cs`) is a `delegate void (IServiceCollection, TeamId)` invoked inside `TeamContextFactory.CreateAsync` to populate a fresh per-team `ServiceCollection` whose `BuildServiceProvider()` becomes `TeamContext.Services`. The existing `AddSunfish*` extensions are **reusable almost verbatim inside the registrar** — they are `TryAddSingleton`-based, so calling them against a fresh `ServiceCollection` per team yields exactly the desired per-team singletons.

**Canonical DI pattern adopted (all six services):**

```csharp
// In the per-team registrar callback (NEW file: packages/kernel-runtime/Teams/DefaultTeamServiceRegistrar.cs
// or in a composition-root-local helper):
TeamServiceRegistrar perTeamWiring = (services, teamId) =>
{
    var layout = /* resolve TeamDirectoryLayout from the outer provider */;
    services.AddSunfishEventLog(o => { o.Directory = layout.EventLogDirectory(teamId); o.EpochId = "epoch-0"; });
    services.AddSunfishQuarantineQueue();
    services.AddSunfishEncryptedStore(o =>
    {
        o.DatabasePath = layout.SqlCipherPath(teamId);
        o.KeystoreKeyName = layout.KeystoreKeyName(teamId);
    });
    services.AddSunfishKernelSync(o => { /* per-team options */ });
    services.AddSunfishKernelLease(localNodeId: /* team-subkey-derived NodeId */);
    services.AddSunfishKernelBuckets();
    // Team-scoped identity bridge (replaces singleton INodeIdentityProvider per team)
    services.AddSingleton<INodeIdentityProvider>(sp =>
        new InMemoryNodeIdentityProvider(
            TeamScopedNodeIdentity.Derive(rootIdentity, teamId.ToString(), subkeyDerivation)));
};
```

**Per-service target interface change:** none of the six public interfaces change. The only thing that changes is *where they get resolved from* — always through `TeamContext.Services`, never `sp.GetRequiredService<>()` from the outer provider.

**Per-team state isolation — concrete contract:**

| Service | Per-team slot |
|---|---|
| `IEventLog` | `EventLogOptions.Directory = {DataDirectory}/teams/{team_id}/events/` |
| `IEncryptedStore` | `EncryptionOptions.DatabasePath = {DataDirectory}/teams/{team_id}/sunfish.db` |
| `IEncryptedStore` keystore key | `EncryptionOptions.KeystoreKeyName = "sunfish:team:{team_id}:primary"` |
| `IGossipDaemon` | Per-team `INodeIdentityProvider` seeded from `TeamScopedNodeIdentity.Derive(root, teamId, subkeyDerivation)`. Transport stays install-level (one Unix socket) — the daemon multiplexes by team id at the handshake layer; this is **not** in scope for 6.3 (handshake-team-id is a deferred follow-up). |
| `IGossipDaemon` (in-process) | Per-team `VectorClock`, per-team `KnownPeers`. |
| `ILeaseCoordinator` | `localNodeId` = hex of team-subkey public key (first 16 bytes). Per-team transport is shared with gossip. |
| `IQuarantineQueue` | Transitively per-team via the resolved `IEventLog`. |
| `IBucketRegistry` | Per-team bucket manifests loaded from `{DataDirectory}/teams/{team_id}/buckets/*.yaml` (new convention — today manifest source is unspecified). |

---

## 3. Proposed Sub-Task Breakdown (6 agents)

### DAG shape

```
6.3.A (TeamPaths + DefaultTeamServiceRegistrar scaffold)
    │
    ├─▶ 6.3.B (Ledger trio: IEventLog + IQuarantineQueue + IEncryptedStore)
    │
    └─▶ 6.3.C (Sync pair: IGossipDaemon + ILeaseCoordinator, depends on 6.2 identity)
    │
    └─▶ 6.3.D (IBucketRegistry + manifest path convention)

                   │  │  │
                   ▼  ▼  ▼
               6.3.E (local-node-host composition root rewire)
                         │
                         ▼
               6.3.F (Anchor shell bind + Bridge relay verify)
```

6.3.B, 6.3.C, 6.3.D run in parallel after 6.3.A. 6.3.E gates on all three. 6.3.F gates on 6.3.E.

---

### 6.3.A — Scaffold `TeamPaths` + empty `DefaultTeamServiceRegistrar`

**Scope:** Lands the shared primitives both state (B/C/D) subtasks depend on, with zero service wiring yet.

**Files touched:**
- `packages/kernel-runtime/Teams/TeamPaths.cs` (NEW) — static helper: `TeamPaths.DatabasePath(string dataDirectory, TeamId)`, `TeamPaths.EventLogDirectory(...)`, `TeamPaths.BucketsDirectory(...)`, `TeamPaths.KeystoreKeyName(TeamId)`. All concrete strings live here.
- `packages/kernel-runtime/Teams/DefaultTeamServiceRegistrar.cs` (NEW) — `static class` exposing `Compose(RootIdentityAccessor, ITeamSubkeyDerivation, string dataDirectory) : TeamServiceRegistrar`. Initially a no-op body (just captures the deps in a closure); B/C/D fill it in.
- `packages/kernel-runtime/Teams/ITeamDirectoryLayout.cs` (NEW, optional) — interface wrapper over `TeamPaths` if tests need to override. Recommend keeping `TeamPaths` static; add `ITeamDirectoryLayout` only if Anchor's MDM-override (`docs/specifications/mdm-config-schema.md`) needs runtime override. **Decision gate (see §6 anti-patterns): default to static `TeamPaths` unless sub-task B finds an override consumer.**
- `packages/kernel-runtime/tests/TeamPathsTests.cs` (NEW) — asserts the six string conventions against ADR 0032 §Default (line 103 of the ADR) and the migration spec (line 186). Parameterize over Windows + POSIX path separators.

**Test strategy:** New unit tests only. No existing tests change.

**Dependencies:** Wave 6.1 (`TeamId`, `TeamServiceRegistrar` delegate). Wave 6.2 (`ITeamSubkeyDerivation` — needed for the accessor signature, not yet called).

**Risk notes:**
- **Anti-pattern flag #3 (vague success criteria):** "string conventions" is the whole deliverable — write them out explicitly in tests as literal strings, not as functions of each other.
- Path separator cross-platform bug — use `Path.Combine`, assert with `Path.DirectorySeparatorChar`-agnostic comparison.
- `{team_id}` form is the GUID "D" format per `TeamId.ToString()` — document this in `TeamPaths` XML doc to prevent callers from URL-encoding or hyphen-stripping it.

**Effort:** ~4 hours.

---

### 6.3.B — Ledger trio: `IEventLog` + `IQuarantineQueue` + `IEncryptedStore` per-team

**Scope:** These three are grouped because `IQuarantineQueue`'s only per-team state is transitive through `IEventLog`, and both it and `IEncryptedStore` share the same directory-layout shape. One agent can hold the full mental model.

**Files touched:**
- `packages/kernel-runtime/Teams/DefaultTeamServiceRegistrar.cs` — add event-log, quarantine, encrypted-store registrations using `TeamPaths`.
- `packages/foundation-localfirst/Encryption/EncryptionOptions.cs` — doc-only: note `KeystoreKeyName` convention under multi-team (`"sunfish:team:{team_id}:primary"`). Leave default untouched for back-compat with non-multi-team callers.
- `packages/kernel-event-bus/EventLogOptions.cs` — doc-only: note that under multi-team, `Directory` is set explicitly by the registrar; default stays as-is for non-multi-team callers.
- `packages/kernel-runtime/tests/DefaultTeamServiceRegistrarLedgerTests.cs` (NEW) — build a registrar against a temp `DataDirectory`, resolve `IEventLog` + `IQuarantineQueue` + `IEncryptedStore` from two `TeamContext`s, assert: (a) different directories, (b) writes to team-A don't surface in team-B's `ListKeysAsync`, (c) disposing team-A's context closes its SQLCipher connection without affecting team-B.
- Existing tests unaffected (`AddSunfish*` helpers are called the same way — just against a fresh `ServiceCollection`).

**Test strategy:** New integration test. Existing `EncryptedStoreContractTests`, `EventLogContractTests`, `QuarantineQueueTests` stay green unchanged (they don't depend on team scoping).

**Dependencies:** 6.3.A.

**Risk notes:**
- **Anti-pattern #1 (unvalidated assumption):** `SqlCipherEncryptedStore.OpenAsync` throws on second-open — confirm the per-team factory construct-and-open sequence doesn't double-open. The store is constructed in the registrar but `OpenAsync` must happen lazily (deferred to a hosted service or first use). **Open question: where does the key (32 bytes from Argon2id over the team subkey) come from at open time?** This likely requires a per-team `ITeamKeystoreBridge` — flag as a stop-work item if 6.2's subkey-to-keystore binding is underspecified.
- Directory creation race — the event log directory is auto-created by `FileBackedEventLog`; the SQLCipher store auto-creates its parent. No explicit `Directory.CreateDirectory` needed in the registrar.
- Keystore key name collision on upgrade — v1 `sunfish-primary` slot will already exist; v2 must either migrate to `sunfish:team:{legacy_team_id}:primary` or keep reading the legacy name for team-0. That migration is **Wave 6.7**, not 6.3. For 6.3, verify new teams use the new name.

**Effort:** ~8-10 hours.

---

### 6.3.C — Sync pair: `IGossipDaemon` + `ILeaseCoordinator` per-team

**Scope:** These two are grouped because `ILeaseCoordinator` takes a hard constructor dependency on `IGossipDaemon` and on `localNodeId` (string) — per-team identity derivation lands in both places at once.

**Files touched:**
- `packages/kernel-runtime/Teams/DefaultTeamServiceRegistrar.cs` — register per-team `INodeIdentityProvider` via `TeamScopedNodeIdentity.Derive`; register `IGossipDaemon` + `ILeaseCoordinator` through existing `AddSunfishKernelSync` / `AddSunfishKernelLease` with the derived `localNodeId`.
- `packages/kernel-lease/DependencyInjection/ServiceCollectionExtensions.cs` — **no code change** if the registrar computes the derived `localNodeId` ahead of the `AddSunfishKernelLease(services, localNodeId)` call. If tests need a delegate form (`Func<IServiceProvider, string>`), add an overload that resolves `localNodeId` from `INodeIdentityProvider`. **Prefer no-overload path first; only add the overload if tests reveal a real need.**
- `packages/kernel-runtime/Teams/DefaultTeamServiceRegistrar.cs` — also register `IResourceGovernor`-adapter (Wave 6.4 is a separate wave, but gossip consumption is per-team; just ensure the registrar does not re-register the governor — it stays in the root container). Document the split explicitly: governor is root-scope, gossip is team-scope.
- `packages/kernel-runtime/tests/DefaultTeamServiceRegistrarSyncTests.cs` (NEW) — two teams, assert distinct `INodeIdentity.PublicKey`, distinct `VectorClock`, distinct `KnownPeers`. Start both gossip daemons, tick once, verify no cross-talk.
- Existing `GossipDaemonTests.cs`, `FleaseLeaseCoordinatorTests.cs` untouched.

**Test strategy:** New integration test (uses `InMemorySyncDaemonTransport` so no real socket). Existing tests stay green.

**Dependencies:** 6.3.A **and Wave 6.2** (`ITeamSubkeyDerivation`, `TeamScopedNodeIdentity`). 6.2 is already landed per commit `2f70ed0`, so this is not a blocker.

**Risk notes:**
- **Anti-pattern #1 (unvalidated assumption):** `ISyncDaemonTransport` is install-level, not team-level. Multiple per-team `GossipDaemon` instances sharing one transport means inbound messages need team-id routing on the handshake. Today HELLO doesn't carry a team id. **Stop-work item:** confirm whether the transport-multiplexing follow-up (a separate wave) is prerequisite or whether 6.3 ships with a single "default team" constraint and the multiplex is a future gap in the ADR 0032 open questions. Based on the ADR text, ADR 0032 §Default treats it as in-process — multiple daemons sharing one transport is **not** covered. **Recommend: 6.3 ships each team with its own transport endpoint** (different socket paths per team), which the ADR doesn't forbid and which sidesteps the multiplex question.
- **Root/team identity conflation:** `GossipDaemon` falls back to generating its own keypair if no `INodeIdentityProvider` is registered (`ServiceCollectionExtensions.cs:70-83`). In the per-team `ServiceCollection` we MUST register the team-scoped identity **before** `AddSunfishKernelSync` is called (or use `services.RemoveAll<INodeIdentityProvider>()` first). Write a guard test.
- Lease coordinator's `localNodeId` must deterministically round-trip — two identically-configured processes on the same machine with the same root key and team id must produce the same `localNodeId`. Asserting this is the clearest validation.

**Effort:** ~10-12 hours.

---

### 6.3.D — `IBucketRegistry` per-team + manifest path convention

**Scope:** Smallest of the three parallel branches — `IBucketRegistry` is purely additive state (no identity dep).

**Files touched:**
- `packages/kernel-runtime/Teams/DefaultTeamServiceRegistrar.cs` — add `AddSunfishKernelBuckets` call; register a per-team `IBucketYamlLoader` source pointing at `TeamPaths.BucketsDirectory(teamId)`.
- `packages/kernel-buckets/BucketYamlLoader.cs` — if the loader today has a hard-coded path, parameterize it via options. Check its ctor. **Validate assumption:** loader may already be filesystem-agnostic.
- `packages/kernel-runtime/tests/DefaultTeamServiceRegistrarBucketsTests.cs` (NEW) — two teams with distinct manifest directories produce distinct `IBucketRegistry.All`.
- Existing `BucketRegistryTests.cs`, `BucketYamlLoaderTests.cs` untouched.

**Test strategy:** New integration test. Existing tests untouched.

**Dependencies:** 6.3.A.

**Risk notes:**
- **Anti-pattern #17 (premature precision):** The bucket manifest source directory is not specified in ADR 0032. Don't over-specify — document it as "proposed convention, subject to block-developer-experience review" and note the decision lives in the `TeamPaths` XML docs so future agents can amend it in one place.
- Storage budget manager is a singleton inside `kernel-buckets`. It's a counter, not an I/O resource. It becomes per-team naturally through the registrar. No extra work.

**Effort:** ~4-6 hours.

---

### 6.3.E — `local-node-host` composition root rewire

**Scope:** The last-mile rewire of the one process that wires all six today.

**Files touched:**
- `apps/local-node-host/Program.cs` — REPLACE the 10-line service-chain at lines 40-50 with: (a) root-only services (`AddSunfishKernelRuntime`, `AddSunfishKernelSecurity`, `AddSunfishMultiTeam(registrar: ...)`, `AddSunfishResourceGovernor`); (b) a `MultiTeamBootstrapHostedService` that (on startup) reads `LocalNodeOptions.TeamId` + enumerates any additional team ids from config/keystore and calls `ITeamContextFactory.GetOrCreateAsync(...)` for each.
- `apps/local-node-host/LocalNodeWorker.cs` — CHANGE `IGossipDaemon` ctor dep to accept `ITeamContextFactory` + `IActiveTeamAccessor` instead. The worker iterates `factory.Active` and calls `StartAsync` on each team's gossip daemon — or, for the minimum viable 6.3, keeps the single-team path and bootstraps only the configured default team (multi-team gossip-fan-out is demonstrated in 6.4/6.5 test harness).
- `apps/local-node-host/LocalNodeOptions.cs` — doc update: `TeamId` is now "default team for legacy single-team mode"; introduce `Teams : List<TeamBootstrap>` optional list for multi-team config.
- `apps/local-node-host/tests/LocalNodeWorkerTests.cs` — UPDATE fakes: replace singleton `IGossipDaemon` fake with a fake `ITeamContextFactory` + fake `TeamContext`. Most assertions stay structurally equivalent.

**Test strategy:** Existing `LocalNodeWorkerTests` updated; add one new test for "bootstrap two teams at startup; both gossip daemons reach the `Started` state."

**Dependencies:** 6.3.B + 6.3.C + 6.3.D all green.

**Risk notes:**
- **Anti-pattern #6 (missing Resume Protocol):** If 6.3.E is dispatched before B/C/D are all complete, the composition root tries to wire a registrar that only partially populates per-team services, producing cryptic DI errors at runtime. **Review gate:** 6.3.E dispatch must verify all three parallel branches' PRs are merged first.
- v1 upgrade path — `local-node-host` v1 data at `{DataDirectory}/sunfish.db` is NOT migrated in 6.3; that's Wave 6.7. For 6.3, production users on v1 would lose access to their data if they boot v2 `local-node-host` with the new path. **Mitigation:** ship 6.3 behind a config flag (`LocalNode.MultiTeam.Enabled = false` by default), or gate on 6.7 landing in the same release cycle. Recommend: ship 6.3 with flag-off default and enable only in the internal build used by 6.7 test cases.
- `Bridge.AppHost` per-tenant orchestration (Wave 5.2) **spawns** `local-node-host` child processes and expects single-team config per child. 6.3's multi-team rewire of `local-node-host` must not break that contract. **Validation:** the default (flag-off / one-team-config) path must produce the same runtime behaviour as today's.

**Effort:** ~8-10 hours.

---

### 6.3.F — Anchor shell bind + Bridge relay verify

**Scope:** The two other composition roots. Small deliverable.

**Files touched:**
- `accelerators/anchor/MauiProgram.cs` — REPLACE `AddSunfishEncryptedStore()` at line 36 with `AddSunfishMultiTeam(registrar: ...)`. Remove direct `IEncryptedStore` singleton (downstream `QrOnboardingService` + `AnchorSessionService` become `IActiveTeamAccessor`-scoped — a small refactor inside those services). Optional: defer the `QrOnboardingService`/`AnchorSessionService` rewrite to Wave 6.6 (switcher UI) if they can keep their current direct-singleton contract for v0 single-team.
- `accelerators/anchor/Services/QrOnboardingService.cs` — CHANGE `IEncryptedStore` ctor dep to `IActiveTeamAccessor`, resolve `_store` via `accessor.Active!.Services.GetRequiredService<IEncryptedStore>()`. Guard: null active team → throw `InvalidOperationException("No active team — call SetActiveAsync first")`.
- `accelerators/anchor/Services/AnchorSessionService.cs` — same pattern.
- `accelerators/anchor/tests/QrOnboardingServiceTests.cs` — UPDATE the `NoopEncryptedStore` fake to be wrapped in a fake `TeamContext` + fake `IActiveTeamAccessor`.
- `accelerators/anchor/tests/AnchorSessionServiceTests.cs` — same.
- `accelerators/bridge/Sunfish.Bridge/Program.cs` — **VERIFY ONLY, no code change.** Confirm SaaS posture doesn't resolve any of the six (it doesn't — SaaS is Postgres + DAB + SignalR). Confirm Relay posture (`ConfigureRelayPosture`, line 219) only wires `AddSunfishKernelSync` + `AddSunfishKernelSecurity` for transport-terminator duty — Relay has no team concept. Add a code comment in Relay posture stating "Relay is team-agnostic — no `AddSunfishMultiTeam` here by design." This prevents a future well-meaning agent from "fixing" the omission.

**Test strategy:** Existing Anchor tests updated; no new integration test required (6.6's switcher UI test is the end-to-end demo).

**Dependencies:** 6.3.E green.

**Risk notes:**
- **Anti-pattern #8 (blind delegation trust):** Anchor's current "no local kernel services" posture (line 36 only wires encrypted-store + security + runtime) means there's nothing to rewire for gossip/lease/event-log/quarantine/buckets. The agent may be tempted to wire them in 6.3; resist. ADR 0032 places those in `local-node-host`, not in the Anchor shell process. Explicitly document this carve-out.
- **Null-active-team NullReferenceException:** the `Active!` non-null assertion will fire at runtime before the user has joined any team. Gate on `TeamContext?` nullable with clean error message, not an NRE.
- Bridge Relay posture is the easiest possible verification but also the most likely to be casually broken — pin it with an assertion test in `Sunfish.Bridge.Tests.Unit` that `ConfigureRelayPosture` does NOT register `ITeamContextFactory`. That's a one-line assertion test worth writing.

**Effort:** ~6-8 hours.

---

## 4. Composition-root touchpoints (summary)

| Root | Today's 6-service registrations | Target state after 6.3 |
|---|---|---|
| `apps/local-node-host/Program.cs` | All six via chained `.AddSunfish*` (lines 40-50) | Root: `AddSunfishKernelRuntime` + `AddSunfishKernelSecurity` + `AddSunfishMultiTeam(registrar)` + `AddSunfishResourceGovernor`. Bootstrap hosted service calls `GetOrCreateAsync` per configured team. |
| `accelerators/anchor/MauiProgram.cs` | Only `AddSunfishEncryptedStore` (line 36) | Replace with `AddSunfishMultiTeam(registrar)`. `QrOnboardingService`/`AnchorSessionService` resolve through `IActiveTeamAccessor`. |
| `accelerators/bridge/Sunfish.Bridge/Program.cs` | Relay posture wires gossip + security; SaaS posture wires neither | **NO CHANGE.** Add comment + assertion test pinning the carve-out. |

---

## 5. Per-team directory layout decision (lock-in)

**Decision:** Strings live in a single `TeamPaths` static helper in `packages/kernel-runtime/Teams/TeamPaths.cs`. Not an interface. Upgrade to `ITeamDirectoryLayout` only if and when Anchor's MDM-override (per `docs/specifications/mdm-config-schema.md`) needs runtime substitution — at that point, wrap `TeamPaths` behind the interface.

**Concrete paths (cited to ADR 0032 lines 103 + 186):**

| Slot | Convention |
|---|---|
| Team root | `{DataDirectory}/teams/{TeamId}/` where `{TeamId}` is `TeamId.ToString()` (standard Guid "D" form) |
| SQLCipher DB | `{DataDirectory}/teams/{TeamId}/sunfish.db` |
| Event log | `{DataDirectory}/teams/{TeamId}/events/` |
| Bucket manifests | `{DataDirectory}/teams/{TeamId}/buckets/` |
| Keystore key | `"sunfish:team:{TeamId}:primary"` |
| Legacy backup (Wave 6.7) | `{DataDirectory}/legacy-backup/` |

`{DataDirectory}` continues to resolve from `LocalNodeOptions.DataDirectory` or Anchor's equivalent path accessor. **`TeamPaths` is install-wide — it takes `dataDirectory` as a parameter; it does not itself read `LocalNodeOptions`.** That keeps it usable from `accelerators/anchor/` without `LocalNodeOptions` baggage.

---

## 6. Anti-patterns flagged (per `.claude/rules/universal-planning.md`)

1. **#1 Unvalidated assumptions — HIGH RISK:**
   - *Assumption A:* `ISyncDaemonTransport` can be shared across per-team `IGossipDaemon` instances without handshake-level team-id routing. **Verify by:** reading `HandshakeProtocol.cs` for team-id fields; if absent, switch to per-team transport endpoints (recommended above) rather than silently multiplexing.
   - *Assumption B:* `SqlCipherEncryptedStore.OpenAsync` can be deferred to a hosted service after the registrar builds the provider. **Verify by:** inspecting ctor + OpenAsync separation; confirmed — the store ctor is parameterless.
   - *Assumption C:* `IBucketYamlLoader`'s manifest directory is already parameterizable. **Verify by:** reading `BucketYamlLoader.cs`.
   - *Assumption D:* The v1 singleton keystore name `"sunfish-primary"` is not hard-coded anywhere outside `EncryptionOptions` defaults. **Verify by:** grep — if hard-coded in Anchor shell, ship 6.7's migration before 6.3's default flip.

2. **#8 Blind delegation trust — MITIGATION:** Each sub-task's PR is reviewed against the others' contracts before 6.3.E is dispatched. Specifically: 6.3.A's `TeamPaths` strings must be finalized and reviewed before B/C/D start, because all three call into it. 6.3.E gate-checks that B/C/D merged.

3. **#17 Premature precision — AVOIDED:** The bucket manifest directory convention is proposed but flagged as "subject to block-developer-experience review" in `TeamPaths` XML docs. Effort estimates are in hour-ranges (4-12), not specific numbers.

4. **#4 No rollback — MITIGATION:** `local-node-host` ships 6.3 behind a config flag (`MultiTeam.Enabled = false` default) until Wave 6.7 migration lands. Rollback is a config change.

---

## Stop-work items warranting human review before dispatch

1. **Transport multiplexing vs. per-team transport endpoints.** ADR 0032 is silent on whether the one install-level `UnixSocketSyncDaemonTransport` is shared by all per-team `GossipDaemon`s or whether each team gets its own socket. Recommendation is per-team endpoints (simpler, no protocol change). Needs BDFL confirmation before 6.3.C dispatch.

2. **SQLCipher key provisioning at team-context open time.** The registrar builds the store *unopened*. The Argon2id-derived key comes from where — a per-team keystore entry seeded by 6.2's subkey derivation? A hosted service that runs post-build? The `OpenAsync(databasePath, key)` contract requires the key on first open. 6.2 shipped `ITeamSubkeyDerivation` but did not ship the "subkey → 32-byte SQLCipher key" bridge. This may require a small pre-wave (call it 6.2.5 or a 6.3.0 prelude) before 6.3.B can meaningfully test `IEncryptedStore` end-to-end.

3. **`local-node-host` default-off flag coordination with Wave 5.2.** Bridge's per-tenant orchestration (Wave 5.2 per `paper-alignment-plan.md` line 125) spawns `local-node-host` child processes expecting single-team semantics. 6.3's rewire must preserve that contract when the multi-team flag is off. Explicit test needed; Wave 5 workstream owner must sign off.

---

## Summary

**Decomposition shape:** Six sub-agents in a three-tier DAG. Tier 1 is 6.3.A (shared `TeamPaths` strings + empty registrar scaffold, ~4h). Tier 2 is three parallel agents: 6.3.B (event-log + quarantine + encrypted-store, ~8-10h), 6.3.C (gossip + lease, ~10-12h), 6.3.D (buckets, ~4-6h) — grouped by shared-state affinity rather than per-service to avoid thrashing the shared registrar file. Tier 3 is 6.3.E (`local-node-host` rewire, ~8-10h) and 6.3.F (Anchor shell + Bridge relay verify, ~6-8h). Total: ~40-50 agent-hours across 6 dispatchable chunks. Every sub-task has its own test file; existing contract tests stay untouched because the per-service interfaces do not change — only the resolution scope does.

**DAG:** 6.3.A → (6.3.B ‖ 6.3.C ‖ 6.3.D) → 6.3.E → 6.3.F.

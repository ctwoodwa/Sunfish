---
id: 32
title: Multi-Team Anchor (Slack-Style Workspace Switching)
status: Accepted
date: 2026-04-23
tier: accelerator
concern:
  - identity
  - multi-tenancy
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0032 — Multi-Team Anchor (Slack-Style Workspace Switching)

**Status:** Accepted (2026-04-23)
**Date:** 2026-04-23
**Resolves:** ADR 0031 deferred "multi-team Anchor (workspace-switcher) scope for v2" as one of five follow-up decisions. This ADR decides the architecture of a multi-team Anchor — how one desktop installation can host a user's membership in multiple teams (tenants), how those teams are isolated in-process, how the user switches between them, and where the hard edges lie.

---

## Context

Anchor today ships as **single-team per install** (ADR 0031 decision for v1). A user who is a member of multiple teams — "I work at Acme, consult for Globex, run a personal team" — has three choices today, all bad:

1. **Install Anchor multiple times in separate OS user accounts.** Maximum isolation but poor UX: log out of Windows to switch teams, separate Start-menu entries, separate notification permissions, no unified inbox.
2. **Install Anchor once and pick one team to participate in.** Simple but forces the user to choose a primary context and give up participation in others.
3. **Run multiple Anchor instances under the same OS user.** Only works if the build supports multiple parallel instances; risks cross-instance config/keystore collisions.

The established solution from the chat-app world is the Slack model: **one installation, one UI shell, a workspace switcher, per-workspace state isolation**. Figma, Discord, Microsoft Teams, and 1Password all implement variants. The pattern works.

The question for Sunfish is *where* in the stack the isolation boundary lives. Unlike Slack (cloud-authoritative, per-workspace data fetched on demand), Sunfish's local-first architecture means every team the user is a member of comes with its own local encrypted database, event log, CRDT documents, gossip daemon state, role keys, and plugin set. Running multiple teams = running multiple full local data planes on one device.

Three architectural axes must be settled:

1. **Isolation boundary**: OS-process level, AppDomain/runtime level, or intra-process namespacing?
2. **Device identity**: one keypair shared across all teams, or per-team keypair?
3. **Concurrency model**: only the active team runs, or all teams sync in background?

Each axis has implications for security, UX, resource usage, and code complexity.

---

## Decision drivers

- **Paper §5.1 microkernel-monolith model** — plugins run in-process with the kernel runtime. This biases toward intra-process team isolation with per-team scopes rather than cross-process RPC.
- **Paper §11 defense-in-depth** — ciphertext at rest is the last line of defense. Multi-team intra-process bugs yield undecryptable ciphertext across team boundaries if role keys are strictly per-team.
- **Paper §13.1 complexity-hiding standard** — users should not need to understand isolation boundaries. The switcher is just "my workspaces," same as Slack.
- **Pre-release + breaking-changes-approved** — kernel-runtime + session APIs can be reshaped.
- **Resource footprint** — Anchor is a desktop app; running 4 full kernel stacks for 4 teams is a real RAM/CPU concern on 8GB laptops. The paper's idle target is under 1GB; 4× that is not acceptable.
- **Operator correlation threat** — if the user's device uses one Ed25519 keypair across all teams, a hostile relay operator for Team A could correlate the same public key appearing in Team B's gossip and infer cross-team membership. For privacy-conscious users, per-team keypair eliminates this.
- **v1 compat** — Anchor v1 users migrating to v2 must not lose state. Their single team becomes team-0 in the new multi-team model, no data migration required.

---

## Considered options

### Option A — Status quo: multiple installs, no in-app multi-team

Keep single-team-per-install. Users manage multiple teams via multiple OS user accounts or community-maintained launcher tricks.

- **Pro:** zero new code; strongest OS-level isolation.
- **Con:** worst UX; not what "Slack-style" means; unlikely to be acceptable for knowledge-worker users past v2.
- **Rejected** — doesn't address the question this ADR was opened to answer.

### Option B — Shell + N child processes per team

One Anchor shell process plus one `local-node-host`-equivalent child process per team. Shell communicates with each child over IPC (named pipes / Unix sockets); shell brokers UI updates.

- **Pro:** strongest intra-app isolation — OS process boundaries between teams. A plugin bug in Team A cannot read Team B's decrypted state.
- **Pro:** team can crash independently; shell can restart individual children without affecting others.
- **Con:** resource-heavy — 4 teams = 4 full kernel stacks = ~1.5-3GB RAM.
- **Con:** complex IPC layer; the shell becomes a mini-relay brokering UI events.
- **Con:** cross-team features (unified notifications, global search) require the shell to aggregate from N IPC streams.

### Option C — Single process, per-team scopes

One Anchor shell, one in-process kernel-runtime instance that holds N **TeamContext** objects. Each TeamContext owns its own SQLCipher DB, its own event log, its own CRDT document set, its own gossip daemon instance, its own keystore entries, its own role attestations. Team switching is a UI-only operation: the shell re-binds its views to a different TeamContext.

- **Pro:** lightweight — one process, shared runtime infrastructure, per-team data structures only. RAM proportional to team count × per-team working set, not × per-team kernel overhead.
- **Pro:** cross-team features are in-process queries over N TeamContexts (simple).
- **Pro:** simplest migration from single-team v1 — v1's singleton kernel services become factories-returning-per-team instances.
- **Con:** weaker isolation than Option B. A kernel bug (not an application bug — kernel is in-process-trusted code) that cross-wires TeamContexts leaks state across teams.
- **Con:** plugins loaded for Team A run in the same process as Team B's data. A malicious plugin is a cross-team concern. (Mitigation: plugins are policy-gated at install; kernel never auto-loads untrusted plugins; enterprise users who can't accept this risk use Option B variant.)
- **Con:** a kernel crash takes down all teams. Recoverable by auto-restart, but worse tail latency than Option B.

### Option D — Hybrid: shell manages children for privileged teams, single process for default teams

Mix: most teams live in the shared-process Option-C model. A tenant can opt into a dedicated child process (Option-B model) for compliance-sensitive teams.

- **Pro:** default path is lightweight (Option C), compliance escape hatch exists (Option B).
- **Pro:** maps naturally onto ADR 0031's trust-level gradient (Relay-only / Attested / No-hosted-peer).
- **Con:** two code paths for the same thing — every cross-team feature has to handle both in-process and out-of-process peers.
- **Con:** complex to explain to users and to test.

### Option E — WebView-per-team (Electron/Tauri model)

Anchor shell is a native window; each team runs in its own WebView with its own cookies/storage scope. Adopts Electron's BrowserWindow-per-tab model.

- **Pro:** strong in-app isolation via WebView origin sandboxing.
- **Con:** Anchor is MAUI Blazor Hybrid; WebView-per-team is not a natural MAUI pattern and adds significant shell-rewrite cost.
- **Con:** WebViews don't help with the data-plane isolation — local-node-host is outside the WebView.
- **Rejected** for Sunfish's MAUI stack; would be the right pattern for a Tauri/Electron variant.

---

## Decision (recommended)

**Adopt Option C (single process, per-team scopes) as the default. Reserve Option B (shell + child processes) as an explicit compliance-tier opt-in for regulated industries.**

### Default: Option C — Per-team TeamContext

The kernel runtime becomes aware of multiple teams:

1. **`TeamContext` type** (new, `packages/kernel-runtime/`) — holds everything team-scoped: `TeamId`, `INodeIdentity`, `IEncryptedStore`, `IEventLog`, `IQuarantineQueue`, `ICrdtEngine`, `IGossipDaemon`, `ILeaseCoordinator`, `IBucketRegistry`, `IPluginRegistry` (per-team plugin set), `IAttestationVerifier`, `IRoleKeyManager`.
2. **`ITeamContextFactory`** — resolves a `TeamContext` from a `TeamId`. Lazily initializes: first call per team wires all services against that team's SQLCipher DB (at `{DataDirectory}/teams/{team_id}/sunfish.db`), event log (`{DataDirectory}/teams/{team_id}/events/`), keystore entries (prefixed with `sunfish:team:{team_id}:*`).
3. **`IActiveTeamAccessor`** — the UI's handle on "which team is currently foreground." UI components bind `[Inject] IActiveTeamAccessor Team` instead of direct service injection; the accessor scopes resolution through the active `TeamContext`.
4. **Team switcher component** — new UI at `packages/ui-adapters-blazor/Components/LocalFirst/SunfishTeamSwitcher.razor`. Renders the user's known teams as a sidebar (Slack-style) with per-team badge counts (unread gossip events, unread conflicts). Click → `IActiveTeamAccessor.SetActive(teamId)` → UI re-binds.
5. **Background sync policy** — all teams' gossip daemons run in all tick windows, not just the active team's. Paper §6.1's 30-second tick is cheap enough that 4–8 background teams is negligible. Per-team `IGossipDaemon` lives for the lifetime of the process; suspension is a separate escape valve.
6. **Unified notifications** — a new `INotificationAggregator` service sits above `IActiveTeamAccessor` and subscribes to every TeamContext's notification streams. UI's notification surface (`SunfishNodeHealthBar`'s tray, OS-native toasts) shows per-team badges plus an aggregate count.
7. **Settings scope** — UI preferences (theme, provider selection, notification policy) are stored in a **global** config file under the user's profile path, not per-team. Block/bundle enablement is **per-team** (different teams have different plugin sets).

### Device identity: one Ed25519 keypair per install, one Noise-pattern subkey per team

To balance operator-correlation risk against cryptographic cost:

- The install holds **one root Ed25519 keypair** as the hardware-bound device identity. Stored once in the OS keystore.
- For each team the user joins, the install **derives a per-team subkey** via HKDF(root_private, "sunfish-team-subkey-v1:" + team_id). This subkey is what the user uses to sign HELLO messages + as the public-key target in role-attestation issuance for that team.
- Operators of different teams see **different public keys** — they cannot correlate the same user across teams.
- Admin-assisted-recovery remains bound to the root keypair (tenant admin can rotate role keys; users can't forget their team-subkey without losing the root).

This is a middle path between "one shared keypair everywhere" (operator correlation trivial) and "fresh keypair per team" (admin recovery impossible). Rationale in `docs/specifications/sync-daemon-protocol.md` §9 and paper §11.3.

### Trust-level escape hatch: Option B for compliance-sensitive teams

For regulated-industry users (healthcare, finance, legal) whose policy or auditor requires OS-level process isolation between tenants:

- The tenant's install configuration can mark the team as `"isolation": "process"` (default is `"intra-process"`).
- `TeamContextFactory` detects the flag and spawns a dedicated `local-node-host` child process for that team instead of constructing services in-process.
- Shell communicates with the child via the existing sync-daemon transport (Unix socket / named pipe) — same protocol as Bridge's relay ingress.
- Team switcher renders isolated teams with a small 🔒 glyph so users understand the security model.
- Plugins that run in-process for in-process teams run in the child process for isolated teams; the plugin API is identical.
- Cost: the team pays the process overhead (paper's ~1GB idle) and accepts longer cold-start when switching into that team.

This escape hatch is **not part of v2 MVP** but the APIs are designed so Option B can layer in later without breaking in-process teams.

### Concurrency model: all teams sync, only one renders

- Every joined team's `IGossipDaemon` runs on the 30-second tick continuously. Writes to each team's event log happen in the background regardless of which team is active in the UI.
- Only the active team's state is rendered in the main UI viewport. Switching teams is a view-swap, not a service-start.
- Background teams accumulate notifications, conflict counts, and delta-since-last-viewed metrics. Switching surfaces them.
- Resource governor (new `packages/kernel-runtime/ResourceGovernor.cs`) caps concurrent gossip rounds at `MaxActiveRoundsPerTick = 2` by default — even with 10 joined teams, at most 2 round trips happen per tick, round-robined. Keeps network + CPU bounded on small laptops.

### Onboarding flow for joining additional teams

Anchor already ships the QR-from-phone + founder-bundle paths (Wave 3.3). Extend for multi-team:

- User with Anchor installed on Team A clicks "Add workspace" in team switcher.
- Options: "Join via QR scan from existing member," "I'm the founder of a new team," "Paste invitation bundle."
- Each flow creates a new `TeamContext` with a fresh per-team subkey; no impact on existing team contexts.

### What stays single-tenant

- **Anchor v1 installs upgrade trivially**: existing team becomes TeamContext with team_id = existing NodeId-derived team identifier. No data migration; no reboot of existing gossip connections.
- **Device OS integration** (autostart, system tray, protocol handlers) is install-level, not team-level.
- **Auto-update** is install-level.

---

## Consequences

### Positive

- Users who belong to multiple teams have a single Anchor install, single login session, single system tray icon, single notification permission. Matches Slack/Figma/Discord mental models.
- Cross-team features (unified search, unified notification center, cross-team clipboard) become in-process queries — fast, no IPC.
- Resource usage grows sub-linearly with team count (shared runtime infrastructure; per-team working set only).
- Per-team subkey eliminates operator-level cross-team correlation — a genuine privacy improvement over "same public key everywhere."
- Option-B escape hatch gives compliance-sensitive deployments an explicit isolation path without forking Anchor.
- Anchor v1 installs upgrade without user action; their single team becomes team-0.
- Paper §13.1's complexity-hiding standard preserved: users see "my workspaces" and a switcher, not "my per-team SQLCipher databases."

### Negative

- Intra-process isolation is weaker than OS process isolation. A kernel bug or a malicious plugin in a shared-process team can access other teams' decrypted state in memory. Mitigation: plugin-loading is policy-gated (already the paper §5.1 model); Option B available for teams that can't accept the risk.
- Team count × gossip tick frequency must be governed; uncapped, a user in 20 teams on a metered connection could consume bandwidth unexpectedly. The `ResourceGovernor` mitigates but needs tuning from real-world usage.
- Unified notification aggregation has edge cases — rate-limiting, per-team do-not-disturb, notification grouping across teams.
- Per-team subkey derivation via HKDF adds a new crypto contract the kernel must maintain; attestation issuance needs to prove the subkey roots to the device root (paper §11.3 role-attestation chains extend).
- Settings scoping (global UI prefs vs. per-team plugin enablement) has bikeshed potential — specific edge cases will surface in beta.
- Team switcher is new UI surface that must live in every adapter (Blazor now, React Wave 3.5 eventually). Parity cost per ADR 0014.
- Compliance-sensitive opt-in Option B is an unimplemented promise in v2 — writing the API without wiring the implementation creates a TODO that future-you must honor.

---

## Compatibility plan

### Anchor v1 → v2 upgrade

- v1 installs have exactly one team's data under `DataDirectory/sunfish.db`, `DataDirectory/events/`, etc.
- First launch of v2 detects the legacy layout and migrates in-place: creates `DataDirectory/teams/{legacy_team_id}/` subfolder, moves files. One-time, non-destructive (original data also kept under `DataDirectory/legacy-backup/` for one minor version cycle before deletion).
- Team switcher shows the single legacy team with a "(primary)" tag until the user adds a second team.

### Kernel runtime API changes

- `IGossipDaemon` + `ILeaseCoordinator` + `IBucketRegistry` + `IEventLog` + `IEncryptedStore` + `IQuarantineQueue` become **team-scoped** — resolved via `ITeamContextFactory`, not directly from DI.
- Cross-process consumers (local-node-host, anchor, bridge) update their composition roots to resolve via the factory.
- Existing `AddSunfishKernelSync()` et al. extensions become helpers for **intra-team** composition; they're called by `TeamContextFactory` internally rather than by composition roots directly.
- Breaking change for any downstream code that injects singleton kernel services directly. Pre-release status allows this break.

### ADR 0031 interaction

- ADR 0031 (Bridge hybrid multi-tenant SaaS) is **not** superseded — it covers the server-side tenant-isolation model.
- This ADR covers the **client-side** multi-team model. The two are orthogonal: a user can have Team A (served by Bridge's hosted peer at acme.sunfish.example.com) + Team B (served by a different Bridge tenant at globex.sunfish.example.com) + Team C (fully self-hosted, no Bridge) all in the same Anchor install.
- ADR 0031's "single-tenant per install for v1" clause is updated by this ADR: v2 supports multi-team. ADR 0031 is amended, not superseded.

### React adapter parity

- `SunfishTeamSwitcher` has to ship in both Blazor and React adapters per ADR 0014.
- React scaffold (Wave 3.5) doesn't yet include the switcher; added to the React adapter's parity backlog.

---

## Implementation checklist

- [ ] Add `TeamContext` + `ITeamContextFactory` + `IActiveTeamAccessor` to `packages/kernel-runtime/`.
- [ ] Reshape per-team services (IGossipDaemon, ILeaseCoordinator, IEventLog, IEncryptedStore, IQuarantineQueue, IBucketRegistry) to be factory-resolved rather than singleton.
- [ ] Add per-team subkey derivation to `packages/kernel-security/Keys/` — HKDF(root, "sunfish-team-subkey-v1:" + team_id).
- [ ] Extend `NodeIdentity` to carry both root public key and per-team derived public keys.
- [ ] Add `ResourceGovernor` to cap concurrent-gossip-rounds-per-tick.
- [ ] Add `INotificationAggregator` + per-team notification streams.
- [ ] New UI component: `SunfishTeamSwitcher` (Blazor first, React parity backlog).
- [ ] Extend `accelerators/anchor/` shell to render the team switcher + bind active-team UI.
- [ ] Extend `QrOnboardingService` (Wave 3.4) with "add additional team" flow.
- [ ] v1 → v2 migration code in Anchor's first-launch handler.
- [ ] Amend ADR 0031 with a pointer to this ADR under "Multi-team Anchor."
- [ ] Update `accelerators/anchor/README.md` with the multi-team mental model diagram.
- [ ] Settings scope doc in `docs/specifications/multi-team-settings-scoping.md` — global-vs-per-team for each setting category.
- [ ] Update `_shared/product/paper-alignment-plan.md` — new Wave 6 for multi-team Anchor implementation.
- [ ] Flagged follow-up ADRs:
  - [ ] ADR for Option B compliance-tier isolation (process-per-team mechanics).
  - [ ] ADR for cross-team features surface (unified search, clipboard, command palette).
  - [ ] ADR for resource-governor tuning policy based on beta telemetry.

---

## Open questions (named; deferred to implementation)

1. **Max teams per install.** Slack caps free workspaces at 50. Is there a ceiling? Likely governed by `ResourceGovernor` rather than a hard limit, but a soft UI warning at ~20 is defensible.
2. **Team-local DND (do-not-disturb).** Per-team or global? Likely both — DND schedule is global, per-team override exists.
3. **Global search across teams.** Does search query all TeamContexts' CRDT stores? Privacy implications — user sees data from multiple teams in one UI list. Default off, opt-in per team.
4. **Offline-first onboarding.** Can a user add a second team while fully offline (e.g., from a paste-bundle)? Yes by the paper's architecture; write tests before shipping.
5. **Plugin trust across teams.** If Team A has plugin X enabled and Team B does not, but X is loaded in-process because Team A needs it, does X run against Team B's data? No — plugins are scoped to the TeamContext that enabled them. Verify in the plugin registry's isolation tests.

---

## References

- [ADR 0031](./0031-bridge-hybrid-multi-tenant-saas.md) — Bridge multi-tenant server side. This ADR is the client-side counterpart.
- [ADR 0014](./0014-adapter-parity-policy.md) — React parity requirement for the new switcher component.
- [ADR 0027](./0027-kernel-runtime-split.md) — the kernel-runtime surface extended here.
- [Paper v12.0 §5.1](../../_shared/product/local-node-architecture-paper.md#51-kernel-and-plugin-model) — microkernel-monolith model with plugins in-process.
- [Paper v12.0 §11.3](../../_shared/product/local-node-architecture-paper.md#113-role-attestation-vs-key-distribution) — attestation chains extended for per-team subkeys.
- [`packages/kernel-runtime/`](../../packages/kernel-runtime/) — where `TeamContext` + `ITeamContextFactory` land.
- [`packages/kernel-security/Keys/`](../../packages/kernel-security/Keys/) — per-team subkey derivation.
- [`accelerators/anchor/`](../../accelerators/anchor/) — the shell that hosts the team switcher.

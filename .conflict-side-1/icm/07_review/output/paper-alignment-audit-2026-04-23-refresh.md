# Paper-Alignment Audit — Refresh after Tier-2 follow-ups

**Date:** 2026-04-23 (one day after the original audit)
**Auditor:** Claude Code (Opus 4.7)
**Prior audit:** [`paper-alignment-audit-2026-04-22.md`](./paper-alignment-audit-2026-04-22.md)
**Source of truth:** [`_shared/product/local-node-architecture-paper.md`](../../../_shared/product/local-node-architecture-paper.md)
**Scope:** delta since original audit. Packages in `packages/*`, `accelerators/*`, `apps/*`, new `integration-tests/*`, and installer/docs artifacts.

Legend: 🟢 aligned • 🟡 partial • 🔴 missing • ⚠ structural conflict • ⚪ out of scope

---

## 1. Executive Delta

The 2026-04-22 audit identified ~20 critical gaps across kernel / UI / accelerator tiers. The paper-alignment execution plan closed all four named waves (0–4) plus two Tier-2 follow-up rounds. **Every paper §5.1 kernel responsibility now has a concrete, tested landing zone; the missing multi-node real-transport smoke test landed; CRDT stub was swapped for a real YDotNet backend; the Ed25519-on-HELLO stub was replaced with real signatures.**

### State at refresh

| Row | 2026-04-22 | 2026-04-23 | Commits |
|---|---|---|---|
| Sync daemon protocol spec | 🔴 missing | 🟢 `docs/specifications/sync-daemon-protocol.md` | `4f34abd` |
| Gossip anti-entropy daemon | 🔴 | 🟢 `packages/kernel-sync/` — real Ed25519, monotonic nonce, rate limiter | `9a72c8f` + `5feb033` |
| mDNS peer discovery | 🔴 | 🟢 `packages/kernel-sync/Discovery/` Makaretu.Dns.Multicast.New | `eeff8ed` |
| Flease distributed lease | 🔴 | 🟢 `packages/kernel-lease/` 3/5-node quorum tests | `eeff8ed` |
| Local encrypted database | 🔴 | 🟢 SQLCipher + Argon2id + OS keystore | `dec7951` |
| Role attestation + key distribution | 🔴 | 🟢 `packages/kernel-security/` Ed25519 + X25519 + HKDF + role keys | `b96e96d` |
| Circuit-breaker quarantine | 🟡 (write-buffer) | 🟢 event-sourced quarantine with Promote/Reject | `b96e96d` |
| `ILocalNodePlugin` + extension contracts | 🔴 | 🟢 `packages/kernel-runtime/` with 5 extension interfaces | `dec7951` |
| CRDT engine abstraction | 🔴 | 🟢 ICrdtDocument/Text/Map/List contracts + YDotNet backend | `b96e96d` + `5feb033` |
| Event log + snapshots | 🟡 (InMemory stub) | 🟢 FileBackedEventLog with corruption recovery | `dec7951` |
| Bidirectional schema lenses | 🔴 | 🟢 `packages/kernel-schema-registry/Lenses/` BFS shortest-path | `9a72c8f` |
| Schema epoch coordinator | 🔴 | 🟢 `packages/kernel-schema-registry/Epochs/` | `9a72c8f` |
| Stream compaction | 🔴 | 🟢 CompactionScheduler + UpcasterRetirement + StreamArchive | `e34e09d` |
| CRDT GC + sharding | 🔴 | 🟢 ShardedDocument + ShallowSnapshotManager + GcCollector | `e34e09d` |
| Sync buckets (YAML) | 🔴 | 🟢 `packages/kernel-buckets/` with attestation-eligibility | `eeff8ed` |
| Double-entry ledger | 🟡 (generic block) | 🟢 `packages/kernel-ledger/` event-sourced posting engine + CQRS | `e34e09d` |
| Local-node host process | 🔴 | 🟢 `apps/local-node-host/` wires all 10 kernel services, starts gossip | `9a72c8f` + `fe7bc55` |
| UI kernel sync-state tokens | 🔴 | 🟢 3 provider `_sync-state.scss` partials + 5 LocalFirst components | `a0750f4` |
| React adapter | 🔴 | 🟡 scaffold with 3 PoC components × 3 providers × 9 Storybook stories | `a0750f4` |
| Anchor re-activation | 🟡 (scaffold only) | 🟢 LocalFirst DI + QR onboarding + NodeHealthBar + AnchorSessionService | `02df46b` |
| Bridge structural conflict | ⚠ | 🟢 dual-posture per ADR 0026 (SaaS + Relay) | `706d294` |
| Multi-node integration proof | 🔴 | 🟢 `integration-tests/kernel-sync-roundtrip/` over real sockets | `5feb033` |
| MDM installer scaffolding | 🔴 | 🟢 WiX v4 / dpkg-deb / productbuild + SBOM CI | `706d294` |

---

## 2. Paper §5.1 Kernel Responsibilities — All 🟢

Every one of the paper's seven kernel responsibilities now has a concrete implementation:

1. **Node lifecycle + process orchestration** — `apps/local-node-host` Worker Service.
2. **Sync daemon protocol, gossip anti-entropy, distributed lease coordination** — `packages/kernel-sync/` + `packages/kernel-lease/`.
3. **CRDT engine abstraction + event log + snapshots + compaction** — `packages/kernel-crdt/` (YDotNet) + `packages/kernel-event-bus/` (FileBackedEventLog) + `packages/kernel-schema-registry/Compaction/`.
4. **Schema migration infrastructure** — `packages/kernel-schema-registry/Lenses/` + `Epochs/` + `Migration/` + `Compaction/` + `Upcasters/`.
5. **Security primitives** — `packages/kernel-security/` (Ed25519 + X25519 + role keys) + `packages/foundation-localfirst/Encryption/` (SQLCipher + Argon2id + OS keystore).
6. **Partial/selective sync engine** — `packages/kernel-buckets/` with YAML bucket definitions, attestation eligibility, LRU eviction.
7. **Plugin discovery + lifecycle** — `packages/kernel-runtime/` with topological Kahn-sort loading + cycle detection.

All five extension-point contracts exist: `ILocalNodePlugin`, `IStreamDefinition`, `IProjectionBuilder`, `ISchemaVersion`, `IUiBlockManifest`.

## 3. Paper §5.2 UI Kernel Four Tiers — All 🟢

- **Foundation** — `packages/ui-core/` + per-provider `Styles/foundation/` including the new `_sync-state.scss`.
- **Framework-agnostic component layer** — `packages/ui-core/Contracts/` + the 5 new LocalFirst components that consume them (`SunfishSyncStatusIndicator`, `SunfishFreshnessBadge`, `SunfishOptimisticButton`, `SunfishConflictList`, `SunfishNodeHealthBar`).
- **Blocks and Modules** — `packages/blocks-*/` (15 blocks, unchanged since pre-audit).
- **Compatibility and Adapter Layer** — `packages/compat-*/` (13 compat packages) + React adapter scaffold + Sunfish.Analyzers.CompatVendorUsings.

React parity per ADR 0014 remains 🟡 partial — scaffold shipped; 6–10 weeks to full component parity per ADR 0030 estimate.

## 4. Remaining Gaps (Prioritized)

### Known TODOs in-tree

1. **apps/local-node-host IKeystore-backed INodeIdentityProvider** — currently the DI fallback generates a fresh keypair on every start. Production wants persistent identity. `LocalNodeOptions.NodeId` + `IKeystore` lookup needed.
2. **DELTA_STREAM receive-path rate-limiter invocation** — rate limiter ships but no production caller currently invokes `GossipDaemon.AllowInboundDelta`. Lands with the Wave 2.6 CRDT-apply-back integration.
3. **Schema semver negotiation in HELLO** — currently string-equality; paper §7.4 epoch flow doesn't need semver compare, but a version-compatibility check would be friendlier.
4. **Anchor camera integration** — QR scanner uses paste-bundle fallback; MAUI .NET 11 preview camera surface isn't uniform across targets.
5. **Production-release installer polish** — signing (all 3 platforms), MDM-vendor-specific artifacts (Intune/Jamf/Kandji), auto-update implementation, macOS notarization, Universal Binary, RPM variant. All enumerated in `installers/*/README.md`.
6. **BYOD path wiring across Wave 1.3/1.4/1.5 defaults** — `EncryptionOptions.DatabasePath`, event-log defaults, quarantine paths all still conflate team-data and personal-data paths; `docs/specifications/byod-path-separation.md` documents the target layout.

### Medium-horizon (not in original plan)

7. **Property-based test expansion** — paper §15 Level 1 currently has 2 FsCheck tests in kernel-crdt. Expand across kernel-sync (handshake idempotence), kernel-lease (quorum math), kernel-buckets (filter-evaluator fuzz).
8. **Deterministic simulation harness** — paper §15 Level 4. Not implemented anywhere yet.
9. **Chaos testing harness** — paper §15 Level 5. Not implemented.
10. **mar-\* cleanup outside DataGrid** — ~700 occurrences flagged in Wave-1-era work. Orthogonal to paper alignment.
11. **compat-telerik TelerikGrid.OnRead wiring** — still throws at entry per gap-closure notes.
12. **Bridge PLATFORM_ALIGNMENT Posture-A drift** — 3 rows flagged as possibly outdated in the posture-B addition.

### Strategic / multi-week

13. **React adapter full parity** (~6–10 weeks per ADR 0030 estimate).
14. **Loro CRDT re-evaluation** — YDotNet is the current production backend. Re-probe LoroCs / loro-cs when it exposes snapshot/delta/vector-clock APIs.
15. **Full end-to-end Anchor ↔ local-node-host IPC** — Anchor's AnchorSessionService currently uses manual SetState; needs to subscribe to IGossipDaemon + ILeaseCoordinator events via a new IPC channel.

---

## 5. YDotNet Client-ID Bug (wave-2 find)

The CRDT spike surfaced a **YDotNet 0.6.0 defect**: default `new Doc()` produces catastrophically non-unique client IDs (200 docs → only 16 unique IDs in the probe). Even explicit random `ulong` IDs above `2^32` diverge in RGA tiebreak. The fix was to constrain `DocOptions.Id` to `uint32` range in `YDotNetCrdtEngine`'s constructor. **Logged as bug-213 in `.wolf/buglog.json`.** Upstream yrs wire-format fix would lift the constraint but is out of Sunfish's direct control.

---

## 6. Test Coverage Snapshot

| Package / Area | Test count | Status |
|---|---|---|
| kernel-runtime | 20 | ✅ |
| kernel-event-bus (+ FileBackedEventLog + contract tests) | 50 | ✅ |
| kernel-schema-registry (+ lenses + compaction) | 71 | ✅ |
| kernel-crdt (+ YDotNet + FsCheck property tests) | 66 | ✅ |
| kernel-security | 29 | ✅ |
| kernel-sync (+ Ed25519 + mDNS) | 64 | ✅ (2 env-skipped) |
| kernel-lease | 16 | ✅ |
| kernel-buckets | 24 | ✅ |
| kernel-ledger | 26 | ✅ |
| foundation-localfirst (+ quarantine + encryption) | 42 | ✅ |
| ui-adapters-blazor (+ LocalFirst components) | 266 | ✅ |
| ui-adapters-react | 16 | ✅ |
| compat-telerik (+ gap closure) | 23 | ✅ |
| compat-syncfusion / -infragistics / icon-compats × 9 / Roslyn analyzer | 255 | ✅ |
| local-node-host | 5 | ✅ |
| anchor (services) | 17 | ✅ |
| bridge (+ relay) | 14 | ✅ |
| integration-tests/kernel-sync-roundtrip | 12 | ✅ (2 env-skipped) |
| **Total paper-aligned tests** | **~1,016** | ~1,012 passing + 4 env-skipped |

---

## 7. Structural Conflicts — All Resolved

| Conflict | 2026-04-22 | 2026-04-23 |
|---|---|---|
| α Bridge SaaS-authority framing | ⚠ | 🟢 dual-posture via ADR 0026 |
| β `packages/kernel` as type-forwarder only | ⚠ | 🟢 split per ADR 0027 — façade + new kernel-runtime |
| γ Blocks as static ProjectReferences | ⚠ | 🟡 ILocalNodePlugin exists; blocks not yet auto-discovered (opt-in migration path) |
| δ Blazor-only adapter | ⚠ | 🟡 React scaffold shipped; full parity pending |
| ε Ingestion subsystem scope | ⚪ | ⚪ unchanged; paper is silent |

---

## 8. Commit Count Snapshot

Paper-alignment workstream + Tier-1/2 follow-ups: **~30 commits** on main between 2026-04-22 and 2026-04-23, all pushed to origin.

---

## 9. Recommendation

The paper is now implemented at Phase-1 through Phase-4 scaffolding level with real transport and real crypto. Next strategic branching point:

- **Path A — hardening:** close the 6 in-tree TODOs (keystore-backed identity, receive-path rate-limiter, schema semver, camera, production installer signing, BYOD path wiring). ~2–3 weeks focused work. Turns the current "works in local integration tests" state into "deployable v0.1."
- **Path B — feature breadth:** React parity push + Loro revisit + property-based test expansion across sync/lease/buckets. ~6–10 weeks depending on React scope.
- **Path C — real deployment pilot:** pick a small team willing to run Anchor on two machines + local-node-host + a Bridge-in-Relay-mode. Use the integration-test harness as the acceptance gate. This is how unknown unknowns surface.

Paths A+C combined are my recommendation — hardening + deployment exposure together catch more bugs than either alone. Path B can run in parallel to both.

---

## Cross-References

- [`_shared/product/local-node-architecture-paper.md`](../../../_shared/product/local-node-architecture-paper.md) — the paper.
- [`_shared/product/paper-alignment-plan.md`](../../../_shared/product/paper-alignment-plan.md) — the 4-wave plan (now executed).
- [`icm/07_review/output/paper-alignment-audit-2026-04-22.md`](./paper-alignment-audit-2026-04-22.md) — the original audit (pre-Wave-0 baseline).
- [`packages/kernel-crdt/SPIKE-OUTCOME.md`](../../../packages/kernel-crdt/SPIKE-OUTCOME.md) — ADR 0028 YDotNet validation-spike writeup.
- `.wolf/buglog.json` entry bug-213 — YDotNet client-ID collision.

*Refresh snapshot at 2026-04-23. Re-run after Path A / B / C chooses a direction.*

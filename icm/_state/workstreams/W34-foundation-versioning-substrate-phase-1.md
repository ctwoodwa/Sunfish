---
sort_order: 37
number: 34
slug: foundation-versioning-substrate-phase-1
title: "Foundation.Versioning substrate Phase 1 (ADR 0028-A6+A7 contract surface)"
status: "built"
status_cell: "`built` (5 phases shipped 2026-05-01)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM ✓"
reference_cell: "https://github.com/ctwoodwa/Sunfish/pull/417 (P1 — scaffold + core types) + https://github.com/ctwoodwa/Sunfish/pull/418 (P2 — `ICompatibilityRelation` 6-rule engine) + https://github.com/ctwoodwa/Sunfish/pull/420 (P3 — `IVersionVectorExchange` handshake) + https://github.com/ctwoodwa/Sunfish/pull/421 (P4 — `IVersionVectorIncompatibility` audit + dedup) + Phase 5 (this PR — DI extension + apps/docs + ledger flip)"
---

## Notes

**Built 2026-05-01 across 5 phases.** P1: `Sunfish.Foundation.Versioning` package scaffold + `VersionVector` / `PluginVersionVectorEntry` / `VersionVectorVerdict` / `PluginId` + `AdapterId` (with `JsonConverter` `ReadAsPropertyName` / `WriteAsPropertyName` for dictionary-key contexts) / `ChannelKind` / `InstanceClassKind` (post-A7.6 reduced to `{ SelfHost, ManagedBridge }`) / `VerdictKind` / `FailedRule` 6-value enum; camelCase + `JsonStringEnumConverter` so `CanonicalJson.Serialize` round-trip is signature-stable per A7.8. P2: `ICompatibilityRelation` + `DefaultCompatibilityRelation` 6-rule engine (declared-order; first-failure-wins) with tunable `MaxKernelMinorLag` (default 2); symmetric evaluation + asymmetric-pathology resolution test (Stable ↔ Nightly) per A7.1. P3: `IVersionVectorExchange` + `InMemoryVersionVectorExchange` two-phase verdict-commit (both-peers-must-agree teardown) + receive-only mode (kernel_minor_lag > MaxKernelMinorLag) per A6.5. P4: `IVersionVectorIncompatibility` + `InMemoryVersionVectorIncompatibility` two-overload ctor (audit-disabled / audit-enabled with TenantId requirement; both-or-neither per W#32) + `VersionVectorAuditPayloads` factory (alphabetized bodies) + 2 new `AuditEventType` (`VersionVectorIncompatibilityRejected`, `LegacyDeviceReconnected`) + A7.4 dedup (`ConcurrentDictionary` keyed on (node, rule, detail) for 1h rejections + (node, lag) for 24h reconnects). P5: `AddInMemoryVersioning()` DI extension (audit-disabled + audit-enabled overloads; both-or-neither at the registration boundary too) + `apps/docs/foundation/versioning/overview.md` walkthrough + `foundation/toc.yml` row + this ledger flip. **59/59 foundation-versioning tests pass** (8 P1 + 21 P2 + 8 P3 + 15 P4 + 7 P5 DI). All 7 hand-off halt-conditions stayed off the trip-wire — none surfaced. Substrate-only; consumer wiring (W#23 / W#28 / future federation) remains separate workstreams.

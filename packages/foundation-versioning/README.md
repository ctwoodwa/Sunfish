# Sunfish.Foundation.Versioning

Foundation-tier substrate for cross-version Sunfish federation handshake — version vectors, compatibility rule engine, two-phase verdict-commit handshake, and audit emission with dedup.

Implements [ADR 0028 amendments A6 + A7](../../docs/adrs/0028-crdt-engine-selection.md) (post-A7 council-fixed surface).

## What this ships (Phase 1 — types + JSON shape)

### Wire-format types

- **`VersionVector(Kernel, Plugins, Adapters, SchemaEpoch, Channel, InstanceClass)`** — handshake-time payload exchanged between peers.
- **`PluginVersionVectorEntry(Version, Required)`** — per-plugin entry; carries the required-flag on the wire (per A7.3 augmentation) so rule-3 evaluation symmetrizes.
- **`VersionVectorVerdict(Verdict, FailedRule?, FailedRuleDetail?)`** — per-peer two-phase commit message (A7.1).
- **`PluginId`** + **`AdapterId`** — opaque string-wrapper record structs with property-name-aware JSON converters.
- **`ChannelKind`** (Stable/Beta/Nightly), **`InstanceClassKind`** (SelfHost/ManagedBridge per A7.6), **`VerdictKind`** (Compatible/Incompatible).
- **`FailedRule`** — 6-value enum naming the specific A6.2 rule that produced an Incompatible verdict.

### Canonical JSON

Serialized via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` per A7.8:

```json
{"adapters":{"blazor":"0.9.0"},"channel":"Stable","instanceClass":"SelfHost","kernel":"1.3.0","plugins":{"Sunfish.Blocks.PublicListings":{"required":true,"version":"1.0.0"}},"schemaEpoch":7}
```

Keys are camelCase + alphabetized; enum values serialize as their literal name; the `plugins` map values carry both `version` + `required`.

## Phases

- **Phase 1 (this PR):** scaffold + 9 model types + JSON shape verification (~6-8 tests).
- **Phase 2:** `ICompatibilityRelation` + `DefaultCompatibilityRelation` (6-rule engine per A6.2 / A7.3 augmentation).
- **Phase 3:** `IVersionVectorExchange` handshake protocol (A7.1 two-phase commit; A6.5 receive-only mode for legacy reconnects).
- **Phase 4:** `IVersionVectorIncompatibility` + 2 `AuditEventType` constants + A7.4 dedup (1h for incompat, 24h for legacy reconnect).
- **Phase 5:** DI extension + apps/docs + ledger flip.

## ADR map

- [ADR 0028](../../docs/adrs/0028-crdt-engine-selection.md) (CRDT engine selection) — amendments A6 + A7 are this substrate's spec.
- [ADR 0049](../../docs/adrs/0049-foundation-audit.md) — audit emission contract.

## See also

- [Sunfish.Foundation.Crypto.CanonicalJson](../foundation/Crypto/CanonicalJson.cs) — wire-format canonicalizer
- [Sunfish.Kernel.Audit](../kernel-audit/README.md) — audit substrate consumer

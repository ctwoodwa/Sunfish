# Foundation.Versioning substrate

`Sunfish.Foundation.Versioning` is the foundation-tier substrate for the Sunfish federation handshake — the building block behind cross-version peer compatibility, schema-epoch gating, plugin/adapter set intersection, channel-ordering rules, and legacy-device receive-only mode.

It implements [ADR 0028](../../../docs/adrs/0028-crdt-engine-selection.md) amendments A6 (federation handshake contract) and A7 (post-council corrections to A6).

## What it gives you

| Type | Role |
|---|---|
| `VersionVector` | The wire-format envelope a peer publishes at handshake time: schema epoch, kernel semver, channel, instance class, plugin map, adapter set. |
| `PluginVersionVectorEntry` | One plugin's `(Version, Required)` tuple inside a `VersionVector`. The `Required` flag (A7.3.2) lets rule 3 evaluate symmetrically without bundle manifests. |
| `VersionVectorVerdict` | The handshake result: `Compatible` or `Incompatible` + which `FailedRule` + a free-form detail string for diagnostics. |
| `FailedRule` | The 6-value rule taxonomy: `SchemaEpochMismatch`, `KernelSemverWindow`, `RequiredPluginIntersection`, `AdapterSetIncompatible`, `ChannelOrdering`, `InstanceClassIncompatible`. |
| `ICompatibilityRelation` | Pure-function evaluator: `(VersionVector local, VersionVector peer) → VersionVectorVerdict`. The 6 rules run in declared order; the first failure short-circuits. |
| `IVersionVectorExchange` | Two-phase verdict-commit handshake (A7.1): both peers evaluate independently; agreement proceeds, disagreement tears down cleanly from both sides. |
| `IVersionVectorIncompatibility` | Audit-emission + UX-surface contract for handshake rejections + legacy-device reconnects. Honours the A7.4 dedup windows in-process. |
| `DefaultCompatibilityRelation` / `InMemoryVersionVectorExchange` / `InMemoryVersionVectorIncompatibility` | Reference implementations; thread-safe; not durable. |

## Compatibility-rule order (A6.2)

Evaluated in this exact sequence; first failure wins:

1. **SchemaEpochMismatch** — schema epochs must match exactly.
2. **KernelSemverWindow** — the kernel-minor lag between peers must be ≤ `MaxKernelMinorLag` (default 2). When lag is in the legacy band, the trailing peer enters receive-only mode (A6.5) rather than tearing down.
3. **RequiredPluginIntersection** — every plugin marked `Required: true` on either side must appear (and version-match) on both sides. Optional plugins do not gate.
4. **AdapterSetIncompatible** — the adapter sets must agree on every adapter both peers know about.
5. **ChannelOrdering** — peers on incompatible channels (e.g., Stable ↔ Nightly) reject. Same-channel + same-major-band passes.
6. **InstanceClassIncompatible** — `SelfHost` and `ManagedBridge` may federate freely; the surface is open per A7.6 (the prior `Embedded` value was struck).

## Wire-format invariants (A7.8)

`VersionVector` round-trips through `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` with:

- camelCase property names (`kernel`, `schemaEpoch`, `channel`, `instanceClass`, `plugins`, `adapters`).
- `JsonStringEnumConverter` on `Channel` + `InstanceClass` so enum literals serialize as their string names (forward-compatible with future enum additions).
- Dictionary keys (`PluginId`, `AdapterId`) implement `ReadAsPropertyName` / `WriteAsPropertyName`, so the map serializes naturally.

The wire format is canonical and signature-stable: two peers building a `VersionVector` from the same inputs produce byte-identical JSON.

## Dedup discipline (A7.4)

`InMemoryVersionVectorIncompatibility` is a flood-guard, not a correctness invariant:

| Event | Tuple | Window |
|---|---|---|
| `VersionVectorIncompatibilityRejected` | `(remote_node_id, failed_rule, failed_rule_detail)` | 1 hour |
| `LegacyDeviceReconnected` | `(remote_node_id, kernel_minor_lag)` | 24 hours |

Within the window, repeated calls update no state. After the window, the next call fires fresh. Worst-case duplicate emission across process restarts is acceptable per A7.4 (the dedup state is a `ConcurrentDictionary`, not durable).

## API at a glance

```csharp
// Bootstrap (audit-disabled — test/bootstrap)
services.AddInMemoryVersioning();

// Bootstrap (audit-enabled — production; both IAuditTrail + IOperationSigner
// must already be registered, per the W#32 both-or-neither pattern)
services.AddInMemoryVersioning(currentTenantId);

// Evaluate a peer at handshake time
var exchange = sp.GetRequiredService<IVersionVectorExchange>();
var verdict = await exchange.EvaluateAsync(localVector, peerVector, ct);
if (verdict.Verdict == VerdictKind.Incompatible)
{
    var incompat = sp.GetRequiredService<IVersionVectorIncompatibility>();
    await incompat.RecordRejectionAsync(peerNodeId, verdict, ct);
    return; // tear the handshake down
}
```

## Audit emission

Two new `AuditEventType` discriminators ship with this substrate:

| Event type | Emitted by |
|---|---|
| `VersionVectorIncompatibilityRejected` | `IVersionVectorIncompatibility.RecordRejectionAsync` |
| `LegacyDeviceReconnected` | `IVersionVectorIncompatibility.RecordLegacyReconnectAsync` |

Payload bodies are alphabetized + opaque to the substrate (per the kernel-audit convention used by `TaxonomyAuditPayloadFactory` and `LeaseAuditPayloadFactory`). Audit emission is opt-in: pass an `IAuditTrail` + `IOperationSigner` + `TenantId` to the `InMemoryVersionVectorIncompatibility` constructor (or use the audit-enabled DI overload). Without them, the dedup state still tracks but no record fires.

## Phase 1 scope (this package)

- Substrate types, contracts, and reference implementations.
- 6-rule engine in declared order with first-failure-wins semantics.
- Two-phase verdict-commit handshake protocol.
- A7.4 dedup windows for both audit event types.
- 2 new `AuditEventType` discriminators in `Sunfish.Kernel.Audit`.
- `AddInMemoryVersioning()` DI extension (audit-disabled + audit-enabled overloads).

Subsequent phases (per ADR 0028 follow-ons): durable backends, `BusinessCaseBundleManifest.Requirements` integration once ADR 0007-A1 ships, the libp2p-style explicit-version-set kernel-compat model (OQ-A6.4 migration target), and the iOS A1.x envelope augmentation (per A6.11 + A7.5 — coordinated in W#23).

## Cohort lesson — pre-merge council on substrate ADRs

ADR 0028 amendments A6 + A7 are the canonical illustration of why substrate-tier ADRs go through pre-merge council review. A7 was authored as a corrective amendment after A6's council surfaced 7 structural defects in the original handshake spec (`InstanceClassKind` enum bloat, asymmetric rule 3 evaluation, missing dedup contract, PascalCase vs camelCase wire format, etc.). The cohort batting average of 15-of-15 substrate amendments needing council fixes — and the §A0 self-audit catch rate of 0-of-4 on ADR 0063 — is why pre-merge council on substrate ADRs is now canonical (per `feedback_decision_discipline.md`). Implementation-tier hand-offs like W#34 do not require pre-merge council; they ship behind `ready-to-build` ledger flips after XO authoring.

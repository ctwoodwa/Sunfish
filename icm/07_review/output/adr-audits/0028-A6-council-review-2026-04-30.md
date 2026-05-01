# ADR 0028 Amendment A6 — Council Review (Stage 1.5 Adversarial)

**Date:** 2026-04-30
**Reviewer:** research session, four-perspective adversarial council per UPF Stage 1.5 (substrate-tier compatibility-contract scope)
**Amendment under review:** [ADR 0028 — A6 "Version-vector compatibility contract for mixed-version Sunfish clusters"](../../../docs/adrs/0028-crdt-engine-selection.md) (PR #395, branch `docs/adr-0028-a6-version-vector-compatibility`, auto-merge intentionally DISABLED pre-council per cohort discipline)
**Companion intake:** [`2026-04-30_version-vector-compatibility-intake.md`](../../00_intake/output/2026-04-30_version-vector-compatibility-intake.md) (W#33)
**Companion ADR (depends on A6):** [ADR 0028 — A5 (cross-form-factor migration)](../../00_intake/output/2026-04-30_cross-form-factor-migration-intake.md) — sibling intake; A5 inherits A6's compatibility relation
**Driver discovery:** Mission Space Matrix §5.8 (`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`)

---

## 1. Verdict

**Accept with amendments. Grade: B (Solid).**

The architectural shape is sound: a tuple-typed version vector evaluated symmetrically at federation handshake-time with a small, enumerated set of compatibility rules and a one-sided receive-only escape valve for the long-offline-reconnect scenario is the right shape. The substrate-tier substantive gaps are: (1) the compatibility relation is described as if symmetric but is actually asymmetric in three places (channel partial-order; one-sided receive-only schemaEpoch-equality exception in A6.5; "either-side-required" plugin manifest evaluation in A6.2 rule 3) — and A6 doesn't reconcile what happens when the symmetry breaks, particularly when **A says compatible / B says incompatible**; (2) gRPC's 2-minor-deprecation-window precedent is being applied to a peer-to-peer CRDT cluster, but gRPC's analog doesn't carry over cleanly — the canonical P2P-CRDT prior art is libp2p's protocol-version negotiation + Apple CloudKit's record-zone schema versioning + Yjs/Automerge's own format-version handshake, none of which use a SemVer-window model; (3) A6.2 rule 3 cites a `required: true` field per ADR 0007 that does not exist on the module manifest — ADR 0007 has `requiredModules: string[]` on the **bundle** manifest and `required: bool` only on `ProviderRequirement`, not on the per-plugin manifest A6.2 rule 3 names; (4) A6.5's audit emission contract has no rate-limit or de-duplication semantics for high-throughput legacy-device reconnect storms, where each ack of a queued event from the legacy device could trigger a `LegacyDeviceReconnected` audit (not just one per session establishment); (5) iOS A1's append-only event envelope explicitly does NOT carry a `VersionVector` — A6.5 hints at "v0-compatible envelope" forward-compat but doesn't actually spec the version-vector semantic for the iOS append-only path; (6) `instanceClass = Embedded` is named for forward-compat with no real consumer (OQ-A6.3 acknowledges) — adding a tuple field whose only known compatibility behavior is "never blocks" creates surface-area drift.

Six required amendments + four encouraged. None block W#33 Stage 02 design (all are mechanical to apply); all should land before W#33 Stage 06 build emits its first `VersionVectorExchange` message on the wire. The cohort lesson holds: pre-merge council is dramatically cheaper than post-merge for substrate-tier amendments — A6 is exactly the class of work that benefits.

---

## 2. Findings (severity-tagged)

### F1 — One-sided incompatibility ("A compatible / B incompatible") is unspecified (Critical)

A6.2 says "BOTH peers MUST agree (the relation is symmetric for everything except `channel` ordering and one-sided `schemaEpoch` receive-only)" — flagging two known asymmetries — but doesn't reconcile what actually happens at the wire when the relation evaluates differently on each side. Consider the canonical case:

- **Peer A:** local kernel `1.3.0`, remote kernel `1.0.0`. Window check: `|3 - 0| = 3 > 2`. A evaluates **incompatible**.
- **Peer B:** local kernel `1.0.0`, remote kernel `1.3.0`. Same arithmetic on B's side. B also evaluates **incompatible**.
- Symmetric incompatibility — both close cleanly per A6.4.

But the channel rule is *not* symmetric:

- **Peer A:** local channel `stable`, remote channel `nightly`. Rule 5: `V1.channel ≤ V2.channel` → `stable ≤ nightly` → **A evaluates compatible.**
- **Peer B:** local channel `nightly`, remote channel `stable`. Rule 5: `V1.channel ≤ V2.channel` → `nightly ≤ stable`? **No. B evaluates incompatible.**

Now: A has decided to proceed to gossip. B has decided to close the federation session. A ships an event over the (still-open-on-A's-side) Noise channel; B has already torn down the channel (per A6.4 step 2 "close the federation session cleanly"). What does A see? An EOF mid-write? A6 doesn't say.

Same pathology applies to A6.2 rule 3 — "for each plugin `p` declared as `required: true` in either node's manifest". *Either node's manifest* is a load-bearing word. If A's manifest declares plugin `acme.payroll` required and B's manifest does not, A's evaluation of "required intersection covers" depends on which node's `required: true` declaration matters. The rule says "either" — meaning if A says `acme.payroll` is required and B doesn't have it, A rejects. But B's evaluation runs "is `acme.payroll` declared required by *either* manifest?" → yes (A declared it required; A told B that in the `VersionVectorExchange` message? — wait, the wire format in A6.3 sends `VersionVector`, which is the *runtime* tuple, not the *manifest* with `required: true` flags). The manifest-level `required: true` semantic is **not transmitted in the wire format A6.1 specs** — so B has no way to know whether A considered `acme.payroll` required. Each node only evaluates its own manifest's `required` flags against the received `VersionVector`. Asymmetry: A rejects (because A's manifest required `acme.payroll`); B accepts (because B's manifest didn't list `acme.payroll` at all).

Same handshake outcome as the channel case — A tears down, B is mid-handshake-completion, leaving B with a half-open Noise session it has to time out.

A6 needs to specify either: (a) **both peers must agree symmetrically** before federation proceeds — which means an explicit two-phase commit on the compatibility evaluation (each peer sends its evaluation result; only proceed when both are `compatible`), OR (b) **the rejecting peer sends an explicit `VersionVectorIncompatibilityRejection` message back** before tearing down the Noise session, so the other side gets a clean signal rather than an EOF. The latter is what gRPC does (status code in the trailer); the former is what TLS does (Alert + close_notify). Either is fine, but A6 has to pick. **Critical** — the substrate ships with a guaranteed-to-occur ambiguity on every channel-mismatch pairing (which is normal during a beta canary rollout — exactly when the substrate is most exercised).

### F2 — gRPC's 2-minor-deprecation precedent doesn't transfer to P2P CRDT clusters (Major)

A6.2 rule 2 anchors the kernel SemVer window on "the gRPC API design guide deprecation default." This is the wrong precedent to lean on. gRPC's deprecation window applies to **client-server** API contracts where:

1. The server controls the contract; clients adapt.
2. Versions are *announced* (deprecated-since headers; sunset timestamps).
3. Compatibility is *forward-only* — older clients keep working against newer servers, not the reverse.
4. There's a single canonical source-of-truth version namespace (the server's exposed surface).

A peer-to-peer CRDT cluster has none of those properties:

1. **No central authority.** Every node is both client and server simultaneously; both nodes' versions are equally authoritative.
2. **No deprecation announcements.** Nodes upgrade independently, on their owners' schedules; there's no central sunset notice.
3. **Bidirectional compatibility needed.** A node running `1.0.0` and a node running `1.3.0` need to interop *both directions* — older node's events flow to newer; newer node's events flow back to older.
4. **Format-version is more important than feature-version.** What matters is "can node B parse the events node A produces" — a structural question, not a SemVer-tier feature question.

The right prior art for P2P CRDT version negotiation is:

- **libp2p protocol negotiation:** each protocol has an explicit version string negotiated at multistream-select time. Compatibility is per-protocol, not per-stack. Multiple versions can be supported simultaneously by a single peer (the peer advertises all versions it supports; the negotiated version is the highest both sides support). No SemVer windows — explicit intersection of supported-version sets.
- **Apple CloudKit record-zone schema versioning:** each zone has a `recordZoneCapabilities` set; cross-version sync uses *additive-only* schema evolution where new fields are present-or-absent rather than gating compatibility. Old clients silently drop fields they don't understand; new clients tolerate their absence.
- **Yjs / Automerge format-version negotiation:** the Yjs protocol prefixes each sync message with a structural version byte; older Yjs reads forward-compatible-by-construction streams from newer Yjs because the format is designed for forward-compat. Compatibility is decided *per-message*, not per-session.
- **IPFS bitswap:** versioned protocol; nodes advertise which versions they support; intersection wins. Multi-version support per node is the norm.
- **Firebase Realtime DB:** schema-on-read; version is a soft hint, not a gate.

The common thread across genuinely-P2P prior art: **explicit set intersection of supported-versions, not arithmetic SemVer windows.** A node that says "I support kernel formats 1.1, 1.2, 1.3" can interop with a node that says "I support 1.0, 1.1, 1.2" by negotiating to 1.1 or 1.2 — both nodes simultaneously support multiple versions, and the negotiation picks the best mutual version.

A6's "same major + ≤2 minor window" model has two specific failure modes the libp2p / Yjs / IPFS / CloudKit models don't have:

- **The ratchet problem.** Once node A is updated to `1.5.0`, it can no longer talk to node B at `1.0.0` (5 minor versions away) — even if A's wire format hasn't actually changed since `1.0.0`. The arithmetic window doesn't track *actual* format compatibility, it tracks *time-since-bump*.
- **Forced upgrade cascades.** If owner upgrades phone (`1.5.0`) but couch device is `1.0.0`, the *static window* says "incompatible" even if both could interop if the explicit format version were checked. A6.5 mitigates this with one-sided receive-only, but only for `kernel_minor_lag > 2` (not for the broader question of what the *actual* format compatibility frontier is).

The 2-minor window is also tunable per OQ-A6.1, which is a hint that A6 itself recognizes the model is fragile — the right answer is probably "explicit `supported_kernel_formats: SemVer[]` array in `VersionVector`; intersection wins" rather than "tunable arithmetic window."

**Recommendation:** either (a) replace the SemVer-window model with an explicit `supported_kernel_formats: SemVer[]` field per the libp2p/IPFS/Yjs precedent — A6 keeps one rule that says "intersection non-empty; pick max", OR (b) keep the SemVer window but explicitly cite that A6 is choosing an *arithmetic* model over an *explicit-set* model for v0 simplicity, with the explicit-set model named as the Phase 3+ migration target. The latter is the smaller change for A6; the former is the right long-term answer. **Major** — substrate-tier choice with high downstream cost to revisit.

### F3 — A6.2 rule 3 cites a `required: true` field that doesn't exist on the plugin/module manifest (Major)

A6.2 rule 3: *"For each plugin `p` declared as `required: true` in either node's manifest (per ADR 0007 bundle-manifest-schema)…"*

Verification against ADR 0007 on `origin/main`:

- **`BusinessCaseBundleManifest`** has `requiredModules: string[]` (a list of module *keys* that must be installed for the bundle) and `optionalModules: string[]`. There is no `required: bool` field on the bundle.
- **`ProviderRequirement`** has `required: bool` — this is what determines whether a *provider* (payments, banking-feed, channel-manager) is required by a bundle. NOT a per-plugin/module flag.
- **`ModuleManifest`** has only `key, name, version, description, capabilities` — *no* `required: bool` field.

A6.2 rule 3 is conflating three distinct ADR 0007 concepts: bundle-level `requiredModules: string[]`, provider-level `required: bool` on `ProviderRequirement`, and a hypothetical per-module `required: true` flag that doesn't exist. The correct mapping is probably:

- "For each module `m` listed in `bundle.requiredModules` of either node's installed bundles…" — this matches ADR 0007's actual schema.

But this raises a deeper question: **is the `VersionVector.plugins` map even the right field to gate compatibility on?** A6.1's `plugins: Map<PluginId, SemVer>` includes all installed plugins, not just required ones. If the rule needs to consult bundle manifests to know which plugins are required, then either:

- The handshake also exchanges bundle-manifest data (currently not specified), OR
- Each node evaluates its own bundle-manifest's `requiredModules` against the received plugin map (so "required" is per-node, not negotiated), OR
- The `VersionVector.plugins` map is augmented with a per-plugin `required: bool` flag — which is what A6.2 rule 3 implicitly assumes but A6.1 doesn't ship.

The "either" word in rule 3 is also load-bearing-and-broken (per F1's analysis): if "either node's manifest" determines required-ness, but the wire format doesn't carry the required-ness flag, then each node's evaluation runs against its own manifest only, producing the asymmetric-evaluation pathology in F1.

**Required fix:** A6.1 augments the `plugins` map shape to `Map<PluginId, {version: SemVer, required: bool}>` AND A6.2 rule 3 is rewritten to evaluate "for each plugin `p` where `p.required = true` in *the locally-evaluating node's view* (i.e., the union of `p.required = true` from this node's plugin map AND `p.required = true` from the received node's plugin map): `p ∈ V1.plugins ∩ V2.plugins` AND SemVer window holds." This makes the rule symmetric (both nodes consult both maps' required-flags), eliminates the F1 asymmetry on this rule, AND aligns the wire format with what the rule actually needs to evaluate. **Major** — citation is wrong AND the wire format doesn't carry the data the rule needs.

### F4 — Audit-emission scaling under high-throughput legacy-device reconnect storms (Major)

A6.5 specifies `LegacyDeviceReconnected` audit emission for the receive-only mode. The audit-emission contract has scaling problems under realistic legacy-reconnect storms:

**Scenario:** the "couch device offline 3+ major versions" wakes after 6 months. It has accumulated ~5,000 queued events (per ADR 0028-A1's iOS event queue contract — `device_local_seq` monotonic; no compaction on the device per A1.2). It reconnects to the current Anchor, handshakes (A6.5 detects N > 2 minor lag, accepts receive-only), and starts uploading the 5,000-event backlog.

**Open question A6 doesn't answer:** when does `LegacyDeviceReconnected` fire?

- (a) Once per Noise-session establishment? Then it fires once per reconnect. Reasonable. But what if the couch device's network is flaky and it reconnects 50 times during the upload (every flap re-establishes the Noise session)?
- (b) Once per `VersionVectorExchange` message? Same as (a) effectively — these happen once per session per A6.3.
- (c) Once per event uploaded? Then 5,000 events generates 5,000 `LegacyDeviceReconnected` audit records, all with identical `(remote_node_id, remote_kernel, kernel_minor_lag)` tuples — pure noise.

A6.5 says "A emits `AuditEventType.LegacyDeviceReconnected` with `(remote_node_id, remote_kernel, kernel_minor_lag)`" — the *what*, but not the *when* or *with-what-frequency*. Reading the audit-substrate ADR (0049): the audit trail is append-only; deduplication is the producer's responsibility. So the producer (A6's exchange handler) needs to specify de-duplication explicitly.

**Adjacent problem:** `VersionVectorIncompatibilityRejected` (A6.4) has the same scaling issue but in the opposite direction. If a misconfigured node retries-loop on incompatibility, each retry generates an audit record. A6.4 says "neither should retry-loop" — but this is a SHOULD, not a MUST. A misbehaving / older / forked Sunfish implementation could retry; the substrate needs an at-receive-side rate limit, not an honor-system request.

**Recommended fix:** A6.5 specifies "`LegacyDeviceReconnected` emits at most once per `(remote_node_id, kernel_minor_lag)` tuple per 24-hour window; subsequent reconnects within the window are subsumed." A6.4 specifies the same kind of dedup for `VersionVectorIncompatibilityRejected`: "at most once per `(remote_node_id, failed_rule, failed_rule_detail)` tuple per 1-hour window." Both rate limits are enforced at the audit-emission boundary (the `VersionVectorAuditPayloads` factory consults a recent-emissions cache before constructing the payload). This pattern is already established in Sunfish — `EventLogBackedAuditTrail` could grow a `EmitWithDeduplicationAsync` overload, OR the dedup happens at the call site. Either way, A6.5 + A6.6 needs to spec it. **Major** — without it, substrate ships an audit-flood foot-gun.

### F5 — iOS append-only path's version-vector semantic is unspecified (Major)

A6.5's "v0-compatible envelope (per ADR 0028-A1 iOS event envelope contract)" hints at iOS forward-compat but skips the actual semantics. Reading ADR 0028-A1.2 (origin/main, post-A2.1 fix) the iOS append-only envelope is:

```text
{ device_local_seq: uint64, captured_at: ISO 8601 UTC, device_id: string,
  event_type: enum, payload: JSON-canonical bytes }
```

**This envelope does NOT contain a `VersionVector`.** The iPad doesn't ship its kernel version, schema epoch, plugin set, or channel in each event. So when the Anchor merge boundary (per A1.2 "Conflict resolution at Anchor merge boundary") receives an event from the iPad, the merge service has no per-event version-vector context — it only knows the iPad's version-vector at *handshake time*, not at *event-capture time*.

This matters because:

- **The iPad may capture an event at iOS app version `v1`, then upgrade to `v2`, then upload that event later.** The event was captured under `v1` semantics; uploaded under a `v2` handshake. Which version-vector applies to the merge logic?
- **`schemaEpoch` is supposed to gate compatibility (A6.2 rule 1).** If the iPad captured an event at `schemaEpoch=7` and Anchor is now at `schemaEpoch=8`, is the event applicable? Per A6.2 rule 1, no — schema-epoch crossings are coordinated cutovers. But the iPad's queued event has no per-event epoch tag; only the iPad's *current* (post-upgrade) epoch is on the wire at handshake.
- **A6.5's one-sided receive-only mode** is supposed to handle "couch device offline 3+ major versions." But for the iPad-specific case, the device may have been *online and capturing* throughout the period — its events span multiple of its own kernel versions, not just one historical version-vector.

The iOS append-only path needs *per-event version-vector tagging* (or an explicit declaration that the iPad's `device_local_seq` ordering implicitly anchors all events to the device's version-vector at last-handshake-with-Anchor time, with a forward-compat guarantee that older-captured events remain replayable under newer Anchor schemas).

**Recommended fix:** A6 adds a sub-section "A6.11 — iOS append-only path: per-event version-vector semantics" that specifies one of two options:

- **Option α (smaller):** the iPad tags each event with `captured_under_kernel: SemVer` (the kernel version that was running when the event was captured). The merge boundary checks `event.captured_under_kernel` against Anchor's current kernel using A6.2 rule 2's window. Events that fail the window-check are sequestered for human review, not dropped.
- **Option β (larger):** the iPad's append-only envelope is itself versioned (`envelope_version: uint8`). New iPad releases bump the envelope version; Anchor maintains a per-envelope-version replay path; old envelope versions are deprecated on a timeline (10 years or one major-format-cutover, whichever is sooner). This is the libp2p/Yjs precedent applied to the queue itself.

A6.5's "v0-compatible envelope" implicitly assumes Option β but doesn't spec the version field, the deprecation timeline, or the replay path. The iOS A1 envelope as currently defined has no version field. This is load-bearing-and-unresolved. **Major** — A6.5's claim does not hold without explicit envelope-versioning.

### F6 — `instanceClass = Embedded` is forward-compat without a real consumer (Encouraged → Major-on-reflection)

OQ-A6.3 acknowledges the issue: "is `instanceClass = Embedded` a real Phase 2.1 concept or is it premature? A6.1 names it for forward-compat; the compatibility relation treats it as informational; if no real Embedded instance ships in the next ~6 months, defer-but-don't-remove." This is exactly the YAGNI anti-pattern that creates surface-area drift:

- The tuple field is now part of the wire format. Removing it later requires either a flag-day deprecation or an explicit envelope-version bump.
- "All instance-classes are mutually compatible at A6 layer" (A6.2 rule 6) means the field literally never gates federation — so it's pure metadata, not part of the compatibility relation. Why is it in the version-vector tuple at all rather than in a separate `node_capabilities` exchange?
- Separating "instance class" from "version vector" is consistent with the libp2p prior art: capabilities and versions are different concerns. A node's *capabilities* (instance class, deployment mode, hosted-relay flag) belong in a `NodeCapabilities` exchange separate from the version vector. A6.3 already has the handshake mechanism — adding a second message would not be expensive.

This was the strongest line from the Forward-Compat reviewer: **adding a tuple field with no real consumer is exactly the kind of substrate-tier surface-area drift that paper §3 explicitly counsels against** ("the kernel is small; resist the urge to grow it"). The substrate-tier defense is to ship the smaller surface and grow it when a real consumer materializes.

Counter-argument (worth naming): **forward-compat for binary-stable wire formats is hard, and adding a field later is more expensive than reserving it now.** This is the Cap'n Proto / Protobuf argument — reserved fields are cheap. If Sunfish's CanonicalJson supports unknown-key tolerance (does it?), then adding the field later is also cheap; if not, then reserving it now is the right call.

**Verification:** does `CanonicalJson.Serialize` round-trip cleanly when consumers add unknown fields? Reading the source (`packages/foundation/Crypto/CanonicalJson.cs`):

```csharp
public static byte[] Serialize<T>(T value) {
    var node = SerializeToNodeByRuntimeType(value);
    var sorted = SortKeys(node);
    return NodeToBytes(sorted);
}
```

Serialization preserves whatever fields exist on the type at runtime. Deserialization is `JsonSerializer`-based; per the System.Text.Json defaults, unknown fields are *silently ignored* on deserialize unless `JsonSerializerOptions.UnmappedMemberHandling = Disallow` is set. Sunfish's CanonicalJson uses `WriteIndented = false` and `SkipValidation = true` but doesn't pin `UnmappedMemberHandling`. So forward-compat by adding fields is the default behavior — unknown-key tolerance holds.

**This means the YAGNI argument wins over the reserve-fields argument:** adding a field later is cheap (unknown-key tolerance preserves cross-version compat). Strip `Embedded` from A6.1 until a real Embedded consumer ships. **Promoted from Encouraged to Major** because substrate-tier surface-area drift is exactly the cohort-discipline anti-pattern A6 should not be allowed to ship under cohort-discipline review.

### F7 — Handshake is named "in established Noise channel after auth, before gossip" but Noise channel auth path isn't specified by ADR 0027 (Minor)

A6.3 step 2: "Noise session establishment (per ADR 0027) completes; both peers have authenticated each other via Ed25519 root keypair (per ADR 0032)."

ADR 0027 (kernel-runtime-split) defines the kernel-runtime separation but does NOT specify a Noise pattern. Noise is a *transport-tier* concern (Noise_XX, Noise_IK, Noise_NK, etc. — each pattern has different anonymity / known-identity / mutual-auth properties). The XX pattern provides mutual auth of static keys; NK provides server-static-known + client-anonymous. ADR 0061 (three-tier-peer-transport) is the place Noise patterns *would* be specified, but reading ADR 0061 on `origin/main` (post-A1–A4 amendments) it specifies the three-tier transport architecture without naming a Noise pattern either.

A6.3 is asserting a Noise-pattern-having transport that no upstream ADR has specified. This is a small forward-reference — Stage 06 will need a transport-tier ADR amendment to actually pick a Noise pattern. Alternatively, A6 could be deliberately transport-pattern-agnostic ("any authenticated channel suffices; A6 just needs to know it's authenticated").

**Recommended fix:** A6.3 reword to "Noise session establishment OR equivalent authenticated transport (per ADR 0061's three-tier transport model + a future transport-pattern ADR; A6's contract is transport-pattern-agnostic at the substrate tier — it requires only mutual authentication of Ed25519 root keypairs)." Drops the load-bearing "per ADR 0027" claim that ADR 0027 doesn't actually carry. **Minor** — clarification, not a flaw.

### F8 — JSON canonical encoding key naming inconsistency (Minor)

A6.1 names the type-level fields as `kernel`, `plugins`, `adapters`, `schemaEpoch`, `channel`, `instanceClass` (PascalCase / camelCase mixed in the type signature). But the JSON canonical shape A6.1 ships uses `schema_epoch`, `instance_class` (snake_case for multi-word; flat for single-word). Reading other Sunfish JSON canonical shapes (e.g., audit payload keys in `LeaseAuditPayloadFactory.cs`):

```csharp
public static AuditPayload Drafted(Lease lease, ActorId actor) =>
    new AuditPayload(new Dictionary<string, object?>
    {
        // … keys are mostly camelCase or PascalCase per Sunfish convention
    });
```

Spot check: most Sunfish audit payloads use **camelCase** for multi-word keys (`tenantId`, `actorId`), not snake_case. A6.1's JSON example uses `schema_epoch`, `instance_class` — snake_case. This is a stylistic inconsistency with the rest of the codebase. CanonicalJson.Serialize sorts keys alphabetically regardless of casing convention, so the serialization works either way; but consumers of the JSON (audit-payload factories, downstream tools) will need to know which casing applies.

**Recommended fix:** A6.1 clarifies casing — either commit to camelCase (`schemaEpoch`, `instanceClass`) matching the rest of Sunfish's canonical shapes, OR cite a specific Sunfish-convention reference saying snake_case is correct here (e.g., wire-format-style for cross-process/cross-language interop). The default expectation reading the codebase is camelCase. **Minor** — purely stylistic, but worth pinning before Stage 06 ships the canonical encoder.

### F9 — `failed_rule` enum naming overlaps with existing recovery-substrate `FailedRule` if any exists (Minor / verification needed)

A6.6 introduces `Sunfish.Foundation.Versioning.FailedRule` enum. Spot check via `git grep "enum FailedRule" origin/main -- '**/*.cs'`:

```text
(no results)
```

Clear — no existing `FailedRule` enum anywhere in Sunfish. Naming is unambiguous. **Minor** — verification step, no actual finding. (This is the kind of spot-check the cohort batting average says to run.)

### F10 — Channel partial order is named "stable < beta < nightly" but the rule is "more permissive can read stable" (Encouraged)

A6.1 says: "`channel` is partial-order: `stable < beta < nightly` (more permissive channels can read stable data; reverse is not implied)." A6.2 rule 5 then says "`V1.channel ≤ V2.channel` OR `V1.channel == V2.channel`. Stable-channel nodes can read from beta/nightly nodes."

These two statements are compatible but expressed with subtle direction-of-arrow confusion:

- A6.1 says `stable < beta < nightly` (nightly is "greater") AND "more permissive channels can read stable data" → "more permissive" = "higher in the partial order" = nightly. So nightly reads stable. Yes.
- A6.2 rule 5 says `V1.channel ≤ V2.channel` is OK. If V1=stable, V2=nightly, then `stable ≤ nightly` is `0 ≤ 2` = true. So stable can connect to nightly. → "Stable-channel nodes can read from beta/nightly nodes."

But this is semantically weird: typically, "stable reads from nightly" is the *less* safe direction (stable production picking up beta/nightly state). The rationale A6.2 gives is "in case of beta-channel canary deployments testing forward-compat" — i.e., the canary case where you *want* stable to receive nightly events to validate them.

The rule as stated permits stable to **receive** nightly events. But A6.2 rule 5 also says "the reverse is forbidden by default to prevent stable production data from being polluted by unstable beta state." Wait — which direction is "the reverse"? If V1=nightly, V2=stable, then V1.channel ≤ V2.channel is `2 ≤ 0` = false. So nightly can't connect to stable. Meaning **nightly nodes can't write to stable nodes.** OK — nightly-to-stable direction is forbidden; stable-to-nightly is allowed.

This is actually backwards from the typical concern. The typical concern is *production data being polluted by beta state* — i.e., nightly state flowing INTO stable. A6.2 rule 5 forbids that direction. Good. But A6.2 *permits* stable state flowing INTO nightly — which is fine; nightly has more-permissive expectations.

The asymmetry IS correct, but the prose explanation is hard to follow because of the direction-of-arrow ambiguity. The phrase "Stable-channel nodes can read from beta/nightly nodes" is also weird — typically you'd say "beta/nightly nodes can read from stable" (because beta/nightly are the *consumer* of validation events from stable).

**Recommended fix:** A6.2 rule 5 reword: *"A node on a more-permissive channel (nightly > beta > stable) MAY federate with a node on a less-permissive channel (consumer-direction); a less-permissive node MUST NOT accept federation from a more-permissive node by default (this would pollute the less-permissive node's state with state from a less-tested code path). Operators MAY override per-deployment via `--allow-channel-downgrade`."* Cleaner direction-of-arrow framing. **Encouraged.**

### F11 — `Sunfish.Kernel.Audit` is the right namespace per A6.6 — verified (no finding, positive spot-check)

A6.6: "Two new `AuditEventType` constants in `Sunfish.Kernel.Audit`: `VersionVectorIncompatibilityRejected`, `LegacyDeviceReconnected`."

Verification:

```bash
git ls-tree -r --name-only origin/main packages/kernel-audit
# → packages/kernel-audit/AuditEventType.cs (Sunfish.Kernel.Audit namespace)
# → packages/kernel-audit/IAuditTrail.cs
# → packages/kernel-audit/Sunfish.Kernel.Audit.csproj
```

Confirmed: `Sunfish.Kernel.Audit` is the actual namespace (per `kernel-audit/Sunfish.Kernel.Audit.csproj`). The package contains `AuditEventType` (a `readonly record struct` with string `Value`, with constants like `KeyRecoveryInitiated`, `PaymentAuthorized`, etc.). New constants would be added there. A6.6 is correct on this citation. **No finding** — but logged because the cohort-discipline rule says "spot-check positive-existence claims" too. This citation passes.

### F12 — `CanonicalJson.Serialize` round-trip claim is verified (no finding, positive spot-check)

A6.1 says canonical encoding is via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize`. Verification:

```bash
git show origin/main:packages/foundation/Crypto/CanonicalJson.cs | grep "public static.*Serialize"
# → public static byte[] Serialize<T>(T value)
# → public static byte[] SerializeSignable<T>(T payload, ...)
```

Confirmed: `Serialize<T>(T value)` exists with that exact signature. The implementation sorts keys alphabetically, no whitespace, UTF-8 output. Also confirmed the unknown-field-tolerance behavior on deserialization (System.Text.Json defaults silently ignore unmapped members) — relevant to F6's analysis. **No finding** — citation passes.

### F13 — A4 retraction claim about `JsonCanonical` is consistent with A6.1's pin (no finding)

ADR 0028-A4 (already on `origin/main`) retracted A2.10's false-positive claim that `Sunfish.Foundation.Canonicalization.JsonCanonical` exists, pinning instead to `Sunfish.Foundation.Crypto.CanonicalJson.Serialize`. A6.1 cites `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` directly — consistent with the post-A4 ground truth. A6 has correctly absorbed the A4 retraction. **No finding** — but worth noting because this is the kind of cross-amendment consistency that the cohort discipline *is* catching.

### F14 — All 7 cited ADRs verified Accepted on origin/main (no finding, positive spot-check)

Per the cohort-discipline rule "verify positive-existence claims":

| ADR | Status header on `origin/main` | Verdict |
|---|---|---|
| 0001 schema-registry-governance | Accepted | Pass |
| 0007 bundle-manifest-schema | Accepted | Pass |
| 0027 kernel-runtime-split | Accepted (2026-04-22) | Pass |
| 0031 bridge-hybrid-multi-tenant-saas | Accepted (2026-04-23) | Pass |
| 0032 multi-team-anchor-workspace-switching | Accepted (2026-04-23) | Pass |
| 0049 audit-trail-substrate | Accepted (2026-04-27) | Pass |
| 0061 three-tier-peer-transport | Accepted (2026-04-29) — A1–A4 landed | Pass |

All 7 verified. ADR 0061 specifically: A6 cites it as Accepted, which is correct per the post-A3 (of ADR 0028) retraction that fixed the prior false-vapourware claim. **No finding** — positive-existence spot-check confirms cohort discipline applied correctly.

---

## 3. Recommended amendments

### A1 (Required) — Specify symmetric-evaluation handshake protocol

Replace A6.3 step 3's "BOTH peers MUST agree" with an explicit two-phase commit:

> **3a. Each peer evaluates compatibility against the received version-vector (per A6.2) AND records its own verdict (`compatible` | `incompatible`).**
>
> **3b. Each peer sends a `VersionVectorVerdict` follow-up message** carrying its verdict + (if incompatible) the `failed_rule` + `failed_rule_detail` per A6.4. This message is sent inside the same Noise channel before any teardown.
>
> **3c. Federation proceeds iff BOTH verdicts are `compatible`.** If either side reports `incompatible`, both peers MUST close the federation session cleanly per A6.4 (audit-emission + UX surface). No half-open state.
>
> **3d. The asymmetric rules (channel ordering rule 5; one-sided receive-only rule 6 of A6.5) are evaluated by each peer independently against the received vector, but BOTH peers must independently agree the asymmetry resolves to `compatible` for federation to proceed. This eliminates the asymmetric-evaluation pathology where A says compatible / B says incompatible.**

This addresses F1's Critical finding. **Required.**

### A2 (Required) — Reframe SemVer compatibility window with explicit P2P prior art

Replace A6.2 rule 2's "matches the gRPC API design guide deprecation default" with a more honest framing:

> **Rule 2 — Kernel SemVer compatibility window (v0 model).** `V1.kernel.major == V2.kernel.major` AND `|V1.kernel.minor - V2.kernel.minor| ≤ 2`. **Rationale:** A6 ships an arithmetic-window v0 model for simplicity. The canonical P2P prior art (libp2p protocol-version negotiation, Apple CloudKit zone-capability sets, Yjs/Automerge format-version handshake, IPFS bitswap version intersection) uses **explicit supported-version sets with intersection-wins negotiation** rather than arithmetic windows. The arithmetic-window model is a v0 simplification that will be revisited in a Phase 3+ amendment when (a) kernel format actually changes (currently the format is stable; the SemVer minor is mostly tracking feature additions, not breaking changes), or (b) a real cross-version interop case demonstrates the arithmetic model is too restrictive. Track at OQ-A6.4 (added below).
>
> Configuration: tunable per-deployment (per OQ-A6.1) via a `MaxKernelMinorLag: uint8` setting; default 2.

Add a new open question:

> **OQ-A6.4:** When does Sunfish migrate from the arithmetic-window kernel-compat model to the libp2p-style explicit-version-set model? Trigger candidates: (a) first kernel format-breaking change ships (forces explicit-set anyway), (b) field deployment surfaces an arithmetic-window false-rejection (legacy device with a still-valid format being rejected purely on minor-distance), (c) Phase 3+ when CRDT-on-mobile lands and per-message versioning becomes the natural pattern. **Default expectation:** revisit at Phase 3.

This addresses F2's Major finding by being honest about the v0 model's limits. **Required.**

### A3 (Required) — Fix A6.2 rule 3 plugin-required citation + augment wire format

Two coupled changes:

**A3.1:** Rewrite A6.2 rule 3 to cite ADR 0007's actual schema:

> **Rule 3 — Required-plugin intersection.** For each plugin `p` listed in `BusinessCaseBundleManifest.requiredModules` of any bundle installed on either peer (per ADR 0007's bundle manifest), `p ∈ V1.plugins ∩ V2.plugins` AND its SemVer comparison passes the same major + 2-minor rule. Plugins not listed in any peer's bundle's `requiredModules` (i.e., installed-but-optional plugins) MAY be missing from one side without blocking federation.

**A3.2:** Augment A6.1's `plugins` map shape so the wire format actually carries the required-flag (per F1+F3 analysis):

> **A6.1 (revised):** the `plugins` field becomes `Map<PluginId, PluginVersionVectorEntry>` where `PluginVersionVectorEntry = { version: SemVer, required: bool }`. The `required` flag is set per-bundle: for each plugin `p` in any installed bundle's `requiredModules`, the entry's `required = true`; otherwise `required = false`. Both peers receive the same canonical view of which plugins each declares required. A6.2 rule 3's evaluation is then symmetric: both peers consult the union of required-flags from both sides' plugin maps.

This addresses F3's Major finding (citation correctness) and reinforces F1's symmetric-evaluation fix (rule-3 specifically). **Required.**

### A4 (Required) — Audit-emission de-duplication for both incompatibility-rejection AND legacy-reconnect

Add a new sub-section A6.5.1:

> **A6.5.1 — Audit-emission rate limits.**
>
> Both `VersionVectorIncompatibilityRejected` and `LegacyDeviceReconnected` audit events use de-duplication windows to prevent audit-flood under realistic operational scenarios:
>
> - **`VersionVectorIncompatibilityRejected`:** at most one emission per `(remote_node_id, failed_rule, failed_rule_detail)` tuple per **1-hour rolling window**. Subsequent rejections from the same misconfigured peer with the same failure are subsumed.
> - **`LegacyDeviceReconnected`:** at most one emission per `(remote_node_id, kernel_minor_lag)` tuple per **24-hour rolling window**. A reconnecting legacy device that flaps 50 times in an hour generates 1 audit, not 50.
>
> Implementation: `VersionVectorAuditPayloads` factory class consults a recent-emissions cache (in-memory; per-node-bounded; eviction-safe) before constructing the payload. Cache resets are not load-bearing (worst case under reset: one duplicate emission in the de-dup window). The audit-substrate behavior is independent of dedup — the dedup decision is made at the *emission* boundary, not the *substrate* boundary.

Add to A6.6 acceptance criteria:

- [ ] De-duplication tests for both audit types (rapid-reconnect storm; misconfigured-peer-retry storm)
- [ ] Cache-reset behavior test (at most one duplicate in the de-dup window after reset)

This addresses F4's Major finding. **Required.**

### A5 (Required) — Spec iOS append-only path's per-event version-vector semantic

Add a new sub-section A6.11:

> **A6.11 — iOS append-only path version-vector semantics.**
>
> Per ADR 0028-A1's iOS event-queue contract (post-A2 fixes), the per-event envelope is `{ device_local_seq, captured_at, device_id, event_type, payload }` — **without** a per-event `VersionVector`. A6 specifies how cross-version interop is preserved on this path:
>
> 1. **Per-event capture-context tagging.** Each event is augmented at capture time with `captured_under_kernel: SemVer` (the kernel version running on the iPad when the event was captured) and `captured_under_schema_epoch: uint32` (the schema epoch the iPad was on at capture time). These two fields are added to the iOS A1 envelope. Per F12-verified `CanonicalJson.Serialize` unknown-field-tolerance, this is a forward-compat addition: older receivers ignore the fields silently; newer receivers consume them.
> 2. **Merge-boundary evaluation.** When Anchor's merge service consumes an iPad event, it evaluates A6.2 rule 2 against `event.captured_under_kernel` (not against the iPad's *current* version-vector at upload time). Rule 1 is evaluated against `event.captured_under_schema_epoch`.
> 3. **Cross-epoch events are sequestered, not dropped.** When an event's `captured_under_schema_epoch` doesn't match Anchor's current epoch, the event is sequestered to a `LegacyEpochEvent` audit-record + held for human review (the iPad captured an event under an old epoch; an operator decides whether the migration logic can safely apply it). Hard-dropping is not the default; epochs change rarely and silent loss of captured field-data is unacceptable.
> 4. **Forward-compat:** the iOS A1 envelope's evolution path is `envelope_version: uint8` added in a future amendment when the envelope shape itself needs to change (vs adding optional fields, which CanonicalJson tolerance handles for free). Until then, additive-only field evolution is the pattern.

Update ADR 0028-A1 referenced by A6 → noting that A1's envelope is augmented per A6.11 (this requires a coordinated A1.x amendment, but A6 declares the augmentation needed; A1.x lands in a follow-up PR). Add to A6.6 acceptance criteria:

- [ ] iOS A1 envelope test that includes `captured_under_kernel` + `captured_under_schema_epoch` fields with round-trip via CanonicalJson
- [ ] Merge-boundary test for cross-epoch event sequestration

This addresses F5's Major finding. **Required.**

### A6 (Required) — Strip `instanceClass = Embedded` from A6.1 v0 surface

Remove `Embedded` from `A6.1`'s `instanceClass` enum:

> **A6.1 (revised):** `instanceClass: enum { SelfHost, ManagedBridge }`. The previously-named `Embedded` value is **deferred** until a real Embedded consumer ships. Per F12-verified `CanonicalJson.Serialize` unknown-field-tolerance, adding the value later is forward-compat (older receivers reject unknown enum values; the migration is an additive enum bump). YAGNI applied per cohort-discipline anti-pattern-13 (premature precision).

Drop OQ-A6.3 (no longer relevant; the question has been answered "defer per YAGNI"). The 1-line acknowledgment in A6.10 about `Embedded` instance-class also goes.

**Counter-point worth surfacing:** If forward-compat across enum-bumps is *not* tolerant in CanonicalJson (older receivers may reject an unknown enum value rather than silently ignore), then reserving the value now is the safer call. Verification needed at A5 implementation hand-off:

- Test: encode `VersionVector` with a hypothetical-future enum value via `CanonicalJson.Serialize`; deserialize on a `ReadEnumWithFallback = false` consumer; observe behavior.

If the test reveals that adding enum values is *not* forward-compat in v0 CanonicalJson, then A6 reverses this amendment and the YAGNI argument loses to the reserve-fields argument. **Required pending verification.**

This addresses F6's promoted-to-Major finding. **Required.**

### A7 (Encouraged) — Reword A6.3 to be transport-pattern-agnostic

Replace A6.3 step 2 with:

> **2. The transport channel is established and authenticated** (Noise pattern per a future transport-tier ADR, or any equivalent authenticated channel; A6's contract is transport-pattern-agnostic at the substrate tier — it requires only mutual authentication of Ed25519 root keypairs per ADR 0032). ADR 0027 defines the kernel-runtime split that makes the kernel transport-pluggable; the actual Noise pattern selection is downstream of A6's compatibility contract.

This addresses F7's Minor finding by removing the load-bearing claim that ADR 0027 specifies a Noise pattern (it doesn't). **Encouraged.**

### A8 (Encouraged) — Pin canonical-JSON casing to camelCase

A6.1's JSON example uses `schema_epoch` and `instance_class` (snake_case for multi-word keys). Reword to match Sunfish convention:

```json
{
  "kernel": "1.3.0",
  "plugins": {"sunfish.blocks.maintenance": {"version": "1.2.0", "required": true}, "sunfish.blocks.public-listings": {"version": "1.0.0", "required": false}},
  "adapters": {"blazor": "1.3.0", "react": "1.1.0"},
  "schemaEpoch": 7,
  "channel": "stable",
  "instanceClass": "selfHost"
}
```

(Also reflects A3.2's plugin-shape change.) **Encouraged.**

### A9 (Encouraged) — Reword A6.2 rule 5's channel direction explanation

Per F10's analysis, A6.2 rule 5 reword:

> **Rule 5 — Channel ordering.** A node on a more-permissive channel (`nightly > beta > stable`) MAY federate with a node on a less-permissive channel (consumer-direction; e.g., a nightly-canary node receiving stable production state to validate against). A node on a less-permissive channel MUST NOT accept federation from a more-permissive node by default — this would pollute the less-permissive node's state with state captured under a less-tested code path. Operators MAY override per-deployment via `--allow-channel-downgrade` (e.g., for staged rollout testing where stable receives beta-channel events deliberately). **Configurable at the operator level** (production deployments typically pin `channel == stable` strictly, blocking even the default-allowed direction).

Cleaner direction-of-arrow framing. **Encouraged.**

### A10 (Encouraged) — Add a "what-this-doesn't-cover" sub-section to A6

A6 covers static compatibility evaluation at federation handshake time. It does NOT cover:

- Dynamic mid-session compatibility re-evaluation if a peer upgrades during a long-lived federation (out of scope; periodic re-handshake addresses this; spec belongs in a future transport-tier ADR)
- Schema-epoch migration mechanics (A6 only checks epoch equality; the actual `sunfish migrate` command's contract is ADR 0001 and a future migration-tooling ADR)
- Plugin runtime version skew within a single node (e.g., a node where the kernel was upgraded but a plugin wasn't restarted) — this is a single-node concern, not a federation concern
- CRDT operation-log compatibility within an established federation (this is paper §15 and A1's iOS event-queue contract; A6 is upstream of these)

Adding this subsection prevents future amendments from accidentally claiming A6 covers concerns it doesn't. **Encouraged.**

---

## 4. Quality rubric grade

**Grade: B (Solid).** Path to A is mechanical (A1–A6 land + verification of CanonicalJson enum-bump tolerance per A6).

- **C threshold (Viable):** All structural elements present (driver, type signature, compatibility relation, handshake mechanics, error behavior, edge-case escape valve, acceptance criteria, cited-symbol verification, open questions, sibling-amendment dependency, cohort discipline). No critical *planning* anti-patterns. **Pass.**
- **B threshold (Solid):** Stage 0 sparring evident in OQ-A6.1 + OQ-A6.2 + OQ-A6.3 (three explicit deferrals); FAILED conditions present in A6.4 (four `failed_rule` enum values + recovery actions per rule); Cold Start Test plausible — a Stage 06 implementer can read A6.6 and know what to scaffold. **Pass.**
- **A threshold (Excellent):** Misses on five counts: (1) Distributed-systems reviewer perspective wasn't fully run pre-PR — F1 (asymmetric-evaluation) would have surfaced; (2) Industry-prior-art reviewer wasn't fully run — F2 (gRPC vs P2P-CRDT prior art) would have surfaced; (3) the `required: true` citation in A6.2 rule 3 against ADR 0007 is wrong (F3) — same anti-pattern that the Decision Discipline Rule 6 was created to catch; (4) audit-emission scaling under storms (F4) — the kind of implementation-detail-but-substrate-shaping concern that Pessimistic-Risk-Assessor surfaces; (5) iOS append-only path's version-vector semantic (F5) is the most important load-bearing gap — A6 implicitly assumes A1's envelope carries enough version-context, but A1's envelope explicitly does not. **Does not reach A.**

A grade of **B with required amendments A1–A6 applied promotes to A**, conditional on A6's CanonicalJson enum-bump tolerance verification.

---

## 5. Council perspective notes (compressed)

- **Distributed-systems reviewer:** "The compatibility relation is described as symmetric but is asymmetric in three places — channel partial-order, one-sided receive-only schemaEpoch, plugin-required-from-either-side. The pathology where A says compatible / B says incompatible WILL occur in practice during beta-channel canary deployments. Two-phase commit on the verdict (each peer sends its verdict; both must agree) is the cleanest fix; alternative is gRPC-style explicit rejection trailer. The receive-only mode in A6.5 is good — but the audit-emission scaling under reconnect storms is unspecified. Per-event de-duplication on both audit types is the standard pattern; A6 needs to spec it." Drives F1 + F4 + amendments A1, A4.

- **Industry-prior-art reviewer:** "gRPC's 2-minor-deprecation-window precedent doesn't transfer to P2P CRDT clusters. gRPC has a single canonical version source (the server); P2P-CRDT has no central authority. The genuinely-applicable prior art is libp2p's protocol-version negotiation (explicit supported-version sets; intersection wins), Apple CloudKit's record-zone capability sets, Yjs/Automerge format-version handshake (per-message versioning, forward-compat by construction), IPFS bitswap version intersection. All of these use explicit-set models, not arithmetic windows. The arithmetic-window model has two failure modes: ratchet problem (once node A is updated to 1.5.0, it can no longer talk to node B at 1.0.0 even if formats are compatible) and forced-upgrade cascades. v0 arithmetic-window model is a reasonable simplification but should be honest about being a v0 simplification, with the explicit-set model named as the Phase 3+ migration target. Better still: ship the explicit-set model now — it's not significantly more complex." Drives F2 + amendment A2.

- **Cited-symbol / cohort-discipline reviewer:** "Spot-checked A6.7 in both directions per the new memory rule. Positive-existence claims pass: `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` exists (verified `git show packages/foundation/Crypto/CanonicalJson.cs`); `Sunfish.Kernel.Audit.AuditEventType` exists (verified `git ls-tree packages/kernel-audit/AuditEventType.cs`); ADRs 0001/0007/0027/0031/0032/0049/0061 are all Accepted on origin/main (verified Status header). Spot-check on `IOperationSigner` passes (existing per A2/A4). HOWEVER: A6.2 rule 3's `required: true` citation against ADR 0007's bundle-manifest-schema is broken — ADR 0007 has `requiredModules: string[]` on the bundle manifest, NOT a `required: true` field on per-plugin manifests. The `ModuleManifest` only has `key, name, version, description, capabilities`. A6 needs to either change the citation (cite `bundle.requiredModules` correctly) or augment the wire format to actually carry per-plugin `required` flags. This is the same class of error that the Decision Discipline Rule 6 was created to catch. Mechanical fix; substrate is fine. Auto-merge-disabled was the right call. Cohort batting average: this will be 8-of-N if A3 fix lands post-merge — consistent with cohort precedent." Drives F3 + F11 + F12 + F13 + F14 + amendment A3.

- **Forward-compatibility reviewer:** "`instanceClass = Embedded` is the canonical YAGNI surface-area-drift pattern — a tuple field added for forward-compat with no real consumer. OQ-A6.3 acknowledges. Strip it. Per the F12-verified CanonicalJson unknown-field-tolerance, adding fields later is forward-compat by default — the reserve-fields argument loses to the YAGNI argument. **Caveat:** enum-value forward-compat is more uncertain than field forward-compat — adding an enum value later may not be tolerated by older deserializers if `JsonStringEnumConverter` defaults reject unknown values. Verification step needed at hand-off. Separately: the iOS append-only path is the load-bearing gap. A6.5 hints at v0-compatible envelope forward-compat but A1's envelope literally does NOT carry per-event version-vector data. The iPad captures events under whatever kernel version is on the device at capture time; the events may sit in the queue across iPad-app upgrades; the upload handshake reflects the iPad's *current* version-vector, not the *capture-time* version-vector. A6 needs A6.11 to spec per-event capture-context tagging (at minimum `captured_under_kernel` + `captured_under_schema_epoch` on the iOS envelope) + merge-boundary evaluation against the capture-time version-vector + sequestration (not silent dropping) for cross-epoch events. This is the gap A1's council also pressure-tested in a different domain (LWW vs forward-only-status) — same pattern: the substrate-tier amendment under-specifies a load-bearing semantic that Stage 06 will hit." Drives F5 + F6 + F7 + F8 + F9 + F10 + amendments A5, A6, A7, A8, A9, A10.

---

## 6. Cohort discipline scorecard

| Cohort baseline | This amendment |
|---|---|
| 7+ prior substrate amendments needed post-acceptance fixes | Will be 8-of-N if A3/A1/A2 fixes are applied post-merge (or zero post-merge fixes if A1–A6 land pre-merge per current auto-merge-disabled approach) |
| Cited-symbol verification: avg ~1 missed symbol per amendment; both-direction spot-check now standard | This amendment: 0 missed positive-existence symbols (all 7 ADRs + 4 type symbols verified Accepted/exist) + 1 broken citation (`required: true` in A6.2 rule 3 against ADR 0007 — the field doesn't exist on ModuleManifest) |
| Council false-claim rate (both directions) per ADR 0028-A4.3: 2-of-9 | This council: 0 false-existence claims as of writing; 0 false-non-existence claims; F11/F12/F13/F14 are explicit positive-existence verifications with verification commands shown |
| Council pre-merge vs post-merge | Pre-merge (correct call: substrate-tier amendment with 1 Critical + 4 Major + 4 Encouraged + 4 verification-passes; pre-merge fix cost ~2-3h vs post-merge held-state cost ~24h+ per cohort precedent) |
| Severity profile | 1 Critical (F1), 5 Major (F2 + F3 + F4 + F5 + F6), 4 Minor (F7 + F8 + F9 + F10), 4 verification-passes (F11 + F12 + F13 + F14) |

The cohort lesson holds: every substrate-tier amendment so far has needed council fixes; pre-merge council is dramatically cheaper than post-merge. F11/F12/F13/F14 deliberately include positive-existence spot-checks per the new "spot-check both directions" memory — A6's positive-existence claims all pass, but A6.2 rule 3's structural citation against ADR 0007 fails (the field A6 cites doesn't exist on the schema A6 cites). This is a different failure mode from "symbol doesn't exist" (which the prior cohort caught) — it's "symbol exists but at the wrong layer of the schema." Updating the council-discipline memory to also cover *structural* citation correctness (does the cited field exist *on the cited type*, not just somewhere in the cited ADR) is a follow-up XO action.

---

## 7. Closing recommendation

**Accept A6 with required amendments A1–A6 applied before W#33 Stage 06 build emits its first `VersionVectorExchange` message.** The architectural decision (tuple-typed version vector + handshake-time exchange + small enumerated rule set + one-sided receive-only escape valve) is correct and consistent with substrate-cohort design taste. The substantive gaps are:

1. Symmetric-evaluation handshake (F1 / A1) — load-bearing for any channel-mismatch pairing
2. P2P prior-art framing of the SemVer window (F2 / A2) — honesty about the v0 model's limits
3. Plugin-required citation correctness + wire-format augmentation (F3 / A3) — substrate citation correctness
4. Audit-emission de-duplication (F4 / A4) — anti-flood substrate
5. iOS append-only path version-vector semantic (F5 / A5) — load-bearing for the only known cross-version consumer
6. Strip `Embedded` instance-class (F6 / A6) — YAGNI

A1, A4, A5, A6 are mechanical-on-the-amendment-text but substrate-shaping. A2 + A3 are also mechanical but require a wire-format change (A6.1's `plugins` map shape augmentation). All six are 2-3h of XO work pre-merge.

W#33 Stage 02 design can begin immediately on the architectural decision; Stage 06 build gates on A1–A6 + the CanonicalJson enum-bump-tolerance verification per A6.

**Sibling A5 (cross-form-factor migration) blocks on A6 settling.** Per A6.9 + W#33 §7.2 sequencing, A5's compatibility-relation citations should not be authored until A6 lands amended — otherwise A5 inherits A6's gaps.

**Standing rung-6 task (per ADR 0028-A4.3 commitment):** XO spot-checks A6's cited-symbol table within 24h of merge (already done as part of this council; F11–F14 cover the positive-existence spot-checks). If F3's broken citation is not fixed pre-merge and lands as substrate, file an A6.x retraction matching the A3/A4 retraction pattern from the prior cohort.

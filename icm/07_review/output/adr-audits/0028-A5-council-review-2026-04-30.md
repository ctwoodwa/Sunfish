# ADR 0028 Amendment A5 — Council Review (Stage 1.5 Adversarial)

**Date:** 2026-04-30
**Reviewer:** research session, four-perspective adversarial council per UPF Stage 1.5 (substrate-tier cross-form-factor migration scope)
**Amendment under review:** [ADR 0028 — A5 "Cross-device + cross-form-factor migration semantics"](../../../docs/adrs/0028-crdt-engine-selection.md) (PR #402, branch `docs/adr-0028-a5-cross-form-factor-migration`, auto-merge intentionally DISABLED pre-council per cohort discipline)
**Companion intake:** [`2026-04-30_cross-form-factor-migration-intake.md`](../../00_intake/output/2026-04-30_cross-form-factor-migration-intake.md) (W#33)
**Companion ADR (predecessor A5 depends on):** [ADR 0028 — A6 "Version-vector compatibility contract"](../../../docs/adrs/0028-crdt-engine-selection.md) (post-A7 fixes; PR #395 merged; PR #396 council merged)
**Driver discovery:** Mission Space Matrix §5.7 (`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`)

---

## 1. Verdict

**Accept with amendments. Grade: B (Solid).**

The architectural shape is sound: a `FormFactorProfile` tuple + a derived-surface filter (`form-factor.capabilities ∩ workspace.declaredCapabilities`) + an explicit data-loss-vs-feature-loss invariant (Invariant DLF) + sequestration-over-deletion + audit-emission for every transition is the right shape. A5 generalizes A1's iOS Phase 2.1 carve-out cleanly and inherits A6's compatibility-relation gate without re-litigating it. The substrate-tier substantive gaps are: (1) the two structural-citation claims on ADR 0046 are mostly right but **A5.7's reference path through the substrate is shaped wrong** — it cites `IFieldDecryptor` as the per-tenant key-transfer surface, but the per-tenant key transfer flows through `ITenantKeyProvider` (per ADR 0046-A4.1), NOT `IFieldDecryptor` (which is the *consumer* of the per-tenant DEK at decrypt time, not the cross-device transfer mechanism); (2) A5.7's QR-onboarding handshake protocol shape ("F derives its session key from the QR-code secret (zero-knowledge of the long-term keys)") cites ADR 0032 + paper §13.4, but **neither artifact actually formalizes the one-time-secret + Ed25519-key-derivation handshake A5.7 describes** — paper §13.4 hints at QR scanning conceptually (3 steps: install / authenticate / sync) and ADR 0032 names the Ed25519 root keypair as device identity, but the cryptographic protocol shape is unspecified upstream; (3) Invariant DLF (A5.4) makes a "data is never silently dropped" promise that has at least three edge cases A5 does not name (cryptographically-undecryptable sequestration; per-record-class CP/AP asymmetries under partition; field-level sequestration-vs-record-level sequestration); (4) the 8-form-factor migration table (A5.1) misses two industry-prior-art rows that the Mission Space Matrix §5.3 footnoted (CarPlay / Android Auto migration as a *cross-OS-on-vehicle* case; iOS-Watch-pairing as a *parent-device-mediated* case) — the existing `Vehicle` and `Watch` rows handwave through these; (5) cross-form-factor concurrent-edit semantics under different `derivedSurface` filters is named as A5-followup-2 but deserves a v0 spec because the Phase 2.1 Anchor merge boundary will hit this case immediately when laptop + tablet edit the same lease record's metadata; (6) A5.5's forward-compat policy correctly names CanonicalJson unknown-field-tolerance as the substrate, but does not specify the **bidirectional round-trip** requirement (older→newer→older) that the Yjs / IPFS / CloudKit prior art treats as load-bearing — A5.5 only specifies older→newer→other-peers, not older→newer→older.

Six required amendments + four encouraged. None block W#33's broader Stage 02 design (the architectural shape is fine); all should land before W#23 Stage 06 build emits its first `FormFactorProfile` over the wire. The cohort lesson holds: A5 is the 12th substrate amendment and the pre-merge council remains dramatically cheaper than post-merge.

---

## 2. Findings (severity-tagged)

### F1 — A5.7 mis-cites `IFieldDecryptor` as the cross-device key-transfer surface (Critical, structural-citation)

A5.7 step 2: *"the inviting peer signs F's `FormFactorProfile` (binding the form factor to the workspace's identity surface) and returns the signed profile + the workspace's per-tenant encryption keys (per ADR 0046's `IFieldDecryptor` substrate; A5 does NOT define new key types)."*

This is a structural-citation failure of the same class A6.2 rule 3 hit (cited symbol exists in cited ADR, but at the wrong layer of the schema):

- **`IFieldDecryptor` exists** — verified `git grep -n "interface IFieldDecryptor" packages/foundation-recovery/Crypto/IFieldDecryptor.cs:13`. **Positive-existence: passes.**
- **But `IFieldDecryptor` is not the per-tenant key-transfer surface.** Reading ADR 0046-A2.2 + A2.3 + A4.1 in full: `IFieldDecryptor` is the *capability-checked decrypt-on-read interface*; it consumes the per-tenant DEK that is *derived* from `ITenantKeyProvider` (per A4.1's "consume existing `ITenantKeyProvider` seam" resolution to F2 of the A4 council). The per-tenant DEK itself never crosses the wire — `ITenantKeyProvider` derives it locally on each peer from the keystore root seed via HKDF-SHA256 with a per-tenant info string.
- **What A5.7 actually needs to cite is `ITenantKeyProvider` + the keystore root seed.** The cross-device key transfer (paper §13.4 step 2 "transferring the role attestation bundle") is fundamentally about transferring the **keystore root seed** (or a derivation tree from it) to the new device — not the per-tenant DEKs themselves. `IFieldDecryptor` is downstream of this transfer; it's the consumer, not the substrate.

The structural error matters because A5.8's acceptance criteria say "encode/decode `FormFactorProfile` via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` round-trip" — fine — but A5.8 has no acceptance criterion for the actual key-transfer surface. A Stage 06 implementer reading A5.7 would build against `IFieldDecryptor` (the cited surface), would discover at integration-test time that `IFieldDecryptor` cannot accept a key from another peer (because the DEK is derived locally; there's no `Ingest(EncryptedField key)` API on the interface), and would then have to amend A5.7 with the correct citation. Pre-merge council fix is dramatically cheaper than post-Stage-06-discovery fix.

**Verification command** (run against `origin/main`):

```bash
git show origin/main:packages/foundation-recovery/Crypto/IFieldDecryptor.cs | head -30
# Confirms IFieldDecryptor.DecryptAsync(field, capability, ct) — no key-ingest API
git show origin/main:packages/foundation-recovery/TenantKey/ITenantKeyProvider.cs | head -30
# Confirms ITenantKeyProvider is the per-tenant DEK-derivation seam
```

Required fix: A5.7 step 2 reword to cite `ITenantKeyProvider` + the keystore root seed transfer (not `IFieldDecryptor`); A5.9 add `ITenantKeyProvider` to the verified-existing list with the correct namespace `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider`. **Critical** because the citation is load-bearing (A5.7 is the cross-device key-transfer canonical) and the fix is mechanical but substantive.

### F2 — A5.7's QR-onboarding handshake protocol shape is not formalized in any cited ADR (Critical, structural-citation)

A5.7's three-step protocol shape:

> **1. QR scan establishes the trust anchor.** F scans a QR code displayed by the inviting peer; the QR code carries the inviting peer's Ed25519 public key + a one-time secret derived per ADR 0032. F derives its session key from the QR-code secret (zero-knowledge of the long-term keys).
> **2. Form-factor registration handshake.** F sends its newly-generated Ed25519 public key + its `FormFactorProfile` over the QR-derived session. […]

Verification against the cited upstream artifacts:

- **Paper §13.4 (verified existing)** says: *"the user scans a QR code from an existing team member's device, transferring the role attestation bundle and initial CRDT snapshot."* — three-step conceptual flow: install / authenticate / sync. The paper does NOT formalize the QR payload, the one-time-secret derivation, the session-key derivation, or the zero-knowledge property A5.7 names.
- **ADR 0032 (verified Accepted)** says: *"the install holds **one root Ed25519 keypair** as the hardware-bound device identity. Stored once in the OS keystore."* The ADR mentions QR scanning as a UX flow ("scan a QR code from an existing member") but does NOT formalize the QR payload or the cryptographic handshake.
- **A5.7 explicitly acknowledges this** in "What A5.7 does NOT cover" → *"The QR code's serialization format — that's a ~ADR-0032-A1 follow-up (paper §13.4 names QR-onboarding but no ADR has formalized the QR payload schema yet)."*

So A5.7 partially acknowledges the gap (the *serialization format* is unspecified) — but A5.7 step 1 nonetheless asserts substantive cryptographic claims ("zero-knowledge of the long-term keys"; "one-time secret derived per ADR 0032"; "F derives its session key from the QR-code secret") that depend on a protocol shape that is not formalized anywhere. The gap is bigger than just serialization — the actual protocol has not been ratified.

This is the same structural-citation failure mode the A7 lesson covers, but more severe: A6.2 rule 3 cited a real field at the wrong layer (`required: true` exists in ADR 0007 on `ProviderRequirement`, not on `ModuleManifest`). A5.7 cites a protocol shape that **does not exist in any upstream artifact** — the cited concepts (one-time secret; session-key derivation; zero-knowledge) are A5's own invention, framed as if borrowed from ADR 0032.

**Verification command:**

```bash
git show origin/main:docs/adrs/0032-multi-team-anchor-workspace-switching.md | grep -i "one-time\|session key\|zero-knowledge\|key derivation\|HKDF"
# (no matches — ADR 0032 specifies neither a one-time secret nor a session-key derivation)
grep -A 10 "13.4" /Users/christopherwood/Projects/Sunfish/_shared/product/local-node-architecture-paper.md | head -20
# Three-step conceptual flow only; no protocol shape specified
```

**Required fix:** A5.7 step 1 reword to acknowledge the protocol-shape gap explicitly:

> **1. QR scan establishes the trust anchor.** F scans a QR code displayed by the inviting peer (UX flow per paper §13.4 + ADR 0032). The QR payload schema and the cryptographic handshake (one-time-secret derivation, session-key derivation, zero-knowledge property over long-term keys) are NOT formalized in any current ADR — A5.7 names the shape A5 *expects* the future ~ADR-0032-A1 amendment to ratify, but A5 does NOT itself define the cryptographic primitive. Stage 06 build of A5 GATES on ~ADR-0032-A1 landing with the actual protocol formalized. Default expectation: a Noise_NK or Noise_IK pattern over the QR-transmitted ephemeral key, with the inviting peer's Ed25519 root key as the static long-term key — but the actual choice is downstream of A5.

Add a hard halt-condition to A5.13 cohort discipline: "A5 Stage 06 build CANNOT begin until ~ADR-0032-A1 (QR-onboarding protocol formalization) is Accepted on `origin/main`." OR alternatively, A5 takes responsibility for formalizing the QR handshake itself in an A5.1x sub-section (significantly larger scope).

**Critical** because the gap is currently invisible to Stage 06 readers — A5.7 reads as if the protocol exists upstream when it doesn't.

### F3 — Invariant DLF has at least three unspecified edge cases (Major)

A5.4 makes the substrate-tier guarantee: *"feature deactivation never causes data loss."* The three concrete behaviors enumerated (sequestration-over-deletion; re-emergence-on-surface-expansion; cross-peer-rescue) are correct but incomplete. Three edge cases are unspecified:

**Edge case (a) — cryptographically-undecryptable sequestration.** A5.7 step 3: *"If F's `FormFactorProfile` does not include `BiometricAuth` capability, the per-tenant keys for biometric-protected fields are NOT transferred to F. F can still see those records but cannot decrypt them; sequestration applies."* But A5.4's sequestration-over-deletion guarantee promises the data is "preserved as read-only-but-not-lost" — and "read-only" implies F can *read* the ciphertext but not the plaintext. From F's user's perspective, this is functionally indistinguishable from data loss (the user can see "a record exists" but cannot view its contents). The Invariant DLF prose does not name this case. Recommend: A5.4 explicitly distinguishes **plaintext-sequestration** (data plaintext-readable but UI-hidden) from **ciphertext-sequestration** (data ciphertext-stored but plaintext-unrecoverable on this form factor) and names the user-facing UX expectation for each.

**Edge case (b) — per-record-class CP/AP asymmetries under partition.** Sunfish's per-record-class CP/AP model (paper §5.x; ADR 0028 main decision) means some records have strong-consistency requirements (CP) and some have eventual-consistency (AP). A5's sequestration-on-form-factor-downgrade is described as if uniform across record classes, but: when a CP record is sequestered on F because F lacks capability C, and the workspace's other peers cannot reach quorum without F's vote (e.g., a 2-of-3 threshold with F as a deciding voter), does F's sequestered state count toward quorum? If yes, F is participating in decisions about data it can't see — a security concern. If no, the workspace deadlocks. A5 does not name this case. Recommend: A5.4 names the CP-record carve-out: sequestered CP records on F do NOT count toward F's quorum vote; F's vote-eligibility is conditional on F having capability to read the record.

**Edge case (c) — field-level vs record-level sequestration.** A5.4's three concrete behaviors implicitly treat data as record-grained ("the data is moved to an in-replica 'sequestered' partition"). But A5.7's per-tenant key transfer is field-grained (biometric-protected fields specifically). A record may have some fields F can decrypt and some it cannot. A5.4 does not specify whether the record is sequestered as a whole (lowest-common-denominator: any one field F can't decrypt → record fully sequestered) or whether field-level redaction is the model (record visible; specific fields hidden). The two have different UX, different audit-emission shapes, and different concurrent-edit semantics (per A5-followup-2). Recommend: A5.4 picks one — recommend **field-level redaction** as the default (record visible; un-decryptable fields shown as `[encrypted; not available on this form factor]` placeholder) with record-level sequestration as the explicit fallback when the un-decryptable field is the record's primary key or required-for-display.

**Verification:** Reading ADR 0046-A2.2 + the `EncryptedField.cs` source confirms that `EncryptedField` is field-grained (the `EncryptedField` value type wraps a single ciphertext + nonce + key-version triple). So field-level redaction is the natural model — but A5.4 does not commit to it.

**Major** — Invariant DLF is the load-bearing substrate-tier guarantee; under-specified edge cases produce divergent Stage 06 implementations.

### F4 — Migration table (A5.1) misses two industry-prior-art form-factor combinations (Major)

A5.1's 9-row table covers Laptop / Desktop / Tablet / Phone / Watch / Headless / IoT / Vehicle (8 form factors + the Laptop→Laptop default). Mission Space Matrix §5.3 (form factor) names 7 form factors at L1 and footnotes "Vehicle" and "Watch" as Phase 2+. The table coverage is reasonable for v0 but misses two industry-prior-art combinations:

**Missing combination (a) — Phone ↔ Watch (parent-device-mediated pairing).** Apple Watch's pairing model (canonical industry prior art) is *not* a direct Watch ↔ Anchor federation — it's Watch ↔ paired Phone ↔ Anchor. The Watch never directly federates with the workspace's other peers; all data flows through the paired Phone. A5.1 lists Laptop ↔ Watch as if the Watch is a peer in the federation (per Rule 1 "F's incoming sync receives only data classified by features in `F.derivedSurface`") — but in the Apple Watch model, the Watch's `FormFactorProfile` is *announced by the paired Phone*, not by the Watch itself, and the Watch's federation handshake is a sub-handshake of the Phone's. A5 does not name this pattern. The pattern matters because:

- Watch hardware-tier changes (battery low; locked; off wrist) propagate via the Phone's adapter, not via the Watch's adapter (per A5.3's adapter-detection table — A5.3 names "Watch" detection cadence as "On app foreground; pairing-handshake to phone re-fires" but doesn't name the parent-device-mediated pattern as the canonical model).
- The `FormFactorProfile` reported for a Watch is effectively `phone.formFactor.merged_with(watch.constraints)` — the Watch's actual capability surface is bounded by both its own hardware AND the paired Phone's constraints (e.g., Watch can't sync if Phone is unreachable).
- Sequestration-over-deletion semantics on the Watch are different — the Watch storage budget is so small (16-32GB) that A5.4's "cross-peer rescue" path is the *common* case, not the fallback. A5 should name this.

**Missing combination (b) — Vehicle as a cross-OS form factor (CarPlay / Android Auto / built-in IVI).** A5.1's Vehicle row says *"v0 doesn't ship a Vehicle adapter"* — fine — but the Mission Space Matrix §5.3 implies Vehicle is a Phase 2+ candidate. The industry prior art for Vehicle migration is significantly more complex than A5.1 hints:

- **CarPlay model:** the iOS Phone is the actual compute; the in-car display is a *projection*. The "Vehicle form factor" in this case is functionally the Phone's `FormFactorProfile` with `displayClass = Large` and `inputModalities = { Voice, Touch (limited) }`. There's no separate Vehicle node in the federation.
- **Android Auto model:** same as CarPlay — projection; Phone is the compute.
- **Built-in IVI (e.g., Tesla, Rivian, Ford Sync):** the Vehicle IS the compute; runs an embedded OS (Linux, Android Automotive, QNX); is a real federation peer. Different `FormFactorProfile` than CarPlay.

A5.1's Vehicle row collapses all three into one row. The three cases have substantively different migration semantics:

- CarPlay/Android Auto: no separate sync; the Vehicle "form factor" is the Phone's projection.
- Built-in IVI: real peer; storage constraints; air-gapped or intermittent connectivity; safety-critical UX (driver-distraction filter A5.1 names is real for IVI but not for CarPlay).

**Recommended fix:** A5.1 add two table rows:

| Source → Target | Same data set? | Filter applied | Capability re-evaluation | Notes |
|---|---|---|---|---|
| **Phone ↔ Watch (parent-device-mediated pairing)** | Watch sees a Phone-projected subset | `(phoneProfile.capabilities ∩ watchProfile.capabilities) ∩ glanceableSet` | Re-evaluate via paired Phone's adapter; Watch's `FormFactorProfile` is composed from both | Watch is not a direct federation peer; the paired Phone mediates. Watch storage budget assumes A5.4 cross-peer-rescue is the common case. |
| **Phone → CarPlay-projected Vehicle** | Vehicle sees a Phone projection (no separate peer) | `phoneProfile.capabilities ∩ vehicleSafeSet` (driver-distraction filter applied at projection time) | Re-evaluate when Phone is paired to Vehicle (BTLE / USB) | NOT a federation peer; the Vehicle is a projection of the Phone, not a participant in the federation. |

And split the existing Vehicle row into **Vehicle (built-in IVI; real federation peer)** vs **Phone-projected Vehicle (CarPlay/Android Auto)** — only the former is a federation peer for A5 purposes.

**Major** — coverage gap on a Phase 2+ form factor that real users will hit (Apple Watch is the single most common multi-form-factor pairing in the consumer market).

### F5 — Cross-form-factor concurrent-edit semantics deserve v0 spec (Major)

A5.12 names this as A5-followup-2: *"Multi-form-factor concurrent-edit semantics under different `derivedSurface` filters. If laptop creates a record under capability C, and tablet (which lacks C in its derived surface) attempts a concurrent edit on the same record's metadata — does the merge see the record? Currently per Rule 2, tablet's outgoing writes are unfiltered; tablet COULD edit the metadata if it has the metadata-only capability. The CRDT merge logic resolves the result. A5 doesn't explicitly spec this case; likely needs an A5.x amendment if test coverage finds the case underspecified."*

The deferral is wrong. This case is **not** speculative — it's the canonical Phase 2.1 case:

- **Laptop creates a lease record** (full surface). The lease has `tenant_demographics` (CP, requires capability `BiometricAuth` per ADR 0058 + ADR 0046-A2 EncryptedField pattern), `lease_terms` (CP, all peers can read), `lease_notes` (AP, all peers can edit).
- **Tablet (no `BiometricAuth`)** receives the lease record. Per A5.7 step 3 + F3 edge case (c), tablet sees `lease_terms` + `lease_notes` but not `tenant_demographics` (sequestered or field-redacted).
- **Tablet edits `lease_notes`** concurrently with laptop editing `lease_notes`. Standard CRDT LWW or last-write-wins-with-vector-clock semantics apply (per A1.2). Fine.
- **Tablet attempts to edit `tenant_demographics`** (which it cannot decrypt). What happens?

Three possible behaviors:

- **(α)** Tablet has no UI surface to author `tenant_demographics` because it can't display the field. The case never arises. (Probably the intended behavior — but A5 doesn't say so.)
- **(β)** Tablet has an admin UI that can author `tenant_demographics` *blindly* (e.g., "set this field to a new ciphertext" without reading the existing value). Now tablet's encrypt-side requires `IFieldEncryptor` access; per ADR 0046-A4.1, encrypt requires `ITenantKeyProvider` access too — same gate as decrypt. So unless tablet has *encrypt* capability, it can't author. (This is the reasonable behavior — but A5 doesn't say so.)
- **(γ)** Tablet's CRDT layer accepts a write that the merge boundary later rejects because the ciphertext is invalid (wrong DEK; wrong key version). Now we have a phantom write that the laptop has to deal with. (Bad behavior — but A5 doesn't rule it out.)

A5.12 punting on this is wrong because the laptop+tablet+lease-with-encrypted-field case is THE canonical Phase 2.1 multi-form-factor scenario. Stage 06 will hit it on day 1. A future A5.x amendment after Stage 06 has shipped is more expensive than spec'ing it now.

**Required fix:** A5.2 add Rule 6:

> **Rule 6 — Field-level write authorization mirrors field-level read authorization.** A form factor F can author a field `f` on record `r` only if F has the capability to read `f` on `r` (per A5.7's per-tenant key filtering). A field that is read-sequestered on F (because F lacks the per-tenant key for that field's encryption surface) is also write-sequestered on F. Concurrent-edit attempts to write-sequestered fields are rejected at F's local CRDT-write boundary (not at the merge boundary) with a `FieldWriteSequestered` audit event + a clear UX surface. The CRDT merge logic never sees a phantom write from F on a field F can't decrypt.

This makes the canonical model **(α) + (β) collapsed into one rule**: tablet can't author `tenant_demographics` because tablet can't read it. **Major** — substrate-tier semantic that Stage 06 implementers MUST know up front.

### F6 — A5.5 forward-compat policy doesn't specify bidirectional round-trip (Major)

A5.5: *"Older nodes treat unknown fields, capabilities, and feature-tier annotations as *informational* — they store them losslessly via `CanonicalJson` unknown-key tolerance […] but do not act on them. Older nodes do NOT generate the unknown surface (because they can't), but they preserve it on read+write round trips so newer peers see no loss."*

This is the *one-way* forward-compat property: newer→older→newer (the older node is a transparent passthrough). But the load-bearing forward-compat property in real P2P-CRDT systems is **bidirectional round-trip** — older→newer→older with no loss of information that the older node *did* generate. The Yjs protocol, the CloudKit zone-capability model, and the IPFS bitswap version intersection all treat bidirectional round-trip as the actual contract. A5.5 only specifies the easier direction.

The bidirectional case matters because:

- **Older creates a record.** Older's record schema has fields X, Y, Z. Newer reads it.
- **Newer mutates the record** by adding field W (which older doesn't know about). Newer pushes back to older.
- **Older receives the updated record.** Older preserves field W per A5.5 unknown-key tolerance.
- **Older mutates the record again** by changing field Y. Now: when older serializes the record back to canonical JSON, does field W *still* round-trip correctly? Per A5.5's "preserve them on read+write round trips" claim — yes. But this is an active claim about CanonicalJson's serialization behavior that A5.5 doesn't actually verify.

**Verification:** Reading `packages/foundation/Crypto/CanonicalJson.cs` (per F6 of the A6 council and per F12-verified):

```csharp
public static byte[] Serialize<T>(T value) {
    var node = SerializeToNodeByRuntimeType(value);
    var sorted = SortKeys(node);
    return NodeToBytes(sorted);
}
```

The serializer takes a `T value` — a *typed* object. If older deserialized the record into a typed C# class with fields X, Y, Z (no W field on the type), then re-serialized via `Serialize<T>`, **field W would be lost** because the typed class doesn't contain it. CanonicalJson.Serialize is forward-compat on **deserialization** (unknown keys silently ignored per System.Text.Json defaults) but is **NOT** forward-compat on **serialization** (the type system erases the unknown fields).

Bidirectional round-trip requires older to either:
- (i) Use a `JsonNode`-typed intermediate representation (preserves all keys; loses type safety), or
- (ii) Augment the typed class with a `Dictionary<string, JsonNode> _unknownFields` catch-all (preserves keys; preserves type safety; requires explicit support).

A5.5's claim is unverified at best, false at worst. The cohort discipline rule applies: A5.5's forward-compat policy needs an actual verification gate.

**Required fix:** A5.5 add a verification step:

> **A5.5 verification gate:** Bidirectional round-trip MUST be verified per-record-type at A5 Stage 06 hand-off. The verification consists of:
> 1. Older node deserializes a newer-format record (containing fields X, Y, Z + unknown field W).
> 2. Older node mutates a known field (Y).
> 3. Older node re-serializes via `CanonicalJson.Serialize` and ships back to newer.
> 4. Newer node verifies field W is still present at byte-identical location to the original.
>
> The verification SHALL fail unless the older node's deserialization model is one of: (i) `JsonNode`-typed intermediate, OR (ii) typed class with `Dictionary<string, JsonNode> _unknownFields` catch-all. The default System.Text.Json behavior (typed deserialization with unknown keys silently dropped on serialize) does NOT satisfy A5.5.
>
> Stage 06 hand-off MUST select option (i) or (ii) per record type and document the choice in A5.8 acceptance criteria.

**Major** — the substrate-tier forward-compat claim does not hold under the cited substrate's actual default behavior. Without this fix, Stage 06 ships an unverified-but-asserted bidirectional-round-trip claim that breaks on the first older-mutates-a-newer-record scenario.

### F7 — `AdapterRollbackDetected` 6-hour dedup window is unjustified (Minor)

A5.6 + A5.8 specify a **6-hour rolling window** for `AdapterRollbackDetected` audit dedup. A6.5.1 specifies 1-hour for `VersionVectorIncompatibilityRejected` and 24-hour for `LegacyDeviceReconnected`. The 6-hour value for `AdapterRollbackDetected` is asserted without rationale.

The choice is partially defensible (rollback is rarer than incompat; more durable than legacy-reconnect) but the rationale should be explicit. Three plausible alternatives:

- **1 hour** (matches incompat) — would fire on every retry of a failing rollback (bad).
- **24 hours** (matches legacy-reconnect) — masks intentional rollback-and-re-roll-forward sequences during testing (probably bad for ops).
- **6 hours** (current A5 choice) — balance, but no stated rationale.

Recommend: A5.6 add a one-line rationale: *"The 6-hour window is chosen to absorb a typical 'roll back, observe behavior, roll forward' cycle within a single workday while still alerting on repeated rollback patterns across days. Tunable per-deployment per OQ-A5.x."*

**Minor** — clarification, not a flaw.

### F8 — A5.3 detection cadence table omits Web/PWA form factor (Minor)

A5.3's detection-cadence table lists iOS / iPadOS / Android / MAUI desktop / Headless / Bridge / Watch. It omits Web/PWA — a form factor the Sunfish frame might support in Phase 2+ via the React adapter. The browser is its own form factor with its own detection cadence (`navigator.connection`, `BatteryStatus API`, `StorageManager.estimate()`).

Reading the ADR 0028 main decision + ADR 0048 + the React adapter setup: the React adapter targets desktop browsers as a Phase 2+ deployment surface. A5.3's table should name it (or explicitly defer it).

**Recommended fix:** A5.3 detection-cadence table add row:

| Form factor | Detection cadence | Why |
|---|---|---|
| Browser / PWA (React adapter; Phase 2+) | `navigator.connection.change` event; `StorageManager.estimate()` polled at sync time; `visibilitychange` for foreground transitions | Browser exposes a different API surface than native OSes; storage-budget changes are app-driven, not OS-driven |

**Minor** — coverage gap on a Phase 2+ form factor.

### F9 — `Sunfish.Foundation.Migration` namespace verified non-existent (verification pass, no finding)

Per the cohort discipline rule "spot-check positive-existence claims": A5.9 introduces `Sunfish.Foundation.Migration.FormFactorProfile`, `IFormFactorMigrationService`, `HardwareTierChangeEvent`, and `MigrationAuditPayloads`. Verification:

```bash
git grep -n "namespace Sunfish.Foundation.Migration" packages/
# (no results)
ls packages/ | grep -i migration
# (no results — the foundation-migration package does not exist on origin/main)
```

Confirmed: no `Sunfish.Foundation.Migration.*` symbols on `origin/main`. A5.9's "Introduced by A5" list is correctly identified — every symbol in the list is genuinely new. **Verification pass — no finding.** (This is the kind of spot-check the cohort batting average says to run; recording it because cohort discipline tracks both directions.)

### F10 — `IFieldDecryptor` exists at the cited namespace (verification pass on the surface; F1 covers the structural failure)

Per cohort discipline: A5.7 cites `IFieldDecryptor` as a substrate type. Verification:

```bash
git grep -n "interface IFieldDecryptor" packages/foundation-recovery/
# packages/foundation-recovery/Crypto/IFieldDecryptor.cs:13:public interface IFieldDecryptor
git grep -n "namespace " packages/foundation-recovery/Crypto/IFieldDecryptor.cs
# packages/foundation-recovery/Crypto/IFieldDecryptor.cs:6:namespace Sunfish.Foundation.Recovery.Crypto;
```

Confirmed: `Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor` exists. **Positive-existence: passes.**

But A5.7 cites the namespace as `Sunfish.Foundation.Recovery` (in A5.9 verified-existing list: *"`Sunfish.Foundation.Recovery.IFieldDecryptor`"*). The actual namespace is `Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor`. A5.9 has the wrong namespace. **Sub-finding (Minor):** A5.9's verified-existing list should cite the correct full namespace `Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor`. (This is a minor sub-finding to F1's structural failure — the citation is partially right but namespaced wrong.)

**Verification pass on existence + Minor sub-finding on namespace.**

### F11 — `IFieldEncryptor` exists at the cited namespace (verification pass)

Per cohort discipline structural-citation rule: A5.7 step 3's "Per-tenant keys for biometric-protected fields are NOT transferred to F" implies a per-field encrypt/decrypt path. The encrypt side is `IFieldEncryptor`. Verification:

```bash
git grep -n "interface IFieldEncryptor" packages/foundation-recovery/
# packages/foundation-recovery/Crypto/IFieldEncryptor.cs:15:public interface IFieldEncryptor
```

Confirmed: `Sunfish.Foundation.Recovery.Crypto.IFieldEncryptor` exists. **Verification pass.** A5.9 should cite this surface alongside `IFieldDecryptor` since A5.7's per-tenant-key-filtering applies to both encrypt (writes) and decrypt (reads) paths.

### F12 — All 8 cited ADRs verified Accepted on origin/main (verification pass)

Per the cohort discipline rule "verify positive-existence claims" — applied to ADR citations:

| ADR | Status header on `origin/main` | Filename verified | Verdict |
|---|---|---|---|
| 0001 schema-registry-governance | Accepted | `0001-schema-registry-governance.md` | Pass |
| 0007 bundle-manifest-schema | Accepted | `0007-bundle-manifest-schema.md` | Pass |
| 0027 kernel-runtime-split | Accepted (2026-04-22) | `0027-kernel-runtime-split.md` | Pass |
| 0028 (this ADR; A1, A2, A3, A4 Accepted; A5 under review; A6 Accepted post-A7) | Accepted | `0028-crdt-engine-selection.md` | Pass |
| 0032 multi-team-anchor-workspace-switching | Accepted (2026-04-23) | `0032-multi-team-anchor-workspace-switching.md` | Pass |
| 0046 key-loss-recovery-scheme-phase-1 | Accepted (2026-04-26; A2/A3/A4/A5 amendments landed 2026-04-30) | `0046-key-loss-recovery-scheme-phase-1.md` | Pass |
| 0049 audit-trail-substrate | Accepted (2026-04-27) | `0049-audit-trail-substrate.md` | Pass |
| 0061 three-tier-peer-transport | Accepted (2026-04-29) | `0061-three-tier-peer-transport.md` | Pass |

All 8 verified. ADR 0046 specifically — the filename is `0046-key-loss-recovery-scheme-phase-1.md`, not `0046-key-recovery-substrate.md` or any other shorthand A5 might colloquially use. A5 does not cite a wrong filename, but consumers of A5 should know the canonical filename. **Verification pass — no finding** (positive-existence spot-check confirms cohort discipline applied correctly).

### F13 — A5.7 cites "ADR 0046's rotation primitive (currently deferred per A4.3)" — citation verified correct (verification pass, structural-citation positive)

Per the new structural-citation rule (A7 lesson): A5.7 says *"per ADR 0046's rotation primitive — currently deferred per A4.3 […]"*. Verification:

```bash
git show origin/main:docs/adrs/0046-key-loss-recovery-scheme-phase-1.md | grep -A 5 "^#### A4\.3"
# #### A4.3 — F5 resolution: defer rotation primitive; Phase 1 = fixed key-version 1
# **Decision:** Phase 1 ships with **fixed key-version 1**. No rotation primitive in Phase 1. […]
# **Deletes:** `IFieldEncryptionKeyRotator`, `RecoveryRootSeedFieldEncryptionKeyRotator`, …
```

Confirmed: ADR 0046-A4.3 explicitly defers the rotation primitive. A5.7's citation "currently deferred per A4.3" is structurally correct — the cited concept (rotation primitive) IS deferred, AND it IS deferred at the cited section (A4.3), AND A4.3 IS the resolution to F5 of the prior council. **Verification pass — no finding.** (This is the kind of structural-citation spot-check the A7 lesson canonicalized; A5.7's citation passes the new rule.)

### F14 — Companion A1.x intake (PR #397) verified merged on origin/main (verification pass)

A5.11 cites *"the companion A1.x intake (PR #397) augments A1's envelope with capture-context tagging per A6.11."* Verification:

```bash
gh pr view 397 --json state,title
# {"state":"MERGED","title":"chore(icm): A1.x intake — iOS envelope capture-context tagging"}
```

Confirmed: PR #397 merged. The intake exists at `icm/00_intake/output/2026-04-30_ios-envelope-capture-context-tagging-intake.md`. A5 correctly cites a real, landed intake. **Verification pass — no finding.**

### F15 — Driver discovery `Mission Space Matrix §5.7` verified existing at line range (verification pass)

A5's driver line cites *"Mission Space Matrix discovery (`icm/01_discovery/output/2026-04-30_mission-space-matrix.md` §5.7)"*. Verification:

```bash
grep -n "^### 5\.7" /Users/christopherwood/Projects/Sunfish/icm/01_discovery/output/2026-04-30_mission-space-matrix.md
# 334:### 5.7 — Migration
```

Confirmed: §5.7 "Migration" exists at line 334; gate definition + examples + current-coverage + what's-missing + industry-prior-art + recommendation sections all present. The `Coverage tag: Gap *(one peripheral hint exists)*` claim aligns with A5's driver framing ("§5.7 identifies cross-form-factor migration as a Gap with one peripheral hint"). **Verification pass — no finding.**

---

## 3. Recommended amendments

### A1 (Required) — Reword A5.7's per-tenant-key-transfer surface citation

A5.7 step 2 reword:

> **2. Form-factor registration handshake.** F sends its newly-generated Ed25519 public key + its `FormFactorProfile` over the QR-derived session. The inviting peer signs F's `FormFactorProfile` (binding the form factor to the workspace's identity surface) and returns the signed profile + sufficient material to derive the workspace's per-tenant encryption keys (per ADR 0046-A4.1's `ITenantKeyProvider` substrate; concretely: the keystore root seed material, gated by the form-factor capability filter in step 3). A5 does NOT define new key types — the per-tenant DEK derivation continues to flow through `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider` (which uses HKDF-SHA256 + per-tenant info string from the keystore root seed; per ADR 0046-A2.3 + A4.1). `IFieldDecryptor` and `IFieldEncryptor` are downstream consumers, not the cross-device transfer mechanism.

A5.9 verified-existing list: change `Sunfish.Foundation.Recovery.IFieldDecryptor` to `Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor`; add `Sunfish.Foundation.Recovery.Crypto.IFieldEncryptor`; add `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider`.

This addresses F1 (Critical, structural-citation) and the F10 sub-finding (namespace correctness) and F11. **Required.**

### A2 (Required) — Acknowledge QR-onboarding handshake protocol gap explicitly + add halt-condition

A5.7 step 1 reword (per F2 verbatim recommendation):

> **1. QR scan establishes the trust anchor.** F scans a QR code displayed by the inviting peer (UX flow per paper §13.4 + ADR 0032's Ed25519-keypair-as-device-identity). The QR payload schema and the cryptographic handshake (one-time-secret derivation, session-key derivation, zero-knowledge property over long-term keys) are NOT formalized in any current ADR — A5.7 names the shape A5 *expects* the future ~ADR-0032-A1 amendment to ratify, but A5 does NOT itself define the cryptographic primitive. Default expectation: a Noise_NK or Noise_IK pattern over the QR-transmitted ephemeral key, with the inviting peer's Ed25519 root key as the static long-term key — but the actual choice is downstream of A5.

A5.13 cohort discipline add halt-condition:

> **A5 Stage 06 build halt-condition:** A5 Stage 06 build (any code that emits a `FormFactorProfile` over a QR-derived session) CANNOT begin until ~ADR-0032-A1 (QR-onboarding protocol formalization) is Accepted on `origin/main`. A5.7's protocol shape is expected; ~ADR-0032-A1 ratifies it. Without ratification, A5.7's cryptographic claims (zero-knowledge over long-term keys; one-time-secret derivation; session-key derivation) are unverified.

This addresses F2 (Critical, structural-citation). **Required.**

### A3 (Required) — Specify Invariant DLF edge cases

A5.4 add three concrete behaviors after the existing four:

> **5. Plaintext-vs-ciphertext sequestration distinction.** Sequestration applies at two layers:
>    - **Plaintext sequestration** (the data is plaintext-readable on F but UI-hidden because F lacks the *feature* surface to display it): F's UI hides the data; F's storage retains plaintext; release on surface expansion is immediate.
>    - **Ciphertext sequestration** (the data is ciphertext-stored but plaintext-unrecoverable on F because F lacks the *cryptographic* capability — typically because A5.7 step 3 filtered out the per-tenant key for F's `FormFactorProfile`): F's UI hides the data; F's storage retains ciphertext; release on surface expansion requires re-running A5.7's key transfer.
>
> The two sequestration types emit different audit events (`PlaintextSequestered` / `CiphertextSequestered`) and have different UX surfaces ("hidden on this device; available on others" vs "encrypted; not available on this form factor"). User-facing UX MUST distinguish.
>
> **6. CP-record quorum participation.** When a CP-class record is sequestered on F (per A5.4 rule 5), F's vote does NOT count toward the CP record's quorum. F's vote-eligibility is conditional on F having the capability to read the record. A workspace where F is a deciding voter on a CP record F can't read MUST detect the case at A5.7-key-transfer-time and emit a `FormFactorQuorumIneligible` audit + UX surface (operator chooses: re-grant capability, or remove F from the quorum set for this record).
>
> **7. Field-level redaction is the default; record-level sequestration is the fallback.** When a record's encrypted fields cannot be decrypted on F but the record's primary-key + display-name fields can, F sees the record with `[encrypted; not available on this form factor]` placeholders for the un-decryptable fields. Record-level sequestration (entire record hidden) applies only when F cannot decrypt the record's primary-key or required-for-display fields. The choice is per-record-type and documented at Stage 06 hand-off.

This addresses F3 (Major). **Required.**

### A4 (Required) — Add Phone↔Watch + CarPlay/Android-Auto rows; split Vehicle row

A5.1 migration table add:

| Source → Target | Same data set? | Filter applied | Capability re-evaluation | Notes |
|---|---|---|---|---|
| **Phone ↔ Watch (parent-device-mediated pairing)** | Watch sees a Phone-projected subset | `(phoneProfile.capabilities ∩ watchProfile.capabilities) ∩ glanceableSet` | Re-evaluate via paired Phone's adapter; Watch's `FormFactorProfile` is composed from both | Watch is NOT a direct federation peer; the paired Phone mediates. Watch storage budget assumes A5.4 rule 3 cross-peer-rescue is the common case, not the fallback. |
| **Phone → Phone-projected Vehicle (CarPlay / Android Auto)** | Vehicle sees a Phone projection (no separate peer) | `phoneProfile.capabilities ∩ vehicleSafeSet` (driver-distraction filter applied at projection time) | Re-evaluate when Phone is paired to Vehicle (BTLE / USB / wired CarPlay) | NOT a federation peer; the Vehicle is a projection of the Phone. The "Vehicle form factor" in this case is the Phone's `FormFactorProfile` with `displayClass = Large` and constrained `inputModalities`. |

Replace the existing Vehicle row with **Vehicle (built-in IVI; real federation peer)** with notes clarifying the IVI case is in scope; CarPlay/Android Auto handled by the previous Phone-projection row.

This addresses F4 (Major). **Required.**

### A5 (Required) — Spec field-level write authorization (Rule 6 in A5.2)

A5.2 add Rule 6:

> **Rule 6 — Field-level write authorization mirrors field-level read authorization.** A form factor F can author a field `f` on record `r` only if F has the capability to read `f` on `r` (per A5.7's per-tenant key filtering). A field that is read-sequestered on F (because F lacks the per-tenant key for that field's encryption surface) is also write-sequestered on F. Concurrent-edit attempts to write-sequestered fields are rejected at F's local CRDT-write boundary (BEFORE the merge boundary) with a `FieldWriteSequestered` audit event + a clear UX surface ("This field is encrypted with a key your form factor cannot access; ask an admin to grant the capability"). The CRDT merge logic NEVER sees a phantom write from F on a field F can't decrypt.

A5.8 acceptance criteria add:

- [ ] `AuditEventType.FieldWriteSequestered` (new constant) emitted when F attempts to write a sequestered field
- [ ] Test: tablet with `FormFactorProfile.formFactor = Tablet` + no `BiometricAuth` capability attempts to write `lease.tenant_demographics` (encrypted under biometric-protected key) → write rejected at local CRDT boundary; audit emitted; merge boundary never sees the attempt

A5.12 remove A5-followup-2 (now resolved by Rule 6).

This addresses F5 (Major) and pre-resolves what was deferred as A5-followup-2. **Required.**

### A6 (Required) — Bidirectional round-trip verification gate for A5.5 forward-compat

A5.5 add verification step (per F6 verbatim recommendation):

> **A5.5 verification gate (forward-compat bidirectional round-trip).** Bidirectional round-trip MUST be verified per-record-type at A5 Stage 06 hand-off. The verification consists of:
>
> 1. Older node deserializes a newer-format record (containing fields X, Y, Z + unknown field W).
> 2. Older node mutates a known field (Y).
> 3. Older node re-serializes via `CanonicalJson.Serialize` and ships back to newer.
> 4. Newer node verifies field W is still present at byte-identical location to the original.
>
> The verification SHALL fail unless the older node's deserialization model is one of: **(i)** `JsonNode`-typed intermediate (preserves all keys; loses type safety), OR **(ii)** typed class with `Dictionary<string, JsonNode> _unknownFields` catch-all (preserves keys; preserves type safety; requires explicit support). The default System.Text.Json behavior (typed deserialization with unknown keys silently dropped on serialize) does NOT satisfy A5.5.
>
> Stage 06 hand-off MUST select option (i) or (ii) per record type and document the choice in A5.8 acceptance criteria. **Default expectation:** option (ii) for performance-critical records; option (i) for low-traffic records where type safety is not load-bearing.

A5.8 acceptance criteria add:

- [ ] Bidirectional round-trip test for at least 3 representative record types (lease, inspection, asset) using option (ii) catch-all dictionary

This addresses F6 (Major). **Required.**

### A7 (Encouraged) — Add 6-hour rationale to `AdapterRollbackDetected` dedup

A5.6 add one-line rationale: *"The 6-hour window is chosen to absorb a typical 'roll back, observe behavior, roll forward' cycle within a single workday while still alerting on repeated rollback patterns across days. Tunable per-deployment per a future OQ."*

This addresses F7 (Minor). **Encouraged.**

### A8 (Encouraged) — Add Browser/PWA row to A5.3 detection-cadence table

A5.3 detection-cadence table add:

| Form factor | Detection cadence | Why |
|---|---|---|
| Browser / PWA (React adapter; Phase 2+) | `navigator.connection.change` event; `StorageManager.estimate()` polled at sync time; `visibilitychange` for foreground transitions | Browser exposes a different API surface than native OSes; storage-budget changes are app-driven, not OS-driven |

This addresses F8 (Minor). **Encouraged.**

### A9 (Encouraged) — Add `ITenantKeyProvider` to A5.9 verified-existing list

A5.9 already has `Sunfish.Foundation.Recovery.IFieldDecryptor` (mis-namespaced; A1 fixes). Even after A1's namespace fix, the canonical per-tenant-key surface that A5.7 actually depends on is `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider`. Add it explicitly:

> **A5.9 verified-existing additions:**
> - `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider` (per ADR 0046-A4.1; the per-tenant-DEK-derivation seam consumed by `IFieldDecryptor`/`IFieldEncryptor`)
> - `Sunfish.Foundation.Recovery.Crypto.IFieldEncryptor` (per ADR 0046-A2.2; encrypt-side companion to `IFieldDecryptor`)

This is partially covered by A1 but worth pulling out as its own encouraged item to make the surface coverage explicit. **Encouraged.**

### A10 (Encouraged) — Add a "what A5 doesn't cover" sub-section (A5.14)

Mirror A6.12's pattern. A5 currently has A5-followup-1 and A5-followup-2 (the latter resolved by A5 fix); a "what A5 doesn't cover" list would reinforce scope discipline:

> **A5.14 — What A5 doesn't cover.**
>
> A5 covers cross-form-factor migration semantics at the substrate tier. A5 does NOT cover:
>
> - **Per-form-factor UI design** — A5 specifies *what* data F sees; the UI surface for displaying it is per-adapter (Blazor/React/SwiftUI) territory.
> - **The ~ADR-0032-A1 QR-onboarding protocol formalization** — A5.7 names the expected shape; ~ADR-0032-A1 ratifies it.
> - **Form-factor revocation mechanics** — when an admin revokes F's access, the cryptographic side is ADR 0046's revocation territory; A5 does not special-case form-factor revocation beyond standard ADR 0046 flows. (Listed as A5-followup-1; deferred.)
> - **Field-level redaction UI surface** — A5.4 rule 7 specifies the substrate behavior (placeholders for un-decryptable fields); the UX choice of how to present the placeholder is per-adapter.
> - **Cross-OS native integration** — CarPlay/Android Auto are scoped per A5.1 (projection model, not federation peer); built-in IVI is scoped as a future federation peer with its own adapter; the actual native-API integration is per-adapter Stage 06 work.
> - **Mission Space Negotiation Protocol (~ADR 0063)** — A5 ships the naive `workspace.declaredCapabilities` (union of installed plugins); ~ADR 0063 substitutes a negotiated form when it lands.

This addresses A5's missing scope-fence. **Encouraged.**

---

## 4. Quality rubric grade

**Grade: B (Solid).** Path to A is mechanical (A1–A6 land + ~ADR-0032-A1 halt-condition acknowledgment).

- **C threshold (Viable):** All structural elements present (driver, type signature, migration table, semantics rules, hardware-tier mechanics, Invariant DLF, forward-compat policy, rollback semantics, key-transfer protocol shape, acceptance criteria, cited-symbol verification, open questions, companion-amendment dependencies, cohort discipline). No critical *planning* anti-patterns. **Pass.**
- **B threshold (Solid):** Stage 0 sparring evident in OQ-A5.1–A5.5 (five explicit deferrals); FAILED conditions named in A5.13 (council-pressure-test list); Cold Start Test plausible — a Stage 06 implementer can read A5.8 + A5.9 and know what to scaffold. Companion-amendment dependencies explicit (A5.11). **Pass.**
- **A threshold (Excellent):** Misses on six counts:
  1. **F1 (Critical, structural-citation):** A5.7's `IFieldDecryptor` citation as the per-tenant-key-transfer surface is structurally wrong (the surface is `ITenantKeyProvider`; `IFieldDecryptor` is the consumer). Same class of failure as A6.2 rule 3's `required: true` mis-citation that A7 fixed.
  2. **F2 (Critical, structural-citation):** A5.7's QR-onboarding handshake cites paper §13.4 + ADR 0032 as upstream formalizations, but neither artifact ratifies the protocol shape A5.7 describes.
  3. **F3 (Major):** Invariant DLF has three unspecified edge cases (cryptographic sequestration; CP-record quorum; field-vs-record granularity).
  4. **F4 (Major):** Migration table misses Phone↔Watch (parent-device-mediated) and CarPlay/Android-Auto (Phone-projection) rows; collapses three substantively different Vehicle cases into one.
  5. **F5 (Major):** A5-followup-2 (cross-form-factor concurrent-edit semantics under different `derivedSurface` filters) deserves v0 spec, not deferral — Phase 2.1 hits it on day 1.
  6. **F6 (Major):** A5.5 forward-compat policy's bidirectional round-trip claim is unverified at the substrate level; CanonicalJson.Serialize's typed-class-erases-unknown-fields default behavior breaks the claim.

A grade of **B with required amendments A1–A6 applied promotes to A**, conditional on the ~ADR-0032-A1 halt-condition (A5 Stage 06 build gates on its ratification per A2's halt-condition addition).

---

## 5. Council perspective notes (compressed)

- **Distributed-systems reviewer:** "Invariant DLF is the load-bearing substrate-tier guarantee; under-specifying its edge cases produces divergent Stage 06 implementations that aren't compatible with each other. The three concrete behaviors A5.4 names are correct but incomplete: (a) cryptographically-undecryptable sequestration vs plaintext sequestration is a different UX class; (b) CP-record quorum participation when F is a deciding voter on a record F can't read is a security concern that A5 doesn't name; (c) field-level vs record-level sequestration is a per-record-type choice that needs explicit defaulting. Separately: the cross-form-factor concurrent-edit semantics under different `derivedSurface` filters (A5-followup-2) deserve v0 spec, not deferral — the laptop+tablet+lease-with-encrypted-fields case is the canonical Phase 2.1 multi-form-factor scenario; Stage 06 will hit it on day 1; field-level write authorization (Rule 6) mirroring read authorization is the obvious answer. Sequestration over partition is named correctly (A5.4 rule 3 cross-peer-rescue) but the per-record-class CP/AP asymmetry isn't. Two-phase commit on form-factor handshakes (analogous to A6.3's symmetric-evaluation per A7.1) would be the natural extension if A5.7 ever produces an asymmetric-evaluation case — but currently A5.7 is one-sided (inviting peer is the authority), so the asymmetry doesn't arise. Forward to F3 + F5; amendments A3 + A5." Drives F3, F5; amendments A3, A5.

- **Industry-prior-art reviewer:** "iOS device-to-device data restoration (Apple's iCloud backup migration) is the closest direct analog and IS named in W#33 §5.7's industry-prior-art section — A5 inherits the reference correctly. Apple Watch's parent-device-mediated pairing model is the canonical industry pattern for Watch form factors, and A5.1's Watch row treats the Watch as a direct federation peer (which is wrong for the Apple Watch model). Android Wear is the same pattern. CarPlay/Android Auto are NOT federation peers (they're projections of the Phone); Tesla/Rivian/Ford Sync built-in IVI ARE federation peers with their own embedded OSes. A5.1 collapses all three Vehicle cases into one row that says 'v0 doesn't ship a Vehicle adapter.' The collapse is fine for v0 but the Phase 2+ migration rules need to differentiate. Separately on prior art: the Yjs/Automerge format-version handshake, IPFS bitswap version intersection, and Apple CloudKit zone-capability sets all treat **bidirectional round-trip** as the load-bearing forward-compat property — older→newer→older with no loss of older's known fields. A5.5 only specifies the easier direction (newer→older→other-peers transparent passthrough). Bidirectional round-trip needs an explicit verification gate because System.Text.Json's default (typed deserialization erases unknown fields on re-serialize) breaks the claim. Web/PWA detection cadence is also missing from A5.3 — the React adapter targets browser as a Phase 2+ deployment surface and the browser API surface is different from native OS APIs. Drives F4 + F6 + F8; amendments A4 + A6 + A8." Drives F4, F6, F8; amendments A4, A6, A8.

- **Cited-symbol / cohort-discipline reviewer:** "Spot-checked all three directions per the new memory rule. Positive-existence claims pass: all 8 cited ADRs (0001/0007/0027/0028/0032/0046/0049/0061) verified Accepted on `origin/main` (F12); `IFieldDecryptor` exists at the cited surface (F10); `IFieldEncryptor` exists at the cited surface (F11); `Sunfish.Foundation.Migration.*` symbols verified as introduced-by-A5 — none exist on `origin/main` (F9); ADR 0046-A4.3's deferred-rotation-primitive citation is structurally correct (F13); PR #397 verified merged (F14); Mission Space Matrix §5.7 verified existing at line 334 (F15). HOWEVER: F1 fires — A5.7 step 2 cites `IFieldDecryptor` as the per-tenant-key-transfer surface, but the actual transfer flows through `ITenantKeyProvider` per ADR 0046-A4.1. `IFieldDecryptor` is the *consumer* of the per-tenant DEK at decrypt time, not the cross-device transfer mechanism. Same class of structural-citation failure as A6.2 rule 3's `required: true` mis-citation that A7 fixed. F2 also fires — A5.7's QR-onboarding handshake protocol shape is asserted with cryptographic specificity ('one-time secret derived per ADR 0032'; 'F derives its session key from the QR-code secret'; 'zero-knowledge of the long-term keys') but neither paper §13.4 nor ADR 0032 actually formalize the protocol — paper §13.4 is conceptual (3-step UX flow), ADR 0032 names the Ed25519-keypair-as-device-identity but no handshake. F2 is a more severe structural-citation failure than F1 because the cited *protocol shape* doesn't exist anywhere upstream — A5.7 reads as if the protocol exists when it doesn't. F1 is mechanical to fix (cite the right surface); F2 requires either acknowledging the gap (A2 amendment) or A5 taking ownership of formalizing the QR handshake itself (significantly larger scope; out of A5's claimed scope). Sub-finding on F10: A5.9's namespace `Sunfish.Foundation.Recovery.IFieldDecryptor` is wrong — the actual namespace is `Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor` (Crypto subnamespace). Mechanical fix in A1. Council batting average: this is the 12th substrate amendment under review; pre-merge canonical posture caught both structural failures pre-merge — the cohort lesson holds. Cohort false-claim rate this review: 0 false-existence + 0 false-non-existence + 2 structural-citation failures (F1 + F2). Updates the cohort baseline to 4-of-12 across the three-direction rule (1 from A1, 1 from A2, 1 from A6.2 rule 3, plus F1+F2 from A5 = 5 total structural-citation failures across the cohort, but these are pre-merge catches, not post-merge retractions). Drives F1 + F2 + F9–F15; amendments A1 + A2 + A9." Drives F1, F2, F9, F10, F11, F12, F13, F14, F15; amendments A1, A2, A9.

- **Forward-compatibility reviewer:** "A5.5's forward-compat policy is *almost right* — it correctly cites CanonicalJson unknown-key tolerance as the substrate (per F12 of the A6 council), but misses the bidirectional-round-trip case that the Yjs/IPFS/CloudKit prior art treats as load-bearing. CanonicalJson.Serialize's typed-class-erases-unknown-fields default breaks the bidirectional case unless the older node uses a `JsonNode` intermediate or a `Dictionary<string, JsonNode> _unknownFields` catch-all on its typed classes. A5.5 needs a verification gate at Stage 06 hand-off (see F6 + amendment A6). On the QR-onboarding handshake (A5.7): the protocol shape is forward-compat-naive — the QR payload is single-version with no negotiation. If the QR format ever changes (e.g., a future Sunfish version uses post-quantum keys instead of Ed25519), the QR scanner has no negotiation surface to fall back to a compatible format. This is fine for v0 (QR codes are one-time-use; the workspace's first device decides the format) but the OQ should name it. Separately: the `formFactor` enum in A5.1 has 8 values (Laptop / Desktop / Tablet / Phone / Watch / Headless / IoT / Vehicle). Per the A7.6 lesson on `instanceClass = Embedded` (System.Text.Json's `JsonStringEnumConverter` rejects unknown enum values by default), adding new form-factor values later is NOT forward-compat unless A5.1 commits to a custom converter with `AllowIntegerValues = true` and an unknown-value fallback. A5 should name this verification at Stage 06 hand-off (same as A7.6 named for `instanceClass`). On the per-tenant-key transfer (A5.7 step 2): the structural citation is wrong (per F1) but the *substantive* design is forward-compat-tolerant — `ITenantKeyProvider` already supports per-tenant DEK derivation; A5.7's filter (don't transfer biometric-protected keys to F if F lacks `BiometricAuth`) is purely an authorization check on existing substrate. Once F1's citation is fixed, the design holds. Drives F6; amendments A6, A10. Also reinforces F4's Vehicle/CarPlay row split (forward-compat for Phase 2+ form factors)." Drives F6; amendments A6, A10. Reinforces F4 (Vehicle row split for forward-compat to Phase 2+).

---

## 6. Cohort discipline scorecard

| Cohort baseline | This amendment |
|---|---|
| 11 prior substrate amendments needed council fixes (cohort batting average through A6/A7) | This is the **12th substrate amendment** (A5). Pre-merge council canonical posture maintained (auto-merge intentionally DISABLED on PR #402 pending this review). 6 required + 4 encouraged amendments — all mechanical to absorb pre-merge. Cohort batting average is now **12-of-12**. |
| Cited-symbol verification: avg ~1 missed symbol per amendment; all-three-direction spot-check now standard per A7 lesson | This amendment: 0 missed positive-existence (all 8 ADRs + 4 type symbols verified existing); 0 missed negative-existence (all introduced-by-A5 symbols verified non-existent on `origin/main`); **2 structural-citation failures** — F1 (`IFieldDecryptor` cited as per-tenant-key-transfer surface; actual surface is `ITenantKeyProvider`) + F2 (QR-onboarding handshake protocol shape cited per ADR 0032 + paper §13.4, but neither artifact formalizes the protocol). Plus 1 sub-finding (F10's namespace correction `Sunfish.Foundation.Recovery` → `Sunfish.Foundation.Recovery.Crypto`). |
| Council false-claim rate (all three directions) per A7 retro: was 2-of-10 pre-A6, became 3-of-11 after A6.2 rule 3 caught | This council: 0 false-existence claims; 0 false-non-existence claims; 0 structural-citation false claims by the council itself (F1, F2, F3, F5, F6 are all *substantive findings* about A5, not council citation errors). F11–F15 are explicit positive/structural verification passes with verification commands shown. Updated cohort baseline: **5-of-12 structural-citation failures across the cohort** (A1's ADR 0061 false-negative, A2's `JsonCanonical` false-positive, A6.2 rule 3's `required: true` field-on-wrong-type, **F1's `IFieldDecryptor` cited at wrong substrate layer**, **F2's QR handshake protocol shape cited where it doesn't exist**) — but all 5 caught pre-merge, all retracted/fixed without post-acceptance churn. |
| Council pre-merge vs post-merge | **Pre-merge** (correct call: substrate-tier amendment with 2 Critical + 4 Major + 2 Minor + 7 verification-passes; pre-merge fix cost ~3-4h vs post-merge held-state cost ~28h+ per cohort precedent. Pre-merge canonical remains the right posture.) |
| Severity profile | **2 Critical (F1 + F2), 4 Major (F3 + F4 + F5 + F6), 2 Minor (F7 + F8), 7 verification-passes (F9 + F10 + F11 + F12 + F13 + F14 + F15)**. Both Criticals are structural-citation class — same class as A6.2 rule 3 that A7 fixed. The cohort discipline is *catching this class of failure pre-merge consistently*, which is the intended effect of the new memory rule. |
| Three-direction spot-check application | F9 (negative-existence pass on `Foundation.Migration.*`), F10 (positive-existence pass on `IFieldDecryptor` + sub-finding on namespace), F11 (positive-existence pass on `IFieldEncryptor`), F12 (positive-existence pass on 8 ADRs), F13 (structural-citation pass on A4.3 deferred-rotation), F14 (positive-existence pass on PR #397), F15 (positive-existence pass on Mission Space Matrix §5.7). **Plus** F1 (structural-citation FAIL on `IFieldDecryptor` as transfer surface) + F2 (structural-citation FAIL on QR handshake protocol shape upstream formalization). All three directions exercised; the new third direction (structural-citation) catches both Criticals. |

The cohort lesson holds with a sharper edge: the **structural-citation failure class** (introduced as the third direction in the post-A7 memory update) is now responsible for **both Critical findings in this council review**. The class is consistent across the cohort: an amendment cites a real symbol or concept, but at the wrong layer of the schema (A6.2 rule 3) or in an artifact that doesn't actually formalize it (A5.7 step 1's QR handshake; A5.7 step 2's `IFieldDecryptor`). Pre-merge canonical posture catches them; post-merge would require retraction-pattern fixes (cf. A3 / A4 retractions in the prior cohort). Recommend: the next cohort discipline memory update reinforces that **structural-citation spot-checks are now equal-priority with positive-existence and negative-existence spot-checks**, not a third-tier add-on.

---

## 7. Closing recommendation

**Accept A5 with required amendments A1–A6 applied before W#23 Stage 06 build emits its first `FormFactorProfile` over the wire.** The architectural decision (cross-form-factor migration via `FormFactorProfile` + derived-surface filter + Invariant DLF + sequestration-over-deletion + per-tenant-key filtering at A5.7) is correct and consistent with substrate-cohort design taste. The substantive gaps are:

1. **F1 / A1 — `IFieldDecryptor` mis-cited as cross-device key-transfer surface.** Mechanical fix; `ITenantKeyProvider` is the correct substrate per ADR 0046-A4.1.
2. **F2 / A2 — QR-onboarding handshake protocol shape unformalized upstream.** Acknowledge the gap; add halt-condition that A5 Stage 06 GATES on ~ADR-0032-A1 ratifying the QR handshake.
3. **F3 / A3 — Invariant DLF edge cases (cryptographic sequestration; CP-record quorum; field-vs-record granularity).** Spec the three unspecified behaviors.
4. **F4 / A4 — Migration table misses Phone↔Watch + CarPlay/Android-Auto rows; Vehicle row collapses three different cases.** Add 2 rows; split Vehicle.
5. **F5 / A5 — A5-followup-2 (cross-form-factor concurrent-edit semantics) deserves v0 spec.** Add Rule 6 to A5.2 (field-level write authorization mirrors read authorization). Resolves the deferred follow-up.
6. **F6 / A6 — A5.5 forward-compat bidirectional round-trip is unverified at the substrate level.** Add verification gate at Stage 06 hand-off; pin one of two deserialization models per record type.

A1, A3, A4, A5, A6 are mechanical-on-the-amendment-text but substrate-shaping. A2 is also mechanical but adds a hard halt-condition (Stage 06 build gates on ~ADR-0032-A1 landing) — XO should file the ~ADR-0032-A1 intake as a follow-up workstream. All six are 3-4h of XO work pre-merge.

W#33 Stage 02 design can begin immediately on the architectural decision; W#23 Stage 06 build gates on A1–A6 + ~ADR-0032-A1 ratification.

**Sibling amendment dependency:** A5 declares hard dependencies on A6 (post-A7), A1 (post-A2/A3/A4), ADR 0032, ADR 0046 (post-A2/A3/A4/A5). All declared dependencies are landed on `origin/main` per F12 + F14. The one undeclared dependency surfaced by this review is ~ADR-0032-A1 (QR-onboarding protocol formalization; F2 / A2's halt-condition).

**Standing rung-6 task (per ADR 0028-A4.3 + A7.12 commitment):** XO spot-checks A5's cited-symbol table within 24h of merge (already done as part of this council; F9–F15 cover the verification passes; F1 + F2 cover the structural-citation failures). If F1 or F2 is not fixed pre-merge and lands as substrate, file an A8 retraction matching the A3 / A4 retraction pattern from the prior cohort.

**Cohort metric update:** the substrate-amendment council batting average is now **12-of-12** (every substrate amendment so far has needed council fixes). The council false-claim rate (all three directions) is unchanged at **0 by the council itself**; the cohort's structural-citation failure rate (across all 12 amendments) is now **5-of-12** (all caught pre-merge under the auto-merge-disabled posture). Pre-merge canonical is the right posture; the cost differential vs post-merge is ~7-9× per the cohort precedent.

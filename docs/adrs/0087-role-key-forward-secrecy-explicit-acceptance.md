---
id: 87
title: Role-Key Forward-Secrecy Trade-off — Explicit Acceptance
status: Proposed
date: 2026-05-13
tier: policy
pipeline_variant: sunfish-feature-change

concern:
  - security

composes:
  - 33   # browser-shell-render-model-and-trust-posture (cites role keys)
  - 46   # key-loss-recovery-scheme-phase-1 (covers role-key recovery)
  - 76   # crew-comms-foundation-channels (pairwise X25519 currently; group ratchet is a separate concern, ADR forthcoming for OpenMLS/Megolm)

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

amendments: []
---

# ADR 0087 — Role-Key Forward-Secrecy Trade-off: Explicit Acceptance

**Status:** Proposed
**Date:** 2026-05-13

---

## Context

The foundational paper [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) §11.3 ("Role Attestation vs. Key Distribution") describes Sunfish's role-key management flow:

1. Administrator generates per-role symmetric keys.
2. Keys are wrapped with each qualifying member's public key (asymmetric encryption).
3. Wrapped bundles are distributed as administrative events in the log.
4. Each node decrypts its bundle using its private key; stores in OS keystore.
5. Rotation: administrator generates new role keys, re-wraps for current authorized members, publishes new bundles. Revoked members do not receive new bundles.

This scheme **does not provide forward secrecy** for role-key-encrypted records.

**The non-forward-secret property in detail:**

- Wrapping uses each member's long-term asymmetric key pair. There is no per-rotation ephemeral key exchange.
- An attacker who compromises a member's private key at time T can:
  - Decrypt all role-key bundles delivered to that member up to time T.
  - Use those role keys to decrypt all records encrypted under those role keys (past records that the member's node would have had access to).
  - Cannot decrypt role-key bundles delivered after revocation (rotation excludes the compromised member).

By contrast, a forward-secret protocol (Double Ratchet, MLS) would generate ephemeral keys per session such that compromise of long-term keys does not retroactively reveal past sessions.

**The 2026-05-11 F/OSS gap/conflict analysis** (memory: `project_foss_gap_conflict_analysis_2026_05_11.md`, item C2) flagged this property as **deliberate but undocumented**:

> "Paper §11.3 role keys have no forward secrecy — this is deliberate but undocumented as a signed-off decision. Needs an explicit paper §11 note or ADR amendment confirming it's intentional, not a gap."

This ADR provides the explicit acceptance: it is a deliberate trade-off, not an oversight. The W#60 F/OSS re-check (`icm/01_discovery/output/2026-05-13_w60-final-stack-foss-substitutability-recheck.md`) lists C2 as a remaining high-priority carryover; this ADR closes that carryover.

---

## Decision

**Sunfish role keys (per paper §11.3) intentionally do not provide forward secrecy. This is accepted, with the explicit rationale and mitigations documented below.**

### Rationale (why we accept the trade-off)

1. **Group cardinality and key-management complexity.** A forward-secret scheme requires either:
   - Pairwise ratchets between every pair of role members (`O(N²)` channel state per role), or
   - A group ratchet (e.g., Megolm, MLS) with explicit ratchet-message ordering across role members.

   Both options add substantial protocol complexity and increase the failure modes a member's local store must handle (lost ratchet state, recovery, out-of-order messages). For Sunfish's small-group, slow-rotation domain (typical role = 3–20 members; rotation cadence weeks-to-months), the operational cost of group ratchet management exceeds the marginal security gain.

2. **Threat model fit.** The dominant private-key compromise scenarios Sunfish must defend against are:
   - **Device theft + cold storage access** — mitigated by OS keystore protections (Secure Enclave, TPM, hardware-backed keystores) before forward secrecy would matter.
   - **Insider threat (rogue authorized member)** — defeats forward secrecy anyway, since the insider already legitimately holds the role keys for their period of access. Forward secrecy protects against post-revocation access; this is addressed by **rotation cadence + revocation propagation** (below), not by ephemeral keys.
   - **Targeted long-term key extraction (state-level adversary)** — outside Sunfish's documented threat tier per paper §11.1.

   The threat where forward secrecy would dominate (cryptographic attacker who compromises a member's long-term private key and replays archived ciphertext) is non-trivial to mount and is bounded by the rotation cadence below.

3. **Key Loss Recovery interaction** (ADR 0046 + amendments). The recovery scheme depends on the ability to **re-wrap existing role keys** for a re-keyed member. Forward-secret group ratchets do not have this property natively (recovery requires re-initiation of the group ratchet across all members). ADR 0046's recovery semantics would require substantial re-design under a forward-secret role-key scheme.

4. **Distinct from Crew Comms.** The Crew Comms surface (ADR 0076) and group E2E for crew messaging is a **separate concern**. The 2026-05-11 F/OSS analysis item G2 (Megolm / OpenMLS for crew comms beyond pair sessions) addresses group E2E for human-to-human messaging, where the threat model and cardinality are different. **This ADR does not preclude G2's resolution**: a future crew-comms group ratchet (Megolm or MLS) is compatible with the role-key scheme remaining non-forward-secret.

### Mitigation (why the trade-off is bounded)

1. **Rotation cadence policy.** Administrators rotate role keys on a documented cadence:
   - **Quarterly** for low-sensitivity roles (default)
   - **Monthly** for medium-sensitivity roles (financial, legal-document-access)
   - **On-revocation** for high-sensitivity roles (admin, owner-equivalent) and after any known device-loss event

   Rotation cadence is a runtime policy (per-role, per-tenant), surfaced as a Sunfish operator setting. **Future workstream:** automated reminders + policy templates at Phase 5+ self-hosting docs.

2. **Revocation propagation guarantee.** When an administrator revokes a member, the rotation cycle begins immediately, and revoked members are excluded from subsequent wrapped-key bundles. Records encrypted with the new role keys are inaccessible to revoked members from rotation T+1 forward.

3. **Backwards-decryption scope is bounded.** A compromise at time T reveals records encrypted with role keys the member legitimately held up to T. **It does not reveal records:**
   - Encrypted with rotated role keys (T+1 onward)
   - Encrypted under roles the member did not hold
   - Encrypted under pairwise (non-role) channels (Crew Comms pair sessions per ADR 0076)
   - Stored on other tenants' nodes (cross-tenant isolation per ADR 0001 + ADR 0084)

4. **OS keystore as the load-bearing defense.** The role keys live in OS keystore (Secure Enclave on Apple, TPM on Windows, keychain on Linux) — not in process memory or plain disk. Extraction requires physical device compromise + keystore bypass. Sunfish does not attempt to defeat this layer; instead, the architecture relies on it.

5. **Layered with kernel-audit (ADR 0011).** Every role-key decryption operation produces an audit event. Anomalous decryption patterns (e.g., bulk historical decryption from a single member's node) are detectable; this is post-hoc but provides a forensic trail.

---

## Consequences

### Positive

- **Simpler protocol.** No group ratchet state to manage; no per-rotation choreography beyond "publish new wrapped bundles."
- **Compatible with ADR 0046 recovery.** Re-wrapping for a re-keyed member is straightforward.
- **Operator-visible policy levers.** Rotation cadence is a knob administrators understand without cryptographic-protocol expertise.
- **Aligned with threat model.** No security claim made that the architecture cannot defend; closes the gap-vs-decision ambiguity flagged by the F/OSS analysis.

### Negative

- **Backwards-decryption window** for compromised long-term keys. Worst case: an attacker holding a member's private key + having archived all wrapped bundles can decrypt records back to the member's first authorization.
- **Documentation burden.** Operators must understand the rotation-cadence trade-off; not a one-and-done property.

### Risks

- **State-level adversary** with long-term collection capability + late private-key extraction would defeat the architecture. Documented as out-of-scope per paper §11.1.
- **Insider with quiet long-term archival** would have backwards-decryption power after their revocation. Mitigated by rotation cadence + audit anomaly detection (ADR 0011).
- **Operator failure to rotate.** A tenant that never rotates role keys gets a single key for the lifetime of the role — strictly worse property. Mitigation: Phase 5 self-hosting docs include rotation reminders; future automated rotation reminder service is a candidate workstream.

---

## Alternatives considered

1. **Group ratchet (Megolm or MLS) for role keys** — rejected for the operational complexity reasons above. Reconsider if the threat model shifts (e.g., Sunfish enters a regulatory environment requiring strict forward secrecy).

2. **Pairwise ratchets per role member** — rejected for `O(N²)` state cost. Doesn't scale beyond small roles.

3. **Frequent (daily) rotation as a substitute for forward secrecy** — rejected as operationally unrealistic for human-administered roles. Daily rotation pressure on administrators trades cryptographic property for operator burnout.

4. **Document silently as an architecture property without ADR** — rejected. The 2026-05-11 F/OSS analysis is explicit that the "deliberate but undocumented" state is itself the problem. Visibility is the deliverable.

---

## Paper §11.3 amendment language (binding)

The following paragraph is appended to paper §11.3 ("Role Attestation vs. Key Distribution") as an explicit forward-secrecy note. The paper edit is a separate small PR (not bundled with this ADR's acceptance commit):

> **Forward secrecy:** This scheme does not provide forward secrecy for role-key-encrypted records. A member's long-term private key, if compromised, can decrypt the wrapped role-key bundles delivered to that member up to the compromise time, and thereby decrypt records encrypted under those role keys. This is intentional — the trade-off and its mitigations are documented in **ADR 0087**. Rotation cadence + revocation propagation + OS-keystore-backed key storage + kernel-audit anomaly detection together bound the residual risk. A forward-secret group ratchet (Megolm, MLS) is **not** in the role-key path; the Crew Comms group-ratchet decision (per the F/OSS analysis item G2) is a separate concern.

---

## Verification

- [ ] Paper §11.3 updated with the amendment paragraph above (separate PR)
- [ ] ADR 0046 cross-reference verified — recovery scheme remains compatible (still-current as of 2026-05-13)
- [ ] Rotation-cadence operator setting surfaced in Phase 5 self-hosting docs (deliverable carried into Phase 5 intake)
- [ ] Crew Comms group-ratchet work (G2 from F/OSS analysis) opened as separate workstream when crew sessions scale beyond pair (W#45 follow-on)

---

## Workstream closure

This ADR closes carryover **C2** from the 2026-05-11 F/OSS gap/conflict analysis (memory: `project_foss_gap_conflict_analysis_2026_05_11.md`).

Remaining open carryovers from that analysis:

- **G2** Group E2E for Crew Comms beyond pair sessions (Megolm/MLS) — W#45 follow-on workstream when crew sessions grow beyond pairs
- **OQ-1** GPLv3 boundary for Sunfish-over-ERPNext — Phase 5 prerequisite; needs legal counsel
- **OQ-3** Loro→Automerge migration story — Phase 3 FAILED-trigger readiness; deferred until Phase 3 is in flight

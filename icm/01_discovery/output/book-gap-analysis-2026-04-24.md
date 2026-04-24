# Discovery — Book-to-Sunfish Gap Analysis
**Date:** 2026-04-24  
**Source:** Full read of all *The Inverted Stack* chapters + complete Sunfish package audit  
**Method:** Parallel agent analysis — book spec extraction and Sunfish implementation audit conducted simultaneously

---

## Context

*The Inverted Stack* is the written specification for the architecture Sunfish implements. The book makes 18 top-level claims that a reference implementation must satisfy. The Sunfish audit shows ~80% of Phase-1 primitives are landed; however, several of the book's most visible and council-reviewed requirements are completely absent.

The gaps below are not discovered by reading the architecture paper alone — they come specifically from the book's council review (ch05–ch10), which produced 15 conditions, and from Part III's precise implementation constraints, which go beyond what the paper specifies.

---

## Gap Classification

Each gap is tagged:

- **[BLOCK]** — Council condition marked as blocking; must be resolved before v1
- **[ARCH]** — Architectural invariant the book states is non-negotiable
- **[TEST]** — Testing requirement (5-level pyramid); unmet
- **[ENT]** — Enterprise procurement gate; blocks enterprise customers
- **[DOC]** — Documentation/playbook that should exist but doesn't
- **[DEFERRED]** — Already tracked in wave plan; included for completeness

---

## Critical Gaps — Block v1 Release

### G01 — Three-Tier CRDT GC Policy [BLOCK] [ARCH]

**Book says:** Chapter 12 + Prof. Shevchenko Round 1 condition. Three tiers: Ephemeral (aggressive GC, no durability), Standard (90-day retention + peer ack, compact after), Compliance (no GC, indefinite). Sunfish CRDT accumulates without bound — a Round 1 BLOCK.

**Sunfish has:** `kernel-crdt` with YDotNet backend; no GC tiers defined or enforced.

**Gap:** GC tier classification per record class is absent. Ephemeral presence/cursor data accumulates. Standard records are never compacted. Compliance records have no designation.

**Action:** Define GC tier enum in `foundation`. Annotate record types per tier. Implement compaction job for Standard tier. Wire into event log retention. Write stale peer recovery protocol (see G06).

---

### G02 — Flease Split-Write Safety Fence [BLOCK] [ARCH]

**Book says:** Chapter 14 + Prof. Shevchenko Round 1 condition. When a new lease holder takes over, it must explicitly acknowledge that the previous lease has expired before its first write. Without this fence, a partition scenario produces a split-write window where two nodes both believe they hold the lease simultaneously.

**Sunfish has:** `kernel-lease` with Flease-based distributed leases, 30-second default, quorum enforcement.

**Gap:** The fence protocol (new holder waits for previous expiry acknowledgment) is not mentioned in any existing code or ADR. The lease may be correctly implemented but this specific invariant hasn't been validated or documented.

**Action:** Add split-write fence validation to `ILeaseCoordinator`. Document the fence semantics in ADR. Add deterministic simulation test covering the partition split scenario.

---

### G03 — DEK/KEK Field-Level Envelope Encryption [BLOCK] [ARCH]

**Book says:** Chapter 15. Each document gets a random DEK (AES-GCM). Each DEK is wrapped with the current role KEK. KEK rotation wraps DEKs only (not document bodies) — rotation work ∝ document count, not size. Key hierarchy: Org Root → Role KEK → per-document DEK → ciphertext. Per-role KEKs wrapped with member device public keys and distributed via signed admin events.

**Sunfish has:** SQLCipher AES-256 page-level encryption at the database level. OS keystore for node identity key. Argon2id key derivation.

**Gap:** No DEK/KEK layer exists. There is no per-document encryption, no per-role KEK, no key wrapping, no admin event publishing of wrapped KEKs, no KEK rotation mechanism. The entire field-level encryption layer (defense-in-depth layer 2) is absent.

**Action:** This is the largest single security gap. New subsystem: `kernel-security` DEK/KEK engine. Admin event format for wrapped key bundles. Role KEK lifecycle (generate, wrap per member, distribute, rotate, revoke). Capability negotiation must verify attestation bundles include valid KEKs before granting subscriptions.

---

### G04 — Per-Role Key Distribution via Admin Events [BLOCK] [ARCH]

**Book says:** Chapter 15. Admin generates per-role KEK from fresh entropy. Wraps KEK with each member's device public key. Publishes as signed admin events. Member node decrypts with device private key, stores KEK in OS keystore. Capability negotiation verifies attestation bundle.

**Sunfish has:** `kernel-security` has Ed25519 signing and per-team HKDF subkey derivation. Role attestation is scaffolded but not wired.

**Gap:** The admin event format for wrapped key bundles doesn't exist. No UI/API surface for an admin to issue KEKs. No member-side receipt flow. Attestation bundles in capability negotiation aren't verified against KEK possession.

**Action:** Define admin event schema for key distribution. Implement admin CLI/UI flow. Implement member receipt + OS keystore storage. Wire into capability negotiation.

---

### G05 — Incident Response Runbook [BLOCK] [ENT]

**Book says:** Chapter 15 + Dr. Voss Round 1 condition. A formal incident response procedure is required — not just an audit trail. Must cover: triggering events (physical loss, anomalous audit log, admin report), artifact collection, chain of custody, communication protocol, data-at-risk window disclosure, revocation procedure, new KEK generation, DEK re-wrapping.

**Sunfish has:** Audit trail (event log). No runbook.

**Gap:** No incident response documentation exists anywhere in the repo. The `ERR_KEY_REVOKED` error code exists in the gossip protocol but the upstream procedure that triggers it is not documented.

**Action:** Write formal incident response runbook in `_shared/product/` or `docs/operations/`. Cover: detection triggers, artifact collection, chain of custody, relay-enforced revocation, new KEK issuance, DEK re-wrapping background job, user notification with data-at-risk window.

---

### G06 — Stale Peer Recovery Protocol [BLOCK] [ARCH]

**Book says:** Chapter 12 + Prof. Shevchenko Round 2 condition. When a peer reconnects after a long offline period (predating the GC compaction boundary), deltas are unavailable. The protocol must fall back to full-state snapshot transfer. Without this, a peer that went offline for 90+ days cannot reconnect.

**Sunfish has:** Snapshot support in event log. No reconnect fallback logic when vector clock predates GC.

**Gap:** The recovery path is unspecified. Gossip daemon has no code path for "peer vector clock predates compaction boundary → initiate snapshot transfer."

**Action:** Implement fallback in gossip daemon: if delta computation returns "vector clock predates compaction boundary" error, initiate full snapshot push. Define error code in wire protocol. Add integration test covering 90-day offline scenario.

---

### G07 — Non-Technical Disaster Recovery Path [ARCH] [ENT]

**Book says:** Chapter 16. "Data ownership is meaningless if only a developer can perform the restore." User must restore after device loss without calling support. The walkthrough must be testable by a non-technical user. A backup status UX and recovery walkthrough are required.

**Sunfish has:** User-controlled cloud backup is listed as a storage tier in the paper but not implemented. QR onboarding transfers attestation bundle + CRDT snapshot.

**Gap:** No disaster recovery UX exists. No documented walkthrough. No backup status indicator. If a user loses their device, the recovery path is unclear. The QR onboarding partially covers this (peer-to-peer recovery) but full device-loss recovery (no second device) is not addressed.

**Action:** Design and implement disaster recovery UX for Anchor: backup configuration screen, backup status indicator, recovery walkthrough (non-technical). Define recovery path when no second device is available (cloud backup tier). Document the walkthrough. Write a test plan covering non-technical user recovery.

---

### G08 — Plain-File Export [ARCH]

**Book says:** Chapter 8 + Tomás Ferreira condition (passed). JSON/CSV/Markdown export with no vendor cooperation required, no internet needed, no subscription required. The export must be comprehensive — not a partial data dump.

**Sunfish has:** No export functionality visible in any package or accelerator.

**Gap:** No export path exists. This was an unconditional pass condition from the practitioner reviewer — Tomás Ferreira's only conditions in Round 1 was that this be added. It was added to the paper but not implemented.

**Action:** Implement export subsystem in `foundation-localfirst` or as a kernel plugin. Formats: JSON (complete), CSV (tabular records), Markdown (readable). Export must work offline. Add to Anchor UI: "Export all data." Add to Bridge: admin-level export.

---

## Important Gaps — Block Enterprise Beta

### G09 — Property-Based CRDT Test Harness [TEST]

**Book says:** Appendix D. ≥10,000 random operation sequences per CRDT property: convergence (all peers reach same state), idempotency (re-applying op is safe), commutativity (op order doesn't matter), monotonicity (state only grows). Required before first release.

**Sunfish has:** Integration tests. No property-based tests.

**Gap:** No property-based test harness exists for any CRDT property.

**Action:** Add `tests/kernel-crdt-property-tests/` using a property-based testing library. Cover all four CRDT properties. Target: 10,000 operations per property. Run in CI.

---

### G10 — Fault Injection & Deterministic Simulation Tests [TEST]

**Book says:** Appendix D. Fault injection (network partition, packet loss, node crash, recovery to consistent state). Deterministic simulation (mixed-version sync, epoch transitions offline, Flease edge cases, controllable clock). Chaos in staging (production-representative load, random kills, latency spikes).

**Sunfish has:** Wave 5.3.C load-test harness for `TenantWebSocketReverseProxy`. No partition/fault tests.

**Gap:** No fault injection, no deterministic simulation, no chaos harness.

**Action:** Three separate workstreams: (1) Fault injection suite in `tests/fault-injection/`, (2) Deterministic simulation harness with controllable clock, (3) Staging chaos setup (Aspire-based). Covers the mandatory scenarios: 30-day offline merge, CP quorum loss, 1000+ queued ops, N-1→N schema sync, offline epoch transition, couch device 3+ versions.

---

### G11 — Enterprise Procurement Checklist [ENT]

**Book says:** Chapter 19 + Dr. Voss Round 2 conditions. Non-negotiable before enterprise customers: MDM-compatible silent installation (Intune/Jamf, no user interaction, Group Policy config), signed and notarized binaries (Apple Developer ID + notarization; Authenticode + WDAC), SBOM at build time (CycloneDX format, Syft generation, Grype scanning, CVE SLA 14 days critical), defined incident response procedure (see G05), admin tooling for deprovisioning (console/CLI/API, status indicators, all-nodes confirmation), clear licensing (dual-license structure, CLA before first external PR).

**Sunfish has:** ADR 0018 (governance + license). No MDM packaging, no code signing pipeline, no SBOM, no deprovisioning tooling.

**Gap:** The entire enterprise procurement layer is absent. Six items are blocking enterprise deployments.

**Action:** Phase this across releases. SBOM + code signing are CI pipeline changes. MDM packaging is an installer project. Deprovisioning tooling is an admin backoffice feature. Dual-license structure needs legal review + repository configuration. ADR 0018 covers governance; need implementation.

---

### G12 — Supply Chain Security [ENT]

**Book says:** Chapter 15. Content-addressed updates via CID (CBOR hash identifier). Release signing key in HSM under multi-party quorum authorization. Sigstore transparency log for signing events. Reproducible builds for source-binary verification.

**Sunfish has:** Standard .NET build pipeline. No supply chain tooling.

**Gap:** No HSM, no Sigstore, no reproducible builds, no CID-addressed updates.

**Action:** Long-horizon work. Start with: Sigstore integration in CI pipeline. Reproducible build flags in .csproj. SBOM generation (part of G11). HSM procurement + multi-party signing is a governance decision.

---

### G13 — GDPR Article 17 Crypto-Shredding [ARCH] [ENT]

**Book says:** Chapter 15. To fulfill right-to-erasure requests: destroy the DEK for the subject's documents (content becomes unrecoverable). Operation metadata remains in the event log (cannot delete without breaking continuity). Organizations must obtain legal review of whether metadata constitutes personal data.

**Sunfish has:** Event log with append-only semantics. No DEK (see G03, which is a prerequisite).

**Gap:** Crypto-shredding cannot be implemented until DEK/KEK layer (G03) exists. This is a dependent gap.

**Action:** Implement G03 first. Then add `kernel-security` DEK destruction operation. Document the Article 17 limitation (metadata remains). Add to disaster recovery and admin runbooks.

---

### G14 — WireGuard Mesh VPN Tier 2 [ARCH]

**Book says:** Chapter 14. Three-tier peer discovery: mDNS (LAN), WireGuard mesh VPN (cross-network, NAT traversal), managed relay (fallback). The VPN tier enables cross-network peer sync without going through the managed relay.

**Sunfish has:** mDNS (tier 1) and managed relay (tier 3). No WireGuard integration visible.

**Gap:** Tier 2 peer discovery is absent. Cross-network peers that aren't in the same mDNS segment must use the relay, which adds latency and relay dependency.

**Action:** Research WireGuard integration options (.NET). Likely a separate adapter (not in kernel — the kernel defines the three-tier interface but doesn't implement the VPN layer directly). Define the adapter contract. Implement as optional plugin.

---

### G15 — Gossip Rate Limiter Hookup [ARCH]

**Book says:** Gossip anti-entropy has resource governance to prevent flooding.

**Sunfish has:** `ResourceGovernor` and `AllowInboundDelta` method exist in `kernel-sync` but are unhooked from `GossipDaemon`.

**Gap:** Rate limiter exists but doesn't execute. Delta floods are uncontrolled.

**Action:** Wire `ResourceGovernor` into `GossipDaemon.AllowInboundDelta`. Part of Wave 6.3 per-team service rewiring.

---

### G16 — Stream Compaction + Upcaster Retirement [DEFERRED]

**Book says:** Chapter 13. Background idempotent copy-transform migrator replays old stream through lenses, writes to new epoch stream, does not block sync. Old stream retained in archival mode until retention window expires.

**Sunfish has:** Lenses + epochs in `kernel-schema-registry`. No compaction job.

**Gap:** Old streams accumulate indefinitely. Wave 4.3 deferred.

**Action:** Implement background copy-transform migrator. Wire to epoch advancement. Add to schema migration runbook.

---

### G17 — CRDT Shallow Snapshots [DEFERRED]

**Book says:** Chapter 12. For documents where CRDT operation history exceeds memory budget, shallow snapshots allow pruning older ops while preserving read correctness via projection rebuild.

**Sunfish has:** Standard YDotNet snapshots. No shallow snapshot support.

**Gap:** Large documents accumulate unbounded operation history. Wave 4.4 deferred.

**Action:** Implement shallow snapshot support. Depends on Loro evaluation (Loro has native shallow snapshot support). May be deferred until Loro migration decision.

---

## Documentation Gaps — Book Playbooks Not Reflected

### G18 — Implementation Playbook (Ch17) [DOC]

**Book says:** Chapter 17 is a step-by-step guide to "Building Your First Node." This is the primary onboarding chapter for developers.

**Sunfish has:** Anchor accelerator as the Zone A reference implementation. No connection between Anchor and the ch17 playbook.

**Gap:** Developers reading ch17 cannot map its steps to Sunfish packages. The accelerator isn't documented as "here is how you follow the ch17 playbook with Sunfish."

**Action:** Add `accelerators/anchor/docs/getting-started-playbook.md` that maps ch17 steps to Sunfish packages and APIs.

---

### G19 — SaaS Migration Playbook (Ch18) [DOC]

**Book says:** Chapter 18 is a guide to migrating an existing SaaS to the inverted stack. Bridge is positioned as the Zone C migration path.

**Sunfish has:** Bridge accelerator. ADR 0031 explains the Zone C model. No migration guide document.

**Gap:** Teams migrating an existing SaaS to Bridge have no step-by-step guide. The migration strategy (control-plane narrowing, per-tenant hosted-node, browser shell) is implicit in ADR 0031 but not documented as a migration guide.

**Action:** Add `accelerators/bridge/docs/migration-guide.md` that maps ch18 steps to Bridge implementation stages.

---

### G20 — Enterprise Shipping Guide (Ch19) [DOC] [ENT]

**Book says:** Chapter 19 covers shipping to enterprise: MDM deployment, compliance requirements, procurement process, negotiation posture.

**Sunfish has:** ADR 0018 covers governance/licensing. No enterprise guide.

**Gap:** No document explaining how to deploy Sunfish in an enterprise environment (MDM, air-gap, compliance attestations, procurement questions).

**Action:** Add `docs/enterprise-deployment-guide.md`. Covers MDM, compliance posture, procurement checklist, SBOM, incident response.

---

### G21 — Sync UX and Conflict Resolution Guide (Ch20) [DOC]

**Book says:** Chapter 20 provides specific UX guidance for surfacing sync states and conflict resolution to users. The SyncState enum must surface correctly at every tier.

**Sunfish has:** `SyncState` enum in foundation, `SunfishConflictList`, `SunfishFreshnessBadge` components. Sync State Alignment Invariant defined.

**Gap:** The UX patterns from ch20 (how to present conflicts to users, conflict resolution flows, what each SyncState means in user-visible language) are not documented in Sunfish. The invariant is defined but the UX guidance isn't codified.

**Action:** Add `docs/sync-ux-guide.md`. Document each SyncState in user-visible language, show resolution flows, reference the ch20 patterns.

---

### G22 — Relay Metadata Privacy + Self-Hosted Relay Mitigation [DOC]

**Book says:** Chapter 15 + Nia Okonkwo condition. Relay sees connection metadata (who syncs with whom, frequency) even though it cannot decrypt content. Self-hosted relay mitigates this. Documentation must disclose this limitation.

**Sunfish has:** Relay implemented in Bridge. No metadata privacy disclosure.

**Gap:** The managed relay's metadata visibility is not disclosed. Teams deploying Bridge don't know the relay operator sees connection metadata.

**Action:** Add metadata privacy disclosure to Bridge documentation and relay operator agreement. Document self-hosted relay option.

---

## Commercial / Governance Gaps

### G23 — Dual-License Structure Implementation [ENT]

**Book says:** Chapter 8 + Jordan Kelsey Round 2 conditions. AGPLv3 default + commercial license for enterprises. CLA required before first external PR. Dual-license decision must be made before repository opens publicly.

**Sunfish has:** ADR 0018 covers governance and license. Per memory, repo stays private until LLC forms (these are coupled decisions).

**Gap:** ADR 0018 is accepted but the CLA mechanism, commercial license text, and legal structure are not in place. Per project memory, LLC formation precedes public flip — so this is blocked on LLC formation.

**Action:** Track separately. LLC formation triggers this workstream. Prepare CLA template, commercial license text, and Contributor License Agreement tooling in advance. Coordinate with legal counsel.

---

### G24 — Analytics/Telemetry Model [ARCH]

**Book says:** Chapter 9 + Dr. Voss Round 2. Telemetry model must be specified before product manager pressure introduces re-centralization. Operator telemetry must not recentralize the architecture.

**Sunfish has:** No telemetry specification anywhere.

**Gap:** As Bridge matures into a commercial product, there will be pressure to add server-side analytics for funnel reports, usage tracking, etc. Without a specified boundary, implementation drift could silently violate the data minimization invariant.

**Action:** Define telemetry boundary in `_shared/product/telemetry-model.md`. Specify: what operators may observe, what they may not, what users must consent to. Establish the principle: telemetry must not reveal content or create a centralized behavior graph.

---

### G25 — RelayServer Identity Persistence [ARCH]

**Book says:** Relay participates in peer discovery and is addressable by node_id. Identity must persist across restarts.

**Sunfish has:** RelayServer generates fresh identity on each restart (known defect in audit).

**Gap:** Restarting the relay breaks peer discovery because the node_id changes. Other nodes have bookmarked the old relay node_id.

**Action:** Implement persisted relay identity in `INodeIdentityProvider` backed by OS keystore in `local-node-host`. Fix tracked as known defect.

---

## Gap Summary Table

| ID | Gap | Priority | Blocked By | Wave/Phase |
|----|-----|----------|------------|------------|
| G01 | Three-tier CRDT GC policy | CRITICAL | — | New Wave |
| G02 | Flease split-write fence | CRITICAL | — | kernel-lease |
| G03 | DEK/KEK field-level encryption | CRITICAL | — | New Phase |
| G04 | Per-role key distribution | CRITICAL | G03 | New Phase |
| G05 | Incident response runbook | CRITICAL | — | Docs |
| G06 | Stale peer recovery protocol | CRITICAL | G01 | kernel-sync |
| G07 | Non-technical disaster recovery | CRITICAL | — | Anchor/Bridge |
| G08 | Plain-file export | CRITICAL | — | New feature |
| G09 | Property-based CRDT tests | HIGH | — | tests/ |
| G10 | Fault injection + simulation | HIGH | — | tests/ |
| G11 | Enterprise procurement checklist | HIGH | — | Multi-workstream |
| G12 | Supply chain security | HIGH | — | CI/ops |
| G13 | GDPR crypto-shredding | HIGH | G03 | kernel-security |
| G14 | WireGuard VPN tier 2 | MEDIUM | — | New adapter |
| G15 | Gossip rate limiter hookup | MEDIUM | Wave 6.3 | kernel-sync |
| G16 | Stream compaction | MEDIUM | — | Wave 4.3 |
| G17 | CRDT shallow snapshots | MEDIUM | Loro eval | Wave 4.4 |
| G18 | Ch17 implementation playbook | MEDIUM | — | Docs |
| G19 | Ch18 migration playbook | MEDIUM | Wave 5.2 | Docs |
| G20 | Ch19 enterprise guide | MEDIUM | G11 | Docs |
| G21 | Ch20 sync UX guide | LOW | — | Docs |
| G22 | Relay metadata privacy | LOW | — | Docs |
| G23 | Dual-license implementation | BLOCKED | LLC formation | Legal |
| G24 | Analytics/telemetry model | MEDIUM | — | _shared/product |
| G25 | RelayServer identity persistence | MEDIUM | — | local-node-host |

---

## Recommended Plan of Action

### Track A — Security Architecture (G03, G04, G13) — New ICM Request

DEK/KEK field-level encryption is the largest unaddressed system. It requires a new ICM request using the `sunfish-feature-change` variant. Scope:
- New `IDocumentEncryption` service in `kernel-security`
- Admin key bundle format (CBOR) and event schema
- Member key receipt + OS keystore storage flow
- Capability negotiation attestation verification
- GDPR crypto-shredding operation

This is a multi-wave effort. Start with a design ADR.

### Track B — Testing Infrastructure (G09, G10) — New ICM Request

The 5-level testing pyramid is completely unstarted at levels 1, 3, 4, 5. This is a `sunfish-test-expansion` ICM request. Scope:
- Property-based test library selection and harness
- Fault injection suite (partition, crash, packet loss)
- Deterministic simulation framework
- Staging chaos configuration

### Track C — Critical Invariants (G01, G02, G06) — Feature Change Requests

Three requests into existing kernel packages:
- `kernel-crdt`: Three-tier GC policy (G01)
- `kernel-lease`: Split-write safety fence (G02)
- `kernel-sync`: Stale peer recovery protocol (G06)

### Track D — Operational Documentation (G05, G07, G08) — Docs + Feature

- Incident response runbook (G05): `sunfish-docs-change`
- Disaster recovery UX (G07): `sunfish-feature-change` (Anchor + Bridge)
- Plain-file export (G08): `sunfish-feature-change` (new subsystem)

### Track E — Enterprise Readiness (G11, G12, G24, G25) — Quality Control

- Enterprise procurement: `sunfish-quality-control` audit → implementation plan
- Supply chain security: CI pipeline changes
- Telemetry model: `_shared/product/telemetry-model.md`
- RelayServer identity: Bug fix in `local-node-host`

### Track F — Documentation Playbooks (G18, G19, G20, G21, G22) — Docs Change

Five `sunfish-docs-change` requests:
- Ch17 playbook → Anchor getting started
- Ch18 migration guide → Bridge migration guide
- Ch19 enterprise guide → `docs/enterprise-deployment-guide.md`
- Ch20 sync UX guide → `docs/sync-ux-guide.md`
- Relay metadata privacy disclosure

### Sequencing

```
Week 1-2:   Track C (G01, G02, G06) — kernel invariants, no dependencies
            Track D docs (G05) — incident response runbook
Week 2-4:   Track A design ADR — DEK/KEK architecture
            Track B test harness setup (G09)
Week 4-6:   Track A implementation — Phase 1 (admin key bundle + member receipt)
            Track D feature (G07, G08) — disaster recovery + export
            Track B fault injection (G10)
Week 6-8:   Track A implementation — Phase 2 (capability negotiation wiring)
            Track E enterprise procurement (G11, G12)
            Track F documentation (G18-G22)
Ongoing:    G14 (WireGuard) — research spike, low urgency
            G23 (dual-license) — blocked on LLC formation
```

---

*Exit criteria for this discovery stage: gap table reviewed and approved by Chris Wood. Each gap classified as actionable, deferred, or blocked. Track A–F routed into ICM backlog.*

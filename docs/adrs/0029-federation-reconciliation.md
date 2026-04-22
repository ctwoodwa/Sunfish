# ADR 0029 — Federation vs. Gossip Reconciliation

**Status:** Proposed
**Date:** 2026-04-22
**Resolves:** Sunfish ships four `packages/federation-*` packages implementing a signed-envelope federation model (`ISyncTransport`, `InMemorySyncTransport`, `IPeerRegistry`, `SyncEnvelope`, `EnvelopeSigning`, RIBLT-reconciled entity and capability sync via `IEntitySyncer` / `ICapabilitySyncer` / `CapabilityReconcileResult`, and Kubo-backed IPFS blob replication via `IpfsBlobStore`). The local-node architecture paper's §6.1 (Gossip Anti-Entropy) and §6.2 (Sync Daemon Protocol) mandate a gossip anti-entropy daemon with a specific protocol shape — HELLO, CAPABILITY_NEG, DELTA_STREAM, GOSSIP_PING on a 30-second tick, leaderless, random peer selection. These are materially different sync models; both cannot be the single canonical path. Paper-alignment audit 2026-04-22 §2.4 and §5 conflict γ called this out as a structural conflict that must be resolved before Wave 1 build.

## Context

Two sync models exist in the tree, with overlapping but non-identical intent:

**Federation model (existing code, 4 packages).** Explicit envelope exchange between authenticated peers. `ISyncTransport` is request/response rather than streaming. Peers are enumerated via `IPeerRegistry`. Envelopes are individually signed (`EnvelopeSigning`). Entity sync uses a change-store/protocol split (`IChangeStore` + `federation-entity-sync/protocol/`). Capability sync uses RIBLT (Rateless Invertible Bloom Lookup Tables) to reconcile set differences between peers (`federation-capability-sync/RIBLT`, `Riblt`). Blobs are content-addressed and replicated via IPFS/Kubo. The mental model is two distinct organizations exchanging a curated bundle of state.

**Gossip model (paper §6.1–6.2).** Leaderless, periodic (≈30s tick), randomized peer selection inside a single trust domain. Protocol is streaming: HELLO → CAPABILITY_NEG → DELTA_STREAM, with GOSSIP_PING as a keep-alive. Authentication is by role attestation, not per-envelope signature. Transport is a Unix-socket-backed kernel daemon (§5.1 kernel responsibility). The mental model is anti-entropy between peer nodes that already trust each other.

The intents are different. Federation is inter-organizational — cross-jurisdictional, cross-team, or relay-mediated. Gossip is intra-team — same trust domain, authenticated via role attestation, assumes tight network proximity. Collapsing either into the other loses the structure.

## Decision drivers

- The paper is the source of truth for sync semantics; §6.1–6.2 specifies gossip, not federation.
- The existing federation packages represent real engineering investment (envelope signing, RIBLT, IPFS blob replication) and should not be discarded without cause.
- Sunfish is pre-release; breaking API changes are allowed, but avoidable churn should be avoided when a natural architectural split exists.
- Paper §6.1 describes peer discovery as a tiered stack (tier 1 mDNS, tier 2 VPN, tier 3 "managed relay (optional)"). The tier-3 relay hint is a natural home for the federation model.
- ADR 0013 (foundation-integrations) already positions cross-jurisdictional exchange as a separate concern from intra-team runtime.

## Considered options

### Option A — Retrofit `federation-*` as the gossip transport layer

The gossip daemon would use `ISyncTransport` under the hood; HELLO / CAPABILITY_NEG / DELTA_STREAM / GOSSIP_PING become envelope types on the federation layer.

- Pro: maximum reuse of existing transport, signing, peer registry.
- Con: the semantic mismatch (inter-org federation vs intra-team gossip) leaks through. Role-attestation key distribution, which is the gossip trust primitive, does not map naturally onto per-envelope signed federation. Gossip's streaming DELTA_STREAM is at odds with request/response envelope exchange. The retrofit produces a worse version of both protocols.

### Option B — Keep federation as inter-organizational sync; add new `packages/kernel-sync/` as intra-team gossip

Two packages, two protocols, two scopes. `federation-*` remains cross-jurisdictional, relay-mode, IPFS-backed. `kernel-sync` becomes intra-team, role-attestation-driven, Unix-socket-daemon, implementing §6.1–6.2 verbatim.

- Pro: clean semantic split that matches the paper's own §6.1 tier structure (mDNS → VPN → relay).
- Pro: no retrofit churn on federation packages.
- Pro: preserves IPFS blob replication (paper §2.4 Tier 4 "Content-addressed distribution"), which has value independent of gossip.
- Con: two codebases to maintain; need cross-tests to verify the boundary is clean.

### Option C — Deprecate `federation-*` entirely; rewrite as gossip only

- Pro: single sync path, single mental model.
- Con: throws away working envelope signing, RIBLT reconciliation, and IPFS-backed content-addressed blob replication. Loses the cross-jurisdictional use case entirely, which the paper still needs at tier 3.

### Option D — Merge both models into one hybrid protocol

- Pro: one protocol to learn.
- Con: protocols are defined by their constraints; combining the constraints of gossip (intra-team, leaderless, role-attested, streaming) with those of federation (inter-org, enumerated peers, per-envelope signed, request/response) yields a protocol that is worse than either alone at its intended job.

## Decision (recommended)

**Adopt Option B — dual-track: federation + kernel-sync.**

Rationale:

- The paper's §6.1 peer-discovery tiering (mDNS / VPN / managed relay) is itself a dual-track story. Gossip between local peers uses tier 1 / tier 2; relay-mediated federation uses tier 3. Two packages map cleanly onto the two tiers.
- `federation-blob-replication`'s IPFS-backed content-addressed blob replication is architecturally correct per paper §2.4 Tier 4 and should not be rewritten.
- `federation-capability-sync`'s RIBLT reconciliation is a reasonable cross-team mechanism; it stays in federation, not gossip.
- Gossip needs a home anyway — `packages/kernel-sync/` is the natural place, as paper §5.1 locates the sync daemon inside the kernel boundary.
- Pre-release freedom means no backward-compat debt is owed to federation consumers; the split is additive — federation packages keep their surfaces, kernel-sync appears alongside.

## Decision consequences

### Positive

- Paper alignment without regression to existing federation work.
- Clear separation: federation = "managed relay + cross-jurisdictional"; kernel-sync = "intra-team gossip."
- Keeps IPFS blob replication as a first-class capability.
- Leaves room for both tier-3 and tier-1/2 stacks to evolve on independent cadences.

### Negative

- Two sync codebases to maintain; cross-tests needed to verify there is no gap between them.
- Risk of unintended overlap at the cross-team / relay boundary — explicit ADR language or a boundary test is needed to document which protocol handles what.
- Documentation burden: consumers need to know which protocol applies to their topology.

## Compatibility plan

- All four `federation-*` package README files gain a scope clarification: "inter-team / cross-jurisdictional / relay-mediated. NOT intra-team gossip; for intra-team sync see `packages/kernel-sync/`."
- `packages/kernel-sync/` is scaffolded in Wave 2.1 of the paper-alignment plan, implementing HELLO / CAPABILITY_NEG / DELTA_STREAM / GOSSIP_PING per paper §6.2.
- A new `docs/specifications/sync-architecture.md` documents the tier-1/tier-2 vs tier-3 boundary and links to the sync-daemon-protocol spec.
- No public API change to existing federation packages is required by this ADR.

## Implementation checklist

- [ ] Scaffold `packages/kernel-sync/` per Wave 2.1 of the alignment plan.
- [ ] Update all four `packages/federation-*/README.md` files with the scope clarification.
- [ ] Author `docs/specifications/sync-architecture.md` documenting the tier boundary.
- [ ] Cross-test in Wave 2: spin up team-A (federation + kernel-sync) and team-B (federation + kernel-sync); verify A-gossips-to-A, A-federates-to-B, and no gossip traffic leaks across the team boundary.
- [ ] Add a follow-up ADR or spec note if the relay / cross-team boundary needs sharper definition after Wave 2 cross-tests.

## References

- `_shared/product/local-node-architecture-paper.md` §6.1 (Gossip Anti-Entropy), §6.2 (Sync Daemon Protocol), §2.4 (Content-addressed distribution), §5.1 (kernel responsibility)
- `packages/federation-common/` — `ISyncTransport`, `InMemorySyncTransport`, `IPeerRegistry`, `SyncEnvelope`, `EnvelopeSigning`
- `packages/federation-entity-sync/` — `IEntitySyncer`, `IChangeStore`, `protocol/`
- `packages/federation-capability-sync/` — `ICapabilitySyncer`, `CapabilityReconcileResult`, `RIBLT`, `Riblt`
- `packages/federation-blob-replication/` — `IpfsBlobStore`, `Kubo`
- `icm/07_review/output/paper-alignment-audit-2026-04-22.md` §2.4, §5 conflict γ
- ADR 0013 — foundation-integrations
- ADR 0027 — kernel-runtime-split

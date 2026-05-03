# Platform Phase D: Federation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

---

## Platform Context

> **⚠ Read this before executing.** Phase D is the **federation track** of the Sunfish platform — peer-to-peer sync of entities, capability graph, and binary blobs between Sunfish nodes running in different jurisdictions. It is the spec-§10.4 story made concrete: two property management companies push inspection data to a municipal code-enforcement agency, a central military command syncs with air-gapped child bases, a PM company grants a contractor portal access bounded by a Macaroon.
>
> The authoritative platform specification is `docs/specifications/sunfish-platform-specification.md` (v0.2). Phase D operationalizes:
>
> - **§2.5 Federation Model** — peer-to-peer at the entity level, not the organization level; three patterns (push / pull / gossip); transport-pluggable (HTTP/2, libp2p, sneakernet).
> - **§3.6 Event Bus** — libp2p pubsub is named as a federation transport; Phase D uses HTTP+TLS first, libp2p deferred.
> - **§3.7 Blob Store** — `IpfsBlobStore` is the federation-ready backend alternative to `FileSystemBlobStore`.
> - **§10.4 Federation Patterns** — three canonical scenarios Phase D must demonstrate end-to-end (Pattern A: PM companies + city code enforcement; Pattern B: base command with air-gapped child bases; Pattern C: contractor portal).
>
> Two research notes inform every design decision:
>
> - `docs/specifications/research-notes/automerge-evaluation.md` — Automerge's sync-protocol shape (peers negotiate heads, exchange missing changes, deterministic merge) is the reference for entity-side federation; Keyhive's **RIBLT set reconciliation** is the reference for capability-graph sync.
> - `docs/specifications/research-notes/ipfs-evaluation.md` — IPFS-Cluster for multi-node blob replication; private IPFS networks via swarm keys for enterprise; silent-content-loss detection via periodic attestations.
>
> **Where this Phase D plan fits:** Phase D runs **after** Platform Phases A (asset modeling — entity + version + audit primitives) and B (crypto + capability graph). Phase D federates what A+B produce. This plan explicitly documents those as prerequisites and refuses to ship if they're missing (see the prerequisite section below).
>
> **What makes Phase D a distinct phase (not a sub-task of A or B):**
>
> 1. Phase D introduces **network protocol surface** — sync-message envelopes, HTTP endpoint contracts, RIBLT encoders, Kubo RPC clients. These are new primitives that don't exist in A/B at all.
> 2. Phase D adds a **large operational footprint** — Kubo daemons, IPFS-Cluster with Raft consensus, swarm-key management, pinning policy. These are sidecar infrastructure concerns that need their own operator guide.
> 3. Phase D's correctness story is **integration-test-heavy** — two or three peer nodes, network transport, cross-peer CRDT merge, blob replication timing. The tests need Testcontainers and a dedicated fixture abstraction.
> 4. Phase D delivers the **three canonical deployment patterns** (spec §10.4) end-to-end. Pattern A (PM + city) is the primary demo; B (base command) and C (contractor portal) round out the capability surface.

---

**Goal:** Ship three independent but composable federation concerns — entity sync, capability-graph sync, and blob replication — packaged as `Sunfish.Federation.*` libraries with HTTP+TLS transport, Kubo-sidecar IPFS integration, and end-to-end integration tests covering the three canonical spec-§10.4 patterns. The result is that two or more Sunfish nodes running in different jurisdictions can reconcile their entity state, capability graph, and attached blobs deterministically, with every operation signed, every blob content-addressed, and every transport observable.

**Architecture context:** Federation packages are **framework-agnostic server-to-server libraries**. They depend only on Foundation (crypto, blobs, entity + version + audit primitives from Phases A/B/B-blobs) and on .NET BCL + `HttpClient`. They do **not** depend on Blazor, UI-core, or any adapter. The Phase 2 `HasNoBlazorDependency` invariant holds for every new package in this phase.

**Source references:**

- Spec §2.5 Federation Model, §3.6 Event Bus, §3.7 Blob Store, §10.4 Federation Patterns.
- `automerge-evaluation.md` §4.3 (sync protocol shape), §1.3 (Keyhive RIBLT).
- `ipfs-evaluation.md` §3 (material mismatches and concerns — especially §3.2 .NET library maturity, §3.4 public DHT leakage, §3.5 pinning ≠ durability), §4 (how IPFS composes with Automerge + Keyhive).
- Phase 9 bridge accelerator plan (`2026-04-17-sunfish-phase9-bridge-accelerator.md`) — style and structure reference; PLATFORM_ALIGNMENT.md in Task 9-10 already names "Platform Phase D (federation)" as this plan.

**Tech Stack:** .NET 10, C# 13, `HttpClient` with `System.Net.Http.Json`, `NSec.Cryptography` (Ed25519 from Phase B), IPFS Kubo daemon (Go sidecar — runs in its own container), IPFS-Cluster (Go sidecar — Raft consensus), Testcontainers 4.x for IPFS integration fixtures, xUnit 2.9.x, NSubstitute 5.3.x. No direct IPFS .NET library (see D-KUBO-CLIENT).

**Prerequisite phases — this plan does NOT ship if these are missing:**

1. **Platform Phase A (asset modeling)** — must produce: `Sunfish.Foundation.Entities.Entity`, `Sunfish.Foundation.Versions.Version` (CRDT change-log shape per automerge-evaluation §4.1), `Sunfish.Foundation.Audit.AuditRecord` with hash-chain `Prev` link per spec §3.3. Phase D's entity sync reads and writes these primitives directly.
2. **Platform Phase B (crypto + capability graph)** — must produce: `Sunfish.Foundation.Crypto.Ed25519KeyPair`, `Sunfish.Foundation.Crypto.SignedOperation<T>`, `Sunfish.Foundation.Capabilities.Principal` / `.Group` / `.MembershipOp` per spec §10.2.1. Phase D's capability sync reconciles these CRDT ops between peers; entity sync verifies signatures on every received change using Phase B's verification primitives.
3. **Platform Phase B-blobs** — must produce: `Sunfish.Foundation.Blobs.IBlobStore`, `Sunfish.Foundation.Blobs.Cid`, `Sunfish.Foundation.Blobs.FileSystemBlobStore` (shipped — per `ipfs-evaluation.md` §6.3 recommendation). Phase D adds `Sunfish.Federation.BlobReplication.IpfsBlobStore` as a sibling `IBlobStore` backend.

**Prerequisite verification — Task D-0 Step 1:** `dotnet build packages/foundation/Sunfish.Foundation.csproj` must succeed, and the following symbols must resolve (spot-checked via `grep` and `dotnet tool reference-check` or equivalent): `Sunfish.Foundation.Entities.Entity`, `Sunfish.Foundation.Versions.Version`, `Sunfish.Foundation.Audit.AuditRecord`, `Sunfish.Foundation.Crypto.SignedOperation<T>`, `Sunfish.Foundation.Capabilities.Principal`, `Sunfish.Foundation.Capabilities.MembershipOp`, `Sunfish.Foundation.Blobs.IBlobStore`, `Sunfish.Foundation.Blobs.Cid`. If any are missing, halt — refer back to the phase that owns the primitive.

---

## Scope

### In scope

1. **Entity sync** (`Sunfish.Federation.EntitySync.*`)
   - `IEntitySyncer.PullFromAsync(peer)` and `.PushToAsync(peer)` — Automerge-style delta sync.
   - Wire-format for "I have heads X, Y, Z" announcement + "here are the changes you need" response.
   - Signature verification on every received change.
   - HTTP+JSON transport with server-side endpoint; in-memory transport for tests.
   - Deterministic CRDT merge on both sides, reusing Phase A's `Version` merge operator.
   - Integration test: two peers reconcile divergent histories and converge.

2. **Capability graph sync** (`Sunfish.Federation.CapabilitySync.*`)
   - `ICapabilitySyncer.ReconcileAsync(peer)` — RIBLT-based set reconciliation of signed membership ops.
   - RIBLT encoder/decoder for `SignedOperation<MembershipOp>` set differences.
   - Per-op Ed25519 signature verification on receive.
   - Revocation semantics: removing a member is itself a CRDT op; it must sync and apply consistently.
   - Integration test: two peers, divergent capability ops including a revocation; convergence and correct final access state.

3. **Blob replication** (`Sunfish.Federation.BlobReplication.*`)
   - `IpfsBlobStore : IBlobStore` implemented over Kubo's HTTP RPC API (`/api/v0/*`).
   - Startup registration (`AddSunfishFederation`) wires the Kubo client from config and fails fast if private-network config is missing in production mode (D-PRIVATE-NETWORK).
   - IPFS-Cluster integration: pinning via Cluster's Raft-consensus API; replication factor 3 by default (D-PINNING-POLICY).
   - Attestation producer: 24-hour periodic signed statement "I have CIDs A, B, C as of T" (D-ATTESTATION).
   - Integration test: three-node Cluster via Testcontainers, `PutAsync` on node 1 produces a CID, `GetAsync` on node 3 retrieves same bytes after replication.

4. **Three canonical pattern integration tests** (spec §10.4)
   - **Pattern A**: Two PM companies + city code-enforcement agency share inspection data (entity sync + capability sync + blob replication, all three at once).
   - **Pattern B**: Base command federates with air-gapped child base via signed JSON bundle export/import (no live network; verifies Phase D's transport abstraction supports offline transfer).
   - **Pattern C**: Contractor portal with Macaroon-bound ephemeral access (reuses Phase B Macaroon primitive; no persistent federation — contractor sees a scoped read-view backed by the PM company's node).

5. **Operator guide** — private-network setup, swarm-key generation, IPFS-Cluster config, Kubo version pinning, attestation schedule, common troubleshooting.

### Out of scope

- **BeeKEM continuous group key agreement** — the research edge. Keyhive's group-key agreement is replaced with a simpler **pre-shared group secret** for Phase D. Rationale per `automerge-evaluation.md` §4.2: BeeKEM is active Ink & Switch research, not production-ready. Phase D's confidentiality model is: transport is TLS, blobs can be pre-encrypted by the producer before `PutAsync`. Group key agreement becomes its own phase when Keyhive is mature.
- **Byzantine fault tolerance.** Sunfish federation assumes peers follow the protocol. Malicious peers producing invalid signatures are rejected at verification time (which is enforced). Malicious peers producing protocol-conformant but semantically-bogus ops, or DoS-ing the sync endpoint, are out of scope.
- **Cross-chain / blockchain integration.** Spec §12.1 considers blockchain governance as a research reference, not a Phase D deliverable.
- **WebRTC / Bluetooth transports.** Ship HTTP+TLS only for Phase D. Additional transports are follow-ups (§10.4 mentions libp2p for future optionality).
- **WebAssembly runtime.** No Blazor WASM federation. Phase D is server-to-server only.
- **libp2p in-process.** No mature .NET libp2p. Per `ipfs-evaluation.md` §8 open question 4, `nethermind/dotnet-libp2p` is immature. Sidecar Kubo is the chosen integration path (D-IPFS-INTEGRATION).
- **Full IPNS / DIDs integration.** Mutable pointers via IPNS and DID-based identity resolution are platform-wide decisions. Phase D uses Ed25519 keys directly; DID layering is a separate track.

---

## Key Decisions

**D-SYNC-TRANSPORT (new):** HTTP+JSON over TLS for Phase D. Rationale: universally supported, easy to debug with `curl`, works behind corporate firewalls that block peer-to-peer transports, and every team running Kubernetes already has TLS termination solved. The sync-message envelope is transport-neutral so libp2p or gRPC can slot in later as additional `ISyncTransport` implementations. See `automerge-evaluation.md` §4.3 — the sync protocol shape is conn-agnostic. Upgrade to libp2p becomes a future phase once the mental model is stable and there is a driving requirement (e.g., NAT traversal for contractor-to-contractor sync).

**D-CRDT-LIB (new):** Sunfish-native CRDT change log from Platform Phase A. **Do not introduce a new CRDT library.** The sync protocol is Sunfish's, inspired by Automerge's design. Per `automerge-evaluation.md` §3.1: there is no .NET Automerge binding, and integrating automerge-rs via P/Invoke or a sidecar costs more than it buys for Phase D's needs. Phase A's `Version` change log is the CRDT; Phase D sends and merges changes from it.

**D-IPFS-INTEGRATION (new):** Sidecar Kubo daemon, per `ipfs-evaluation.md` §3.2 recommendation and §8 open question 4. The Kubo Go binary runs in its own container (or host process) and exposes its HTTP RPC API on `localhost:5001` by default. `IpfsBlobStore` is a thin .NET HTTP client talking to that API. **Do not** attempt to embed libp2p in-process — no mature .NET libp2p exists, and operating IPFS is its own discipline (pinning policy, bootstrap peer management, swarm key rotation) that benefits from deployment-level isolation.

**D-KUBO-CLIENT (new):** Write a thin .NET HTTP client for Kubo's RPC, not a wrapper around the dormant `Ipfs.Http.Client` (richardschneider) package. Per `ipfs-evaluation.md` §3.2: `Ipfs.Http.Client` last updated August 2019, targets .NET Standard 1.4/2.0, viable but dormant. A Phase D-owned thin client (estimated ~400-600 lines covering `/api/v0/add`, `/api/v0/cat`, `/api/v0/block/get`, `/api/v0/pin/add`, `/api/v0/pin/ls`, `/api/v0/pin/rm`, `/api/v0/config`, `/api/v0/swarm/peers`) gives full control over error handling, cancellation, and HTTP pipeline integration. The Kubo RPC spec is stable — maintenance cost is low.

**D-PRIVATE-NETWORK (new):** Production federated Sunfish deployments run **private IPFS networks** with swarm keys. Per `ipfs-evaluation.md` §3.4 public DHT leakage risk. `AddSunfishFederation()` must fail fast at startup if:
1. `Sunfish:Federation:Environment` is `Production`, AND
2. No swarm-key path is configured OR the Kubo daemon's `/api/v0/config` reports a non-private-network profile.

Development and test environments may run without swarm keys (local-only Kubo) for convenience. The fail-fast check emits a `LogLevel.Critical` message with the specific misconfiguration before throwing.

**D-PINNING-POLICY (new):** IPFS-Cluster manages pinning. Default replication factor is **3 nodes**. Consumers configure per-organization pinning policy via `IBlobPinningPolicy` (what gets pinned on Put, what gets unpinned on entity delete, what the cluster-wide min/max replication factor is). Default policy pins every blob produced via `PutAsync`; delete semantics are entity-layer (a deleted entity unpins its referenced CIDs only if no other entity still references them — reference-counted via the entity store).

**D-ATTESTATION (new):** Every pinning node produces a signed attestation every **24 hours**: "I have these CIDs as of timestamp T." Attestation is a signed JSON record stored in the audit log. Sunfish kernel can query attestations to detect silent content loss before consumers do — if a CID has zero attestations in the last 72 hours across the cluster, it raises an operational alert. See `ipfs-evaluation.md` §3.5 (pinning ≠ durability) and §7.4 (silent content loss mitigation). Attestation interval is configurable (`Sunfish:Federation:AttestationIntervalHours`, default 24); 72-hour alert threshold is also configurable.

**D-AUDIT-TRAIL-FEDERATION (new):** Phase A's audit log also federates. Per spec §3.3, the audit log is a per-entity hash chain with `Prev` references forming a Merkle-like DAG. When Phase D's entity sync transfers a change, it also transfers the corresponding audit record(s), and the receiving peer verifies both the operation signature AND the audit chain linkage. An auditor in jurisdiction B can verify that records received from jurisdiction A are internally consistent and untampered, even if A has ceased operations. This is a first-class Phase D deliverable, not an incidental side effect.

**D-NO-BLAZOR-INVARIANT (inherited from Phase 2):** Every federation package has zero Blazor, UI-core, ui-adapter dependencies. Verified via the same `HasNoBlazorDependency` .csproj check used by Foundation. Federation is server-to-server; the federated-data-consumption UI is a separate composition layer (blocks, adapters, accelerator code).

**D-PEER-AUTH (new):** Peer-to-peer transport authentication is **mutual TLS** (mTLS) — each Sunfish node has a server certificate (for its sync endpoint) and a client certificate (used when pulling from peers). Certificate pinning is supported but not required; the default model trusts a configured peer-certificate fingerprint list. Rationale: avoids the complexity of a global PKI for Phase D, keeps peer enrollment an explicit operator action.

**D-RIBLT-COMPLEXITY (new — risk acknowledgement):** RIBLT (Rateless Invertible Bloom Lookup Tables) is the reconciliation primitive Keyhive uses for set sync. Per `automerge-evaluation.md` §1.3 Keyhive section. RIBLT is a genuinely complex algorithm — the Keyhive notebook covers it but there is no production-grade .NET implementation. Task D-4 implements a **first-pass RIBLT encoder/decoder** backed by test vectors from the Keyhive reference implementation (JavaScript). A fallback **full-set exchange** mode is also shipped for correctness reassurance: when RIBLT fails to decode or the estimated diff exceeds a threshold, peers fall back to exchanging the full set. This keeps Phase D shippable even if RIBLT turns out to have edge cases we miss the first time.

**D-MACAROON-REUSE (new):** Pattern C (contractor portal) reuses Phase B's Macaroon primitive, not a new federation-specific token. Per spec §10.2.2: Macaroons are the supplementary delegation primitive for short-lived, third-party, or scope-bounded scenarios. Phase D's contribution for Pattern C is the **federated read-only view endpoint** that accepts a Macaroon bearer token and serves entity data filtered by the token's caveats — not a new token format.

**D-TESTCONTAINERS (new):** IPFS integration tests use Testcontainers 4.x with the official Kubo Docker image (`ipfs/kubo:v0.28.0` pinned) and IPFS-Cluster Docker image (`ipfs/ipfs-cluster:v1.0.8` pinned). Each test fixture spins up 1-3 containers, waits for readiness via the `/api/v0/id` endpoint, runs the test, tears down. Pinning the image versions avoids "the tests passed last month but fail today because Kubo changed" drift.

---

## File Structure

```
packages/
  federation-common/                                               ← new package
    Sunfish.Federation.Common.csproj
    SyncEnvelope.cs                                                ← signed message wrapper
    ISyncTransport.cs                                              ← transport abstraction
    InMemorySyncTransport.cs                                       ← test transport
    HttpSyncTransport.cs                                           ← production transport
    SignedSyncMessage.cs                                           ← integration with SignedOperation<T>
    PeerDescriptor.cs                                              ← peer identity + certificate fingerprint
    IPeerRegistry.cs                                               ← known peer list
    FederationEnvironment.cs                                       ← Dev / Staging / Production enum
    Extensions/
      ServiceCollectionExtensions.cs                               ← AddSunfishFederation root
      FederationStartupChecks.cs                                   ← D-PRIVATE-NETWORK fail-fast
    tests/
      Sunfish.Federation.Common.Tests/
        SyncEnvelopeTests.cs
        InMemorySyncTransportTests.cs
        FederationStartupChecksTests.cs
        ServiceCollectionExtensionsTests.cs

  federation-entity-sync/                                          ← new package
    Sunfish.Federation.EntitySync.csproj
    IEntitySyncer.cs                                               ← PullFromAsync / PushToAsync
    EntitySyncer.cs                                                ← default impl
    Protocol/
      HeadsAnnouncement.cs                                         ← "I have heads X, Y, Z"
      ChangesResponse.cs                                           ← "here are the missing changes"
      SyncSession.cs                                               ← session state machine
    Http/
      EntitySyncEndpoint.cs                                        ← ASP.NET Core MapPost handler
      EntitySyncClient.cs                                          ← HttpClient-based caller
      EntitySyncServiceCollectionExtensions.cs
    InMemory/
      InMemoryEntitySyncer.cs                                      ← two-peer test harness
    Verification/
      ChangeSignatureVerifier.cs                                   ← reuses Phase B crypto
      AuditChainVerifier.cs                                        ← D-AUDIT-TRAIL-FEDERATION
    tests/
      Sunfish.Federation.EntitySync.Tests/
        InMemoryTwoPeerConvergenceTests.cs
        HttpEntitySyncEndpointTests.cs
        AuditChainVerifierTests.cs
        ConcurrentEditMergeTests.cs
        SignatureRejectionTests.cs

  federation-capability-sync/                                      ← new package
    Sunfish.Federation.CapabilitySync.csproj
    ICapabilitySyncer.cs                                           ← ReconcileAsync
    CapabilitySyncer.cs                                            ← default impl
    Riblt/
      RibltEncoder.cs                                              ← Keyhive-reference-inspired
      RibltDecoder.cs
      RibltSymbol.cs                                               ← coded symbol type
      RibltFallback.cs                                             ← full-set exchange fallback
    Protocol/
      CapabilityDigest.cs                                          ← summary for initial exchange
      CapabilityDiff.cs                                            ← ops-to-apply payload
    Http/
      CapabilitySyncEndpoint.cs
      CapabilitySyncClient.cs
    Verification/
      MembershipOpVerifier.cs                                      ← Ed25519 verify on every op
      RevocationApplier.cs                                         ← group semantics on removal
    tests/
      Sunfish.Federation.CapabilitySync.Tests/
        RibltEncoderRoundTripTests.cs
        RibltDecoderFailureFallbackTests.cs
        TwoPeerConvergenceTests.cs
        RevocationConvergenceTests.cs
        SignatureRejectionTests.cs

  federation-blob-replication/                                     ← new package
    Sunfish.Federation.BlobReplication.csproj
    IpfsBlobStore.cs                                               ← IBlobStore backend, delegates to Kubo
    Kubo/
      KuboHttpClient.cs                                            ← thin RPC client
      KuboEndpoints.cs                                             ← route constants
      KuboResponses.cs                                             ← deserialized payload types
      KuboHealthCheck.cs                                           ← /api/v0/id readiness probe
    Cluster/
      IpfsClusterClient.cs                                         ← Cluster's /pins API
      ClusterPinningPolicy.cs                                      ← D-PINNING-POLICY
      IBlobPinningPolicy.cs
      DefaultBlobPinningPolicy.cs
    Attestation/
      BlobAttestationProducer.cs                                   ← D-ATTESTATION 24-hour job
      BlobAttestationVerifier.cs
      SilentContentLossDetector.cs                                 ← 72-hour alert
    Configuration/
      IpfsConfiguration.cs
      SwarmKeyLoader.cs                                            ← private-network config
      PrivateNetworkStartupCheck.cs                                ← D-PRIVATE-NETWORK fail-fast
    tests/
      Sunfish.Federation.BlobReplication.Tests/
        IpfsBlobStoreTests.cs                                      ← uses Testcontainers Kubo
        KuboHttpClientTests.cs                                     ← mocked HTTP
        ClusterPinningPolicyTests.cs
        BlobAttestationProducerTests.cs
        SilentContentLossDetectorTests.cs
        SwarmKeyLoaderTests.cs
        PrivateNetworkStartupCheckTests.cs

  federation-patterns/                                             ← new package — end-to-end tests
    Sunfish.Federation.Patterns.Tests/
      PatternA_PmAndCityCodeEnforcement/
        ThreeNodeFixture.cs                                        ← ACME + BigCo + City
        InspectionFederationTests.cs
        CapabilityDelegationTests.cs
        BlobReplicationTimingTests.cs
        AttestationFlowTests.cs
      PatternB_BaseCommandAirGapped/
        SignedBundleExporter.cs                                    ← offline transport
        SignedBundleImporter.cs
        AirGappedFederationTests.cs
      PatternC_ContractorPortal/
        MacaroonFederationTests.cs
        ContractorReadOnlyEndpointTests.cs

docs/
  federation/
    operator-guide.md                                              ← Task 10 — new
    private-network-setup.md
    swarm-key-generation.md
    ipfs-cluster-config.md
    kubo-version-pinning.md
    attestation-schedule.md
    troubleshooting.md

scripts/
  generate-swarm-key.sh                                            ← 32-byte secret generator
  bootstrap-federation-test-cluster.sh                             ← dev-time 3-node Kubo + Cluster

Directory.Packages.props                                           ← updated with Testcontainers, etc.
```

---

## Package dependency graph

```
                         Sunfish.Foundation  (Phase A + B + B-blobs)
                         │
                         │  depended on by every federation package
                         ▼
                  Sunfish.Federation.Common
                         │
         ┌───────────────┼───────────────────┐
         ▼               ▼                   ▼
 Sunfish.Federation   Sunfish.Federation   Sunfish.Federation
    .EntitySync       .CapabilitySync       .BlobReplication
         │               │                   │
         └───────────────┼───────────────────┘
                         │
                         ▼
             Sunfish.Federation.Patterns.Tests
             (integration test project only; not packable)
```

Every federation package is packable (produces a NuGet). `Patterns.Tests` is a test project, not a shipped library.

No federation package references any `ui-core`, `ui-adapters-*`, `blocks-*`, or `compat-*` package. The Phase 2 `HasNoBlazorDependency` invariant is checked per-csproj in Task D-0.

---

## Task D-0: Prerequisites, branch, package references

- [ ] **Step 1: Verify prerequisite phases landed**

```bash
cd C:/Projects/Sunfish

# Verify Foundation builds
dotnet build packages/foundation/Sunfish.Foundation.csproj

# Spot-check symbols exist (Phase A + B + B-blobs)
grep -rE 'class Entity\b|record Entity\b' packages/foundation/ --include='*.cs'
grep -rE 'class Version\b|record Version\b' packages/foundation/ --include='*.cs'
grep -rE 'class AuditRecord\b|record AuditRecord\b' packages/foundation/ --include='*.cs'
grep -rE 'Ed25519KeyPair|SignedOperation' packages/foundation/ --include='*.cs'
grep -rE 'class Principal\b|record Principal\b' packages/foundation/ --include='*.cs'
grep -rE 'class MembershipOp\b|record MembershipOp\b' packages/foundation/ --include='*.cs'
grep -rE 'interface IBlobStore\b' packages/foundation/ --include='*.cs'
grep -rE 'class Cid\b|record Cid\b' packages/foundation/ --include='*.cs'
```

All eight symbol lookups must return at least one hit. If any returns zero, the corresponding prerequisite phase has not shipped — halt and escalate.

- [ ] **Step 2: Create the feature branch**

```bash
git checkout -b feat/platform-phase-D-federation
```

- [ ] **Step 3: Add package references to `Directory.Packages.props`**

```xml
<!-- Federation transport + testing -->
<PackageVersion Include="System.Net.Http.Json" Version="10.0.6" />
<PackageVersion Include="Microsoft.Extensions.Http" Version="10.0.6" />
<PackageVersion Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.6" />
<PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.6" />
<PackageVersion Include="Microsoft.Extensions.Options" Version="10.0.6" />

<!-- IPFS integration test infrastructure -->
<PackageVersion Include="Testcontainers" Version="4.1.0" />
<PackageVersion Include="Testcontainers.Xunit" Version="4.1.0" />

<!-- ASP.NET Core MapPost handler used by federation endpoints -->
<PackageVersion Include="Microsoft.AspNetCore.App" Version="10.0.6" />
```

- [ ] **Step 4: Confirm no other NuGet is needed for this phase**

The Kubo daemon runs as a sidecar (D-KUBO-CLIENT), not via a .NET library. `NSec.Cryptography` is already referenced by Phase B's crypto package; Phase D transitively picks it up via `Sunfish.Foundation`. No additional NuGet for RIBLT — we implement it ourselves (D-RIBLT-COMPLEXITY).

- [ ] **Step 5: Document the Kubo sidecar dependency**

Create `docs/federation/kubo-sidecar-dependency.md` (referenced by operator guide Task 10) noting:

- Kubo is a Go binary, not a .NET library.
- Production Sunfish federation nodes run Kubo as a separate container or process.
- Version pinning: `ipfs/kubo:v0.28.0` is the Phase D tested version.
- Kubo RPC is exposed on `localhost:5001` by default; `IpfsBlobStore` config defaults match.

- [ ] **Step 6: Commit prerequisites**

```bash
git add Directory.Packages.props docs/federation/kubo-sidecar-dependency.md
git commit -m "chore(federation): add Testcontainers + System.Net.Http.Json; document Kubo sidecar (D-KUBO-CLIENT)"
```

---

## Task D-1: `Sunfish.Federation.Common` — transport abstraction + signed envelope

**Goal:** Ship the shared primitives every other federation package consumes: a signed sync-message envelope, an `ISyncTransport` abstraction with HTTP and in-memory implementations, a peer registry, and the root `AddSunfishFederation` service-collection extension with private-network fail-fast.

- [ ] **Step 1: Create project scaffold**

```bash
mkdir -p packages/federation-common/Extensions
mkdir -p packages/federation-common/tests/Sunfish.Federation.Common.Tests
```

- [ ] **Step 2: `Sunfish.Federation.Common.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <HasNoBlazorDependency>true</HasNoBlazorDependency>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Microsoft.Extensions.Http" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Options" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: `SyncEnvelope.cs` — the wire-format primitive**

```csharp
namespace Sunfish.Federation.Common;

/// <summary>
/// Wire-format envelope for a single federation sync message. Every sync interaction between
/// two peers uses this envelope. The envelope itself is signed by the sender's Ed25519 key;
/// the payload inside is an already-signed operation from Phase B (SignedOperation&lt;T&gt;).
///
/// Double-signing is intentional: the payload signature proves the operation is authentic
/// (peer B cannot forge a change claiming peer A authored it), and the envelope signature
/// proves the *transport hop* is authentic (peer C in the middle cannot replay a message
/// claiming it came from peer A).
/// </summary>
public sealed record SyncEnvelope(
    SyncMessageId Id,
    PeerId FromPeer,
    PeerId ToPeer,
    SyncMessageKind Kind,
    Instant SentAt,
    Nonce Nonce,
    ReadOnlyMemory<byte> Payload,
    Ed25519Signature Signature);

public enum SyncMessageKind
{
    EntityHeadsAnnouncement,
    EntityChangesRequest,
    EntityChangesResponse,
    CapabilityDigest,
    CapabilityRibltEncoded,
    CapabilityFullSet,
    BlobPinRequest,
    BlobAttestationBroadcast,
    HealthProbe
}
```

- [ ] **Step 4: `ISyncTransport.cs`**

```csharp
namespace Sunfish.Federation.Common;

public interface ISyncTransport
{
    /// <summary>Send one envelope; receive one envelope in response. Idempotent at the Nonce level.</summary>
    ValueTask<SyncEnvelope> SendAsync(PeerDescriptor target, SyncEnvelope envelope, CancellationToken ct);

    /// <summary>Register a handler for incoming envelopes. Invoked by the transport when a peer connects.</summary>
    IDisposable RegisterHandler(Func<SyncEnvelope, ValueTask<SyncEnvelope>> handler);
}
```

- [ ] **Step 5: `InMemorySyncTransport.cs`**

Used in tests only. A static `Dictionary<PeerId, Func<SyncEnvelope, ValueTask<SyncEnvelope>>>` routes envelopes by `ToPeer`. `SendAsync` invokes the target peer's handler directly. No network, no serialization — verifies protocol correctness without transport complications.

- [ ] **Step 6: `HttpSyncTransport.cs`**

Production transport. Uses `HttpClient` with `System.Net.Http.Json` and `Ed25519` signing of the envelope before serialization. Endpoint URL pattern: `https://{peer-host}/.well-known/sunfish/federation/sync`. mTLS per D-PEER-AUTH.

- [ ] **Step 7: `FederationStartupChecks.cs`**

```csharp
namespace Sunfish.Federation.Common.Extensions;

public sealed class FederationStartupChecks : IHostedService
{
    private readonly IOptions<FederationOptions> _options;
    private readonly IKuboHealthProbe _kuboHealth;   // from BlobReplication, optional
    private readonly ILogger<FederationStartupChecks> _logger;

    public async Task StartAsync(CancellationToken ct)
    {
        var opts = _options.Value;

        // D-PRIVATE-NETWORK fail-fast
        if (opts.Environment == FederationEnvironment.Production)
        {
            if (string.IsNullOrEmpty(opts.SwarmKeyPath))
            {
                _logger.LogCritical(
                    "FATAL: Production federation requires a swarm key. Set Sunfish:Federation:SwarmKeyPath " +
                    "to a file containing a 32-byte hex-encoded IPFS swarm key (see docs/federation/swarm-key-generation.md).");
                throw new InvalidOperationException("Swarm key required in production federation environment.");
            }

            // Check Kubo reports private-network profile
            var cfg = await _kuboHealth.GetConfigAsync(ct);
            if (cfg.NetworkProfile != "private")
            {
                _logger.LogCritical(
                    "FATAL: Kubo daemon at {Address} reports NetworkProfile={Profile}; expected 'private'. " +
                    "Refusing to start — see docs/federation/private-network-setup.md.",
                    opts.KuboRpcAddress, cfg.NetworkProfile);
                throw new InvalidOperationException("Kubo daemon is not running in private-network mode.");
            }
        }
        else
        {
            _logger.LogInformation(
                "Federation environment is {Env}; skipping private-network enforcement. " +
                "This is acceptable for development/test only.", opts.Environment);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

- [ ] **Step 8: `ServiceCollectionExtensions.cs`**

```csharp
public static IServiceCollection AddSunfishFederation(
    this IServiceCollection services,
    Action<FederationOptions> configure)
{
    services.Configure(configure);
    services.AddHostedService<FederationStartupChecks>();
    services.AddHttpClient<HttpSyncTransport>();
    services.AddSingleton<ISyncTransport, HttpSyncTransport>();
    services.AddSingleton<IPeerRegistry, FilePeerRegistry>();
    return services;
}
```

- [ ] **Step 9: Unit tests**

- `SyncEnvelopeTests`: round-trip serialization, signature verification happy path, tampered-payload rejection.
- `InMemorySyncTransportTests`: two peers exchange an envelope; handler receives intact.
- `FederationStartupChecksTests`: (a) throws in Production with no swarm key, (b) throws in Production when Kubo reports non-private profile, (c) passes in Development with no swarm key.
- `ServiceCollectionExtensionsTests`: `AddSunfishFederation` wires hosted service and transport.

- [ ] **Step 10: Build + test**

```bash
dotnet build packages/federation-common/Sunfish.Federation.Common.csproj
dotnet test packages/federation-common/tests/Sunfish.Federation.Common.Tests/
```

Both green. Commit:

```bash
git add packages/federation-common/
git commit -m "feat(federation): add Sunfish.Federation.Common — envelope, transport, startup checks (D-SYNC-TRANSPORT, D-PRIVATE-NETWORK)"
```

---

## Task D-2: `Sunfish.Federation.EntitySync.InMemory` — two-peer delta-sync

**Goal:** Before any HTTP transport, prove the Automerge-style sync protocol shape works with in-memory peers. Two peers with divergent histories exchange heads, request missing changes, merge deterministically, end up with identical state.

- [ ] **Step 1: Create project scaffold**

```bash
mkdir -p packages/federation-entity-sync/{Protocol,Http,InMemory,Verification}
mkdir -p packages/federation-entity-sync/tests/Sunfish.Federation.EntitySync.Tests
```

- [ ] **Step 2: `Sunfish.Federation.EntitySync.csproj`**

References `Sunfish.Foundation` (for `Version`, `AuditRecord`, `SignedOperation<T>`) and `Sunfish.Federation.Common` (for `ISyncTransport`, `SyncEnvelope`).

- [ ] **Step 3: `IEntitySyncer.cs`**

```csharp
namespace Sunfish.Federation.EntitySync;

public interface IEntitySyncer
{
    /// <summary>
    /// Pull any entity changes (and their audit records) that <paramref name="peer"/> has but the
    /// local node does not. Applies them locally, verifying signatures and audit chain linkage.
    /// Returns a summary of what was transferred.
    /// </summary>
    ValueTask<SyncResult> PullFromAsync(PeerDescriptor peer, CancellationToken ct);

    /// <summary>
    /// Push local entity changes (and their audit records) that <paramref name="peer"/> does not
    /// have. The peer verifies signatures and chain linkage before applying. Returns what was
    /// accepted, rejected, or already-present.
    /// </summary>
    ValueTask<SyncResult> PushToAsync(PeerDescriptor peer, CancellationToken ct);
}

public sealed record SyncResult(
    int ChangesTransferred,
    int ChangesAlreadyPresent,
    int ChangesRejected,
    IReadOnlyList<SyncRejection> Rejections);

public sealed record SyncRejection(VersionId VersionId, string Reason);
```

- [ ] **Step 4: `Protocol/HeadsAnnouncement.cs` and `ChangesResponse.cs`**

Per `automerge-evaluation.md` §4.3, the protocol is three-phase:

1. **Heads announcement**: "I have heads X, Y, Z" — a set of version IDs that are the tips of my local history.
2. **Changes request**: "I have heads {A, B, C}; give me every change I don't have that is reachable from your heads."
3. **Changes response**: the requested changes + their transitive audit records.

```csharp
public sealed record HeadsAnnouncement(
    EntityId Scope,                    // null = all entities; otherwise scope to one entity
    IReadOnlyList<VersionId> LocalHeads);

public sealed record ChangesRequest(
    EntityId Scope,
    IReadOnlyList<VersionId> LocalHeads,
    IReadOnlyList<VersionId> WantedHeads);

public sealed record ChangesResponse(
    IReadOnlyList<SignedChangeRecord> Changes,
    IReadOnlyList<AuditRecord> AuditRecords);

public sealed record SignedChangeRecord(
    VersionId VersionId,
    VersionId? ParentId,
    JsonPatch Diff,
    ActorId Author,
    Instant Timestamp,
    Ed25519Signature Signature);
```

- [ ] **Step 5: `InMemory/InMemoryEntitySyncer.cs`**

Default implementation that uses `ISyncTransport` (specifically `InMemorySyncTransport` in tests). Protocol state machine:

1. `PullFromAsync(peer)`: send `HeadsAnnouncement` with local heads → receive peer's `HeadsAnnouncement` → compute diff (version IDs peer has that we don't, walking backward from their heads) → send `ChangesRequest` with what we want → receive `ChangesResponse` → verify every signature → apply changes to local entity store.

2. `PushToAsync(peer)`: mirror — ask peer for their heads, compute what we have that they don't, send them a `ChangesResponse` directly (one-shot push).

- [ ] **Step 6: `Verification/ChangeSignatureVerifier.cs`**

```csharp
public sealed class ChangeSignatureVerifier
{
    private readonly IPrincipalResolver _principals;  // Phase B

    public bool Verify(SignedChangeRecord change)
    {
        // Reject if author principal unknown locally
        if (!_principals.TryResolve(change.Author, out var principal))
            return false;

        // Recompute the bytes-to-sign (canonical serialization of the change, minus signature)
        var bytesToSign = CanonicalSerialize(change with { Signature = default });

        // Ed25519 verify
        return NSec.Cryptography.SignatureAlgorithm.Ed25519.Verify(
            principal.PublicKey, bytesToSign, change.Signature.Bytes.Span);
    }
}
```

- [ ] **Step 7: `Verification/AuditChainVerifier.cs` (D-AUDIT-TRAIL-FEDERATION)**

For every received audit record, verify:

1. `record.Prev` points to an audit record we already have locally (either from a previous sync or from our own history).
2. `record.Signature` verifies against the actor's public key.
3. The computed hash of the record matches what the *next* record's `Prev` field points to (when the chain continues).

If any link is broken, the entire received batch is rejected and logged as a chain-verification failure.

- [ ] **Step 8: Integration test — `InMemoryTwoPeerConvergenceTests.cs`**

```csharp
[Fact]
public async Task two_peers_with_divergent_histories_converge_after_bidirectional_sync()
{
    // Arrange: two in-memory Sunfish kernels, each with its own entity store + crypto keys
    var alice = await TestKernel.CreateAsync("alice");
    var bob   = await TestKernel.CreateAsync("bob");
    alice.KnowsAbout(bob);  // exchange public keys
    bob.KnowsAbout(alice);

    var entityId = await alice.Entities.CreateAsync(new LeaseDraft("unit-42", tenant: "jim"));
    // Both peers see the same initial state via a setup-phase sync
    await alice.Federation.PushToAsync(bob.PeerDescriptor, CancellationToken.None);

    // Divergent mutations
    await alice.Entities.UpdateAsync(entityId, e => e with { RentAmount = 1200 });
    await bob.Entities.UpdateAsync(entityId, e => e with { LeaseTerm = TimeSpan.FromDays(365) });

    // Act: bidirectional sync
    await alice.Federation.PullFromAsync(bob.PeerDescriptor, CancellationToken.None);
    await bob.Federation.PullFromAsync(alice.PeerDescriptor, CancellationToken.None);

    // Assert: both peers see the merged state
    var aliceView = await alice.Entities.GetAsync<LeaseDraft>(entityId);
    var bobView   = await bob.Entities.GetAsync<LeaseDraft>(entityId);

    Assert.Equal(1200, aliceView.RentAmount);
    Assert.Equal(TimeSpan.FromDays(365), aliceView.LeaseTerm);
    Assert.Equal(aliceView, bobView);
}
```

- [ ] **Step 9: Integration test — `SignatureRejectionTests.cs`**

Construct a `SignedChangeRecord` with a valid-looking Ed25519 signature but flipped bytes in the diff. Sync to peer. Expect `SyncResult.Rejections` contains the change; expect the local entity store is unchanged.

- [ ] **Step 10: Integration test — `ConcurrentEditMergeTests.cs`**

Three peers, each modifying a different field of the same entity concurrently. After pairwise sync in all directions, all three converge to the same state (CRDT merge is commutative + associative). This reuses Phase A's merge operator; Phase D verifies federation preserves the semantics.

- [ ] **Step 11: Build + test**

```bash
dotnet build packages/federation-entity-sync/Sunfish.Federation.EntitySync.csproj
dotnet test packages/federation-entity-sync/tests/
```

Green. Commit:

```bash
git add packages/federation-entity-sync/
git commit -m "feat(federation): add Sunfish.Federation.EntitySync with in-memory two-peer convergence (D-CRDT-LIB, D-AUDIT-TRAIL-FEDERATION)"
```

---

## Task D-3: `Sunfish.Federation.EntitySync.Http` — HTTP+JSON transport

**Goal:** Wire the sync protocol from Task D-2 to a real HTTP endpoint so two processes on different machines can federate. This task adds the server-side endpoint handler, the client-side caller, and integration tests using `WebApplicationFactory`.

- [ ] **Step 1: `Http/EntitySyncEndpoint.cs`**

```csharp
public static class EntitySyncEndpoint
{
    public static IEndpointRouteBuilder MapEntitySyncEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/.well-known/sunfish/federation/entity-sync",
            async (HttpContext ctx, IEntitySyncer syncer, CancellationToken ct) =>
            {
                var envelope = await ctx.Request.ReadFromJsonAsync<SyncEnvelope>(ct)
                    ?? throw new BadHttpRequestException("Empty envelope");

                // Validate the envelope signature against the peer's known pubkey
                await ValidateEnvelopeAsync(envelope, ctx, ct);

                // Dispatch on SyncMessageKind
                var response = envelope.Kind switch
                {
                    SyncMessageKind.EntityHeadsAnnouncement => await syncer.HandleHeadsAsync(envelope, ct),
                    SyncMessageKind.EntityChangesRequest    => await syncer.HandleChangesRequestAsync(envelope, ct),
                    SyncMessageKind.EntityChangesResponse   => await syncer.HandleChangesResponseAsync(envelope, ct),
                    _ => throw new BadHttpRequestException($"Unsupported kind: {envelope.Kind}")
                };

                return Results.Json(response);
            })
            .RequireHost()
            .Produces<SyncEnvelope>(200);

        return endpoints;
    }
}
```

- [ ] **Step 2: `Http/EntitySyncClient.cs`**

Uses `HttpClient` + `SendAsync` flowing through `HttpSyncTransport` from Task D-1.

- [ ] **Step 3: `Http/EntitySyncServiceCollectionExtensions.cs`**

```csharp
public static IServiceCollection AddEntitySync(this IServiceCollection services)
{
    services.AddSingleton<IEntitySyncer, EntitySyncer>();
    services.AddSingleton<ChangeSignatureVerifier>();
    services.AddSingleton<AuditChainVerifier>();
    return services;
}
```

- [ ] **Step 4: Integration test — `HttpEntitySyncEndpointTests.cs`**

Uses `WebApplicationFactory<TProgram>` to host two in-process ASP.NET Core apps on different ports, each wired with its own `TestKernel`. Tests the full HTTP round-trip: signature on envelope, serialization through `JsonSerializer`, deserialization at peer, handler execution, response.

```csharp
[Fact]
public async Task peer_sync_over_http_transfers_changes_with_valid_signatures()
{
    await using var factoryA = new FederationWebApplicationFactory("alice");
    await using var factoryB = new FederationWebApplicationFactory("bob");

    using var aliceClient = factoryA.CreateClient();
    using var bobClient   = factoryB.CreateClient();

    await factoryA.SeedEntityAsync("unit-42", new { RentAmount = 1200 });

    // Trigger pull from bob's side — bob's EntitySyncer uses an HttpClient pointed at factoryA
    var result = await factoryB.Services.GetRequiredService<IEntitySyncer>()
        .PullFromAsync(factoryA.PeerDescriptor, CancellationToken.None);

    Assert.Equal(1, result.ChangesTransferred);
    Assert.Empty(result.Rejections);
}
```

- [ ] **Step 5: Integration test — HTTP 400 on malformed envelopes**

A hand-constructed request missing the signature field returns HTTP 400. Logs capture the verifier rejection reason.

- [ ] **Step 6: Integration test — HTTP 401 on unknown peer**

An envelope signed by a peer not in the target's `IPeerRegistry` returns HTTP 401. Demonstrates D-PEER-AUTH isolation.

- [ ] **Step 7: Build + test; commit**

```bash
dotnet build packages/federation-entity-sync/Sunfish.Federation.EntitySync.csproj
dotnet test packages/federation-entity-sync/tests/
git add packages/federation-entity-sync/
git commit -m "feat(federation): add HTTP+JSON transport for EntitySync (D-SYNC-TRANSPORT)"
```

---

## Task D-4: `Sunfish.Federation.CapabilitySync` — RIBLT reconciliation

**Goal:** Keyhive-inspired sync of the signed capability graph. Per `automerge-evaluation.md` §1.3, the primitive is RIBLT (Rateless Invertible Bloom Lookup Tables) for efficient set reconciliation. Phase D's first-pass RIBLT is usable-and-tested; falls back to full-set exchange when decode fails (D-RIBLT-COMPLEXITY).

- [ ] **Step 1: Create project scaffold**

```bash
mkdir -p packages/federation-capability-sync/{Riblt,Protocol,Http,Verification}
mkdir -p packages/federation-capability-sync/tests/Sunfish.Federation.CapabilitySync.Tests
```

- [ ] **Step 2: `ICapabilitySyncer.cs`**

```csharp
public interface ICapabilitySyncer
{
    /// <summary>
    /// Reconcile the local capability graph with <paramref name="peer"/>. Both peers converge
    /// on the same set of signed membership ops. Revocations apply immediately; sync is CRDT.
    /// </summary>
    ValueTask<CapabilityReconcileResult> ReconcileAsync(PeerDescriptor peer, CancellationToken ct);
}

public sealed record CapabilityReconcileResult(
    int OpsTransferred,
    int OpsAlreadyPresent,
    int OpsRejected,
    bool UsedRibltFastPath,
    bool UsedFullSetFallback);
```

- [ ] **Step 3: `Riblt/RibltEncoder.cs` + `RibltDecoder.cs`**

First-pass implementation following the Keyhive notebook structure. A rough sketch of the data flow:

```csharp
/// <summary>
/// Rateless Invertible Bloom Lookup Tables encoder. Reference:
/// Keyhive notebook 05 (https://www.inkandswitch.com/keyhive/notebook/05/).
///
/// RIBLT encodes a set as a stream of "coded symbols"; the decoder can reconstruct
/// set differences between two peers by peeling coded symbols against local hashes.
/// The "rateless" property means the encoder keeps producing symbols until the peer
/// signals "I have enough to decode"; this adapts to the actual diff size without
/// prior negotiation.
/// </summary>
public sealed class RibltEncoder<TItem>
    where TItem : notnull
{
    private readonly Func<TItem, UInt128> _hash;
    private readonly ISet<TItem> _items;

    public RibltEncoder(IEnumerable<TItem> items, Func<TItem, UInt128> hash) { ... }

    /// <summary>Produce the next coded symbol in the rateless stream.</summary>
    public CodedSymbol NextSymbol() { ... }
}

public sealed class RibltDecoder<TItem>
{
    public DecodeResult<TItem> TryDecode(ReadOnlySpan<CodedSymbol> remoteSymbols, ISet<TItem> localItems)
    {
        // Peel symbols iteratively; if progress stalls with symbols remaining, return FailureNeedMoreSymbols
        // or FailureInconsistent.
    }
}

public enum DecodeOutcome
{
    Success,
    FailureNeedMoreSymbols,
    FailureInconsistent
}
```

- [ ] **Step 4: `Riblt/RibltFallback.cs` (D-RIBLT-COMPLEXITY)**

When `RibltDecoder.TryDecode` returns `FailureInconsistent` or after N rounds of `FailureNeedMoreSymbols`, fall back to **full-set exchange**: each peer sends the complete list of `SignedOperation<MembershipOp>.Id` values they have; the other peer responds with ops by ID for the intersection-complement. Slower, bigger payload, but correct.

The threshold for fallback (default: 3 RIBLT rounds or 10kb of coded symbols, whichever first) is configurable. Tests exercise both the happy path (RIBLT decodes first-try) and the fallback (seeded inconsistency triggers full-set mode).

- [ ] **Step 5: `Protocol/CapabilityDigest.cs` and `CapabilityDiff.cs`**

```csharp
public sealed record CapabilityDigest(
    int OpCount,
    byte[] InitialRibltSymbols);   // small batch to probe for diff size

public sealed record CapabilityDiff(
    IReadOnlyList<SignedOperation<MembershipOp>> OpsToAdd);
```

- [ ] **Step 6: `Verification/MembershipOpVerifier.cs`**

Every incoming `SignedOperation<MembershipOp>` is Ed25519-verified against the principal identified by its `Author` field. Reuses Phase B's verification primitives.

- [ ] **Step 7: `Verification/RevocationApplier.cs`**

When a `MembershipOp.Kind == Revoke` arrives, apply it to the local group state: remove the target principal from the group's member list. If the revocation timestamp is earlier than a local Add op for the same target, the group membership CRDT resolves per Phase B semantics (typically, the later-timestamped op wins with deterministic tiebreaker). Test vectors cover Add-then-Revoke, Revoke-then-Add-later, and Add-Revoke-Add interleavings.

- [ ] **Step 8: `Http/CapabilitySyncEndpoint.cs` and `CapabilitySyncClient.cs`**

Mirror the EntitySync endpoints: `/.well-known/sunfish/federation/capability-sync`. Same envelope format, same mTLS, dispatch on `SyncMessageKind.CapabilityDigest | CapabilityRibltEncoded | CapabilityFullSet`.

- [ ] **Step 9: Integration tests**

`RibltEncoderRoundTripTests`: known set differences produce known coded-symbol streams; decode returns original diff. Uses test vectors that should match a reference JavaScript Keyhive implementation (borrow vectors into the test suite under `tests/fixtures/riblt-vectors.json`).

`RibltDecoderFailureFallbackTests`: seeded inconsistency → decoder returns `FailureInconsistent` → syncer invokes full-set fallback → both peers converge.

`TwoPeerConvergenceTests`: two peers each with different membership ops for the same group; `ReconcileAsync` converges them to the same set; final group view is identical.

`RevocationConvergenceTests`: peer A has (Add bob), peer B has (Add bob, Revoke bob). After sync, both peers report bob as not a member.

`SignatureRejectionTests`: malicious peer sends a MembershipOp with a valid envelope signature but an invalid op-internal signature. Op is rejected, `OpsRejected` count reflects it, local graph unchanged.

- [ ] **Step 10: Build + test; commit**

```bash
dotnet build packages/federation-capability-sync/Sunfish.Federation.CapabilitySync.csproj
dotnet test packages/federation-capability-sync/tests/
git add packages/federation-capability-sync/
git commit -m "feat(federation): add CapabilitySync with RIBLT reconciliation + revocation semantics (D-RIBLT-COMPLEXITY)"
```

---

## Task D-5: `IpfsBlobStore` — Kubo HTTP RPC integration

**Goal:** Ship `IpfsBlobStore : IBlobStore` that delegates to a Kubo sidecar via its HTTP RPC API. Integration-tested via Testcontainers spinning up an actual Kubo daemon.

- [ ] **Step 1: Create project scaffold**

```bash
mkdir -p packages/federation-blob-replication/{Kubo,Cluster,Attestation,Configuration}
mkdir -p packages/federation-blob-replication/tests/Sunfish.Federation.BlobReplication.Tests
```

- [ ] **Step 2: `Sunfish.Federation.BlobReplication.csproj`**

References `Sunfish.Foundation` (for `IBlobStore`, `Cid`), `Sunfish.Federation.Common`, and for tests, `Testcontainers` + `Testcontainers.Xunit`.

- [ ] **Step 3: `Kubo/KuboHttpClient.cs` (D-KUBO-CLIENT)**

Thin HTTP client over the Kubo RPC. Methods map one-to-one to Kubo endpoints:

```csharp
public interface IKuboHttpClient
{
    ValueTask<KuboIdResponse> GetIdAsync(CancellationToken ct);
    ValueTask<KuboConfigResponse> GetConfigAsync(CancellationToken ct);
    ValueTask<KuboAddResponse> AddAsync(ReadOnlyMemory<byte> content, CancellationToken ct);
    ValueTask<ReadOnlyMemory<byte>> CatAsync(Cid cid, CancellationToken ct);
    ValueTask<KuboPinResponse> PinAddAsync(Cid cid, CancellationToken ct);
    ValueTask<KuboPinResponse> PinRmAsync(Cid cid, CancellationToken ct);
    ValueTask<KuboPinListResponse> PinListAsync(CancellationToken ct);
    ValueTask<KuboSwarmPeersResponse> SwarmPeersAsync(CancellationToken ct);
}

public sealed class KuboHttpClient : IKuboHttpClient
{
    private readonly HttpClient _http;
    private readonly ILogger<KuboHttpClient> _logger;

    public async ValueTask<KuboAddResponse> AddAsync(ReadOnlyMemory<byte> content, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        using var bytesContent = new ReadOnlyMemoryContent(content);
        form.Add(bytesContent, "file", "blob");

        using var response = await _http.PostAsync("/api/v0/add?cid-version=1&pin=true", form, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<KuboAddResponse>(cancellationToken: ct)
            ?? throw new HttpRequestException("Empty /add response");
    }

    // ...cat, pin/add, pin/rm, pin/ls, swarm/peers all follow the same pattern
}
```

- [ ] **Step 4: `IpfsBlobStore.cs`**

```csharp
public sealed class IpfsBlobStore : IBlobStore
{
    private readonly IKuboHttpClient _kubo;
    private readonly IBlobPinningPolicy _pinning;
    private readonly ILogger<IpfsBlobStore> _logger;

    public async ValueTask<Cid> PutAsync(ReadOnlyMemory<byte> content, CancellationToken ct)
    {
        var response = await _kubo.AddAsync(content, ct);
        var cid = Cid.Parse(response.Hash);

        if (_pinning.ShouldPinOnPut(cid))
            await _kubo.PinAddAsync(cid, ct);

        return cid;
    }

    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(Cid cid, CancellationToken ct)
    {
        try
        {
            return await _kubo.CatAsync(cid, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async ValueTask<bool> ExistsLocallyAsync(Cid cid, CancellationToken ct)
    {
        var pins = await _kubo.PinListAsync(ct);
        return pins.Keys.ContainsKey(cid.ToString());
    }

    public ValueTask PinAsync(Cid cid, CancellationToken ct) => _kubo.PinAddAsync(cid, ct).AsValueTask();
    public ValueTask UnpinAsync(Cid cid, CancellationToken ct) => _kubo.PinRmAsync(cid, ct).AsValueTask();

    public ValueTask<BlobAttestation> AttestAsync(Cid cid, CancellationToken ct)
    {
        // Delegates to BlobAttestationProducer in Task D-6 Step 3
    }
}
```

- [ ] **Step 5: `Configuration/SwarmKeyLoader.cs` and `PrivateNetworkStartupCheck.cs`**

`SwarmKeyLoader`: reads the swarm key from the configured file path, validates format (Kubo's `/key/swarm/psk/1.0.0/` prefix + hex-encoded 32 bytes).

`PrivateNetworkStartupCheck` (invoked from `FederationStartupChecks` in Task D-1 Step 7): calls `GetConfigAsync` on the Kubo daemon and verifies the `Swarm.SwarmKey` field is non-empty.

- [ ] **Step 6: Unit tests — `KuboHttpClientTests.cs`**

Mock `HttpMessageHandler`. Verify request URLs, HTTP methods, multipart form construction for `/add`, query-string params for `/cid-version=1&pin=true`.

- [ ] **Step 7: Integration test — `IpfsBlobStoreTests.cs` using Testcontainers**

```csharp
public sealed class KuboSingleNodeFixture : IAsyncLifetime
{
    public IContainer Kubo { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        Kubo = new ContainerBuilder()
            .WithImage("ipfs/kubo:v0.28.0")
            .WithPortBinding(5001, assignRandomHostPort: true)
            .WithEnvironment("IPFS_PROFILE", "test")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(req => req.ForPath("/api/v0/id").ForPort(5001)))
            .Build();

        await Kubo.StartAsync();
    }

    public ValueTask DisposeAsync() => Kubo.DisposeAsync();

    public Uri RpcAddress => new($"http://localhost:{Kubo.GetMappedPublicPort(5001)}");
}

[Collection("Kubo")]
public sealed class IpfsBlobStoreTests(KuboSingleNodeFixture fx)
{
    [Fact]
    public async Task put_get_roundtrip_produces_stable_cid()
    {
        var store = new IpfsBlobStore(new KuboHttpClient(fx.RpcAddress), ...);
        var bytes = "hello, sunfish"u8.ToArray();

        var cid1 = await store.PutAsync(bytes, default);
        var cid2 = await store.PutAsync(bytes, default);  // idempotent
        Assert.Equal(cid1, cid2);

        var retrieved = await store.GetAsync(cid1, default);
        Assert.True(retrieved.HasValue);
        Assert.Equal(bytes, retrieved.Value.ToArray());
    }

    [Fact]
    public async Task pin_unpin_reflects_in_exists_locally() { ... }

    [Fact]
    public async Task get_of_unknown_cid_returns_null() { ... }
}
```

- [ ] **Step 8: Build + test; commit**

```bash
dotnet build packages/federation-blob-replication/Sunfish.Federation.BlobReplication.csproj
dotnet test packages/federation-blob-replication/tests/Sunfish.Federation.BlobReplication.Tests/ \
  --filter "FullyQualifiedName!~Cluster&FullyQualifiedName!~Attestation"
git add packages/federation-blob-replication/
git commit -m "feat(federation): add IpfsBlobStore + thin Kubo RPC client (D-IPFS-INTEGRATION, D-KUBO-CLIENT)"
```

---

## Task D-6: IPFS-Cluster integration + attestation

**Goal:** Multi-node pinning with replication factor + attestation scaffolding. Cluster runs alongside Kubo as a second sidecar and coordinates pinning via Raft consensus.

- [ ] **Step 1: `Cluster/IpfsClusterClient.cs`**

Thin HTTP client over IPFS-Cluster's `/pins` API (separate from Kubo; Cluster exposes its API on port 9094 by default). Methods: `PinAsync(Cid, replicationFactorMin, replicationFactorMax)`, `UnpinAsync(Cid)`, `GetPinStatusAsync(Cid)`, `ListPinsAsync()`.

- [ ] **Step 2: `Cluster/DefaultBlobPinningPolicy.cs`**

```csharp
public sealed class DefaultBlobPinningPolicy : IBlobPinningPolicy
{
    private readonly ClusterPinningOptions _options;

    // D-PINNING-POLICY: default replication factor 3
    public int ReplicationFactorMin => _options.MinReplicas;  // default 3
    public int ReplicationFactorMax => _options.MaxReplicas;  // default 3

    public bool ShouldPinOnPut(Cid cid) => true;
    public bool ShouldUnpinOnEntityDelete(Cid cid, int remainingReferences) => remainingReferences == 0;
}
```

- [ ] **Step 3: `Attestation/BlobAttestationProducer.cs` (D-ATTESTATION)**

Hosted service running on a 24-hour timer (configurable). Each cycle:

1. Enumerate all pinned CIDs on this node (`kubo /api/v0/pin/ls`).
2. Produce a `BlobAttestation` record: `{ NodeId, Cids, Timestamp, Signature }`.
3. Sign with the node's Ed25519 key.
4. Store in the local audit log (D-AUDIT-TRAIL-FEDERATION — attestations are themselves audit records).
5. Broadcast to peers in `IPeerRegistry` via `ISyncTransport` as `SyncMessageKind.BlobAttestationBroadcast`.

```csharp
public sealed record BlobAttestation(
    PeerId NodeId,
    IReadOnlyList<Cid> Cids,
    Instant AttestedAt,
    Ed25519Signature Signature);

public sealed class BlobAttestationProducer : BackgroundService
{
    private readonly TimeSpan _interval;  // from config, default 24h

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            await ProduceAndBroadcastAsync(ct);
        }
    }
}
```

- [ ] **Step 4: `Attestation/SilentContentLossDetector.cs`**

Second hosted service — checks every N hours whether every CID referenced by the local entity store has a recent attestation (default window: 72 hours) from at least one peer. If a CID goes without a fresh attestation, raise an `OperationalAlert` event. Per `ipfs-evaluation.md` §7.4.

- [ ] **Step 5: Integration test — `ThreeNodeClusterReplicationTests.cs`**

Three-container Testcontainers fixture: `kubo-a` + `cluster-a`, `kubo-b` + `cluster-b`, `kubo-c` + `cluster-c`. Cluster members are bootstrapped into a single Raft consensus group. Test:

1. `PutAsync` on node A → CID returned.
2. `IpfsClusterClient.PinAsync` with replication factor 3 → cluster distributes.
3. Wait for `GetPinStatusAsync` to report 3 `pinned` allocations.
4. `GetAsync` on node C → retrieves same bytes (Bitswap fetches from wherever in the cluster has them).

The 3-node cluster fixture is significant enough that it gets its own fixture class reused across Pattern A tests.

- [ ] **Step 6: Integration test — `BlobAttestationProducerTests.cs`**

Fast-forward the timer (inject a `TimeProvider`), verify the producer emits an attestation with the expected CID list and signature.

- [ ] **Step 7: Integration test — `SilentContentLossDetectorTests.cs`**

Seed an entity referencing a CID. Seed no attestation. Run detector. Assert an `OperationalAlert` is raised with the expected CID.

- [ ] **Step 8: Build + test; commit**

```bash
dotnet test packages/federation-blob-replication/tests/Sunfish.Federation.BlobReplication.Tests/
git add packages/federation-blob-replication/
git commit -m "feat(federation): add IPFS-Cluster integration + attestation flow (D-PINNING-POLICY, D-ATTESTATION)"
```

---

## Task D-7: Pattern A — PM companies + city code enforcement (worked example)

**Goal:** End-to-end integration test demonstrating spec §10.4 Pattern A. Three Sunfish nodes (ACME Rentals, BigCo Properties, City of Springfield). PMs push inspection reports; city pulls; city produces attestations that flow back. Exercises all three federation concerns at once.

**Worked example setup:**

```
  ACME Rentals (node A)          Big Co Properties (node B)         City of Springfield (node C)
         │                               │                                  │
         │   push: inspection report     │                                  │
         ├─────────────────────────────────────────────────────────────────▶│
         │                               │   push: inspection report       │
         │                               ├─────────────────────────────────▶│
         │                               │                                  │
         │◀─── attest: "received, compliant" ─────────────────────────────────│
         │                               │◀────── attest: "received" ───────│
```

Both PMs have delegations from the city (capability-graph edges) enabling them to write inspection reports federated to the city's node. The city issues signed attestations back.

- [ ] **Step 1: `PatternA_PmAndCityCodeEnforcement/ThreeNodeFixture.cs`**

Testcontainers fixture that stands up three full Sunfish nodes (three `WebApplicationFactory` instances + three Kubo sidecars + one IPFS-Cluster + Postgres per node via Testcontainers). Not cheap — this fixture runs once per test *class*, not per test.

- [ ] **Step 2: `InspectionFederationTests.cs`**

```csharp
[Fact]
public async Task acme_push_inspection_to_city_with_photos()
{
    var (acme, bigCo, city) = (Fixture.Acme, Fixture.BigCo, Fixture.City);

    // Setup: city has delegated inspect-report-write capability to acme and bigco (Phase B)
    await city.Capabilities.DelegateAsync(
        from: city.InspectorRole,
        to:   acme.PropertyManagerPrincipal,
        scope: "property.inspection.write",
        expires: Instant.Now.Plus(Duration.FromDays(365)));
    await acme.Federation.Capabilities.ReconcileAsync(city.PeerDescriptor, default);

    // ACME creates an inspection with two photos
    var photo1 = await acme.Blobs.PutAsync(TestImages.Kitchen_JPEG, default);
    var photo2 = await acme.Blobs.PutAsync(TestImages.Bathroom_JPEG, default);

    var inspectionId = await acme.Entities.CreateAsync(new Inspection(
        PropertyId: "property-42",
        InspectorId: acme.InspectorJim.Id,
        Deficiencies: [new Deficiency("Leak under kitchen sink", Severity.High, Photos: [photo1, photo2])],
        OccurredAt: Instant.Now));

    // ACME pushes to city
    await acme.Federation.Entities.PushToAsync(city.PeerDescriptor, default);

    // City should now have the entity (verified signature) and the blobs (via IPFS replication)
    var cityView = await city.Entities.GetAsync<Inspection>(inspectionId);
    Assert.Equal(acme.InspectorJim.Id, cityView.InspectorId);

    var cityPhoto1 = await city.Blobs.GetAsync(photo1, default);
    Assert.NotNull(cityPhoto1);
    Assert.Equal(TestImages.Kitchen_JPEG, cityPhoto1.Value.ToArray());

    // City produces an attestation back
    var attestation = await city.IssueAttestationAsync(inspectionId, "received, compliant", default);
    await city.Federation.Entities.PushToAsync(acme.PeerDescriptor, default);

    var acmeAttestation = await acme.Audit.GetAttestationsForAsync(inspectionId);
    Assert.Single(acmeAttestation);
    Assert.Equal("received, compliant", acmeAttestation[0].Assertion);
}
```

- [ ] **Step 3: `CapabilityDelegationTests.cs`**

Focus on the capability-graph side: city delegates to ACME; capability sync propagates the delegation; ACME's local capability evaluator now grants ACME write access to city-owned inspection schemas. Revocation test: city revokes; after re-sync, ACME's evaluator denies.

- [ ] **Step 4: `BlobReplicationTimingTests.cs`**

Measure end-to-end latency: ACME `PutAsync` → city `GetAsync` returns non-null. Must complete within a generous test-environment budget (e.g., 30 seconds). Not a perf benchmark — a sanity check that Cluster eventually replicates.

- [ ] **Step 5: `AttestationFlowTests.cs`**

City pins received blob, emits 24-hour attestation (fast-forwarded), attestation propagates back to ACME. ACME queries its audit log and sees city's attestation for the CID. Simulate 72 hours of no attestation → `SilentContentLossDetector` raises alert.

- [ ] **Step 6: Commit**

```bash
git add packages/federation-patterns/
git commit -m "test(federation): pattern A end-to-end — two PM companies + city code enforcement (spec §10.4 A)"
```

---

## Task D-8: Pattern B — base command with air-gapped child bases (worked example)

**Goal:** Demonstrate spec §10.4 Pattern B. A military central command has a Sunfish instance. A child base is air-gapped (no network). Federation happens via **signed JSON bundles** on physical media. Phase D's transport abstraction is exercised without any live connection.

**Key property:** the `ISyncTransport` abstraction (Task D-1) accommodates offline transfer because the protocol is message-based — an envelope is a self-contained signed payload. "Transport" can be a USB stick.

- [ ] **Step 1: `PatternB_BaseCommandAirGapped/SignedBundleExporter.cs`**

```csharp
public sealed class SignedBundleExporter
{
    private readonly IEntitySyncer _entitySyncer;
    private readonly ICapabilitySyncer _capabilitySyncer;
    private readonly IBlobStore _blobs;
    private readonly Ed25519KeyPair _nodeKey;

    /// <summary>
    /// Produce a signed JSON bundle containing: (1) heads announcement for every entity to export,
    /// (2) all change records + audit records up to those heads, (3) membership ops relevant to
    /// the receiver, (4) blob bytes for every CID referenced by exported entities.
    /// </summary>
    public async ValueTask<byte[]> ExportBundleAsync(
        PeerDescriptor recipient,
        IReadOnlyList<EntityId> scope,
        CancellationToken ct)
    {
        var bundle = new SignedBundle
        {
            ProducedBy = _nodeKey.PublicKey,
            ProducedAt = Instant.Now,
            Recipient  = recipient.PeerId,
            Entities   = await GatherEntitiesAsync(scope, ct),
            Capabilities = await GatherCapabilityOpsForAsync(recipient, ct),
            Blobs      = await GatherBlobsAsync(scope, ct)
        };

        var serialized = CanonicalSerialize(bundle);
        var signature  = _nodeKey.Sign(serialized);
        bundle.BundleSignature = signature;

        return SerializeWithSignature(bundle);
    }
}
```

- [ ] **Step 2: `PatternB_BaseCommandAirGapped/SignedBundleImporter.cs`**

```csharp
public sealed class SignedBundleImporter
{
    public async ValueTask<ImportResult> ImportBundleAsync(byte[] bundleBytes, CancellationToken ct)
    {
        var (bundle, signature) = Deserialize(bundleBytes);

        // 1. Verify producer's pubkey is in our known-peer registry
        if (!_peers.IsTrusted(bundle.ProducedBy))
            return ImportResult.Rejected("Unknown producer");

        // 2. Verify bundle-level signature
        if (!Ed25519.Verify(bundle.ProducedBy, CanonicalSerialize(bundle), signature))
            return ImportResult.Rejected("Invalid bundle signature");

        // 3. Apply capability ops (these gate everything else)
        foreach (var op in bundle.Capabilities)
            await _capabilitySyncer.ApplyOpAsync(op, ct);

        // 4. Import blobs into the local blob store (content-addressed — CID will match)
        foreach (var (cid, bytes) in bundle.Blobs)
            await _blobs.PutAsync(bytes, ct);  // idempotent on CID

        // 5. Apply entity changes + audit records, verifying each change's signature
        foreach (var change in bundle.Entities.Changes)
            await _entitySyncer.ApplyChangeAsync(change, ct);

        return ImportResult.Accepted(bundle.Entities.Changes.Count, bundle.Capabilities.Count);
    }
}
```

- [ ] **Step 3: `AirGappedFederationTests.cs`**

```csharp
[Fact]
public async Task central_base_exports_bundle_child_base_imports_matches_state()
{
    var central = await TestKernel.CreateAsync("central-command");
    var child   = await TestKernel.CreateAsync("child-base-alpha");

    // Central has some data; child knows nothing yet
    var orderId = await central.Entities.CreateAsync(new OperationalOrder(...));
    await central.Blobs.PutAsync(TestFiles.MissionBrief_PDF, default);

    // Export to a bundle. In production, this file is written to physical media.
    var bundle = await central.Federation.ExportBundleAsync(child.PeerDescriptor, [orderId], default);

    // Simulate courier: bundle bytes move to child base
    var result = await child.Federation.ImportBundleAsync(bundle, default);

    Assert.True(result.Accepted);
    Assert.Equal(1, result.EntitiesImported);

    var childView = await child.Entities.GetAsync<OperationalOrder>(orderId);
    Assert.NotNull(childView);
}

[Fact]
public async Task tampered_bundle_is_rejected_at_signature_check()
{
    var bundle = await central.Federation.ExportBundleAsync(...);
    bundle[100] ^= 0xFF;  // flip a byte
    var result = await child.Federation.ImportBundleAsync(bundle, default);
    Assert.False(result.Accepted);
    Assert.Contains("signature", result.Reason, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Commit**

```bash
git add packages/federation-patterns/PatternB_BaseCommandAirGapped/
git commit -m "test(federation): pattern B end-to-end — air-gapped base command via signed bundles (spec §10.4 B)"
```

---

## Task D-9: Pattern C — contractor portal with Macaroon-bound access

**Goal:** Spec §10.4 Pattern C — PM grants temporary access to a contractor via Macaroon. No persistent federation peer. Contractor hits a read-only endpoint on the PM's Sunfish node; endpoint accepts Macaroons as bearer tokens (D-MACAROON-REUSE) and filters visible data per caveats.

This is the "not every federation is peer-to-peer" pattern — it demonstrates that Sunfish federation surface area includes Macaroon-bound portals as a narrow, scoped federation shape.

- [ ] **Step 1: `PatternC_ContractorPortal/ContractorReadOnlyEndpoint.cs`**

```csharp
public static class ContractorReadOnlyEndpoint
{
    public static IEndpointRouteBuilder MapContractorPortal(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/sunfish/federation/contractor/{entityId}",
            async (string entityId, HttpContext ctx, IMacaroonVerifier verifier, IEntityStore entities, CancellationToken ct) =>
            {
                // Extract Macaroon from Authorization header
                if (!TryExtractMacaroon(ctx, out var macaroon))
                    return Results.Unauthorized();

                // Verify + evaluate caveats (Phase B)
                var evaluation = verifier.Verify(macaroon, new RequestContext
                {
                    Time = Instant.Now,
                    Resource = entityId,
                    Action = "read",
                    DeviceIp = ctx.Connection.RemoteIpAddress
                });

                if (!evaluation.Authorized)
                    return Results.Forbid();

                var entity = await entities.GetAsync(entityId, ct);
                if (entity is null) return Results.NotFound();

                // Filter output per caveats (e.g., redact fields the macaroon doesn't grant)
                var filtered = FilterPerCaveats(entity, evaluation);
                return Results.Json(filtered);
            });

        return endpoints;
    }
}
```

- [ ] **Step 2: `MacaroonFederationTests.cs`**

```csharp
[Fact]
public async Task contractor_with_valid_macaroon_can_read_scoped_work_order()
{
    var pm = await TestKernel.CreateAsync("pm-company");
    var workOrderId = await pm.Entities.CreateAsync(new WorkOrder("unit-42", "Paint living room"));

    var macaroon = pm.MacaroonService.Mint(
        subject: "contractor:alice",
        caveats: [
            Caveat.Time($"< {Instant.Now.Plus(Duration.FromDays(7))}"),
            Caveat.ResourceSchemaMatches("sunfish.pm.work-order/*"),
            Caveat.ResourceId(workOrderId.Value),
            Caveat.ActionIn("read")
        ]);

    using var client = pm.CreateHttpClient();
    client.DefaultRequestHeaders.Add("Authorization", $"Macaroon {Convert.ToBase64String(macaroon.Serialize())}");

    var response = await client.GetAsync($"/.well-known/sunfish/federation/contractor/{workOrderId}");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var workOrder = await response.Content.ReadFromJsonAsync<WorkOrder>();
    Assert.Equal("unit-42", workOrder.UnitId);
}

[Fact]
public async Task expired_macaroon_returns_403()
{
    var macaroon = pm.MacaroonService.Mint(
        subject: "contractor:alice",
        caveats: [Caveat.Time($"< {Instant.Now.Minus(Duration.FromSeconds(1))}")]);
    // ... request returns 403
}

[Fact]
public async Task macaroon_scoped_to_unit_42_cannot_read_unit_99() { ... }

[Fact]
public async Task attenuated_macaroon_is_strictly_less_powerful() { ... }
```

- [ ] **Step 3: Commit**

```bash
git add packages/federation-patterns/PatternC_ContractorPortal/
git commit -m "test(federation): pattern C end-to-end — contractor portal via Macaroons (spec §10.4 C, D-MACAROON-REUSE)"
```

---

## Task D-10: Operator guide documentation

**Goal:** Ship a federation operator guide under `docs/federation/`. The operator audience is a site-reliability engineer or DevOps person setting up a Sunfish federation node — not a C# developer, not a product manager. The guide must be concrete enough to execute, not aspirational.

- [ ] **Step 1: `docs/federation/operator-guide.md` (index)**

```markdown
# Sunfish Federation Operator Guide

This guide covers setting up, operating, and troubleshooting a Sunfish federation node.
It assumes you are familiar with Linux, Docker, and basic PKI concepts.

## Contents
- [Private IPFS network setup](./private-network-setup.md)
- [Swarm key generation and rotation](./swarm-key-generation.md)
- [IPFS-Cluster configuration](./ipfs-cluster-config.md)
- [Kubo version pinning and upgrade policy](./kubo-version-pinning.md)
- [Attestation schedule and alerting](./attestation-schedule.md)
- [Troubleshooting common issues](./troubleshooting.md)

## Quick-start reference architecture

Each Sunfish federation node is three processes running side-by-side:

1. **Sunfish application server** (.NET 10; the app that consumes `Sunfish.Federation.*` packages).
2. **Kubo daemon** (`ipfs/kubo:v0.28.0`) listening on `localhost:5001` RPC, configured with a private-network swarm key.
3. **IPFS-Cluster** (`ipfs/ipfs-cluster:v1.0.8`) listening on `localhost:9094`, joined to the jurisdiction's cluster via Raft consensus.

No node talks to the public IPFS DHT; all peer discovery is via cluster bootstrap + explicit Sunfish peer registry.
```

- [ ] **Step 2: `docs/federation/private-network-setup.md`**

Covers:
- Generating the swarm key with `scripts/generate-swarm-key.sh` (32-byte hex-encoded secret).
- Placing the swarm key at `~/.ipfs/swarm.key` or equivalent volume mount.
- Setting `LIBP2P_FORCE_PNET=1` environment variable (Kubo refuses to start without a swarm key when this is set).
- Removing default bootstrap peers (`ipfs bootstrap rm --all`) and adding only your cluster's bootstrap peers.
- Verifying isolation: a test CID put on node A should NOT be retrievable from a public gateway.

- [ ] **Step 3: `docs/federation/swarm-key-generation.md`**

Covers the exact hex format Kubo expects, key rotation procedure (all nodes must get the new key simultaneously; use a rolling window), storing the key in a secrets manager (Vault, Azure Key Vault, AWS Secrets Manager) and mounting it read-only at node startup.

- [ ] **Step 4: `docs/federation/ipfs-cluster-config.md`**

Covers:
- Cluster peer bootstrap: `service.json` and `identity.json` layout.
- Raft consensus configuration: peer-add workflow, safe peer-remove workflow, Raft snapshot tuning.
- Replication policy: setting `replication_factor_min` / `replication_factor_max` to 3 (D-PINNING-POLICY default).
- Cluster upgrade procedure: rolling restart, Raft leader step-down.

- [ ] **Step 5: `docs/federation/kubo-version-pinning.md`**

The Phase-D-tested Kubo version is `v0.28.0`. Operators should pin to a known-good version; silent upgrades break integration tests as often as they fix anything. Documented upgrade procedure: stage on a non-production node, run integration smoke tests, promote to production.

- [ ] **Step 6: `docs/federation/attestation-schedule.md`**

Covers the 24-hour `BlobAttestationProducer` schedule (D-ATTESTATION), the 72-hour `SilentContentLossDetector` alert window, how to tune both via `Sunfish:Federation:AttestationIntervalHours` and `Sunfish:Federation:LossAlertThresholdHours`, and operational integration (wiring the alert into PagerDuty, Opsgenie, or Slack).

- [ ] **Step 7: `docs/federation/troubleshooting.md`**

Common issues and remediation:

| Symptom | Likely cause | Remediation |
|---|---|---|
| App startup throws "Swarm key required in production" | Missing D-PRIVATE-NETWORK config | Set `Sunfish:Federation:SwarmKeyPath`; see swarm-key-generation.md |
| `PullFromAsync` returns `ChangesRejected` with "Unknown principal" | Peer's public key not in local `IPeerRegistry` | Add peer via `scripts/add-peer.sh` |
| `IpfsBlobStore.GetAsync` returns null for CID that peer claims to have | Blob not yet replicated by Cluster | Check `cluster-ctl status <cid>`; verify replication factor |
| Cluster Raft re-election loop | Network partition or clock skew | Check NTP sync; see ipfs-cluster-config.md rolling restart |
| RIBLT capability sync falls back to full-set every time | Diff too large or hash collision | Expected for initial sync of new peer; monitor fallback count trend |
| Attestation producer emits no attestations | Ed25519 node key missing or unreadable | Check `Sunfish:Federation:NodeKeyPath` points to a valid keypair file |

- [ ] **Step 8: `scripts/generate-swarm-key.sh`**

```bash
#!/usr/bin/env bash
# Generate a 32-byte IPFS swarm key in the format Kubo expects.
# Usage: scripts/generate-swarm-key.sh > swarm.key
set -euo pipefail
printf '/key/swarm/psk/1.0.0/\n/base16/\n'
head -c 32 /dev/urandom | od -A n -t x1 | tr -d ' \n'
printf '\n'
```

- [ ] **Step 9: `scripts/bootstrap-federation-test-cluster.sh`**

Dev-time helper that starts 3 Kubo + 1 Cluster via Docker Compose for local federation testing. Not for production — purely for operator-guide walk-throughs and quick spin-ups.

- [ ] **Step 10: Commit**

```bash
git add docs/federation/ scripts/generate-swarm-key.sh scripts/bootstrap-federation-test-cluster.sh
git commit -m "docs(federation): operator guide — private network, swarm keys, cluster, attestations, troubleshooting"
```

---

## Phase D Summary — What This Produces

| Deliverable | Location |
|---|---|
| Shared sync envelope + transport abstraction | `packages/federation-common/` |
| Entity sync (Automerge-inspired delta sync) | `packages/federation-entity-sync/` |
| Capability sync (RIBLT + Ed25519 + revocation) | `packages/federation-capability-sync/` |
| IPFS-backed blob store + cluster replication + attestation | `packages/federation-blob-replication/` |
| Pattern A integration test — PM + city code enforcement | `packages/federation-patterns/PatternA_PmAndCityCodeEnforcement/` |
| Pattern B integration test — air-gapped base command | `packages/federation-patterns/PatternB_BaseCommandAirGapped/` |
| Pattern C integration test — contractor portal with Macaroons | `packages/federation-patterns/PatternC_ContractorPortal/` |
| Operator guide | `docs/federation/` |
| Swarm key helper + bootstrap scripts | `scripts/generate-swarm-key.sh`, `scripts/bootstrap-federation-test-cluster.sh` |
| Updated central package pinning | `Directory.Packages.props` (Testcontainers, System.Net.Http.Json, etc.) |

---

## Self-Review Checklist

- [ ] Prerequisite phase artifacts verified (Task D-0 Step 1): `Entity`, `Version`, `AuditRecord`, `Ed25519KeyPair`, `SignedOperation<T>`, `Principal`, `MembershipOp`, `IBlobStore`, `Cid` all exist in Foundation
- [ ] Branch is `feat/platform-phase-D-federation`
- [ ] `Directory.Packages.props` includes Testcontainers 4.1.0, Testcontainers.Xunit, System.Net.Http.Json, Microsoft.Extensions.Http
- [ ] Every new `.csproj` has `<HasNoBlazorDependency>true</HasNoBlazorDependency>` (D-NO-BLAZOR-INVARIANT)
- [ ] No federation package references `ui-core`, `ui-adapters-*`, `blocks-*`, or `compat-*`
- [ ] `Sunfish.Federation.Common.csproj` builds
- [ ] `SyncEnvelope` has Ed25519 signature verification
- [ ] `InMemorySyncTransport` works for two-peer tests
- [ ] `HttpSyncTransport` uses mTLS per D-PEER-AUTH
- [ ] `FederationStartupChecks` throws in Production with no swarm key (D-PRIVATE-NETWORK)
- [ ] `FederationStartupChecks` throws in Production when Kubo reports non-private profile
- [ ] `FederationStartupChecks` passes in Development
- [ ] `IEntitySyncer.PullFromAsync` and `.PushToAsync` exist and are covered by tests
- [ ] `InMemoryTwoPeerConvergenceTests` green — two peers with divergent histories converge
- [ ] `SignatureRejectionTests` green — tampered change is rejected and not applied
- [ ] `ConcurrentEditMergeTests` green — three-way concurrent edit converges under CRDT
- [ ] `AuditChainVerifierTests` green — broken chain linkage is rejected (D-AUDIT-TRAIL-FEDERATION)
- [ ] `HttpEntitySyncEndpointTests` green — cross-process HTTP sync works
- [ ] Unknown peer returns HTTP 401 on entity-sync endpoint (D-PEER-AUTH)
- [ ] `ICapabilitySyncer.ReconcileAsync` exists
- [ ] `RibltEncoder` / `Decoder` round-trip tested with reference vectors
- [ ] `RibltDecoderFailureFallbackTests` green — fallback to full-set exchange works (D-RIBLT-COMPLEXITY)
- [ ] `TwoPeerConvergenceTests` for capability sync green
- [ ] `RevocationConvergenceTests` green — revocation propagates and denies access
- [ ] `MembershipOpVerifier` rejects invalid Ed25519 signatures on ops
- [ ] `IpfsBlobStore` uses thin `KuboHttpClient` (not `Ipfs.Http.Client` library) (D-KUBO-CLIENT)
- [ ] `IpfsBlobStoreTests` green against Testcontainers Kubo v0.28.0
- [ ] `PutAsync` of identical bytes produces identical CID (idempotent)
- [ ] `GetAsync` on unknown CID returns null (not an exception)
- [ ] `IpfsClusterClient` talks to Cluster `/pins` API on port 9094
- [ ] `DefaultBlobPinningPolicy.ReplicationFactorMin == 3` (D-PINNING-POLICY)
- [ ] `BlobAttestationProducer` emits signed attestations on 24-hour timer (D-ATTESTATION)
- [ ] `SilentContentLossDetector` raises alert after 72 hours of no attestation
- [ ] `ThreeNodeClusterReplicationTests` green — put on A, get on C works after replication
- [ ] Pattern A `InspectionFederationTests` green — ACME pushes inspection + photos; city gets signature-verified entity + IPFS-replicated blobs
- [ ] Pattern A `CapabilityDelegationTests` green — city delegates to ACME, capability sync propagates, revocation works
- [ ] Pattern A `AttestationFlowTests` green — city emits attestation, ACME audit log records it, silent-loss simulation raises alert
- [ ] Pattern B `AirGappedFederationTests` green — central exports bundle, child imports, state matches
- [ ] Pattern B tampered-bundle test green — flipped byte → rejection
- [ ] Pattern C `MacaroonFederationTests` green — valid Macaroon allows scoped read; expired / scope-mismatch rejected
- [ ] Pattern C attenuation test green — attenuated Macaroon is strictly less powerful
- [ ] `docs/federation/operator-guide.md` index exists and links all sub-docs
- [ ] `docs/federation/private-network-setup.md` complete with fail-fast check documentation
- [ ] `docs/federation/swarm-key-generation.md` complete with format + rotation
- [ ] `docs/federation/ipfs-cluster-config.md` complete with Raft operational guidance
- [ ] `docs/federation/kubo-version-pinning.md` names `ipfs/kubo:v0.28.0` explicitly
- [ ] `docs/federation/attestation-schedule.md` documents 24h producer + 72h detector defaults
- [ ] `docs/federation/troubleshooting.md` covers swarm key missing, unknown peer, blob not yet replicated, Raft re-election, RIBLT fallback, missing node key
- [ ] `scripts/generate-swarm-key.sh` is executable and produces Kubo-format keys
- [ ] `scripts/bootstrap-federation-test-cluster.sh` spins up 3 Kubo + 1 Cluster locally
- [ ] `dotnet build` for every federation csproj = 0 warnings, 0 errors
- [ ] `dotnet test` for every federation test project = all green
- [ ] Federation packages' public surface is documented with XML doc comments on `I*` and public types
- [ ] No reference to automerge-rs, automerge-c, or any automerge library in any csproj (D-CRDT-LIB confirms we rebuild, not integrate)
- [ ] No reference to `dotnet-libp2p` — sidecar Kubo is the only path (D-IPFS-INTEGRATION)
- [ ] Top-of-plan Platform Context section links to `docs/specifications/sunfish-platform-specification.md` and both research notes

---

## Known Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Prerequisite Phase A is not complete (no `Entity` / `Version` / `AuditRecord` primitives) | Task D-0 Step 1 halts the phase; explicit escalation to the Phase A plan owner |
| Prerequisite Phase B is not complete (no `Ed25519KeyPair` / `Principal` / `MembershipOp`) | Same — Task D-0 Step 1 halts |
| Prerequisite Phase B-blobs is not complete (no `IBlobStore` / `Cid`) | Same — Task D-0 Step 1 halts |
| RIBLT implementation has edge cases we miss | D-RIBLT-COMPLEXITY: `RibltFallback` shipped as a correctness safety net; tests exercise both happy path and fallback |
| Kubo Docker image changes behavior between minor versions | D-TESTCONTAINERS: pin `ipfs/kubo:v0.28.0` explicitly; operator guide documents upgrade procedure |
| IPFS-Cluster Raft election instability on CI | `ThreeNodeClusterReplicationTests` uses generous wait budget (30s for replication); tests are `[Trait("Category", "slow-integration")]` so they can be excluded from fast-feedback CI rings |
| `.NET libp2p` matures mid-phase and pressure arises to adopt it in-process | D-IPFS-INTEGRATION locks sidecar Kubo for Phase D; future phase can re-evaluate |
| Keyhive (BeeKEM) matures mid-phase and pressure arises to adopt it | Out-of-scope declaration at the top of Scope; pre-shared group secrets for confidentiality in Phase D; Keyhive is its own phase |
| Byzantine-peer DoS concerns from security review | Out-of-scope declaration + operator guide documents rate-limiting the federation endpoint (standard ASP.NET middleware) as a deployment concern |
| Schema-CID mismatches when peers run different schema-registry versions | Phase A's schema-registry CID-resolution handles this at the entity layer; Phase D's sync verifies the CID and refuses to apply changes whose schema CID is unknown locally — surfaces as a `Rejections` entry with a specific reason |
| Attestation spam — every node every 24 hours across N peers | Operator guide documents that attestations are broadcast to peers + stored in local audit log; retention policy ages out old attestations after a quarter; alert detector only reads the freshest |
| `Ipfs.Http.Client` pressure from operators wanting a "supported" library | D-KUBO-CLIENT documents the thin-client rationale; the Kubo RPC spec is stable; the Phase D-owned code is ~400-600 lines, low maintenance cost |
| CI cost of Testcontainers-heavy integration tests | Tests are categorized; `fast` tier excludes `slow-integration`; full matrix runs on nightly and on merge-to-main |

---

## Parking Lot — Deferred to Future Phases

- **BeeKEM continuous group key agreement** for end-to-end encryption of entity payloads. Phase D uses TLS transport + pre-shared group secrets; BeeKEM arrives when Keyhive stabilizes.
- **libp2p pubsub** as a federation transport (spec §3.6 names it). Phase D ships HTTP+TLS only.
- **WebRTC / Bluetooth transports** for contractor mobile use cases (offline-first). Phase D is server-to-server; a future local-first accelerator is the natural home.
- **Byzantine-fault-tolerant consensus** across federated peers. Phase D assumes peers follow the protocol.
- **Blockchain-based audit notarization** (spec §3.3 extension point mentioning Hyperledger Fabric). Phase D's audit federation is signed-chain-based, not blockchain.
- **Cross-jurisdictional policy composition** (spec §10.5). Phase D federates the capability graph; policy-pack composition is a separate concern.
- **Multi-master Cluster federation** — federated IPFS-Clusters across jurisdictions (not just within one). Phase D has one Cluster per jurisdiction with peer-to-peer replication at the Bitswap layer; cross-Cluster replication is richer ops.
- **Schema migration during sync** — a peer receives a change whose schema CID is unknown locally. Phase D surfaces this as a `Rejections` entry; a future phase auto-fetches the schema CID and retries.
- **Performance optimization** — Phase D correctness first; benchmark + optimize later. Sync throughput, RIBLT decode time, Kubo RPC round-trip time all need measurement once the functional surface is stable.

---

## Cross-Reference Index

- Spec §2.5 Federation Model: entity-level federation, three patterns (push/pull/gossip), transport-pluggable → Tasks D-1, D-2, D-3, D-4.
- Spec §3.3 Audit Log with per-entity hash chain + `Prev` link → Task D-2 `AuditChainVerifier`, D-AUDIT-TRAIL-FEDERATION.
- Spec §3.6 Event Bus naming libp2p pubsub as a federation transport → parking lot; HTTP+TLS in Phase D per D-SYNC-TRANSPORT.
- Spec §3.7 Blob Store with CID + pin + attest primitives → Tasks D-5, D-6; D-PINNING-POLICY, D-ATTESTATION.
- Spec §10.2.1 Keyhive-inspired group membership → Task D-4 capability sync.
- Spec §10.2.2 Macaroon-style bearer tokens as supplementary primitive → Task D-9 Pattern C, D-MACAROON-REUSE.
- Spec §10.3 Time-bound access → Pattern C time-caveat test.
- Spec §10.4 Patterns A, B, C → Tasks D-7, D-8, D-9 respectively.
- `automerge-evaluation.md` §1.1 Automerge sync protocol shape → Task D-2 `HeadsAnnouncement` / `ChangesRequest` / `ChangesResponse`.
- `automerge-evaluation.md` §1.3 Keyhive RIBLT for set reconciliation → Task D-4 `RibltEncoder` / `RibltDecoder`.
- `automerge-evaluation.md` §3.1 No .NET Automerge binding → D-CRDT-LIB (rebuild, don't integrate).
- `automerge-evaluation.md` §3.2 Keyhive vs Macaroons reconciliation → spec §10.2 adoption is a prerequisite for Phase D.
- `automerge-evaluation.md` §4.3 Sync protocol is conn-agnostic → D-SYNC-TRANSPORT (HTTP first, other transports later).
- `ipfs-evaluation.md` §3.2 .NET library maturity → D-KUBO-CLIENT (thin client, not `Ipfs.Http.Client`).
- `ipfs-evaluation.md` §3.3 Operational complexity → Task D-10 operator guide.
- `ipfs-evaluation.md` §3.4 Public DHT leakage → D-PRIVATE-NETWORK fail-fast.
- `ipfs-evaluation.md` §3.5 Pinning ≠ durability → D-ATTESTATION + `SilentContentLossDetector`.
- `ipfs-evaluation.md` §4 How IPFS composes with Automerge and Keyhive → Phase D's three concerns (entity, capability, blob) are exactly this composition.
- `ipfs-evaluation.md` §7.4 IPFS-specific risks → operator guide troubleshooting sections.
- `external-references.md` §5.1 IPFS paper → operator guide background reading.
- Phase 9 bridge accelerator `PLATFORM_ALIGNMENT.md` → Platform Phase D row is operationalized by this plan.

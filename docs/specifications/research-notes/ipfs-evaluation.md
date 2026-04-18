# IPFS as a Candidate Implementation for Sunfish Content-Addressed Storage

**Document type:** Research evaluation note
**Audience:** Architects, Phase 9 implementers, future decentralization-track planners
**Date:** 2026-04-17
**Status:** v0.1 — initial evaluation after surveying IPFS, IPLD, libp2p, Kubo, Helia, IPFS-Cluster
**Companion:** `automerge-evaluation.md` — reads together; the two technologies are complementary, not competing

---

## TL;DR

IPFS (InterPlanetary File System) is a content-addressed storage protocol that fits several Sunfish platform-spec deliverables well — specifically **blob storage** (inspection photos, drone imagery, scanned PDFs, BIM models), **content integrity / verifiability** (CID-based), and **peer-to-peer replication** across jurisdictional boundaries. It is complementary to Automerge/Keyhive: Automerge handles mutable structured entities, Keyhive handles capability-based access, **IPFS handles content-addressed blobs**.

Status is better than Automerge in one way (.NET libraries exist) and similar in another (operational complexity is real).

**Recommendation:** Adopt IPFS-style **content-addressing semantics** (SHA-256/BLAKE3 CIDs as blob identifiers) immediately as the Sunfish blob-storage primitive. **Defer** running actual IPFS nodes / libp2p / the public DHT to a later phase when peer-to-peer federation is an active requirement. This gives us verifiability and deduplication benefits now, with a clean upgrade path to full IPFS later.

For Phase 9 specifically: **no immediate change**. Bridge can store blobs in filesystem/S3/Postgres with CID-style keys without running an IPFS node. Future input-modalities work (spec §7 — drone imagery, sensor logs, satellite) is the first place where real IPFS pays off.

---

## 1. What IPFS provides

### 1.1 Core protocol stack

IPFS isn't a single library — it's a stack of composable protocols:

- **CID (Content Identifier).** A self-describing hash of content. A CID contains: version, codec (what kind of content), multihash (which algorithm — SHA-256, BLAKE3, etc.), and the hash digest itself. Two bytes of content with the same codec produce the same CID. Different content produces different CIDs with very high probability (cryptographic collision resistance).

- **IPLD (InterPlanetary Linked Data).** A data model for content-addressed graphs. An IPLD node is a piece of data (bytes, a JSON-like map, a list) that may contain CID references to other nodes. IPLD gives you a **Merkle DAG**: every node hashes the content of its children transitively, so a root CID uniquely identifies an entire tree or DAG.

- **libp2p.** A modular peer-to-peer networking stack — transports (TCP, QUIC, WebSocket, WebRTC), stream multiplexing, peer discovery, routing. Used by IPFS for inter-node communication, but libp2p is a standalone library used by other projects too.

- **Bitswap.** The block-exchange protocol between IPFS nodes. One peer asks for blocks by CID; another peer that has them sends them.

- **DHT (Distributed Hash Table).** A Kademlia-style DHT for discovering which peers have a given CID. IPFS ships with two modes: public DHT (anyone can participate) and private networks (shared secret required).

- **MFS (Mutable File System).** A name-addressed overlay on top of the content-addressed substrate — lets you think in terms of paths even though the underlying data is keyed by CID.

- **IPNS (InterPlanetary Name System).** Mutable pointers that sign-and-publish an updatable CID under a public key. Gives you "the latest version of X" on top of immutable CIDs.

### 1.2 Implementations

- **Kubo** — Go reference implementation; ships as `ipfs` CLI daemon and library. This is what production IPFS nodes run.
- **Helia** — JavaScript/TypeScript implementation for browser and Node. Modular, actively developed.
- **Rust libp2p** — used by many non-IPFS projects (Polkadot, Substrate); a production-grade peer-to-peer stack.
- **Boxo** — shared Go libraries that Kubo and other Go-based IPFS tooling use.

### 1.3 Higher-level tooling

- **IPFS-Cluster** — orchestration layer that coordinates pinning (keeping content persistent) across a cluster of IPFS nodes. Uses Raft consensus for allocation decisions. Essential for enterprise deployments.
- **Pinning services** — commercial (Pinata, Filebase, web3.storage) or self-hosted. Guarantee that content stays retrievable.
- **IPFS Desktop / IPFS Companion** — end-user apps. Not relevant to Sunfish server-side.
- **DNSLink** — maps DNS names to IPNS addresses, for `ipfs://<domain>` resolution via DNS.

### 1.4 Deployment patterns

**Public IPFS:** Default mode. Anyone can discover your content if you announce it on the DHT. Good for open publishing, bad for enterprise data.

**Private IPFS network:** All nodes share a common swarm key (32-byte secret). Nodes refuse connections from outside the network. DHT is optional. Good for enterprise; typical Sunfish deployment pattern.

**Local-only IPFS:** Single-node or small LAN cluster; no internet; used where an embedded content-addressed object store is needed.

---

## 2. Mapping to Sunfish spec deliverables

| Spec section | IPFS contribution | Fit |
|---|---|---|
| §3 Core Kernel — Blob storage | Content addressing + deduplication + verifiability via CIDs | **Excellent** — direct fit |
| §3 Core Kernel — Entity storage | IPFS is not a database; no queries, no indexing | **No fit** — use Automerge-style CRDT + relational projection |
| §3 Core Kernel — Audit trail | CIDs are tamper-evident; immutable blocks form an audit log naturally | **Good** — complements the change-log primitive |
| §3 Core Kernel — Schema registry | Schemas can be published as IPFS content (immutable, versioned by CID) | **Good** — content-addressed schemas give verifiable schema resolution |
| §3 Core Kernel — Event bus | libp2p pubsub works but isn't the primary IPFS use case | **Partial** — libp2p can be used independently |
| §7 Input Modalities — Forms, spreadsheets | Small structured data; IPFS overhead not justified | **Overkill** |
| §7 Input Modalities — Voice transcription | Raw audio blob: CID is natural identifier; transcription (structured) separate | **Good** for raw-audio archival |
| §7 Input Modalities — Sensor data | High-volume time-series; IPFS per-record is inefficient; batch into chunks is plausible | **Partial** |
| §7 Input Modalities — Drone/robot/satellite imagery | Large binary files, often resubmitted (same CID = automatic dedup); immutable once captured | **Excellent** — primary use case |
| §8 Asset Evolution & Versioning | IPLD Merkle DAG is essentially the same design as Automerge's change graph | **Good** — reinforces CRDT model; shared vocabulary |
| §9 BIM Integration | IFC/Revit files are large immutable snapshots; CID addressing fits perfectly | **Excellent** — fits the enrichment-not-source-of-truth stance (BIM artifacts are external blobs Sunfish references by CID) |
| §10 Federation — Content replication | Inter-jurisdictional peers replicate blobs via Bitswap; private network mode is enterprise-compatible | **Excellent** |
| §10 Federation — Access control | IPFS has no access control; anyone in the swarm can request any CID they know. Access must be layered (Keyhive ciphertext, pre-shared URLs, etc.) | **Mismatch** — IPFS is confidentiality-neutral |

---

## 3. Material mismatches and concerns

### 3.1 IPFS is not a database

**Reality:** IPFS is key-value storage where keys are hashes of values. It does not provide:

- Indexes (need to know the CID ahead of time, or maintain your own mapping)
- Queries (no filter, sort, join)
- Mutation semantics (blobs are immutable; "updating" means new CID)

**Implication:** Sunfish structured entities (leases, inspections, properties) remain in PostgreSQL / EF Core / Automerge-style change log. IPFS is specifically for **large immutable blobs** referenced from entities by CID.

**Recommendation:** Position IPFS as the **blob-storage primitive** in spec §3, separate from the entity-storage primitive. Entities hold CID references to blobs; the kernel resolves those CIDs when fetching binary content.

### 3.2 .NET library maturity

**Reality:** The NuGet packages `Ipfs.Http.Client` and `Ipfs.Core` (by richardschneider) are the main .NET HTTP-RPC clients. Last updated August 2019. Target .NET Standard 1.4/2.0. They work, but they're dormant. A newer community fork (`caunt/IPFS.NET`) exists but is not widely adopted.

**Options:**

| Path | Effort | Maintenance | Verdict |
|---|---|---|---|
| Use `Ipfs.Http.Client` as-is | Low | Medium (dormant library, may need patches) | Plausible for low-intensity use |
| Write a thin HTTP RPC client in .NET | Low-medium | Low (own the code; Kubo RPC is stable) | **Recommended for Sunfish-grade use** |
| Run IPFS node + talk via CLI | Low | Low | Plausible for dev; not production |
| Skip the IPFS library entirely, implement CID hashing natively in .NET | Very low | Very low (CID v1 + SHA-256 is simple) | **Recommended for Phase 9 / near-term** |

**Recommendation:** Start with option 4 — native CID calculation in Sunfish.Foundation (a ~100-line module: `Cid.FromBytes(byte[]) → string`, using SHA-256 and multihash/multibase encoding). This gives us content-addressed blob identifiers without any IPFS daemon. Plan option 2 (custom HTTP RPC client) when real IPFS nodes are in the picture.

### 3.3 Operational complexity

Running a production IPFS setup involves: one or more Kubo daemons per node, IPFS-Cluster for replication coordination, swarm-key management, bootstrap peer configuration, DHT mode selection, pinning policy, gateway configuration if HTTP access is desired.

This is meaningfully more operational work than "PostgreSQL + filesystem." For Sunfish Bridge (single-tenant, server-hosted), the marginal benefit over filesystem blobs is zero. The benefit appears when:

- Multiple jurisdictional nodes federate (inspection report replicates to code-enforcement agency automatically)
- Large blobs are resubmitted often (drone flies over property, re-uploads — same CID, deduped)
- Verifiability matters (regulator can independently hash the blob and compare to the CID Sunfish reported)

**Recommendation:** Don't run IPFS for Phase 9 Bridge. Do plan it for the future federation phase, and make sure the CID-based blob-reference scheme we adopt now (option 4 above) is forward-compatible with a real IPFS node dropping in later.

### 3.4 Public DHT leakage risk

**Reality:** If IPFS nodes are started in default mode, content is discoverable on the public DHT once announced. For a property-management platform handling PII (tenant names, addresses, lease terms), that's a serious risk.

**Mitigation:** Enterprise Sunfish deployments would use **private IPFS networks** (swarm key + disable public DHT + curated bootstrap peers). The setup is well-documented but requires discipline.

**Recommendation:** Document the private-network requirement explicitly in spec §11 (deployment) so that any IPFS integration is **never** the public DHT default. Include a startup check that fails fast if a production deployment is running with a public DHT.

### 3.5 Pinning ≠ durability

**Reality:** IPFS content is retained by nodes that pin it. Unpinned content is eligible for garbage collection. Nodes that never had the content can't serve it.

**Implication:** For an inspection report from 2024 to be retrievable in 2030, *some* node must have been continuously pinning it. Without discipline, content silently disappears.

**Mitigation:** IPFS-Cluster with a replication factor (every blob pinned by at least 3 nodes) is standard. A Sunfish deployment would have at least two pinning nodes per jurisdiction + periodic audits that confirm each entity's referenced CIDs are still pinned somewhere.

**Recommendation:** Spec §3 blob-storage primitive should include an "attestation" concept — a periodic signed statement from storage nodes "I still have CID X, as of timestamp T" — so that Sunfish can detect silent content loss before consumers do.

---

## 4. How IPFS composes with Automerge and Keyhive

The three technologies solve three different problems and compose cleanly:

```
┌─────────────────────────────────────────────────────────────┐
│  Sunfish entity (property:42, inspection:2026-04-17, ...)   │
│                                                              │
│  { id, kind, ...fields, blob_cids: [Q...abc, Q...def] }     │
│        ▲                   ▲                                │
│        │                   │                                │
│        │ Automerge CRDT    │ IPFS CID references            │
│        │ change log        │ to immutable blobs             │
│        │                   │                                │
│   ┌────┴────┐          ┌───┴────┐                           │
│   │ Entity  │          │ Blobs  │                           │
│   │ store   │          │ (IPFS) │                           │
│   └────┬────┘          └───┬────┘                           │
│        │                   │                                │
│        │ Who can read/write entity vs decrypt blob?         │
│        │ Keyhive group membership over Ed25519 keys.        │
│        ▼                   ▼                                │
│   ┌─────────────────────────────┐                           │
│   │ Keyhive capability graph    │                           │
│   │ (principals, groups, ops)   │                           │
│   └─────────────────────────────┘                           │
└─────────────────────────────────────────────────────────────┘
```

- **Automerge semantics** → Sunfish entity change log: mutable structured data, CRDT merges, temporal queries, federation of the *structured* part
- **IPFS CIDs** → Sunfish blob storage: immutable binary content, deduplication, verifiability, federation of the *binary* part
- **Keyhive group graph** → Sunfish capabilities: who can read/write what; membership syncs as its own CRDT; Ed25519 signatures on every operation
- **BeeKEM / encrypted commit graphs** → Content stays confidential even when peer-to-peer replicated, because IPFS stores ciphertext and Keyhive-derived keys gate decryption

Keyhive's own architecture (per the Ink & Switch notebook) uses IPFS-like CIDs internally for its encrypted commit graphs, so the three are already converging toward a shared design.

---

## 5. What adopting IPFS patterns (without running IPFS) gives Sunfish now

Even without deploying a single IPFS node, adopting IPFS's **content-addressing semantics** in Sunfish gives real benefits immediately:

### 5.1 CID-keyed blob storage

Every file Sunfish ingests (inspection photo, scanned lease PDF, drone footage, BIM IFC export) gets hashed and stored keyed by its CID. Reuploads are automatically deduped. Two inspection reports that both reference the same photo share a single blob. Cross-entity queries ("show me everywhere this photo appears") become a single CID lookup.

### 5.2 Verifiable content

A regulator can independently re-hash the blob they received and confirm it matches the CID Sunfish reported. Tampering is detectable. The integrity is cryptographic and non-repudiable.

### 5.3 Forward-compatibility with federation

The CID format (self-describing multihash) is the same whether the blob lives in Sunfish's local PostgreSQL blob table or in a private IPFS network. Migration from "CIDs in Postgres" to "CIDs in IPFS" is a backend swap with no entity-layer changes. We pay zero cost now and preserve the upgrade path.

### 5.4 Schema registry as content

Sunfish schemas (spec §3 primitive) can themselves be content-addressed. A schema version has a CID; entities reference their schema-CID. Reproducible, deterministic, peer-verifiable.

### 5.5 BIM integration

Spec §9 says BIM is an optional enrichment layer. BIM files (IFC) are large immutable snapshots — exactly the kind of blob IPFS was designed for. Sunfish entities reference BIM CIDs without trying to own the BIM workflow. A later IPFS deployment makes these blobs federatable across jurisdictions.

---

## 6. Impact on Phase 9 (Bridge accelerator)

### 6.1 No immediate changes to Phase 9 tasks

Bridge's PostgreSQL + DAB + Aspire setup doesn't need IPFS. Tasks 9-1 through 9-9 unchanged. Task 9-10 inventory gets small additions.

### 6.2 Update Task 9-10's PLATFORM_ALIGNMENT.md

Add rows for blob storage:

```markdown
## Spec Section 3 — Blob Storage

| Primitive | Bridge Status | Notes |
|---|---|---|
| Content-addressed blob IDs (CIDs) | 🔴 | Candidate: IPFS-style CIDs computed in Sunfish.Foundation; see docs/specifications/research-notes/ipfs-evaluation.md |
| Deduplicated blob storage | 🔴 | Adopted automatically if CIDs are used as primary keys |
| Verifiable content integrity | 🔴 | Recipient can re-hash and verify; no platform-level enforcement yet |
| Blob replication across peers | 🔴 | Candidate: IPFS-Cluster in private-network mode; future phase |
```

### 6.3 Near-term adoption opportunity

If time permits during Phase 9 or immediately after, implement a tiny `Sunfish.Foundation.Blobs` module:

- `Cid.FromBytes(byte[] content) → string` — computes a CID v1 (raw codec, SHA-256, base32)
- `IBlobStore` interface — Get/Put/Exists by CID
- `FileSystemBlobStore` default impl — writes to a blob directory using CID-prefix sharding

Bridge uses it for any binary the app ingests (avatars, document attachments). Future work swaps `FileSystemBlobStore` for `IpfsBlobStore` or `S3BlobStore` without changing call sites.

This is ~150 lines of .NET and gives the whole repo immediate dedupe+verify benefits.

---

## 7. Impact on the platform specification

### 7.1 Spec §3 — add blob-storage primitive explicitly

Current §3 kernel has entity storage, version store, audit log, schema registry, permission evaluator, event bus. Add a seventh primitive:

> **Blob store** — content-addressed binary storage. Every blob is identified by a CID (self-describing cryptographic hash). The store handles put/get/exists/pin; replication and persistence are backend-specific (filesystem, S3, IPFS, IPFS-Cluster). Entities reference blobs by CID in their metadata; the kernel resolves CIDs to bytes when requested. Out-of-scope at this primitive layer: encryption, access control (those come from Keyhive), versioning (blobs are immutable).

### 7.2 Spec §7 — call out IPFS as primary transport for large-blob modalities

Input modalities section should name IPFS as the primary replication mechanism for drone imagery, satellite imagery, sensor-batch uploads, BIM artifacts. Small structured inputs (forms, spreadsheets) use the Automerge-style entity store; large binaries go via IPFS. Clear split.

### 7.3 Spec §10 — note IPFS's role in federation

Federation patterns in §10 should explicitly state: the *structured-entity* side of federation uses Automerge-style sync; the *blob* side uses IPFS replication. Both happen over libp2p or equivalent transport; operationally they may be two separate processes on each node.

### 7.4 Spec §12 — add IPFS-specific risks

- **Public DHT leakage** — fail-fast check that production never runs with public DHT enabled
- **Silent content loss** — periodic CID attestations; replication-factor enforcement in IPFS-Cluster
- **Operational complexity** — IPFS-Cluster is not zero-effort; document the ops budget honestly

---

## 8. Open questions

1. **When does Sunfish actually need IPFS vs filesystem blob storage?** Private-single-tenant deployments (most Bridge consumers) never need IPFS; federated multi-jurisdiction deployments do. Should we ship the `FileSystemBlobStore` default and upgrade paths, or commit to IPFS as the canonical implementation from day one?

2. **CID stability across hashing algorithms.** IPFS defaults to SHA-256 but the ecosystem is moving toward BLAKE3 for speed. Which does Sunfish standardize on? The multihash self-describing format means we *can* mix, but the operational story is cleaner with one.

3. **Encryption-at-rest vs encryption-in-transit.** IPFS stores plaintext by default (the point is content-addressability — encrypting changes the CID). For private data, encryption happens at the application layer (Keyhive). Does Sunfish encrypt every blob before IPFS, or only blobs that are going to be federated?

4. **.NET libp2p.** If we ever want to run an IPFS node in-process (no separate Kubo daemon), we need a .NET libp2p implementation. `nethermind/dotnet-libp2p` is the leading .NET candidate but immature. Sidecar Kubo is more pragmatic near-term.

5. **Pinning economics.** Commercial pinning services charge per-GB-per-month. For a property-management org with TBs of drone footage, the ongoing cost can be significant. Self-hosted IPFS-Cluster has a capex flavor. Sunfish should document this as a go-to-market consideration (§13).

6. **IPNS vs DIDs.** IPNS gives mutable pointers over IPFS. W3C DIDs (spec §2 mentions these) are mutable identity pointers. Are they the same thing in different clothes, or do they compose?

---

## 9. Sources

- [IPFS documentation root](https://docs.ipfs.tech/)
- [Content Identifiers (CIDs) concept](https://docs.ipfs.tech/concepts/content-addressing/)
- [Merkle Directed Acyclic Graphs concept](https://docs.ipfs.tech/concepts/merkle-dag/)
- [How IPFS works](https://docs.ipfs.tech/concepts/how-ipfs-works/)
- [Kubo (Go reference implementation)](https://github.com/ipfs/kubo)
- [Helia (JS implementation)](https://helia.io/)
- [IPFS-Cluster](https://ipfscluster.io/)
- [net-ipfs-http-client (.NET HTTP RPC client)](https://github.com/richardschneider/net-ipfs-http-client)
- [Ipfs.Http.Client on NuGet](https://www.nuget.org/packages/Ipfs.Http.Client)
- [IPFS.NET community fork](https://github.com/caunt/IPFS.NET)
- [ELEKS — Building Private IPFS Network with IPFS-Cluster](https://eleks.com/research/ipfs-network-data-replication/)
- [Startup House — Complete Guide to IPFS Private Network](https://startup-house.com/blog/ipfs-private-network-guide)
- [Kubo RPC API clients reference](https://docs.ipfs.tech/reference/kubo-rpc-cli/)

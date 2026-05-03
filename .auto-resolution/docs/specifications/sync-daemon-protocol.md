# Sync Daemon Protocol Specification

**Status:** Draft — Wave 0.1 deliverable of the Paper-Alignment Plan
**Date:** 2026-04-22
**Source paper:** [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) §6 (authoritative) and §11.3
**Audience:** Kernel implementers. Assumes familiarity with the foundational paper.

> This document specifies the wire protocol between Sunfish sync daemons. It does not motivate the architecture — see paper §6 for the *why*. It does not describe CRDT internals, role attestation cryptography, or bucket evaluation — those are sibling specs. It specifies only what goes on the socket.

---

## 1. Overview, Goals, Non-Goals

The sync daemon is a **separate OS process** from the application container. The application connects to it over a local IPC socket; the daemon speaks to peer daemons on other nodes. The split exists so the daemon survives application restarts, crashes, and updates — peer sessions remain established while the app layer churns. The protocol is **leaderless**: every daemon is symmetric, every session is peer-to-peer, and there is no coordinator role baked into message semantics. Two daemons that complete a handshake form an equal session; either may initiate any message type permitted post-handshake.

**In scope:**

- Handshake and capability negotiation between two daemons.
- Continuous CRDT delta streaming, filtered by subscription eligibility.
- CP-class lease coordination messages (Flease, paper §6.3).
- Gossip anti-entropy ping / vector-clock exchange (paper §6.1).
- Error reporting, reconnection, and version negotiation.

**Explicitly out of scope (non-goals):**

- **Application-layer events.** Domain events (`TaskCreated`, `InvoicePosted`, …) flow over the in-process event bus inside the application; they cross the wire only as CRDT operations in `DELTA_STREAM`, never as first-class daemon messages.
- **UI state sync.** Presence, cursors, typing indicators, selection highlights — those ride a separate ephemeral channel (TBD, not this protocol) because they have no durability requirement and should not compete with durable-state sync for bandwidth.
- **Peer discovery.** mDNS / WireGuard / managed-relay discovery is upstream of this protocol; the daemon receives a peer address and opens a session. Discovery is speced in a sibling doc (Wave 2.2).
- **Interior cryptography of attestation bundles.** Opaque CBOR blobs on this wire; speced by the role-attestation doc (Wave 1.6).

**Relationship to `packages/federation-common`.** Existing `ISyncTransport` / `SyncEnvelope` are signed-envelope primitives built for inter-team federation. ADR 0029 (Wave 0.5, sibling) decides whether this daemon reuses those primitives as its transport layer or runs alongside them. This spec is neutral on that choice; where it would influence wire format (e.g., envelope double-signing), the decision is deferred to the ADR.

---

## 2. Transport Layer

### 2.1 Socket path and framing

The daemon listens on a local-only socket:

| Platform | Socket type | Default path |
|---|---|---|
| Linux / macOS / BSD | Unix-domain (SOCK_STREAM) | `$XDG_RUNTIME_DIR/sunfish-sync.sock` |
| Windows | Named pipe | `\\.\pipe\sunfish-sync` |

**Fallback search order on POSIX** (daemon binds the first writable; clients probe in the same order):

1. `$XDG_RUNTIME_DIR/sunfish-sync.sock` if `$XDG_RUNTIME_DIR` is set and the directory exists.
2. `$TMPDIR/sunfish-sync-$UID.sock`.
3. `/tmp/sunfish-sync-$UID.sock`.

Peer-to-peer sessions (daemon-to-daemon across machines) use TCP on a configurable port (default 7473). Framing and message format are identical on both socket flavors; only the transport substrate differs.

### 2.2 Frame format

Every message is length-prefixed:

```
┌────────────────┬──────────────────────────┐
│ u32 length BE  │ CBOR message (length B)  │
└────────────────┴──────────────────────────┘
```

- **Length header:** 4 bytes, big-endian unsigned, gives the byte count of the CBOR payload that follows. Maximum single-frame payload is 16 MiB (`0x01000000`); larger messages (rare — only full bucket snapshots) are chunked with sequence metadata inside the CBOR.
- **Payload:** CBOR (RFC 8949). CBOR is chosen over JSON for compactness and over Protobuf for schema flexibility across mixed-version peers — a newer daemon can include fields an older daemon ignores without breaking parse.

### 2.3 Keep-alive

A daemon that has sent no message for **30 seconds** (paper §6.1) MUST emit a `GOSSIP_PING`. A peer that receives no frame for **90 seconds** (3 × tick) SHOULD close the session and enter reconnection backoff. These are defaults; both are configurable per-session at handshake.

---

## 3. Message Types and Wire Format

All messages share a CBOR envelope with a `type` discriminator and a `body` map:

```
{
  "type": "HELLO" | "CAPABILITY_NEG" | "ACK" | "DELTA_STREAM" |
          "LEASE_REQUEST" | "LEASE_GRANT" | "LEASE_RELEASE" | "LEASE_DENIED" |
          "GOSSIP_PING" | "ERROR",
  "body": { ... type-specific ... }
}
```

CBOR field names are short strings as shown below. Types use compact notation: `u64` = unsigned 64-bit, `bstr` = byte string, `tstr` = text string, `map<K,V>` / `array<T>` as standard.

### 3.1 `HELLO`

Sent by both initiator and responder. **Initiator sends first; responder replies after validating.**

| Field | Type | Notes |
|---|---|---|
| `node_id` | `bstr` (16) | UUID, this node's stable identifier. |
| `schema_version` | `tstr` | Semver of the protocol this daemon speaks (e.g., `"1.0.0"`). |
| `supported_versions` | `array<tstr>` | All protocol versions this daemon can fall back to. |
| `public_key` | `bstr` (32) | Ed25519 public key (raw, not DER). |
| `sent_at` | `u64` | Unix epoch seconds. Replay window ±30 s. |
| `hello_sig` | `bstr` (64) | Ed25519 signature over `node_id ‖ schema_version ‖ sent_at`. |

### 3.2 `CAPABILITY_NEG`

Sent by initiator after both HELLOs exchanged.

| Field | Type | Notes |
|---|---|---|
| `crdt_streams` | `array<tstr>` | Stream identifiers this node wishes to subscribe to. |
| `cp_leases` | `array<tstr>` | Lease-class names this node may request. |
| `bucket_subscriptions` | `array<BucketRequest>` | Bucket eligibility requests. |

Where `BucketRequest`:

| Field | Type | Notes |
|---|---|---|
| `bucket_name` | `tstr` | Declared bucket name per paper §10.2. |
| `attestation_bundle` | `bstr` | **Opaque CBOR blob**; interior format speced by role-attestation doc (Wave 1.6). The daemon forwards unopened. |

### 3.3 `ACK`

Sent by the receiver of `CAPABILITY_NEG`.

| Field | Type | Notes |
|---|---|---|
| `granted_subscriptions` | `array<tstr>` | Subset of requested `crdt_streams ∪ bucket_names` that are granted. |
| `rejected` | `array<Rejection>` | Explicit rejections with reasons. |
| `tick_interval_s` | `u32` | Negotiated GOSSIP_PING interval, default 30. |
| `max_deltas_per_sec` | `u32` | Rate-limit advertised by the sender (see §8). |

Where `Rejection`:

| Field | Type | Notes |
|---|---|---|
| `subscription` | `tstr` | Stream or bucket name. |
| `reason` | `tstr` | One of the codes in §5. |

### 3.4 `DELTA_STREAM`

Continuous after ACK. One frame per CRDT operation or batched operation set.

| Field | Type | Notes |
|---|---|---|
| `stream_id` | `tstr` | Must be in the peer's `granted_subscriptions`. |
| `op_sequence` | `u64` | Monotonically increasing within a stream from this sender. |
| `crdt_ops` | `bstr` | **Opaque to the sync daemon.** CRDT engine deserializes downstream (paper §9). |
| `causal_deps` | `array<tstr>` | Optional; hashes of ops this op depends on, for out-of-order arrival. |

### 3.5 Flease lease messages

#### `LEASE_REQUEST`

| Field | Type | Notes |
|---|---|---|
| `lease_id` | `bstr` (16) | UUID, requester-generated. |
| `resource_id` | `tstr` | Opaque resource identifier (e.g., `invoice:4f2a`). |
| `lease_class` | `tstr` | Must be in granted `cp_leases`. |
| `requested_duration_s` | `u32` | Default 30. |
| `requester_node_id` | `bstr` (16) | Echoed from HELLO. |

#### `LEASE_GRANT`

| Field | Type | Notes |
|---|---|---|
| `lease_id` | `bstr` (16) | Matches request. |
| `granted_duration_s` | `u32` | May be ≤ requested. |
| `granted_at` | `u64` | Unix epoch seconds. |
| `grantor_node_id` | `bstr` (16) | This peer's node_id. |

#### `LEASE_DENIED`

| Field | Type | Notes |
|---|---|---|
| `lease_id` | `bstr` (16) | Matches request. |
| `reason` | `tstr` | `QUORUM_UNAVAILABLE`, `LEASE_HELD_BY_OTHER`, `CLASS_NOT_GRANTED`, `EXPIRED_ATTESTATION`. |
| `held_by` | `bstr` (16, optional) | node_id of current holder if `LEASE_HELD_BY_OTHER`. |

#### `LEASE_RELEASE`

| Field | Type | Notes |
|---|---|---|
| `lease_id` | `bstr` (16) | The lease being released. |
| `released_at` | `u64` | Unix epoch seconds. |

### 3.6 `GOSSIP_PING`

Every `tick_interval_s` (default 30 s, paper §6.1).

| Field | Type | Notes |
|---|---|---|
| `vector_clock` | `map<bstr, u64>` | `node_id → highest op_sequence observed`. |
| `membership_delta` | `MembershipDelta` | Added / removed peers since last tick. |
| `monotonic_nonce` | `u64` | Replay protection (§8). |

Where `MembershipDelta`:

| Field | Type | Notes |
|---|---|---|
| `added` | `array<bstr>` | node_ids newly seen. |
| `removed` | `array<bstr>` | node_ids presumed gone (no gossip for N ticks). |

### 3.7 `ERROR`

Sent at any time; typically terminates the session if `recoverable=false`.

| Field | Type | Notes |
|---|---|---|
| `code` | `tstr` | See enumeration in §5 and §9. |
| `message` | `tstr` | Human-readable, for logs. Do not parse. |
| `recoverable` | `bool` | If `true`, session may continue; if `false`, sender closes immediately. |

---

## 4. Handshake Sequence

```
Node A                                   Node B
  │                                        │
  │  HELLO                                 │
  │  ─────────────────────────────────────→│
  │                                        │
  │                                HELLO   │
  │ ←──────────────────────────────────────│
  │                                        │
  │  CAPABILITY_NEG                        │
  │ ──────────────────────────────────────→│
  │                                        │
  │                                  ACK   │
  │ ←──────────────────────────────────────│
  │                                        │
  │  DELTA_STREAM (continuous, bidir)      │
  │ ←─────────────────────────────────────→│
  │                                        │
  │  LEASE_* (on demand)                   │
  │ ←─────────────────────────────────────→│
  │                                        │
  │  GOSSIP_PING (every tick_interval_s)   │
  │ ←─────────────────────────────────────→│
```

**Failure modes during handshake:**

- **Schema-version mismatch** — responder sends `ERROR { code: "SCHEMA_VERSION_INCOMPATIBLE", recoverable: false }` if initiator's `schema_version` is below the responder's minimum and no overlap exists in `supported_versions`. Session closes.
- **Bad HELLO signature** — receiver sends `ERROR { code: "HELLO_SIGNATURE_INVALID", recoverable: false }`.
- **Stale HELLO timestamp** (outside ±30 s) — `ERROR { code: "HELLO_TIMESTAMP_STALE", recoverable: false }`.
- **Attestation rejected** — NOT an error; `ACK` lists the rejected subscription in `rejected` with a reason code. Session continues for any accepted subscriptions.
- **Socket close during handshake** — reconnect with exponential backoff per §7.

---

## 5. Capability Negotiation — Subscription Filtering

The data-minimization invariant from paper §6.2 and §11.2 Layer 3 is codified here:

> **A node receives CRDT operations and events only for streams it is subscribed to. Subscription eligibility is determined by role attestation during capability negotiation. A node that lacks the required attestation never receives the operations — receiving and hiding is not a security control.**

The daemon enforces this at the socket level. Unsubscribed streams are not written to the wire; the unsubscribed peer learns of their existence only via whatever metadata the application chooses to publish, which is out of band to this protocol.

### Rejection reason codes

| Code | Meaning |
|---|---|
| `MISSING_ATTESTATION` | Bundle required for this subscription was not included. |
| `EXPIRED_ATTESTATION` | Bundle timestamps place it outside the validity window. |
| `INVALID_SIGNATURE` | Bundle failed cryptographic verification. |
| `UNSUPPORTED_STREAM` | Stream identifier unknown on this node. |
| `QUOTA_EXCEEDED` | Peer has reached per-session subscription cap. |
| `POLICY_BLOCKED` | Organization-level policy forbids this subscription (e.g., BYOD segregation). |

Rejections are final for the session. A peer may reconnect with updated attestations.

---

## 6. Flease Lease Coordination (CP-Class Writes)

Implements paper §6.3. This spec covers the wire format; see the Xtreemfs Flease paper for the full algorithm proof.

**Algorithm sketch.** A node wishing to acquire a lease:

1. Sends `LEASE_REQUEST` to all reachable peers whose attestations include the lease class.
2. Collects `LEASE_GRANT` responses. A grant is valid if `granted_at + granted_duration_s > now`.
3. Lease acquired when **quorum** of grants received within request timeout (default 5 s).
4. Holder broadcasts `LEASE_RELEASE` when done, or lets the lease expire.

**Defaults.**

- Lease duration: **30 s**.
- Quorum: **ceil(N/2) + 1** where N is the number of peers that attest the lease class.
- Request timeout: 5 s.
- Renewal: holder may re-request within `granted_duration_s / 2` remaining to extend.

**Failure modes.**

- **Quorum unreachable** → each peer that cannot confirm quorum itself replies `LEASE_DENIED { reason: "QUORUM_UNAVAILABLE" }`; requester blocks the write and surfaces staleness per paper §13.2.
- **Holder goes offline** → lease expires at `granted_at + granted_duration_s`; any peer may grant a new lease after expiry.
- **Explicit release** → holder sends `LEASE_RELEASE` to all peers; peers immediately free the resource for re-grant.
- **Split-brain (two concurrent grants)** → detected by sequence comparison at reconvergence; the lower `granted_at` wins, the losing write is quarantined per paper §11.2 Layer 4.

---

## 7. Error Handling and Reconnection

**Exponential backoff schedule for reconnects** after any unplanned socket close:

| Attempt | Delay |
|---|---|
| 1 | 100 ms |
| 2 | 500 ms |
| 3 | 2 s |
| 4 | 10 s |
| 5+ | 60 s (cap) |

Jitter ±20 % is applied to each delay to avoid thundering-herd on a peer that recovers simultaneously for many nodes.

**Resume semantics after reconnection.**

1. New HELLO / CAPABILITY_NEG / ACK handshake runs from scratch — session state is not resumed wholesale.
2. `DELTA_STREAM` **resumes from last-acknowledged `op_sequence` per stream**. The capability ACK on the fresh handshake includes a `resume_from` map `{ stream_id → last_delivered_op_sequence }` as an optional extension field; peers use it to skip already-delivered ops.
3. `GOSSIP_PING` cadence restarts at handshake completion (not at the wall-clock tick).
4. Lease state is **not** inherited — a reconnected peer must re-negotiate any in-flight lease.

---

## 8. Security Considerations

### Transport authentication

Authentication happens at the application layer via Ed25519 `hello_sig` in HELLO. TLS is **not** used on the wire for the common case because:

- On the local IPC socket, kernel-enforced process isolation and filesystem permissions provide isolation.
- On the peer-to-peer wire, deployments use a mesh VPN (WireGuard — paper §6.1) that handles transport-layer encryption.
- Direct internet-exposed daemons are a deployment-level decision; operators who choose that path wrap the socket in TLS. This is out of scope for the protocol spec.

### Replay protection

- **HELLO** includes `sent_at` (Unix epoch seconds). Receiver rejects if outside ±30 s of its own clock. The `hello_sig` covers this field; tampering invalidates the signature.
- **GOSSIP_PING** includes `monotonic_nonce` that must strictly increase for a given sender within a session. A PING with a non-monotonic nonce is dropped (not fatal — recoverable ERROR with code `NONCE_REPLAY`).
- **LEASE_REQUEST** / **LEASE_GRANT** include `lease_id` UUIDs that are one-shot; reuse is rejected.

### Rate limiting

- A daemon rejects a peer that exceeds its advertised `max_deltas_per_sec` (default 1000) by sending `ERROR { code: "RATE_LIMIT_EXCEEDED", recoverable: true }` and temporarily muting DELTA_STREAM from that peer for 5 s.
- Configurable per peer-class in daemon config.

### Field-level encryption

Field-level encryption (paper §11.2 Layer 2) is transparent to the sync daemon. Ciphertext rides in `crdt_ops` as opaque bytes; decryption happens in the application with per-role keys whose distribution is a separate flow (paper §11.3).

---

## 9. Versioning

- `schema_version` in HELLO is the protocol version (semver). Minor and patch versions are backwards-compatible within a major.
- `supported_versions` lists every major this daemon can fall back to.
- **Negotiation:** responder picks the highest common major and replies in HELLO with that version; the session uses it.
- **Incompatibility:** if no common major exists, responder sends `ERROR { code: "SCHEMA_VERSION_INCOMPATIBLE", recoverable: false, message: "upgrade to ≥N.0" }`. Session closes; the application surfaces upgrade guidance to the user per paper §7.4.
- **Epoch bump** (breaking change) mirrors paper §7's schema-epoch approach — a new major version is announced, deployed to a critical mass, and older clients are cut off.

Error code `SCHEMA_VERSION_INCOMPATIBLE` is the signal that drives the couch-device recovery path (paper §15.2).

---

## 10. Reference Test Vectors

The following CBOR byte sequences (diagnostic notation per RFC 8949 §8, followed by hex encoding) let an implementer verify their serializer. Signatures in HELLO are stubbed as 64 zero bytes for readability; real implementations must produce valid Ed25519 signatures. Node IDs use recognizable patterns for debugging, not real UUIDs.

### 10.1 HELLO

Diagnostic:
```
{
  "type": "HELLO",
  "body": {
    "node_id":            h'00112233445566778899aabbccddeeff',
    "schema_version":     "1.0.0",
    "supported_versions": ["1.0.0"],
    "public_key":         h'...32 bytes...',
    "sent_at":            1745280000,
    "hello_sig":          h'...64 bytes (stub zeros)...'
  }
}
```

Hex (with stub keys/sigs):
```
a26474797065654845 4c4c4f64626f6479a6
676e6f64655f6964 5000112233445566778899aabbccddeeff
6e736368656d615f76657273696f6e 65312e302e30
72737570706f727465645f76657273696f6e73 8165312e302e30
6a7075626c69635f6b6579 5820 0000000000000000000000000000000000000000000000000000000000000000
6773656e745f6174 1a6804b180
6968656c6c6f5f736967 5840 00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
```

### 10.2 CAPABILITY_NEG

Diagnostic:
```
{
  "type": "CAPABILITY_NEG",
  "body": {
    "crdt_streams":         ["team_core", "financial_records"],
    "cp_leases":            ["invoice_post", "resource_reserve"],
    "bucket_subscriptions": [
      { "bucket_name": "team_core",         "attestation_bundle": h'a1...' },
      { "bucket_name": "financial_records", "attestation_bundle": h'a1...' }
    ]
  }
}
```

Hex (with stub attestation blobs):
```
a26474797065 6e4341504142494c4954595f4e454744626f6479a3
6c637264745f73747265616d73 82 6974 65616d5f636f7265 71 66696e616e6369616c5f7265636f726473
69637069 5f6c6561736573 82 6c696e766f6963655f706f7374 70 7265736f757263655f72657365727665
756275636b65745f737562736372697074696f6e73 82
  a2 6b6275636b65745f6e616d65 69 7465616d5f636f7265 72 61747465737461 74696f6e5f62756e646c65 41a1
  a2 6b6275636b65745f6e616d65 71 66696e616e6369616c5f7265636f726473 72 61747465737461 74696f6e5f62756e646c65 41a1
```

### 10.3 ACK

Diagnostic:
```
{
  "type": "ACK",
  "body": {
    "granted_subscriptions": ["team_core"],
    "rejected": [
      { "subscription": "financial_records", "reason": "MISSING_ATTESTATION" }
    ],
    "tick_interval_s":    30,
    "max_deltas_per_sec": 1000
  }
}
```

Hex:
```
a26474797065 63414b4664626f6479 a4
776772616e7465645f737562736372697074696f6e73 81 69 7465616d5f636f7265
68726 56a6563746564 81 a2 6c73756273637269 7074696f6e 71 66696e616e6369616c5f7265636f726473 66 726561 736f6e 74 4d495353494e475f4154544553544154494f4e
70 7469636b5f696e74657276616c5f73 181e
726d61785f64656c7461735f7065725f736563 1903e8
```

### 10.4 LEASE_GRANT

Diagnostic:
```
{
  "type": "LEASE_GRANT",
  "body": {
    "lease_id":            h'fedcba98765432100123456789abcdef',
    "granted_duration_s":  30,
    "granted_at":          1745280045,
    "grantor_node_id":     h'00112233445566778899aabbccddeeff'
  }
}
```

Hex:
```
a26474797065 6b4c454153455f4752414e5464626f6479a4
686c656173655f6964 50fedcba98765432100123456789abcdef
726772616e7465645f6475726174696f6e5f73 181e
6a6772616e7465645f6174 1a6804b1ad
6f6772616e746f725f6e6f64655f6964 5000112233445566778899aabbccddeeff
```

> Implementers should round-trip these through their CBOR library and compare. Any divergence in map-key ordering is acceptable only if the library emits deterministic encoding (RFC 8949 §4.2); this protocol does not require deterministic CBOR but recommends it for reproducible test vectors.

---

## 11. Open Questions / TODOs

1. **CBOR library selection.** In the .NET ecosystem, `PeterO.Cbor` is the common mature choice; `System.Formats.Cbor` is BCL-native but more primitive. Decide by Wave 1.2 when CRDT engine integration lands.
2. **Quorum sizing for teams of two.** Paper §2.3 notes that teams smaller than quorum require either a managed relay as an additional quorum participant or a config downgrade to AP-with-conflict-detection. The downgrade semantics need their own short spec; this doc assumes N ≥ 3 in the happy path.
3. **Transport multiplexing.** Open question: one socket per peer (simple, one session per connection) versus one socket total with multiplexed stream IDs (fewer file descriptors, more protocol surface area). Lean: one socket per peer for v1, revisit if fd pressure becomes real.
4. **Reuse of `federation-common` envelope signing.** ADR 0029 (Wave 0.5 sibling) will decide whether every frame carries a `SyncEnvelope`-style signature or whether HELLO establishes a session key that signs only session-level control messages. This spec is written neutrally; the final binding adds one signature field per frame or a per-session key-exchange message.
5. **Gossip fan-out tuning.** Paper §6.1 says "two random peers" per tick. This spec does not mandate fan-out — it's a daemon-internal policy knob. Revisit after Wave 2 load tests.
6. **Compression.** CBOR is compact but not compressed. Whether to add per-frame zstd is deferred; measure first on realistic CRDT op volumes.

---

*End of specification. Review gate: ratification by BDFL unblocks Wave 1 kernel work per paper-alignment-plan.md.*

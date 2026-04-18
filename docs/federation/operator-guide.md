# Sunfish Federation — Operator Guide

Platform Phase D ships the federation primitives that let two or more Sunfish nodes in different jurisdictions reconcile their entity state and capability graph, with every operation signed and every transport observable. This guide covers the four packages shipped in Phase D's first wave plus the infrastructure work deferred to follow-up phases.

## Packages shipped (Phase D first wave)

| Package | Purpose | Spec |
|---|---|---|
| `Sunfish.Federation.Common` | Envelope, signed transport abstraction, peer registry, startup checks | §2.5, §10.4 |
| `Sunfish.Federation.EntitySync` | Automerge-style entity delta sync (head announcement + change exchange) | §2.5 |
| `Sunfish.Federation.EntitySync.Http` (in the same assembly as EntitySync) | ASP.NET Core endpoint + HttpClient for server-to-server sync | §2.5 |
| `Sunfish.Federation.CapabilitySync` | RIBLT-based capability-graph set reconciliation with full-set fallback | §10.2, Keyhive reference |

See the commits on `feat/platform-phase-D-federation` (`e9b5d31` → `a2cda39`) for the full changeset.

## Core concepts

### Signed envelopes, double-signed operations

Every federation message is a `SyncEnvelope` (Sunfish.Federation.Common):

```
┌──────────────────────────────────────────────┐
│ SyncEnvelope                                  │
│   Id (SyncMessageId)                          │
│   FromPeer (PeerId)                           │
│   ToPeer (PeerId)                             │
│   Kind (SyncMessageKind)                      │
│   SentAt (DateTimeOffset)                     │
│   Nonce (Nonce)                               │
│   Payload (ReadOnlyMemory<byte>)              │
│   Signature (Signature)  ← Ed25519 over above │
└──────────────────────────────────────────────┘
```

The envelope's `Signature` covers the header fields + payload. When the payload itself is an already-signed `SignedOperation<T>` from Phase B (e.g., a signed capability op), you get **double-signing**:

- Payload signature proves the operation is authentic (peer B can't forge a change claiming peer A authored it).
- Envelope signature proves the transport hop is authentic (a middleman can't replay a message claiming to be from A).

Canonical-JSON coverage for both signatures uses the PrincipalId + Signature JsonConverters added in commit `a2cda39` — every principal and every nested signature field contributes its bytes to the signable envelope.

### Peer identity = Ed25519 public key

`PeerId.From(PrincipalId)` constructs a peer identifier from an Ed25519 public key rendered as base64url. Receivers derive the expected signer via `PrincipalId.FromBase64Url(envelope.FromPeer.Value)` and verify the envelope signature with Phase B's `Ed25519Verifier`.

### Transport abstraction

`ISyncTransport` has two methods:

- `SendAsync(PeerDescriptor target, SyncEnvelope envelope, CancellationToken)` — push an envelope outbound, receive a response envelope.
- `RegisterHandler(PeerId local, Func<SyncEnvelope, ValueTask<SyncEnvelope>> handler)` — attach a handler for envelopes targeting `local`.

The companion `ILocalHandlerDispatcher.DispatchAsync(SyncEnvelope)` lets host listeners (ASP.NET Core endpoint) route incoming HTTP-delivered envelopes to the locally-registered handler.

Two transports ship:

- `InMemorySyncTransport` — for tests; handlers registered at the same transport instance talk directly.
- `HttpSyncTransport` — POSTs JSON to `{peer.Endpoint}/.well-known/sunfish/federation/entity-sync` and reads the reply. Uses `IHttpClientFactory` with a named client (`sunfish-federation`) behind a singleton transport so inbound handler registrations stay consistent.

## Entity sync flow

Two peers (Alice, Bob) with divergent histories converge in two round trips:

```
Alice                                    Bob
  │                                        │
  │  (1) Pull from Bob                     │
  │───── EntityHeadsAnnouncement ─────────►│
  │        payload = {Scope, MyHeads}      │
  │                                        │
  │◄───── EntityHeadsAnnouncement ─────────│  Bob replies with his heads
  │        payload = {Scope, BobHeads}     │
  │                                        │
  │  (2) Request missing changes           │
  │───── EntityChangesRequest ────────────►│
  │        payload = {Scope,               │
  │          MyHeads, WantedHeads}         │
  │                                        │
  │◄───── EntityChangesResponse ───────────│
  │        payload = [SignedChangeRecordDto...]
  │                                        │
  │   (3) Verify each signature,           │
  │       apply to local ChangeStore       │
```

Each `SignedChangeRecord` is a `SignedOperation<ChangeRecord>` with `ChangeRecord(EntityId, VersionId, VersionId? Parent, Timestamp, byte[] Diff)`. Federation treats the `Diff` as opaque — consumers interpret it (JSON Patch, Automerge binary, etc.).

`InMemoryEntitySyncer.ApplyReceivedChanges` rejects any change whose signature verification fails; rejections land in `SyncResult.Rejections`.

## Capability sync flow

`Sunfish.Federation.CapabilitySync` reconciles the set of `SignedOperation<CapabilityOp>` (Mint / Delegate / Revoke / AddMember / RemoveMember) between peers using a RIBLT (Rateless IBLT) probe-guarded encoder/decoder:

```
Alice                                       Bob
  │                                           │
  │ Alice encodes her op set as N symbols     │
  │ (density schedule = 1 + idx/4)            │
  │───── CapabilityRibltBatch (16 syms) ─────►│
  │                                           │
  │      Bob's decoder subtracts his local    │
  │      items, peels count-1 symbols         │
  │                                           │
  │      If Success → diff known, fetch       │
  │        Alice's RemoteOnly ops             │
  │      If NeedMoreSymbols → request         │
  │        batch 1, batch 2 (up to 3)         │
  │      If Inconsistent or budget exhausted  │
  │        → full-set fallback                │
  │                                           │
  │◄────── fetch SignedOperation<CapabilityOp>│
  │        by nonce (verified on receive)     │
```

Every received op is Ed25519-verified against its embedded `IssuerId` before insertion into `IChangeStore`'s capability equivalent (`ICapabilityOpStore`). Revocation semantics are preserved by delivering both the Add and Revoke ops to each peer; downstream `ICapabilityGraph.QueryAsync(..., asOf)` applies them in order per Phase B's closure algorithm.

## Startup checks (production federation)

`FederationStartupChecks` is a hosted service that runs at application startup. In production (`FederationOptions.Environment = Production`):

- **Missing swarm key → fatal.** Requires `Sunfish:Federation:SwarmKeyPath` to be set to a 32-byte hex-encoded IPFS swarm key file. See `docs/federation/kubo-sidecar-dependency.md` for key generation.
- **Kubo health probe not registered → warning.** Phase D-5 (IpfsBlobStore) will register `IKuboHealthProbe`. Until that lands, startup logs a warning and proceeds — acceptable for early federation deployments that don't yet need blob replication.
- **Kubo reports public network profile → fatal.** Once the probe is wired, the daemon MUST be running in private-network mode (swarm-key-gated). Startup refuses to continue if Kubo reports `NetworkProfile = "public"`.

Dev / staging environments bypass these checks entirely; startup logs an informational notice.

## Wiring

```csharp
var builder = WebApplication.CreateBuilder();

// Phase A + B + B-blobs already wired via AddSunfish() + AddSunfishDecentralization()
builder.Services.AddSunfish()
    .AddSunfishDecentralization(o => { o.EnableDevKeyMaterial = true; });

// Phase D federation
builder.Services.AddSunfishFederation(o =>
{
    o.Environment = FederationEnvironment.Development;
    o.LocalPeerRegion = "us-west-2";
});
builder.Services.AddSunfishEntitySync();         // InMemoryChangeStore + InMemoryEntitySyncer
builder.Services.AddSunfishEntitySyncHttp();     // HttpSyncTransport (replaces InMemorySyncTransport)
builder.Services.AddSunfishCapabilitySync();     // InMemoryCapabilityOpStore

var app = builder.Build();
app.MapEntitySyncEndpoints();                    // POST /.well-known/sunfish/federation/entity-sync
app.Run();
```

Production consumers plug their own `IOperationSigner` (KMS / HSM / OS-keyring) before wiring the syncer — never register the dev-only `Ed25519Signer` in production.

## Canonical deployment patterns (spec §10.4)

All three patterns are implementable with the Phase-D-first-wave stack; full worked-example tests are deferred to the follow-up tasks listed below.

### Pattern A — Two PM companies + municipal code enforcement

Two property-management companies push inspection entities to a city code-enforcement agency. The city node holds read-only permissions; the PM nodes retain write capability via their locally-controlled capability graph. All three peers run independent Sunfish nodes; the PM → city push is a one-way entity sync followed by a capability-sync delegate that grants the city read-only on each inspection entity's resource URI.

### Pattern B — Base command with air-gapped child bases

A central military command syncs with air-gapped child bases via intermittent sneakernet. The sync protocol works identically — envelopes are serialized to a USB drive, carried across the air gap, and replayed at the other end. Signature verification, nonce deduplication, and CRDT merge all hold across the gap.

### Pattern C — Contractor portal with Macaroon-bound access

A PM company mints a short-lived Macaroon (`Sunfish.Foundation.Macaroons`) for a contractor and federates it to a portal node. The contractor presents the macaroon, the portal node validates it against its `IRootKeyStore`, and the Phase B `IPermissionEvaluator` evaluates the policy model against the macaroon's caveats.

## Deferred to follow-up (require infrastructure)

The following tasks from the Phase D plan were not shipped in this wave because they require Docker / Podman to run Kubo and IPFS-Cluster sidecars locally, and that infrastructure wasn't available in the session that authored Phase D:

| Task | Scope | Blocker |
|---|---|---|
| **D-5: IpfsBlobStore (Kubo RPC)** | `Sunfish.Federation.BlobReplication.IpfsBlobStore` talking to Kubo's `/api/v0/*` HTTP RPC surface | Needs a running Kubo daemon (via Testcontainers) for integration tests |
| **D-6: IPFS-Cluster integration** | Raft-consensus pinning at replication factor 3; 24-hour signed attestation producer | Needs IPFS-Cluster sidecar alongside Kubo |
| **D-7: Pattern A worked example** | Full PM + city end-to-end scenario exercising entity sync + capability sync + blob replication | Blob replication leg requires D-5 |
| **D-8: Pattern B worked example** | Base command + air-gapped child bases with sneakernet packaging | Blob replication leg requires D-5 |
| **D-9: Pattern C worked example** | Contractor portal + macaroon validation + capability eval | Principally code-only; may ship in a follow-up without requiring infra — tracked separately |

### How to finish Phase D when infrastructure is available

1. Stand up Kubo via `ipfs/kubo:v0.28.0` container (or local install). Generate a swarm key.
2. Implement `Sunfish.Federation.BlobReplication` per the Task D-5 plan in `docs/superpowers/plans/2026-04-18-platform-phase-D-federation.md`. Use Testcontainers' `DockerImageName.Parse("ipfs/kubo:v0.28.0")` to spin up per-test daemons.
3. Wire `IKuboHealthProbe` implementation so `FederationStartupChecks` can verify private-network mode in production.
4. Add IPFS-Cluster container + Raft pinning for D-6.
5. Port the three canonical patterns (A/B/C) as integration tests exercising the full stack end-to-end.

## Parking lot

Longer-term items that are explicit non-goals for Phase D:

- **libp2p transport** — spec §3.6 names libp2p pubsub as a federation transport; HTTP+TLS is the Phase D first pass.
- **BeeKEM group key agreement** — Keyhive's continuous group key agreement for confidentiality between federation peers.
- **Large-file streaming blob-write path** — current Phase C buffers images to memory before `IBlobStore.PutAsync(ReadOnlyMemory<byte>)`. Multi-GB federated blobs need a `PutStreamingAsync(Stream)` on IBlobStore.
- **Cross-jurisdiction policy overlay** — city-level policy that supersedes PM-level policy when a conflict occurs; needs a policy-layer diff/merge operator not yet specified.
- **RIBLT test vectors vs reference JS Keyhive** — capability-sync's RIBLT implementation passes round-trip tests against its own encoder/decoder; a conformance pass against the Ink & Switch JavaScript reference is parked.

# Platform Phase D — Parking Lot

Items scoped out of Platform Phase D's first-wave shipment (tasks D-0 through D-4 + D-10). Each entry names the future package / contract, the gating dependency, and the phase that will deliver it.

## 1. IpfsBlobStore (Task D-5) — infrastructure-gated

- **Package:** `Sunfish.Federation.BlobReplication` (future)
- **Shape:** `IpfsBlobStore : IBlobStore` talking to Kubo's `/api/v0/*` HTTP RPC surface via `HttpClient`. `IKuboHealthProbe` implementation lands here so `FederationStartupChecks` can verify private-network mode in production.
- **Why deferred:** Integration tests require a running Kubo daemon (`ipfs/kubo:v0.28.0`) via Testcontainers. Docker / Podman was unavailable in the session that implemented Phase D's first wave.
- **Reference:** `docs/specifications/research-notes/ipfs-evaluation.md` §3 (concerns), §4 (composition with Keyhive), and `docs/federation/kubo-sidecar-dependency.md`.

## 2. IPFS-Cluster integration (Task D-6) — infrastructure-gated

- **Package:** `Sunfish.Federation.BlobReplication.Cluster` (future, or folded into #1)
- **Shape:** Pinning via IPFS-Cluster's Raft-consensus API at replication factor 3 by default. 24-hour signed attestation producer ("I have CIDs A, B, C as of T") per spec §10.4.
- **Why deferred:** Needs both Kubo and IPFS-Cluster containers side-by-side; same infrastructure blocker as #1.

## 3. Pattern A worked example (Task D-7) — depends on #1+#2

- Full end-to-end scenario: two PM companies push inspection entities + attached blobs (drone imagery) to a municipal code-enforcement agency; all three peers run independent Sunfish nodes; federation routes via signed envelopes over HTTP.
- **Why deferred:** Blob-replication leg requires #1 and #2. Entity-sync + capability-sync legs are individually shippable but the value is in the composed scenario.

## 4. Pattern B worked example (Task D-8) — depends on #1

- Base command + air-gapped child bases via sneakernet. Envelopes serialized to USB, carried across the air gap, replayed at the other end. Signature verification + nonce deduplication + CRDT merge all hold across the gap.
- **Why deferred:** Same blob-replication dependency.

## 5. Pattern C worked example (Task D-9) — mostly code-ready

- Contractor portal with macaroon-bound access. PM mints a short-lived `Sunfish.Foundation.Macaroons.Macaroon` for a contractor; federates it to a portal node; portal validates against its `IRootKeyStore` + `IPermissionEvaluator`.
- **Why deferred:** Principally code-only — could ship alongside D-5/D-6 in a single follow-up wave, or separately as "Phase D-patterns-c-only" without blob infrastructure.

## 6. libp2p transport

- **Shape:** Replace `HttpSyncTransport` with a libp2p-based pubsub transport per spec §3.6.
- **Why deferred:** HTTP + TLS is the Phase D first pass; libp2p is a much larger integration surface and not required for most operator deployments.

## 7. BeeKEM group key agreement

- **Shape:** Continuous group key agreement for confidentiality between federation peers. Encrypted transport-layer envelope that only current group members can decrypt.
- **Why deferred:** Keyhive's BeeKEM is research-grade; HTTPS + Ed25519 signing is sufficient for Phase D's authenticity guarantees. Confidentiality is a separate future phase.

## 8. Streaming blob-write path

- **Contract:** `IBlobStore.PutStreamingAsync(Stream, CancellationToken)` on `Sunfish.Foundation.Blobs.IBlobStore`.
- **Why deferred:** Multi-GB federated blobs (drone tiles, satellite scenes, sensor archive files) would need streaming to avoid in-memory buffering. Also flagged as a Phase C parking-lot item in `2026-04-18-platform-phase-C-parking-lot.md`.

## 9. Cross-jurisdiction policy overlay

- **Shape:** City-level policy that supersedes PM-level policy when a conflict occurs; needs a policy-layer diff/merge operator not yet specified.
- **Why deferred:** The semantic shape of cross-jurisdiction policy composition isn't nailed down in the spec. Pattern A implementation will surface the concrete requirements.

## 10. RIBLT conformance vs reference JS Keyhive

- **Shape:** Test vectors from the Ink & Switch Keyhive JavaScript reference implementation to verify Sunfish's RIBLT encoder/decoder is bit-compatible.
- **Why deferred:** Sunfish's RIBLT passes round-trip tests against its own encoder/decoder and delivers convergence across two peers. Cross-implementation conformance is a later concern.

## 11. Persistent nonce replay-protection store

- **Shape:** On-disk / database-backed nonce tracker so a restart doesn't reset the replay window.
- **Why deferred:** Phase B's in-memory nonce tracking is sufficient for single-process + test scenarios. Phase D's HTTP transport uses fresh nonces per envelope so the window is effectively per-connection; persistent tracking is needed when federation runs continuously across process restarts.

---

*Canonical tracker for Phase D deferrals. When infrastructure is available and a follow-up phase ships an item, update that entry here and remove it.*

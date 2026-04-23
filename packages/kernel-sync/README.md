# Sunfish.Kernel.Sync

Intra-team gossip anti-entropy daemon for the Sunfish local-node architecture.
Wave 2.1 of the paper-alignment plan. Paper §6.1 (Gossip Anti-Entropy), §6.2
(Sync Daemon Protocol). [ADR 0029](../../docs/adrs/0029-federation-reconciliation.md)
(Accepted) sets the dual-track boundary between this package (intra-team
gossip) and the `federation-*` stack (cross-organization relay sync).

## Status

**Provisional — Wave 2.1 end-to-end shape.** The message codec, transport
abstraction, gossip scheduler, and handshake ladder are wired and tested
through the in-memory transport. The Unix-socket / named-pipe substrate is
operational but not yet exercised by the round-loop harness — the Wave 2.1
test suite runs over `InMemorySyncDaemonTransport` to keep the CI matrix
single-platform.

Deferred to later waves (per ADR 0029 and the paper-alignment plan):

- **Wave 2.2** — mDNS + WireGuard peer discovery (this package receives a
  pre-resolved peer endpoint; discovery is upstream).
- **Wave 2.3** — Flease lease coordination (the wire messages are speced and
  codec-tested here; the algorithm lives in a sibling package).
- **Wave 2.4** — Bucket-eligibility evaluation against
  `IAttestationVerifier`; today the handshake's `policy` callback is
  "grant everything".
- **Wave 2.5** — Round-loop wiring into `ICrdtDocument` for actual delta
  application on receipt.

## What's here

| Path | Role |
|---|---|
| `Protocol/Messages.cs` | CBOR message records for every wire type (HELLO, CAPABILITY_NEG, ACK, DELTA_STREAM, LEASE_*, GOSSIP_PING, ERROR). Each has `ToCbor()` / `FromCbor()`. |
| `Protocol/ISyncDaemonTransport.cs` | Connect / Listen abstraction over the substrate. |
| `Protocol/InMemorySyncDaemonTransport.cs` | Process-local channel pair for tests. |
| `Protocol/UnixSocketSyncDaemonTransport.cs` | Unix-domain-socket on POSIX; named pipe on Windows. Length-prefixed framing per spec §2.2. |
| `Gossip/VectorClock.cs` | Lamport-style clock with dominates/concurrent/merge ops. |
| `Gossip/IGossipDaemon.cs` | Daemon contract. |
| `Gossip/GossipDaemon.cs` | `PeriodicTimer`-driven round scheduler. |
| `Gossip/GossipDaemonOptions.cs` | `IOptions` knobs — round interval, peer pick count, connect timeout, dead-peer backoff. |
| `Handshake/HandshakeProtocol.cs` | `InitiateAsync` / `RespondAsync` ladder per spec §4. |
| `DependencyInjection/ServiceCollectionExtensions.cs` | `AddSunfishKernelSync()`. |
| `tests/` | xUnit harness for the above. |

## Using it

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Kernel.Sync.Gossip;
using Sunfish.Kernel.Sync.DependencyInjection;

var services = new ServiceCollection();
services.AddSunfishKernelSync(opts =>
{
    opts.RoundIntervalSeconds = 30;
    opts.PeerPickCount = 2;
});

var provider = services.BuildServiceProvider();
var daemon = provider.GetRequiredService<IGossipDaemon>();

daemon.AddPeer("/run/sunfish/peer-a.sock", peerAPublicKey);
daemon.AddPeer("/run/sunfish/peer-b.sock", peerBPublicKey);

await daemon.StartAsync(CancellationToken.None);
```

## Why this is *not* `federation-*`

Per [ADR 0029](../../docs/adrs/0029-federation-reconciliation.md):

- **`kernel-sync`** (this package) — intra-team gossip. Leaderless,
  role-attestation-driven, Unix-socket / named-pipe daemon, 30-second tick,
  random peer selection. Maps onto paper §6.1's tier-1 (mDNS) and
  tier-2 (WireGuard VPN) discovery tiers.
- **`federation-*`** (sibling packages) — inter-organization sync. Enumerated
  peers, per-envelope signed, RIBLT set-reconciliation, IPFS-backed
  content-addressed blob replication. Maps onto paper §6.1's tier-3
  (managed relay) and paper §2.4 tier-4 (content-addressed distribution).

The two stacks are intentionally distinct; the `sync-daemon-protocol` spec
is authoritative for this package, and `federation-*`'s `SyncEnvelope` is
authoritative for the other. Cross-tests in Wave 2.5 verify the boundary
does not leak.

## References

- Paper §6.1 — Gossip anti-entropy
- Paper §6.2 — Sync daemon protocol
- Paper §11.3 — Role attestation vs. key distribution (bucket-eligibility feeder)
- [ADR 0029](../../docs/adrs/0029-federation-reconciliation.md) — Dual-track decision
- [Sync Daemon Protocol Specification](../../docs/specifications/sync-daemon-protocol.md) — Wire-format contract
- [Wave 2.1 deliverable](../../_shared/product/paper-alignment-plan.md#wave-2---phase-2-sync-daemon-in-process)

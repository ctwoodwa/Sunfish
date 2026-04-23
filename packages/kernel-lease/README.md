# Sunfish.Kernel.Lease

Flease-inspired distributed lease coordinator for Sunfish CP-class record writes.

## References

- **Paper [§6.3 Distributed Lease Coordination (CP Mode)](../../_shared/product/local-node-architecture-paper.md)**
  (authoritative motivation)
- **[sync-daemon-protocol §6](../../docs/specifications/sync-daemon-protocol.md)**
  (wire-format contract, algorithm sketch, failure modes)
- **[paper-alignment-plan Wave 2.3](../../_shared/product/paper-alignment-plan.md)**
  (roadmap slot this package fills)

## What it does

CP-class records require a distributed lease before the owning node is allowed
to write. The coordinator rides the `LEASE_REQUEST` / `LEASE_GRANT` /
`LEASE_RELEASE` / `LEASE_DENIED` messages defined in `Sunfish.Kernel.Sync`
over the shared `ISyncDaemonTransport`.

- **Acquire** sends a `LEASE_REQUEST` to every known peer. A lease is granted
  when `ceil(N/2)+1` of them reply `LEASE_GRANT` within the proposal timeout
  (default 5 s). Timeout or majority-denied returns `null` — the caller must
  block the CP-class write and surface staleness per paper §13.2.
- **Responder** loop listens for inbound `LEASE_REQUEST` frames and answers
  from a local conflict cache. If no unexpired lease exists on the same
  resource, it grants; otherwise it denies with reason `CONFLICT` (plus
  `held_by` when known).
- **Release** broadcasts `LEASE_RELEASE` to every peer that participated in
  the grant. Idempotent — safe to call twice, safe to call after the lease
  has already expired.
- **Expiry** is monotonic: a lease past `ExpiresAt` is dead whether or not a
  release message arrived. The background pruner reclaims memory on a
  configurable cadence (default 10 s).

## Happy path

```csharp
services
    .AddSunfishKernelSync()        // registers transport + gossip
    .AddSunfishKernelLease(
        localNodeId: identity.NodeIdHex,
        localListenEndpoint: "/var/run/sunfish.sock");

// ...

var lease = await coordinator.AcquireAsync("order:1234", TimeSpan.FromSeconds(30), ct);
if (lease is null)
{
    // Quorum unreachable — paper §6.3 mandates we block the write.
    throw new InvalidOperationException("CP write blocked: no quorum.");
}

try
{
    await repository.WriteAsync(...);
}
finally
{
    await coordinator.ReleaseAsync(lease, ct);
}
```

## Quorum-unreachable path

When fewer than `ceil(N/2)+1` peers respond within `ProposalTimeout`,
`AcquireAsync` returns `null`. This is the fail-closed state required by
the paper. For teams smaller than quorum, the paper suggests either a
managed relay as an additional quorum participant or a configuration
downgrade to AP mode; both decisions are surfaced to the operator at
install time and are **out of scope** for this package.

## What is NOT shipped here (yet)

- Wire-level Flease **proposal numbers** in the grant-denial messaging
  (sync-daemon-protocol §6 is silent on whether they are required for the
  Wave-2 cut; we track a local monotonic counter for logging and
  correlation, but the wire frames carry only `lease_id`).
- Integration with the schema-registry's `EpochCoordinator` — follow-up
  after this package lands.
- TLS/attestation on the proposal path — the coordinator trusts the
  transport for authentication. Paper §6.1's mesh-VPN deployment covers
  that layer.

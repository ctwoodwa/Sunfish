# Kubo Sidecar Dependency

Sunfish federation's blob replication surface (`Sunfish.Federation.BlobReplication.IpfsBlobStore`) talks to an [IPFS Kubo](https://github.com/ipfs/kubo) daemon over its HTTP RPC API (`/api/v0/*`). Kubo is a Go binary — **not** a .NET library — and runs as a sidecar container next to each Sunfish federation node.

## Version

- **Tested against:** `ipfs/kubo:v0.28.0`
- **Pin via:** container image tag or host-installed binary.
- **Newer versions:** expected to work given the stable `/api/v0/*` surface. Re-run the federation integration tests before promoting.

## Default port

Kubo exposes its RPC API on `localhost:5001`. Sunfish's `IpfsBlobStore` config defaults to that endpoint; override via the `IpfsBlobStoreOptions.RpcEndpoint` option if running Kubo on a different port or host.

## Production deployment shape

```
┌────────────────────────────┐    ┌────────────────────────────┐
│ Sunfish node (.NET 10)     │    │ Kubo daemon (Go)           │
│ packages/foundation/       │ ⇄  │ ipfs/kubo:v0.28.0          │
│ packages/federation/       │    │ :5001 (RPC) / :4001 (swarm)│
│ IpfsBlobStore RPC client   │    │ Content-addressed storage  │
└────────────────────────────┘    └────────────────────────────┘
                                              ⇄
                                   ┌────────────────────────────┐
                                   │ IPFS-Cluster (Go)          │
                                   │ Raft consensus + pinning   │
                                   │ replication-factor=3       │
                                   └────────────────────────────┘
```

## Why not a .NET IPFS library?

Per `docs/specifications/research-notes/ipfs-evaluation.md` §3.2, the .NET IPFS ecosystem (`Ipfs.Http.Client`, `Ipfs.Engine`) is thin: either an unofficial HTTP-RPC wrapper with sparse maintenance or an in-process Go-port. Kubo is the reference implementation; the HTTP RPC surface is stable; a hand-rolled `HttpClient` call against `/api/v0/*` is a few hundred lines and avoids pinning to a volatile third-party NuGet.

## Private network

Federation deployments MUST run Kubo with a **swarm key** (`~/.ipfs/swarm.key`) so nodes only gossip with peers they share the key with. Public-DHT participation is forbidden for enterprise deployments — see `ipfs-evaluation.md` §3.4 (public DHT leakage concern). Phase D's `AddSunfishFederation` fails fast at startup if `IpfsBlobStoreOptions.RequirePrivateNetwork = true` and the configured Kubo daemon has public DHT enabled.

## Local development

For local dev or tests, the integration fixture spins up a Kubo container via Testcontainers. See `packages/federation-tests/` for the `KuboFixture` pattern.

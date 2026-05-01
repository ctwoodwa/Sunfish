# Headscale operator guide (Tier 2 mesh-VPN)

Operator setup for `Sunfish.Providers.Mesh.Headscale` — the first Tier-2 mesh-VPN adapter in the `providers-mesh-*` family per [ADR 0061](../../../docs/adrs/0061-three-tier-peer-transport.md) + ADR 0013 provider neutrality.

## When to use Headscale

Tier 2 mesh-VPN is for peer pairs that share a logical network but not a physical LAN — e.g., a developer's laptop reaching a home-server Anchor while travelling, or two property-management offices sharing inspection records. The mesh-VPN tunnel makes the remote peer reachable as if on Tier 1 without exposing it to the open internet.

Use Headscale specifically when:

- You want a self-hosted control plane (no SaaS dependency on Tailscale Inc.).
- BSD-3 licensing posture matters to you (SSPL/BSL excluded per the W#30 hand-off charter).
- You're comfortable running a small Go binary + Postgres/SQLite for the control-plane DB.

If a self-hosted control plane is too heavy, Tailscale (SaaS, BSL above 100 nodes) or NetBird (BSD-3, hosted or self-hosted) are reasonable alternatives. Both ship as separate `providers-mesh-*` packages (deferred follow-ups).

## Prereqs

- A reachable host for the Headscale control plane (HTTPS endpoint, valid TLS cert).
- An API key with permission to list + register nodes (`GET /api/v1/node` + `POST /api/v1/node/register`).
- Each Sunfish peer host must have WireGuard available (kernel module on Linux ≥ 5.6; `wireguard-go` userspace on older kernels / macOS / Windows).

## Wiring

```csharp
using Sunfish.Foundation.Transport.DependencyInjection;
using Sunfish.Providers.Mesh.Headscale;

services.AddHttpClient<HeadscaleClient>();
services.AddSingleton(new HeadscaleOptions
{
    BaseUrl = new Uri("https://headscale.your-org.example/"),
    ApiKey = Environment.GetEnvironmentVariable("HEADSCALE_API_KEY")!,
    User = "sunfish-prod", // optional; default user used if null
});
services.AddSingleton<IPeerTransport>(sp => new HeadscaleMeshAdapter(
    sp.GetRequiredService<HeadscaleClient>(),
    sp.GetRequiredService<HeadscaleOptions>()));

services.AddBridgeRelay(new BridgeRelayOptions
{
    RelayUrl = new Uri("wss://relay.bridge.example.com/sync"),
});

services.AddSunfishTransport(); // selector picks up Headscale + Bridge from IEnumerable<IPeerTransport>
```

For audit-enabled production hosts:

```csharp
services.AddSingleton<IPeerTransport>(sp => new HeadscaleMeshAdapter(
    sp.GetRequiredService<HeadscaleClient>(),
    sp.GetRequiredService<HeadscaleOptions>(),
    sp.GetRequiredService<IAuditTrail>(),
    sp.GetRequiredService<IOperationSigner>(),
    currentTenantId));
services.AddSunfishTransport(currentTenantId);
```

## Sunfish PeerId mapping

The adapter encodes the Sunfish `PeerId` as a Headscale ACL tag:

```
tag:sunfish-peer-{base64url-encoded-peer-id}
```

`HeadscaleMeshAdapter.RegisterDeviceAsync` writes this tag automatically; `ResolvePeerAsync` and `GetMeshStatusAsync` filter on it. The Headscale-issued device id (numeric string) is independent — a single Sunfish peer can rotate its Headscale device record without changing its `PeerId` (per ADR 0061 A1's two-field `(DeviceId, PeerId)` shape).

## Operational notes

- **Health-check cache.** `HeadscaleMeshAdapter.IsAvailable` polls `GET /health` and caches the result for `HeadscaleOptions.AvailabilityCacheDuration` (default 5s). The selector calls `IsAvailable` before every Tier-2 attempt; without the cache, every selection round would hit the control plane.
- **Per-request timeout.** `HeadscaleOptions.RequestTimeout` (default 3s) sits well inside the Tier-2 5s budget per ADR 0061 A4 — leaves headroom for retries inside the budget.
- **WireGuard data plane.** This adapter only manages the control plane; the host OS is responsible for bringing up the WireGuard interface. On Linux: `wg-quick up wg0` after the device is registered. On macOS / Windows: the Tailscale client (which works with Headscale) is one option; `wireguard-go` is another.
- **Mesh-up-but-peer-not-registered.** When `ResolvePeerAsync` returns null, the selector falls through cleanly — no thrown exception, no audit-trail noise beyond `MeshTransportFailed` for the specific adapter that missed.
- **Two-adapter migration.** When Headscale and Tailscale are both registered (operator running both during a migration window), the deterministic registration-order priority + lexicographic tie-break per ADR 0061 §"Tier selection algorithm" resolves any disagreement. Operators set the source-of-truth adapter by registering it first in DI.

## License posture (canonical)

Headscale: BSD-3, [github.com/juanfont/headscale](https://github.com/juanfont/headscale). No SSPL / BSL deps. Consistent with the W#30 hand-off charter.

The `Sunfish.Providers.Mesh.Headscale` adapter itself uses `HttpClient` directly — no `Headscale-Sharp` / `Tailscale.Net` SDK dependency, mirroring the `providers-recaptcha` precedent.

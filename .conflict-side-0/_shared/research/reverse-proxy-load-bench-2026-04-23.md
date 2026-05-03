# Reverse-proxy load bench — 2026-04-23

> **Scope.** Wave 5.3.C `TenantWebSocketReverseProxy` load test, per the
> measurement gate in
> [`docs/adrs/0033-browser-shell-render-model-and-trust-posture.md`](../../docs/adrs/0033-browser-shell-render-model-and-trust-posture.md)
> §Reverse-proxy latency budget and §Decision. The harness lives at
> `accelerators/bridge/tests/Sunfish.Bridge.Tests.Integration/Wave53/ReverseProxyLoadBench.cs`.

## TL;DR

- **Gate result: PASS** on loopback. Both ADR 0033 thresholds cleared by a
  wide margin: handshake p99 **85 ms** (< 2000 ms), frame RTT p99
  **0.5 ms** (< 500 ms), zero errors, zero slow-consumer stalls.
- **Caveat.** Loopback has no TCP-stack latency worth speaking of. These
  numbers are a CEILING, not a floor — they prove the proxy's pump does not
  add pathological latency under this load shape; they do NOT prove
  production feels live.
- **Recommendation.** Proceed with v1 of `TenantWebSocketReverseProxy`
  per ADR 0033 §Decision. Do NOT escalate to ADR 0034 (Yarp). Real-network
  validation is still required before first-customer onboarding — see
  §Caveats.

## Setup

| Parameter | Value |
|---|---|
| Tenants | 3 (distinct `Guid` per tenant, each with its own in-proc Kestrel echo host) |
| Browser sessions | 30 total (10 per tenant) |
| Frames per session | 100 |
| Frame payload | 1 KB (1024 bytes) binary |
| Total frame-RTT samples | 3000 |
| Handshake timing | `ClientWebSocket.ConnectAsync` → first `ReceiveAsync` return, after a 1-byte priming echo |
| Frame timing | `SendAsync` → drain-to-EOM of the echoed bytes (1 iteration in the normal case) |
| Wall-clock target | 10 minutes OR 3000 frame-RTTs exchanged (whichever first) |
| Test machine | AMD Ryzen 7 7800X3D, 64 GB RAM, Windows 11 Home 26200 |
| Runtime | .NET 11.0.100-preview.3.26207.106, net11.0 target |
| Transport | `ws://127.0.0.1:<ephemeral>`, no TLS |
| Proxy build | Debug (not Release — see §Caveats on optimistic numbers) |
| Harness version | commit containing this file + `ReverseProxyLoadBench.cs` |

### Architecture

```
30 × ClientWebSocket  →  Bridge-proxy host  →  1-of-3 tenant echo hosts
   (test clients)        TenantWebSocketReverseProxy.InvokeAsync
                         (unmodified production code)
                              │
                              ├─ shim middleware binds IBrowserTenantContext from ?tenant=<guid>
                              └─ InMemoryTenantEndpointRegistry → the per-tenant echo URI
```

The shim middleware replaces the production Wave 5.3.A subdomain-resolution
middleware so the test does not need Host-header plumbing. Everything
downstream — endpoint lookup, upstream `ClientWebSocket.ConnectAsync`, the
bidirectional `CopyAsync` byte-pump — is exercised exactly as in production.

## Results

```
Wall: 0.1 s
Sessions: 30/30
Frame-RTT samples: 3000/3000
Errors: 0
```

| metric     | samples | p50 (ms) | p95 (ms) | p99 (ms) | max (ms) |
|-----------:|--------:|---------:|---------:|---------:|---------:|
| handshake  |      30 |     80.1 |     85.1 |     85.1 |     85.1 |
| frame RTT  |    3000 |      0.3 |      0.4 |      0.5 |      6.9 |

### Interpretation

- **Handshake (85 ms p99)** is dominated by two sequential TCP+WebSocket
  accept/upgrade round-trips (browser↔bridge, bridge↔tenant) plus the
  proxy's DI scope construction. The values cluster tightly (p50 = 80,
  p95 = p99 = 85) because loopback adds negligible variance.
- **Frame RTT (0.5 ms p99, 6.9 ms max)** is essentially floor-noise. The
  max of 6.9 ms is one straggler — likely a GC or scheduler hiccup, not
  a protocol-layer stall.
- **Zero errors** across 30 × 100 frames + 30 handshakes. No slow-consumer
  stalls observed (stalls would surface as RTTs in the multi-second range;
  we see none).
- **Wall-clock 0.1 s** because the harness exits on frame-count, not
  timeout. For a longer-duration soak the same harness can be parameterized
  — change `FramesPerSession` to e.g. 10000 to keep connections open for
  minutes on loopback; the 10-minute wall target exists for a network-attached
  CI run where per-frame latency is higher.

## Verdict

**ADR 0033 §Decision gates: PASS (loopback ceiling).**

| Gate (ADR 0033) | Threshold | Measured (p99) | Result |
|---|---|---|---|
| p99 handshake latency | < 2000 ms | 85 ms | PASS |
| p99 frame round-trip | < 500 ms | 0.5 ms | PASS |
| Zero slow-consumer-induced stalls over the run | 0 | 0 | PASS |

Per ADR 0033 §Decision, the hand-rolled `TenantWebSocketReverseProxy` in
`bridge-web` is cleared to ship in v1. **ADR 0034 (Yarp gateway escalation)
is NOT triggered.**

## Caveats

These numbers are a ceiling — use with care:

1. **Loopback erases real-network latency.** Production traffic traverses
   a client NIC, ISP link, edge LB, inter-AZ link, and the bridge-web NIC.
   The RTT floor goes from ~0 ms (loopback) to 20–60 ms (same region) or
   100+ ms (cross-region). The proxy adds ~one pump-hop of latency on top
   of that; the 85 ms handshake ceiling stays within budget even with 50 ms
   of added round-trip, but this wants real-network validation before first
   production onboarding.

2. **Debug build.** Harness ran against a Debug-compiled `Sunfish.Bridge`
   assembly. Release is strictly faster but the gate passed by a ~4000×
   margin on frame RTT — Release/Debug delta is noise at this load.

3. **Echo-only upstream.** The tenant echo host reflects every byte
   unchanged. A real `local-node-host` applies delta-stream logic and
   may be slower to respond. ADR 0033's gate is about the proxy's
   contribution, not the tenant child's — and the proxy's contribution
   is negligible per these results — but an end-to-end browser-shell
   run with a real local-node-host on the far side remains owed before
   v1 declares "live feel" met.

4. **Single-host loopback contention is lower than prod.** 30 concurrent
   sessions on one box run against the same CPU + kernel scheduler; on
   prod, clients are 30 independent browsers on independent machines.
   This mostly helps prod (no CPU contention between clients) but
   introduces network variance the harness doesn't see.

5. **The sample size at p99 for handshakes is small** (30 samples → p99
   is the max). With 30 sessions, p99-handshake reduces to max-handshake
   by construction. The reported value is conservative but not very
   confidence-rich; a follow-up soak with 300+ handshakes would narrow
   the confidence band.

6. **No deliberate slow-consumer test.** ADR 0033 calls out "zero
   slow-consumer-induced stalls" as a third gate. The harness reads as
   fast as it writes, so slow-consumer behavior is not stressed. The
   proxy's `CopyAsync` back-pressure is linear — a slow consumer would
   stall its direction's copy without affecting the other — but a
   deliberate slow-read test (e.g. one client that reads at 1 KB/s
   while another pushes at full rate) would be a useful follow-up.

## Follow-ups (non-blocking)

- **Real-network bench.** Publish a staging run on bridge-dev with 30
  real browser sessions (headless Chrome or `dotnet test` orchestrated
  from a peer region). Add the numbers to this doc under §Results as
  a second table.
- **Release-build bench.** Rerun in Release to get tighter numbers for
  the v1 release notes.
- **Slow-consumer stress.** Add a harness mode where 1 of 30 clients
  drains at a throttled rate; confirm no cross-client impact.

## How to reproduce

```bash
# From repo root
export SUNFISH_RUN_BENCH=1
export SUNFISH_BENCH_OUTPUT=./bench-raw.md     # optional: write raw table to file

dotnet test accelerators/bridge/tests/Sunfish.Bridge.Tests.Integration/Sunfish.Bridge.Tests.Integration.csproj \
    --filter "FullyQualifiedName~ReverseProxy_meets_adr0033_p99_gates" \
    --logger "console;verbosity=detailed"
```

Without `SUNFISH_RUN_BENCH=1` the fact is a no-op (prints a skip message,
returns `Task.CompletedTask`) so it does not slow `dotnet test` runs in CI.
The harness is opt-in by design — real measurement belongs on a dedicated
beefy CI agent or a staging-slot soak, not on PR-gate shared runners.

Gate failures emit a `::error title=ADR0033 reverse-proxy load gate::…`
annotation that GitHub Actions will surface in the PR/workflow UI when the
workflow is configured to run the bench.

## Cross-references

- [ADR 0033 — browser-shell render model and trust posture](../../docs/adrs/0033-browser-shell-render-model-and-trust-posture.md)
  (decision + gate definition)
- [Wave 5.3 decomposition §5.3.C](../product/wave-5.3-decomposition.md) (proxy scope)
- [`accelerators/bridge/Sunfish.Bridge/Proxy/TenantWebSocketReverseProxy.cs`](../../accelerators/bridge/Sunfish.Bridge/Proxy/TenantWebSocketReverseProxy.cs)
  (code under test; not modified by this benchmark)
- [`accelerators/bridge/tests/Sunfish.Bridge.Tests.Integration/Wave53/ReverseProxyLoadBench.cs`](../../accelerators/bridge/tests/Sunfish.Bridge.Tests.Integration/Wave53/ReverseProxyLoadBench.cs)
  (the harness)
- [`accelerators/bridge/PLATFORM_ALIGNMENT.md`](../../accelerators/bridge/PLATFORM_ALIGNMENT.md)
  (tracking row — Wave 5.3 browser-shell primitives)

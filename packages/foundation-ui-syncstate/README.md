# Sunfish.Foundation.UI.SyncState

Foundation-tier public enum exposing [ADR 0036](../../docs/adrs/0036-syncstate-multimodal-encoding-contract.md)'s canonical 5-state sync-state encoding (`healthy / stale / offline / conflict / quarantine`) — the typed surface that downstream substrate ADRs (e.g., ADR 0063's `SyncStateSpec.AcceptableStates`) consume.

Per ADR 0036-A1: the existing string-form encoding contract becomes a typed C# enum without breaking string-form consumers (additive).

## API

```csharp
using Sunfish.Foundation.UI;

var state = SyncState.Healthy;
var wire  = state.ToCanonicalIdentifier();           // "healthy"

if (SyncStateExtensions.TryFromCanonicalIdentifier("offline", out var parsed))
{
    // parsed == SyncState.Offline
}
```

## Wire form

`JsonStringEnumConverter` with `JsonNamingPolicy.CamelCase` produces the lowercase canonical strings — single-word identifiers (`Healthy` / `Stale` / `Offline` / `Conflict` / `Quarantine`) flat-case identically to the canonical wire form. Round-trips through `Sunfish.Foundation.Crypto.CanonicalJson.Serialize`.

## Related

- [ADR 0036](../../docs/adrs/0036-syncstate-multimodal-encoding-contract.md) — the encoding contract.
- ADR 0036-A1 — the public-enum amendment this package ships.
- W#34 / W#35 cohort — established the per-package canonical-JSON + `JsonStringEnumConverter` pattern.

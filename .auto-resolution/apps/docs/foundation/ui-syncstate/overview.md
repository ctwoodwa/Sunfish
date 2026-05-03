# Foundation.UI.SyncState

Public enum exposing [ADR 0036](../../../docs/adrs/0036-syncstate-multimodal-encoding-contract.md)'s canonical 5-state sync-state encoding as a typed C# surface — per ADR 0036 amendment A1.

## Why

ADR 0036 defines the wire-format encoding contract for sync-state — five canonical lowercase identifiers (`healthy`, `stale`, `offline`, `conflict`, `quarantine`) that ARIA roles + visibility tables read. Downstream substrate ADRs (specifically ADR 0063's `SyncStateSpec.AcceptableStates`) want a typed enum in their type signatures rather than a string. A1 ratifies that exposure.

Backward-compat preserved: existing string-form consumers continue to work. The enum is additive.

## Surface

```csharp
namespace Sunfish.Foundation.UI;

public enum SyncState
{
    Healthy,    // canonical "healthy"
    Stale,      // canonical "stale"
    Offline,    // canonical "offline"
    Conflict,   // canonical "conflict"
    Quarantine, // canonical "quarantine"
}

public static class SyncStateExtensions
{
    public static string ToCanonicalIdentifier(this SyncState state);
    public static bool TryFromCanonicalIdentifier(string? identifier, out SyncState state);
}
```

## Wire form

Pair the enum with `JsonStringEnumConverter` configured for `JsonNamingPolicy.CamelCase`. Single-word identifiers (`Healthy` / `Stale` / `Offline` / `Conflict` / `Quarantine`) flat-case identically to the canonical lowercase wire form. Round-trips through `Sunfish.Foundation.Crypto.CanonicalJson.Serialize`.

```csharp
var options = new JsonSerializerOptions
{
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
};
JsonSerializer.Serialize(SyncState.Conflict, options); // "conflict"
```

## Drift detection

`TryFromCanonicalIdentifier` parses the lowercase canonical form only — `"Healthy"` (PascalCase), `"HEALTHY"` (ALLCAPS), and mixed-case all return `false`. This is deliberate: external consumers writing the wire form must match the canonical contract, and any drift surfaces here rather than silently round-tripping wrong.

## Cohort context

W#34 (Foundation.Versioning) + W#35 (Foundation.Migration) established the `JsonStringEnumConverter` + `CanonicalJson.Serialize` pattern for substrate enums; W#37 reuses it for the smaller UI-tier surface. The pattern: PascalCase enum literals + camelCase property names + lowercase wire-form for enum values.

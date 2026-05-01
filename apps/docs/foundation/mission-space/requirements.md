# MinimumSpec — install-time UX (ADR 0063)

W#41 extends `Sunfish.Foundation.MissionSpace` with the install-UX layer per [ADR 0063](../../../docs/adrs/0063-mission-space-requirements.md) + A1. Bundle authors declare a `MinimumSpec`; the substrate evaluates it against the host's runtime `MissionEnvelope` and produces a `SystemRequirementsResult` that drives a per-adapter UX surface (Steam-style System Requirements page, post-install regression banner, etc.).

This page complements [Mission Space overview](overview.md) — the W#40 substrate ships the `MissionEnvelope` + 10-dimension probe surface that this layer consumes.

## What it gives you

| Type | Role |
|---|---|
| `MinimumSpec` | Bundle-author spec — `Policy` (`Required` / `Recommended` / `Informational`) + 10 per-dimension specs + optional `PerPlatformSpec[]` overrides + `JsonExtensionData` forward-compat catch-all per A1.5/A1.6. |
| `PerPlatformSpec` | Per A1.7 COMPOSE rule — for-each-dimension override-replaces-baseline (NOT wholesale REPLACE). |
| 10 per-dimension records | `HardwareSpec` (bytes-canonical per A1.6) · `UserSpec` · `RegulatorySpec` · `RuntimeSpec` · `FormFactorSpec` (consumes W#35 `FormFactorKind`) · `EditionSpec` · `NetworkSpec` (consumes W#30 `TransportTier`) · `TrustSpec` · `SyncStateSpec` (consumes W#37 `SyncState`) · `VersionVectorSpec`. |
| `IMinimumSpecResolver` / `DefaultMinimumSpecResolver` | Pure-function evaluation; cost class Medium per A1.6; 30s cache TTL; `InvalidateCache()` per A1.7. |
| `SystemRequirementsResult` + `DimensionEvaluation` + `OperatorRecoveryAction` | Resolver output — `Overall` verdict + per-dimension `Pass`/`Fail`/`Unevaluated` outcomes + optional operator recovery hint per A1.4. |
| `IInstallForceEnableSurface` / `DefaultInstallForceEnableSurface` | Operator-only override per A1.11 (justification text required); audit shape mirrors W#40's `FeatureForceEnabled`. |
| `ISystemRequirementsRenderer` / `ISystemRequirementsSurface` | Per-adapter UX surface (interface only; concrete renderers in W#42+ per-adapter Stage 06). |

## Verdict semantics (A1.8)

| Verdict | Trigger |
|---|---|
| `Pass` | All Required dimensions pass (or no Required dimensions are declared). |
| `WarnOnly` | No Required dimension fails AND at least one Recommended dimension fails. |
| `Block` | Any Required dimension fails. |

**Per A1.8 explicit Informational rule:** `Informational` dimensions are surfaced in the `Dimensions` list for visibility but **never gate the verdict**. A bundle that declares an Informational `Hardware` spec with `MinMemoryBytes = 32 GB` and runs on a 16 GB host produces a `Pass` overall verdict with a single `Informational` `Fail` entry.

## Per-platform COMPOSE rule (A1.7)

Per-platform overrides **compose** with the baseline, per dimension:

```json
{
  "policy": "Required",
  "hardware": { "minMemoryBytes": 17179869184, "minCpuLogicalCores": 8 },
  "perPlatform": [
    { "platform": "ios", "trust": { "requiresIdentityKey": true } },
    { "platform": "android", "hardware": { "minMemoryBytes": 4294967296 } }
  ]
}
```

Evaluating this spec on `platform: "ios"` produces a merged spec with **baseline Hardware + iOS Trust** (the iOS override doesn't erase Hardware — it adds Trust). Evaluating on `platform: "android"` produces **android-overridden Hardware (4 GB) + no Trust** (the override replaces the baseline Hardware value for that platform). Evaluating on `platform: null` or an unknown platform produces the baseline only.

## Operator force-install (A1.11)

When the resolver returns `Block`, an operator with the appropriate role can override via `IInstallForceEnableSurface.RequestAsync`:

```csharp
var record = await forceEnable.RequestAsync(new InstallForceRequest
{
    OperatorPrincipalId = "ops-admin",
    Reason = "Override per ticket OPS-123: hardware refresh in flight; user authorized 2026-05-01.",
    OverrideTargets = new[] { DimensionChangeKind.Hardware },
    EnvelopeHash = envelope.EnvelopeHash,
    Platform = "ios",
});
```

Substrate-level invariants per A1.11:

- `OperatorPrincipalId` MUST be non-empty (auth-middleware-checked role is host's responsibility).
- `Reason` MUST be non-empty (justification text required by council fix).
- `OverrideTargets` MUST contain ≥ 1 dimension.

The audit-enabled overload emits `InstallForceEnabled` with shape parity to W#40's `FeatureForceEnabled` (operator_principal_id + reason + override_targets) per Phase 3 halt-condition #5.

## Audit emission

5 new `AuditEventType` discriminators in `Sunfish.Kernel.Audit`:

| Event type | Emitted by | Dedup |
|---|---|---|
| `MinimumSpecEvaluated` | `IMinimumSpecResolver.EvaluateAsync` | 5 min, keyed on `(spec, envelopeHash, platform)` |
| `InstallBlocked` | Resolver when verdict = `Block` | per-attempt |
| `InstallWarned` | Resolver when verdict = `WarnOnly` | per-attempt |
| `PostInstallSpecRegression` | Resolver on `Pass` → `Fail` transition (drift across snapshots) | 24 h, keyed on `(dimension, platform)` |
| `InstallForceEnabled` | `IInstallForceEnableSurface.RequestAsync` | per-attempt |

Audit emission is opt-in: pass `IAuditTrail` + `IOperationSigner` + `TenantId` via the audit-enabled DI overload (W#32 both-or-neither). Without them, the substrate runs but no records fire.

## Forward-compat catch-all (A1.5 + A1.6)

`MinimumSpec.UnknownFields` is a `JsonExtensionData` dictionary that captures any properties the deserialiser doesn't recognize. This is the A1.5 / A1.6 forward-compat verification gate (option ii) — bundles authored against a future schema epoch round-trip cleanly through this version's deserialiser without losing data:

```csharp
const string forwardJson = """
    { "policy": "Required", "experimental": { "flagX": true } }
    """;
var spec = JsonSerializer.Deserialize<MinimumSpec>(forwardJson);
// spec.UnknownFields["experimental"] survives the round-trip.
```

## API at a glance

```csharp
// Bootstrap (audit-disabled — test/bootstrap). Wires probes + force-enable
// surfaces from W#40 + the W#41 IMinimumSpecResolver + IInstallForceEnableSurface.
services.AddInMemoryMissionSpace();

// Bootstrap (audit-enabled — production; both IAuditTrail + IOperationSigner
// must already be registered, per the W#32 both-or-neither pattern).
services.AddInMemoryMissionSpace(currentTenantId);

// Evaluate at install time:
var spec = JsonSerializer.Deserialize<MinimumSpec>(bundleJson);
var envelope = await provider.GetCurrentAsync();
var resolver = sp.GetRequiredService<IMinimumSpecResolver>();
var result = await resolver.EvaluateAsync(spec!, envelope, platform: "windows-desktop");

if (result.Overall == OverallVerdict.Block)
{
    // Show the renderer (per-adapter UX) or accept an operator force-enable.
}
```

## Phase 1 scope (this package — closed by W#41)

- `MinimumSpec` schema + 10 per-dimension spec records + 5 enums.
- `IMinimumSpecResolver` + `DefaultMinimumSpecResolver` with COMPOSE rule + cache.
- `IInstallForceEnableSurface` + `DefaultInstallForceEnableSurface` per A1.11.
- `ISystemRequirementsRenderer` + `ISystemRequirementsSurface` interfaces (concrete renderers deferred to per-adapter Stage 06).
- 5 new `AuditEventType` discriminators + `MissionSpaceAuditPayloads` builders.
- `AddInMemoryMissionSpace()` DI extension augmented with W#41 surfaces.

## Out of Phase 1 scope

- **Concrete renderers** — W#42+ per-adapter Stage 06 hand-offs ship the actual UX (Anchor MAUI Razor / Bridge React / iOS SwiftUI).
- **Bundle authoring** — bundle authors define `MinimumSpec` JSON in their bundle manifest (already wired into `BusinessCaseBundleManifest.Requirements` per W#38).

## W#38 stub deprecation note

The temporary stub at `Sunfish.Foundation.Catalog.Bundles.MinimumSpec` (introduced by W#38 PR #460 to unblock the catalog field landing) is **scheduled for removal 2026-08-01**. New code MUST use `Sunfish.Foundation.MissionSpace.MinimumSpec` directly. The stub remains for the transition window so the W#38 test surface (71/71 tests) doesn't break; the follow-up removal PR will retype `BusinessCaseBundleManifest.Requirements` to the canonical type and delete the stub file.

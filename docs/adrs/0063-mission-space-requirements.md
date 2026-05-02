---
id: 63
title: Mission Space Requirements (install-UX layer)
status: Proposed
date: 2026-04-30
tier: foundation
concern:
  - capability-model
  - mission-space
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0063 — Mission Space Requirements (install-UX layer)

**Status:** Proposed (2026-04-30 — auto-merge intentionally DISABLED until Stage 1.5 council reviewed per cohort discipline; see §"Cohort discipline" at end)
**Date:** 2026-04-30
**Author:** XO (research session)
**Pipeline variant:** `sunfish-feature-change` (introduces new public-facing UX contract; no breaking change to existing surface)

**Resolves:** [`icm/00_intake/output/2026-04-30_mission-space-requirements-intake.md`](../../icm/00_intake/output/2026-04-30_mission-space-requirements-intake.md); fourth item in W#33 Mission Space Matrix follow-on authoring queue per [`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`](../../icm/01_discovery/output/2026-04-30_mission-space-matrix.md) §7.2.

---

## Context

Sunfish has no canonical install-time minimum-spec specification. Each feature implementer derives hardware/environment gates ad hoc from paper §4 ("16GB RAM, 8-core CPU, 500GB NVMe baseline; 1GB at idle"), ADR 0044 (Windows-only Phase 1), and ADR 0048 (Phase 2 multi-platform target list). The W#33 Mission Space Matrix discovery doc (§5.2) classifies hardware/environment as **Partial** coverage — paper §4 establishes a baseline but does not specify per-feature minimum-spec tables, install-time UX, or runtime probe behavior.

**The gap is user-facing.** Today an operator installs Sunfish, opens Anchor, and discovers feature-by-feature what their device can / cannot do — through trial, error, and sometimes silent failures. There is no Steam-style "System Requirements" page that says, up front, *"this feature requires biometric authentication; your device does not have a fingerprint reader"* — feature gates fire silently at runtime; the user sees gaps without explanations.

**The substrate to fix this exists — almost.** ADR 0062 (Mission Space Negotiation Protocol; landed 2026-04-30 with A1 council fixes) ships:

- `MissionEnvelope` with 10 dimensions
- `IMissionEnvelopeProvider` coordinator
- `IFeatureGate<TFeature>` runtime gate contract (post-A1.2 rename from `ICapabilityGate`)
- 5-value `DegradationKind` taxonomy (Hide / DisableWithExplanation / DisableWithUpsell / ReadOnly / HardFail)
- `ProbeStatus` per-dimension (per A1.10)
- 9 `AuditEventType` constants for probe + verdict telemetry

ADR 0063 sits at the **install-UX layer** *above* ADR 0062. ADR 0062 produces the runtime envelope; ADR 0063 specifies how:

1. Each feature/bundle declares its minimum-spec along the 10 W#33 dimensions (the **`MinimumSpec` schema**)
2. The install-time UX renders the user's current envelope against the per-feature spec (the **System Requirements page** — Steam-style)
3. Operators distinguish install-blocking vs install-warning specs (the **`SpecPolicy` enum**)
4. Post-install hardware/environment changes re-evaluate against existing installations (the **re-evaluation contract** — leverages ADR 0062 + ADR 0028-A8 events)

ADR 0063 does NOT change anything about the runtime layer; it adds an install-time + bundle-manifest layer that consumes ADR 0062's output.

W#33 Mission Space Matrix §6.1 names this ADR's user-visibility framing: *"users meet Sunfish's capability profile at first contact; the install-UX is the first impression."*

---

## A0 cited-symbol audit

Per the cohort-discipline pattern (ADR 0028-A8.11 / A5.9 / A7.13; ADR 0062-A1.14):

**Existing on `origin/main` (verified 2026-04-30):**

- ADR 0062 (Mission Space Negotiation Protocol; post-A1) — landed via PR #406; provides `MissionEnvelope`, `IMissionEnvelopeProvider`, `IFeatureGate<TFeature>`, `MinimumSpecDimension` types ADR 0063 will consume
- ADR 0028 (CRDT Engine Selection; post-A8 + post-A7) — provides `FormFactorProfile` per A8 + `VersionVector` per A7
- ADR 0007 (BusinessCaseBundleManifest) — bundle manifest schema with `requiredModules: string[]`; ADR 0063 extends with `requirements: MinimumSpec`
- ADR 0009 (Edition / IEditionResolver) — provides `EditionCapabilities` consumed by ADR 0062 dimension
- ADR 0036 (sync-state surface) — consumed by ADR 0062's `SyncStateSnapshot` dimension
- ADR 0044 (Phase 1 Windows-only platform scope) — establishes the .NET-MAUI-Windows ground truth ADR 0063's per-platform spec encodes
- ADR 0048 (Phase 2 multi-platform target list) — establishes the Phase 2 target list: macOS Anchor + Linux Anchor + iOS Field-Capture + Android Field-Capture
- ADR 0049 (audit substrate) — telemetry emission target
- Paper §4 (hardware baseline: 16GB RAM, 8-core CPU, 500GB NVMe; 1GB at idle)
- Paper §13.1 + §13.2 (Complexity Hiding Standard + AP/CP visibility tables)
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` — encoding contract
- `Sunfish.Kernel.Audit.AuditEventType` — telemetry surface

**Introduced by ADR 0063:**

- New types in `Sunfish.Foundation.MissionSpace` (extends ADR 0062's package): `MinimumSpec`, `MinimumSpecDimension`, `SpecPolicy` enum, `SystemRequirementsResult`, `IMinimumSpecResolver`, `ISystemRequirementsRenderer`
- 4 new `AuditEventType` constants (per §"Telemetry shape" below): `MinimumSpecEvaluated`, `InstallBlocked`, `InstallWarned`, `PostInstallSpecRegression`
- New `BusinessCaseBundleManifest` field: `requirements: MinimumSpec` (extends ADR 0007 schema; **coordinated ADR 0007 amendment required** — see §"Sibling amendment dependencies")
- New apps/docs surface: `apps/docs/foundation/mission-space-requirements/` walkthrough page + per-feature spec catalog

**Removed by ADR 0063:**

- None — purely additive.

---

## Decision drivers

- **The user is the audience.** ADR 0062 governs runtime substrate; ADR 0063 governs how that substrate is *communicated to the user*. Decisions favor user clarity over substrate elegance where they conflict.
- **Steam's "System Requirements" model is the right shape.** It lists per-feature requirements; it's pre-install (you see it before clicking Install); it shows minimum-spec vs your device clearly; it allows install with warning when your device only meets minimum-spec partially; it matches user mental model.
- **Bundle-manifest is the unit of declaration.** Per ADR 0007, plugins are organized as bundles (`BusinessCaseBundleManifest`). Bundles are the granularity at which install/uninstall happens; spec declarations should match. Per-feature spec declarations within a bundle are deferred to a future amendment if needed.
- **Two-tier blocking: required vs recommended.** A bundle's spec separates *required* (install BLOCKS if the user's device cannot meet it) from *recommended* (install proceeds with WARNING if the user's device cannot meet it). Force-install (analogous to ADR 0062's force-enable surface) is an operator-only override.
- **Dimensions match ADR 0062's 10 envelope dimensions.** A `MinimumSpec` is a structured "filter" on the `MissionEnvelope`: each dimension declared in the spec is checked against the corresponding envelope dimension. Spec authors declare only the dimensions they care about; unspecified dimensions are "any."
- **Re-evaluation reuses ADR 0062 + ADR 0028-A8 events.** Post-install hardware/environment changes are surfaced via the existing `EnvelopeChange` event stream + `HardwareTierChangeEvent` per ADR 0028-A8.3; ADR 0063 doesn't introduce new event types for this. Specifically, a bundle that was install-OK on day 1 may surface a `PostInstallSpecRegression` audit event on day 30 if the device's MissionEnvelope drifts below the bundle's `requirements`.
- **Per-platform variation is data, not branching code.** A bundle's spec may declare per-platform overrides (e.g., "iOS requires `BiometricAuth`; macOS only recommends it"); the renderer evaluates the right override for the current platform.

---

## Considered options

### Option A — `MinimumSpec` schema in bundle manifest + Steam-style System Requirements page [RECOMMENDED]

A `MinimumSpec` schema lives inside `BusinessCaseBundleManifest` (per ADR 0007 extension); a System Requirements UX page renders the spec against the user's `MissionEnvelope` at install time + on-demand post-install ("Why is this feature unavailable?" → click → System Requirements page for the relevant bundle).

- **Pro:** Bundle-manifest is already the unit of install; spec declarations attach naturally
- **Pro:** Steam-style System Requirements is a pattern users already understand (no novel UX)
- **Pro:** ADR 0007 amendment is mechanical (one new field on an existing schema)
- **Pro:** Renderer is per-adapter (Anchor MAUI / Bridge React / iOS) but `MinimumSpec` is platform-agnostic data
- **Con:** Bundle authors must declare `MinimumSpec` for every new bundle (added authoring cost; mitigated by sensible defaults and templates)

**Verdict:** Recommended. Aligns with ADR 0062 substrate + ADR 0007 manifest pattern; matches user mental model.

### Option B — Per-feature `MinimumSpec` declarations (sub-bundle granularity)

Each `IFeature`-tagged feature inside a bundle declares its own spec; the bundle is install-OK iff every feature is install-OK.

- **Pro:** Finer granularity — partial install where some features pass spec, others don't
- **Con:** Substantially more authoring overhead — 5–20 features per bundle each need spec declarations
- **Con:** UX is harder to render — Steam-style page becomes a per-feature accordion instead of a single bundle summary
- **Con:** No clear "install" decision — does a bundle install if 80% of features pass spec? 50%? Threshold becomes another config knob

**Verdict:** Rejected. Bundle granularity is the right tradeoff for v0; per-feature granularity is deferred to a future amendment if real-world UX exposes the gap.

### Option C — No declarative spec; rely on runtime gate verdicts only

Skip the install-time spec layer entirely; users install the bundle and discover unavailable features via runtime `IFeatureGate` verdicts.

- **Pro:** Zero authoring overhead; status quo
- **Con:** Status quo's failure mode (user installs and discovers gaps post-install) is preserved
- **Con:** No pre-install user education; operator support burden unchanged
- **Con:** Defeats the W#33 §6.1 user-visibility framing — Mission Space remains invisible until first runtime contact

**Verdict:** Rejected. ADR 0063's existence is justified by the gap Option C preserves.

### Option D — Hybrid: bundle-level `MinimumSpec` + optional per-feature overrides

Bundle declares its baseline spec; individual features within the bundle MAY declare stricter requirements that the bundle inherits as a "OR" check.

- **Pro:** Balance — bundle authors declare once; feature authors override when feature-specific signals matter
- **Pro:** Backward-compat path from Option A — start with bundle-only specs (Option A); add per-feature overrides if real-world need surfaces
- **Con:** Two-tier reasoning at authoring time (matching ADR 0062's two-tier reasoning around dimension-vs-bespoke probes)

**Verdict:** Recommended **as the actual decision** — Option A pure-bundle is too coarse if a single bundle spans features with substantively different specs; Option D's hybrid handles the future-proofing case. Treated as Option A specialization in §"Decision".

---

## Decision

**Option A specialized via Option D's hybrid.** Bundle manifest declares baseline `MinimumSpec` (Option A); individual `IFeature`-tagged features MAY declare per-feature `MinimumSpec` that overrides the bundle's baseline (Option D). At install time, the bundle's baseline is checked; at runtime, per-feature gates use the per-feature `MinimumSpec` (if declared) for `FeatureVerdict.UserMessage` content.

### Initial contract surface

Located in `Sunfish.Foundation.MissionSpace` (extends ADR 0062's package; same DI extension `AddSunfishMissionSpace()`):

```csharp
namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Declares the minimum capability profile a bundle (or feature within a bundle) requires.
/// Each dimension is optional — unspecified dimensions are "any."
/// </summary>
public sealed record MinimumSpec(
    HardwareSpec?           Hardware,
    UserSpec?               User,
    RegulatorySpec?         Regulatory,
    RuntimeSpec?            Runtime,
    FormFactorSpec?         FormFactor,
    EditionSpec?            Edition,
    NetworkSpec?            Network,
    TrustSpec?              Trust,
    SyncStateSpec?          SyncState,
    VersionVectorSpec?      VersionVector,
    PerPlatformSpec?        PerPlatformOverrides,
    SpecPolicy              Policy
);

/// <summary>
/// Per-platform spec override. Anchor MAUI / Bridge / iOS / Android each may have a stricter or looser spec.
/// The active platform's override is used in preference to the baseline; if no override is declared for the active platform, the baseline applies.
/// </summary>
public sealed record PerPlatformSpec(
    MinimumSpec? AnchorMaui,
    MinimumSpec? BridgeReact,
    MinimumSpec? IosFieldCapture,
    MinimumSpec? AndroidFieldCapture
);

public enum SpecPolicy
{
    Required,        // install BLOCKS if user's MissionEnvelope cannot meet the spec; force-install requires operator override
    Recommended,     // install proceeds with WARNING if user's MissionEnvelope cannot meet the spec; user dismisses warning
    Informational    // install proceeds silently; spec is documentation; surfaces in System Requirements page only
}
```

Per-dimension spec records (each may declare a single-dimension subset of the `MissionEnvelope`'s corresponding dimension shape):

```csharp
public sealed record HardwareSpec(
    int?                    MinMemoryBytesGb,    // e.g., 16 (paper §4 baseline)
    int?                    MinCpuCores,         // e.g., 8 (paper §4 baseline)
    long?                   MinDiskBytes,        // e.g., 500GB
    DisplayClass?           MinDisplayClass,     // Large / Medium / Small / MicroDisplay / NoDisplay
    IReadOnlySet<SensorRequirement>? RequiredSensors  // Camera / Mic / Gps / BiometricAuth / NfcReader / BarcodeScanner
);

public sealed record UserSpec(
    IReadOnlySet<string>?   RequiredRoles,       // e.g., { "owner", "admin" }
    IReadOnlySet<AuthMethod>? RequiredAuthMethods // e.g., { Biometric }
);

public sealed record RegulatorySpec(
    IReadOnlySet<string>?   AllowedJurisdictions,    // e.g., { "US-*", "CA-*" }
    IReadOnlySet<string>?   ProhibitedJurisdictions, // e.g., { "RU-*", "IR-*" }
    IReadOnlySet<string>?   RequiredConsents         // e.g., { "ccpa-v3", "gdpr-v2" }
);

public sealed record RuntimeSpec(
    string?                 MinDotnetVersion,    // e.g., "11.0"
    string?                 MinSwiftVersion,     // e.g., "6.0" (iOS only)
    IReadOnlySet<Platform>? AllowedPlatforms     // e.g., { Windows, Macos } per ADR 0044/0048
);

public sealed record FormFactorSpec(
    IReadOnlySet<FormFactor>?       AllowedFormFactors,         // e.g., { Laptop, Desktop, Tablet }
    NetworkPosture?                 MinNetworkPosture,          // AlwaysConnected / IntermittentConnected / OfflineFirst
    PowerProfile?                   MinPowerProfile             // Wallpower / Battery / LowPower
);

public sealed record EditionSpec(
    IReadOnlySet<string>?   AllowedEditions     // e.g., { "anchor-self-host", "bridge-pro" } per ADR 0009
);

public sealed record NetworkSpec(
    IReadOnlySet<TransportTier>?    RequiredTransports  // e.g., { Tier1Mdns } for offline-first per ADR 0061
);

public sealed record TrustSpec(
    int?                    MinTrustAnchorCount,
    string?                 MinTrustTier        // mirrors EditionSpec; per ADR 0009
);

public sealed record SyncStateSpec(
    IReadOnlySet<SyncState>? AcceptableStates    // e.g., { Synced, Local } — exclude Stale, Quarantined
);

public sealed record VersionVectorSpec(
    string?                 MinKernelVersion,    // e.g., "1.3.0"
    int?                    MinSchemaEpoch       // e.g., 7
);
```

System-requirements evaluation contract:

```csharp
public interface IMinimumSpecResolver
{
    /// <summary>
    /// Evaluates a MinimumSpec against the current MissionEnvelope. Returns a structured result
    /// listing which dimensions pass / fail / are unevaluated.
    /// </summary>
    ValueTask<SystemRequirementsResult> EvaluateAsync(
        MinimumSpec spec,
        MissionEnvelope envelope,
        CancellationToken ct = default
    );
}

public sealed record SystemRequirementsResult(
    OverallVerdict          Overall,             // Pass / WarnOnly / Block
    IReadOnlyList<DimensionEvaluation> Dimensions,
    LocalizedString?        OperatorRecoveryAction  // null if Overall == Pass
);

public enum OverallVerdict
{
    Pass,       // every required dimension meets spec; recommended dimensions all pass too
    WarnOnly,   // every required dimension passes; one or more recommended dimensions fail
    Block       // one or more required dimensions fail
}

public sealed record DimensionEvaluation(
    DimensionChangeKind     Dimension,
    DimensionPolicyKind     PolicyKind,          // Required / Recommended / Informational / Unevaluated
    DimensionPassFail       Result,              // Pass / Fail / Unevaluated
    LocalizedString?        UserMessage,         // why this dimension failed; null if Pass
    LocalizedString?        OperatorRecoveryHint // operator-readable next step; null if Pass
);

public enum DimensionPolicyKind { Required, Recommended, Informational, Unevaluated }
public enum DimensionPassFail   { Pass, Fail, Unevaluated }
```

Per-platform renderer:

```csharp
public interface ISystemRequirementsRenderer
{
    /// <summary>
    /// Renders a System Requirements UX surface for the given evaluation result.
    /// Implementations: AnchorMauiSystemRequirementsRenderer, BridgeReactSystemRequirementsRenderer,
    /// IosFieldCaptureSystemRequirementsRenderer.
    /// </summary>
    ValueTask RenderAsync(
        SystemRequirementsResult result,
        ISystemRequirementsSurface surface,
        CancellationToken ct = default
    );
}

public interface ISystemRequirementsSurface
{
    /// <summary>The platform-specific UI surface (Razor view / React component / SwiftUI view) to render into.</summary>
    object PlatformSurface { get; }

    /// <summary>The render mode — pre-install (full-page) vs post-install (modal/inline).</summary>
    SystemRequirementsRenderMode Mode { get; }
}

public enum SystemRequirementsRenderMode
{
    PreInstallFullPage,             // Steam-style full page before user clicks "Install"
    PostInstallInlineExplanation,   // tooltip / sidebar revealing why a feature is gated
    PostInstallRegressionBanner     // top-of-screen banner when post-install regression detected
}
```

### Bundle manifest extension (coordinated ADR 0007 amendment required)

ADR 0007's `BusinessCaseBundleManifest` gains one new field:

```csharp
public sealed record BusinessCaseBundleManifest(
    // ... existing fields ...
    MinimumSpec? Requirements          // NEW: bundle-level minimum spec; null = "any"
);
```

`null` means the bundle declares no requirements (effectively "runs anywhere ADR 0044/0048 supports"). Phase 1 bundles MAY declare `null`; Phase 2+ bundles SHOULD declare meaningful specs.

### Pre-install UX flow

1. User browses available bundles via Anchor's bundle marketplace (Bridge-hosted; subscription-gated per ADR 0009).
2. User selects a bundle → bundle-detail view loads.
3. **System Requirements page renders inline** within the bundle-detail view (NOT a separate page; matches Steam's pattern). Format:
   - **Required** section header → table of required dimensions; each row colored green (pass) / red (fail).
   - **Recommended** section header → table of recommended dimensions; each row colored green (pass) / yellow (fail).
   - **Informational** section header → table of informational dimensions; not pass/fail-styled.
   - Below the table: **Install** button (enabled if Overall is Pass or WarnOnly; disabled with explanation if Block).
4. If Overall is Block: Install button is disabled; tooltip naming the failed Required dimensions; **operator-only "Force Install" link** in tooltip (operator authentication required).
5. If Overall is WarnOnly: Install button enabled; clicking shows a dismissable warning naming the failed Recommended dimensions; user clicks "Install Anyway" to proceed.
6. If Overall is Pass: Install button enabled; clicking proceeds to install flow (no warning).

### Post-install UX flow

1. **On gate verdict surfaced** (per ADR 0062 `FeatureVerdictSurfaced` audit): if a feature gate's `FeatureVerdict.State == Unavailable`, the gate's `OperatorRecoveryAction` includes a link to "View System Requirements" → opens the bundle's System Requirements page in `PostInstallInlineExplanation` mode.
2. **On post-install regression** (per ADR 0062 `EnvelopeChange` event with `Severity = Warning` or `Critical`): if any installed bundle's `requirements` are no longer met, fire `PostInstallSpecRegression` audit event + surface `PostInstallRegressionBanner` UX. Operator may reconfigure (recover the failed dimension) or uninstall the affected bundle; user-visible banner names the affected bundle and the failed dimensions.

### Per-platform per-bundle spec resolution

When `BusinessCaseBundleManifest.Requirements.PerPlatformOverrides` is set, the active platform's override is used in preference to the baseline. Resolution order:

1. **Active platform override** (e.g., `IosFieldCapture` if running on iOS) — if set, use this `MinimumSpec` directly.
2. **Baseline spec** — if no platform override is set, use the baseline.
3. **No spec** (`Requirements == null`) — bundle is "any" (effectively `OverallVerdict.Pass` always).

Spec authors declare per-platform overrides ONLY when the spec genuinely differs per platform. Common case (iOS requires `BiometricAuth` because no password fallback; macOS recommends it but doesn't require) shows up cleanly:

```csharp
new MinimumSpec(
    Hardware: new HardwareSpec(
        MinMemoryBytesGb: 16,
        // ...
    ),
    Policy: SpecPolicy.Required,
    PerPlatformOverrides: new PerPlatformSpec(
        IosFieldCapture: new MinimumSpec(
            Hardware: new HardwareSpec(
                RequiredSensors: new HashSet<SensorRequirement> { SensorRequirement.BiometricAuth }
            ),
            Policy: SpecPolicy.Required,
            PerPlatformOverrides: null
        ),
        AnchorMaui: null,             // baseline applies
        BridgeReact: null,            // baseline applies
        AndroidFieldCapture: null     // baseline applies
    )
)
```

### Telemetry shape

Four new `AuditEventType` constants (per ADR 0049 emission contract; dedup per ADR 0028-A6.5.1 pattern):

| Event | Trigger | Payload | Dedup window |
|---|---|---|---|
| `MinimumSpecEvaluated` | Each `IMinimumSpecResolver.EvaluateAsync` call | `(bundle_key, overall_verdict, dimension_count, fail_count, latency_ms)` | 5-min per `(bundle_key)` |
| `InstallBlocked` | Install attempted on a bundle whose Overall is Block; user did not force-install | `(bundle_key, failed_required_dimensions)` | None (per-attempt) |
| `InstallWarned` | Install attempted on a bundle whose Overall is WarnOnly; user proceeded with warning | `(bundle_key, failed_recommended_dimensions)` | None (per-attempt) |
| `PostInstallSpecRegression` | Post-install: bundle's spec evaluation transitions Pass/WarnOnly → Block | `(bundle_key, dimensions_that_regressed, time_since_install)` | 24-hour per `(bundle_key)` |

Capability-cohort analytics aggregate at the audit substrate per ADR 0049's snapshot mechanism (out of ADR 0063 scope; ships only the per-event emission contract).

---

## Consequences

### Positive

- **Pre-install user education.** The System Requirements page is the user's first contact with capability gating; it sets accurate expectations.
- **Reduced operator support burden.** Failed-feature reports include the failed-dimension diagnostic; "this feature requires X; you have Y" is self-service.
- **Capability-cohort baseline.** Install-time evaluations produce telemetry that drives roadmap decisions ("70% of operators install Bundle X with `BiometricAuth` failure-warning; investigate alternative auth for that bundle").
- **Force-install + force-enable composition.** ADR 0062's force-enable surface composes with ADR 0063's force-install link — operators can override at install AND at runtime.
- **Conformance with paper §4 baseline.** The per-bundle spec language can express paper §4's "16GB RAM, 8-core CPU, 500GB NVMe" baseline directly; no mechanism gap.
- **Backward-compat for bundles without specs.** Phase 1 bundles can ship `Requirements: null` and continue to install on every device that ADR 0044 supports; spec authoring is opt-in.

### Negative

- **ADR 0007 amendment required.** Adding `Requirements: MinimumSpec?` to `BusinessCaseBundleManifest` is a coordinated amendment; not a free addition. Migration: existing bundles get `Requirements: null` (no behavior change).
- **Bundle authors must learn the spec schema.** 10 dimension-spec record types is a learning surface. Mitigated by templates + sensible defaults.
- **Per-platform overrides increase author cognitive load.** Per-platform overrides are optional; default behavior is platform-agnostic baseline.
- **Renderer per platform.** 3 renderers (Anchor MAUI / Bridge React / iOS) — but they consume the same `SystemRequirementsResult` data, so divergence is bounded.

### Trust impact / Security & privacy

- **Force-install is operator-only + audited.** Same shape as ADR 0062's force-enable surface. No silent override path.
- **Telemetry payload contains bundle keys + dimension keys, not user data.** `(bundle_key, dimensions_that_regressed)` — no PII.
- **Regulatory dimension surface is honest.** Bundles requiring specific jurisdictions (e.g., a US-CA-only fair-housing bundle) declare it; users in unsupported jurisdictions see clear blocking + operator-recovery hint.
- **No privileged-spec leakage.** The `MinimumSpec` schema is bundle-public — every user can see every bundle's requirements. Bundles do NOT have hidden specs; `Informational` policy is the lightest-weight surfacing for "FYI; not gating."

---

## Compatibility plan

### Existing callers

No existing callers — this is an install-UX layer introduction. ADR 0062's runtime layer (post-A1) is unchanged; ADR 0063 consumes the runtime layer's outputs at install time.

**Migration order (recommended):**

1. **Phase 1:** ship `MinimumSpec` schema + `IMinimumSpecResolver` + `ISystemRequirementsRenderer` substrate (3 renderers; Anchor MAUI / Bridge React / iOS).
2. **Phase 2:** coordinated ADR 0007 amendment landing the `Requirements: MinimumSpec?` field on `BusinessCaseBundleManifest`.
3. **Phase 3:** update kitchen-sink demo bundles + apps/docs to declare meaningful `Requirements` specs.
4. **Phase 4:** per-bundle authoring of `Requirements` — gradual; each existing bundle's owner declares specs as part of their normal maintenance cycle.

### Affected packages

- Modified: `packages/foundation-mission-space/` — extends ADR 0062's package with `MinimumSpec` + spec resolver + renderer surface.
- Modified: `packages/foundation-bundles/` (per ADR 0007) — `BusinessCaseBundleManifest.Requirements` field added (coordinated A1 amendment to ADR 0007 declared as sibling dependency).
- Modified: `apps/docs/foundation/mission-space-requirements/` — new walkthrough + per-bundle spec catalog.
- Modified: `apps/kitchen-sink/` — demo bundles gain `Requirements` declarations.
- New (per-platform): Anchor MAUI / Bridge React / iOS Field-Capture each gain a `SystemRequirementsRenderer` implementation.

### Migration

Existing bundles default to `Requirements: null` — no install-time gating, no behavior change. Bundle authors opt in to declare `Requirements` as part of their normal maintenance cycle. There is no flag-day migration; the system tolerates a long-running mixed state where some bundles have specs and others don't.

---

## Implementation checklist

- [ ] `MinimumSpec` record + 10 per-dimension spec records (`HardwareSpec`, `UserSpec`, `RegulatorySpec`, `RuntimeSpec`, `FormFactorSpec`, `EditionSpec`, `NetworkSpec`, `TrustSpec`, `SyncStateSpec`, `VersionVectorSpec`)
- [ ] `PerPlatformSpec` record + `SpecPolicy` enum
- [ ] `IMinimumSpecResolver` interface + `DefaultMinimumSpecResolver` implementation
- [ ] `SystemRequirementsResult` + `DimensionEvaluation` + `OverallVerdict` + `DimensionPolicyKind` + `DimensionPassFail` enums
- [ ] `ISystemRequirementsRenderer` interface + 3 platform-specific implementations:
  - `AnchorMauiSystemRequirementsRenderer` (Razor view)
  - `BridgeReactSystemRequirementsRenderer` (React component)
  - `IosFieldCaptureSystemRequirementsRenderer` (SwiftUI view)
- [ ] DI extension: `AddSunfishMinimumSpec()` registering the resolver + 3 default renderers
- [ ] 4 new `AuditEventType` constants:
  - `MinimumSpecEvaluated`
  - `InstallBlocked`
  - `InstallWarned`
  - `PostInstallSpecRegression`
- [ ] `MinimumSpecAuditPayloadFactory` (mirrors ADR 0062-A1 + ADR 0028-A6.6 patterns)
- [ ] Audit dedup at the emission boundary per ADR 0028-A6.5.1 pattern (5-min `MinimumSpecEvaluated`; 24-hour `PostInstallSpecRegression`; no dedup on `InstallBlocked` / `InstallWarned`)
- [ ] Test coverage:
  - 10 per-dimension spec resolution tests (one per dimension)
  - 3 OverallVerdict transition tests (Pass / WarnOnly / Block)
  - Force-install round-trip test (operator override flow)
  - Post-install regression detection test (envelope change → spec re-evaluation → audit event)
  - Per-platform override resolution test (iOS uses iOS override; macOS uses baseline)
  - Steam-style System Requirements rendering integration test (each of the 3 renderers)
- [ ] Coordinated ADR 0007 amendment (sibling dependency) — `BusinessCaseBundleManifest.Requirements: MinimumSpec?` field
- [ ] `apps/docs/foundation/mission-space-requirements/` walkthrough page + per-bundle spec catalog
- [ ] `apps/kitchen-sink/` demo bundle gets meaningful `Requirements` declarations as the first reference example

---

## Open questions

- **OQ-0063.1:** Should `SpecPolicy = Informational` produce a UI surface at all, or is it purely documentation? Recommend UI surface — Steam-style requirements pages routinely include "Notes" text; informational dimensions have a place. Revisit if the UX is cluttered.
- **OQ-0063.2:** Should per-feature `MinimumSpec` overrides be supported in v0 or deferred? Recommend defer (Option D's hybrid is the long-term shape but Option A pure-bundle is shippable first); revisit if feature-bespoke spec authoring is a real-world pain.
- **OQ-0063.3:** What's the operator UX for "Force Install" given the audit trail? Recommend the operator force-install path requires a justification text (similar to ADR 0062's `CapabilityForceEnableRequest.Justification`) that gets attached to the `InstallBlocked` audit event payload. Revisit if operator workflow gets too friction-heavy.
- **OQ-0063.4:** Should `MinimumSpec` support "EITHER OR" composition (e.g., "user has biometric OR password-with-2FA")? v0 ships AND-only composition (every declared dimension must pass); OR composition is deferred. Real-world need likely exists for auth methods specifically; revisit when the first OR-required bundle surfaces.
- **OQ-0063.5:** Localization of the per-dimension `UserMessage`/`OperatorRecoveryHint` — does ADR 0063 ship default localization keys per dimension (e.g., `mission-space.requirements.hardware.min-memory.fail`), or does each renderer roll its own? Recommend ship defaults; renderers may override per-key.

---

## Revisit triggers

- **Bundle-author authoring cost is too high.** If declaring `Requirements` becomes a real friction, revisit Option D per-feature granularity OR provide a `MinimumSpec` builder DSL.
- **Per-platform override usage is rare.** If post-Phase-3 telemetry shows < 10% of bundles use per-platform overrides, simplify the schema in an A1 amendment.
- **Cohort analytics surface a pattern AND-only composition can't express.** OR composition becomes a future amendment.
- **Steam-style UX produces friction in real user testing.** If users skip the System Requirements page or misinterpret warnings, revisit Option B per-feature granularity OR the rendering hierarchy.

---

## References

- W#33 Mission Space Matrix discovery: [`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`](../../icm/01_discovery/output/2026-04-30_mission-space-matrix.md) §5.2 + §6.1 + §7.2
- Intake: [`icm/00_intake/output/2026-04-30_mission-space-requirements-intake.md`](../../icm/00_intake/output/2026-04-30_mission-space-requirements-intake.md)
- ADR 0062 (Mission Space Negotiation Protocol; post-A1) — runtime layer that ADR 0063 consumes
- ADR 0028 (CRDT Engine Selection; post-A8 + post-A7) — `FormFactorProfile` + `VersionVector` types
- ADR 0007 (BusinessCaseBundleManifest) — bundle manifest schema; coordinated A1 amendment required for the `Requirements` field
- ADR 0009 (Edition / IEditionResolver) — Edition vocabulary consumed by `EditionSpec`
- ADR 0036 (sync-state surface) — consumed by `SyncStateSpec`
- ADR 0044 (Phase 1 Windows-only platform scope) — establishes the .NET-MAUI-Windows ground truth
- ADR 0048 (Phase 2 multi-platform target list) — establishes the Phase 2 target list
- ADR 0049 (audit substrate) — telemetry emission target
- Paper [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) §4 (hardware baseline) + §13.1 (Complexity Hiding) + §13.2 (visibility tables)
- Steam System Requirements page (closest UX prior-art for per-product capability surfacing)

---

## Sibling amendment dependencies named

- **ADR 0007 amendment A1** — `BusinessCaseBundleManifest.Requirements: MinimumSpec?` field. Coordinated with ADR 0063's Phase 2; ADR 0063 Phase 1 ships the substrate but cannot wire bundles' specs until ADR 0007-A1 lands. Queued as XO follow-up: file `2026-04-30_bundle-manifest-requirements-field-intake.md` for ADR 0007-A1.
- **ADR 0031 amendment A1** (per ADR 0062-A1.6 / A1.16) — Bridge → Anchor subscription-event-emitter contract. Independent of ADR 0063 but blocks ADR 0062 Phase 1; ADR 0063 Phase 1 can proceed in parallel since it consumes ADR 0062's existing 30-second cache TTL behavior on `EditionCapabilities`.

---

## Cohort discipline

Per `feedback_decision_discipline.md` cohort batting average (13-of-13 substrate amendments needing council fixes; structural-citation failure rate 6-of-13 XO-authored amendments — all caught pre-merge):

- **Pre-merge council canonical** for ADR 0063. Auto-merge intentionally DISABLED until a Stage 1.5 council subagent reviews. Council should specifically pressure-test:
  - The `MinimumSpec` schema partition vs ADR 0062's `MissionEnvelope` — is the spec dimension naming consistent (e.g., `EditionSpec` vs `EditionCapabilities`; is the symmetry useful or confusing)?
  - The `SpecPolicy = Required / Recommended / Informational` taxonomy — exhaustive or are there real cases that don't fit (e.g., "soft-required" — recommended-but-strongly-encouraged)?
  - The Steam-style UX prior-art — is Steam the right anchor (gaming context vs business-app context), or are App Store / Microsoft Store / Mac App Store closer?
  - Per-platform override resolution — is "active platform override OR baseline" the right rule, or should overrides COMPOSE with baseline (e.g., baseline says 16GB RAM; iOS override says BiometricAuth; combined: 16GB RAM + BiometricAuth)?
  - Force-install audit shape — is the `InstallBlocked` audit emitted on the user's "blocked" event AND on operator's "force-install"? The current spec emits only on user-blocked-attempt; force-install is a separate `CapabilityForceEnabled`-style event the spec should declare.
  - The 4 new audit-event constants — collision-check against existing `AuditEventType` constants in `packages/kernel-audit/`.
- **Cited-symbol verification** per the §A0 cited-symbol audit (existing on origin/main verified; introduced types listed; structural-citation spot-check on all field-on-type claims per the A7 lesson)
- **Standing rung-6 spot-check** within 24h of ADR 0063 merging (per ADR 0028-A4.3 + A7.12 + A8.12 + ADR 0062-A1.15 commitment)

---

## Amendments (post-acceptance, 2026-04-30 council)

### A1 (REQUIRED, mechanical) — 0063 council-review fixes

**Driver:** Stage 1.5 adversarial council review of ADR 0063 at `icm/07_review/output/adr-audits/0063-council-review-2026-04-30.md` (PR #413, merged 2026-04-30) returned verdict **B (low-B) with 11 required + 4 encouraged amendments**. Severity profile: **4 Critical (F1 + F2 + F3 + F6; three of four are structural-citation failures — A7-third-direction class) + 7 Major + 2 Minor + 2 Encouraged + 9 verification-passes**. Per `feedback_decision_discipline` Rule 3, mechanical council fixes auto-accept; A1 absorbs all 15 recommendations into ADR 0063's surface before Phase 1 substrate scaffold begins.

**Cohort milestone (load-bearing lesson):** ADR 0063 is the **first amendment in the W#33 lineage where the §A0 self-audit pattern (introduced post-ADR-0062 council per A1.14) caught zero of the structural-citation failures the council later surfaced**. Three of four Critical findings (F1, F2, F3) were structural-citation failures that passed XO's pre-merge §A0 audit but failed council verification:

- **F1:** §A0 cited `MinimumSpecDimension` (an invented type) as an ADR 0062 export. ADR 0062 has `DimensionChangeKind` (10-value enum at line 145). XO's §A0 audit listed `MinimumSpecDimension` without verifying it against ADR 0062's actual surface.
- **F2:** `SyncStateSpec.AcceptableStates` example used `{ Synced, Local, Stale, Quarantined }`. ADR 0036's actual canonical state names are `healthy / stale / offline / conflict / quarantine`. `Local` is non-existent; the others conflate UI labels with canonical state-identifiers.
- **F3:** `NetworkSpec.RequiredTransports` example used `Tier1Mdns`. ADR 0061's `TransportTier` enum has `LocalNetwork / MeshVpn / ManagedRelay`; `Tier1Mdns` does not exist.
- **F5 (Major):** `packages/foundation-bundles/` cited; actual package is `packages/foundation-catalog/` (namespace `Sunfish.Foundation.Catalog.Bundles`).

The §A0 audit pattern was introduced specifically to catch this failure mode. It did not. The pattern is **necessary but not sufficient**. Council remains canonical for substrate-tier ADRs in the W#33 lineage; this lesson is captured explicitly in A1.15 below + propagated to memory.

**Cohort batting average:** 14-of-14 substrate amendments needing council fixes. Structural-citation failure rate (XO-authored): now **6+4 = 10 of 14 amendments** (~71%); ADR 0063 alone contributes 4 instances — high-water mark.

#### A1.1 — Replace `MinimumSpecDimension` with `DimensionChangeKind` in §A0 (council A1 / F1 Critical structural-citation)

§A0 "Existing on origin/main" entry for ADR 0062 is corrected:

> ADR 0062 (Mission Space Negotiation Protocol; post-A1) — landed via PR #406; provides `MissionEnvelope`, `IMissionEnvelopeProvider`, `IFeatureGate<TFeature>`, `FeatureVerdict`, `DegradationKind`, `EnvelopeChange`, `DimensionChangeKind` (10-value dimension-key enum), `LocalizedString`, and `FeatureVerdictSurfaced` audit constant — ADR 0063 consumes these post-A1.2 / A1.8 / A1.11 / A1.12 surfaces.

The invented `MinimumSpecDimension` type is removed from §A0 entirely.

#### A1.2 — Replace `SyncStateSpec.AcceptableStates` example with ADR-0036-canonical state names + halt-condition (council A2 / F2 Critical structural-citation)

§"Initial contract surface" `SyncStateSpec` example comment changes to:

```csharp
public sealed record SyncStateSpec(
    IReadOnlySet<SyncState>? AcceptableStates    // e.g., { Healthy, Stale } — exclude Offline, Conflict, Quarantine.
                                                  // State names are the PascalCase form of ADR 0036's encoding-contract identifiers
                                                  // (healthy / stale / offline / conflict / quarantine)
);
```

**Halt-condition added (post-A1):** ADR 0063 Stage 06 build CANNOT ship `SyncStateSpec` until either (a) `Sunfish.Foundation.UI.SyncState` (or equivalent foundation-tier `SyncState` enum) is publicly exposed by a sibling amendment to ADR 0036, OR (b) the `SyncStateSpec.AcceptableStates` field type is changed to `IReadOnlySet<string>?` with the doc-comment naming the canonical state strings as the contract. Phase 1 substrate scaffold MAY ship `SyncStateSpec` as `Sunfish.Foundation.UI.SyncState` and Stage 06 work picks up the halt-resolution.

#### A1.3 — Replace `NetworkSpec.RequiredTransports` example with the actual `TransportTier` enum value (council A3 / F3 Critical structural-citation)

§"Initial contract surface" `NetworkSpec` example comment changes to:

```csharp
public sealed record NetworkSpec(
    IReadOnlySet<TransportTier>?    RequiredTransports  // e.g., { LocalNetwork } for same-LAN-required scenarios per ADR 0061.
                                                         // (For offline-first declarations, prefer
                                                         // FormFactorSpec.MinNetworkPosture: NetworkPosture.OfflineFirst per ADR 0028-A5.1;
                                                         // this dimension is for declaring REQUIRED transports, not capability profile.)
);
```

#### A1.4 — Rename `OperatorRecoveryHint` → `OperatorRecoveryAction` throughout `DimensionEvaluation` (council A4 / F4 Major vocabulary-fork)

ADR 0062 post-A1.11 canonical vocabulary uses `OperatorRecoveryAction`. ADR 0063's `DimensionEvaluation` introduced a divergent `OperatorRecoveryHint` field; this is corrected:

```csharp
public sealed record DimensionEvaluation(
    DimensionChangeKind     Dimension,
    DimensionPolicyKind     PolicyKind,
    DimensionPassFail       Result,
    LocalizedString?        UserMessage,
    LocalizedString?        OperatorRecoveryAction  // (renamed from OperatorRecoveryHint per ADR 0062 post-A1.11 vocabulary)
);
```

Same rename applies to all references to `OperatorRecoveryHint` elsewhere in §"Initial contract surface" + §"Pre-install UX flow" + §"Post-install UX flow".

#### A1.5 — Replace `packages/foundation-bundles/` with `packages/foundation-catalog/` (council A5 / F5 Major structural-citation)

§"Affected packages" + §"Implementation checklist" references corrected:

> Modified: `packages/foundation-catalog/` (per ADR 0007; namespace `Sunfish.Foundation.Catalog.Bundles`) — `BusinessCaseBundleManifest.Requirements` field added (coordinated A1 amendment to ADR 0007 per sibling amendment dependencies).

#### A1.6 — Memory-spec unit alignment (council A6 / F6 Critical unit-mismatch)

Per Option α (recommended in council A6): `HardwareSpec.MinMemoryBytesGb: int?` is renamed to `MinMemoryBytes: long?` to match `MinDiskBytes: long?` and the envelope's `memoryBytes: long`:

```csharp
public sealed record HardwareSpec(
    long?                   MinMemoryBytes,      // e.g., 17_179_869_184L (= 16 GB; matches paper §4 baseline)
    int?                    MinCpuCores,         // e.g., 8 (paper §4 baseline)
    long?                   MinDiskBytes,        // e.g., 536_870_912_000L (= 500 GB; paper §4 baseline)
    DisplayClass?           MinDisplayClass,
    IReadOnlySet<SensorRequirement>? RequiredSensors
);
```

The resolver compares envelope's `MemoryBytes: long` against spec's `MinMemoryBytes: long` directly — no unit conversion; no foot-gun.

#### A1.7 — Per-platform override resolution: COMPOSE not REPLACE (council A7 / F7 Major internal-contradiction)

The original §"Per-platform per-bundle spec resolution" rule said "active platform override OR baseline" but the canonical example (iOS adds `BiometricAuth` on top of baseline) implies COMPOSE. The rule is corrected to match the example:

> When `BusinessCaseBundleManifest.Requirements.PerPlatformOverrides` is set, the active platform's override is **merged** with the baseline per the following per-dimension rule:
>
> 1. **For each dimension**, if the override declares a value for that dimension, the override's value REPLACES the baseline's value for that dimension. If only the baseline declares, the baseline's value applies. If neither declares, the dimension is unspec'd ("any").
> 2. The merged spec is the spec used for evaluation against the user's MissionEnvelope.
> 3. **No override AND no baseline** (`Requirements == null`): bundle is "any" (effectively `OverallVerdict.Pass` always).

The example prose is updated: the iOS override "adds `BiometricAuth` on top of the baseline's 16GB / 8-core / etc." and the merged effective spec on iOS is `{ Hardware: { MinMemoryBytes: 17_179_869_184L, ..., RequiredSensors: { BiometricAuth } }, Policy: Required }`.

#### A1.8 — `OverallVerdict` rule for `Informational` dimensions (council A8 / F8 Major)

`OverallVerdict` doc-comments updated to make the Informational rule explicit:

```csharp
public enum OverallVerdict
{
    Pass,       // every Required dimension passes; every Recommended dimension passes; Informational dimensions IGNORED for verdict (surfaced in System Requirements page only)
    WarnOnly,   // every Required dimension passes; one or more Recommended dimensions fail; Informational dimensions IGNORED for verdict
    Block       // one or more Required dimensions fail; Recommended/Informational do not move the needle from Block
}
```

A clarifying paragraph added above the enum: *"`Informational` dimensions are evaluated and surfaced in the System Requirements page table but do NOT affect `Overall`. Failure of an Informational dimension is purely diagnostic — the user sees it in the page; it does not block install or warn."*

#### A1.9 — Post-install regression evaluation cost class + optimization (council A9 / F9 Major scaling)

A new sub-section added under §"Post-install UX flow":

> **Re-evaluation cost class.** On `EnvelopeChange` events with `Severity ∈ { Warning, Critical }`, the resolver re-evaluates **only the installed bundles whose `Requirements` declare at least one of the dimensions in `EnvelopeChange.ChangedDimensions`** (filtering optimization). For each such bundle, the resolver runs `EvaluateAsync` against the new envelope. Cost class: `Low` (in-memory comparison after envelope is fetched). Per-event budget: P95 < 100ms for ≤50 installed bundles × full re-evaluation (typical home/SMB tenant). Above 50 bundles, the resolver SHOULD batch evaluations on a background-priority worker; this is a SHOULD not a MUST for v0.

#### A1.10 — Warning-persistence policy after `WarnOnly` dismissal (council A10 / F10 Major UX-spec)

A new sub-section added under §"Pre-install UX flow":

> **Warning persistence policy.** `WarnOnly` warnings are dismissed per-version-per-install. Specifically:
>
> - Bundle install with version V1: warning shown; user dismisses; warning hidden for the install lifetime of V1.
> - Bundle update from V1 to V2 (any version bump): warning re-shown if WarnOnly still applies under V2's spec. The dismissal does NOT carry over.
> - Bundle uninstall + reinstall: treated as a new install; warning re-shown.
> - User envelope change that resolves the warning silently no-ops; the warning surface disappears (no notification).
> - User envelope change that introduces a NEW Recommended-dimension failure produces a fresh warning surface (not a dismissed one).

#### A1.11 — `InstallForceEnabled` audit event for operator force-install (council A11 / F11 Major audit-gap)

A 5th `AuditEventType` constant is added per the original ADR 0063 telemetry shape:

| Event | Trigger | Payload | Dedup window |
|---|---|---|---|
| `InstallForceEnabled` | Operator force-installs a bundle whose Overall is Block | `(bundle_key, failed_required_dimensions, operator_id, justification)` | None (per-attempt) |

Force-install path now emits both `InstallBlocked` (the user's blocked attempt) AND `InstallForceEnabled` (the operator's override) — symmetric with ADR 0062's force-enable audit shape (`FeatureForceEnabled` + `FeatureForceEnableRejected`).

OQ-0063.3's question ("operator UX for Force Install given the audit trail") is now resolved: justification text required; attached to `InstallForceEnabled` payload.

#### A1.12 — §"Prior art" subsection (council A12 / F12 Encouraged)

A new §"Prior art" subsection added (between §"Considered options" and §"Decision"):

> **Declarative-spec prior art.** While Steam's System Requirements page is the user-facing UX anchor (per §"Decision drivers"), the *declarative-schema* anchors are:
>
> - **Apple `UIRequiredDeviceCapabilities`** in `Info.plist` — direct prior-art for `HardwareSpec.RequiredSensors`. iOS App Store filters by these declarations.
> - **Android `<uses-feature android:required="true|false">`** in `AndroidManifest.xml` — direct prior-art for the Required vs Recommended split + per-feature granularity.
> - **Microsoft Store `<DeviceCapability>`** in `appxmanifest.xml` — capability-declaration enforcement at install.
> - **.NET msbuild `<TargetFramework>`** in `*.csproj` — direct prior-art for `RuntimeSpec.MinDotnetVersion`.
> - **VS Code Extension Manifest `engines` field** — JSON declarative-spec analog matched to ADR 0007's manifest-as-JSON shape.
>
> ADR 0063's `MinimumSpec` schema is best understood as a cross-platform substrate generalization of these per-platform manifest declarations, hoisted to the bundle-manifest tier so that a single bundle ships per-platform overrides that the local renderer translates into the active platform's idiom (or simply consumes the substrate spec directly when no platform-specific manifest exists).

#### A1.13 — §"Localization" subsection (council A13 / F13 Encouraged)

A new §"Localization" subsection added (under §"Decision"):

> **Localization.** Spec authoring is purely structural data (enums, numeric thresholds, sets). Spec rendering produces `LocalizedString?` outputs from per-dimension default localization keys (e.g., `mission-space.requirements.hardware.min-memory.fail`). Renderers may override per-key. Keys live alongside the spec records in `Sunfish.Foundation.MissionSpace.Localization.MinimumSpecKeys` (a static class of constants); the renderer fetches them via the platform's localization framework — `.resx` for Anchor MAUI; `i18next` for Bridge React; `.strings` for iOS Field-Capture. Default English values ship in `MinimumSpecKeys.DefaultEnglish` for the `LocalizedString.DefaultValue` fallback.

#### A1.14 — Force-Install link visibility (council A14 / F14 Encouraged)

§"Pre-install UX flow" step 4 corrected:

> 4. If Overall is Block: Install button is disabled; tooltip naming the failed Required dimensions; **operator-only "Force Install" link rendered server-side only when the requesting user has the operator role** (so the link is never visible to non-operators; non-operators see the tooltip without the link). Server-side rendering is canonical; client-side hiding via CSS is forbidden (the link's existence in the DOM would leak the operator-override surface).

#### A1.15 — §A0 cited-symbol audit lesson (council A15 / Encouraged)

§"Cohort discipline" gains a closing paragraph documenting the load-bearing lesson:

> **§A0 cited-symbol audit lesson (post-ADR-0063 council).** ADR 0063's §A0 audit was structurally correct in shape (mirroring ADR 0062-A1.14 + ADR 0028-A8.11) but failed to catch 4 structural-citation issues — `MinimumSpecDimension` (invented; F1), `SyncStateSpec.AcceptableStates` example values (non-existent in ADR 0036; F2), `NetworkSpec.RequiredTransports` example values (non-existent in ADR 0061; F3), and `packages/foundation-bundles/` (non-existent; F5). The §A0 self-audit pattern is **necessary but not sufficient**. Council-side spot-checks (the original A7 lesson) remain canonical for substrate ADRs in the W#33 lineage. Future ADRs in this lineage MUST include both: (a) §A0 self-audit at draft time, AND (b) pre-merge council with three-direction structural verification per the `feedback_council_can_miss_spot_check_negative_existence` memory's third direction.
>
> **Memory commitment:** The memory note `feedback_council_can_miss_spot_check_negative_existence` is updated separately (next memory edit) to record this outcome — the §A0 self-audit pattern was tested on ADR 0063 and produced 0% catch-rate against the structural-citation failures the council later surfaced. The pattern is retained but downgraded from "primary defense" to "necessary supplement"; pre-merge council is the canonical defense.

#### A1.16 — Cohort discipline log

Per `feedback_decision_discipline.md` cohort batting average:

- **Substrate-amendment council batting average:** **14-of-14** (forward pattern). Council surfaced 4 Critical + 7 Major + 2 Minor + 2 Encouraged pre-merge — all mechanical to absorb. Cohort lesson holds: pre-merge council remains dramatically cheaper than post-merge.
- **Council false-claim rate (all three directions):** unchanged at 2-of-12 across the cohort. The 0063 council made 0 false-existence + 0 false-non-existence + 0 false-structural claims (verification-pass findings are explicit positive-existence + structural-citation verifications with verification commands).
- **Structural-citation failure rate (XO-authored):** **10-of-14 amendments** (~71%); ADR 0063 alone contributes 4 instances — high-water mark in the cohort. The §A0 self-audit pattern caught 0 of 4. Pre-merge council remains the canonical defense.
- **Standing rung-6 task reaffirmed:** XO spot-checks A1's added/modified citations within 24h of merge. If any A1-added claim turns out to be incorrect, file an A2 retraction.

#### A1.17 — Sibling amendment dependencies named

A1.2's halt-condition declares: ADR 0063 Stage 06 build cannot ship `SyncStateSpec` as `IReadOnlySet<SyncState>?` until either (a) ADR 0036-A1 exposes a public `Sunfish.Foundation.UI.SyncState` enum, OR (b) `SyncStateSpec.AcceptableStates` is changed to `IReadOnlySet<string>?` with the doc-comment naming the canonical state strings. Recommend (a) — sibling intake to ADR 0036-A1 is queued as XO follow-up: file `2026-04-30_sync-state-public-enum-intake.md` for ADR 0036-A1.

ADR 0007-A1 (`BusinessCaseBundleManifest.Requirements: MinimumSpec?` field) — already filed via PR #412; remains the canonical sibling for ADR 0063 Phase 2 wiring.

ADR 0064 (Runtime Regulatory Policy Evaluation) — independent of ADR 0063; queued next per W#33 §7.2; can run in parallel.

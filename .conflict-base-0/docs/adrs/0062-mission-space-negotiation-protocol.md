# ADR 0062 — Mission Space Negotiation Protocol (runtime layer)

**Status:** Proposed (2026-04-30 — auto-merge intentionally DISABLED until Stage 1.5 council reviewed per cohort discipline; see §"Cohort discipline" at end)
**Date:** 2026-04-30
**Author:** XO (research session)
**Pipeline variant:** `sunfish-feature-change` (introduces new cross-cutting protocol; affects all ten Mission Space dimensions)

**Resolves:** [`icm/00_intake/output/2026-04-30_mission-space-negotiation-protocol-intake.md`](../../icm/00_intake/output/2026-04-30_mission-space-negotiation-protocol-intake.md); third item in W#33 Mission Space Matrix follow-on authoring queue per [`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`](../../icm/01_discovery/output/2026-04-30_mission-space-matrix.md) §7.2.

---

## Context

The foundational paper [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) §13.2 specifies AP/CP visibility tables (staleness thresholds + UX treatments for *some* dimensions); ADR 0036 specifies 5 sync states with ARIA roles; ADR 0041 specifies the rich-vs-MVP UI degradation primitive. The W#33 Mission Space Matrix discovery doc identifies **10 dimensions** that gate Sunfish features (hardware × user × regulatory × runtime × form-factor × commercial-tier × network × trust × sync-state × lifecycle/migration × version-vector). Each dimension can be probed, cached, and re-evaluated; each can produce a feature-availability change that user-facing UI must surface.

Today there is **no protocol** for how a deployment probes its own capability profile, communicates that profile, re-evaluates it when conditions change, or degrades gracefully when a previously-available feature becomes unavailable. Each feature implementer re-derives the probe-cache-communicate-re-evaluate logic from scratch — producing inconsistent UX (one feature shows a toast, another silently disappears, a third renders a hard error), inconsistent caching (one polls every minute, another caches for 24 hours, a third never re-evaluates), and inconsistent telemetry shapes (capability-cohort analysis is ad-hoc).

Substrate ADRs ADR 0028-A6 (post-A7; version-vector compatibility) and ADR 0028-A5 (post-A8; cross-form-factor migration) establish the underlying compatibility relations. ADR 0062 sits at the runtime layer ABOVE those — it consumes the compatibility verdicts and packages them into a coherent Mission Envelope that user-facing surfaces (Anchor + Bridge + iOS + future adapters) can render uniformly.

W#33 Mission Space Matrix §6.2 names this ADR as the **highest engineering priority** of the 4 follow-on items: every other dimensional gate ultimately surfaces through this protocol's UX channels. ADR 0063 (Mission Space Requirements; queued next) consumes this ADR's Mission Envelope to render the install-time UX layer. ADR 0064 (Runtime Regulatory Policy Evaluation; queued after) extends this ADR's probe mechanics with jurisdictional re-evaluation.

---

## Decision drivers

- **Paper §13.2 + ADR 0036 + ADR 0041 are downstream consumers, not specs.** None of them define the negotiation protocol; they describe what the protocol's output should *look like*. ADR 0062 produces what they need.
- **Industry prior art has a clear winner: DirectX Feature Levels.** Of the canonical capability-negotiation prior art (SIP/SDP per RFC 5939; TLS cipher-suite negotiation; OpenGL/Vulkan extension queries; DirectX Feature Levels; HTTP content negotiation; WebRTC codec negotiation), DirectX Feature Levels (FL_9_1 / FL_10_0 / FL_11_0 / FL_12_0) are the closest engineering analog: discrete tiers; runtime-queryable; degrade gracefully; OS surfaces them through a uniform API that game engines consume directly.
- **DirectX Feature Levels' specific lessons:** (a) tiers are *enumerated*, not arithmetic (an app declares "needs FL_10_0+", not "needs version ≥ 10.0"); (b) the OS *guarantees* the tier behaviorally — if FL_10_0 is reported, every FL_10_0 feature works; (c) re-evaluation is rare (only on driver upgrades, not app launches); (d) graceful degradation is the developer's responsibility, not the OS's — but the OS provides the *information* the developer needs.
- **Probe-cost asymmetry is real.** Some probes are cheap (e.g., "is the camera permission granted?" — local API call). Some are expensive (e.g., "is the Bridge subscription active?" — HTTP round trip). The negotiation protocol must distinguish per-probe cost classes.
- **User-communication is opinionated, not freeform.** Paper §13.2 already constrains the UX surface (staleness thresholds + visibility tables); ADR 0036 already constrains sync-state UI. ADR 0062's user-communication policy must conform to these existing primitives, not introduce competing patterns.
- **Force-enable is a power-user feature, not the default.** The substrate must offer a force-enable surface (per the intake's "Per-feature force-enable" scope item) — but the default UX must not surface it; only an explicit operator opt-in path.
- **Telemetry shape feeds product decisions.** Capability-cohort analytics drive roadmap (e.g., "if 60% of Anchor users lack feature X's hardware prereq, deprioritize feature X polish"). The shape MUST be designed up-front so every feature emits compatible cohort data.

---

## Considered options

### Option A — Tier-based Mission Envelope with central coordinator + per-feature gates [RECOMMENDED]

A single `IMissionEnvelopeProvider` acts as the central coordinator: it owns the full Mission Envelope (the canonical record of which capabilities are available right now), runs probes on demand (cached) or on scheduled re-evaluation, emits change events when the envelope shifts. Per-feature gates (`ICapabilityGate<TCapability>`) consume the envelope and decide whether their feature is available; gates also subscribe to envelope-change events to trigger UI updates.

DirectX-Feature-Level analogue: `IMissionEnvelopeProvider` is the OS device-caps query; `ICapabilityGate<TCapability>` is per-feature probing the relevant tier flag.

- **Pro:** Single source of truth; consistent caching policy across all features
- **Pro:** Per-feature gates compose cleanly with ADR 0036 sync-state primitive + ADR 0041 rich-vs-MVP degradation
- **Pro:** Telemetry is uniform (every gate emits `CapabilityProbed` + `CapabilityChanged` audit events with the same shape)
- **Pro:** Re-evaluation cost is bounded — the coordinator probes once; all gates consume the result
- **Con:** Coordinator becomes a chokepoint for capability changes (mitigated: change events are async; gates don't wait synchronously)
- **Con:** Adds 2 new substrate types (`IMissionEnvelopeProvider` + `ICapabilityGate<TCapability>`); ~1 new package (`Sunfish.Foundation.MissionSpace`)

**Verdict:** Recommended. Matches DirectX-Feature-Level prior art; conforms to existing paper §13.2 + ADR 0036 + ADR 0041 surfaces; acceptable cost for a substrate of this load-bearing weight.

### Option B — Per-feature ad-hoc probes (status quo, codified)

Each feature owns its own probe + cache + re-evaluation logic. ADR 0062 documents conventions but doesn't introduce shared types.

- **Pro:** Zero new packages; no migration cost
- **Pro:** Each feature can optimize its own probe
- **Con:** Status quo's failure modes (inconsistent UX, inconsistent caching, ad-hoc telemetry) all preserved
- **Con:** Capability-cohort analytics impossible without per-feature instrumentation
- **Con:** Re-evaluation triggers cascade: a hardware change requires N feature gates to independently detect, instead of one coordinator broadcasting

**Verdict:** Rejected. Codifying status quo defeats the purpose of authoring ADR 0062 in the first place; the W#33 §6.2 "highest engineering priority" framing presumes a substrate-tier solution.

### Option C — Push-based capability bus (publisher/subscriber across all probes)

Probes publish capability changes to a central bus; gates subscribe; no central envelope record (the bus IS the protocol). Every feature owns its own probe but consumes the bus uniformly.

- **Pro:** Decentralized — no single chokepoint
- **Pro:** Per-feature probes can be optimized
- **Con:** No canonical "what's the current envelope?" query — the bus has *change events*, not *current state*. Diagnostics and telemetry both need a state record on top.
- **Con:** Re-evaluation triggers still per-probe; no central re-eval cadence
- **Con:** UX consistency requires gates to coordinate their bus-subscription patterns, which is more complex than a single coordinator

**Verdict:** Rejected. The state-record gap is load-bearing — diagnostics + "what your device can do" UI + telemetry all need a current-envelope query, which a pure bus doesn't provide.

### Option D — Hybrid: coordinator owns *some* dimensions; per-feature owns *others*

Coordinator owns the 10 dimensions named in W#33 §6 (hardware × user × regulatory × runtime × form-factor × commercial-tier × network × trust × sync-state × lifecycle/migration × version-vector); per-feature gates handle their own bespoke probes for feature-specific signals (e.g., "is the SQLite database file writable?" — feature-specific; not a Mission Space dimension).

- **Pro:** Coordinator owns the canonical 10 dimensions; gates have flexibility for bespoke per-feature signals
- **Pro:** Capability-cohort analytics are uniform on the 10 dimensions; bespoke signals get bespoke instrumentation
- **Pro:** Composition cost is low (gates that only need the 10 dimensions don't write any probe code)
- **Con:** Two-tier reasoning is required at gate authoring time ("is this signal a Mission Space dimension or feature-bespoke?")
- **Con:** Risk of dimension creep (every bespoke signal eventually wants to migrate into the coordinator)

**Verdict:** Recommended as the *actual decision* — Option A pure-coordinator is too rigid; Option D's hybrid is what production deployments will need. Treated as an Option-A specialization in §"Decision".

---

## Decision

**Option A specialized via Option D's hybrid pattern.** A central coordinator (`IMissionEnvelopeProvider`) owns the 10 W#33-canonical dimensions; per-feature gates (`ICapabilityGate<TCapability>`) consume the envelope AND optionally extend it with feature-bespoke signals via a separate `IFeatureBespokeProbe` extension surface. Both compose into a uniform `MissionEnvelope` snapshot at any given moment.

### Initial contract surface

Located in new package `Sunfish.Foundation.MissionSpace`:

```csharp
namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Canonical record of all Mission Space dimensions at a single moment.
/// Snapshots are immutable; change events ship updated snapshots.
/// </summary>
public sealed record MissionEnvelope(
    HardwareCapabilities  Hardware,
    UserCapabilities      User,
    RegulatoryCapabilities Regulatory,    // jurisdiction × consent × policy
    RuntimeCapabilities   Runtime,        // .NET / Swift / OS / arch
    FormFactorProfile     FormFactor,     // per ADR 0028-A5/A8 — same type
    CommercialTier        CommercialTier, // per ADR 0009/0031
    NetworkCapabilities   Network,        // per ADR 0061 transport tiers
    TrustCapabilities     Trust,          // per ADR 0009 trust tiers
    SyncStateSnapshot     SyncState,      // per ADR 0036
    VersionVector         VersionVector,  // per ADR 0028-A6/A7
    Instant               CapturedAt,
    string                EnvelopeHash    // sha256 of canonical-JSON encoding; for change detection
);

public interface IMissionEnvelopeProvider
{
    /// <summary>Returns the current Mission Envelope (cached if available; probes if not).</summary>
    ValueTask<MissionEnvelope> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>Forces a fresh probe of all dimensions, bypassing cache.</summary>
    ValueTask<MissionEnvelope> ProbeAsync(CancellationToken ct = default);

    /// <summary>Subscribes to envelope-change events. Returns an IDisposable to unsubscribe.</summary>
    IDisposable Subscribe(IMissionEnvelopeObserver observer);
}

public interface IMissionEnvelopeObserver
{
    /// <summary>Called when any dimension of the envelope changes (post-probe; debounced per A6.5.1-style dedup).</summary>
    ValueTask OnEnvelopeChangedAsync(EnvelopeChange change, CancellationToken ct);
}

public sealed record EnvelopeChange(
    MissionEnvelope Previous,
    MissionEnvelope Current,
    IReadOnlyList<DimensionChangeKind> ChangedDimensions
);

public enum DimensionChangeKind
{
    Hardware,
    User,
    Regulatory,
    Runtime,
    FormFactor,
    CommercialTier,
    Network,
    Trust,
    SyncState,
    VersionVector
}
```

Per-feature gate contract:

```csharp
public interface ICapabilityGate<TCapability> where TCapability : ICapability
{
    /// <summary>Evaluates whether <typeparamref name="TCapability"/> is available given the current Mission Envelope.</summary>
    ValueTask<CapabilityVerdict> EvaluateAsync(MissionEnvelope envelope, CancellationToken ct = default);
}

public sealed record CapabilityVerdict(
    CapabilityState State,                                       // Available / DegradedAvailable / Unavailable
    DegradationKind DegradationKind,                             // see §"Graceful-degradation taxonomy"
    string?         UserMessage,                                 // localized; nullable if State == Available
    string?         OperatorRecoveryAction,                      // operator-readable next step; nullable if State == Available
    IReadOnlyList<DimensionChangeKind> RelevantDimensions        // which envelope dimensions drove this verdict
);

public enum CapabilityState
{
    Available,            // feature works; no UI hint needed
    DegradedAvailable,    // feature works with reduced surface; UI hint per DegradationKind
    Unavailable           // feature does not work; UI per DegradationKind
}

public enum DegradationKind
{
    None,                 // (used only with State.Available)
    Hide,                 // UI surface removed entirely; no user message
    DisableWithExplanation,  // UI shows disabled state + explanation tooltip; no upsell
    DisableWithUpsell,    // UI shows disabled state + upsell prompt (commercial-tier gate)
    ReadOnly,             // UI surface visible; writes blocked; per A5.4 Invariant DLF
    HardFail              // operator-targeted error; user UI surface removed; logs to audit + diagnostics
}
```

Feature-bespoke probe extension surface (for signals not in the 10 dimensions):

```csharp
public interface IFeatureBespokeProbe<TBespokeSignal> where TBespokeSignal : IBespokeSignal
{
    /// <summary>Probes a feature-specific signal not covered by the 10 Mission Space dimensions.</summary>
    ValueTask<TBespokeSignal> ProbeAsync(CancellationToken ct = default);

    /// <summary>Returns the cache TTL for this bespoke signal. Coordinator does not own the cache; the probe does.</summary>
    TimeSpan CacheTtl { get; }
}

public interface IBespokeSignal
{
    Instant CapturedAt { get; }
    string  SignalKey { get; }    // for telemetry + diagnostics
}
```

### Probe mechanics

Each of the 10 dimensions has a probe-cost classification:

| Dimension | Probe cost | Cadence | Re-evaluation triggers |
|---|---|---|---|
| Hardware | Low (local OS API) | Install + startup + on hot-plug events | OS hardware-change events; `HardwareTierChangeEvent` per ADR 0028-A8 |
| User | Low (local identity store) | Startup + on identity-change | Sign-in / sign-out; role-change events |
| Regulatory | Medium (jurisdiction probe — IP geo + user-set jurisdiction) | Install + per-session + on geo-change | Operator-set jurisdiction; geolocation API change (mobile); deferred to ADR 0064 for the full mechanics |
| Runtime | Low (local API queries) | Startup only | Process restart |
| FormFactor | Low (per ADR 0028-A5) | Install + startup + on adapter-change | Per ADR 0028-A8 `HardwareTierChangeEvent` |
| CommercialTier | High (Bridge HTTP round-trip per ADR 0009/0031) | On-demand + 1-hour cache | Subscription start/end events; explicit operator action |
| Network | Low (local network stack) | Startup + on network-change | OS network-change events; per ADR 0061 transport-tier observations |
| Trust | Low (local trust-anchor inspection) | Startup + on trust-change | New trust anchor added; trust anchor revoked |
| SyncState | Live (per ADR 0036; already a live observable) | Continuous | Per ADR 0036 sync-state-machine transitions |
| VersionVector | Medium (per ADR 0028-A6 federation handshake) | Per-handshake | New peer encountered; version vector mismatch detected |

Probe-cost classes:

- **Low:** local API call; sub-100ms wall-clock; cache TTL 1 hour
- **Medium:** local computation or trusted-source query (e.g., bundled jurisdiction database; cached transport list); 100ms–1s wall-clock; cache TTL 5 minutes
- **High:** network round-trip (Bridge subscription verification; remote feature flag); 1s–5s wall-clock; cache TTL 1 hour with stale-while-revalidate
- **Live:** continuously observed (sync state); no caching; events drive UI

### Manifest format

The Mission Envelope serializes via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` (camelCase per ADR 0028-A7.8):

```json
{
  "hardware": {
    "cpuArch": "arm64",
    "memoryBytes": 17179869184,
    "diskBytes": 274877906944,
    "displayClass": "large",
    "sensorSurface": ["camera", "mic"]
  },
  "user": {
    "userId": "...",
    "roles": ["owner", "admin"],
    "authMethods": ["password", "biometric"]
  },
  "regulatory": {
    "jurisdiction": "US-CA",
    "consentVersion": "v3",
    "appliedPolicies": ["ccpa", "fcra"]
  },
  "runtime": {
    "platform": "macos",
    "platformVersion": "15.4",
    "dotnetVersion": "11.0.0",
    "swiftVersion": null
  },
  "formFactor": { /* per ADR 0028-A5/A8 */ },
  "commercialTier": "anchor-self-host",
  "network": {
    "primaryTransport": "tier1Mdns",
    "availableTransports": ["tier1Mdns", "tier3BridgeRelay"],
    "networkPosture": "alwaysConnected"
  },
  "trust": {
    "trustAnchorCount": 1,
    "trustTier": "anchor-self-host",
    "biometricAvailable": true
  },
  "syncState": "synced",
  "versionVector": { /* per ADR 0028-A6/A7 */ },
  "capturedAt": "2026-04-30T22:00:00Z",
  "envelopeHash": "sha256:..."
}
```

**Diagnostic surface.** Anchor + Bridge expose `GET /diagnostics/mission-envelope` (admin-only) returning the current Mission Envelope. iOS exposes the envelope via the field-app's `/diagnostics` debug screen. The envelope is intentionally NOT exposed to end-users by default; it surfaces through capability gates, not through a "what your device can do" page (which is ADR 0063 territory — install-time UX).

### Re-evaluation triggers (canonical)

The coordinator MUST re-evaluate the envelope on:

1. **Hot-plug events.** Per the dimension table; OS-level hardware-change events trigger Hardware re-probe.
2. **Version upgrades.** App restart (process death + new process) triggers full re-probe (Runtime + VersionVector + others).
3. **Network-topology changes.** Per ADR 0061; each network-change event triggers Network re-probe.
4. **Jurisdiction crossings (mobile).** Per ADR 0064 (queued); geolocation-API delta beyond a threshold triggers Regulatory re-probe.
5. **Commercial-tier changes.** Subscription start/end events from Bridge per ADR 0031 trigger CommercialTier re-probe.
6. **Trust-store changes.** New trust anchor added; existing anchor revoked.
7. **FormFactor changes.** Per ADR 0028-A8 `HardwareTierChangeEvent`.

The coordinator MUST NOT re-evaluate on:

- Every ICapabilityGate.EvaluateAsync call. The gate consumes the cached envelope; the coordinator decides freshness.
- Background timer alone (no event signal). Continuous polling is anti-pattern; events drive freshness.

### Cache vs live-probe

- **Cache** is per-dimension, with TTL per the probe-cost class above.
- **Stale-while-revalidate** for High-cost dimensions: the coordinator returns the cached envelope IMMEDIATELY on `GetCurrentAsync` if any dimension is stale, and asynchronously kicks off a re-probe; when the re-probe completes, an `EnvelopeChange` event fires.
- **Force-fresh** via `ProbeAsync` (bypasses cache for all dimensions). Used by diagnostics surface; NOT used by typical capability-gate evaluation paths.
- **Cache invalidation** is event-driven only — no time-based TTL expiration (the TTL governs *when stale* the cache is, not *when discarded*); discarded only on explicit re-probe.

### Graceful-degradation taxonomy

The 5 `DegradationKind` values formalize what was previously ad-hoc:

| DegradationKind | When to use | UX surface | Audit event |
|---|---|---|---|
| `Hide` | Feature is irrelevant in this context (e.g., a Phase-3-only feature in a Phase-2 deployment). User does not need to know the feature exists. | Hide UI surface entirely; no message | `CapabilityHidden` |
| `DisableWithExplanation` | Feature is unavailable due to a recoverable condition the user can address (e.g., "camera permission needed"). | Disabled UI surface; explanation tooltip; recovery action button | `CapabilityDisabledWithExplanation` |
| `DisableWithUpsell` | Feature is unavailable due to a commercial-tier gate (e.g., "this feature requires Bridge subscription"). | Disabled UI surface; upsell prompt; "Upgrade" call-to-action | `CapabilityDisabledWithUpsell` |
| `ReadOnly` | Feature data is preserved but writes are blocked (e.g., A5.4 Invariant DLF — feature deactivated but data preserved). | Visible UI surface; read-only state; placeholder for un-decryptable fields | `CapabilityReadOnly` |
| `HardFail` | Feature failure is a substrate-tier problem the user cannot address (e.g., schema-epoch crossing rejected by ADR 0028-A6). | UI surface removed; operator-targeted error; logs to audit + diagnostics | `CapabilityHardFail` |

**UX surface conformance:** All 5 DegradationKind values produce UI per paper §13.2's visibility tables + ADR 0036's sync-state ARIA roles. Specifically:

- `DisableWithExplanation` MUST use ADR 0036's `aria-live=polite` for the recovery action's call-out
- `DisableWithUpsell` MUST conform to paper §13.2's commercial-tier surface conventions (no surprise paywalls; explicit upsell CTA visible at all times)
- `ReadOnly` MUST conform to ADR 0028-A8.3 rule 7 (field-level redaction default; record-level fallback)
- `HardFail` MUST emit to the audit substrate per ADR 0049

### User-communication policy

The protocol distinguishes **expected** vs **unexpected** capability changes:

- **Expected** (operator-initiated; e.g., user revokes camera permission in OS settings): the protocol re-evaluates silently; the next render of the relevant UI surface reflects the new state via `DegradationKind`. NO toast / modal / popup.
- **Unexpected** (substrate-detected; e.g., schema-epoch crossing detected at handshake time): the protocol fires an `EnvelopeChange` event with `Severity = Critical`; the UI subscribes via ADR 0036's sync-state surface and renders an inline banner per ADR 0036 conventions. NO toast / modal — paper §13.2 explicitly forbids surprise modals.
- **Recoverable** (the change is unexpected but the user can act, e.g., "Bridge subscription expired"): the protocol fires `EnvelopeChange` with `Severity = Warning` + a `RecoveryAction` field; the UI renders a banner with the recovery CTA. The banner persists until dismissed or until the recovery is taken.
- **Informational** (the change is expected but worth noting, e.g., "you're now in offline mode"): the protocol fires `EnvelopeChange` with `Severity = Informational`; the UI may render a transient toast (3s default) per existing ADR 0036 conventions; no persistent banner.

Severity levels:

```csharp
public enum EnvelopeChangeSeverity
{
    Informational,    // toast OK; no persistent UI
    Warning,          // persistent banner with recovery action
    Critical          // persistent banner; operator-targeted; cannot be dismissed without acknowledgment
}
```

### Per-feature force-enable surface

Operators may force-enable a capability that the negotiation protocol has determined is unavailable. The force-enable surface is INTENTIONALLY not surfaced to end-users:

```csharp
public interface ICapabilityForceEnableSurface
{
    /// <summary>
    /// Force-enables a capability against the negotiation protocol's verdict.
    /// Requires operator authentication + audit emission. Persists until explicitly revoked.
    /// </summary>
    ValueTask ForceEnableAsync<TCapability>(
        CapabilityForceEnableRequest request,
        CancellationToken ct = default
    ) where TCapability : ICapability;

    /// <summary>Revokes a previously-applied force-enable.</summary>
    ValueTask ForceRevokeAsync<TCapability>(CancellationToken ct = default) where TCapability : ICapability;

    /// <summary>Lists currently-active force-enables.</summary>
    ValueTask<IReadOnlyList<ForceEnableRecord>> ListActiveAsync(CancellationToken ct = default);
}

public sealed record CapabilityForceEnableRequest(
    string CapabilityKey,
    string Justification,
    Instant? ExpiresAt    // optional auto-expiry; null = persistent until ForceRevokeAsync
);
```

Force-enable emits `AuditEventType.CapabilityForceEnabled` with `(capability_key, justification, expires_at, operator_id)`. The UI surface is operator-only — accessible via `Anchor → Settings → Advanced → Force-enable capabilities` and `Bridge → Admin → Force-enable capabilities`. End-users never see this surface in the default UX.

When a force-enable is active, capability gates that would have returned `Unavailable` instead return `DegradedAvailable` with `DegradationKind = DisableWithExplanation` and a `UserMessage` indicating the capability is force-enabled (so end-users see a "Force-enabled by admin" indicator on the affected feature, not a hidden state).

### Telemetry shape

Capability-cohort analytics emit through the existing audit substrate (ADR 0049):

- `CapabilityProbed` — per probe execution; payload `(capability_key, dimensions_consulted, verdict_state, latency_ms)`. Used for probe-cost analysis + capability-cohort baseline.
- `CapabilityChanged` — per gate verdict change (Available → Unavailable or vice versa); payload `(capability_key, previous_state, current_state, dimensions_changed, change_reason)`. Used for cohort transition analysis.
- `CapabilityProbeFailure` — per probe error; payload `(capability_key, error_kind, error_message, dimensions_attempted)`. Used for substrate health monitoring.

Audit dedup per ADR 0028-A6.5.1 pattern: `CapabilityProbed` capped at 1-per-(capability_key)-per-5-minute-window; `CapabilityChanged` capped at 1-per-(capability_key, transition)-per-1-minute-window; `CapabilityProbeFailure` no dedup (each failure is independently surfaced).

**Capability-cohort analytics aggregation:** the audit substrate aggregates the events into a `capability_cohort_baseline.csv` snapshot exported nightly (per ADR 0049's snapshot mechanism); the snapshot drives roadmap decisions like "60% of Anchor users lack feature X's hardware prereq → deprioritize feature X polish." Aggregation is at the audit layer, not the negotiation-protocol layer; ADR 0062 ships only the per-event emission contract.

### Negotiation protocol participants

The protocol has three participant roles:

1. **Coordinator** (`IMissionEnvelopeProvider`) — owns the envelope; runs probes; emits change events.
2. **Gates** (`ICapabilityGate<TCapability>`) — consume the envelope; produce verdicts; subscribe to change events.
3. **Probes** (`IDimensionProbe<TDimension>` for the 10 canonical dimensions; `IFeatureBespokeProbe<TBespokeSignal>` for feature-specific signals) — execute the actual probing logic; called by the coordinator.

Probe contracts for the 10 dimensions:

```csharp
public interface IDimensionProbe<TDimension> where TDimension : IDimension
{
    /// <summary>Probes the dimension and returns its current state. Called by the coordinator on cache-miss or forced re-probe.</summary>
    ValueTask<TDimension> ProbeAsync(CancellationToken ct = default);

    /// <summary>The probe-cost class; governs caching policy in the coordinator.</summary>
    ProbeCostClass CostClass { get; }

    /// <summary>The set of envelope dimensions this probe affects (for change-event filtering).</summary>
    IReadOnlySet<DimensionChangeKind> AffectsDimensions { get; }
}

public enum ProbeCostClass { Low, Medium, High, Live }
```

Each of the 10 dimensions ships a default probe implementation in `Sunfish.Foundation.MissionSpace.Probes.*`; adapters MAY substitute their own implementation via DI. iOS and Anchor MAUI likely substitute several dimensions (e.g., the `IDimensionProbe<HardwareCapabilities>` for iOS uses iOS-specific OS APIs; the Anchor MAUI version uses .NET APIs).

---

## Consequences

### Positive

- **Single source of truth for capability state.** Every feature gate consumes the same envelope; UX consistency is enforced by the protocol rather than by per-feature convention.
- **Capability-cohort analytics are uniform.** Every gate emits the same telemetry shape; product roadmap decisions drive on consistent data.
- **Re-evaluation is centralized + event-driven.** No more per-feature timers polling redundantly; re-eval happens once and broadcasts.
- **Force-enable is opt-in + auditable.** Operators get the override; end-users never see it as default surface; audit trail is preserved.
- **Graceful-degradation taxonomy is explicit.** 5 named `DegradationKind` values replace ad-hoc per-feature "what should we do when this feature is gone?" decisions.
- **Conforms to existing primitives.** Paper §13.2 visibility tables + ADR 0036 sync-state ARIA roles + ADR 0041 rich-vs-MVP surface — all consumed; none replaced.
- **Substrate-tier work is unblocked.** ADR 0063 (Mission Space Requirements; install-time UX) can proceed once ADR 0062 lands; ADR 0064 (Runtime Regulatory Policy Evaluation) extends 0062's probe mechanics.

### Negative

- **New substrate package + 4 new types.** `Sunfish.Foundation.MissionSpace` adds package count; `IMissionEnvelopeProvider` + `ICapabilityGate<TCapability>` + `IFeatureBespokeProbe<TBespokeSignal>` + `IDimensionProbe<TDimension>` add type count.
- **Migration cost.** Every existing feature that has its own probe + cache + UI logic must migrate to the protocol. Migration is gradual — gates that aren't migrated continue to work; they just don't get the cohort analytics benefit until they migrate.
- **Coordinator becomes a substrate-tier dependency.** Every package that consumes capability state now depends on `Sunfish.Foundation.MissionSpace`. Dependency-graph review at ADR 0062 hand-off is mandatory.
- **Two-tier reasoning at gate authoring.** Gate authors must decide whether their signal is a Mission Space dimension (use coordinator) or feature-bespoke (use `IFeatureBespokeProbe`). Misclassification → cohort-analytics gaps.

### Trust impact / Security & privacy

- **Force-enable is operator-only + audited.** No end-user surface; no silent override path. Force-enable records persist until explicitly revoked.
- **Telemetry payload contains capability keys, not user data.** `CapabilityProbed` audit payload is `(capability_key, dimensions_consulted, verdict_state, latency_ms)` — no PII.
- **Mission Envelope is internal-only by default.** `GET /diagnostics/mission-envelope` is admin-only; end-user surfaces consume gate verdicts, not the envelope directly.
- **Probe-cost classes prevent denial-of-service.** A misbehaving feature requesting `ProbeAsync` repeatedly is rate-limited at the coordinator (1-per-second cap on force-fresh probes per process); legitimate probes go through `GetCurrentAsync` (cached).

---

## Compatibility plan

### Existing callers

No existing callers of the to-be-introduced protocol; this is a substrate-tier introduction. Existing per-feature probes continue to work; migration is pull-based per feature gate.

**Migration order (recommended):**

1. **Phase 1:** ship `Sunfish.Foundation.MissionSpace` substrate (types + default probe implementations + coordinator + DI extension).
2. **Phase 2:** migrate ADR 0036 sync-state surface to consume `IMissionEnvelopeProvider` for sync-state observation.
3. **Phase 3:** migrate ADR 0028-A6/A7 + A5/A8 surfaces to expose their relations through `IDimensionProbe<VersionVector>` + `IDimensionProbe<FormFactorProfile>`.
4. **Phase 4:** migrate ADR 0041 rich-vs-MVP surface to consume `ICapabilityGate<TCapability>` verdicts.
5. **Phase 5+:** per-feature opt-in migrations; not gated.

### Affected packages

- New: `packages/foundation-mission-space/` — substrate types + default probes + DI extension.
- Modified: `packages/foundation-localfirst/` — ADR 0036 sync-state observation refactored to expose `IDimensionProbe<SyncStateSnapshot>`.
- Modified: `packages/foundation-recovery/` — version-vector + form-factor relation exposed via `IDimensionProbe<VersionVector>` + `IDimensionProbe<FormFactorProfile>`.
- Modified: `packages/ui-core/` — degradation primitive consumes `CapabilityVerdict` instead of ad-hoc per-feature inputs.

### Migration

Per-feature gate migration follows a uniform pattern:

```csharp
// Before (status quo):
public sealed class MyFeatureService
{
    public async Task<bool> IsAvailableAsync()
    {
        // ad-hoc probe logic; ad-hoc caching; ad-hoc telemetry
    }
}

// After (post-ADR-0062):
public sealed class MyFeatureCapabilityGate : ICapabilityGate<MyFeatureCapability>
{
    public async ValueTask<CapabilityVerdict> EvaluateAsync(MissionEnvelope envelope, CancellationToken ct)
    {
        if (!envelope.CommercialTier.IsAtLeast("bridge-anchor")) {
            return new CapabilityVerdict(
                State: CapabilityState.Unavailable,
                DegradationKind: DegradationKind.DisableWithUpsell,
                UserMessage: "MyFeature requires Bridge subscription.",
                OperatorRecoveryAction: "Upgrade to Bridge tier in Bridge admin.",
                RelevantDimensions: [DimensionChangeKind.CommercialTier]
            );
        }
        // ... additional dimensional checks ...
        return CapabilityVerdict.Available;
    }
}
```

The pre-existing service continues to work (no code change required); the new gate ships in parallel; consumers gradually migrate from the service-direct call to gate-mediated evaluation.

---

## Implementation checklist

- [ ] `Sunfish.Foundation.MissionSpace` package scaffolded (per `Sunfish.Blocks.*` precedent)
- [ ] Core types: `MissionEnvelope`, `IMissionEnvelopeProvider`, `IMissionEnvelopeObserver`, `EnvelopeChange`, `DimensionChangeKind`
- [ ] Gate types: `ICapabilityGate<TCapability>`, `CapabilityVerdict`, `CapabilityState`, `DegradationKind`, `EnvelopeChangeSeverity`
- [ ] Probe types: `IDimensionProbe<TDimension>`, `IFeatureBespokeProbe<TBespokeSignal>`, `ProbeCostClass`, `IDimension`, `IBespokeSignal`
- [ ] Force-enable: `ICapabilityForceEnableSurface`, `CapabilityForceEnableRequest`, `ForceEnableRecord`
- [ ] Default coordinator implementation: `DefaultMissionEnvelopeProvider` with per-dimension cache + stale-while-revalidate
- [ ] 10 default dimension probes (one per dimension; cite ADR 0028-A6/A7 + A5/A8 + 0036 + 0049 dependencies)
- [ ] DI extension: `AddSunfishMissionSpace()` registering the coordinator + 10 default probes
- [ ] 6 new `AuditEventType` constants:
  - `CapabilityProbed`
  - `CapabilityChanged`
  - `CapabilityProbeFailure`
  - `CapabilityForceEnabled`
  - `CapabilityForceRevoked`
  - `EnvelopeChangeBroadcast` (for observability of the coordinator's change-event broadcast)
- [ ] `MissionSpaceAuditPayloadFactory` (mirrors A6.6 / A8.5 patterns)
- [ ] Audit dedup at the emission boundary per A6.5.1 pattern
- [ ] Test coverage:
  - 10 per-dimension probe tests (one per dimension)
  - 5 graceful-degradation tests (one per `DegradationKind`)
  - Coordinator cache + stale-while-revalidate test
  - Force-enable + force-revoke round-trip test
  - Audit dedup tests for the 6 new event types
  - Integration test: per-feature gate consumes envelope; verdict drives UI per `DegradationKind`
- [ ] Cited-symbol verification per the 3-direction spot-check rule (positive + negative + structural-citation)
- [ ] `apps/docs/foundation/mission-space/` walkthrough page

---

## Open questions

- **OQ-0062.1:** Should `MissionEnvelope` be a single immutable record, or a delta-based observable? Recommend immutable record for v0 simplicity; deltas are observable via `EnvelopeChange`. Revisit if change events arrive faster than v0 allows.
- **OQ-0062.2:** Should the coordinator support per-tenant Mission Envelopes (multi-tenant Bridge case) or a single envelope per process? Recommend per-process for v0; per-tenant complicates the protocol and Bridge tenants typically share runtime/network surfaces. Revisit when Bridge multi-tenant features need per-tenant capability variation.
- **OQ-0062.3:** Should `IFeatureBespokeProbe<TBespokeSignal>` results be cached by the coordinator or by the probe itself? Recommend by the probe (per the `CacheTtl` field) for v0; coordinator-cached complicates the bespoke surface. Revisit if per-feature probe caching diverges to inconsistent behavior across features.
- **OQ-0062.4:** What's the right granularity for `DegradationKind = Hide` audit emission? If the feature is hidden, the user never sees it — but the operator may want to know the capability was probed and produced `Hide`. Recommend emit `CapabilityProbed` always; emit `CapabilityChanged` only on transitions; don't emit anything bespoke for `Hide`.
- **OQ-0062.5:** Does the protocol need a "soft probe" mode where the coordinator skips High-cost dimensions (e.g., when the user is offline and Bridge cannot be reached)? Recommend yes — the coordinator returns the cached envelope for High-cost dimensions when offline; explicitly stale; surfaces the `Unreachable` state. Spec the offline behavior as part of A1 follow-up if real-world testing exposes the gap.

---

## Revisit triggers

- **Migration cost exceeds estimate.** If feature-by-feature migration to gates exceeds ~2x the pre-ADR-0062 cost, revisit the gate-authoring ergonomics in an A1 amendment.
- **Capability-cohort analytics surface a use case the audit dedup blocks.** Recommend revisit if 5-minute dedup on `CapabilityProbed` produces false-negative cohort signals.
- **Force-enable surface is misused.** If audit shows force-enable is used as the default workaround for capability bugs, revisit the gate-authoring ergonomics — the gates may be too rigid.
- **Per-tenant variation surfaces.** If Bridge multi-tenant features need per-tenant capability variation, revisit OQ-0062.2.

---

## References

- W#33 Mission Space Matrix discovery: [`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`](../../icm/01_discovery/output/2026-04-30_mission-space-matrix.md) §5.6 + §6.2 + §7.2
- Intake: [`icm/00_intake/output/2026-04-30_mission-space-negotiation-protocol-intake.md`](../../icm/00_intake/output/2026-04-30_mission-space-negotiation-protocol-intake.md)
- ADR 0028 (CRDT Engine Selection) — A5/A8 (FormFactorProfile per cross-form-factor migration); A6/A7 (VersionVector per compatibility relation)
- ADR 0036 — Sync-state surface (consumed by `SyncState` dimension)
- ADR 0041 — Rich-vs-MVP UI degradation primitive
- ADR 0049 — Audit substrate (telemetry emission target)
- ADR 0061 — Three-tier peer transport (consumed by `Network` dimension)
- Paper [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) §13.2 — AP/CP visibility tables
- DirectX Feature Levels documentation (closest engineering analog to enumerated capability tiers)
- RFC 5939 — SDP capability negotiation (one of the considered prior arts)

---

## Cohort discipline

Per `feedback_decision_discipline.md` cohort batting average (12-of-12 substrate amendments needing council fixes; structural-citation failure rate 5-of-12 XO-authored amendments — all caught pre-merge):

- **Pre-merge council canonical** for ADR 0062. Auto-merge intentionally DISABLED until a Stage 1.5 council subagent reviews. Council should specifically pressure-test:
  - The 10-dimension partition — are the dimensions cleanly separated, or do some bleed into others (e.g., is FormFactor genuinely distinct from Hardware in this protocol's semantics)?
  - The probe-cost classification — is "1-hour cache TTL for High-cost" right, or does Bridge-subscription verification need a shorter TTL given billing-cycle UX expectations?
  - The graceful-degradation taxonomy — are 5 `DegradationKind` values exhaustive, or are there real cases that don't fit?
  - The force-enable surface — is operator-only the right scope, or should some capabilities (e.g., debug-mode features) be self-service for users?
  - The user-communication policy — does the "no surprise modals" rule (per paper §13.2) hold under all 4 EnvelopeChangeSeverity levels, or do Critical changes ever justify modal interruption?
  - The migration cost — is the 5-phase migration plan realistic, or does it underestimate per-feature gate-authoring effort?
- **Cited-symbol verification** per the implementation checklist (every cited Sunfish.* symbol verified; every cited ADR Accepted on origin/main; every field-on-type claim structurally verified per the A7 lesson)
- **Standing rung-6 spot-check** within 24h of ADR 0062 merging (per ADR 0028-A4.3 + A7.12 + A8.12 commitment)

---

## Amendments (post-acceptance, 2026-04-30 council)

### A1 (REQUIRED, mechanical) — 0062 council-review fixes

**Driver:** Stage 1.5 adversarial council review of ADR 0062 at `icm/07_review/output/adr-audits/0062-council-review-2026-04-30.md` (PR #408, merged 2026-04-30) returned verdict **B (low-B) with 10 required + 4 encouraged amendments**. Severity profile: **2 Critical (F1 + F2; F1 is structural-citation A7-third-direction class) + 8 Major + 4 Minor/Encouraged + 7 verification-passes**. Per `feedback_decision_discipline` Rule 3, mechanical council fixes auto-accept; A1 absorbs all 14 recommendations into ADR 0062's surface before Phase 1 substrate work begins.

**Cohort milestone:** ADR 0062 council brings cohort batting average to **13-of-13 substrate amendments needing post-acceptance fixes**. Structural-citation failure rate (XO-authored): **6-of-13 (~46%)** — F1 (ADR 0041 mis-cited) + F8 (ADR 0031 + ADR 0009 mis-cited) are the 6th and 7th instances; both caught pre-merge.

**Reading note:** A1 is a corrections layer above the original ADR 0062 surface. The original §"Decision / Initial contract surface" is preserved as authored; the renames + additions below are authoritative for downstream consumers (Phase 1 substrate scaffold uses the post-A1 surface).

#### A1.1 — Drop ADR 0041 citation; restate antecedents (council A1 / F1 Critical structural-citation)

ADR 0041 is the **component-pair-coexistence policy** (governing rich-vs-MVP coexistence under foundation-localfirst's `IComponentPairing`), NOT a "rich-vs-MVP UI degradation primitive." The mis-citation in §"Context", §"Decision drivers" (bullet 5), §"Compatibility plan / Migration order / Phase 4", and §"References" is corrected:

- Strike *"ADR 0041 specifies the rich-vs-MVP UI degradation primitive."*
- Replace with: *"the runtime graceful-degradation taxonomy is what ADR 0062 itself introduces; the closest in-repo predecessors are paper §13.2 (visibility tables) + ADR 0036 (sync-state encoding contract) — both surface-treatment ADRs, not degradation primitives."*
- Migration order Phase 4 is **dropped**; remaining phases renumber Phase 5+ → Phase 4+.
- §"References" — ADR 0041 removed.

#### A1.2 — Rename `ICapability`/`ICapabilityGate` → `IFeature`/`IFeatureGate` (council A2 / F2 Critical naming-collision)

The `Foundation.Capabilities.ICapabilityGraph` namespace already exists; introducing `Foundation.MissionSpace.ICapability` would produce import-resolution ambiguity at build time. Rename:

```csharp
// Renames (post-A1):
public interface IFeature {}                                           // was: ICapability
public interface IFeatureGate<TFeature> where TFeature : IFeature      // was: ICapabilityGate<TCapability>
public sealed record FeatureVerdict(...);                              // was: CapabilityVerdict
public enum FeatureAvailabilityState { Available, DegradedAvailable, Unavailable };  // was: CapabilityState
public interface IFeatureForceEnableSurface                            // was: ICapabilityForceEnableSurface
public sealed record FeatureForceEnableRequest(...)                    // was: CapabilityForceEnableRequest
```

The `MissionEnvelope` + `IMissionEnvelopeProvider` + `EnvelopeChange` + `DimensionChangeKind` + `DegradationKind` + `EnvelopeChangeSeverity` types stay (they're not "capability"-named).

The 6 new `AuditEventType` constants rename:

| Original (per original ADR 0062) | Renamed (post-A1) |
|---|---|
| `CapabilityProbed` | `FeatureProbed` |
| `CapabilityChanged` | `FeatureAvailabilityChanged` |
| `CapabilityProbeFailure` | `FeatureProbeFailed` |
| `CapabilityForceEnabled` | `FeatureForceEnabled` |
| `CapabilityForceRevoked` | `FeatureForceRevoked` |
| `EnvelopeChangeBroadcast` | `MissionEnvelopeChangeBroadcast` |

Side-benefit: matches ADR 0009's existing `FeatureKey` / `FeatureSpec` / `IFeatureEvaluator` vocabulary. ADR 0009 added to §"References" as the runtime-feature-availability antecedent (replacing the dropped ADR 0041 citation).

#### A1.3 — Replace `Instant` with `DateTimeOffset` everywhere (council A3 / F3 Major)

NodaTime's `Instant` is not used anywhere in `packages/`; substrate convention is `DateTimeOffset`. Replace in `MissionEnvelope.CapturedAt`, `IBespokeSignal.CapturedAt`, `FeatureForceEnableRequest.ExpiresAt`. JSON shape stays identical (ISO-8601 string round-trippable via either type).

#### A1.4 — Coordinator concurrency semantics (council A4 / F4 Major)

New sub-section appended after §"Cache vs live-probe":

> **Single-flight on cache-miss.** When N concurrent callers request `GetCurrentAsync` and the relevant dimension's cache is stale, the coordinator launches **1** probe; all N callers await the same probe completion. Implementation: per-dimension `Lazy<Task<TDimension>>` reset on cache-invalidation event.
>
> **Per-cost-class wall-clock timeout.** Each probe has a maximum wall-clock budget: Low: 1s; Medium: 2s; High: 5s; DeepHigh: 10s; Live: N/A. On timeout, the coordinator returns the last-known-cached value (or `Unreachable` sentinel if no prior value), emits `FeatureProbeFailed` audit, and treats the dimension as `ProbeStatus.Failed` (per A1.10) until a successful re-probe.
>
> **Observer fanout policy.** `IDisposable Subscribe(IMissionEnvelopeObserver)` registers an observer; the coordinator fans out `OnEnvelopeChangedAsync` calls concurrently (fire-and-forget; no back-pressure to the change source). Each observer's queue is bounded at 100 pending change events; observers exceeding the bound drop oldest-first with a `MissionEnvelopeObserverOverflow` audit event. Dimension changes within a 100ms coalescing window are merged into a single `EnvelopeChange` (ChangedDimensions list is the union; Previous is the envelope at the start of the window; Current is the envelope at coalescing-flush time).

#### A1.5 — Tighten DirectX-FL prior-art rationale; engage Vulkan + RFC 5939 (council A5 / F5 Major)

§"Decision drivers" bullets 2 + 3 rewritten:

> **Industry prior art is multi-source.** DirectX Feature Levels (FL_9_1 / FL_10_0 / FL_11_0 / FL_12_0) inspire the *enumerated-not-arithmetic* + *runtime-queryable* + *developer-owns-graceful-degradation* properties — but Mission Space explicitly does NOT inherit DirectX's *single-axis* or *OS-guarantees-joint-stability* properties (Mission Space is multi-axis; dimensions flip independently; no joint-stability guarantee). Vulkan's `VkPhysicalDeviceFeatures` (~50 boolean flags per device, each independently queryable, gates `vkCmd*` calls) is the closer multi-axis analog; Mission Space's 10 dimensions trade off finer-granularity (Vulkan) vs coarser-aggregation (DirectX) at a deliberate intermediate point. SDP RFC 5939's offer/answer pattern is engaged with in Option C — Mission Space's coordinator-owns-state model is preferred over SDP's pure-publish/subscribe because diagnostics + telemetry-shape + operator-debugging all benefit from a state record (not just a change-event stream).

§"Considered options / Option C" gains:

> **Comparison to RFC 5939 (offer/answer):** RFC 5939 provides offer/answer semantics with both a state record (the offer/answer pair) AND change events (re-INVITE re-negotiations) — a hybrid that solves Option C's state-record gap on its face. **However** — RFC 5939's state record is per-session (an SDP body inside a SIP transaction); Mission Space's needs are per-process (one envelope governing N feature-gates). Per-session state forces every gate to negotiate its own envelope-fetch; Mission Space's coordinator-owns-state model centralizes the negotiation. The cost of RFC 5939's per-session-state is exactly what Option C inherits + Mission Space rejects.

#### A1.6 — `EditionCapabilities` cache TTL realism + Bridge subscription-event-emitter halt-condition (council A6 / F6 Major)

§"Probe mechanics / Probe-cost classes" — split `High` into two cost classes:

```
- High: network round-trip; 1s–5s wall-clock; cache TTL 30 seconds with stale-while-revalidate (default for billing-cycle-sensitive dimensions where users expect sub-minute reflection of changes — e.g., subscription state)
- DeepHigh: network round-trip; 1s–5s wall-clock; cache TTL 1 hour with stale-while-revalidate (for genuinely-rare-changing remote signals — e.g., feature-flag rollout where eventual consistency is acceptable)
```

`EditionCapabilities` (renamed from `CommercialTier` per A1.8) is `High` (30-second TTL).

§"Re-evaluation triggers" — bullet 5 replaced:

> **Edition / commercial-tier changes.** ADR 0031 has not yet specified a Bridge → Anchor subscription-event-emitter contract. ADR 0062 Stage 06 build cannot ship an `EditionCapabilities` probe with sub-minute responsiveness UNTIL ADR 0031 is amended to add such a contract OR the 30-second cache TTL is accepted as the operational ceiling. **Halt-condition:** Phase 1 of the migration may NOT begin until either (a) the 30-second TTL is operationally acceptable per the Phase 1 acceptance review, or (b) ADR 0031 has been amended to specify the subscription-event-emitter contract.

#### A1.7 — Probe dependencies (council A7 / F7 Major)

New sub-section after §"Probe mechanics / Probe-cost classes":

> **Probe dependencies.** Some probes depend on other probes' results:
>
> | Dimension | Depends on | Behavior on dependency-unavailable |
> |---|---|---|
> | `Regulatory` (jurisdiction-from-IP-geo subset) | `Network` (online state) | Coordinator falls back to user-set jurisdiction; emits `FeatureProbeFailed` for the IP-geo subset; `Regulatory` returns with `ProbeStatus.PartiallyDegraded` |
> | `EditionCapabilities` (commercial tier) | `Network` (online state) | Coordinator returns last-known-good value with `ProbeStatus.Stale` if cache age < 24h; otherwise `ProbeStatus.Unreachable` and `EditionCapabilities` defaults to `anchor-self-host` |
> | `VersionVector` | `Network` + `Trust` | Coordinator returns last-known-good value with `ProbeStatus.Stale`; gates consulting `VersionVector` produce `DegradationKind.HardFail` if `ProbeStatus.Unreachable` |
> | `User` (biometric-auth-method subset) | `Hardware` (biometric sensor surface) | Coordinator returns user-without-biometric-method; emits `FeatureProbeFailed` for the biometric subset |
> | All other dimensions | None | N/A |
>
> The coordinator runs probes in topologically-sorted dependency order at startup + on full re-probe (`ProbeAsync`); probes within a dependency-level run in parallel; failures cascade per the table above. The `ProbeStatus` enum is `Healthy / Stale / Failed / PartiallyDegraded / Unreachable` (see A1.10); each `<TDimension>` record carries its own `ProbeStatus`.

#### A1.8 — Rename `CommercialTier` → `EditionCapabilities`; rename `Trust` → `TrustAnchorCapabilities`; drop ADR 0031 citation (council A8 / F8 Critical structural-citation)

ADR 0031 has no subscription-event-emitter contract; ADR 0009 uses `Edition` not "trust tier" / "commercial tier". Renames:

```csharp
// Post-A1 MissionEnvelope members:
EditionCapabilities     Edition,        // per ADR 0009 (Edition / IEditionResolver)
TrustAnchorCapabilities Trust,          // local-trust-anchor inspection (no in-repo predecessor; new in 0062)
```

`DimensionChangeKind` enum: `CommercialTier` → `Edition`. `IDimensionProbe<CommercialCapabilities>` → `IDimensionProbe<EditionCapabilities>`. JSON example field rename: `"commercialTier"` → `"edition"`. ADR 0031 removed from §"References" (paper §17.2 Bridge-relay-substrate is the closer paper antecedent if needed). `TrustAnchorCapabilities` is explicitly new in ADR 0062 (no in-repo predecessor).

#### A1.9 — `ForceEnablePolicy` taxonomy per dimension (council A9 / F9 Major)

§"Per-feature force-enable surface" gains:

> **Force-enable policy per dimension.** The force-enable surface is gated by a `ForceEnablePolicy` per dimension:
>
> | Dimension | ForceEnablePolicy | Force-enable verdict |
> |---|---|---|
> | `Hardware` | `NotOverridable` | `ForceEnableAsync` rejected; throws `ForceEnableNotPermittedException("Hardware-driven Unavailable cannot be force-enabled; the substrate cannot conjure capabilities the device lacks.")` |
> | `Runtime` | `NotOverridable` | Same shape as Hardware. |
> | `Regulatory` | `OverridableWithCaveat` | Force-enable produces `DegradedAvailable` + UX surface naming legal/regulatory consequence ("Force-enable acknowledges the operator assumes responsibility for jurisdictional non-compliance.") |
> | `EditionCapabilities` | `OverridableWithCaveat` | Force-enable produces `DegradedAvailable` + UX surface naming the contractual consequence ("Force-enable bypasses subscription gating; usage may incur Bridge-tier costs not covered by current subscription.") |
> | `User`, `Network`, `Trust`, `SyncState`, `VersionVector`, `FormFactor` | `Overridable` | Force-enable produces `DegradedAvailable` per the existing rule. |
>
> `IFeatureForceEnableSurface.ForceEnableAsync<TFeature>` checks the relevant dimension's `ForceEnablePolicy` before applying; rejection emits `FeatureForceEnableRejected` audit event (8th new constant).

#### A1.10 — `ProbeStatus` + `EnvelopeChangeSeverity.ProbeUnreliable` (council A10 / F10 Major)

```csharp
public enum ProbeStatus
{
    Healthy,            // probe succeeded; result is fresh
    Stale,              // probe succeeded earlier; result is stale per cache TTL but the dimension's value is still trusted
    Failed,             // probe attempted; threw / timed out; last-known-good value returned per A1.4 amendment
    PartiallyDegraded,  // probe succeeded but a sub-component failed (e.g., Regulatory IP-geo subset failed; user-set subset fine)
    Unreachable         // probe not attempted (e.g., dependency unavailable per A1.7); last-known-good or sentinel returned
}
```

Each `<TDimension>` record carries `ProbeStatus Status { get; }` as an additional field (Stage 06 contract change to the 10 dimension records).

```csharp
public enum EnvelopeChangeSeverity
{
    Informational,
    Warning,
    Critical,
    ProbeUnreliable     // (NEW) coordinator could not produce a fresh probe; UI surfaces a "diagnostics check required" indicator per ADR 0036's quarantine state
}
```

UX: same as Critical (persistent banner, operator-targeted) but the recovery action is "Open diagnostics" rather than "Acknowledge."

#### A1.11 — `LocalizedString` for `UserMessage` + `OperatorRecoveryAction` (council A11 / F11 Encouraged)

```csharp
public sealed record LocalizedString(
    string Key,             // localization key; rendering layer resolves against active framework
    string DefaultValue     // fallback English string if key is missing
);
```

`FeatureVerdict.UserMessage` and `FeatureVerdict.OperatorRecoveryAction` become `LocalizedString?`. Anchor MAUI uses .resx; Bridge React uses i18next; iOS uses .strings; rendering layer consumes `LocalizedString` and resolves per its framework convention.

#### A1.12 — `FeatureVerdictSurfaced` audit event (council A12 / F12 Encouraged)

9th new `AuditEventType` constant: `FeatureVerdictSurfaced` — emitted at UI-render time (NOT at gate-evaluate time) when a verdict is surfaced to a user-visible UI element; payload `(feature_key, verdict_state, degradation_kind, surface_id)`. Used for product-roadmap analytics ("did the user actually see this verdict, or was it gated behind a route-change before render?"). Audit dedup: `FeatureVerdictSurfaced` capped at 1-per-(feature_key, surface_id)-per-30-second-window.

#### A1.13 — Reword "no surprise modals" attribution (council A13 / F13 Encouraged)

In §"User-communication policy", replace:

> NO toast / modal — paper §13.2 explicitly forbids surprise modals.

with:

> NO toast / modal for unexpected substrate-detected changes. Consistent with paper §13.1 "Complexity Hiding Standard" + §13.2's framing of UX as "non-intrusive under normal conditions, informative under degraded ones." Banners — not modals — are the protocol's surface for user-actionable changes; the visual distinction: banners occupy a fixed top-of-screen region; do not block click-through to the application; can be acknowledged inline.

#### A1.14 — §"Cited-symbol audit" added at the head of the ADR (council A14 / F14 Encouraged)

Mirroring ADR 0028-A8.11 / A5.9 / A7.13, a §"A0 cited-symbol audit" is added immediately after §"Context", listing every Sunfish.* symbol cited + every cited ADR + every cited paper section, classified `Existing` / `Introduced by ADR 0062 (post-A1)` / `Removed by ADR 0062 (post-A1)`. The seed for this section is the council review's §2 verification-passes (F15-F21) + the renames in A1.2 / A1.8.

**Existing (verified on origin/main, post-spot-check):**

- ADR 0009 (Edition / IEditionResolver / FeatureKey / FeatureSpec / IFeatureEvaluator) — adopted in A1.2 + A1.8
- ADR 0028 (post-A8 FormFactorProfile + post-A7 VersionVector) — verified per F17 + F18
- ADR 0036 (5 sync states; ARIA roles) — verified per F16
- ADR 0049 (audit substrate; emission-boundary dedup) — verified per F19
- ADR 0061 (three-tier transport; Network dimension) — existing
- Paper §13.1 (Complexity Hiding Standard) + §13.2 (visibility tables) — verified per F15
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` — verified existing per F20-context
- `Sunfish.Kernel.Audit.AuditEventType` — verified existing per F20

**Introduced by ADR 0062 (post-A1):**

- New package: `Sunfish.Foundation.MissionSpace`
- New types: `MissionEnvelope`, `IMissionEnvelopeProvider`, `IMissionEnvelopeObserver`, `EnvelopeChange`, `DimensionChangeKind`, `IFeature`, `IFeatureGate<TFeature>`, `FeatureVerdict`, `FeatureAvailabilityState`, `DegradationKind`, `EnvelopeChangeSeverity` (with `ProbeUnreliable`), `IDimensionProbe<TDimension>`, `ProbeCostClass` (Low / Medium / High / DeepHigh / Live), `IFeatureBespokeProbe<TBespokeSignal>`, `IBespokeSignal`, `IFeatureForceEnableSurface`, `FeatureForceEnableRequest`, `ForceEnableRecord`, `ForceEnablePolicy`, `ProbeStatus`, `LocalizedString`, `TrustAnchorCapabilities`, `EditionCapabilities` (consumed from ADR 0009), `ForceEnableNotPermittedException`
- 9 new `AuditEventType` constants (per A1.2 renames + A1.9 + A1.12): `FeatureProbed`, `FeatureAvailabilityChanged`, `FeatureProbeFailed`, `FeatureForceEnabled`, `FeatureForceRevoked`, `FeatureForceEnableRejected`, `MissionEnvelopeChangeBroadcast`, `MissionEnvelopeObserverOverflow`, `FeatureVerdictSurfaced`

**Removed by ADR 0062 (post-A1):**

- The original ADR 0041 citation as a "rich-vs-MVP UI degradation primitive" — removed per A1.1.
- The original ADR 0031 citation as a subscription-event-emitter — removed per A1.8 (until ADR 0031 is amended to add such a contract).
- The original `ICapability` / `ICapabilityGate` / `CapabilityVerdict` / `CapabilityState` / `ICapabilityForceEnableSurface` / `CapabilityForceEnableRequest` types — renamed per A1.2.
- The original 6-of-the-original-AuditEventType-naming — renamed per A1.2 (now 9 total constants post-A1).

#### A1.15 — Cohort discipline log

Per `feedback_decision_discipline.md` cohort batting average:

- **Substrate-amendment council batting average:** **13-of-13** (forward pattern; council catches XO drift). Council surfaced 2 Critical + 8 Major + 4 Encouraged pre-merge — all mechanical to absorb pre-merge per the auto-merge-disabled posture. Cohort lesson holds: pre-merge council remains dramatically cheaper than post-merge.
- **Council false-claim rate (all three directions):** unchanged at 2-of-11 across the cohort. The 0062 council made 0 false-existence + 0 false-non-existence + 0 false-structural claims (verification-pass findings F15-F21 are explicit positive-existence + structural-citation verifications with verification commands).
- **Structural-citation failure rate (XO-authored):** **6-of-13 (~46%)** — F1 (ADR 0041) + F8 (ADR 0031 + ADR 0009) of the 0062 council are the 6th and 7th instances; both caught pre-merge. The cohort discipline IS catching them; the failure-mode IS recurring; the rate is nontrivial; XO continues to apply structural reads at draft-time but the council remains the safety net.
- **Standing rung-6 task reaffirmed:** XO spot-checks A1's added/modified citations within 24h of merge. If any A1-added claim turns out to be incorrect, file an A2 retraction matching the prior cohort retraction patterns.

#### A1.16 — Sibling amendment dependencies named

A1.6's halt-condition declares: Phase 1 of the migration may NOT begin until either (a) the 30-second TTL is operationally acceptable per the Phase 1 acceptance review, or (b) ADR 0031 has been amended to specify a Bridge → Anchor subscription-event-emitter contract. The latter is queued as a separate intake (XO follow-up: file `2026-04-30_bridge-subscription-event-emitter-intake.md` for ADR 0031-A1).

ADR 0063 (Mission Space Requirements; install-UX) is queued next per W#33 §7.2; it consumes the post-A1 surface (the renames, in particular, propagate downstream).

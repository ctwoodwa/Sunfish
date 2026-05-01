# ADR 0063 — Mission Space Requirements (install-UX layer) — Council Review

**Date:** 2026-04-30
**Reviewer:** research session (XO; adversarial council, UPF Stage 1.5)
**Subject:** ADR 0063 v. 2026-04-30 (Proposed; auto-merge intentionally DISABLED per cohort discipline)
**ADR under review:** [`docs/adrs/0063-mission-space-requirements.md`](../../../docs/adrs/0063-mission-space-requirements.md) on branch `docs/adr-0063-mission-space-requirements`; PR **#411**.
**Companion intake:** [`icm/00_intake/output/2026-04-30_mission-space-requirements-intake.md`](../../00_intake/output/2026-04-30_mission-space-requirements-intake.md)
**Driver discovery:** [`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`](../../01_discovery/output/2026-04-30_mission-space-matrix.md) §5.2 + §6.1 + §7.2 — fourth item in the W#33 Mission Space Matrix follow-on authoring queue
**Companion artifacts read:** ADR 0063 (531 lines, single document); ADR 0062 post-A1 surface (`origin/main:docs/adrs/0062-mission-space-negotiation-protocol.md` — `MissionEnvelope`, `IFeatureGate<TFeature>`, `FeatureVerdict`, `DegradationKind`, `EnvelopeChange`, `FeatureVerdictSurfaced` — verified post-A1.2 / A1.8 / A1.11 / A1.12 surface); ADR 0028 post-A8 + post-A7 (`FormFactorProfile` per A5.1 + A8.4; `VersionVector` per A6 + post-A7); ADR 0007 (`BusinessCaseBundleManifest` schema in `Sunfish.Foundation.Catalog.Bundles`); ADR 0009 (`Foundation.FeatureManagement`; `Edition` / `IEditionResolver`; no "trust tier" or "commercial tier" naming); ADR 0036 (`SyncState Multimodal Encoding Contract`; five state names `healthy / stale / offline / conflict / quarantine`); ADR 0044 (Anchor Windows-only Phase 1 — **amended 2026-04-28**; "Windows-only" softened to "Windows is the default Phase 1 deployment target"); ADR 0048 (Anchor multi-backend MAUI; **A1 + A2 mobile-scope amendments landed 2026-04-30**); ADR 0049 (audit substrate); ADR 0061 (three-tier peer transport; `TransportTier` enum = `LocalNetwork / MeshVpn / ManagedRelay`); paper §4 (hardware baseline: 16GB RAM, 8-core CPU, 500GB NVMe; 1GB at idle); paper §13.1 + §13.2 (visibility tables); `packages/foundation-catalog/` (actual package — NOT `foundation-bundles`); `packages/kernel-audit/AuditEventType.cs` (full constant set; verified non-collision for the 4 new constants); prior cohort councils 0061 / 0062 / 0028-A1 / 0028-A6 / 0028-A8 / 0046-A2 / 0046-A4 / 0048-A1.

**Council perspectives applied:** (1) Distributed-systems / install-UX reviewer; (2) Industry-prior-art reviewer; (3) Cited-symbol / cohort-discipline reviewer (3-direction spot-check per the A7 lesson — positive-existence, negative-existence, structural-citation correctness); (4) UX / authoring-cost reviewer.

---

## 1. Verdict

**Accept-with-amendments — grade B (low-B).** ADR 0063 is doing a **necessary** thing: paper §4's hardware baseline plus W#33 §5.2's "Partial coverage" classification name a real gap (no canonical install-time spec; users discover capability mismatches feature-by-feature through trial and silent failure), and the W#33 §6.1 user-visibility framing is the right forcing function. Steam's per-product "System Requirements" UX is a defensible prior-art anchor for the user mental model, and the bundle-manifest unit-of-declaration choice (vs per-feature) is the right Stage-0 tradeoff.

**However**, this ADR exhibits **four** structural-citation failures of the A7-third-direction class — more than any prior council in this cohort, including the high-water-mark 0062 review (two structural-citation findings). Two of the four are load-bearing for downstream Stage 02 / Stage 06: the `MinimumSpecDimension` type is invented (ADR 0062 has `DimensionChangeKind`); the `SyncStateSpec` example uses sync-state values (`Synced`, `Local`, `Quarantined`) that are not in ADR 0036's actual schema (which uses `healthy / stale / offline / conflict / quarantine`); the `NetworkSpec.RequiredTransports` example uses `Tier1Mdns` which is not a `TransportTier` enum value in ADR 0061 (which uses `LocalNetwork / MeshVpn / ManagedRelay`); and the `OperatorRecoveryHint` field name on `DimensionEvaluation` diverges from ADR 0062's post-A1.11 canonical `OperatorRecoveryAction`. Plus the affected-package reference is wrong (`foundation-bundles` does not exist; the actual package is `foundation-catalog`).

Beyond the citations, four substantive design gaps warrant amendment: the `OverallVerdict` rule under-specifies how `Informational` dimensions affect overall verdict; per-platform override resolution is named as "active platform override OR baseline" but the example uses **compose-with-baseline** semantics (the iOS override only adds a sensor requirement; the baseline 16GB RAM is preserved) — internal contradiction; post-install regression evaluation has no specified scaling-cost class for N installed bundles × hot-plug events; and `WarnOnly` warning persistence after dismissal is unspecified (does the warning re-show on bundle-update, on re-install, on subsequent feature use?).

None block W#33 Phase 1 of this protocol's substrate work; all should land before any bundle's first `Requirements: MinimumSpec?` field is authored, because the spec-vs-envelope unit mismatch (`MinMemoryBytesGb: int` vs envelope's `MemoryBytes: long`) ships forensic debt the moment the first bundle declares `MinMemoryBytesGb: 16`.

---

## 2. Findings (severity-tagged)

### F1 — `MinimumSpecDimension` is an invented type; ADR 0062 has `DimensionChangeKind` (Critical, structural-citation, A7-third-direction class)

ADR 0063 §A0 line 47 lists among the "existing on origin/main, verified" claims:

> *"ADR 0062 (Mission Space Negotiation Protocol; post-A1) — landed via PR #406; provides `MissionEnvelope`, `IMissionEnvelopeProvider`, `IFeatureGate<TFeature>`, `MinimumSpecDimension` types ADR 0063 will consume"*

**ADR 0062 does not define a type called `MinimumSpecDimension`.** Verified by reading ADR 0062 in full on `origin/main`:

- The dimension-key enum is named **`DimensionChangeKind`** (introduced in ADR 0062 line 145), with 10 values matching the envelope's 10 dimensions (`Hardware / User / Regulatory / Runtime / FormFactor / Edition / Network / Trust / SyncState / VersionVector` after the post-A1.8 `CommercialTier → Edition` rename).
- ADR 0063 line 268 uses `DimensionChangeKind` correctly inside the `DimensionEvaluation` record (`DimensionChangeKind Dimension,` field).
- The §A0 reference to `MinimumSpecDimension` is therefore self-contradictory with the contract surface 70 lines later: the §A0 audit names a type that doesn't exist, while the actual decision section uses the correct type.

This is the same A7-third-direction failure shape that fired on:

- ADR 0028-A6 council F3 (`required: true` cited on `ModuleManifest` when it actually lives on `ProviderRequirement`)
- ADR 0028-A5.7 (per-tenant key surface cited on `IFieldDecryptor` when it actually lives on `ITenantKeyProvider`)
- ADR 0062 council F1 (ADR 0041 cited as a degradation primitive when it's a component-pair coexistence policy)
- ADR 0062 council F8 (ADR 0031 cited as a subscription-event-emitter when it's the Bridge Zone-C SaaS architecture)

**Critical** — the §A0 cited-symbol audit is the surface ADR 0063 itself acknowledges as the place where structural-citation correctness is verified. Putting an invented type in the audit defeats the audit's purpose; Stage 02 implementer reading the §A0 list as authoritative will search for a type that doesn't exist. Mechanical fix: rename `MinimumSpecDimension` → `DimensionChangeKind` in §A0 (or drop the line entirely; the contract surface already cites the correct type).

### F2 — `SyncStateSpec.AcceptableStates` example uses sync-state names that don't exist in ADR 0036 (Critical, structural-citation, A7-third-direction class)

ADR 0063 line 229 declares:

```csharp
public sealed record SyncStateSpec(
    IReadOnlySet<SyncState>? AcceptableStates    // e.g., { Synced, Local } — exclude Stale, Quarantined
);
```

The example values **`Synced`**, **`Local`**, **`Stale`**, **`Quarantined`** are presented as canonical `SyncState` enum values per ADR 0036. **None of these four names match ADR 0036's actual decision section.**

ADR 0036 line 39–47 on `origin/main` declares the canonical state names as:

| State | Short label | Long label |
|---|---|---|
| `healthy` | Synced | Synced with all peers |
| `stale` | Stale | Last synced earlier |
| `offline` | Offline | Offline — saved locally |
| `conflict` | Conflict | Review required — two versions diverged |
| `quarantine` | Held | Can't sync — open diagnostics |

The state **identifiers** are lowercase `healthy / stale / offline / conflict / quarantine`. The user-facing **labels** are `Synced / Stale / Offline / Conflict / Held`. ADR 0063 conflates the two layers:

- `Synced` is the **user-facing label for the `healthy` state** — not the state name itself.
- `Stale` matches the label and the state's PascalCased identifier (when CSharpified) — partial match.
- `Local` does not appear in ADR 0036 in **any** form. The closest semantic match is `offline` ("Offline — saved locally"), but the literal token `Local` is not in the encoding contract.
- `Quarantined` does not match `quarantine` (the actual state); `Quarantined` is grammatically passive-voice, while the state identifier is the noun `quarantine`. The user-facing label is `Held`, not `Quarantined`.

This is two failures stacked: (i) the example uses **non-existent** `Local`; (ii) it uses **labels-as-identifiers** for the others, conflating presentation strings with state-enum names. ADR 0028-A6.2 rule 3 had this same shape (citing a field on the wrong type); ADR 0063 has it on a state-name-vs-label confusion.

**Critical** — the moment the first bundle author writes `new SyncStateSpec(AcceptableStates: new HashSet<SyncState> { SyncState.Local })`, the compiler will fail. ADR 0063 ships forensic debt that materializes at first authoring attempt. Mechanical fix: replace the example with `{ Healthy, Stale }` (using the state names per ADR 0036's encoding contract; PascalCased from the lowercase identifiers).

Note: ADR 0036's enum surface is the **encoding contract**, not the C# enum. ADR 0063 should also clarify whether `Sunfish.Foundation.MissionSpace.SyncStateSpec.AcceptableStates` consumes a `Sunfish.UI.SyncState` (the multimodal-encoded UI primitive) or a freshly-defined `Sunfish.Foundation.MissionSpace.SyncState` enum. ADR 0036 does not currently expose a public C# `SyncState` enum at the foundation tier.

### F3 — `NetworkSpec.RequiredTransports` example uses `Tier1Mdns` which is not a `TransportTier` enum value (Critical, structural-citation, A7-third-direction class)

ADR 0063 line 220:

```csharp
public sealed record NetworkSpec(
    IReadOnlySet<TransportTier>?    RequiredTransports  // e.g., { Tier1Mdns } for offline-first per ADR 0061
);
```

ADR 0061 line 92–97 on `origin/main` declares:

```csharp
public enum TransportTier
{
    LocalNetwork,        // Tier 1: mDNS / link-local; same-LAN
    MeshVpn,             // Tier 2: WireGuard mesh; cross-network direct
    ManagedRelay,        // Tier 3: Bridge HTTPS relay; ciphertext-only
}
```

**`Tier1Mdns` is not a member of `TransportTier`.** The Tier 1 value is `LocalNetwork`. The "mDNS" framing appears only in the doc-comment, not as a token. This is a third A7-class structural-citation failure: the example would not compile against the real enum.

Note also a **semantic** issue separate from the citation: `RequiredTransports` declares "for offline-first per ADR 0061." But `LocalNetwork` is *NOT* an offline-first signal — it's a same-LAN signal. Offline-first is more accurately encoded via `FormFactorSpec.MinNetworkPosture: NetworkPosture.OfflineFirst` (which exists per ADR 0028-A5.1). The example is doubly wrong: wrong token + wrong semantic.

**Critical** — same Stage-06-build-fails-at-compile shape as F2. Mechanical fix: replace `{ Tier1Mdns }` with `{ LocalNetwork }` and update the comment to "for same-LAN-required scenarios per ADR 0061".

### F4 — `OperatorRecoveryHint` diverges from ADR 0062 post-A1.11 canonical `OperatorRecoveryAction` (Major, vocabulary-fork)

ADR 0063 lines 257 + 272 declare:

```csharp
public sealed record SystemRequirementsResult(
    OverallVerdict          Overall,
    IReadOnlyList<DimensionEvaluation> Dimensions,
    LocalizedString?        OperatorRecoveryAction  // null if Overall == Pass
);
// ...
public sealed record DimensionEvaluation(
    // ...
    LocalizedString?        UserMessage,         // why this dimension failed; null if Pass
    LocalizedString?        OperatorRecoveryHint // operator-readable next step; null if Pass
);
```

The top-level `SystemRequirementsResult` uses `OperatorRecoveryAction` (correct, matches ADR 0062 post-A1.11). The per-dimension `DimensionEvaluation` uses `OperatorRecoveryHint`. **Two different field names, same semantic.**

ADR 0062 line 173 + post-A1.11 (line 762) standardize on **`OperatorRecoveryAction`** as the canonical name (`FeatureVerdict.OperatorRecoveryAction` is the exact field). Renaming the per-dimension field to `OperatorRecoveryHint` introduces a new vocabulary token that diverges from the post-A1 baseline within ADR 0062's own package (`Sunfish.Foundation.MissionSpace`).

This is the same substrate-vocabulary discipline class that fired on ADR 0061's `NodeId` vs `PeerId` (Critical there because it crossed package boundaries). Here the divergence is *within* the same package, which makes it harder to spot but no less confusing — Stage 06 implementer reads the contract surface and sees two names for the "what should the operator do?" hint.

**Major** — mechanical fix: rename `OperatorRecoveryHint` → `OperatorRecoveryAction` throughout `DimensionEvaluation`. The semantics are identical.

### F5 — Affected-packages list cites `foundation-bundles` which does not exist; the actual package is `foundation-catalog` (Major, structural-citation)

ADR 0063 line 434 declares:

> *"Modified: `packages/foundation-bundles/` (per ADR 0007) — `BusinessCaseBundleManifest.Requirements` field added (coordinated A1 amendment to ADR 0007 declared as sibling dependency)."*

`foundation-bundles` does not exist in `packages/`. The actual package is **`packages/foundation-catalog/`**, and per ADR 0007 line 28 the namespace is `Sunfish.Foundation.Catalog.Bundles` (manifest types live *inside* the catalog package, not in a standalone bundles package).

This is a fourth structural-citation failure of the same A7-third-direction class. Stage 02 implementer reading the affected-packages list will look for a non-existent package, then either (a) create `packages/foundation-bundles/` (forking the package layout), or (b) realize at integration time that `foundation-catalog` is where the work goes — same Cold-Start-failure shape as the prior cohort lessons.

**Major** — mechanical fix: replace `packages/foundation-bundles/` with `packages/foundation-catalog/` and clarify that the namespace is `Sunfish.Foundation.Catalog.Bundles`.

### F6 — Spec-vs-envelope unit mismatch on memory: `MinMemoryBytesGb: int` vs envelope's `MemoryBytes: long` (Critical, AP-1 unvalidated assumption)

ADR 0063 line 187:

```csharp
public sealed record HardwareSpec(
    int?                    MinMemoryBytesGb,    // e.g., 16 (paper §4 baseline)
    int?                    MinCpuCores,         // e.g., 8 (paper §4 baseline)
    long?                   MinDiskBytes,        // e.g., 500GB
    // ...
);
```

ADR 0062 envelope shape:

```csharp
public sealed record HardwareCapabilities(
    // ... (per ADR 0062 §"Manifest format" line 245-249)
    long memoryBytes,   // e.g., 17179869184 (= 16 GB in bytes)
    long diskBytes,     // e.g., 274877906944 (= 256 GB in bytes)
    // ...
);
```

The envelope reports memory in **bytes** (`long`); the spec declares minimum memory in **gigabytes** (`int`, named confusingly `MinMemoryBytesGb`). To evaluate, the resolver must convert: `userMemoryBytes >= specMinMemoryBytesGb * 1_073_741_824L`. This conversion is:

- **Lossy at the high end** if a future spec wants to declare `MinMemory = 1 TiB` — `int` caps at ~2 TB but `int * 1_073_741_824L` overflows `int` arithmetic at ~2 GB if not explicitly long-cast. (Widely-cited unit-conversion bug; e.g., the original Mars Climate Orbiter analog.)
- **Confusing at the field-name level** — `MinMemoryBytesGb` has both "Bytes" and "Gb" in the name; should it be `MinMemoryGb`? Or `MinMemoryBytes: long`?
- **Inconsistent with the envelope** — the envelope unit is bytes; the spec unit is GB. Same dimension, two units, two field names.
- **Disk is internally consistent** (`MinDiskBytes: long?` matches `diskBytes: long`); only memory diverges.

This is an industry-prior-art anti-pattern: SI-unit-conversion at the schema boundary. The right fix is `MinMemoryBytes: long?` (matching `MinDiskBytes: long?` and the envelope). Authoring is `MinMemoryBytes: 16L * 1_073_741_824L` — verbose but safe. Or define a `MemorySize` value type with conversion helpers (out of scope; deferred).

**Critical** — substrate-vocabulary forensic foot-gun. The first bundle author who writes `MinMemoryBytesGb: 16` will produce a spec that reads correctly; the resolver will multiply by `1_073_741_824L`; the comparison will work. But the moment a future spec wants 1.5 GB or 0.5 GB, the integer dimension breaks. And the moment a council-style audit asks "what unit is `MinMemoryBytesGb` in?", the answer is "GB stored as int, named ambiguously, evaluated against bytes-as-long." This is exactly the AP-1 ("unvalidated assumption") shape ADR 0061 council flagged on `ITransportSelector` semantics, surfaced one layer up in the type system.

### F7 — Per-platform override resolution rule contradicts the canonical example (Major, AP-3 vague success criteria)

ADR 0063 line 346 declares the resolution rule:

> *"When `BusinessCaseBundleManifest.Requirements.PerPlatformOverrides` is set, the active platform's override is used in preference to the baseline. Resolution order:*
> *1. Active platform override (e.g., IosFieldCapture if running on iOS) — if set, use this MinimumSpec directly.*
> *2. Baseline spec — if no platform override is set, use the baseline.*
> *3. No spec (Requirements == null) — bundle is "any" (effectively OverallVerdict.Pass always)."*

This is a **REPLACE** semantic — when iOS is set, use the iOS spec *instead of* the baseline.

But the very next paragraph (line 354–374) gives the canonical example:

```csharp
new MinimumSpec(
    Hardware: new HardwareSpec(MinMemoryBytesGb: 16, ...),  // baseline says 16GB
    Policy: SpecPolicy.Required,
    PerPlatformOverrides: new PerPlatformSpec(
        IosFieldCapture: new MinimumSpec(
            Hardware: new HardwareSpec(
                RequiredSensors: new HashSet<SensorRequirement> { SensorRequirement.BiometricAuth }
            ),  // iOS override declares ONLY BiometricAuth; says nothing about memory
            // ...
        ),
        // ...
    )
)
```

And the prose around it (line 352) says:

> *"Common case (iOS requires BiometricAuth because no password fallback; macOS recommends it but doesn't require) shows up cleanly"*

But under the REPLACE rule, **on iOS this MinimumSpec evaluates to "BiometricAuth required, memory unspecified."** The 16GB baseline is **dropped** because the iOS override doesn't carry it. This is almost certainly NOT the author's intent — the prose ("iOS requires BiometricAuth ON TOP OF baseline") implies COMPOSE semantics.

The two readings produce different verdicts:

- **REPLACE (per the rule):** iOS device with 8GB RAM + BiometricAuth → Pass (memory not in iOS override; 8 < 16 doesn't matter because baseline is dropped).
- **COMPOSE (per the example's apparent intent):** iOS device with 8GB RAM + BiometricAuth → Block (composition: 16GB baseline AND BiometricAuth iOS override; 8 < 16 fails baseline).

ADR 0063 must pick one and align rule + example. Recommended: **COMPOSE** (the more common case is "everything baseline says PLUS this platform-specific addition"). REPLACE forces every iOS override to redeclare memory, CPU, disk, etc. — making per-platform overrides high-friction.

**Major** — internal contradiction; spec authors cannot tell from the ADR which semantics are intended. Same AP-3-shape (vague success criteria) that fired on ADR 0061 council F3 (`ITransportSelector` failover under partial-failure conditions undefined). Mechanical fix: pick COMPOSE; rewrite the resolution rule to "the active platform's override is **merged with** the baseline; per-dimension, the override's value REPLACES the baseline if both declare; if only one declares, the declaring value wins; if neither declares, the dimension is unspec'd."

### F8 — `OverallVerdict` rule under-specifies how `Informational` dimensions affect the verdict (Major, AP-3 vague success criteria)

ADR 0063 lines 263–265 declare:

```csharp
public enum OverallVerdict
{
    Pass,       // every required dimension meets spec; recommended dimensions all pass too
    WarnOnly,   // every required dimension passes; one or more recommended dimensions fail
    Block       // one or more required dimensions fail
}
```

The Pass rule says "every required dimension meets spec; recommended dimensions all pass too." But what about **`Informational`** dimensions? The `DimensionPolicyKind` enum (line 275) has four values — `Required / Recommended / Informational / Unevaluated` — but the Pass rule treats only Required + Recommended.

Three possible readings:

- **(a)** Informational dimensions are excluded from `Overall`; they only surface in the System Requirements page table. (Likely intent.)
- **(b)** Informational dimensions silently fail-but-don't-block → Overall stays at the right tier per Required + Recommended only. (Equivalent to (a).)
- **(c)** Informational dimensions count as Recommended for `Overall` purposes. (Plausible alternate intent.)

OQ-0063.1 (line 478) acknowledges the question: "Should `SpecPolicy = Informational` produce a UI surface at all, or is it purely documentation?" — but that's a UI-surface question, not the verdict-rule question. The verdict-rule question is unanswered.

Stage 06 implementer cannot ship `IMinimumSpecResolver` without picking one. **Major** — mechanical fix: enum doc-comments must spell out "Informational dimensions are evaluated and shown in the System Requirements page but do NOT affect `Overall`; failure of an Informational dimension is purely diagnostic."

### F9 — Post-install regression detection scaling is unspecified for N installed bundles × M hot-plug events (Major, AP-1 unvalidated assumption / scaling cost class)

ADR 0063 lines 79 + 341–342 specify:

> *"Re-evaluation reuses ADR 0062 + ADR 0028-A8 events. Post-install hardware/environment changes are surfaced via the existing EnvelopeChange event stream + HardwareTierChangeEvent per ADR 0028-A8.3"*

> *"On post-install regression (per ADR 0062 EnvelopeChange event with Severity = Warning or Critical): if any installed bundle's requirements are no longer met, fire PostInstallSpecRegression audit event + surface PostInstallRegressionBanner UX."*

**Cost class is not specified.** Concrete scenario:

- 50 installed bundles × N dimensions × hot-plug event = 50 × N spec evaluations per hot-plug event.
- The `MinimumSpecEvaluated` audit event has dedup `5-min per (bundle_key)` (line 382) — but this is *AT* the audit boundary, AFTER the evaluation runs. The evaluation cost itself is uncached.
- A user docking/undocking a USB camera produces an `EnvelopeChange` event with `Hardware` in `ChangedDimensions`; the resolver re-evaluates 50 specs even though only Hardware changed.

ADR 0062's `EnvelopeChange` carries `IReadOnlyList<DimensionChangeKind> ChangedDimensions` precisely to enable this optimization (filter specs by which dimensions they declare; only re-evaluate specs that touch a changed dimension). ADR 0063 doesn't mention this optimization or commit to it.

ADR 0062 line 226 names `Hardware` as `Low (local OS API)` cost; the per-spec evaluation cost should also be `Low` (in-memory comparison after envelope is fetched). 50 × Low evaluations is acceptable. But the ADR should *say so* — name the cost class, name the per-event budget (e.g., "P95 < 100ms for 50 installed bundles × full re-evaluation"), and name the optimization (filter specs by ChangedDimensions before evaluating).

**Major** — same AP-1 + AP-3 shape as ADR 0061's `ITransportSelector` semantics. Mechanical fix: add a §"Re-evaluation cost class" subsection naming the cost class, the optimization, and the budget.

### F10 — `WarnOnly` warning persistence after dismissal is unspecified (Major, AP-3)

ADR 0063 line 336 specifies:

> *"If Overall is WarnOnly: Install button enabled; clicking shows a dismissable warning naming the failed Recommended dimensions; user clicks 'Install Anyway' to proceed."*

After dismissal, what happens? Five plausible scenarios:

- **(a)** User uninstalls + reinstalls the bundle: warning shows again, dismissal not remembered.
- **(b)** User updates the bundle (new version): warning shows again because the new version may have new recommended dimensions.
- **(c)** Bundle's `Requirements.Recommended` changes via spec author update: warning shows again because spec changed.
- **(d)** User's envelope changes (e.g., upgrades RAM from 8GB to 16GB) such that the previously-failing dimension now passes: warning would not surface anyway.
- **(e)** User opens the bundle's "Settings → System Requirements" page post-install: does the page show the dismissable warning, or is the page now read-only?

None of these are answered. Stage 06 implementer must guess.

**Major** — substrate-UX forensic debt. Mechanical fix: add a §"Warning persistence policy" subsection naming the rule (recommended: warnings are dismissed *per-version-per-install*; new bundle version → new warning; reinstall → new warning; envelope changes that resolve the warning silently no-op).

### F11 — `InstallBlocked` audit event is emitted on user-blocked attempts only; force-install path has no separate audit signal (Major, AP-3 / OQ-0063.3 not closed)

ADR 0063 line 383 specifies the `InstallBlocked` audit dedup as "None (per-attempt)" with payload `(bundle_key, failed_required_dimensions)`.

But the cohort-discipline §"Cohort discipline" §526 explicitly raises this concern:

> *"Force-install audit shape — is the InstallBlocked audit emitted on the user's 'blocked' event AND on operator's 'force-install'? The current spec emits only on user-blocked-attempt; force-install is a separate `CapabilityForceEnabled`-style event the spec should declare."*

The ADR raised the concern but didn't resolve it. OQ-0063.3 (line 480) defers the operator-force-install UX to "the operator force-install path requires a justification text" but doesn't add a separate audit event for it.

ADR 0062's force-enable surface emits `FeatureForceEnabled` (one of its 9 new audit constants per A1.2 + A1.9) on operator override. ADR 0063 should mirror this: a 5th new `AuditEventType` constant `InstallForceEnabled` (or `BundleForceInstalled`) emitted when an operator overrides a Block verdict.

Without it, the install-time force-install path is **silent in the audit log**. Operator override + no audit trail is the exact "Schrödinger feature" shape ADR 0062 council F9 named.

**Major** — security/audit gap. Mechanical fix: add a 5th audit constant + emission point in §"Telemetry shape".

### F12 — Steam-as-prior-art is defensible but App Store / Microsoft Store / Mac App Store / iOS `UIRequiredDeviceCapabilities` / Android `<uses-feature>` are closer (Minor, AP-21 prior-art-citation)

ADR 0063 line 75 + 508 cite Steam's "System Requirements" page as the canonical UX prior-art. The cohort-discipline §526 raised the same concern:

> *"The Steam-style UX prior-art — is Steam the right anchor (gaming context vs business-app context), or are App Store / Microsoft Store / Mac App Store closer?"*

Steam is *fine* as a prior-art reference for the **user-facing UX shape** (System Requirements page; pre-install vs post-install rendering). But Sunfish is a business-app substrate, and the closer prior-art for **declarative bundle-manifest spec encoding** is:

- **Apple `UIRequiredDeviceCapabilities`** — `Info.plist` keys per app; values are `gps`, `armv7`, `metal`, `bluetooth-le`, `front-facing-camera`, `still-camera`, `auto-focus-camera`, etc. Apple's App Store filters the Available-for-Download list by these keys at install time. **Direct prior-art** for `HardwareSpec.RequiredSensors`.
- **Android `<uses-feature>` manifest declarations** — `AndroidManifest.xml` declares `<uses-feature android:name="android.hardware.camera" android:required="true"/>`. Required vs not-required maps directly to ADR 0063's `SpecPolicy = Required / Recommended` distinction. **Direct prior-art** for `HardwareSpec.RequiredSensors` AND for the Required/Recommended split.
- **Microsoft Store `<DeviceCapability>`** in `appxmanifest.xml` — capability declarations enforced at install.
- **.NET `<TargetFramework>`** in `*.csproj` — declares minimum .NET version per project. **Direct prior-art** for `RuntimeSpec.MinDotnetVersion`.

ADR 0063 reinvents the cross-platform substrate version of these without citing them. Steam is the right *user-facing UX* anchor; iOS `UIRequiredDeviceCapabilities` + Android `<uses-feature>` + msbuild `<TargetFramework>` are the right *declarative-schema* anchors.

**Minor** — hardening, not blocking. Mechanical fix: add a §"Prior art" subsection naming the four canonical declarative-spec anchors and their direct mappings to ADR 0063 schema fields.

### F13 — Localization framing is partial: spec authoring uses raw strings, only renderer output uses `LocalizedString` (Minor, vocabulary-fork)

ADR 0063 lines 257 + 272 + 273 use `LocalizedString?` for `OperatorRecoveryAction`, `UserMessage`, `OperatorRecoveryHint`. But the §"Per-platform per-bundle spec resolution" example (line 354) authors specs using raw strings (or no strings at all — only enum values). The spec **authoring** surface has no strings to localize; the **rendering** surface produces `LocalizedString?` outputs.

The current shape is internally consistent — authoring is enum/numeric data, rendering produces user-visible localized strings — but ADR 0063 doesn't make this explicit. OQ-0063.5 (line 482) acknowledges the rendering side ("Localization of the per-dimension UserMessage/OperatorRecoveryHint — does ADR 0063 ship default localization keys per dimension?") but doesn't pin the authoring-side rule.

**Minor** — clarification, not a flaw. Mechanical fix: add one paragraph in the Localization subsection (currently absent — would also fix the absence) clarifying that spec authoring is purely structural data; rendering produces `LocalizedString?` outputs from default localization keys (e.g., `mission-space.requirements.hardware.min-memory.fail`); renderers may override per-key.

### F14 — Operator-only "Force Install" link visibility is unspecified (Encouraged, AP-3)

ADR 0063 line 335 says:

> *"If Overall is Block: Install button is disabled; tooltip naming the failed Required dimensions; **operator-only 'Force Install' link** in tooltip (operator authentication required)."*

Tooltips are typically end-user surfaces. How does the UX hide the link from non-operators? Three possible mechanisms:

- **(a)** Tooltip is only rendered if the current user has the operator role (server-side gate; tooltip never reaches non-operator devices).
- **(b)** Tooltip is rendered for all users; link is visible but click triggers operator-auth challenge that fails for non-operators.
- **(c)** Tooltip text differs per user role (operators see "Force Install"; non-operators see no link).

Each has different UX + security implications. (a) is the cleanest; (b) leaks the existence of the override surface; (c) requires per-role rendering.

ADR 0063 doesn't pick. **Encouraged** — mechanical fix: pick (a) and add one sentence to §"Pre-install UX flow".

### F15 — `apps/docs/foundation/mission-space-requirements/` walkthrough citation is forward-reference (Minor, AP-18 unverifiable gate)

ADR 0063 line 65 + 435 cite `apps/docs/foundation/mission-space-requirements/` as a deliverable. Verified: this path **does not exist** on `origin/main`. It is a Phase 3 deliverable (per the migration order line 426) but is cited in §A0 as if it were an introduced surface. Mild forward-reference; acceptable because Phase 3 is explicitly named in the ADR's own migration order — but the §A0 surface mixes "introduced by this ADR" with "introduced by Phase 3 of this ADR" without distinguishing.

**Minor** — mechanical fix: clarify in §A0 that the apps/docs path is "Phase 3 deliverable, not in initial substrate package."

### Verification-pass findings (no issue; cohort spot-check evidence)

**F-VP1 — All 8 cited ADRs verified Accepted on `origin/main`.** ADR 0007 / 0009 / 0028 / 0036 / 0044 / 0048 / 0049 / 0061 / 0062 all confirmed via `git ls-tree origin/main docs/adrs/`. None vapourware. **Pass.**

**F-VP2 — `BusinessCaseBundleManifest` schema verified.** ADR 0007 line 30 declares the type in `Sunfish.Foundation.Catalog.Bundles`; the type is a record with field set per ADR 0007 §"Decision". The `requiredModules: string[]` field exists (line 41). ADR 0063's plan to extend with `Requirements: MinimumSpec?` is a clean additive change. **Pass — note the namespace is `Sunfish.Foundation.Catalog.Bundles`, NOT a hypothetical `Sunfish.Foundation.Bundles`** (drove F5).

**F-VP3 — `Edition` vocabulary in ADR 0009 verified.** ADR 0009 line 50 declares `IEditionResolver` and uses "Edition" canonically (no "trust tier" or "commercial tier" — these strings appear nowhere in 0009). ADR 0063's `EditionSpec.AllowedEditions: IReadOnlySet<string>?` is consistent with ADR 0009's edition-key vocabulary. **Pass.** (Note: ADR 0062 council F8 caught the `CommercialTier` → `EditionCapabilities` rename; ADR 0063 correctly uses `Edition`-derived naming.)

**F-VP4 — ADR 0062 post-A1 surface verified.** `MissionEnvelope`, `IMissionEnvelopeProvider`, `IFeatureGate<TFeature>`, `FeatureVerdict`, `DegradationKind`, `EnvelopeChange`, `FeatureVerdictSurfaced` all exist in ADR 0062's post-A1 text on `origin/main`. The post-A1.2 rename (`ICapabilityGate` → `IFeatureGate`) and post-A1.8 rename (`CommercialTier` → `EditionCapabilities`) and post-A1.11 (`LocalizedString` for `UserMessage` + `OperatorRecoveryAction`) and post-A1.12 (`FeatureVerdictSurfaced` audit event) are all reflected in ADR 0063's citations. **Pass — except `MinimumSpecDimension` which is invented (drove F1).**

**F-VP5 — All 4 new `AuditEventType` constants verified non-colliding.** `MinimumSpecEvaluated`, `InstallBlocked`, `InstallWarned`, `PostInstallSpecRegression` checked against `packages/kernel-audit/AuditEventType.cs`; no collision with the 100+ existing constants. The 9 ADR-0062 new constants (FeatureProbed / FeatureAvailabilityChanged / FeatureProbeFailed / FeatureForceEnabled / FeatureForceRevoked / FeatureForceEnableRejected / MissionEnvelopeChangeBroadcast / MissionEnvelopeObserverOverflow / FeatureVerdictSurfaced) also do not collide with ADR 0063's new 4. **Pass.**

**F-VP6 — Paper §4 baseline verified.** Paper line 99 reads: *"A current mid-range workstation (16GB RAM, 8-core CPU, 500GB NVMe) has more compute than the average cloud VM serving a ten-user team five years ago. A complete three-service containerized stack — API server, sync daemon, local database — runs comfortably under 1GB of RAM at idle."* ADR 0063's per-dimension defaults match. **Pass.**

**F-VP7 — `HardwareTierChangeEvent` per ADR 0028-A8 verified.** Line 535 of ADR 0028 declares the event with `previousProfile: FormFactorProfile, currentProfile: FormFactorProfile, triggeringEvent: enum { StorageBudgetChanged, NetworkPostureChanged, SensorPermissionChanged, PowerProfileChanged, AdapterUpgrade, AdapterDowngrade, ManualReprofile }`. ADR 0063's reuse for post-install regression detection is technically sound (modulo F9's scaling concern). **Pass.**

**F-VP8 — `FormFactorProfile` enum members verified.** ADR 0028 line 471–477 declares `formFactor: enum { Laptop, Desktop, Tablet, Phone, Watch, Headless, Iot, Vehicle }`, `displayClass: enum { Large, Medium, Small, MicroDisplay, NoDisplay }`, `networkPosture: enum { AlwaysConnected, IntermittentConnected, OfflineFirst, AirGapped }`, `powerProfile: enum { Wallpower, Battery, LowPower, IntermittentBattery }`. ADR 0063's `FormFactorSpec` consumes all four. **Pass — but note the spec uses `FormFactor` (singular) where the envelope uses the embedding type `FormFactorProfile`; minor type-name divergence (acceptable; the spec extracts a single field).**

**F-VP9 — ADR 0044 + ADR 0048 phase-1-vs-phase-2 framing verified.** ADR 0044 was amended 2026-04-28 to soften "Windows-only" to "Windows is the default Phase 1 deployment target"; ADR 0048 A1+A2 amendments landed 2026-04-30 (post-A1 mobile scope). ADR 0063's framing of "Phase 1 Windows / Phase 2 multi-platform" is **slightly stale on ADR 0044** (the ADR has been amended) but **substantively accurate** on the deployment-target intent. **Pass — minor amendment-tracking imprecision; not a citation failure.**

---

## 3. Recommended amendments

### A1 (Required) — Replace `MinimumSpecDimension` with `DimensionChangeKind` in §A0 (resolves F1)

§A0 line 47 currently reads:

> *"ADR 0062 (Mission Space Negotiation Protocol; post-A1) — landed via PR #406; provides MissionEnvelope, IMissionEnvelopeProvider, IFeatureGate<TFeature>, MinimumSpecDimension types ADR 0063 will consume"*

Change to:

> *"ADR 0062 (Mission Space Negotiation Protocol; post-A1) — landed via PR #406; provides `MissionEnvelope`, `IMissionEnvelopeProvider`, `IFeatureGate<TFeature>`, `FeatureVerdict`, `DegradationKind`, `EnvelopeChange`, `DimensionChangeKind` (10-value dimension-key enum), `LocalizedString`, and `FeatureVerdictSurfaced` audit constant — ADR 0063 consumes these post-A1.2/A1.8/A1.11/A1.12 surfaces."*

This drops the invented `MinimumSpecDimension` and adds the post-A1 surface ADR 0063 actually uses (in `DimensionEvaluation` line 268). **Required because F1 is Critical.**

### A2 (Required) — Replace `SyncStateSpec.AcceptableStates` example values with ADR-0036-canonical state names (resolves F2)

Line 229's example comment changes from:

> *"e.g., { Synced, Local } — exclude Stale, Quarantined"*

to:

> *"e.g., { Healthy, Stale } — exclude Offline, Conflict, Quarantine. State names match ADR 0036's encoding-contract identifiers (PascalCased from `healthy / stale / offline / conflict / quarantine`)."*

Add a halt-condition: ADR 0063 Stage 06 build cannot ship `SyncStateSpec` until `Sunfish.UI.SyncState` (or equivalent foundation-tier `SyncState` enum) is exposed publicly. ADR 0036 currently declares the encoding contract but does NOT expose a public C# `SyncState` enum at the foundation tier. Either add such an enum (sibling amendment to ADR 0036) or pick a different type (e.g., `IReadOnlySet<string>?` with the doc-comment naming the canonical state strings). **Required because F2 is Critical.**

### A3 (Required) — Replace `NetworkSpec.RequiredTransports` example with the actual `TransportTier` enum value (resolves F3)

Line 220's example comment changes from:

> *"e.g., { Tier1Mdns } for offline-first per ADR 0061"*

to:

> *"e.g., { LocalNetwork } for same-LAN-required scenarios per ADR 0061. (For offline-first declarations, prefer `FormFactorSpec.MinNetworkPosture: NetworkPosture.OfflineFirst` per ADR 0028-A5.1; this dimension is for declaring REQUIRED transports, not capability profile.)"*

**Required because F3 is Critical.**

### A4 (Required) — Rename `OperatorRecoveryHint` to `OperatorRecoveryAction` throughout `DimensionEvaluation` (resolves F4)

Line 273 changes from:

```csharp
LocalizedString?        OperatorRecoveryHint // operator-readable next step; null if Pass
```

to:

```csharp
LocalizedString?        OperatorRecoveryAction // operator-readable next step; matches ADR 0062 post-A1.11 vocabulary; null if Pass
```

**Required because F4 is Major and substrate-vocabulary discipline.**

### A5 (Required) — Replace `packages/foundation-bundles/` with `packages/foundation-catalog/` in §"Affected packages" (resolves F5)

Line 434 changes from:

> *"Modified: `packages/foundation-bundles/` (per ADR 0007) — `BusinessCaseBundleManifest.Requirements` field added"*

to:

> *"Modified: `packages/foundation-catalog/` (per ADR 0007; namespace `Sunfish.Foundation.Catalog.Bundles`) — `BusinessCaseBundleManifest.Requirements` field added"*

**Required because F5 is Major and structural-citation discipline (4th of 4 in this review).**

### A6 (Required) — Pick memory-spec unit; align with envelope; rename field (resolves F6)

Two options; pick one:

- **Option α (recommended):** Rename `MinMemoryBytesGb: int?` → `MinMemoryBytes: long?`; update doc-comment to "e.g., 17_179_869_184L (= 16 GB; matches paper §4 baseline)". Matches `MinDiskBytes: long?` and the envelope's `memoryBytes: long`. Minor authoring verbosity; safe.
- **Option β:** Rename `MinMemoryBytesGb: int?` → `MinMemoryGb: int?` and document the resolver-side conversion to bytes. Less verbose but two units in one schema; slightly higher unit-conversion-bug risk.

Either is acceptable; Option α is recommended for consistency. **Required because F6 is Critical (forensic foot-gun).**

### A7 (Required) — Resolve per-platform override resolution: pick COMPOSE; align rule + example (resolves F7)

Replace the §"Per-platform per-bundle spec resolution" rule (lines 346–350) with:

> *"When `BusinessCaseBundleManifest.Requirements.PerPlatformOverrides` is set, the active platform's override is **merged** with the baseline per the following per-dimension rule:*
>
> *1. **For each dimension**, if the override declares a value for that dimension, the override's value REPLACES the baseline's value for that dimension. If only the baseline declares, the baseline's value applies. If neither declares, the dimension is unspec'd ("any").*
>
> *2. The merged spec is the spec used for evaluation against the user's MissionEnvelope.*
>
> *3. **No override AND no baseline** (`Requirements == null`): bundle is "any" (effectively `OverallVerdict.Pass` always).*"

Update the example prose to clarify that the iOS override "adds BiometricAuth on top of the baseline's 16GB / 8-core / etc." and the merged effective spec on iOS is `{ Hardware: { MinMemoryBytesGb: 16, ..., RequiredSensors: { BiometricAuth } }, Policy: Required }`. **Required because F7 is Major (internal contradiction).**

### A8 (Required) — Spell out `OverallVerdict` rule for `Informational` dimensions (resolves F8)

Update line 264's `OverallVerdict.Pass` doc-comment:

```csharp
public enum OverallVerdict
{
    Pass,       // every Required dimension passes; every Recommended dimension passes; Informational dimensions ignored for verdict (surfaced in System Requirements page only)
    WarnOnly,   // every Required dimension passes; one or more Recommended dimensions fail; Informational dimensions ignored for verdict
    Block       // one or more Required dimensions fail; Recommended/Informational do not move the needle from Block
}
```

Add a paragraph above the enum: *"`Informational` dimensions are evaluated and surfaced in the System Requirements page table but do NOT affect `Overall`. Failure of an Informational dimension is purely diagnostic."* **Required because F8 is Major.**

### A9 (Required) — Specify post-install regression evaluation cost class + optimization (resolves F9)

Add a §"Re-evaluation cost class" subsection under §"Post-install UX flow":

> *"On `EnvelopeChange` events with `Severity ∈ { Warning, Critical }`, the resolver re-evaluates **only the installed bundles whose `Requirements` declare at least one of the dimensions in `EnvelopeChange.ChangedDimensions`** (filtering optimization). For each such bundle, the resolver runs `EvaluateAsync` against the new envelope. Cost class: `Low` (in-memory comparison after envelope is fetched). Per-event budget: P95 < 100ms for ≤50 installed bundles × full re-evaluation (typical home/SMB tenant). Above 50 bundles, the resolver SHOULD batch evaluations on a background-priority worker; this is a SHOULD not a MUST for v0."*

**Required because F9 is Major.**

### A10 (Required) — Specify warning-persistence policy after `WarnOnly` dismissal (resolves F10)

Add a §"Warning persistence policy" subsection:

> *"`WarnOnly` warnings are dismissed per-version-per-install. Specifically:*
>
> *- Bundle install with version V1: warning shown; user dismisses; warning hidden for the install lifetime of V1.*
> *- Bundle update from V1 to V2 (any version bump): warning re-shown if WarnOnly still applies under V2's spec. The dismissal does NOT carry over.*
> *- Bundle uninstall + reinstall: treated as a new install; warning re-shown.*
> *- User envelope change that resolves the warning silently no-ops; the warning surface disappears (no notification).*
> *- User envelope change that introduces a NEW Recommended-dimension failure produces a fresh warning surface (not a dismissed one)."*

**Required because F10 is Major.**

### A11 (Required) — Add 5th `AuditEventType` constant for force-install; emit on operator override (resolves F11)

Update §"Telemetry shape" to add:

| Event | Trigger | Payload | Dedup window |
|---|---|---|---|
| `InstallForceEnabled` | Operator force-installs a bundle whose Overall is Block | `(bundle_key, failed_required_dimensions, operator_id, justification)` | None (per-attempt) |

Update the §"Cohort discipline" §526 force-install bullet to "RESOLVED via amendment A11; force-install emits `InstallForceEnabled` audit event with operator + justification per ADR 0062-style force-enable shape." **Required because F11 is Major (security/audit gap).**

### A12 (Encouraged) — Add §"Prior art" subsection citing iOS / Android / Microsoft Store / msbuild declarative-spec anchors (resolves F12)

Add a §"Prior art" subsection naming:

- **Apple `UIRequiredDeviceCapabilities`** in `Info.plist` — direct prior-art for `HardwareSpec.RequiredSensors`
- **Android `<uses-feature android:required="true|false">`** in `AndroidManifest.xml` — direct prior-art for the Required vs Recommended split
- **Microsoft Store `<DeviceCapability>`** in `appxmanifest.xml` — capability-declaration enforcement at install
- **.NET msbuild `<TargetFramework>`** in `*.csproj` — direct prior-art for `RuntimeSpec.MinDotnetVersion`

Steam remains the right user-facing UX anchor; these are the right *declarative-schema* anchors. **Encouraged; not blocking, but hardening for cohort-citation discipline.**

### A13 (Encouraged) — Add §"Localization" subsection clarifying authoring vs rendering sides (resolves F13)

Add one paragraph: *"Spec authoring is purely structural data (enums, numeric thresholds, sets). Spec rendering produces `LocalizedString?` outputs from per-dimension default localization keys (e.g., `mission-space.requirements.hardware.min-memory.fail`). Renderers may override per-key. Keys live alongside the spec records; the renderer fetches them via the platform's localization framework (`.resx` for Anchor MAUI; `i18next` for Bridge React; `.strings` for iOS)."* **Encouraged.**

### A14 (Encouraged) — Specify Force-Install link visibility mechanism (resolves F14)

Update line 335 to: *"If Overall is Block: Install button is disabled; tooltip naming the failed Required dimensions; **operator-only 'Force Install' link** rendered server-side only when the requesting user has the operator role (so the link is never visible to non-operators; non-operators see the tooltip without the link)."* **Encouraged.**

### A15 (Encouraged) — Bake §A0 cited-symbol audit into the cohort discipline checklist (matches ADR 0062-A1.14 precedent)

Add to §"Cohort discipline":

> *"§A0 cited-symbol audit shall be re-verified by the council reviewer with three-direction spot-check (positive-existence, negative-existence, structural-citation correctness — per the ADR 0028-A6.2 / A7.13 / A8.12 / ADR 0062-A1.14 lesson). The §A0 audit failed structural-citation correctness on this ADR (4 of 4 structural-citation findings — F1, F2, F3, F5 — passed §A0 self-audit but failed council external verification). Future ADRs in this lineage MUST include the council-side spot-check; pre-merge council canonical going forward."*

**Encouraged — captures the lesson explicitly so the cohort discipline is auditable.**

---

## 4. Quality rubric grade

**Grade: B (low-B).** Path to A is mechanical (A1–A11 land).

- **C threshold (Viable):** All 5 CORE present (Context, Decision drivers, Considered options A–D, Decision, Consequences); multiple CONDITIONAL sections (Compatibility plan, Implementation checklist, Open questions, Revisit triggers, References, Sibling amendment dependencies, Cohort discipline, §A0 cited-symbol audit). No critical *planning* anti-patterns. **Pass.**
- **B threshold (Solid):** Stage 0 sparring present (4 options A–D triangulated honestly; Option B per-feature granularity rejected with named rationale; Option C "no spec" rejected explicitly; Option D hybrid named as the actual chosen specialization of A); FAILED conditions present (4 revisit triggers); Cold Start Test plausible *if* citations were correct. **Pass with reservations** — Cold Start Test fails on F1+F2+F3+F5 because Stage 02 implementer reading the contract surface alone hits 4 compile-fail / type-not-found surfaces.
- **A threshold (Excellent):** Misses on five counts:
  - **(1)** §A0 cited-symbol audit failed external structural-citation verification on 4 of 4 introduced or consumed type/package claims — the highest count of any ADR in the cohort. The §A0 self-audit passes its own internal checklist but fails the A7-third-direction class spot-check on `MinimumSpecDimension`, `SyncState` example values, `TransportTier` example values, and `foundation-bundles` package name.
  - **(2)** Per-platform override resolution rule contradicts its own canonical example (REPLACE rule but COMPOSE example).
  - **(3)** Spec-vs-envelope unit mismatch on `MinMemoryBytesGb` vs `memoryBytes`.
  - **(4)** Multiple AP-3 vague-success-criteria failures (`OverallVerdict` for Informational; warning-persistence after dismissal; post-install regression scaling; force-install audit shape).
  - **(5)** Steam-only prior-art citation when iOS / Android / Microsoft Store / msbuild are closer for the declarative-schema side.

A grade of **B with required amendments A1–A11 applied promotes to A.** A12–A15 may land during Stage 02 or as Stage 06 implementation guidance.

---

## 5. Council perspective notes (compressed)

- **Distributed-systems / install-UX reviewer:** "The schema-vs-envelope symmetry is mostly sound, but two seams need attention. First: `MinMemoryBytesGb: int` vs envelope's `memoryBytes: long` is a unit-conversion accident waiting to happen — same dimension, two units, two field names. Second: `EditionSpec.AllowedEditions: IReadOnlySet<string>?` is the *right* shape (edition-keys are externally-defined strings per ADR 0009; not a closed enum) — but ADR 0062's envelope dimension is the typed `EditionCapabilities Edition` value object. The resolver must convert between the typed envelope dimension and the string-set spec — fine, but the conversion contract should be named (e.g., `EditionCapabilities.IncludesAny(IReadOnlySet<string> editionKeys)`). On post-install regression: 50 bundles × hot-plug events = 50 evaluations is acceptable IF the resolver filters by `EnvelopeChange.ChangedDimensions` first. ADR 0062's `EnvelopeChange` carries this exact data for this exact reason; ADR 0063 should commit to using it. On per-platform override semantics: REPLACE-vs-COMPOSE is a real fork; the example is COMPOSE; the rule is REPLACE; pick COMPOSE." Drives F6 + F7 + F9.

- **Industry-prior-art reviewer:** "Steam is fine for the user-facing System Requirements page UX, but the closer prior-art for the *declarative-schema* side is iOS `UIRequiredDeviceCapabilities` + Android `<uses-feature>` + msbuild `<TargetFramework>`. Apple's `UIRequiredDeviceCapabilities` is the direct prior-art for `HardwareSpec.RequiredSensors`; Android's `android:required` attribute is the direct prior-art for the Required/Recommended split; msbuild's `<TargetFramework>` is the direct prior-art for `RuntimeSpec.MinDotnetVersion`. ADR 0063 reinvents the cross-platform substrate version of these without citing them. Cite them. On Steam's UX shape specifically: Steam actually does both inline-summary AND full-page expanded-view; ADR 0063 says inline only. Steam's full pattern is summary-then-expand; either commit to that (better UX) or be explicit that ADR 0063 ships only the summary tier (acceptable v0 if named)." Drives F12.

- **Cited-symbol / cohort-discipline reviewer:** "Three-direction spot-check ran on every cited symbol. Positive-existence: all 8 cited ADRs verified Accepted on `origin/main` (clean). Negative-existence: `MinimumSpecDimension` does not exist in ADR 0062 (drove F1); `Synced`/`Local`/`Quarantined` do not exist in ADR 0036 (drove F2; only `Stale` partial-matches the labels-vs-identifiers conflation); `Tier1Mdns` does not exist in ADR 0061's `TransportTier` enum (drove F3); `foundation-bundles` does not exist in `packages/` (drove F5). Structural-citation correctness: `OperatorRecoveryHint` diverges from ADR 0062 post-A1.11's canonical `OperatorRecoveryAction` (drove F4 — vocabulary fork within the same package). Four structural-citation failures total — the highest count in this cohort. Mechanical fixes; no architectural redesign needed. The §A0 audit's self-check passed its own internal list; the council's external spot-check caught all four, validating the cohort lesson that pre-merge council remains canonical for substrate amendments." Drives F1 + F2 + F3 + F4 + F5 + A15.

- **UX / authoring-cost reviewer:** "Bundle authoring cost: 10 dimension-spec record types + per-platform overrides + spec policies = a substantial declaration surface. The kitchen-sink demo example needs to ship as a *complete* reference (not a partial sketch) so authors copy-paste a known-good shape. `WarnOnly` warning persistence: dismissed-per-version-per-install is the right rule; spec it. Operator 'Force Install' link visibility: server-side render gate only when role is operator (so non-operators never see the link in the DOM); spec it. Localization: spec authoring should be enum/structural data (no strings); rendering produces `LocalizedString?` outputs from default keys; clarify this. On `Informational` policy: useful for documenting what a feature *uses* (not just what it *requires*) — e.g., a bundle that uses GPS for distance calculation but works without it; surfacing this to the user reduces 'why does this app want my location?' confusion. Keep the Informational tier; it's a real UX win when applied right." Drives F8 + F10 + F11 + F13 + F14.

---

## 6. Cohort discipline scorecard

| Cohort baseline (before this review) | This council review |
|---|---|
| **Substrate-amendment council batting average:** 13-of-13 amendments needed council fixes | **14-of-14** if A1–A11 fixes applied post-merge (or pre-merge per current §"Cohort discipline" auto-merge-DISABLED posture). Cohort lesson holds: every substrate ADR/amendment so far has needed council fixes. |
| **Council false-claim rate (all three directions):** 2-of-11 prior councils | **0-of-15 spot-checks fired in this review.** All findings were positive-existence / negative-existence / structural-citation correctness verified twice before publishing. F1's `MinimumSpecDimension` non-existence verified by full re-read of ADR 0062's "New types" list at line 796. F2's sync-state-name non-existence verified by full re-read of ADR 0036's decision section (lines 35–47). F3's `Tier1Mdns` non-existence verified by direct enum-source read at ADR 0061 line 92–97. F5's `foundation-bundles` non-existence verified via `ls packages/`. F4's vocabulary-fork verified via direct comparison to ADR 0062 line 173 + post-A1.11 (line 762). |
| **Structural-citation failure rate (XO-authored):** 6-of-13 amendments | **4-of-13 amendments + this one = 5-of-14 prior + 4 in this single ADR = high water mark.** ADR 0063 alone contributes 4 structural-citation findings, more than any prior single ADR/amendment in the cohort. The §A0 self-audit pattern (introduced post-A6 council) caught zero of these four; the council's external three-direction spot-check caught all four. **The §A0 audit is necessary but not sufficient; council remains canonical.** |
| **Severity profile** | 3 Critical (F1, F2, F3) + 1 Critical-systems (F6) = **4 Critical**; 7 Major (F4, F5, F7, F8, F9, F10, F11); 2 Minor (F12, F15) + 2 Encouraged (F13, F14) = **15 findings** + **9 verification-pass findings (F-VP1 to F-VP9)** for 24 total. |
| **Pre-merge council vs post-merge council** | **Pre-merge** — auto-merge intentionally DISABLED per ADR 0063's own §"Cohort discipline" posture; matches ADR 0062 + ADR 0028-A1 / A6 / A8 cohort discipline. Pre-merge cost: 1–2 hours of XO ADR editing; zero downstream rework. Post-merge cost would be: re-author 4 type/value/package citations, fix 1 internal contradiction, add 4 missing spec subsections (cost class, warning persistence, force-install audit, override merge rule) = ~3–4 hours of XO + risk of partial-fix landing in tranches. **Pre-merge is dramatically cheaper.** |
| **Substrate-amendment vs new-ADR ratio** | This is a new ADR (not an amendment), like 0061 + 0062. Cohort discipline observation: new ADRs in the W#33 lineage have averaged 4–6 council-fix amendments; ADR 0063's 11 required amendments is the high-water mark, driven by the 4-stack of structural-citation failures. The W#33 §7.2 follow-on queue should treat this ADR's lesson as a discipline reset — the §A0 audit pattern needs council enforcement. |

The cohort lesson holds: every substrate ADR/amendment so far has needed council fixes; pre-merge council is dramatically cheaper than post-merge; structural-citation correctness is the most-frequent failure mode and the §A0 audit alone is not sufficient — it requires three-direction council spot-check.

---

## 7. Closing recommendation

**Accept ADR 0063 with required amendments A1–A11 applied before any bundle author writes their first `Requirements: MinimumSpec?` declaration.** The architectural decision (bundle-manifest-as-unit; Steam-style System Requirements UX; `MinimumSpec` as a structured filter on `MissionEnvelope`; per-platform overrides as data not branching code; reuse ADR 0062 + ADR 0028-A8 events for post-install regression) is correct and consistent with substrate-cohort design taste. The 11 required mechanical fixes are 1–2 hours of XO work; the 4 structural-citation failures (F1, F2, F3, F5) are search-and-replace fixes; the 1 internal contradiction (F7) is one paragraph rewrite; the 4 missing-spec subsections (F8 cost-class, F10 warning-persistence, F11 force-install audit, A9 cost-class) are each one paragraph; F6 is a field rename + one doc-comment.

Do **NOT** promote to `Accepted` until A1–A11 land. The four structural-citation failures directly affect the contract surface that Stage 02 will commit to — Stage 06 build will fail at compile time on F2 (`SyncState.Local` doesn't exist) or F3 (`TransportTier.Tier1Mdns` doesn't exist), so the discipline is enforceable both by code-review and by `dotnet build`. Estimated rewrite cost: 1–2 hours of ADR editing, zero code changes (Phase 1 substrate has not yet been built), zero downstream-intake-rework.

If A1–A11 do not land within ~1 working day of council acceptance, the right move is **Reject and re-propose** rather than letting four structural-citation failures + one internal contradiction ship to Stage 02 — which is the same call the council made on ADR 0053's state-set merge, and which the cohort batting average says is the cheaper path.

The §A0 cited-symbol audit pattern (introduced post-A6 council) needs to evolve: the self-audit caught zero of the four structural-citation failures in this ADR. **Recommendation for cohort discipline going forward (per A15):** the council reviewer applies the three-direction spot-check (positive-existence, negative-existence, structural-citation correctness) to the §A0 audit itself, not just to the contract surface. Pre-merge council remains canonical for substrate ADRs in the W#33 lineage.

W#33 §7.2 follow-on queue ordering: ADR 0063 was the fourth item; with A1–A11 applied, the queue can advance to the fifth (ADR 0064 — paper §11 jurisdiction + consent mechanics; per W#33 §7.2). The cohort lesson from this review (4 structural-citation failures from one §A0 self-audit) should be carried forward as discipline guidance for ADR 0064's authoring: §A0 audit MUST be three-direction-verified by the author before council, with explicit `git show origin/main:<file>` evidence cited.

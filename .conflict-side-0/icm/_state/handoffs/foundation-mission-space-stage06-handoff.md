# Hand-off — Foundation.MissionSpace Phase 1 substrate (ADR 0062 + A1)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-01
**Status:** `ready-to-build`
**Spec source:** [ADR 0062 + A1](../../docs/adrs/0062-mission-space-negotiation-protocol.md) (post-A1 council-fixed surface; landed via PR #406)
**Approval:** ADR 0062 + A1 Accepted on origin/main; council batting average 17-of-17 + A1.13 + A1.20 stance reframe + A8.4 / A8.5 council-discipline anchors all integrated
**Estimated cost:** ~10–14h sunfish-PM (foundation-tier substrate package + ~16 type signatures + central coordinator + 10-dimension probe pattern + 5-value DegradationKind taxonomy + 9 audit constants + ~30–40 tests + DI + apps/docs page)
**Pipeline:** `sunfish-feature-change`
**Audit before build:** `ls /Users/christopherwood/Projects/Sunfish/packages/ | grep mission-space` to confirm no collision (audit not yet run; COB confirms before commit)

---

## Context

Phase 1 lands the Foundation.MissionSpace runtime-negotiation substrate per ADR 0062 + A1. **This is the canonical-load-bearing substrate for the entire W#33 §7.2 cohort** — every dimension, every gate, every install-UX surface, every regulatory rule, every Bridge subscription event handler ultimately surfaces through `IMissionEnvelopeProvider` + `IFeatureGate<TFeature>`. ADR 0063, ADR 0064, all 4 sibling amendments (ADR 0028-A9 / ADR 0036-A1 / ADR 0007-A1 / ADR 0031-A1), and W#36's Anchor-side handler all reference types in this substrate.

**Substrate scope:** `Sunfish.Foundation.MissionSpace` package + `IMissionEnvelopeProvider` central coordinator + `IFeatureGate<TFeature>` per-feature gates + 10 default `IDimensionProbe<TDimension>` implementations + `IFeatureBespokeProbe<TBespokeSignal>` extension surface + 5-value `DegradationKind` taxonomy + 9 new `AuditEventType` constants + `ICapabilityForceEnableSurface` operator-only override + `LocalizedString` per A1.11 + DI extension + apps/docs page. **Substrate-only**; consumer wiring (per-feature gate authoring across the cohort) is separate workstreams.

This hand-off mirrors the W#34 + W#35 + W#36 substrate-only patterns COB has executed successfully. **Largest remaining substrate item** — matches W#34's effort estimate.

---

## Files to create

### Package scaffold

```
packages/foundation-mission-space/
├── Sunfish.Foundation.MissionSpace.csproj
├── README.md
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs        (AddInMemoryMissionSpace; mirrors W#34 P5 + W#35 P5 + W#36 P5 + W#39 P5 shape)
├── Models/
│   ├── MissionEnvelope.cs                    (record per A1.2; 10 dimensions)
│   ├── DimensionChangeKind.cs                (enum; 10 values per A1.2)
│   ├── EnvelopeChange.cs                     (record per A1.2; Previous + Current + ChangedDimensions)
│   ├── EnvelopeChangeSeverity.cs             (enum: Informational / Warning / Critical / ProbeUnreliable per A1.10)
│   ├── FeatureVerdict.cs                     (record per A1.2 — post-A1.2 IFeature rename)
│   ├── FeatureAvailabilityState.cs           (enum: Available / DegradedAvailable / Unavailable per A1.2 rename)
│   ├── DegradationKind.cs                    (enum; 5 values per A1.2)
│   ├── ProbeStatus.cs                        (enum per A1.10; 5 values: Healthy / Stale / Failed / PartiallyDegraded / Unreachable)
│   ├── ProbeCostClass.cs                     (enum: Low / Medium / High / DeepHigh / Live per A1.6)
│   ├── LocalizedString.cs                    (record per A1.11)
│   ├── ForceEnablePolicy.cs                  (enum: NotOverridable / OverridableWithCaveat / Overridable per A1.9)
│   ├── ForceEnableRecord.cs                  (record per A1.2)
│   ├── FeatureForceEnableRequest.cs          (record per A1.2)
│   ├── ForceEnableNotPermittedException.cs   (exception per A1.9)
│   └── Dimensions/
│       ├── HardwareCapabilities.cs           (record per A1.2)
│       ├── UserCapabilities.cs               (record per A1.2)
│       ├── RegulatoryCapabilities.cs         (record per A1.2; consumed by ADR 0064 W#39)
│       ├── RuntimeCapabilities.cs            (record per A1.2)
│       ├── FormFactorProfile.cs              (consume from W#35 Sunfish.Foundation.Migration; cross-package reference)
│       ├── EditionCapabilities.cs            (record per A1.2 + A1.6 + A1.8 — wraps ADR 0009 IEditionResolver edition key)
│       ├── NetworkCapabilities.cs            (record per A1.2; consume from W#30 Sunfish.Foundation.Transport when stuck PRs unblock)
│       ├── TrustAnchorCapabilities.cs        (record per A1.8 rename)
│       ├── SyncStateSnapshot.cs              (record per A1.2; consume from ADR 0036 SyncState enum once W#37 lands)
│       └── VersionVector.cs                  (consume from W#34 Sunfish.Foundation.Versioning per A1.3.2 plugin-map shape)
├── Services/
│   ├── IMissionEnvelopeProvider.cs           (per A1.2 coordinator contract)
│   ├── DefaultMissionEnvelopeProvider.cs     (single-flight on cache-miss + per-cost-class wall-clock timeout + observer fanout per A1.4)
│   ├── IMissionEnvelopeObserver.cs           (per A1.2 subscriber contract)
│   ├── IFeatureGate.cs                       (per A1.2 — IFeatureGate<TFeature> generic; renamed from ICapabilityGate per A1.2)
│   ├── IFeature.cs                           (marker interface per A1.2)
│   ├── IDimensionProbe.cs                    (per A1.2 — IDimensionProbe<TDimension> generic)
│   ├── IFeatureBespokeProbe.cs               (per A1.2 — IFeatureBespokeProbe<TBespokeSignal> generic)
│   ├── IBespokeSignal.cs                     (marker interface per A1.2)
│   ├── IFeatureForceEnableSurface.cs         (per A1.2 + A1.9 — operator-only override)
│   └── DefaultFeatureForceEnableSurface.cs   (in-memory; checks ForceEnablePolicy per A1.9)
├── Probes/
│   └── (10 default probes; one per dimension; each implements IDimensionProbe<TDimension>)
├── Audit/
│   └── MissionSpaceAuditPayloads.cs          (factory; 9 event types per A1.7 + A1.12)
└── tests/
    └── Sunfish.Foundation.MissionSpace.Tests.csproj
        ├── MissionEnvelopeTests.cs            (10-dimension round-trip; canonical-JSON shape; envelopeHash sha256)
        ├── DefaultMissionEnvelopeProviderTests.cs  (single-flight; per-cost-class timeout; observer fanout 100ms coalescing)
        ├── IFeatureGateTests.cs                (5-value DegradationKind taxonomy verdicts)
        ├── ProbeDependencyTests.cs            (per A1.7 topological sort + cascading failures)
        ├── CacheInvalidationTests.cs          (per A1.7 — Healthy → Stale invalidates verdicts)
        ├── ProbeStatusTests.cs                 (per A1.10 — 5-value enum + EnvelopeChangeSeverity.ProbeUnreliable)
        ├── ForceEnablePolicyTests.cs          (per A1.9 — Hardware/Runtime NotOverridable; Regulatory/EditionCapabilities OverridableWithCaveat; rest Overridable; FeatureForceEnableRejected audit)
        ├── LocalizedStringTests.cs             (per A1.11 — Key + DefaultValue round-trip)
        ├── AuditEmissionTests.cs              (9 AuditEventType constants emit on right triggers + dedup)
        ├── DiExtensionTests.cs                (audit-disabled / audit-enabled overloads; both-or-neither at registration boundary)
        └── ForceEnableSurfaceTests.cs          (operator-only role check; FeatureForceEnabled + FeatureForceRevoked + FeatureForceEnableRejected audit emissions)
```

### Type definitions (post-A1 surface; implement exactly per ADR 0062 + A1)

Use the post-A1.2 IFeature / IFeatureGate naming throughout (NOT the original ICapability / ICapabilityGate). Use post-A1.6 cost class split (High = 30s TTL; DeepHigh = 1h TTL). Use post-A1.10 ProbeStatus + EnvelopeChangeSeverity.ProbeUnreliable. Use post-A1.11 LocalizedString. Use post-A1.9 ForceEnablePolicy taxonomy.

### Audit constants (9 per A1.7 + A1.12.1)

`AuditEventType` MUST gain 9 new constants in `packages/kernel-audit/AuditEventType.cs`:

```csharp
public static readonly AuditEventType FeatureProbed                      = new("FeatureProbed");                    // A1.2 rename
public static readonly AuditEventType FeatureAvailabilityChanged         = new("FeatureAvailabilityChanged");        // A1.2 rename
public static readonly AuditEventType FeatureProbeFailed                 = new("FeatureProbeFailed");                // A1.2 rename
public static readonly AuditEventType FeatureForceEnabled                = new("FeatureForceEnabled");               // A1.2 rename
public static readonly AuditEventType FeatureForceRevoked                = new("FeatureForceRevoked");               // A1.2 rename
public static readonly AuditEventType FeatureForceEnableRejected         = new("FeatureForceEnableRejected");        // A1.9 new
public static readonly AuditEventType MissionEnvelopeChangeBroadcast     = new("MissionEnvelopeChangeBroadcast");    // A1.2 rename
public static readonly AuditEventType MissionEnvelopeObserverOverflow    = new("MissionEnvelopeObserverOverflow");   // A1.4 new
public static readonly AuditEventType FeatureVerdictSurfaced             = new("FeatureVerdictSurfaced");            // A1.12 new
```

Total: 9 constants. Per A1 collision check completed in council review (no collisions). `MissionSpaceAuditPayloads` factory mirrors the cohort's `LeaseAuditPayloadFactory` shape (alphabetized keys; per ADR 0049).

---

## Phase breakdown (~5 PRs, ~10–14h total — mirrors W#34/W#35 shape)

### Phase 1 — Substrate scaffold + core types + dimension records (~2-3h, 1 PR)

- Package created at `packages/foundation-mission-space/` with foundation-tier csproj
- All Models per the spec block above (~16 types + 10 dimension records)
- 9 new `AuditEventType` constants in `packages/kernel-audit/AuditEventType.cs`
- `MissionEnvelope` round-trip via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` test (camelCase per ADR 0028-A7.8)
- `JsonStringEnumConverter` for all 5 enum types (`DimensionChangeKind`, `EnvelopeChangeSeverity`, `FeatureAvailabilityState`, `DegradationKind`, `ProbeStatus`, `ProbeCostClass`, `ForceEnablePolicy`)
- `envelopeHash` field per A1.2 — sha256 of canonical-JSON encoding for change-detection
- README.md per the standard package-README pattern
- ~6–10 unit tests on Models alone

### Phase 2 — Coordinator + observer fanout + cache invalidation (~2-3h, 1 PR)

- `IMissionEnvelopeProvider` + `DefaultMissionEnvelopeProvider` per A1.4 single-flight + per-cost-class timeout
- `IMissionEnvelopeObserver` + observer fanout policy per A1.4 (100ms coalescing window; 100-pending bound; oldest-first overflow)
- Per-cost-class wall-clock timeout: Low 1s; Medium 2s; High 5s; DeepHigh 10s; Live N/A
- `MissionEnvelopeObserverOverflow` audit emission on observer queue overflow
- Cache invalidation on probe-status transition per A1.7 (Healthy → Stale invalidates verdicts)
- Tests: single-flight (N concurrent callers; 1 probe); timeout (probe exceeds budget; last-known-cached returned); observer fanout (100ms coalescing; overflow → MissionEnvelopeObserverOverflow audit)

### Phase 3 — IFeatureGate + DegradationKind + ProbeStatus + ForceEnablePolicy (~2-3h, 1 PR)

- `IFeature` marker + `IFeatureGate<TFeature>` generic interface + `FeatureVerdict` record per A1.2
- 5-value `DegradationKind` taxonomy implementation per A1.2 + apps/docs UX-surface conformance per ADR 0036 + paper §13.2
- `ProbeStatus` enum + integration with `MissionEnvelope` per A1.10
- `ForceEnablePolicy` per-dimension taxonomy per A1.9:
  - Hardware / Runtime: `NotOverridable` (throws ForceEnableNotPermittedException)
  - Regulatory / EditionCapabilities: `OverridableWithCaveat` (DegradedAvailable + UX caveat)
  - User / Network / Trust / SyncState / VersionVector / FormFactor: `Overridable`
- `IFeatureForceEnableSurface` + `DefaultFeatureForceEnableSurface` per A1.2 + A1.9
- `FeatureForceEnableRejected` audit emission per A1.9
- Tests: 5 DegradationKind verdicts; 9 dimension force-enable policy paths; rejection audit

### Phase 4 — 10 default IDimensionProbe<TDimension> implementations + IFeatureBespokeProbe extension surface (~2-3h, 1 PR)

- 10 default probes per A1.5 dimension table:
  - HardwareDimensionProbe (Low cost; OS API local query)
  - UserDimensionProbe (Low; identity store query)
  - RegulatoryDimensionProbe (Medium; bundled jurisdiction-DB; ADR 0064 W#39 substrate consumes this)
  - RuntimeDimensionProbe (Low; local API queries)
  - FormFactorDimensionProbe (Low; consume `FormFactorProfile` from W#35 `Sunfish.Foundation.Migration`)
  - EditionCapabilitiesDimensionProbe (High = 30s TTL per A1.6; consumes ADR 0009 IEditionResolver + W#36 Bridge subscription event handler)
  - NetworkDimensionProbe (Low; local network stack)
  - TrustDimensionProbe (Low; trust-anchor inspection)
  - SyncStateDimensionProbe (Live; consume ADR 0036 sync-state observable + W#37 SyncState enum once landed)
  - VersionVectorDimensionProbe (Medium; consume W#34 Sunfish.Foundation.Versioning IVersionVectorExchange)
- `IFeatureBespokeProbe<TBespokeSignal>` extension surface per A1.2 (for non-canonical signals)
- Probe dependencies per A1.7 (topological sort at startup + on full re-probe; failures cascade)
- Tests: 10 per-dimension probe round-trips; probe-dependency cascading-failure scenarios

### Phase 5 — Audit emission + DI extension + apps/docs + ledger flip (~2-3h, 1 PR)

- `MissionSpaceAuditPayloads` factory (alphabetized keys per ADR 0049 convention)
- All 9 AuditEventType constants connected; per-event dedup wiring per ADR 0028-A6.5.1 pattern (5-min `FeatureProbed`; 1-min `FeatureAvailabilityChanged`; 30-second `FeatureVerdictSurfaced`; etc.)
- Two-overload constructor (audit-disabled / audit-enabled both-or-neither) per W#32 + W#34 + W#35 + W#36 precedent
- `AddInMemoryMissionSpace()` DI extension (audit-disabled + audit-enabled overloads; both-or-neither at registration boundary)
- `apps/docs/foundation-mission-space/overview.md` walkthrough page (cite ADR 0062 + post-A1 surface explicitly + DegradationKind 5-value taxonomy)
- Active-workstreams.md row 40 flipped from `building` → `built` with PR list
- ~30-40 tests passing across all phases

---

## Halt-conditions (cob-question if any of these surface)

1. **Cross-package dimension dependencies.** Phase 4's 10 default probes consume types from sibling packages:
   - `FormFactorProfile` from W#35 Sunfish.Foundation.Migration ✓ (built)
   - `VersionVector` from W#34 Sunfish.Foundation.Versioning ✓ (built)
   - `SyncState` enum from ADR 0036-A1 (W#37 hand-off; PR #448 stuck on conflict)
   - `NetworkCapabilities` from W#30 Sunfish.Foundation.Transport (W#30 P1-P5 built; P6/P7/P8 stuck on conflict)
   - ADR 0009 `IEditionResolver` ✓ (existing on origin/main)
   - W#36 Bridge subscription event handler ✓ (built)

   If a referenced type is unavailable (W#37 / W#30 P6+ stuck), Phase 4 substrate ships a stub-with-throw or interface-only consumer for that probe; full integration lands when the stuck PR resolves. File `cob-question-*` beacon if the stub-vs-stuck question is unclear.

2. **`EnvelopeHash` SHA-256 canonical-JSON computation.** Per A1.2 the `envelopeHash` field is sha256 over the canonical-JSON-encoded envelope (excluding the hash field itself). The hash is used for change-detection. If the SHA-256 implementation seems to round-trip differently across platforms (Windows/macOS/Linux), file `cob-question-*` beacon — `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` should produce byte-identical output across platforms, but if not, document the platform-specific fix.

3. **Observer fanout 100ms coalescing window.** Per A1.4 the fanout policy coalesces dimension changes within a 100ms window into a single `EnvelopeChange`. If the observer pattern needs to be event-loop-aware (e.g., Anchor MAUI's UI thread vs Bridge ASP.NET Core's request-handler thread), file `cob-question-*` beacon — the coalescing logic may need per-platform timer pumping.

4. **`DegradationKind` UX surface conformance.** Per A1.2 + ADR 0036 + paper §13.2 conformance: all 5 DegradationKind values produce specific UI per the cited references. Phase 3 substrate ships only the type + verdict; the actual rendering lives in Anchor MAUI / Bridge React / iOS adapters (consumers of this substrate; NOT in Phase 1 scope). If COB starts implementing the rendering, file `cob-question-*` beacon — that's W#41+ territory.

5. **Force-enable composition with W#22 / W#28 / W#36 consumers.** Per A1.9 + ADR 0064-A1.11 + W#36 force-enable surface: the substrate's `IFeatureForceEnableSurface` is the canonical force-enable mechanism; per-domain force-enable adapters (W#22 Phase 6 compliance half; W#28 capability-tier overrides; W#36 commercial-tier overrides) consume this substrate. If a consumer requires force-enable signal that doesn't fit the 9-dimension taxonomy, file `cob-question-*` beacon — the answer may be "ship as IFeatureBespokeProbe<TBespokeSignal>" rather than augmenting the canonical 10 dimensions.

6. **`MissionEnvelope` mutability surface.** Per A1.2 the envelope is immutable; change events ship updated snapshots. If a consumer wants in-place mutation (e.g., for performance under tight loops), file `cob-question-*` beacon — the answer is "the envelope is intentionally immutable; consumers can hold a reference to the latest snapshot or subscribe to changes."

7. **Cohort stuck-PR cross-package coupling.** W#30 P6/P7/P8 + W#37 are stuck on commitlint+conflict. W#40's Phase 4 probes reference W#30 NetworkCapabilities + W#37 SyncState. If the stuck status blocks Phase 4 implementation, file `cob-question-*` beacon — XO will ship interface-stubs (e.g., `Sunfish.Foundation.MissionSpace.Stubs.NetworkCapabilities` placeholder) for Phase 4 to compile against; full integration lands when W#30/W#37 unstick.

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-01):**

- ADR 0062 + A1 (PR #406 merged post-A1) — substrate spec source ✓
- ADR 0028-A6+A7 / A5+A8 / A9 / A10 (W#33 substrate amendments) ✓
- ADR 0009 (Foundation.FeatureManagement; IEditionResolver) ✓
- ADR 0036 (sync-state encoding contract) ✓
- ADR 0036-A1 (Sunfish.Foundation.UI.SyncState public enum; PR #427 merged) ✓
- ADR 0049 (audit substrate) ✓
- W#34 Sunfish.Foundation.Versioning (built; PR #423) ✓
- W#35 Sunfish.Foundation.Migration (built; PR #446) ✓
- W#36 Sunfish.Bridge.Subscription (built; PR #458) ✓
- ADR 0028-A8.6 + A1.10 — CanonicalJson unknown-key tolerance verified ✓
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` ✓
- `Sunfish.Kernel.Audit.AuditEventType` ✓

**Introduced by this hand-off** (ship in Phase 1):

- New package: `Sunfish.Foundation.MissionSpace`
- ~16 types per Models + Services blocks above
- 10 default `IDimensionProbe<TDimension>` implementations
- 9 new `AuditEventType` constants per A1.7 + A1.12
- `MissionSpaceAuditPayloads` factory class
- Apps/docs walkthrough at `apps/docs/foundation-mission-space/overview.md`

**Cohort lesson reminder (per ADR 0028-A10 + ADR 0063-A1.15):** §A0 self-audit pattern is necessary but NOT sufficient. COB should structurally verify each Sunfish.* symbol exists (read actual cited file's schema; don't grep alone) before declaring AP-21 clean.

---

## Cohort discipline

This hand-off is **not** a substrate ADR amendment; it's the largest Stage 06 hand-off in the W#33-derived chain. Pre-merge council on this hand-off is NOT required.

- COB's standard pre-build checklist applies
- W#34 + W#35 + W#36 + W#39 cohort lessons incorporated: ConcurrentDictionary dedup; two-overload constructor both-or-neither; JsonStringEnumConverter for all enum types; AddInMemoryX() DI extension naming; apps/docs/{tier}/X/overview.md page convention; reader-caution discipline (per W#39 A1.18) for any user-facing UX surface

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w40-{slug}.md` in `icm/_state/research-inbox/`
- Halt the workstream + add a note in active-workstreams.md row 40
- ScheduleWakeup 1800s

If COB completes Phase 5 + drops to fallback:

- Drop `cob-idle-2026-05-XXTHH-MMZ-{slug}.md` to research-inbox
- Continue with rung-1 dependabot + rung-2 build-hygiene per CLAUDE.md fallback

---

## Cross-references

- Spec source: ADR 0062 + A1 (PR #406 merged post-A1)
- Council that drove A1: PR #408 (merged); council file at `icm/07_review/output/adr-audits/0062-council-review-2026-04-30.md`
- Sibling workstreams in flight / queued / built:
  - W#23 ready-to-build (iOS Field-Capture; consumes A9 envelope-augmentation per addendum)
  - W#34 built (Foundation.Versioning; provides VersionVector type)
  - W#35 built (Foundation.Migration; provides FormFactorProfile)
  - W#36 built (Bridge subscription emitter; provides EditionCapabilities source)
  - W#37 stuck-pending-conflict (Foundation.UI.SyncState; provides SyncState enum)
  - W#38 ready-to-build (BusinessCaseBundleManifest.Requirements field)
  - W#39 ready-to-build (Foundation.MissionSpace.Regulatory; consumes this substrate)
  - W#40 (this hand-off)
- W#33 §7.2 follow-on queue: closed (5/5 substrate ADRs landed); the cohort is now in Stage 06 build phase
- ADR 0062-A1.6 halt-condition closed by W#36 Bridge subscription event emitter (PR #458 built)

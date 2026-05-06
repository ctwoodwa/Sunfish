---
type: question
workstream-or-chapter: W#54 Phase 2b — SickBayDataProvider Mission Envelope integration
last-pr: pending
---

PR for W#54 Phase 2 ships the four reference impls (SickBayDataProvider stub +
DefaultStretcherBearerPolicy + DefaultFirstAidSurface + NoopKeyRotationScheduler)
plus the H4 reflection test. The Mission Envelope integration is deferred to
Phase 2b for two reasons:

**1. Hand-off API drift (H2.A halt).** The hand-off §2.1 cited
`IMissionEnvelopeProvider.GetCurrentEnvelope(tenant)` — the actual contract on
origin/main is `GetCurrentAsync(ct)` (no tenant param; MissionEnvelope is
process-level not tenant-level). Hand-off needs an addendum.

**2. Dimension-to-probe-status mapping is undefined.** The hand-off says
"map MissionEnvelope DegradationKind counts → AtmosphereHealth" but
`MissionEnvelope` exposes typed capability records (Hardware, User, Regulatory,
Runtime, FormFactor, Edition, Network, TrustAnchor, SyncState, VersionVector),
NOT a flat probe-result list with `ProbeStatus`/`DegradationKind` fields. The
projection logic needs an XO ruling on:
- Which dimensions on `MissionEnvelope` carry observable degradation
  (e.g., does `RuntimeCapabilities` expose a `DegradationKind`)?
- How are `WarningProbeCount` / `CriticalProbeCount` derived from the typed
  dimension records?
- Or alternatively: does `DefaultMissionEnvelopeProvider` expose a richer
  surface for downstream consumers like Sick Bay that need the probe-level
  view?

**Phase 2 PR posture:** SickBayDataProvider returns a Green-stub
`AtmosphereReadout` (0 warnings, 0 criticals, ForceEnableActive=false) and an
empty `Lab` list. The Pharmacy projection works (sources from
`SickBayOptions.RegisteredFieldPurposes`). Push-driven invalidation via
`IMissionEnvelopeObserver.Subscribe` is NOT wired; SubscribeSnapshotAsync
emits one snapshot then polls on `FallbackPollingInterval`.

**What would unblock me:** XO ruling on the dimension-to-probe-status mapping
+ hand-off addendum updating the §2.1 API references.

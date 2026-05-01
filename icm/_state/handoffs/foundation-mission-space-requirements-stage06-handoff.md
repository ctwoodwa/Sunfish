# Hand-off — Foundation.MissionSpace.Requirements Phase 1 substrate (ADR 0063 + A1)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-01
**Status:** `ready-to-build`
**Spec source:** [ADR 0063 + A1](../../docs/adrs/0063-mission-space-requirements.md) (post-A1 council-fixed; landed via PR #411)
**Approval:** ADR 0063 + A1 Accepted on origin/main; council batting average 14-of-14; A1 absorbed all 11 Required council recommendations + 4 Encouraged
**Estimated cost:** ~6–8h sunfish-PM (extends W#40's `Sunfish.Foundation.MissionSpace` package + `MinimumSpec` schema + `IMinimumSpecResolver` + `ISystemRequirementsRenderer` interface (impls deferred) + 5 audit constants + ~25 tests + DI extension update + apps/docs page + W#38 stub-replacement)
**Pipeline:** `sunfish-feature-change`
**Audit before build:** verify W#40 substrate built (PR #466 merged) and W#38 BusinessCaseBundleManifest.Requirements field built (PR #462 merged) — both prereqs are now on origin/main

---

## Context

W#41 ships ADR 0063 Phase 1 substrate. Per ADR 0063-A1's location guidance: **"Located in `Sunfish.Foundation.MissionSpace` (extends ADR 0062's package; same DI extension `AddSunfishMissionSpace()`)"** — W#41 EXTENDS W#40's package rather than creating a new one.

**Substrate scope:** Add `MinimumSpec` schema (10 per-dimension spec records) + `SpecPolicy` enum + `PerPlatformSpec` overrides + `IMinimumSpecResolver` evaluation contract + `SystemRequirementsResult` + `ISystemRequirementsRenderer` per-platform UX interface (concrete renderers deferred to per-adapter Stage 06 work) + 5 new `AuditEventType` constants (per A1.11 + A1.12) + force-install operator-only override composition + DI extension update. **Substrate-only**; consumer wiring (Anchor MAUI / Bridge React / iOS SystemRequirementsRenderer impls) is separate per-adapter Stage 06 workstreams.

**W#38 stub replacement:** W#38 currently ships a temporary stub `Sunfish.Foundation.Catalog.Bundles.MinimumSpec` (per the W#38 stub-unblock addendum). W#41 ships the canonical `Sunfish.Foundation.MissionSpace.MinimumSpec` AND deprecates the stub via a follow-up small PR (or as part of Phase 5). The W#38 stub's TODO comment block already documents the future-rename plan.

This hand-off mirrors the W#34 + W#35 + W#36 + W#39 + W#40 substrate-only patterns COB has executed successfully. Smaller scope than W#40 due to the install-UX renderer being deferred.

---

## Files to extend (NOT new package)

W#41 EXTENDS `packages/foundation-mission-space/` per ADR 0063-A1. New files:

```
packages/foundation-mission-space/
├── (existing W#40 surface — unchanged)
├── Models/
│   ├── (existing W#40 dimension records — unchanged)
│   ├── MinimumSpec.cs                        (record per A1.1 + A1.6 unit alignment)
│   ├── SpecPolicy.cs                         (enum: Required / Recommended / Informational per A1.1)
│   ├── PerPlatformSpec.cs                    (record per A1.7 COMPOSE rule)
│   ├── HardwareSpec.cs                       (record per A1.6 + A1.1 — MinMemoryBytes long? not int? Gb)
│   ├── UserSpec.cs                           (record per A1.1)
│   ├── RegulatorySpec.cs                     (record per A1.1)
│   ├── RuntimeSpec.cs                        (record per A1.1)
│   ├── FormFactorSpec.cs                     (record per A1.1; consume FormFactorKind from W#35 Sunfish.Foundation.Migration)
│   ├── EditionSpec.cs                        (record per A1.1; AllowedEditions: IReadOnlySet<string>?)
│   ├── NetworkSpec.cs                        (record per A1.3 — RequiredTransports: { LocalNetwork } per ADR 0061's TransportTier enum; consume from W#30 Sunfish.Foundation.Transport)
│   ├── TrustSpec.cs                          (record per A1.1)
│   ├── SyncStateSpec.cs                      (record per A1.2 — AcceptableStates: IReadOnlySet<SyncState>; consume W#37's Sunfish.Foundation.UI.SyncState enum)
│   ├── VersionVectorSpec.cs                  (record per A1.1; MinKernelVersion + MinSchemaEpoch)
│   ├── OverallVerdict.cs                     (enum: Pass / WarnOnly / Block per A1.8 explicit Informational rule)
│   ├── DimensionPolicyKind.cs                (enum: Required / Recommended / Informational / Unevaluated per A1.1)
│   ├── DimensionPassFail.cs                  (enum: Pass / Fail / Unevaluated per A1.1)
│   ├── DimensionEvaluation.cs                (record per A1.4 — uses post-A1.4 OperatorRecoveryAction NOT OperatorRecoveryHint)
│   ├── SystemRequirementsResult.cs           (record per A1.1; Overall + Dimensions + OperatorRecoveryAction)
│   └── SystemRequirementsRenderMode.cs       (enum: PreInstallFullPage / PostInstallInlineExplanation / PostInstallRegressionBanner)
├── Services/
│   ├── (existing W#40 service contracts — unchanged)
│   ├── IMinimumSpecResolver.cs               (per A1.1 evaluation contract)
│   ├── DefaultMinimumSpecResolver.cs         (compose per-platform per A1.7; cost class Medium per A1.6 + post-A1.6 30-sec cache TTL)
│   ├── ISystemRequirementsRenderer.cs        (per-adapter UX surface; W#41 ships interface only — concrete renderers are W#42+ per-adapter)
│   └── ISystemRequirementsSurface.cs         (platform-surface abstraction)
├── Audit/
│   └── (extend MissionSpaceAuditPayloads.cs from W#40 with 5 new constants per A1.11 + A1.12)
├── (existing W#40 DI extension — augment AddSunfishMissionSpace() to include AddMinimumSpec)
└── tests/
    └── (extend Sunfish.Foundation.MissionSpace.Tests with 7+ test files for the new surface)
        ├── MinimumSpecTests.cs                (round-trip; 10 dimensions; SpecPolicy 3 values; PerPlatformSpec compose per A1.7)
        ├── DefaultMinimumSpecResolverTests.cs (verdict transitions; Pass / WarnOnly / Block per A1.8 explicit Informational rule)
        ├── PerPlatformComposeTests.cs         (per A1.7 COMPOSE rule; not REPLACE)
        ├── ForwardCompatBidirectionalRoundTripTests.cs  (per A1.5 + A1.6 verification gate; option (ii) Dictionary<string, JsonNode> catch-all)
        ├── ForceInstallTests.cs                (per A1.11 — InstallForceEnabled audit emission; operator override composition with W#40 IFeatureForceEnableSurface)
        ├── ProbeStatusInvalidationTests.cs    (per A1.7 cache invalidation on probe-status transition Healthy → Stale)
        └── AuditEmissionTests.cs              (5 new AuditEventType constants emit on right triggers + dedup)
```

### W#38 stub deprecation (Phase 5 work item)

After W#41 lands, the W#38 stub at `packages/foundation-catalog/Bundles/MinimumSpec.cs` becomes a **wrapper-or-alias** that points to `Sunfish.Foundation.MissionSpace.MinimumSpec`. Per the W#38 stub-unblock addendum future-rename plan:

1. Replace stub's class body with `using MinimumSpec = Sunfish.Foundation.MissionSpace.MinimumSpec;` (C# alias) OR delete the stub file and add a using-directive to `BusinessCaseBundleManifest`'s source.
2. The `BusinessCaseBundleManifest.Requirements` field signature is unchanged — type rename is invisible to callers.
3. Add a deprecation TODO comment with a 90-day removal target (per stub-unblock addendum future-rename plan).

This deprecation can ship in Phase 5 of W#41 OR as a separate follow-up PR. Document the choice in the PR description.

---

## Type definitions (post-A1 surface; implement exactly per ADR 0063 + A1)

Use **post-A1.4 OperatorRecoveryAction** (NOT OperatorRecoveryHint). Use **post-A1.6 unit-alignment** (`MinMemoryBytes: long?` not `MinMemoryBytesGb: int?`). Use **post-A1.7 COMPOSE per-platform overrides** (NOT REPLACE). Use **post-A1.13 stance reframe** (the SyncStateSpec example values are `{ Healthy, Stale }` per ADR 0036-A1 canonical state names — not `{ Synced, Local }`). Use **post-A1.3 NetworkSpec.RequiredTransports** with `TransportTier` from W#30's `Sunfish.Foundation.Transport` (e.g., `{ LocalNetwork }` for offline-first; not `{ Tier1Mdns }` invented value).

### Audit constants (5 per A1.11 + A1.12)

`AuditEventType` MUST gain 5 new constants in `packages/kernel-audit/AuditEventType.cs`:

```csharp
public static readonly AuditEventType MinimumSpecEvaluated      = new("MinimumSpecEvaluated");
public static readonly AuditEventType InstallBlocked            = new("InstallBlocked");
public static readonly AuditEventType InstallWarned             = new("InstallWarned");
public static readonly AuditEventType PostInstallSpecRegression = new("PostInstallSpecRegression");
public static readonly AuditEventType InstallForceEnabled       = new("InstallForceEnabled");      // A1.11
```

Total: 5 constants. Per A1.11 + A1.12 collision check completed in council review (no collisions).

---

## Phase breakdown (~5 PRs, ~6–8h total — smaller than W#40 due to deferred renderer impls)

### Phase 1 — `MinimumSpec` schema + 10 per-dimension spec records (~2h, 1 PR)

- All Models per the spec block above (~16 new types extending W#40's package)
- 5 new `AuditEventType` constants in `packages/kernel-audit/AuditEventType.cs`
- `MinimumSpec` round-trip via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` test (camelCase per ADR 0028-A7.8)
- `JsonStringEnumConverter` for new enums (`SpecPolicy`, `OverallVerdict`, `DimensionPolicyKind`, `DimensionPassFail`, `SystemRequirementsRenderMode`)
- Cross-package consumption tests (`FormFactorKind` from W#35; `SyncState` from W#37; `TransportTier` from W#30; `EditionSpec.AllowedEditions` accepts ADR 0009 edition keys)
- ~10 unit tests on the new Models

### Phase 2 — `IMinimumSpecResolver` + per-platform compose + Pass/WarnOnly/Block verdicts (~2h, 1 PR)

- `IMinimumSpecResolver` + `DefaultMinimumSpecResolver` per A1.1 evaluation contract
- `PerPlatformSpec` COMPOSE rule per A1.7 (NOT REPLACE — for-each-dimension override-replaces-baseline; if only baseline declares, baseline applies)
- `OverallVerdict` 3-value with explicit Informational rule per A1.8 (Informational dimensions ignored for verdict)
- Forward-compat bidirectional round-trip test per A1.5 + A1.6 verification gate (option (ii) Dictionary<string, JsonNode> catch-all pattern)
- Cache invalidation on probe-status transition per A1.7
- 8-form-factor migration table integration (consume W#35 FormFactorProfile)
- ~10 tests covering verdict transitions + per-platform compose

### Phase 3 — Force-install + operator override composition + audit emission (~1-2h, 1 PR)

- `InstallForceEnabled` audit constant emission per A1.11 council fix
- Composition with W#40's `IFeatureForceEnableSurface` (operator-only role check; force-install requires justification text per A1.11)
- `InstallBlocked` + `InstallWarned` + `PostInstallSpecRegression` audit emission paths
- Per-event audit dedup per ADR 0028-A6.5.1 pattern (5-min `MinimumSpecEvaluated`; per-attempt `InstallBlocked` / `InstallWarned`; 24-hour `PostInstallSpecRegression`)
- ~5 tests covering force-install + audit emission

### Phase 4 — `ISystemRequirementsRenderer` interface (interface-only; impls deferred) (~1h, 1 PR)

- `ISystemRequirementsRenderer` + `ISystemRequirementsSurface` interfaces per A1.1
- `SystemRequirementsRenderMode` enum (PreInstallFullPage / PostInstallInlineExplanation / PostInstallRegressionBanner)
- **No concrete renderers** in W#41. Per-adapter Stage 06 hand-offs (W#42+ — Anchor MAUI Razor renderer; W#43+ — Bridge React renderer; W#44+ — iOS SwiftUI renderer) ship the actual UI implementations.
- 1 test verifying the interface contract is implementable + dependency-injectable

### Phase 5 — DI extension update + W#38 stub deprecation + apps/docs + ledger flip (~1-2h, 1 PR)

- Extend `AddSunfishMissionSpace()` DI extension (audit-disabled + audit-enabled overloads; both-or-neither at registration; mirrors W#34/W#35/W#36/W#39/W#40 P5 precedent) to register `IMinimumSpecResolver`
- W#38 stub deprecation: replace `Sunfish.Foundation.Catalog.Bundles.MinimumSpec` stub with using-alias to `Sunfish.Foundation.MissionSpace.MinimumSpec` per W#38 stub-unblock addendum future-rename plan; add 90-day removal TODO (or schedule removal PR for date X)
- `apps/docs/foundation-mission-space/requirements.md` walkthrough page (cite ADR 0063 + post-A1 surface explicitly + Steam System Requirements UX prior-art)
- Active-workstreams.md row 41 flipped from `building` → `built` with PR list
- Update active-workstreams row 38 to note the stub has been deprecated to canonical type

---

## Halt-conditions (cob-question if any of these surface)

1. **W#38 stub deprecation timing.** Phase 5 deprecates the stub via using-alias. If the deprecation breaks any existing test (the W#38 71/71 test count includes tests against the stub's specific shape), file `cob-question-*` beacon — the answer is likely "use a typedef-style alias rather than namespace-alias if the test count regresses."

2. **Cross-package dimension type availability.** Phase 1 consumes `FormFactorKind` from W#35 (built ✓), `SyncState` from W#37 (built ✓), `TransportTier` from W#30 (built ✓), edition-key strings from ADR 0009 (existing ✓), `MissionEnvelope` types from W#40 (built ✓). All prereqs landed. If a build error suggests otherwise, file `cob-question-*` beacon.

3. **Per-platform COMPOSE rule edge case.** Per A1.7 the rule is "for-each-dimension override-replaces-baseline." If a tester writes a test where override declares the SAME dimension as baseline with different values (intentional override), the COMPOSE rule should keep override's value (NOT merge them). Verify with the canonical example in A1.7: iOS override adds `BiometricAuth` requirement on top of baseline 16GB / 8-core; merged spec = baseline 16GB / 8-core + iOS BiometricAuth. If the rule's edge cases are unclear, file `cob-question-*` beacon.

4. **`SystemRequirementsRenderMode` consumer pattern.** Phase 4 ships the interface; concrete renderers are deferred. If COB feels the interface is awkward to consume (e.g., `ISystemRequirementsSurface.PlatformSurface: object` is too loose), file `cob-question-*` beacon — the answer may be "tighten with a generic constraint per-platform; see W#34 P1 PluginId precedent for the constraint pattern."

5. **Force-install audit shape parity with ADR 0062.** Per A1.11 + W#40's `IFeatureForceEnableSurface`: the InstallForceEnabled audit shape should match `FeatureForceEnabled` shape (operator_id + justification + override_target). If COB sees a divergence pattern, file `cob-question-*` beacon — symmetry with W#40's force-enable surface is canonical.

6. **CanonicalJson catch-all dictionary forward-compat verification.** Per A1.5 + A1.6 verification gate option (ii): a `Dictionary<string, JsonNode> _unknownFields` catch-all is required per the council. If `System.Text.Json` doesn't roundtrip the catch-all dictionary cleanly out-of-the-box, file `cob-question-*` beacon — the answer may be "use `JsonExtensionData` attribute on the catch-all field per System.Text.Json convention."

7. **W#41 cohort milestone.** This is the 8th substrate in the W#33-derived cohort (W#30 + W#34 + W#35 + W#36 + W#37 + W#38 + W#39-building + W#40 = 7 prior; W#41 = 8th). Per cohort-milestone discipline, this hand-off does NOT trigger pre-merge council (substrate-only Stage 06; not an ADR amendment). If COB is uncertain whether a cohort-milestone amendment should fire, file `cob-question-*` beacon.

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-01):**

- ADR 0063 + A1 (PR #411 merged post-A1) — substrate spec source ✓
- W#40 Foundation.MissionSpace (PR #466 merged) — provides MissionEnvelope + IFeatureGate + 10 dimensions + IFeatureForceEnableSurface ✓
- W#34 Foundation.Versioning (PR #423 built) — provides VersionVector ✓
- W#35 Foundation.Migration (PR #446 built) — provides FormFactorKind ✓
- W#37 Foundation.UI.SyncState (PR #448 built) — provides SyncState enum ✓
- W#30 Foundation.Transport (PR #437 built) — provides TransportTier enum ✓
- W#38 Foundation.Catalog stub MinimumSpec (PR #462 built) — to be deprecated by W#41 ✓
- ADR 0009 (Foundation.FeatureManagement; IEditionResolver) ✓
- ADR 0036 (sync-state encoding contract) ✓
- ADR 0049 (audit substrate) ✓
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` ✓

**Introduced by this hand-off** (ship in Phase 1):

- ~16 new types extending `Sunfish.Foundation.MissionSpace`: `MinimumSpec`, 10 per-dimension spec records, `PerPlatformSpec`, `IMinimumSpecResolver`, `SystemRequirementsResult`, `OverallVerdict` enum, etc.
- 5 new `AuditEventType` constants per A1.11 + A1.12
- `ISystemRequirementsRenderer` interface (concrete impls deferred)
- W#38 stub deprecation (using-alias to canonical type)

**Cohort lesson reminder (per ADR 0028-A10 + ADR 0063-A1.15):** §A0 self-audit pattern is necessary but NOT sufficient. COB should structurally verify each Sunfish.* symbol exists (read actual cited file's schema; don't grep alone) before declaring AP-21 clean.

---

## Cohort discipline

This hand-off is **not** a substrate ADR amendment; it's a Stage 06 hand-off implementing post-A1-fixed ADR 0063 surface. Pre-merge council on this hand-off is NOT required.

- COB's standard pre-build checklist applies
- W#34 + W#35 + W#36 + W#39 + W#40 cohort lessons incorporated: ConcurrentDictionary dedup; two-overload constructor both-or-neither; JsonStringEnumConverter for all enum types; AddInMemoryX() DI extension naming; apps/docs/{tier}/X/overview.md page convention

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w41-{slug}.md` in `icm/_state/research-inbox/`
- Halt the workstream + add a note in active-workstreams.md row 41
- ScheduleWakeup 1800s

If COB completes Phase 5 + drops to fallback:

- Drop `cob-idle-2026-05-XXTHH-MMZ-{slug}.md` to research-inbox
- Continue with rung-1 dependabot + rung-2 build-hygiene per CLAUDE.md fallback

---

## Cross-references

- Spec source: ADR 0063 + A1 (PR #411 merged post-A1)
- Council that drove A1: PR #413 (merged); council file at `icm/07_review/output/adr-audits/0063-council-review-2026-04-30.md`
- Sibling substrate Stage 06 hand-offs in flight / queued / built:
  - W#23 ready (iOS Field-Capture)
  - W#30 built (Mesh VPN / Transport)
  - W#34 built (Foundation.Versioning)
  - W#35 built (Foundation.Migration)
  - W#36 built (Bridge subscription emitter)
  - W#37 built (Foundation.UI.SyncState)
  - W#38 built (BusinessCaseBundleManifest.Requirements field with stub)
  - W#39 building (P1 shipped via PR #467; Foundation.MissionSpace.Regulatory)
  - W#40 built (Foundation.MissionSpace canonical substrate)
  - W#41 (this hand-off) — extends W#40
- W#33 §7.2 follow-on queue: closed (5/5 substrate ADRs landed; cohort in Stage 06 build phase)
- ADR 0062-A1.6 halt-condition closed by W#36 Bridge subscription event emitter (PR #458 built)
- W#38 stub deprecation: per stub-unblock addendum future-rename plan (90-day grace; ships in Phase 5)
- W#42+ per-adapter renderer hand-offs (Anchor MAUI / Bridge React / iOS): downstream consumers of W#41 substrate; not yet authored

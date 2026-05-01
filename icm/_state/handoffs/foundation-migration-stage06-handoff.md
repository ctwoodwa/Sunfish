# Hand-off — Foundation.Migration substrate Phase 1 (ADR 0028-A5+A8 contract surface)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-01
**Status:** `ready-to-build`
**Spec source:** [ADR 0028 amendments A5 + A8](../../docs/adrs/0028-crdt-engine-selection.md) (post-A8 council-fixed surface; landed via PR #402; council in PR #403)
**Approval:** ADR 0028-A5+A8 Accepted on origin/main; A8 absorbed all 10 council recommendations
**Estimated cost:** ~12–16h sunfish-PM (foundation-tier package scaffold + ~12 type signatures + migration table + 6 audit constants + ~30–40 tests + DI + apps/docs page); same shape as W#34 Foundation.Versioning which shipped 59/59 tests in ~5 PRs
**Pipeline:** `sunfish-feature-change`
**Audit before build:** `ls /Users/christopherwood/Projects/Sunfish/packages/ | grep -E "^foundation-migration"` to confirm no collision (audit not yet run; COB confirms before Phase 1 commit)

---

## Context

Phase 1 lands the Foundation.Migration substrate's core types + sequestration logic + audit emission per the post-A8 ADR 0028-A5 surface. Subsequent phases ship:

- **W#23 / W#28 / form-factor consumers** (separate workstreams) — wire the migration semantics into actual capture-flow / sync-boundary code paths
- **A1.x companion (iOS envelope capture-context tagging)** — separate intake at PR #397; will land as its own W# when CO promotes
- **W#34 Foundation.Versioning consumer composition** — A5 cites A6's compatibility relation as input; substrate composition is post-W#34/W#35 build

This hand-off scope is **substrate types + Invariant DLF (data-loss-vs-feature-loss) sequestration logic + reference implementation + audit emission**. Substrate-only; no consumers wired in this hand-off. Concrete enough to unblock:

- W#23 iOS Field-Capture App's cross-form-factor migration path
- W#28 Public Listings cross-form-factor scenarios
- ADR 0028-A6.11 iOS A1 envelope augmentation (per A7.5; coordinated A1.x amendment)

---

## Files to create

### Package scaffold

```
packages/foundation-migration/
├── Sunfish.Foundation.Migration.csproj
├── README.md
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs        (AddInMemoryMigration; mirrors W#34 P5 shape)
├── Models/
│   ├── FormFactorProfile.cs                  (record; post-A8.4 expanded migration table types)
│   ├── HardwareTierChangeEvent.cs            (record; per A5.3)
│   ├── FormFactorKind.cs                     (enum: Laptop, Desktop, Tablet, Phone, Watch, Headless, Iot, Vehicle)
│   ├── InputModalityKind.cs                  (enum: Pointer, Keyboard, Touch, Voice, Pen, GestureSensor, None)
│   ├── DisplayClassKind.cs                   (enum: Large, Medium, Small, MicroDisplay, NoDisplay)
│   ├── NetworkPostureKind.cs                 (enum: AlwaysConnected, IntermittentConnected, OfflineFirst, AirGapped)
│   ├── PowerProfileKind.cs                   (enum: Wallpower, Battery, LowPower, IntermittentBattery)
│   ├── SensorKind.cs                         (enum: Camera, Mic, Gps, Accelerometer, BiometricAuth, NfcReader, BarcodeScanner)
│   ├── TriggeringEventKind.cs                (enum: StorageBudgetChanged, NetworkPostureChanged, SensorPermissionChanged, PowerProfileChanged, AdapterUpgrade, AdapterDowngrade, ManualReprofile)
│   ├── SequestrationFlagKind.cs              (enum: FormFactorFilteredOut, StorageBudgetExceeded, PlaintextSequestered, CiphertextSequestered, FormFactorQuorumIneligible per A8.3)
│   └── DerivedSurface.cs                     (record; computed per A5.1 filter)
├── Services/
│   ├── IFormFactorMigrationService.cs        (per A5.8 acceptance contract)
│   ├── InMemoryFormFactorMigrationService.cs (reference impl; thread-safe; in-process)
│   ├── ISequestrationStore.cs                (sequestration partition contract)
│   └── InMemorySequestrationStore.cs         (reference impl)
├── Audit/
│   └── MigrationAuditPayloads.cs             (factory; mirrors VersionVectorAuditPayloads pattern from W#34)
├── Encoding/
│   └── FormFactorProfileCanonicalEncoding.cs (camelCase round-trip per A7.8 / A8.4 example)
└── tests/
    └── Sunfish.Foundation.Migration.Tests.csproj
        ├── FormFactorProfileTests.cs          (encoding round-trip; canonical-JSON shape)
        ├── HardwareTierChangeEventTests.cs    (per A5.3)
        ├── DerivedSurfaceTests.cs             (per A5.1 filter; intersection logic)
        ├── MigrationTableTests.cs             (8 form-factor combos + post-A8.4 Phone↔Watch + CarPlay/Android-Auto)
        ├── InvariantDlfTests.cs               (sequestration over deletion; cross-peer-rescue; release on surface expansion)
        ├── PlaintextVsCiphertextSequestrationTests.cs  (per A8.3 rule 5)
        ├── CpRecordQuorumIneligibleTests.cs    (per A8.3 rule 6)
        ├── FieldLevelRedactionTests.cs        (per A8.3 rule 7 — placeholder for un-decryptable fields)
        ├── FieldLevelWriteAuthorizationTests.cs  (per A8.5 Rule 6 — write-sequestered if read-sequestered)
        ├── ForwardCompatRoundTripTests.cs     (per A8.6 — option (ii) Dictionary<string, JsonNode> catch-all)
        ├── RollbackSemanticsTests.cs          (per A5.6 — adapter version downgrade; 6-hour AdapterRollbackDetected dedup per A8.7)
        ├── KeyTransferTests.cs                (per A5.7 — QR-onboarding shape; cite ~ADR-0032-A1 halt-condition)
        ├── AuditEmissionTests.cs              (6 AuditEventType constants emit on right triggers + dedup)
        └── DiExtensionTests.cs                (audit-disabled / audit-enabled overloads; both-or-neither at registration boundary)
```

### Type definitions (post-A8 surface; implement exactly)

```csharp
namespace Sunfish.Foundation.Migration;

public sealed record FormFactorProfile(
    FormFactorKind                  FormFactor,
    IReadOnlySet<InputModalityKind> InputModalities,
    DisplayClassKind                DisplayClass,
    NetworkPostureKind              NetworkPosture,
    uint                            StorageBudgetMb,
    PowerProfileKind                PowerProfile,
    IReadOnlySet<SensorKind>        SensorSurface,
    InstanceClassKind               InstanceClass    // matches A6.1's reduced enum per A7.6
);

public sealed record HardwareTierChangeEvent(
    string                NodeId,
    FormFactorProfile     PreviousProfile,
    FormFactorProfile     CurrentProfile,
    TriggeringEventKind   TriggeringEvent,
    DateTimeOffset        DetectedAt
);

public interface IFormFactorMigrationService
{
    /// <summary>
    /// Recomputes the derived surface from the current profile + workspace's declared capabilities.
    /// </summary>
    ValueTask<DerivedSurface> ComputeDerivedSurfaceAsync(
        FormFactorProfile profile,
        IReadOnlySet<string> workspaceDeclaredCapabilities,
        CancellationToken ct = default
    );

    /// <summary>
    /// Applies the sequestration/release transitions per A5.2 + A5.4 + A8.3 (post-A8 rules 5/6/7).
    /// </summary>
    ValueTask ApplyMigrationAsync(
        HardwareTierChangeEvent change,
        CancellationToken ct = default
    );

    /// <summary>
    /// Per A5.7 — handles QR-onboarding key transfer.
    /// HALT-CONDITION: cannot ship until ~ADR-0032-A1 (QR-onboarding protocol formalization)
    /// is Accepted on origin/main per ADR 0028-A8.2.
    /// </summary>
    ValueTask<FormFactorEnrollmentResult> EnrollAsync(
        FormFactorProfile newProfile,
        QrOnboardingPayload payload,
        CancellationToken ct = default
    );
}

// Per A8.6 forward-compat option (ii) — catch-all dictionary for unknown fields
public abstract record CatchAllRecord
{
    public Dictionary<string, JsonNode>? UnknownFields { get; init; }
}
```

### Audit constants

`AuditEventType` MUST gain 6 new constants in `packages/kernel-audit/AuditEventType.cs` (per A8.3 + A8.5):

```csharp
public static readonly AuditEventType HardwareTierChanged                = new("HardwareTierChanged");
public static readonly AuditEventType PlaintextSequestered               = new("PlaintextSequestered");
public static readonly AuditEventType CiphertextSequestered              = new("CiphertextSequestered");
public static readonly AuditEventType DataReleased                       = new("DataReleased");
public static readonly AuditEventType FormFactorQuorumIneligible         = new("FormFactorQuorumIneligible");
public static readonly AuditEventType FieldWriteSequestered              = new("FieldWriteSequestered");
public static readonly AuditEventType AdapterRollbackDetected            = new("AdapterRollbackDetected");
public static readonly AuditEventType FormFactorProvisioned              = new("FormFactorProvisioned");
public static readonly AuditEventType FormFactorEnrollmentCompleted      = new("FormFactorEnrollmentCompleted");
public static readonly AuditEventType LegacyEpochEvent                   = new("LegacyEpochEvent");      // per A7.5.3
```

(Total: 10 new constants. The original A5.8 listed 6; A8.3 + A8.5 augmented to 10. The W#34 cohort lesson noted shipping audit constants in batch is correct.)

`MigrationAuditPayloads` factory (alphabetized keys; canonical-JSON-serialized; per ADR 0049 emission contract).

---

## Phase breakdown (~5 PRs, ~12–16h total — same shape as W#34)

### Phase 1 — Substrate scaffold + core types (~2–3h, 1 PR)

- Package created at `packages/foundation-migration/` with foundation-tier csproj
- All Models per the spec block above
- `FormFactorProfile` round-trip via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` test (camelCase shape per A7.8 / A8.4)
- `JsonStringEnumConverter` for all 7 enum types (parallel to W#34 P1 pattern)
- README.md per the standard package-README pattern
- ~6–10 unit tests on Models alone

### Phase 2 — Migration table + DerivedSurface filter (~2–3h, 1 PR)

- `DerivedSurface` record + `IFormFactorMigrationService.ComputeDerivedSurfaceAsync`
- 8 form-factor migration table tests + post-A8.4 Phone↔Watch + CarPlay/Android-Auto + built-in IVI
- Filter intersection logic: `formFactor.capabilities ∩ workspace.declaredCapabilities`

### Phase 3 — Invariant DLF + sequestration logic (~3–4h, 1 PR)

- `ISequestrationStore` + `InMemorySequestrationStore` (sequestration partition)
- `IFormFactorMigrationService.ApplyMigrationAsync` per A5.4 4 rules + A8.3 3 rules (rules 5/6/7)
- Invariant DLF tests covering all 7 rules
- Plaintext-vs-ciphertext distinction (A8.3 rule 5)
- CP-record quorum participation (A8.3 rule 6)
- Field-level redaction default (A8.3 rule 7)
- Field-level write authorization mirroring read authorization (A8.5 Rule 6)

### Phase 4 — Audit emission + dedup wiring (~2–3h, 1 PR)

- 10 new `AuditEventType` constants in `packages/kernel-audit/AuditEventType.cs`
- `MigrationAuditPayloads` factory (alphabetized keys per ADR 0049 convention)
- 6-hour `AdapterRollbackDetected` dedup per A8.7 (`(node_id, adapter_id, version_pair)` window)
- Other A5/A8 events: standard audit-substrate behavior
- Two-overload constructor pattern (audit-disabled / audit-enabled both-or-neither) per W#32 + W#34 precedent

### Phase 5 — DI extension + apps/docs + ledger flip (~1–2h, 1 PR)

- `AddInMemoryMigration()` DI extension (audit-disabled + audit-enabled overloads; both-or-neither at registration; mirrors W#34 P5)
- `apps/docs/foundation/migration/overview.md` walkthrough page (cite ADR 0028 + post-A8 surface explicitly)
- Active-workstreams.md row 35 flipped from `building` → `built` with PR list

---

## Halt-conditions (cob-question if any of these surface)

1. **A5.7 QR-onboarding protocol formalization is NOT YET ratified.** Per A8.2 halt-condition: ADR 0028-A5 Stage 06 build cannot ship `IFormFactorMigrationService.EnrollAsync` (the QR-onboarding-handshake-consuming method) until ~ADR-0032-A1 is Accepted on origin/main. Phase 3 substrate MAY ship `EnrollAsync` interface stub that throws `NotSupportedException("Awaiting ~ADR-0032-A1 ratification")` until ~ADR-0032-A1 lands; OR halt the EnrollAsync implementation entirely and ship the rest. **Recommend stub-with-throw** so DI works + downstream substrate-consumer code can compile against the interface.

2. **A8.6 forward-compat verification gate.** Phase 1 substrate MUST verify CanonicalJson.Serialize unknown-field-tolerance per A8.6 option (ii) `Dictionary<string, JsonNode> _unknownFields` catch-all pattern. If the test reveals unknown-field-tolerance does NOT round-trip cleanly with the catch-all dictionary pattern, file a `cob-question-*` beacon — A8.6 may need adjustment OR Phase 1 ships the option (i) `JsonNode`-typed intermediate fallback.

3. **A1.x companion amendment surface.** ADR 0028's iOS A1 envelope augmentation (per A6.11 + A7.5) is a separate intake at PR #397; A1.x has NOT yet been authored as an ADR amendment. The Foundation.Migration substrate Phase 1 does NOT need to handle iOS-specific envelope shape (that's W#23 territory + A1.x amendment territory). Specifically: `LegacyEpochEvent` audit constant ships in this hand-off (per A7.5.3 declaration), but iOS-specific cross-epoch sequestration LOGIC stays in W#23 + A1.x territory.

4. **`FormFactorKind` enum forward-compat.** Per A8.4 the enum reduces to 8 values: `{ Laptop, Desktop, Tablet, Phone, Watch, Headless, Iot, Vehicle }`. The verification test (encode FormFactorProfile with hypothetical-future enum value via `CanonicalJson.Serialize`; deserialize on a default `JsonStringEnumConverter` consumer; observe behavior) needs to ship in Phase 1 tests. Same pattern as W#34 P1's instanceClass enum-bump test.

5. **`InstanceClassKind` consistency with W#34.** Both W#34 (Foundation.Versioning) and W#35 (Foundation.Migration) ship `InstanceClassKind` enum. They MUST be the same type — either consumed from W#34's package OR redeclared identically. Recommend: Phase 1 references `Sunfish.Foundation.Versioning.InstanceClassKind` directly (foundation-versioning is now on origin/main as of W#34 P5 — PR #423). If the cross-package dependency is undesirable, file a `cob-question-*` beacon and we'll move the enum to a shared `Sunfish.Foundation.Common` or similar.

6. **Audit dedup cache contention.** Per W#34 P4 lesson: `ConcurrentDictionary` keyed on the dedup tuple is the canonical pattern. Mirror exactly. If contention surfaces under load, file a `cob-question-*` beacon.

7. **CP-record quorum logic dependency.** Per A8.3 rule 6: `FormFactorQuorumIneligible` requires the substrate to know which records are CP-class. The CP/AP record-class metadata is part of the per-record-type contract (paper §15-style); Phase 1 substrate MAY ship a placeholder `IRecordClassResolver` that always returns AP (unknown CP records); a future amendment wires actual CP/AP detection. Document the placeholder; do NOT implement CP-detection logic in this hand-off.

---

## Cited-symbol verification (per cohort lesson)

**Existing on origin/main (verified before hand-off authored):**

- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` — encoding contract ✓
- `Sunfish.Kernel.Audit.AuditEventType` — audit substrate per ADR 0049 ✓
- `Sunfish.Foundation.Versioning.InstanceClassKind` — exposed by W#34 P1 (PR #417) ✓
- ADR 0028 itself (CRDT engine selection) — Accepted; A1+A2+A3+A4+A5+A6+A7+A8 all on origin/main as of 2026-04-30
- ADR 0049 (audit substrate) — Accepted

**Introduced by this hand-off** (ship in Phase 1):

- `Sunfish.Foundation.Migration.FormFactorProfile`
- `Sunfish.Foundation.Migration.HardwareTierChangeEvent`
- `Sunfish.Foundation.Migration.IFormFactorMigrationService` + `InMemoryFormFactorMigrationService`
- `Sunfish.Foundation.Migration.ISequestrationStore` + `InMemorySequestrationStore`
- `Sunfish.Foundation.Migration.DerivedSurface`
- 7 enum types: `FormFactorKind` / `InputModalityKind` / `DisplayClassKind` / `NetworkPostureKind` / `PowerProfileKind` / `SensorKind` / `TriggeringEventKind`
- `Sunfish.Foundation.Migration.SequestrationFlagKind` (5 values per A8.3)
- `Sunfish.Foundation.Migration.Audit.MigrationAuditPayloads` factory
- 10 new `AuditEventType` constants (per A8.3 + A8.5 + A7.5.3)

**Companion amendment dependencies declared:**

- ~ADR-0032-A1 (QR-onboarding protocol formalization) — halt-condition for `EnrollAsync` per A8.2; Phase 1 ships interface stub that throws `NotSupportedException`.
- A1.x iOS envelope capture-context tagging (PR #397 intake) — out of scope; W#23 territory.

**Cohort lesson reminder:** §A0 self-audit pattern is necessary but NOT sufficient (per ADR 0063-A1.15). COB should structurally verify each Sunfish.* symbol exists (read actual cited file's schema; don't grep alone) before declaring AP-21 clean per Decision Discipline Rule 6.

---

## Cohort discipline

This hand-off is **not** a substrate ADR amendment; it's a Stage 06 hand-off implementing post-A8-fixed surface. The cohort discipline applies to ADR amendments, not to this hand-off.

- Pre-merge council on this hand-off is NOT required.
- COB's standard pre-build checklist applies: verify ledger row says `ready-to-build` (this row will after hand-off lands); verify hand-off file describes what to build file-by-file (it does); verify no in-flight PRs overlap; verify but status + git log -all show no parallel-session work.
- **W#34 cohort lesson incorporated:** ConcurrentDictionary dedup pattern; two-overload constructor (audit-disabled / audit-enabled both-or-neither); JsonStringEnumConverter for all enum types; AddInMemoryX() DI extension naming; apps/docs/foundation/X/overview.md page convention. Mirror these exactly.

---

## Beacon protocol

If COB hits a halt-condition (per the 7 named above) or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w35-{slug}.md` in `icm/_state/research-inbox/`
- Halt the workstream + add a note in active-workstreams.md row 35 ("paused on cob-question-XXX")
- ScheduleWakeup 1800s

If COB completes Phase 5 + drops to fallback:

- Drop `cob-idle-2026-05-XXTHH-MMZ-{slug}.md` to research-inbox
- Continue with rung-1 dependabot + rung-2 build-hygiene per CLAUDE.md fallback work order

---

## Cross-references

- Spec source: ADR 0028-A5+A8 (post-A8 surface; ADR 0028's "Amendments" section A5.* + A8.*)
- Council that drove A8: PR #403 (merged 2026-04-30); council-review file at `icm/07_review/output/adr-audits/0028-A5-council-review-2026-04-30.md`
- Sibling workstream just-shipped: W#34 Foundation.Versioning (PRs #417/#418/#420/#421/#423; ledger row 34 `built`) — implementation patterns + test patterns are the canonical reference
- Companion intake (deferred): A1.x iOS envelope capture-context tagging (PR #397; coordinated A1 amendment to ADR 0028 not yet authored)
- Companion intake (halt-condition): ~ADR-0032-A1 QR-onboarding protocol formalization (not yet filed as intake; XO follow-up may file when A5.7 EnrollAsync becomes blocking)
- W#33 follow-on queue (closed): `project_workstream_33_followon_authoring_queue.md` (memory)

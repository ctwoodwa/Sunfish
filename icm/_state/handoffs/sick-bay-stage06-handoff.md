# W#54 Stage 06 Hand-off — Sick Bay Aggregation Surface + IDC Role

**Workstream:** W#54 — Sick Bay Aggregation Surface + IDC Role
**ADR:** [ADR 0082](../../../docs/adrs/0082-sick-bay-aggregation-surface.md) — Proposed 2026-05-05 via PR #589
**Owner (implementation):** sunfish-PM (COB)
**Status:** `ready-to-build` (pending CO Status: Accepted on ADR 0082; ADR-Status flip is the formal gate)
**Effort estimate:** ~14–20h / 5 phases / ~5 PRs
**Pipeline variant:** `sunfish-feature-change`
**W#35 cohort position:** follow-on #6 of 7 (siblings: W#46/49/50/51/52 hand-offs already on origin/main; W#55 hand-off paired with this one)

---

## Hard prerequisites

| # | Prereq | Verify on origin/main | Phase gated |
|---|---|---|---|
| **H1** | W#46 Phase 1 merged — `foundation-ship-common` package + `ShipRole` enum + `IPermissionResolver` + `ShipAction` catalog | `ls packages/foundation-ship-common/Sunfish.Foundation.Ship.Common.csproj 2>/dev/null && grep -l "enum ShipRole" packages/foundation-ship-common/` must be non-empty | Phase 1 (`ShipRole.IDC` enum addition + 6 `ShipAction` constants) |
| **H2** | W#53 Phase 1 merged — `KeyFingerprint` in `packages/foundation/Crypto/` (**not** `foundation-recovery` — cycle-prevention; see PR #633) | `find packages/foundation/Crypto -name "KeyFingerprint*"` must return ≥1 file | Phase 3a only (`KeyFingerprintDisplay.razor`); Phase 1/2 are NOT gated |
| **H3** | ADR 0068 Status: Accepted (Tenant Security Policy / W#37) — typed `KeyRotationTrigger` available | `grep -l "Status: Accepted" docs/adrs/0068-tenant-security-policy.md` (PR #584 merged 2026-05-05) | Phase 2 only — `PharmacyInventoryEntry.PendingTriggerLabel` upgrade from `string?` to typed `KeyRotationTrigger?`. Phase 1 keeps the `string` shape per ADR 0082 §1 "Phase 2 addition" comment. |
| **H4** | Security council pre-Phase-2 verification — `IFieldDecryptor` MUST NOT appear in any `ISickBayDataProvider` implementation | Reflection test in Phase 2: `[Fact] SickBayDataProvider_DoesNotReference_IFieldDecryptor()` | Phase 2 (mandatory pre-merge) |

**NuGet binary halt:** This project has NOT shipped a NuGet binary (pre-v1). No binary-compat halt applies. Confirm before every Phase 1 PR: `find packages/ -name "*.nupkg" | wc -l` must be 0.

**Downstream consumers (informational):**
- `IFirstAidSurface` is consumed by sibling blocks (Engine Room / Quarterdeck / Tactical). Each registers its surface key (`"engine-room"`, `"quarterdeck"`, `"tactical"`) and pulls hints via DI. Phase 2 hint library covers `"pharmacy"`, `"lab"`, `"atmosphere"` only; sibling-block hint coverage is a follow-up for those teams.
- W#37 ADR 0068 follow-up: when `KeyRotationTrigger` ships, raise the Phase 2 amendment to swap the string shape.

---

## Substrate verification (pre-Phase-1)

Run before writing a single line of Phase 1 code, in the worktree off origin/main:

```bash
# 1. Net-new package — must NOT pre-exist
ls packages/foundation-sick-bay/ 2>/dev/null && echo "PRE-EXISTS — halt; coordinate with parallel session" || echo "OK — net-new"
ls packages/blocks-sick-bay/ 2>/dev/null     && echo "PRE-EXISTS — halt" || echo "OK — net-new"

# 2. Substrate symbols (must all be PRESENT on origin/main):
grep -l "EncryptedField"             packages/foundation-recovery/EncryptedField.cs            # ADR 0046-A2
grep -l "IFieldDecryptor"            packages/foundation-recovery/Crypto/IFieldDecryptor.cs    # ADR 0046-A2
grep -l "IFieldEncryptor"            packages/foundation-recovery/Crypto/IFieldEncryptor.cs    # ADR 0046-A2
grep -l "IDimensionProbe"            packages/foundation-mission-space/Services/Contracts.cs   # ADR 0062
grep -l "IMissionEnvelopeProvider"   packages/foundation-mission-space/Services/Contracts.cs   # ADR 0062
grep -l "IMissionEnvelopeObserver"   packages/foundation-mission-space/Services/Contracts.cs   # ADR 0062
grep -l "MissionEnvelope"            packages/foundation-mission-space/Models/MissionEnvelope.cs  # ADR 0062
grep -l "DegradationKind\|ProbeStatus" packages/foundation-mission-space/Models/Enums.cs       # ADR 0062
grep -l "TenantId"                   packages/foundation/Assets/Common/TenantId.cs             # foundation
grep -l "ActorId"                    packages/foundation/Assets/Common/ActorId.cs              # foundation
grep -l "PrincipalId"                packages/foundation/Crypto/PrincipalId.cs                 # ADR 0046

# 3. Audit substrate (must be PRESENT)
grep "AuditEventType " packages/kernel-audit/AuditEventType.cs | head -3   # AuditEventType ledger pattern
grep -l "IAuditTrail"  packages/kernel-audit/IAuditTrail.cs

# 4. H1 / H2 gate state — note absence is EXPECTED at hand-off authoring time
ls packages/foundation-ship-common/ 2>/dev/null && echo "H1 cleared" || echo "H1 BLOCKED — wait for W#46 P1 merge"
find packages/foundation/Crypto -name "KeyFingerprint*" 2>/dev/null | head -1 \
  && echo "H2 cleared" || echo "H2 BLOCKED — wait for W#53 P1 merge (Phase 3a only)"

# 5. AuditEventType collision sweep (all 11 names MUST be absent)
for n in SickBayPharmacyViewed SickBayKeyRotationTriggered SickBayLabDiagnosticViewed \
         SickBayAtmosphereViewed SickBayMedevacInitiated SickBayMedevacAuthorized \
         SickBayMedevacCancelled SickBayMedevacCompleted SickBayMedevacSelfApprovalRejected \
         SickBayRecoveryContactManaged ; do
  grep -q "$n" packages/kernel-audit/ && echo "COLLISION: $n" || true
done

# 6. ShipAction collision sweep (all 6 names MUST be absent — assumes H1 cleared)
for n in ViewSickBay ViewPharmacy ManageRecoveryContacts TriggerKeyRotation \
         InitiateMedevac AuthorizeMedevac ViewFirstAid ; do
  grep -rq "$n" packages/foundation-ship-common/ 2>/dev/null && echo "COLLISION: $n" || true
done
```

If any check fails unexpectedly, stop and write a `cob-question-*.md` to `icm/_state/research-inbox/`.

---

## Phase 1 — `foundation-sick-bay` substrate (contracts + data model)

**Effort:** ~4–5h | **PR:** 1 | **Review:** pre-merge council mandatory (standard 4-perspective adversarial)

**Gate:** H1 (W#46 Phase 1 on origin/main) — required for `ShipRole.IDC` enum addition + `ShipAction` constants.
**Halt:** if H1 not cleared at start, COB has two options: (a) defer Phase 1 until W#46 P1 lands, OR (b) ship Phase 1 in two slices — slice-A = data model + interfaces (no `ShipRole`/`ShipAction` references), slice-B = `ShipRole.IDC` + `ShipAction` constants once H1 clears. Document the choice in the PR description and add a halt-condition note to `icm/_state/research-inbox/cob-question-*.md` if slice-B is deferred.

### 1.1 Project file

`packages/foundation-sick-bay/Sunfish.Foundation.SickBay.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Sunfish.Foundation.SickBay</RootNamespace>
    <AssemblyName>Sunfish.Foundation.SickBay</AssemblyName>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NodaTime" Version="$(NodaTimeVersion)" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="$(MicrosoftExtensionsOptionsVersion)" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions"
                      Version="$(MicrosoftExtensionsDependencyInjectionAbstractionsVersion)" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\kernel-audit\Sunfish.Kernel.Audit.csproj" />
    <ProjectReference Include="..\foundation-recovery\Sunfish.Foundation.Recovery.csproj" />
    <ProjectReference Include="..\foundation-mission-space\Sunfish.Foundation.MissionSpace.csproj" />
    <!-- foundation-ship-common added in slice-B once H1 clears -->
    <ProjectReference Include="..\foundation-ship-common\Sunfish.Foundation.Ship.Common.csproj" />
    <!-- foundation-wayfinder added if Phase 2 wires IStandingOrderEventStream early -->
  </ItemGroup>
</Project>
```

**Architecture rule check:** `foundation → foundation-recovery + foundation-mission-space + foundation-ship-common` is allowed. NEVER add `Sunfish.UICore` or any `blocks-*` reference here — Phase 3a adds those in `blocks-sick-bay/` only.

### 1.2 Data model types (per ADR 0082 §1)

Create the following files in `packages/foundation-sick-bay/`. All in namespace `Sunfish.Foundation.SickBay`. Cite ADR 0082 §1 in the file headers; record-types use `required` keyword consistently.

- **`PharmacyInventoryEntry.cs`** — record with `FieldPurpose`, `FriendlyName`, `RecordCount` (`PharmacyRecordCount`), `LastRotatedAt` (NodaTime.Instant), `RotationStatus` (`RotationHealth`), `HasCompromiseFlag`. Note: `PendingTriggerLabel` is Phase 2 (gated on H3); ship Phase 1 WITHOUT this field.
- **`PharmacyRecordCount.cs`** — sealed record with `static readonly Suppressed`, `static Exact(int)` factory enforcing k=3 floor, `Value` (int?), `IsSuppressed` (bool). Constructor private.
- **`RotationHealth.cs`** — enum: `Current, RotationDue, RotationOverdue, Compromised`.
- **`LabDiagnosticResult.cs`** — record with `ProbeName`, `DimensionId` (kebab-case), `Status` (`ProbeStatus` from `Sunfish.Foundation.MissionSpace`), `Degradation` (`DegradationKind` from `Sunfish.Foundation.MissionSpace`), `LastRunAt`, `DiagnosticDetail` (string? plain text).
- **`AtmosphereReadout.cs`** — record with `OverallHealth` (`AtmosphereHealth`), `WarningProbeCount`, `CriticalProbeCount`, `ForceEnableActive`, `CapturedAt`.
- **`AtmosphereHealth.cs`** — enum: `Unknown, Green, Yellow, Orange, Red`. **ADR 0082-A1**: `Unknown` is zero-value (numeric 0); stubs return `Unknown` until Phase 2b wires `IMissionEnvelopeProvider`.
- **`MedevacState.cs`** — enum: `Idle, Requested, PendingAuthorization, Authorized, InProgress, Complete` (per state-transition table in ADR 0082 §2).
- **`SickBaySnapshot.cs`** — record aggregating `IReadOnlyList<PharmacyInventoryEntry> Pharmacy`, `IReadOnlyList<LabDiagnosticResult> Lab`, `AtmosphereReadout Atmosphere`, `MedevacState MedevacState`, `CapturedAt`.
- **`FirstAidHint.cs`** — record with `Key`, `Title`, `Body` (plain-text-validated; constructor REJECTS strings containing `<`, `>`, `&`, or ASCII control chars except `\n`), `Level` (`FirstAidLevel`).
- **`FirstAidLevel.cs`** — enum: `Info, Caution, Warning`.
- **`StretcherBearerRole.cs`** — enum: `DCA, MPA, CommsOfficer, SonarOfficer` (NOT `ShipRole` — constrained subset; deliberate to prevent role-escalation).
- **`SickBayOptions.cs`** — sealed class with `RegisteredFieldPurposes` (IDictionary<string,string> case-insensitive), `RegisterPurpose(string, string)` builder, `FallbackPollingInterval` (TimeSpan; default 60s).

### 1.3 Provider, command, and contextual interfaces (per ADR 0082 §2 + §3 + §4)

- **`ISickBayDataProvider.cs`** — `Task<SickBaySnapshot> GetSnapshotAsync(TenantId, CancellationToken)` + `IAsyncEnumerable<SickBaySnapshot> SubscribeSnapshotAsync(TenantId, CancellationToken)`. XML doc MUST cite ADR 0046-A2 §4 forbidding `IFieldDecryptor`; recommended completion ≤2s; partial-snapshot-on-timeout posture.
- **`ISickBayCommandService.cs`** — `Task TriggerKeyRotationAsync(TenantId, string fieldPurpose, string triggerReason, CancellationToken)`. XML doc cites pre-op `SickBayKeyRotationTriggered` audit + `ShipAction.TriggerKeyRotation` permission requirement. Phase 2 amendment swaps `string triggerReason` to typed `KeyRotationTrigger` (gated on H3).
- **`IMedevacService.cs`** — full interface per ADR 0082 §2 state-transition table:
  - `Task<MedevacState> GetStateAsync(TenantId, CancellationToken)`
  - `Task RequestAsync(TenantId, PrincipalId requestedBy, string reason, CancellationToken)`
  - `Task AuthorizeAsync(TenantId, PrincipalId authorizingPrincipal, CancellationToken)` — XML doc MUST document four-eyes invariant + `SickBayMedevacSelfApprovalRejected` emission on self-approval + `InvalidOperationException` throw.
  - `Task CancelAsync(TenantId, PrincipalId cancellingPrincipal, CancellationToken)`
  - `Task CompleteAsync(TenantId, CancellationToken)`
  - Interface XML doc lists the valid-transition table verbatim (Idle→Requested, Requested→PendingAuthorization, etc. per §2). Invalid transitions throw `InvalidOperationException` with attempted-transition message.
- **`IFirstAidSurface.cs`** — `Task<IReadOnlyList<FirstAidHint>> GetContextualHintsAsync(string surfaceKey, CancellationToken)`. XML doc: kebab-case keys; unknown keys return empty list (NOT throw).
- **`IStretcherBearerPolicy.cs`** — `Task<IReadOnlyList<StretcherBearerRole>> GetEligibleRespondersAsync(TenantId, CancellationToken)`. XML doc explicitly states this list MUST NOT be consumed for permission/authority decisions — only notification routing/display.
- **`IKeyRotationScheduler.cs`** — net-new contract introduced by THIS ADR. `Task ScheduleAsync(TenantId, string fieldPurpose, string triggerReason, CancellationToken)`. XML doc: abstraction layer between `ISickBayCommandService` and the W#32 / ADR 0046-A2 rotation substrate; Phase 2 wires real implementation; Phase 1 stub returns `Task.CompletedTask`.

All `using Sunfish.Foundation.Assets.Common;` for `TenantId`/`ActorId`. All `using Sunfish.Foundation.Crypto;` for `PrincipalId`. All `using Sunfish.Foundation.MissionSpace;` for `ProbeStatus`/`DegradationKind`.

### 1.4 AuditEventType constants (per ADR 0082 §6)

In `packages/kernel-audit/AuditEventType.cs`, add the following 11 constants in a `// ── Sick Bay (ADR 0082) ──────────────` block. **Pre-add grep** must return zero hits per pre-flight collision sweep:

```csharp
// ── Sick Bay (ADR 0082) ──────────────────────────────────────────────────────
public static readonly AuditEventType SickBayPharmacyViewed              = new("sick-bay.pharmacy.viewed");
public static readonly AuditEventType SickBayKeyRotationTriggered        = new("sick-bay.key-rotation.triggered");
public static readonly AuditEventType SickBayLabDiagnosticViewed         = new("sick-bay.lab.viewed");
public static readonly AuditEventType SickBayAtmosphereViewed            = new("sick-bay.atmosphere.viewed");
public static readonly AuditEventType SickBayMedevacInitiated            = new("sick-bay.medevac.initiated");
public static readonly AuditEventType SickBayMedevacAuthorized           = new("sick-bay.medevac.authorized");
public static readonly AuditEventType SickBayMedevacCancelled            = new("sick-bay.medevac.cancelled");
public static readonly AuditEventType SickBayMedevacCompleted            = new("sick-bay.medevac.completed");
public static readonly AuditEventType SickBayMedevacSelfApprovalRejected = new("sick-bay.medevac.self-approval-rejected");
public static readonly AuditEventType SickBayRecoveryContactManaged      = new("sick-bay.recovery-contact.managed");
```

(That is 10 unique kebab-case keys + 11 named static-readonly constants — `SickBayPharmacyViewed` is the 1st; `SickBayRecoveryContactManaged` is the 10th. The 11th constant is `SickBayLabDiagnosticViewed`'s pre-existing inclusion in this list — verify count = 11 against ADR 0082 §6 before finalizing the PR.)

### 1.5 ShipRole + ShipAction additions (per ADR 0082 §5; gated on H1)

In `packages/foundation-ship-common/ShipRole.cs` (assuming W#46 Phase 1 has shipped a `ShipRole` enum; if the file is named differently, locate the enum declaration and add `IDC`):

```csharp
public enum ShipRole
{
    // ... existing values from W#46 Phase 1 ...
    IDC,  // ADR 0082 — Independent Duty Corpsman ("Doc"); Sick Bay department head
}
```

**Exhaustive-switch caveat:** the `foundation-ship-common` changelog MUST document this addition + provide the default-case pattern for existing `switch` sites in W#49 OOD blocks and W#50 Engine Room blocks (per ADR 0082 §5 note). Verify with `dotnet build` after the addition; if any sibling block raises `CS8509`, add a `default:` case there and note in PR.

In `packages/foundation-ship-common/ShipAction.cs` (or wherever W#46 P1 places the catalog), add 6 `ShipAction` constants:

```csharp
// ── Sick Bay (ADR 0082) ──────────────────────────────────────────────────────
public static readonly ShipAction ViewSickBay              = new("ViewSickBay");
public static readonly ShipAction ViewPharmacy             = new("ViewPharmacy");
public static readonly ShipAction ManageRecoveryContacts   = new("ManageRecoveryContacts");
public static readonly ShipAction TriggerKeyRotation       = new("TriggerKeyRotation");
public static readonly ShipAction InitiateMedevac          = new("InitiateMedevac");
public static readonly ShipAction AuthorizeMedevac         = new("AuthorizeMedevac");
// `ViewFirstAid` is ALL authenticated roles per §5 — ship it but DO NOT add to gated tuple list
public static readonly ShipAction ViewFirstAid             = new("ViewFirstAid");
```

That's 7 names; the ADR §5 table describes 7 actions (`ViewSickBay`, `ViewPharmacy`, `ManageRecoveryContacts`, `TriggerKeyRotation`, `InitiateMedevac`, `AuthorizeMedevac`, `ViewFirstAid`). Per the ledger row text "6 ShipAction" the body conventionally treats `ViewFirstAid` as outside the role-gated set; add all 7 constants but document `ViewFirstAid`'s open access in the permission-tuple registration (§3.b below).

Permission-tuple registration in `DefaultPermissionResolver` (assuming W#46 P1 ships an extensible registration pattern):

| ShipAction | Minimum role |
|---|---|
| `ViewSickBay` | `IDC`, `Captain`, `XO` |
| `ViewPharmacy` | `IDC` only |
| `ManageRecoveryContacts` | `IDC`, `Captain` |
| `TriggerKeyRotation` | `Captain` (System for emergency override) |
| `InitiateMedevac` | `IDC`, `Captain` |
| `AuthorizeMedevac` | `Captain` only — four-eyes enforced in `IMedevacService.AuthorizeAsync` |
| `ViewFirstAid` | all authenticated roles (no role gate) |

### 1.6 DI registration (per ADR 0082 §7)

`packages/foundation-sick-bay/SickBayServiceCollectionExtensions.cs`:

```csharp
public static class SickBayServiceCollectionExtensions
{
    public static IServiceCollection AddSunfishSickBay(
        this IServiceCollection services,
        Action<SickBayOptions>? configure = null)
    {
        services.AddOptions<SickBayOptions>()
                .Configure(opts => configure?.Invoke(opts));

        // Phase 1 ships INTERFACE registrations only — implementations land in Phase 2/3b.
        // services.TryAddSingleton<ISickBayDataProvider, SickBayDataProvider>();   // Phase 2
        // services.TryAddSingleton<ISickBayCommandService, SickBayCommandService>();// Phase 3b
        // services.TryAddSingleton<IMedevacService, MedevacServiceImpl>();          // Phase 3b
        // services.TryAddSingleton<IFirstAidSurface, DefaultFirstAidSurface>();     // Phase 2
        // services.TryAddSingleton<IStretcherBearerPolicy, DefaultStretcherBearerPolicy>(); // Phase 2
        // services.TryAddSingleton<IKeyRotationScheduler, NoopKeyRotationScheduler>();// Phase 2

        return services;
    }
}
```

Phase 1 leaves the registrations commented or stubbed; Phase 2 + Phase 3b uncomment as implementations land.

### 1.7 Phase 1 tests

`packages/foundation-sick-bay/tests/Sunfish.Foundation.SickBay.Tests.csproj` (new project; same xUnit + NSubstitute versioning as cohort siblings).

Required tests:

```
PharmacyRecordCountTests:
  [Fact] Exact_with_count_below_3_returns_Suppressed
  [Fact] Exact_with_count_3_returns_value_3_not_suppressed
  [Fact] Exact_with_count_100_returns_value_100_not_suppressed
  [Fact] Suppressed_singleton_has_null_value_and_IsSuppressed_true

FirstAidHintTests:
  [Fact] Constructor_rejects_body_with_lt_char
  [Fact] Constructor_rejects_body_with_gt_char
  [Fact] Constructor_rejects_body_with_ampersand
  [Fact] Constructor_rejects_body_with_control_char_below_0x20
  [Fact] Constructor_accepts_body_with_newline_0x0A
  [Fact] Constructor_accepts_plain_text_body

MedevacStateTransitionTests:    (state-table verification, interface-only — impl in Phase 3b)
  [Fact] State_machine_documents_six_states
  // The state-transition logic itself is exercised in Phase 3b MedevacOrchestrator tests.

ContractSurfaceTests:
  [Fact] ISickBayDataProvider_has_required_members
  [Fact] ISickBayCommandService_has_required_members
  [Fact] IMedevacService_has_required_members
  [Fact] IFirstAidSurface_has_required_members
  [Fact] IStretcherBearerPolicy_has_required_members
  [Fact] IKeyRotationScheduler_has_required_members

AuditEventTypeConstantsTests:
  [Fact] All_eleven_SickBay_constants_present_and_kebab_case
```

### 1.8 Phase 1 halt conditions

| Halt | Action |
|---|---|
| **H1.A** — `foundation-ship-common` not on origin/main at start of Phase 1 | Either defer Phase 1 OR ship slice-A only (data model + interfaces, NO `ShipRole.IDC` / `ShipAction` / permission-tuple registration). Document choice in PR; file `cob-question-*.md` to `research-inbox/` if deferring slice-B. |
| **H1.B** — `AuditEventType` collision found in collision sweep | STOP. Do NOT add duplicate constant. File `cob-question-*.md` referencing the colliding name. |
| **H1.C** — `ShipAction` collision (e.g., another sibling already added `TriggerKeyRotation`) | STOP. Re-coordinate constant names; W#46/W#49/W#50 cohort may have shipped overlapping naming. |
| **H1.D** — `dotnet build` reveals `foundation-ship-common` exhaustive-switch CS8509 in W#49/W#50 blocks | Add `default: throw new NotSupportedException($"...")` to the offending switch site in the SAME PR. Do NOT split. |

**Pre-merge council:** standard 4-perspective adversarial; security-engineering subagent recommended (audit-constant additions + `IKeyRotationScheduler` new contract surface).

---

## Phase 2 — Reference implementations + DefaultStretcherBearerPolicy

**Effort:** ~4–5h | **PR:** 1 | **Review:** **security-engineering subagent MANDATORY** (per ADR 0082 §Trust impact + §Implementation checklist Phase 2)

**Gate:** Phase 1 merged. H4 (the `IFieldDecryptor`-absence reflection test) is verified IN Phase 2 — it is a Phase 2 deliverable not a prereq.

### 2.1 `SickBayDataProvider : ISickBayDataProvider`

Location: `packages/blocks-sick-bay/SickBayDataProvider.cs` (note: implementation lives in `blocks-sick-bay`, not `foundation-sick-bay` — keeps foundation-tier free of the option-driven aggregation logic).

Per ADR 0082 §1 + §2 + Phase 2 implementation checklist:

- **Pharmacy**: read `SickBayOptions.RegisteredFieldPurposes` for purpose labels and friendly names. Derive `RotationHealth` from `LastRotatedAt` + a configurable rotation-due threshold (in `SickBayOptions`; recommend `RotationDueAfter` default 60d, `RotationOverdueAfter` default 90d — DOCUMENT in option XML). Derive `RecordCount` via `PharmacyRecordCount.Exact(count)` — k-anonymity floor automatically applied by the factory.
- **Lab**: `await _missionEnvelopeProvider.GetCurrentAsync(ct)` (**ADR 0082-A1 corrected API** — no TenantId param; MissionEnvelope is process-level) → derive `LabDiagnosticResult` per probe using `ProbeStatus` + `DegradationKind` from each typed dimension record.
- **Atmosphere**: call `GetCurrentAsync(ct)` → count `ProbeStatus` across all 10 dimensions (Hardware/User/Regulatory/Runtime/FormFactor/Edition/Network/TrustAnchor/SyncState/VersionVector). **Warning** = `ProbeStatus.Stale | PartiallyDegraded`; **Critical** = `ProbeStatus.Failed | Unreachable`. Thresholds: 0W 0C → `Green`; ≥1W 0C → `Yellow`; (≥2W 0C OR 1C) → `Orange`; ≥2C → `Red`; provider null or not yet fetched → `Unknown` (ADR 0082-A1). `ForceEnableActive = false` stub; Phase 3 wires `IInstallForceEnableSurface.HasActiveInstallOverrideAsync`. See XO ruling `xo-ruling-2026-05-06T20-00Z-w54-phase2b-atmosphere-mapping.md`.
- **`SubscribeSnapshotAsync`**: subscribe to `IMissionEnvelopeObserver` for envelope changes; poll `SickBayOptions.FallbackPollingInterval` (default 60s) for Pharmacy + Lab data freshness. Coalesce concurrent change-events.
- **FORBIDDEN**: NO `IFieldDecryptor` reference anywhere in the class graph. Verified by Phase 2 reflection test (H4):

  ```csharp
  [Fact]
  public void SickBayDataProvider_DoesNotReference_IFieldDecryptor()
  {
      var assembly = typeof(SickBayDataProvider).Assembly;
      // Walk all method bodies via System.Reflection + Mono.Cecil OR scan transitive
      // ProjectReference graph for IFieldDecryptor — pick whichever pattern is already
      // established by the security-engineering subagent in cohort PRs (W#52 Tactical
      // shipped a similar reflection test; mirror its pattern).
      Assert.DoesNotContain("IFieldDecryptor",
          assembly.GetReferencedAssemblies().Select(a => a.FullName));
      // Stronger check: scan SickBayDataProvider's compiled IL for the IFieldDecryptor
      // type token. Document whichever approach the security council prefers.
  }
  ```

  **Pattern alignment:** cohort precedent is `Sunfish.Foundation.Tactical` and `Sunfish.Blocks.Tactical` reflection tests — ask the security-engineering subagent which existing utility class to reuse.

### 2.2 `DefaultStretcherBearerPolicy : IStretcherBearerPolicy`

Returns the four `StretcherBearerRole` values unconditionally for v1 (per ADR 0082 §4 default). Tenant-override via Standing Order is deferred per Open Question §2.

### 2.3 `DefaultFirstAidSurface : IFirstAidSurface`

Hardcoded hint library covering surface keys: `"pharmacy"`, `"lab"`, `"atmosphere"` (≥5 hints across these three; per ADR 0082 Open Q3, the static library is sufficient for Phase 1 demo). Each hint passes the `FirstAidHint` constructor's plain-text validation. Unknown keys return empty list.

### 2.4 `NoopKeyRotationScheduler : IKeyRotationScheduler`

Stub returns `Task.CompletedTask`. Phase 3b will swap to a real implementation wired into the W#32 / ADR 0046-A2 rotation substrate — flag the swap in the Phase 3b PR description.

### 2.5 DI extension finalization

In `SickBayServiceCollectionExtensions.AddSunfishSickBay`:

```csharp
services.TryAddSingleton<ISickBayDataProvider, SickBayDataProvider>();
services.TryAddSingleton<IFirstAidSurface, DefaultFirstAidSurface>();
services.TryAddSingleton<IStretcherBearerPolicy, DefaultStretcherBearerPolicy>();
services.TryAddSingleton<IKeyRotationScheduler, NoopKeyRotationScheduler>();
// ISickBayCommandService + IMedevacService remain commented — Phase 3b.
```

### 2.6 Phase 2 tests

```
SickBayDataProviderTests:
  [Fact] DoesNotReference_IFieldDecryptor       (H4 reflection test)
  [Fact] Pharmacy_uses_PharmacyRecordCount_factory_for_k_anonymity_floor
  [Fact] Pharmacy_RotationHealth_derives_from_LastRotatedAt_and_thresholds
  [Fact] Atmosphere_maps_DegradationKind_counts_to_AtmosphereHealth
  [Fact] GetSnapshotAsync_returns_partial_snapshot_on_per_department_timeout
  [Fact] SubscribeSnapshotAsync_emits_on_IMissionEnvelopeObserver_change

DefaultStretcherBearerPolicyTests:
  [Fact] GetEligibleRespondersAsync_returns_all_four_StretcherBearerRole_values

DefaultFirstAidSurfaceTests:
  [Fact] GetContextualHintsAsync_returns_empty_for_unknown_key
  [Fact] GetContextualHintsAsync_returns_pharmacy_hints_for_pharmacy_key
  [Fact] All_built_in_hints_pass_FirstAidHint_constructor_validation
```

### 2.7 Phase 2 halt conditions

| Halt | Action |
|---|---|
| **H4** — security council finds `IFieldDecryptor` referenced (transitively) in `SickBayDataProvider` | STOP. Redesign the Pharmacy read-model. Do NOT merge until reflection test passes AND security council clears. |
| **H2.A** — `IMissionEnvelopeProvider.GetCurrentEnvelope` API signature differs from documented shape | Verify against origin/main at `packages/foundation-mission-space/Services/Contracts.cs`; mirror the actual signature; document deviation in PR. |
| **H2.B** — `AtmosphereHealth` mapping thresholds rejected by council | Document threshold choice with rationale; council recommendation is binding. |

**Pre-merge council:** standard 4-perspective + **security-engineering subagent MANDATORY**.

---

## Phase 3a — `blocks-sick-bay` Blazor UI (Pharmacy + Lab + Atmosphere tabs)

**Effort:** ~4–5h | **PR:** 1 | **Review:** **WCAG/a11y subagent MANDATORY** (per ADR 0082 §8 + §Implementation checklist Phase 3a)

**Gate:** Phase 2 merged. H2 (W#53 Phase 1 — `KeyFingerprint`) clears Phase 3a's `KeyFingerprintDisplay.razor`. If H2 not yet cleared at start of Phase 3a, defer that single component to a slice-B follow-up; the rest of Phase 3a (Pharmacy/Lab/Atmosphere tab content) is NOT gated on H2.

Additional gate: W#46 Phase 3 (`ILiveAnnouncer` + `IFocusTrap` + `SunfishA11yAssertions`) — required for `aria-live` regions and `MedevacDialog.razor` focus-trap behavior. If Phase 3 of W#46 has not landed, defer the live-region wiring to a Phase 3a follow-up addendum.

### 3a.1 Project file

`packages/blocks-sick-bay/Sunfish.Blocks.SickBay.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Sunfish.Blocks.SickBay</RootNamespace>
    <AssemblyName>Sunfish.Blocks.SickBay</AssemblyName>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation-sick-bay\Sunfish.Foundation.SickBay.csproj" />
    <ProjectReference Include="..\foundation-ship-common\Sunfish.Foundation.Ship.Common.csproj" />
    <ProjectReference Include="..\ui-core\Sunfish.UICore.csproj" />
    <ProjectReference Include="..\ui-adapters-blazor\Sunfish.UIAdapters.Blazor.csproj" />
  </ItemGroup>
</Project>
```

### 3a.2 Components

Per ADR 0082 §8 + Phase 3a checklist:

- **`SickBayBlock.razor`** — root; tab navigation Pharmacy / Lab / Atmosphere. Permission gating via injected `IPermissionResolver` — Pharmacy tab hidden when actor lacks `ViewPharmacy`. SC 2.4.3 deterministic focus order on tab change.
- **`PharmacyTabContent.razor`** — inventory list rendering `PharmacyInventoryEntry` rows. `RotationHealth` badge: triple-encoded (color + icon shape + text label) per SC 1.4.1. `PharmacyRecordCount` rendering: when `IsSuppressed`, display literal text `"< 3"`; ARIA label `"record count suppressed below threshold"`.
- **`LabTabContent.razor`** — probe-history table; `<table>` with `<caption>` per SC 1.3.1. Contrast verified per SC 1.4.3.
- **`AtmosphereTabContent.razor`** — health gauge component. Polite live region for status updates (SC 4.1.3). Assertive live region ONLY on Red escalation (per ADR 0082 §8 SC 4.1.3 row).
- **`MedevacDialog.razor`** — `role="alertdialog"` + `aria-labelledby="dialog-title"` + `aria-describedby="dialog-consequence"`. `IFocusTrap.TrapFocus(dialog)` on open; initial focus → Cancel button (per cohort precedent ADR 0081 §7.6 dialog pattern). On state transition → polite live region announcement (Authorized / Cancelled / Complete). On unsolicited system cancel (e.g., timeout) → assertive live region.
- **`KeyFingerprintDisplay.razor`** (gated on H2) — monospace; chunk-grouping (8 chunks of 4 hex chars; per ADR 0082 §1.4.1 + W#53 P1 KeyFingerprint shape). `<span aria-label="fingerprint group N of 8: XX YY">` per chunk. Copy-to-clipboard button with visible focus indicator (SC 2.4.7).
- **`SickBayServiceCollectionExtensions.razor` extension** — `AddSunfishSickBayBlocks()` extension wiring the Razor components into the host (parallel pattern to other blocks-* packages).

### 3a.3 Phase 3a tests

```
SickBayBlockTests:
  [Fact] Pharmacy_tab_hidden_when_actor_lacks_ViewPharmacy
  [Fact] Tab_navigation_focus_lands_in_new_tab_content
  [Fact] Tab_focus_order_is_deterministic_left_to_right

PharmacyTabContentTests:
  [Fact] RotationHealth_badge_uses_color_icon_text_triple_encoding
  [Fact] PharmacyRecordCount_renders_lt_3_when_suppressed
  [Fact] PharmacyRecordCount_aria_label_describes_suppression

AtmosphereTabContentTests:
  [Fact] Status_updates_announce_via_aria_live_polite
  [Fact] Red_escalation_announces_via_aria_live_assertive
  [Fact] Yellow_to_Orange_transition_does_not_trigger_assertive

MedevacDialogTests:
  [Fact] Dialog_has_role_alertdialog_aria_modal_labelledby_describedby
  [Fact] Initial_focus_lands_on_cancel_button
  [Fact] Outcome_announced_on_close
  [Fact] Keyboard_operability_SC_2_1_1

KeyFingerprintDisplayTests:                 (only if H2 cleared)
  [Fact] Renders_monospace_with_chunk_groups
  [Fact] Each_chunk_has_aria_label_with_position_pronunciation
  [Fact] Copy_button_has_visible_focus_indicator
```

### 3a.4 Phase 3a halt conditions

| Halt | Action |
|---|---|
| **H2** — `KeyFingerprint` not yet on origin/main | Defer `KeyFingerprintDisplay.razor` to slice-B; ship Pharmacy/Lab/Atmosphere/Medevac in Phase 3a. |
| **H3a.A** — W#46 Phase 3 (`ILiveAnnouncer` / `IFocusTrap`) not yet on origin/main | Defer `aria-live` + focus-trap wiring to a Phase 3a addendum after W#46 P3 lands; ship the static markup in Phase 3a. |
| **H3a.B** — WCAG/a11y subagent finds SC 1.4.1 violation | Apply triple-encoding fix in same PR; do NOT defer. |
| **H3a.C** — Medevac dialog fails SC 2.1.1 keyboard operability | STOP. Per ADR 0082 §Pre-acceptance audit FAILED-conditions kill trigger: halt Phase 3b until 3a passes. |

**Pre-merge council:** standard 4-perspective + **WCAG/a11y subagent MANDATORY**.

---

## Phase 3b — `ISickBayCommandService` + `IMedevacService` implementations

**Effort:** ~3–4h | **PR:** 1 | **Review:** **security-engineering subagent MANDATORY**

**Gate:** Phase 2 + Phase 3a merged.

### 3b.1 `SickBayCommandService : ISickBayCommandService`

`packages/blocks-sick-bay/SickBayCommandService.cs` (or `foundation-sick-bay/` if no UI dep — verify with security council).

`TriggerKeyRotationAsync` ordering:
1. `IPermissionResolver` check for `ShipAction.TriggerKeyRotation` against current actor.
2. If denied: emit `SickBayPharmacyViewed`-style rejected audit event (NEW: `SickBayKeyRotationDenied`? — verify with council if a denied-event constant should be added; default to throwing `UnauthorizedAccessException` without adding a new audit constant).
3. If permitted: emit `SickBayKeyRotationTriggered` BEFORE calling `IKeyRotationScheduler.ScheduleAsync` (audit-before-operation invariant; cohort pattern from ADR 0081 §8.4).
4. Call `IKeyRotationScheduler.ScheduleAsync`.

### 3b.2 `MedevacServiceImpl : IMedevacService` (or `MedevacOrchestrator`)

State machine implementation per ADR 0082 §2 transition table:
- Each transition method emits its respective `AuditEventType` BEFORE the state mutation (audit-before-operation).
- `AuthorizeAsync` four-eyes invariant: if `authorizingPrincipal == storedRequestedBy`, emit `SickBayMedevacSelfApprovalRejected` THEN throw `InvalidOperationException("Self-approval rejected per four-eyes invariant.")`.
- Invalid transition (e.g., `CancelAsync` when state is `Complete`): throw `InvalidOperationException("Invalid transition: Complete → Cancelled.")` — do NOT silent no-op.
- Phase 1: intra-tenant notification only — `IChannelProvider` use is OPTIONAL for v1; if `IChannelProvider` is not yet built or is intra-tenant only per ADR 0076, leave a hook (`IMedevacEscalationStrategy` interface) for Phase 2 Bridge wire protocol (per ADR 0082 Open Q1).

### 3b.3 `IKeyRotationScheduler` real implementation (replaces `NoopKeyRotationScheduler`)

Wire to W#32 / ADR 0046-A2 rotation substrate. Verify the actual rotation API on origin/main at `packages/foundation-recovery/`; if no public scheduler API exists yet, leave the noop and document the deferred wiring in PR description.

### 3b.4 Phase 3b tests

```
SickBayCommandServiceTests:
  [Fact] TriggerKeyRotationAsync_emits_audit_pre_op
  [Fact] TriggerKeyRotationAsync_throws_when_actor_lacks_permission
  [Fact] TriggerKeyRotationAsync_calls_scheduler_after_audit

MedevacServiceTests:
  [Fact] RequestAsync_transitions_Idle_to_Requested
  [Fact] RequestAsync_emits_SickBayMedevacInitiated_pre_op
  [Fact] AuthorizeAsync_rejects_self_approval_and_emits_SickBayMedevacSelfApprovalRejected
  [Fact] AuthorizeAsync_transitions_PendingAuthorization_to_Authorized
  [Fact] CancelAsync_throws_InvalidOperationException_when_state_is_Complete
  [Fact] CompleteAsync_transitions_InProgress_to_Complete
  [Fact] All_transitions_audit_pre_op
```

### 3b.5 Phase 3b halt conditions

| Halt | Action |
|---|---|
| **H3b.A** — security council finds audit emission AFTER state change in any transition | STOP. Reorder; re-test; resubmit. |
| **H3b.B** — `IChannelProvider` (ADR 0076) cross-tenant scope conflict | Per ADR 0082 Open Q1: defer Bridge medevac wire protocol to follow-on workstream; ship intra-tenant notification only. |
| **H3b.C** — W#32 rotation substrate API differs from documented shape | Use `NoopKeyRotationScheduler`; document deferred wiring; add a follow-up issue. |

**Pre-merge council:** standard 4-perspective + **security-engineering subagent MANDATORY** (four-eyes invariant + audit-before-operation chain + permission gate).

---

## Phase 4 — Anchor + Bridge wiring + apps/docs

**Effort:** ~3–4h | **PR:** 1 | **Review:** standard council; WCAG/a11y subagent recommended (apps/docs accessibility).

**Gate:** Phase 3b merged.

### 4.1 Anchor wiring

In `accelerators/anchor/MauiProgram.cs`:

```csharp
builder.Services.AddSunfishSickBay(opts =>
{
    opts.RegisterPurpose("ssn", "Social Security Number");
    opts.RegisterPurpose("dob", "Date of Birth");
    // Add additional purposes per Anchor's Phase 2 commercial-mvp scope.
    opts.FallbackPollingInterval = TimeSpan.FromSeconds(60);
});
```

Add a Sick Bay tab/page in the Anchor demo shell (kitchen-sink integration per ADR 0082 Phase 4 checklist).

### 4.2 Bridge wiring

In `accelerators/bridge/Program.cs` — same `AddSunfishSickBay` registration. Bridge is multi-tenant; verify `SickBayDataProvider` snapshot honors `IActiveTeamAccessor` / `TenantId` scoping (cohort precedent from W#42 follow-on hand-off). If Bridge React Sick Bay UI is deferred, document and skip the React-side rendering.

### 4.3 apps/docs

- `apps/docs/blocks/sick-bay/overview.md` — block consumer documentation.
- `apps/docs/foundation/sick-bay/overview.md` — contract reference.
- `apps/docs/design-system/sick-bay-wcag.md` — WCAG 2.2 AA declaration listing all 11 SCs from ADR 0082 §8.

### 4.4 Phase 4 tests

```
AnchorSickBayIntegrationTests:
  [Fact] Anchor_DI_resolves_ISickBayDataProvider
  [Fact] Anchor_DI_resolves_IMedevacService
  [Fact] Sick_Bay_demo_page_renders_without_throwing
```

**Pre-merge council:** standard 4-perspective.

---

## Phase 5 — Ledger flip + memory + close

**Effort:** ~30 min | **PR:** 1 (or rolled into Phase 4 if scope is small enough)

### 5.1 Ledger flip

Edit `icm/_state/workstreams/W54-sick-bay-aggregation-surface.md`:
- Set `status: "built"`
- Update `status_cell:` to `` "`built` (5/5 phases shipped; PR #NNN)" ``
- Append a Notes paragraph summarizing PRs landed, halt-conditions cleared, and any follow-ups deferred (per W#41 / W#42 precedent).

Run `python3 tools/icm/render-ledger.py` to regenerate `active-workstreams.md`.
Verify `python3 tools/icm/render-ledger.py --check` exits 0.

### 5.2 XO project memory

Write a project memory file referencing the build outcome (use the `project_workstream_NN_*` naming convention; pattern from `project_workstream_42_wayfinder_built.md`).

### 5.3 W#35 cohort coordination

Sick Bay is the 6th of 7 cohort follow-ons. After W#54 ships `built`, only W#55 (Ship's Office) remains in-flight from the cohort. Confirm the W#55 hand-off file is on origin/main; if its halt-conditions reference a `IFirstAidSurface` hint key for `"ships-office"`, file a follow-up to extend the Phase 2 hint library.

---

## Appendix A — Cited substrate symbols (§A0 self-audit)

All symbols verified on origin/main as of hand-off authoring (2026-05-05):

| Symbol | Location | Verified |
|---|---|---|
| `TenantId` | `packages/foundation/Assets/Common/TenantId.cs` (`Sunfish.Foundation.Assets.Common`) | ✓ |
| `ActorId` | `packages/foundation/Assets/Common/ActorId.cs` (`Sunfish.Foundation.Assets.Common`) | ✓ |
| `PrincipalId` | `packages/foundation/Crypto/PrincipalId.cs` (`Sunfish.Foundation.Crypto`) | ✓ |
| `EncryptedField` | `packages/foundation-recovery/EncryptedField.cs` | ✓ |
| `IFieldDecryptor` | `packages/foundation-recovery/Crypto/IFieldDecryptor.cs` | ✓ (FORBIDDEN inside `ISickBayDataProvider` impls) |
| `IFieldEncryptor` | `packages/foundation-recovery/Crypto/IFieldEncryptor.cs` | ✓ |
| `IDimensionProbe<TDimension>` | `packages/foundation-mission-space/Services/Contracts.cs:19` | ✓ |
| `IMissionEnvelopeProvider` | `packages/foundation-mission-space/Services/Contracts.cs:49` | ✓ |
| `IMissionEnvelopeObserver` | `packages/foundation-mission-space/Services/Contracts.cs:35` | ✓ |
| `MissionEnvelope` | `packages/foundation-mission-space/Models/MissionEnvelope.cs` | ✓ |
| `ProbeStatus` / `DegradationKind` | `packages/foundation-mission-space/Models/Enums.cs` | ✓ |
| `AuditEventType` | `packages/kernel-audit/AuditEventType.cs` | ✓ |
| `IAuditTrail` | `packages/kernel-audit/IAuditTrail.cs` | ✓ |
| `ShipRole`, `IPermissionResolver`, `ShipAction` | `packages/foundation-ship-common/` | **ABSENT** (W#46 Phase 1 not yet built — H1) |
| `KeyFingerprint` | `packages/foundation/Crypto/KeyFingerprint.cs` (NOT foundation-recovery — cycle-prevention per PR #633) | **CLEARED** (W#53 Phase 1b merged via PR #633) |
| `KeyRotationTrigger` | `packages/foundation-recovery/` (per ADR 0068) | **ABSENT** until ADR 0068 Status: Accepted (H3) |
| `IKeyRotationScheduler` | NEW — introduced by THIS ADR (`Sunfish.Foundation.SickBay`) | net-new in Phase 1 |
| `IFirstAidSurface` | NEW — introduced by THIS ADR | net-new in Phase 1 |

§A0 directionality:
- **Negative existence verified:** `foundation-sick-bay/` and `blocks-sick-bay/` packages do NOT exist on origin/main; all 11 audit constants and 6+1 ShipAction names confirmed absent via grep sweeps.
- **Positive existence verified:** all substrate types listed above (Recovery / MissionSpace / Foundation Assets / Kernel Audit) confirmed PRESENT at the cited paths.
- **Structural-citation verified:** ADR 0082 §1 records use `required` keyword; `PharmacyRecordCount` factory enforces k=3 floor; `IFieldDecryptor` prohibition cited verbatim against ADR 0046-A2 §4 (the section governs decryption-as-audit-emitting-operation); `MedevacState` enum has 6 values matching the §2 state-transition table; `StretcherBearerRole` is a 4-value constrained enum (NOT `ShipRole`) per §4 escalation-prevention rationale.

---

## Appendix B — Forward references resolved at each phase

| Phase | Forward-refs needed | Unblocking workstream |
|---|---|---|
| Phase 1 | `ShipRole.IDC` + 6 ShipAction constants in `foundation-ship-common` | W#46 Phase 1 (H1) |
| Phase 2 | None beyond Phase 1 prereqs | — |
| Phase 3a | `KeyFingerprint` for `KeyFingerprintDisplay.razor` | W#53 Phase 1 (H2; affects 1 component only) |
| Phase 3a | `ILiveAnnouncer` + `IFocusTrap` + `SunfishA11yAssertions` | W#46 Phase 3 (H3a.A) |
| Phase 3b | `KeyRotationTrigger` typed in `PharmacyInventoryEntry.PendingTriggerLabel` | ADR 0068 Status: Accepted (H3; Phase 2 deferral; Phase 3b can ship without) |
| Phase 4 | None | — |

---

## Appendix C — Council subagent posture

| Phase | Subagents | Rationale |
|---|---|---|
| Phase 1 | Standard 4-perspective + security-engineering (recommended) | New audit constants + `IKeyRotationScheduler` new contract |
| Phase 2 | Standard + **security-engineering MANDATORY** | `IFieldDecryptor` prohibition (H4) + audit emission ordering + k-anonymity floor |
| Phase 3a | Standard + **WCAG/a11y MANDATORY** | 11 SCs from ADR 0082 §8: SC 1.3.1, SC 1.4.1, SC 1.4.3, SC 2.1.1, SC 2.2.1, SC 2.4.3, SC 2.4.7, SC 3.3.1, SC 3.3.4, SC 3.3.8, SC 4.1.3 |
| Phase 3b | Standard + **security-engineering MANDATORY** | Four-eyes invariant + audit-before-operation chain + permission gate |
| Phase 4 | Standard 4-perspective | Demo wiring; no new security/a11y surface |
| Phase 5 | None (ledger flip) | Mechanical |

Cohort batting average per ADR 0069 D1 substrate-tier rule: pre-merge council canonical for every phase. Hand-offs (this document) are routine — no council required for the hand-off PR itself.

---

## Halt-conditions roll-up (counted)

Eight halt conditions across the 5 phases:

1. **H1** (Phase 1 gate) — `foundation-ship-common` not on origin/main → slice-A/slice-B split.
2. **H1.B** (Phase 1) — AuditEventType collision found → STOP.
3. **H1.C** (Phase 1) — ShipAction collision → STOP.
4. **H1.D** (Phase 1) — exhaustive-switch CS8509 in W#49/W#50 blocks after `ShipRole.IDC` add → fix in same PR.
5. **H4** (Phase 2 gate) — security council finds `IFieldDecryptor` referenced → halt + redesign.
6. **H2** (Phase 3a) — `KeyFingerprint` not on origin/main → defer `KeyFingerprintDisplay.razor`.
7. **H3a.A** (Phase 3a) — W#46 Phase 3 a11y substrate not on origin/main → defer live-region wiring.
8. **H3a.C** (Phase 3a kill trigger) — Medevac dialog fails SC 2.1.1 → halt Phase 3b.
9. **H3** (Phase 3b deferral) — ADR 0068 not Accepted → keep `string triggerReason`; raise A1 amendment when Accepted.
10. **H3b.A** (Phase 3b) — audit emission AFTER state change → STOP, reorder.

(10 enumerated halt-conditions; the 4 high-level halts named in the workstream-row Notes are H1–H4; this hand-off enumerates the operational sub-halts.)

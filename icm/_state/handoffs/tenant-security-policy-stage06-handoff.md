# W#37 Stage 06 Hand-off — Tenant Security Policy

**Workstream:** W#37 — Tenant-Configurable Security Policy + Atlas Surface
**ADR:** [ADR 0068](../../../docs/adrs/0068-tenant-security-policy.md) — Accepted 2026-05-05 via PR #584
**Owner (implementation):** sunfish-PM (COB)
**Status:** `ready-to-build` (ADR 0068 Status: Accepted; Phase 1 gated on W#46 Phase 1)
**Effort estimate:** ~14–20h / 2 phases / ~4–5 PRs
**Pipeline variant:** `sunfish-feature-change`
**W#34 follow-on position:** W#37 — promoted from W#34 §5.7 genuine gap

---

## Hard prerequisites

| # | Prereq | Verify on origin/main | Phase gated |
|---|---|---|---|
| **H1** | W#46 Phase 1 merged — `foundation-ship-common` package + `ShipRole` enum + `IPermissionResolver` interface + `ShipAction` catalog | `ls packages/foundation-ship-common/Sunfish.Foundation.Ship.Common.csproj 2>/dev/null && grep -l "enum ShipRole" packages/foundation-ship-common/` must be non-empty | Phase 1 — every type in `foundation-security-policy` references `ShipRole` (H1 blocks ALL Phase 1 work) |
| **H2** | ADR 0066 Amendment A1 Accepted — `IAtlasProvider<T>` formally ratified in ADR 0066 body | `grep -c "IAtlasProvider" docs/adrs/0066-helm-identity-atlas-surface.md` ≥ 1 amendment block | Phase 2 only |
| **H3** | W#53 Phase 1a merged — `IAtlasProvider<out TView>` in `packages/ui-core/Wayfinder/` | `grep -rn "IAtlasProvider" packages/ui-core/` must return ≥ 1 match | Phase 2 only |

**NuGet binary halt:** Pre-v1; no binary-compat halt. Confirm before every Phase 1 PR: `find packages/ -name "*.nupkg" | wc -l` must be 0.

**§GC.1 — General-counsel note travels with every Phase 1 PR.** This package specifies enforcement behavior (MFA requirements, device attestation, audit retention windows, key rotation cadence) that intersects HIPAA, PCI-DSS, SOC 2, GDPR, and the EU AI Act. Before the Stage 02 Architecture gate, CO confirmed that qualified counsel has reviewed ADR 0068 §§3–6. Every public type in `foundation-security-policy` MUST carry an XML `<remarks>` block referencing §GC.1. `foundation-security-policy/README.md` MUST reproduce §GC.1 verbatim above the API surface.

---

## Substrate verification (pre-Phase-1)

Run before writing a single line of Phase 1 code, in the worktree off origin/main:

```bash
# 1. Net-new package — must NOT pre-exist
ls packages/foundation-security-policy/ 2>/dev/null \
  && echo "PRE-EXISTS — halt; coordinate with parallel session" || echo "OK — net-new"

# 2. Substrate symbols (must be PRESENT on origin/main)
grep -l "enum ShipRole"              packages/foundation-ship-common/ 2>/dev/null   # H1
grep -l "IPermissionResolver"        packages/foundation-ship-common/ 2>/dev/null   # H1
grep -l "EncryptedField"             packages/foundation-recovery/EncryptedField.cs  # ADR 0046-A2
grep -l "IAuditTrail"                packages/kernel-audit/IAuditTrail.cs            # ADR 0049
grep -l "AuditEventType "            packages/kernel-audit/AuditEventType.cs         # ADR 0049
grep -l "IStandingOrderIssuer"       packages/foundation-wayfinder/ 2>/dev/null      # ADR 0065
grep -l "StandingOrderScope"         packages/foundation-wayfinder/ 2>/dev/null      # ADR 0065
grep -l "ApprovalChain"              packages/foundation-wayfinder/ 2>/dev/null      # ADR 0065

# 3. H1 gate state — absence is EXPECTED at hand-off authoring time
ls packages/foundation-ship-common/ 2>/dev/null \
  && echo "H1 cleared — Phase 1 may begin" \
  || echo "H1 BLOCKED — wait for W#46 Phase 1 merge"

# 4. AuditEventType collision sweep (all 11 names MUST be absent)
for n in SecurityPolicyBootstrapped SecurityPolicyProposed SecurityPolicyApprovalReceived \
         SecurityPolicyApplied SecurityPolicyRejected SecurityPolicyRescinded \
         SecurityPolicyMfaViolation SecurityPolicyAttestationViolation \
         SecurityPolicyKeyRotationOverdue SecurityPolicyRecoveryContactViolation \
         SecurityPolicyKeyEmergencyRotation; do
  grep -rq "$n" packages/kernel-audit/ 2>/dev/null && echo "COLLISION: $n" || true
done
echo "Collision sweep complete"
```

If any check fails unexpectedly, stop and write a `cob-question-*.md` to `icm/_state/research-inbox/`.

---

## Phase 1 — `foundation-security-policy` substrate

**Effort:** ~10–13h | **PRs:** ~2–3 | **Review:** pre-merge council mandatory (standard 4-perspective adversarial + security-engineering subagent mandatory)

**Gate: H1** — W#46 Phase 1 on origin/main. No slice without `ShipRole`.

### 1.1 Project file

`packages/foundation-security-policy/Sunfish.Foundation.SecurityPolicy.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Sunfish.Foundation.SecurityPolicy</RootNamespace>
    <AssemblyName>Sunfish.Foundation.SecurityPolicy</AssemblyName>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../foundation/Sunfish.Foundation.csproj" />
    <ProjectReference Include="../foundation-ship-common/Sunfish.Foundation.Ship.Common.csproj" />
    <ProjectReference Include="../foundation-wayfinder/Sunfish.Foundation.Wayfinder.csproj" />
    <ProjectReference Include="../kernel-audit/Sunfish.Kernel.Audit.csproj" />
  </ItemGroup>
</Project>
```

Do NOT add a `foundation-recovery` reference at Phase 1. `EncryptedField` is referenced in `RecoveryContactPolicy` in the ADR for future use; Phase 1 keeps the policy record as plain strings for recovery contacts. A Phase 3 amendment can add the encrypted shape.

### 1.2 Data model (`/Models/`)

Implement all records from ADR 0068 §§1.1–1.5 verbatim. File layout:

```
Models/
  TenantSecurityPolicy.cs          — §1, top-level record
  MfaEnrollmentPolicy.cs           — §1.1
  MfaFactor.cs                     — §1.1 enum
  DeviceAttestationPolicy.cs       — §1.2
  AttestationTier.cs               — §1.2 enum
  AttestationEvidence.cs           — §1.2
  AuditRetentionPolicy.cs          — §1.3
  AuditEventClass.cs               — §1.3 enum
  RetentionJurisdictionPreset.cs   — §1.3 enum
  KeyRotationPolicy.cs             — §1.4
  KeyRotationTrigger.cs            — §1.4 enum
  RecoveryContactPolicy.cs         — §1.5
```

**§GC.1 XML `<remarks>` requirement.** Every public type in `foundation-security-policy` MUST include:

```xml
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md). Enforcement behavior
/// in this package intersects HIPAA, PCI-DSS, SOC 2, GDPR, and the EU AI Act.
/// The presets and defaults are informed guidance, NOT legal advice. Deployers MUST
/// obtain qualified legal counsel before configuring enforcement behavior for production use.
/// </remarks>
```

### 1.3 Validation pipeline (`/Validation/`)

```
Validation/
  ISecurityPolicyValidator.cs           — §2 interface
  SecurityPolicyValidatorPriority.cs    — §2 enum
  SecurityPolicyValidationResult.cs     — §2 record
  SecurityPolicyValidationFinding.cs    — §2 record (with .Error factory)
  SecurityPolicyValidationSeverity.cs   — §2 enum
  SecurityPolicyValidationContext.cs    — §2 record
  ISecurityPolicyFloorValidator.cs      — §2.1.1 separate interface (non-replaceable)
  Validators/
    SchemaValidator.cs                  — §2.1.2 Priority=100
    ConsistencyValidator.cs             — §2.1.2 Priority=200
    FloorPolicyValidator.cs             — §2.1.2 Priority=300 (implements ISecurityPolicyFloorValidator)
```

Floor validator rules (§2.1.2 — ALL required):
- `MinimumContactCount ≥ 1`
- `ShipRole.Captain` MUST NOT have Email-only or Sms-only MFA factors
- `CompromiseIndicatorFlagged` cannot be removed from `AutoTriggers`
- `HipaaInformedDefault` floors `{Identity, Security, Configuration}` at 6 years
- `PciDssInformedDefault` floors `{Financial, Security}` at 12 months
- WCAG 3.3.8 warning for any role with only cognitive-test factors (`Totp`, `Email`, `Sms`)
- EmergencyOverride rate-limit invariant (1/24h)

`SUNFISH_RETENTION_FLOOR_001` Roslyn analyzer (`/Analyzers/RetentionFloorAnalyzer.cs`):
- Warning: `DefaultMinimumRetentionWindow < TimeSpan.FromDays(365)`
- Error: `DefaultMinimumRetentionWindow == TimeSpan.Zero`
- Runtime guard: `AuditRetentionPolicy` constructor also throws `ArgumentOutOfRangeException` when zero; the analyzer is defense-in-depth.

### 1.4 Policy issuance (`/Issuance/`)

```
Issuance/
  ISecurityPolicyIssuer.cs             — §3 interface (ProposeAsync / ApproveAsync / RescindAsync)
  SecurityPolicyApprovalResult.cs      — §3 record
  ISecurityPolicyApprovalFloorProvider.cs  — §3.1 floor assertions (non-replaceable)
  CapabilityProof.cs                   — §3.1.1 (bound to StandingOrderId; 24h expiry)
```

**§3.1 Approval floor assertions (ALL required):**
1. `ApprovalChain.Steps.Count ≥ 2`
2. At least one approver holds `ShipRole.Captain` or `ShipRole.XO`
3. `approver != proposal.IssuedBy` (no self-approval)
4. All `ActorId`s in chain are distinct
5. `CapabilityProof.ExpiresAt > DateTimeOffset.UtcNow` (24h window enforced at `ApproveAsync` time)

**§3.2 Captain-vacancy semantics.** When no actor holds `ShipRole.Captain`, security-policy issuance is blocked unless `ShipRole.XO` satisfies the floor. Log `SecurityPolicyCapVacancyException` audit event (string code via `AuditEventType.SecurityPolicyProposed` + payload flag; no new constant needed).

### 1.5 Policy enforcement (`/Enforcement/`)

```
Enforcement/
  ISecurityPolicyEnforcer.cs           — §4 interface (gate 7 in ADR 0077 §2 step 7)
  PolicyCheckResult.cs                 — §4 record (with .Compliant / .Violation factories)
  PolicyViolationKind.cs               — §4 enum
  KeyRotationStatus.cs                 — §4 enum
  RecoveryContactComplianceStatus.cs   — §4 enum
  DefaultSecurityPolicyEnforcer.cs     — §4 implementation
  IAttestationVerifier.cs              — §4.3 interface (contract only; no Phase 1 impls)
  AttestationVerificationResult.cs     — §4.3 record
```

**`PolicyCheckResult` factory constraint:** `AccessibleMessage` and `SuggestedAction` MUST be non-null at call sites in the `.Violation(...)` factory. `Compliant()` factory leaves them empty string. This enforces WCAG 3.3.1 + 3.3.3 at compile time.

**`DefaultSecurityPolicyEnforcer` Phase 1 behavior:**
- All `Tier > SoftwareSandbox` attestation evidence → `PolicyViolationKind.DeviceAttestationRequired` (no verifiers registered in Phase 1).
- 60-second TTL policy-projection cache (consistent with W#46 `DefaultPermissionResolver` halt-condition C; live-update via `IStandingOrderEventStream` is a Phase 2 follow-up per ADR 0065-A1).

### 1.6 Audit retention enforcement (`/Retention/`)

```
Retention/
  SecurityPolicyRetentionEnforcer.cs   — §5 reads active policy at purge time
```

No new interfaces at Phase 1 — the retention enforcer reads `TenantSecurityPolicy` from DI and applies `AuditRetentionPolicy` windows. The `kernel-audit` expiry mechanism calls this via its scheduled purge path.

### 1.7 Audit event types (§6)

Add 11 `static readonly AuditEventType` constants to `packages/kernel-audit/AuditEventType.cs` (cohort pattern per ADR 0065 §4):

| Constant | Value |
|---|---|
| `SecurityPolicyBootstrapped` | `Sunfish.SecurityPolicy.Bootstrapped` |
| `SecurityPolicyProposed` | `Sunfish.SecurityPolicy.Proposed` |
| `SecurityPolicyApprovalReceived` | `Sunfish.SecurityPolicy.ApprovalReceived` |
| `SecurityPolicyApplied` | `Sunfish.SecurityPolicy.Applied` |
| `SecurityPolicyRejected` | `Sunfish.SecurityPolicy.Rejected` |
| `SecurityPolicyRescinded` | `Sunfish.SecurityPolicy.Rescinded` |
| `SecurityPolicyMfaViolation` | `Sunfish.SecurityPolicy.MfaViolation` |
| `SecurityPolicyAttestationViolation` | `Sunfish.SecurityPolicy.AttestationViolation` |
| `SecurityPolicyKeyRotationOverdue` | `Sunfish.SecurityPolicy.KeyRotationOverdue` |
| `SecurityPolicyRecoveryContactViolation` | `Sunfish.SecurityPolicy.RecoveryContactViolation` |
| `SecurityPolicyKeyEmergencyRotation` | `Sunfish.SecurityPolicy.KeyEmergencyRotation` |

Also add typed payload factories in `packages/kernel-audit/Payloads/SecurityPolicyAuditPayloads.cs` (one `record` per event type carrying actor + policy-diff fields; follow the W#49 / W#50 payload pattern).

### 1.8 `foundation-security-policy/README.md`

MUST reproduce §GC.1 verbatim above the API surface description (ADR 0068 requirement). Include dependency diagram (foundation → foundation-ship-common → foundation-wayfinder → kernel-audit).

### 1.9 Test project

`packages/foundation-security-policy/tests/Sunfish.Foundation.SecurityPolicy.Tests.csproj`:

Required tests:
- `SchemaValidator_RejectsZeroRetentionWindow` — zero minimum window throws
- `FloorPolicyValidator_Captain_Sms_Only_IsError` — single-SMS-factor Captain rejected
- `FloorPolicyValidator_CompromiseIndicator_CannotBeRemoved`
- `FloorPolicyValidator_HipaaDefault_FloorsIdentityAt6Years`
- `FloorPolicyValidator_WcagWarning_CognitiveFactorsOnly`
- `ConsistencyValidator_ReadMoreRestrictiveThanPrivileged_IsError`
- `PolicyCheckResult_Violation_Factory_RequiresAccessibleMessage`
- `DefaultSecurityPolicyEnforcer_Phase1_Hardware_Evidence_Fails`
- `RetentionFloorAnalyzer_Warning_Below365Days` (Roslyn analyzer test)
- `RetentionFloorAnalyzer_Error_ZeroDays`

---

## Phase 2 — Atlas surface (`packages/ui-core/Wayfinder/Security/`)

**Effort:** ~4–7h | **PRs:** ~1–2 | **Review:** pre-merge council mandatory (WCAG/a11y subagent mandatory)

**Gate H2 + H3** — ADR 0066-A1 Accepted AND `IAtlasProvider<T>` present in `packages/ui-core/`:

```bash
grep -rn "IAtlasProvider" packages/ui-core/ | wc -l   # must be ≥ 1
grep -c "IAtlasProvider" docs/adrs/0066-helm-identity-atlas-surface.md  # must be ≥ 1
```

If either check fails: Phase 2 is blocked. Write `cob-question-*.md` to research-inbox. XO must author ADR 0066-A1 before Phase 2 can proceed.

### 2.1 New sub-folder: `packages/ui-core/Wayfinder/Security/`

Files to create (all from ADR 0068 §7 verbatim):

```
Wayfinder/Security/
  ISecurityPolicyAtlasSurface.cs   — IAtlasProvider<SecurityPolicyAtlasView>
  SecurityPolicyAtlasView.cs       — record
  MfaPolicyViewModel.cs            — record
  DeviceAttestationPolicyViewModel.cs — record
  AuditRetentionPolicyViewModel.cs    — record (+ AuditRetentionClassRow)
  KeyRotationPolicyViewModel.cs       — record
  RecoveryContactPolicyViewModel.cs   — record
  MfaComplianceStatus.cs           — enum
  DeviceAttestationComplianceStatus.cs — enum
```

### 2.2 WCAG 2.2 AA conformance requirements (mandatory, from ADR 0068 §8)

Per-method requirements at the Atlas surface:

- **WCAG 3.3.8** — every MFA factor form MUST offer a cognitive-test-free enrollment path (`WebAuthnPasskey` or `HardwareKey`). `FloorPolicyValidator` enforces at configuration time; the Atlas surface surfaces a compliance warning when no such factor is enrolled.
- **WCAG 3.3.7** (SC 3.3.7 — Redundant Entry) — any policy form field whose value was supplied in a prior step MUST be pre-populated; user must not re-enter data available from context.
- **WCAG 3.3.4** (Reversible) — security-policy changes MUST show a diff-preview (per `IDiffPreview<string, JsonNode>` from ADR 0077 §6.4) before commit. Confirmation modal MUST be a child `IFocusTrap` (LIFO stacking; focus returns to diff-preview on dismiss).
- **WCAG 1.3.1** (Info and Relationships) — per-cell `aria-label` on `DiffPreviewView.AccessibleRows`; NOT a single concatenated string.
- **Live region** — compliance status changes use `ILiveAnnouncer` with `Polite` priority (never `Assertive` for non-emergency status).

---

## ADR 0066 Amendment A1 — XO next action (not COB)

Phase 2 of this workstream is blocked on **ADR 0066 Amendment A1** ratifying `IAtlasProvider<out TView>` in the ADR 0066 body. This is an XO deliverable, not COB. When W#53 Phase 1a merges and `IAtlasProvider<T>` is present in `packages/ui-core/`, XO must author ADR 0066-A1 and get it accepted before COB can begin Phase 2. COB should write a `cob-question-*.md` to the research inbox if W#53 Phase 1a is done but ADR 0066-A1 has not been authored.

---

## Halt conditions

| ID | Condition | Action |
|---|---|---|
| **HC-1** | H1 not cleared (no `foundation-ship-common` on origin/main) | Wait for W#46 Phase 1. Do NOT start Phase 1 without `ShipRole`. |
| **HC-2** | Any `ShipRole` or `IPermissionResolver` symbol missing when H1 is claimed clear | Write `cob-question-*.md`; halt until XO confirms W#46 Phase 1 is fully merged. |
| **HC-3** | H2 or H3 not cleared (no ADR 0066-A1 or no `IAtlasProvider<T>` in ui-core) | Phase 2 blocked. Write `cob-question-*.md` noting Phase 2 gate state. |
| **HC-4** | `foundation-security-policy` already exists on origin/main when starting Phase 1 | Pre-existence means a parallel session acted; halt and coordinate. |
| **HC-5** | Security council returns Blocking findings on Phase 1 PR | Apply all Blocking amendments BEFORE enabling auto-merge. |

---

## Acceptance criteria

**Phase 1:**
- [ ] `packages/foundation-security-policy/` present on origin/main; `dotnet build` green
- [ ] All 11 `AuditEventType` constants present in `kernel-audit/AuditEventType.cs`
- [ ] `FloorPolicyValidator` passes all 8 test cases listed in §1.9
- [ ] `SUNFISH_RETENTION_FLOOR_001` analyzer: warning + error cases both verified in test project
- [ ] `DefaultSecurityPolicyEnforcer` Phase 1: hardware-tier evidence fails (no verifiers)
- [ ] Every public type carries §GC.1 `<remarks>` block
- [ ] `README.md` reproduces §GC.1 verbatim
- [ ] Pre-merge council complete; no Blocking findings outstanding

**Phase 2:**
- [ ] ADR 0066-A1 Accepted + W#53 Phase 1a verified on origin/main (gate H2+H3)
- [ ] `packages/ui-core/Wayfinder/Security/` present and compiles
- [ ] `ISecurityPolicyAtlasSurface` implements `IAtlasProvider<SecurityPolicyAtlasView>` per §7
- [ ] WCAG 3.3.8 diff-preview + IFocusTrap confirmation modal
- [ ] Per-cell `aria-label` (not concatenated) on diff-preview rows
- [ ] WCAG/a11y council complete; no Blocking a11y findings outstanding

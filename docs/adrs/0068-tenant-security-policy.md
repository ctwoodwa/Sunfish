---
id: 68
title: Tenant-Configurable Security Policy + Atlas Surface
status: Accepted
date: 2026-05-05
tier: foundation
pipeline_variant: sunfish-feature-change
concern:
  - security
  - configuration
  - accessibility
  - regulatory
  - ui
enables:
  - tenant-mfa-enrollment-policy
  - device-attestation-policy
  - audit-retention-policy
  - key-rotation-policy
  - recovery-contact-policy
  - security-policy-atlas-surface
  - wcag-3-3-8-accessible-authentication
composes:
  - 43
  - 46
  - 49
  - 65
  - 77
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments: []
---

# ADR 0068 — Tenant-Configurable Security Policy + Atlas Surface

**Status:** Proposed
**Date:** 2026-05-05
**Authors:** XO research session (council amendments applied 2026-05-05)
**Pipeline variant:** `sunfish-feature-change`
**Council posture:** standard adversarial + **security-engineering subagent** (mandatory) + **WCAG/a11y subagent** (mandatory; WCAG 3.3.8 Accessible Authentication + 4 WCAG 2.2 new AA SCs in scope) + **Pedantic Lawyer subagent** (mandatory; HIPAA / PCI-DSS / GDPR / SOC 2 / EU AI Act intersection) + **general-counsel engagement** (mandatory before Stage 02 Architecture gate; see §GC.1)
**Resolves:** W#37 — promoted from W#34 Wayfinder configuration UX discovery (§5.7 Layer 7 genuine gap, §6.4 highest commercial priority). Intake at `icm/00_intake/output/2026-05-01_tenant-security-policy-intake.md`.

> **§GC.1 General-counsel note.** This ADR specifies enforcement behavior (MFA requirement, device attestation, audit retention windows, key rotation cadence) that intersects HIPAA, PCI-DSS, SOC 2, GDPR, and the EU AI Act. The author has flagged required-factor defaults and retention presets as design choices, NOT as legal advice. Before the Stage 02 Architecture gate is declared "passed," the CO MUST confirm that a qualified attorney has reviewed §§ 3, 4, 5, and 6 of this ADR. This note travels with every amendment. Every public type defined in this ADR MUST carry an XML `<remarks>` block referencing §GC.1. The `foundation-security-policy/README.md` MUST reproduce §GC.1 verbatim above the API surface description.

---

## §A0 Cited-symbol audit

*Required per [ADR 0069](./0069-adr-authoring-discipline.md). All `Sunfish.*` symbols, package paths, and ADR numbers cited in Decision + Consequences + Compatibility sections.*

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.Wayfinder.StandingOrderScope` | Existing (ADR 0065) | yes — `Security` value present |
| `Sunfish.Foundation.Wayfinder.IStandingOrderIssuer` | Existing (ADR 0065) | yes |
| `Sunfish.Foundation.Wayfinder.StandingOrder` | Existing (ADR 0065) | yes |
| `Sunfish.Foundation.Wayfinder.ApprovalChain` | Existing (ADR 0065) | yes |
| `Sunfish.Foundation.Wayfinder.StandingOrderState` | Existing (ADR 0065) | yes |
| `Sunfish.Foundation.Wayfinder.IStandingOrderEventStream` | Existing (ADR 0065-A1) | yes — Subscribe(Action<StandingOrderAppliedEvent>) |
| `Sunfish.Foundation.Wayfinder.StandingOrderAppliedEvent` | Existing (ADR 0065-A1) | yes |
| `Sunfish.Foundation.Ship.Common.ShipRole` | **Phase-gated** (ADR 0077 / W#46 P1 not yet on origin/main) | yes — ADR 0077 §1 defines; W#46 builds |
| `Sunfish.Foundation.Ship.Common.IPermissionResolver` | **Phase-gated** (ADR 0077 / W#46 P1) | yes — ADR 0077 §2 defines |
| `Sunfish.Foundation.Ship.Common.PermissionDecision` | **Phase-gated** (ADR 0077 / W#46 P1) | yes — ADR 0077 §2 defines |
| `Sunfish.Foundation.Recovery.EncryptedField` | Existing (ADR 0046-A2) | yes |
| `Sunfish.Kernel.Audit.IAuditTrail` | Existing (ADR 0049) | yes |
| `Sunfish.Kernel.Audit.AuditEventType` | Existing (ADR 0049) | yes — audit constants placed here per cohort precedent |
| `Sunfish.UICore.Primitives.ILiveAnnouncer` | **Phase-gated** (ADR 0077 / W#46 P3) | yes — ADR 0077 §6.1 + line 554 `namespace Sunfish.UICore.Primitives` |
| `Sunfish.UICore.Primitives.IFocusTrap` | **Phase-gated** (ADR 0077 / W#46 P3) | yes — ADR 0077 §6.2 + line 554 |
| `Sunfish.UICore.Primitives.IDiffPreview` | **Phase-gated** (ADR 0077 / W#46 P3) | yes — ADR 0077 §6.4 |
| `Sunfish.UICore.Wayfinder.IAtlasProvider<T>` | **Phase-gated (ADR 0066-A1 required)** — W#53 Phase 1a hand-off builds this; formal ratification by ADR 0066 Amendment A1 is required before §7 of this ADR may be accepted for review | yes per hand-off; ADR 0066 body does not yet define it |
| `packages/foundation-security-policy/` | **Introduced** | yes — new package |
| `packages/ui-core/Wayfinder/Security/` | **Introduced** (additive sub-folder; Phase 2) | yes |
| ADR 0043, 0046, 0046-A2, 0049, 0065, 0065-A1, 0077 | Existing | yes — merged to origin/main |
| ADR 0066 (for `IAtlasProvider<T>` amendment) | Existing body; Amendment A1 **not yet authored** | see §7.1 |

*§A0 prerequisite gates: (1) W#46 Phase 1 on origin/main before any `ShipRole`/`IPermissionResolver` Phase 1 code; (2) ADR 0066 Amendment A1 accepted + W#53 Phase 1a on origin/main before Phase 2 (§7) code.*

---

## Status

Proposed. Pre-merge council complete (adversarial + security-engineering + WCAG/a11y + Pedantic Lawyer; amendments applied 2026-05-05). General-counsel review required before Stage 02 Architecture gate (§GC.1). Requires ADR 0066-A1 amendment to formally ratify `IAtlasProvider<T>` before Phase 2 (§7) can be built.

---

## Context

The W#34 Wayfinder discovery (§5.7, Layer 7) identified tenant-configurable security configuration as a **genuine gap**: no current Sunfish ADR specifies the *policy layer* (what security posture each tenant configures) or the *UX surface* (how admins set those policies). The adjacent ADRs each address mechanism, not policy:

- **ADR 0046 + 0046-A2** — key-loss recovery *mechanism* (`EncryptedField`, `IFieldDecryptor`, `IRecoveryCoordinator`). What key-rotation cadence is required is a *policy* question.
- **ADR 0049** — audit-trail *substrate* (`IAuditTrail`, `AuditEventType`). What retention windows apply per jurisdiction is a *policy* question.
- **ADR 0061** — transport tiers + device attestation at federation time. What attestation tier is required per tenant is a *policy* question.
- **ADR 0043** — OSS project merge-path *threat model*. Tenant security posture is out of scope for ADR 0043 by design.

This gap is a launch-blocker for the Phase 2 commercial scope: typically, regulated-industry tenants need configurable MFA, audit-retention, and key-rotation policies to meet their own compliance obligations under regimes such as HIPAA, PCI-DSS, and SOC 2 (per W#34 §6.4 commercial assessment). ADR 0065 provides `StandingOrderScope.Security` — the issuance path exists. ADR 0077 provides the `ShipRole` taxonomy and `IPermissionResolver` that policy composition builds on. This ADR specifies what goes into the security configuration slot.

**WCAG 3.3.8 intersection.** WCAG 2.2 SC 3.3.8 — Accessible Authentication (Level AA, new in WCAG 2.2) is in direct scope for this ADR: MFA enrollment UX, device-attestation prompts, and TOTP entry forms must not use cognitive-function tests (typing an OTP from memory, solving an image CAPTCHA) without providing an accessible alternative. This is also an EN 301 549 procurement requirement for Bridge tenants in EU jurisdictions (§8.6).

---

## Decision drivers

1. **W#34 §6.4 identifies this gap as highest commercial priority among W#34 follow-ons.** Security-policy gaps are launch-blockers for regulated-industry tenants.
2. **ADR 0065 Standing Order contract is the right issuance path.** `StandingOrderScope.Security` is defined. Security-policy changes are append-only, CRDT-aware, and audit-by-construction.
3. **ADR 0077 ShipRole taxonomy composes cleanly.** Per-role MFA requirements map to `ShipRole`; `IPermissionResolver` already gates surfaces by capability.
4. **Typed C# policy records, not raw Rego.** The policy surface is bounded (5-6 named domains). Typed records compose with the C# type system, are auditable at issuance, are schema-describable for Atlas search, and are testable without a policy engine runtime.
5. **New `foundation-security-policy` package.** The regulatory compliance lifecycle of audit-retention windows, HIPAA/PCI-DSS defaults, and jurisdiction presets diverges from role-taxonomy change cadence.
6. **WCAG 3.3.8 conformance as a contract, not a goal.** Per W#34 hardening: every MFA factor form must offer a cognitive-function-test-free path. The `FloorPolicyValidator` enforces this at policy-configuration time.
7. **Multi-actor approval floor that cannot be reduced.** The platform floor is Captain + 1 officer co-approval for any security-policy change. No Standing Order can lower this. Prevents a single compromised admin account from silently removing all MFA requirements.
8. **General-counsel engagement mandatory before enforcement behavior is specified in final form.** Retention windows, HIPAA defaults, and "right to erasure vs audit immutability" are per-deployment legal determinations.

---

## Considered options

### Option A — Extend `foundation-ship-common`

Add types to `Sunfish.Foundation.Ship.Common` in a `Security` sub-namespace.

- **Con:** Compliance lifecycle of retention presets diverges from role-taxonomy change cadence; adds compliance churn to a load-bearing package.
- **Rejected.**

### Option B — New `foundation-security-policy` package **[RECOMMENDED]**

New `packages/foundation-security-policy/` with `Sunfish.Foundation.SecurityPolicy` namespace. Depends on `foundation-ship-common` + `foundation-wayfinder` + `kernel-audit`. Does not depend on `ui-core`.

- **Pro:** Separate change cadence for compliance items; follows the existing one-concern-per-package pattern.
- **Adopted.**

### Option C — Embed in `foundation-recovery`

- **Con:** Pulls `ui-core` Atlas dependency into foundation-tier — inverts dependency arrow. Rejected.

### Option D — Raw Rego / OPA

- **Con:** New language runtime dependency; not type-safe in C#; not justified for a bounded 5-6 domain policy surface. Rejected.

---

## Decision

### 1. Security policy data model

New package: `packages/foundation-security-policy/`
New namespace: `Sunfish.Foundation.SecurityPolicy`

```csharp
public sealed record TenantSecurityPolicy(
    TenantId TenantId,
    MfaEnrollmentPolicy Mfa,
    DeviceAttestationPolicy DeviceAttestation,
    AuditRetentionPolicy AuditRetention,
    KeyRotationPolicy KeyRotation,
    RecoveryContactPolicy RecoveryContact,
    DateTimeOffset LastUpdatedAt,
    StandingOrderId? LastUpdatedBy    // null = DefaultFor / never issued
)
{
    public static TenantSecurityPolicy DefaultFor(TenantId tenantId, DateTimeOffset now) =>
        new(tenantId,
            MfaEnrollmentPolicy.Default,
            DeviceAttestationPolicy.Default,
            AuditRetentionPolicy.Default,
            KeyRotationPolicy.Default,
            RecoveryContactPolicy.Default,
            now,
            null);
}
```

**§1.1 Null `LastUpdatedBy`.** A `null` `LastUpdatedBy` means the policy is the platform default — never modified by a Standing Order. The bootstrap audit event `SecurityPolicyBootstrapped` (§6) is emitted at tenant provisioning. There is NO bootstrap exemption from the multi-actor approval floor (§3.2): the first Standing Order modification of the policy requires Captain + officer co-approval. `TenantSecurityPolicy.DefaultFor` is the platform-provisioned state, not the first tenant Standing Order.

**§1.2 `TenantSecurityPolicy` is immutable.** The in-memory value is a projection of the Standing Order log. When a new Standing Order is applied, a new `TenantSecurityPolicy` replaces the projection. `LastUpdatedBy` points to the most recently applied Standing Order.

---

#### 1.1 MFA enrollment policy

```csharp
public sealed record MfaEnrollmentPolicy(
    IReadOnlyDictionary<ShipRole, IReadOnlyList<MfaFactor>> RequiredFactorsByRole,
    TimeSpan EnrollmentGracePeriod,
    bool RecoveryFlowExemptsFromMfa)   // default false — see §1.1.1
{
    public static readonly MfaEnrollmentPolicy Default = new(
        RequiredFactorsByRole: new Dictionary<ShipRole, IReadOnlyList<MfaFactor>>
        {
            [ShipRole.Captain]         = [MfaFactor.WebAuthnPasskey, MfaFactor.Totp],
            [ShipRole.XO]              = [MfaFactor.WebAuthnPasskey, MfaFactor.Totp],
            [ShipRole.EngineerOfficer] = [MfaFactor.Totp],
            [ShipRole.Navigator]       = [MfaFactor.Totp],
            [ShipRole.TacticalOfficer] = [MfaFactor.Totp],
            [ShipRole.IDC]             = [MfaFactor.Totp],
            [ShipRole.Scribe]          = [MfaFactor.Totp],
            [ShipRole.EOOW]            = [MfaFactor.Totp],
            // DivisionOfficer: absent from default; tenant-configurable
            // OOD: watch designation; attaches to underlying role (§1.1.2)
        },
        EnrollmentGracePeriod: TimeSpan.FromDays(7),
        RecoveryFlowExemptsFromMfa: false);
}

public enum MfaFactor
{
    Totp,           // RFC 6238; copy-paste MUST be enabled (WCAG 3.3.8)
    WebAuthnPasskey, // passkey; no cognitive-function test (preferred)
    HardwareKey,    // FIDO2 hardware token (YubiKey, etc.)
    Email,          // OTP via email — low assurance; copy-paste MUST be enabled
    Sms,            // OTP via SMS — RESTRICTED authenticator per NIST SP 800-63B Rev.3 §5.1.3.3
}
```

**§1.1.1 `RecoveryFlowExemptsFromMfa: false` default.** Recovery restores access, not privileges. Post-recovery, the actor MUST re-enroll an MFA factor before issuing privileged Standing Orders. Pairing with `KeyRotationTrigger.RecoveryCompleted` ensures key material also rotates.

**§1.1.2 `OOD`/`EOOW` excluded from `RequiredFactorsByRole`.** `OOD` and `EOOW` are watch designations, not stable role assignments. MFA requirements attach to the actor's underlying base role (`EngineerOfficer`, `DivisionOfficer`, etc.), not to the watch designation. The watch-transfer attestation requirement (`DeviceAttestationPolicy.RequireAttestationForWatchTransfer: true`) handles watch-specific posture.

**§1.1.3 `DivisionOfficer` absent from default.** Absent = tenant-configurable. Captains may add `DivisionOfficer` to the map in a policy Standing Order.

**§1.1.4 `MfaFactor.Email` note.** Email OTP is low assurance. The `FloorPolicyValidator` (§2.1) MUST reject any configuration where `Email` or `Sms` is the *only* enrolled factor for `ShipRole.Captain`, `ShipRole.XO`, or `ShipRole.EngineerOfficer`.

**§1.1.5 `MfaFactor.Sms` note.** NIST SP 800-63B Rev. 3 (2017) §5.1.3.3 classifies SMS OTP as a **RESTRICTED** authenticator: permitted but discouraged, subject to risk-assessment + user-notification requirements. The RESTRICTED status reflects SIM-swap, SS7 interception, and number-recycling risks. `Sms` is included for backwards compatibility but SHOULD NOT be the only factor for roles above `DivisionOfficer`. Deployers subject to specific regulatory regimes should consult counsel and the current SP 800-63 series.

**§1.1.6 WCAG 3.3.8 `FloorPolicyValidator` rule.** For any role whose `RequiredFactorsByRole` list contains *only* cognitive-test factors (`Totp`, `Email`, `Sms`), the platform must ensure that the UX offering at least one cognitive-test-free enrollment path (`WebAuthnPasskey` or `HardwareKey`) is presented. When no such factor is enrolled by the actor, the Atlas surface surfaces a compliance warning. This does not block access during grace period; it does block privileged Standing Orders after grace period expiry (§4.1).

---

#### 1.2 Device attestation policy

```csharp
public sealed record DeviceAttestationPolicy(
    IReadOnlyList<AttestationTier> AcceptedTiersForPrivilegedActions,
    IReadOnlyList<AttestationTier> AcceptedTiersForReadActions,
    bool RequireAttestationForWatchTransfer)
{
    public static readonly DeviceAttestationPolicy Default = new(
        AcceptedTiersForPrivilegedActions: [
            AttestationTier.AppleSecureElement,
            AttestationTier.AndroidHardwareKeyStore,
            AttestationTier.Tpm2,
            AttestationTier.Fido2HardwareToken,
        ],
        AcceptedTiersForReadActions: [
            AttestationTier.SoftwareSandbox,
            AttestationTier.AppleSecureElement,
            AttestationTier.AndroidHardwareKeyStore,
            AttestationTier.Tpm2,
            AttestationTier.Fido2HardwareToken,
        ],
        RequireAttestationForWatchTransfer: true);

    public bool IsReadAtLeastAsPermissiveAsPrivileged =>
        AcceptedTiersForPrivilegedActions.All(t => AcceptedTiersForReadActions.Contains(t));
}

public enum AttestationTier
{
    None                  = 0,  // no attestation; dev / test only
    SoftwareSandbox       = 10, // software-only; no hardware root of trust
    AndroidHardwareKeyStore = 20, // Android Keystore StrongBox / Titan M / OEM secure element
    Tpm2                  = 30, // Windows TPM 2.0
    AppleSecureElement    = 40, // Apple T2 / Apple Silicon SEP / iOS Secure Enclave
    Fido2HardwareToken    = 50, // FIDO2 hardware token with touch-required assertion
}

public sealed record AttestationEvidence(
    AttestationTier Tier,
    byte[] PlatformProof,   // REQUIRED for Tier > SoftwareSandbox; opaque blob to policy layer
    DateTimeOffset EvidenceAt
);
```

**§1.2.1 `AttestationEvidence.PlatformProof` is not self-reported.** The policy layer stores the opaque blob; the `IAttestationVerifier` (§4.3) is responsible for verifying the proof against the platform's attestation root before `AttestationEvidence` is accepted. An `AttestationEvidence` with `Tier > SoftwareSandbox` and `PlatformProof` empty or null MUST fail the `IAttestationVerifier` check and return `PolicyViolationKind.DeviceAttestationRequired`. The policy model does not trust caller-supplied `Tier` values for hardware tiers.

**§1.2.2 Consistency invariant.** `AcceptedTiersForReadActions` MUST contain every tier in `AcceptedTiersForPrivilegedActions` (read can be equally or less restrictive; never more restrictive than privileged). The `ConsistencyValidator` (§2.1) checks `IsReadAtLeastAsPermissiveAsPrivileged`.

---

#### 1.3 Audit retention policy

```csharp
/// <remarks>
/// RIGHT-TO-ERASURE NOTE (§GC.1). Audit records in Sunfish are append-only per ADR 0049.
/// This policy specifies RETENTION WINDOWS per data class. The kernel-audit substrate
/// handles expiry at the MaximumRetentionWindow boundary.
/// Erasure requests against audit records during a mandatory minimum window require
/// legal sign-off and manual operator action. Sunfish does NOT expose a
/// "delete audit record" endpoint. Whether Article 17(3)(b) GDPR or another
/// legal-obligation exemption applies is a per-deployment legal determination.
/// See §GC.1 and §5.1.
/// </remarks>
public sealed record AuditRetentionPolicy(
    TimeSpan DefaultMinimumRetentionWindow,
    TimeSpan DefaultMaximumRetentionWindow,
    IReadOnlyDictionary<AuditEventClass, (TimeSpan Min, TimeSpan Max)> PerClassOverrides,
    RetentionJurisdictionPreset JurisdictionPreset)
{
    public static readonly AuditRetentionPolicy Default = new(
        DefaultMinimumRetentionWindow: TimeSpan.FromDays(365 * 3),
        DefaultMaximumRetentionWindow: TimeSpan.FromDays(365 * 7),  // common service-organization baseline
        PerClassOverrides: new Dictionary<AuditEventClass, (TimeSpan, TimeSpan)>(),
        JurisdictionPreset: RetentionJurisdictionPreset.Custom);
}

public enum AuditEventClass
{
    Security,       // authentication, key-rotation, policy changes
    Financial,      // transactions, ledger entries, payment events
    Identity,       // role assignments, recovery events, key operations
    Configuration,  // Standing Orders, Wayfinder changes
    System,         // transport, sync, infrastructure
}

/// <summary>
/// Convenience defaults informed by common interpretations of the named
/// regulatory regimes. Selecting a preset does NOT make a tenant compliant
/// with the named regime. Applicable retention windows depend on the specific
/// data being processed, the deployment jurisdiction, and qualified legal counsel.
/// See §GC.1.
/// </summary>
public enum RetentionJurisdictionPreset
{
    Custom              = 0,   // per-class overrides apply; no preset floor
    HipaaInformedDefault  = 1, // 6-year floor on Identity+Security+Configuration; §GC.1 applies
    PciDssInformedDefault = 2, // 12-month retention / 3-month immediately-available for Financial+Security; §GC.1 applies
    Soc2InformedDefault   = 3, // 7-year common baseline; SOC 2 does not mandate a specific window; §GC.1 applies
    GdprInformedDefault   = 4, // requires per-class manual configuration; duration = processing purpose; §GC.1 applies
    EuAiActInformedDefault = 5, // 10-year floor for high-risk AI systems; consult general counsel before enabling; §GC.1 applies
}
```

---

#### 1.4 Key rotation policy

```csharp
public sealed record KeyRotationPolicy(
    TimeSpan DefaultRotationCadence,
    IReadOnlyDictionary<ShipRole, TimeSpan> PerRoleOverrides,
    TimeSpan RotationGracePeriod,
    bool AllowEmergencyRotation,
    IReadOnlyList<KeyRotationTrigger> AutoTriggers)
{
    public static readonly KeyRotationPolicy Default = new(
        DefaultRotationCadence: TimeSpan.FromDays(90),
        PerRoleOverrides: new Dictionary<ShipRole, TimeSpan>
        {
            [ShipRole.Captain] = TimeSpan.FromDays(30),
        },
        RotationGracePeriod: TimeSpan.FromDays(7),
        AllowEmergencyRotation: true,
        AutoTriggers: [
            KeyRotationTrigger.CadenceExpired,
            KeyRotationTrigger.CompromiseIndicatorFlagged,
            KeyRotationTrigger.MfaFactorRevoked,
            KeyRotationTrigger.RecoveryCompleted,
            KeyRotationTrigger.AttestationTierDowngrade,
            KeyRotationTrigger.PolicyTightening,
        ]);
}

public enum KeyRotationTrigger
{
    CadenceExpired,              // normal scheduled rotation
    CompromiseIndicatorFlagged,  // Tactical anomaly-detection surfaces a threat signal
    MfaFactorRevoked,            // enrolled MFA factor removed → keys under that factor rotate
    AttestationTierDowngrade,    // device evidence drops below previously-accepted tier
    RecoveryCompleted,           // post-recovery rotation is mandatory (per ADR 0046)
    RoleChange,                  // role change triggers rotation of previous-role subkey
    ApproverRevoked,             // co-approver of a key-issuing Standing Order has role revoked
    RecoveryContactRemoved,      // recovery contact set drops below MinimumContactCount
    PolicyTightening,            // tenant raises MFA / attestation / retention floors
    EmergencyOverride,           // Captain-triggered immediate rotation; requires multi-actor approval
}
```

**§1.4.1 `CompromiseIndicatorFlagged` collapses grace period.** When `CompromiseIndicatorFlagged` fires, the `RotationGracePeriod` is reduced to zero for the affected key. No grace period extends a compromise-flagged key's lifetime. The `FloorPolicyValidator` enforces this invariant.

**§1.4.2 Emergency rotation.** `AllowEmergencyRotation: true` enables the `EmergencyOverride` trigger path, but emergency rotation MUST still satisfy the multi-actor approval floor (§3.1 — Captain + 1 officer). Rate-limited to 1 per 24h per actor per `FloorPolicyValidator`. The `SecurityPolicyKeyEmergencyRotation` audit event (§6) distinguishes this from cadence rotation.

---

#### 1.5 Recovery contact policy

```csharp
public sealed record RecoveryContactPolicy(
    int MinimumContactCount,
    int PreferredContactCount,
    TimeSpan VerificationCadence,
    TimeSpan EnrollmentDeadlineForNewTenants)
{
    public static readonly RecoveryContactPolicy Default = new(
        MinimumContactCount: 1,
        PreferredContactCount: 3,
        VerificationCadence: TimeSpan.FromDays(90),
        EnrollmentDeadlineForNewTenants: TimeSpan.FromDays(30));
}
```

---

### 2. Security policy validation pipeline

```csharp
public interface ISecurityPolicyValidator
{
    SecurityPolicyValidatorPriority Priority { get; }
    ValueTask<SecurityPolicyValidationResult> ValidateAsync(
        TenantSecurityPolicy proposed,
        TenantSecurityPolicy current,
        SecurityPolicyValidationContext context,
        CancellationToken ct = default);
}

public enum SecurityPolicyValidatorPriority
{
    Schema        = 100,
    Consistency   = 200,
    FloorPolicy   = 300,
    Regulatory    = 400,
}

public sealed record SecurityPolicyValidationResult(
    bool IsValid,
    IReadOnlyList<SecurityPolicyValidationFinding> Findings);

public sealed record SecurityPolicyValidationFinding
{
    public SecurityPolicyValidationSeverity Severity { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;   // WCAG 3.3.1 — plain-English
    public string Suggestion { get; init; } = string.Empty; // WCAG 3.3.3 — error suggestion

    public static SecurityPolicyValidationFinding Error(
        string code, string message, string suggestion) =>
        new() { Severity = SecurityPolicyValidationSeverity.Error,
                Code = code, Message = message, Suggestion = suggestion };
}

public enum SecurityPolicyValidationSeverity { Error, Warning, Info }

public sealed record SecurityPolicyValidationContext(
    TenantId TenantId, ActorId Proposer, ShipRole ProposerRole);
```

**§2.1 Built-in validators — ALL validators run unconditionally.** Validators do not short-circuit: every registered `ISecurityPolicyValidator` runs regardless of prior results. Findings are aggregated. Issuance fails if any `SecurityPolicyValidationSeverity.Error` finding is present. Priority governs the *ordering* of findings in the result set, not short-circuit evaluation. This prevents a low-priority validator from being masked by an early Error.

**§2.1.1 Floor validators cannot be replaced.** `FloorPolicyValidator` and `RegulatoryValidator` (priorities 300, 400) are registered via `services.AddSingleton<ISecurityPolicyFloorValidator, T>()` on a separate interface — not via `TryAddSingleton`. This prevents plugins from shadowing the platform floor.

**§2.1.2 Three built-in validators:**
- **SchemaValidator** (100): non-null required fields, valid enum values, non-negative time spans, counts ≥ 1.
- **ConsistencyValidator** (200): `RotationGracePeriod < DefaultRotationCadence`; `EnrollmentGracePeriod < DefaultRotationCadence`; `IsReadAtLeastAsPermissiveAsPrivileged == true`; `CompromiseIndicatorFlagged` in `AutoTriggers` is non-removable.
- **FloorPolicyValidator** (300): (a) `MinimumContactCount ≥ 1`; (b) `ShipRole.Captain` MUST NOT have `Email`-only or `Sms`-only factors; (c) `CompromiseIndicatorFlagged` collapses grace period (invariant: cannot be removed from AutoTriggers); (d) `RetentionJurisdictionPreset.HipaaInformedDefault` floors `{Identity, Security, Configuration}` classes at 6 years; (e) WCAG 3.3.8 rule: any role with only cognitive-test factors (`Totp`, `Email`, `Sms`) emits a Warning recommending adding a non-cognitive-test factor; (f) EmergencyOverride rate-limit (1/24h) and multi-actor approval requirement enforced.

---

### 3. Security policy issuance

Security-policy Standing Orders use `StandingOrderScope.Security` (ADR 0065). They differ from other Standing Orders in two ways:

1. **Multi-actor approval required (non-removable floor).** Captain + at least one additional Officer co-approval. This floor applies to ALL security-policy changes — including the first change from the platform default. There is NO bootstrap exemption.
2. **Mandatory diff-preview before commit.** The Atlas UI surface MUST present a structured diff per `IDiffPreview<string, JsonNode>` (ADR 0077) before the proposer confirms. Per ADR 0077 §5 First-Aid baseline and WCAG 3.3.4: security-impacting commitments require reversible + checked + confirmed.

```csharp
public interface ISecurityPolicyIssuer
{
    /// <summary>
    /// Propose a security-policy change. Standing Order enters Issued state.
    /// Does NOT take effect until the approval chain is satisfied.
    /// </summary>
    ValueTask<StandingOrderId> ProposeAsync(
        TenantId tenant,
        ActorId proposer,
        TenantSecurityPolicy proposed,
        string rationale,
        CancellationToken ct = default);

    /// <summary>
    /// Add a co-approval. The approver MUST be distinct from the proposer
    /// (no self-approval). When the floor is satisfied, the Standing Order
    /// transitions to Applied automatically.
    /// </summary>
    ValueTask<SecurityPolicyApprovalResult> ApproveAsync(
        TenantId tenant,
        ActorId approver,
        StandingOrderId proposal,
        CapabilityProof approverProof,  // fresh capability proof from ICapabilityGraph
        string? comment,
        CancellationToken ct = default);

    /// <summary>
    /// Rescind a pending proposal. Only the proposer or a Captain can rescind
    /// before the Standing Order is Applied.
    /// </summary>
    ValueTask RescindAsync(
        TenantId tenant,
        ActorId actor,
        StandingOrderId proposal,
        string reason,
        CancellationToken ct = default);
}

public sealed record SecurityPolicyApprovalResult(
    bool IsApprovalChainSatisfied,
    int ApprovalsGranted,
    int ApprovalsRequired);
```

**§3.1 Approval chain floor.** The built-in `ISecurityPolicyApprovalFloorProvider` asserts: (a) `ApprovalChain.Steps.Count ≥ 2`; (b) at least one `ApprovalStep.Approver` holds `ShipRole.Captain` or `ShipRole.XO` at the time `ApproveAsync` is called (role evaluated against the most recently Applied role-assignment Standing Order); (c) `approver != proposal.IssuedBy` (no self-approval); (d) all approver `ActorId`s in the chain are distinct. The floor is enforced at `ApplyAsync` time after all approval steps are received.

**§3.1.1 `CapabilityProof` for approval.** `ApproveAsync` requires a fresh `CapabilityProof` from the approver's `ICapabilityGraph.ExportProofAsync`, bound to the `StandingOrderId` as nonce. Approvals older than 24 hours (configurable) are rejected. This prevents stored-credential replay.

**§3.2 Captain-vacancy semantics.** If no actor holds `ShipRole.Captain`, security-policy Standing Order issuance is blocked until (a) a Captain is restored via the ADR 0046 recovery flow, or (b) the `XO` role satisfies the floor as the sole senior approver on the chain (Captain vacancy exception logged as `SecurityPolicyCapVacancyException` audit event). The platform does NOT auto-elevate any other role.

---

### 4. Policy enforcement

```csharp
/// <summary>
/// Called by IPermissionResolver as the security-posture gate (gate 7,
/// after capability-graph check per ADR 0077 §2 step 7).
/// </summary>
public interface ISecurityPolicyEnforcer
{
    ValueTask<PolicyCheckResult> CheckMfaComplianceAsync(
        TenantId tenant, ActorId actor, ShipRole role, CancellationToken ct = default);

    ValueTask<PolicyCheckResult> CheckDeviceAttestationAsync(
        TenantId tenant, ActorId actor, AttestationEvidence evidence,
        bool isPrivilegedAction, CancellationToken ct = default);

    ValueTask<KeyRotationStatus> GetKeyRotationStatusAsync(
        TenantId tenant, ActorId actor, ShipRole role, CancellationToken ct = default);

    ValueTask<RecoveryContactComplianceStatus> GetRecoveryContactComplianceAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default);
}

public sealed record PolicyCheckResult
{
    public bool IsCompliant { get; init; }
    public PolicyViolationKind? Violation { get; init; }
    public string AccessibleMessage { get; init; } = string.Empty;  // WCAG 3.3.1
    public string SuggestedAction { get; init; } = string.Empty;    // WCAG 3.3.3
    public TimeSpan? GracePeriodRemaining { get; init; }

    public static PolicyCheckResult Compliant() => new() { IsCompliant = true };

    public static PolicyCheckResult Violation(
        PolicyViolationKind violation,
        string accessibleMessage,
        string suggestedAction,
        TimeSpan? gracePeriodRemaining = null) =>
        new()
        {
            IsCompliant = false,
            Violation = violation,
            AccessibleMessage = accessibleMessage,   // required non-null per WCAG 3.3.1
            SuggestedAction = suggestedAction,        // required non-null per WCAG 3.3.3
            GracePeriodRemaining = gracePeriodRemaining,
        };
}

public enum PolicyViolationKind
{
    MfaEnrollmentRequired,
    MfaVerificationRequired,
    DeviceAttestationBelowTier,
    DeviceAttestationRequired,
    KeyRotationOverdue,
    KeyRotationDueSoon,
    RecoveryContactBelowMinimum,
    RecoveryContactVerificationOverdue,
}

public enum KeyRotationStatus { Current, DueSoon, GracePeriod, Overdue }
public enum RecoveryContactComplianceStatus { Compliant, BelowMinimum, NoneEnrolled, VerificationOverdue }
```

**§4.1 Enforcement position in the resolution pipeline.** Per ADR 0077 §2 step 7, `ISecurityPolicyEnforcer` is the **final gate** called by `IPermissionResolver` — after the capability-graph check (steps 1–6) has confirmed the actor holds the required capability. A security-policy violation at this step produces a `PermissionDecision.SecurityPolicyBlocked` result (a new discriminant that callers surface as degraded access per ADR 0077 §4.1 degradation primitive). This position is intentional: the capability proof is verified first (steps 1–6); the security posture is checked last (step 7). Callers MUST NOT call `ICapabilityGraph` directly to bypass the resolver — capability access paths that bypass `IPermissionResolver` also bypass the security-policy gate.

**§4.2 Degraded capability enforcement.** When `KeyRotationStatus.Overdue` or `PolicyViolationKind.MfaEnrollmentRequired`, the actor can still read data but cannot issue Standing Orders, transfer watch, or access privileged Helm actions. This follows the ADR 0077 §6.6 degradation primitive: the deck remains visible, certain actions gate with an accessible escalation path.

**§4.3 `IAttestationVerifier` contract (Phase 2).** Attestation proof verification is per-platform and gated on Phase 2. The interface is declared in `foundation-security-policy` Phase 1 to establish the contract; per-platform implementations ship in Phase 2:

```csharp
public interface IAttestationVerifier
{
    AttestationTier SupportedTier { get; }
    ValueTask<AttestationVerificationResult> VerifyAsync(
        byte[] platformProof, DateTimeOffset evidenceAt, CancellationToken ct = default);
}

public sealed record AttestationVerificationResult(
    bool IsVerified, AttestationTier VerifiedTier, string? FailureReason);
```

`DefaultSecurityPolicyEnforcer` calls the registered `IAttestationVerifier` chain for `Tier > SoftwareSandbox`. If no verifier matches the claimed tier, the result is `PolicyViolationKind.DeviceAttestationRequired`. In Phase 1 (no per-platform verifiers yet), all `Tier > SoftwareSandbox` evidence fails. Platform implementations (Apple App Attest, Google Play Integrity, Windows TPM EK certificate chain, FIDO2 MDS) are Phase 2 deliverables.

---

### 5. Audit retention enforcement

The `AuditRetentionPolicy` configures retention windows per `AuditEventClass`. The `Kernel.Audit` implementation reads the active policy at scheduled purge time.

**§5.1 Right-to-erasure + audit immutability.** Sunfish's audit substrate is append-only per ADR 0049. The platform exposes no UI affordance to delete audit records during the configured retention window. Whether a specific erasure request must be honored, whether GDPR Article 17(3)(b) exempts a specific audit record class from the right-to-erasure (that article exempts processing "for compliance with a legal obligation which requires processing by Union or Member State law to which the controller is subject" — whether that obligation applies is a per-deployment legal determination), and whether HIPAA retention duties preclude an erasure request are per-deployment legal determinations the deployer must make with qualified counsel. Sunfish does not make these determinations. See §GC.1.

**§5.2 `HipaaInformedDefault` floor.** When `RetentionJurisdictionPreset.HipaaInformedDefault` is active, the `FloorPolicyValidator` floors `{Identity, Security, Configuration}` classes at 6 years (informed by 45 CFR § 164.530(j)(2), § 164.316(b)(2)(i), and § 164.528(a)(1) — each applies to specific HIPAA documentation types with specific scope). **The `Configuration` class is included because Standing Orders are policy-and-procedure records.** `PciDssInformedDefault` floors `{Financial, Security}` at 12 months (PCI-DSS v4.0 Requirement 10.5.1). These are informed defaults only; see §GC.1.

**§5.3 `SUNFISH_RETENTION_FLOOR_001` Roslyn analyzer.** Flags any `AuditRetentionPolicy` construction where `DefaultMinimumRetentionWindow < TimeSpan.FromDays(365)` as a Warning, and `DefaultMinimumRetentionWindow == TimeSpan.Zero` as an Error. Runtime-side: `AuditRetentionPolicy` validates in the `SchemaValidator` — `DefaultMinimumRetentionWindow == TimeSpan.Zero` throws `ArgumentOutOfRangeException` at construction time. The analyzer is defense-in-depth; the runtime check is the primary control.

---

### 6. Audit event types

11 `static readonly AuditEventType` constants added to `Sunfish.Kernel.Audit.AuditEventType` (cohort precedent per ADR 0065 §4, ADR 0078/0079/0080/0081):

| Constant | Value | Description |
|---|---|---|
| `SecurityPolicyBootstrapped` | `Sunfish.SecurityPolicy.Bootstrapped` | Platform default set at tenant provisioning |
| `SecurityPolicyProposed` | `Sunfish.SecurityPolicy.Proposed` | Multi-actor proposal submitted |
| `SecurityPolicyApprovalReceived` | `Sunfish.SecurityPolicy.ApprovalReceived` | One approver co-signed |
| `SecurityPolicyApplied` | `Sunfish.SecurityPolicy.Applied` | Approval chain satisfied; policy active |
| `SecurityPolicyRejected` | `Sunfish.SecurityPolicy.Rejected` | Proposal rejected |
| `SecurityPolicyRescinded` | `Sunfish.SecurityPolicy.Rescinded` | Proposal cancelled before Apply |
| `SecurityPolicyMfaViolation` | `Sunfish.SecurityPolicy.MfaViolation` | MFA check failed; session limited |
| `SecurityPolicyAttestationViolation` | `Sunfish.SecurityPolicy.AttestationViolation` | Device attestation below required tier |
| `SecurityPolicyKeyRotationOverdue` | `Sunfish.SecurityPolicy.KeyRotationOverdue` | Key rotation past grace period |
| `SecurityPolicyRecoveryContactViolation` | `Sunfish.SecurityPolicy.RecoveryContactViolation` | Recovery contacts below minimum |
| `SecurityPolicyKeyEmergencyRotation` | `Sunfish.SecurityPolicy.KeyEmergencyRotation` | Emergency rotation invoked |

Typed payload factories in `SecurityPolicyAuditPayloads.cs` follow the W#49/W#50/W#51/W#52 pattern (typed `record` payload per event with all relevant actor + policy-diff fields).

---

### 7. Atlas surface (Phase 2 — gated on ADR 0066-A1 + W#53 Phase 1a)

> **Phase gate.** `ISecurityPolicyAtlasSurface : IAtlasProvider<SecurityPolicyAtlasView>` MUST NOT be authored until: (1) ADR 0066 Amendment A1 is Accepted (formally ratifying `IAtlasProvider<T>` in the ADR body); AND (2) `grep -rn "IAtlasProvider" packages/ui-core/` returns ≥ 1 match (W#53 Phase 1a build verification). Phase 1 deliverables (§§ 1–6) have no gate.

New sub-folder: `packages/ui-core/Wayfinder/Security/`
New namespace: `Sunfish.UICore.Wayfinder.Security`

```csharp
// Requires W#46 P1 (ShipRole) + W#53 P1a (IAtlasProvider<T>)
public interface ISecurityPolicyAtlasSurface : IAtlasProvider<SecurityPolicyAtlasView>
{
    ValueTask<MfaPolicyViewModel> GetMfaPolicyAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default);
    ValueTask<DeviceAttestationPolicyViewModel> GetDeviceAttestationPolicyAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default);
    ValueTask<AuditRetentionPolicyViewModel> GetAuditRetentionPolicyAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default);
    ValueTask<KeyRotationPolicyViewModel> GetKeyRotationPolicyAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default);
    ValueTask<RecoveryContactPolicyViewModel> GetRecoveryContactPolicyAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default);
}

public sealed record SecurityPolicyAtlasView(
    TenantId TenantId,
    MfaComplianceStatus MfaStatus,
    DeviceAttestationComplianceStatus DeviceStatus,
    AuditRetentionPolicyViewModel AuditRetention,
    KeyRotationStatus KeyRotationStatus,
    RecoveryContactComplianceStatus RecoveryContactStatus,
    SyncState OverallCompliance,
    DateTimeOffset LastUpdatedAt
);

public sealed record MfaPolicyViewModel(
    IReadOnlyDictionary<ShipRole, IReadOnlyList<MfaFactor>> RequiredFactors,
    MfaComplianceStatus ActorStatus,
    TimeSpan? GracePeriodRemaining,
    string? ComplianceMessage,
    string? SuggestedAction
);

public sealed record DeviceAttestationPolicyViewModel(
    AttestationTier RequiredTierForPrivileged,
    AttestationTier RequiredTierForRead,
    AttestationTier? ActorCurrentTier,
    DeviceAttestationComplianceStatus ActorStatus
);

public sealed record AuditRetentionPolicyViewModel(
    TimeSpan DefaultMinimum, TimeSpan DefaultMaximum,
    RetentionJurisdictionPreset Preset,
    IReadOnlyList<AuditRetentionClassRow> PerClassRows
);

public sealed record AuditRetentionClassRow(
    AuditEventClass Class, TimeSpan MinimumWindow, TimeSpan MaximumWindow,
    bool IsJurisdictionFloor
);

public sealed record KeyRotationPolicyViewModel(
    TimeSpan DefaultCadence, KeyRotationStatus ActorStatus,
    DateTimeOffset? NextRotationDeadline, TimeSpan? GracePeriodRemaining
);

public sealed record RecoveryContactPolicyViewModel(
    int MinimumRequired, int PreferredCount, int ActorEnrolledCount,
    RecoveryContactComplianceStatus ActorStatus,
    TimeSpan? VerificationCadence, DateTimeOffset? NextVerificationDeadline
);

public enum MfaComplianceStatus { Compliant, GracePeriodActive, Required, NotConfigured }
public enum DeviceAttestationComplianceStatus
    { Compliant, BelowRequiredTier, Required, NotConfigured }
```

**§7.1 ADR 0066 Amendment A1 requirement.** ADR 0066 (Helm + Identity Atlas Surface) defines `IHelmWidget`, `IHelmWidgetRegistry`, and `IIdentityAtlasSurface` but does not formally define `IAtlasProvider<T>` in its body. The W#53 Stage 06 hand-off (`icm/_state/handoffs/helm-identity-atlas-stage06-handoff.md`) builds `IAtlasProvider<out TView>` as the first Phase 1a deliverable. ADR 0066 Amendment A1 is required to ratify this contract in the ADR record before downstream ADRs (0067, 0068, and future Atlas specializations) can be accepted with `IAtlasProvider<T>` as a verified prerequisite.

**§7.2 Diff-preview contract.** The Atlas surface for security policy MUST implement `IDiffPreview<string, JsonNode>` (from `Sunfish.UICore.Primitives` per ADR 0077 §6.4) for any mutation flow. Each row carries per-cell `aria-label` per ADR 0077 §6.4 `DiffPreviewView.AccessibleRows` (not a single concatenated string). The confirmation modal MUST be a child `IFocusTrap` (`Sunfish.UICore.Primitives.IFocusTrap`) per ADR 0077 §6.2.1 LIFO stacking semantics; on dismissal focus returns to the diff-preview region.

---

### 8. WCAG 2.2 AA conformance specification

#### 8.1 WCAG 3.3.8 — Accessible Authentication (Level AA, new in WCAG 2.2)

SC 3.3.8 requires that if a cognitive function test is required by an authentication process, at least one other method is available that does not rely on cognitive testing, OR a mechanism is available to assist (e.g., copy-paste).

**Copy-paste is the *assistance mechanism* for TOTP (not a separate authentication method).** The authoritative non-cognitive-test *method* is `WebAuthnPasskey` or `Fido2HardwareToken`. The `FloorPolicyValidator` SHOULD warn when a role's `RequiredFactorsByRole` contains only cognitive-test factors, to prompt offering a passkey enrollment path.

**Conformant MFA paths:**
- `WebAuthnPasskey`: touch/biometric — no OTP transcription. **Conformant.** (Primary recommended alternative.)
- `Fido2HardwareToken`: touch-required assertion — no transcription. **Conformant.**
- `Totp` with copy-paste enabled: `autocomplete="one-time-code"` on web; `TextContentType = .oneTimeCode` on iOS. Clipboard paste MUST NOT be blocked. **Conformant as assistance mechanism.**
- `Email` / `Sms` OTP: same copy-paste requirement. **Conformant with copy-paste + timer announcements.**

**Forbidden patterns:**
- Disabling paste in OTP entry fields. **Forbidden.**
- Image CAPTCHA as MFA step without cognitive-test-free alternative. **Forbidden.**
- Knowledge-question as the *only* MFA factor. **Forbidden.**

**Recovery flows are authentication.** Recovery-contact verification (ADR 0046) is an authentication step for purposes of SC 3.3.8. Recovery-contact verification MUST offer at least one cognitive-test-free path (e.g., hardware-key signed challenge; passkey).

#### 8.2 WCAG 2.2.1 — Timing Adjustable (Level A)

TOTP windows (RFC 6238 default 30s) MUST announce via `Sunfish.UICore.Primitives.ILiveAnnouncer.AnnounceAsync` at **10 seconds** and **5 seconds** remaining. Announcing at 30s out of a 30s window fires on every page load; 10s/5s is the correct accessibility UX. For SMS/Email OTP (60–120s windows), announce at 30s and 10s. A keyboard-accessible "Use passkey instead" affordance MUST be reachable from the TOTP entry form, providing the non-time-constrained alternative.

#### 8.3 WCAG 3.3.7 — Redundant Entry (Level AA, new in WCAG 2.2)

Security policy forms MUST NOT re-request information the actor already entered in the same session. MFA re-confirm dialogs fall under the "essential" exception (3.3.7(1)) for security confirmation — but the exception MUST be explicitly invoked via `IFirstAidContract.ExemptFromRedundantEntry` (ADR 0077 §5) and documented per surface. Non-security fields in the policy-edit form (e.g., attestation tier dropdown already selected) MUST NOT be re-populated from scratch on validation failure.

#### 8.4 WCAG 3.3.1 + 3.3.3 — Error Identification + Error Suggestion (Level A + AA)

`PolicyCheckResult.AccessibleMessage` and `SuggestedAction` are required non-null when `IsCompliant == false` (enforced by `PolicyCheckResult.Violation(...)` factory — no direct construction of a `PolicyCheckResult` with `IsCompliant = false` and null fields). Same for `SecurityPolicyValidationFinding.Error(...)` factory. A `SUNFISH_SECURITY_POLICY_ERROR_FIELDS_001` Roslyn analyzer (Warning severity) flags `new PolicyCheckResult { IsCompliant = false }` without using the factory.

#### 8.5 WCAG 3.3.4 — Error Prevention (Level AA)

Security-policy Standing Orders: reversible (rescission available until Apply), checked (diff-preview per §7.2 shows current vs proposed), confirmed (confirmation step with `aria-describedby` pointing to diff-preview region). The confirmation modal is a child `IFocusTrap` per §7.2.

#### 8.6 WCAG 4.1.3 — Status Messages (Level AA)

Policy compliance status changes (e.g., "MFA enrollment required" → "compliant") MUST be announced via `Sunfish.UICore.Primitives.ILiveAnnouncer` without moving focus. Use `LiveRegionPoliteness.Polite` for advisory; `LiveRegionPoliteness.Critical` (ADR 0077 §6.1) for security violations requiring immediate action.

#### 8.7 WCAG 2.4.11 — Focus Not Obscured (Level AA, new in WCAG 2.2)

The focus ring MUST be visible above sticky violation banners for `PolicyCheckResult` with `KeyRotationStatus.Overdue`. Per ADR 0077 §6 adapter binding obligation.

#### 8.8 WCAG 2.5.8 — Target Size (Level AA, new in WCAG 2.2)

MFA factor toggle controls, attestation tier dropdowns, and "Approve / Reject" buttons in the multi-actor flow all require ≥ 24 × 24 CSS px (or platform equivalent). Per ADR 0077 `IFirstAidContract.TargetSize`.

#### 8.9 WCAG 3.2.6 — Consistent Help (Level AA, new in WCAG 2.2)

The policy-admin form's help affordance MUST appear in the consistent First-Aid slot per `IFirstAidContract` (ADR 0077 §5). Inherited from the ADR 0077 baseline; no additional implementation required if `IFirstAidContract` is applied.

#### 8.10 EN 301 549 procurement mapping

For Bridge tenants in EU jurisdictions:
- **§11.2.x** (web content): references WCAG 2.2 AA whole — covered by §§ 8.1–8.9 above.
- **§11.7** (user preferences): applies to announcement politeness / timer threshold if user has `prefers-reduced-motion`; the adapter-side `ILiveAnnouncer` MUST respect this OS setting.
- **§11.8.2** (authoring-tool accessibility): the policy-editing Atlas form is an authoring tool (admin authors Standing Orders). Standard `IFirstAidContract` + WCAG chain applies.
- **§11.5.2.3** (closed functionality): N/A for software-only Sunfish; document as out-of-scope in `apps/docs`.

#### 8.11 Conformance table

| SC | Requirement | Implementation |
|---|---|---|
| 3.3.8 (AA-2.2) | Accessible authentication | Copy-paste enabled; WebAuthnPasskey as non-cognitive-test alternative; FloorPolicyValidator warning |
| 2.2.1 (A) | Timing adjustable | `ILiveAnnouncer` at 10s/5s; "Use passkey instead" affordance |
| 3.3.7 (AA-2.2) | Redundant entry | `IFirstAidContract.ExemptFromRedundantEntry` for security re-confirm; non-security fields preserved |
| 3.3.1 (A) | Error identification | `PolicyCheckResult.Violation(...)` factory requires `AccessibleMessage` |
| 3.3.3 (AA) | Error suggestion | `PolicyCheckResult.Violation(...)` factory requires `SuggestedAction` |
| 3.3.4 (AA) | Error prevention | Diff-preview + rescindable + confirmed for policy proposals |
| 4.1.3 (AA) | Status messages | `ILiveAnnouncer` for compliance-state transitions |
| 2.4.11 (AA-2.2) | Focus not obscured | Focus ring above sticky violation banners |
| 2.5.8 (AA-2.2) | Target size | ≥ 24×24px for MFA controls, approval buttons |
| 3.2.6 (AA-2.2) | Consistent help | `IFirstAidContract` First-Aid slot; inherited |
| 1.3.3 (A) | Sensory characteristics | Policy status always text + color |
| 1.4.1 (A) | Use of color | Compliance badges always have text equivalent |

*SMS Note: SMS OTP is WCAG-conformant with copy-paste + timer announcements. This conformance assessment is independent of the security-suitability question (§1.1.5).*

---

### 9. DI registration

```csharp
public static IServiceCollection AddSunfishSecurityPolicy(
    this IServiceCollection services,
    Action<SecurityPolicyOptions>? configure = null)
{
    services.AddOptions<SecurityPolicyOptions>().Configure(configure ?? (_ => { }));
    services.TryAddSingleton<ISecurityPolicyEnforcer, DefaultSecurityPolicyEnforcer>();
    services.TryAddSingleton<ISecurityPolicyIssuer, DefaultSecurityPolicyIssuer>();
    // Validators — run order determined by Priority; all run unconditionally
    services.AddSingleton<ISecurityPolicyValidator, SchemaSecurityPolicyValidator>();
    services.AddSingleton<ISecurityPolicyValidator, ConsistencySecurityPolicyValidator>();
    // Floor validators — AddSingleton (not TryAdd; cannot be shadowed)
    services.AddSingleton<ISecurityPolicyFloorValidator, FloorPolicyValidator>();
    services.AddSingleton<ISecurityPolicyFloorValidator, RegulatoryPolicyValidator>();
    return services;
}

public static IServiceCollection AddSecurityPolicyValidator<TValidator>(
    this IServiceCollection services)
    where TValidator : class, ISecurityPolicyValidator
    => services.AddSingleton<ISecurityPolicyValidator, TValidator>();
```

`AddSunfishSecurityPolicy()` requires `AddSunfishWayfinder()` (ADR 0065) and `AddSunfishSharedDesignSystem()` (ADR 0077) to be called first. If either is missing, the extension throws `InvalidOperationException` with a descriptive message at startup.

---

## Consequences

### Positive

- W#37's highest-commercial-priority gap is closed. Regulated-industry tenants have a documented security-policy surface they can configure.
- Typed C# records compose naturally with the Standing Order log, the audit trail, and the capability graph.
- `ISecurityPolicyEnforcer` is a single injection point for security-posture checks; downstream surfaces route through one contract.
- WCAG 3.3.8 conformance is specified at the contract level; `PolicyCheckResult.Violation` factory enforces `AccessibleMessage` + `SuggestedAction` non-null at call sites.
- `SUNFISH_RETENTION_FLOOR_001` analyzer catches misconfiguration at build time; runtime `SchemaValidator` is the primary control.
- Multi-actor approval floor with no bootstrap exemption; `CompromiseIndicatorFlagged` collapses grace period unconditionally.

### Negative

- One additional package. `IAtlasProvider<T>` phase gate for §7 requires ADR 0066-A1 before Phase 2 can begin.
- `GdprInformedDefault` and `EuAiActInformedDefault` presets require per-class manual configuration; the platform cannot auto-derive correct retention windows.
- `IAttestationVerifier` per-platform implementations are Phase 2 deliverables; Phase 1 treats all `Tier > SoftwareSandbox` as unverified by default.
- General-counsel review adds a non-technical dependency before the ADR can be declared fully accepted for production use.

---

## Revisit triggers

- A regulated-industry tenant surfaces a compliance requirement not covered here (FedRAMP, CMMC, FIPS 140-2). Amend to add corresponding `AttestationTier` value or preset entry.
- WCAG 2.2 is superseded by WCAG 3.0. Reassess §8.
- NIST SP 800-63B revision updates the SMS RESTRICTED classification. Reassess §1.1.5.
- ADR 0066 Amendment A1 authored. Phase 2 (§7) Atlas surface authoring can begin after A1 acceptance + W#53 Phase 1a build verification.
- W#46 Phase 1 lands `ShipRole` + `IPermissionResolver` on `origin/main`. Phase 1 code of this ADR becomes buildable.
- `IStandingOrderEventStream.Subscribe(Action<StandingOrderAppliedEvent>)` (ADR 0065-A1) is wired in Sunfish host registrations. Update `DefaultSecurityPolicyEnforcer` to subscribe and invalidate its policy projection cache on receipt of applied events.
- A council review finds structural gaps in §3.1 approval floor. Amendment A1 process applies.

---

## References

- **Intake:** `icm/00_intake/output/2026-05-01_tenant-security-policy-intake.md`
- **W#34 discovery:** `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` §5.7 + §6.4
- **ADR 0043** — OSS merge-path threat model (distinct scope)
- **ADR 0046 + 0046-A1 + 0046-A2** — key-loss recovery + EncryptedField substrate
- **ADR 0049** — audit-trail substrate
- **ADR 0065 + 0065-A1** — Wayfinder + Standing Order contract; `IStandingOrderEventStream` (hard prerequisite)
- **ADR 0066** — Helm + Identity Atlas Surface (ADR 0066-A1 required for `IAtlasProvider<T>`)
- **ADR 0077** — Shared Design System (`ShipRole` + `IPermissionResolver` + `Sunfish.UICore.Primitives.*`)
- **WCAG 2.2 SC 3.3.8** — Accessible Authentication
- **NIST SP 800-63B Rev. 3** — §5.1.3.3 + §5.2.10 — SMS OTP RESTRICTED authenticator
- **HIPAA:** 45 CFR § 164.530(j)(2), § 164.316(b)(2)(i), § 164.528(a)(1) — retention provisions (scope per section; GC review required)
- **PCI-DSS v4.0** — Requirement 10.5.1 — 12-month retention / 3-month immediately available
- **GDPR Article 17(3)(b)** — legal-obligation erasure exemption (applies when Union or Member State law requires processing; per-deployment legal determination)
- **EN 301 549** — §11.2.x, §11.7, §11.8.2 procurement requirements

---

## Open questions (resolved)

| ID | Question | Disposition |
|---|---|---|
| OQ-1 | Should `ShipRole.DivisionOfficer` be included in the default MFA policy? | Resolved: absent from default map; tenant-configurable. See §1.1.3. |
| OQ-2 | Enum name for Android hardware attestation? | Resolved: `AndroidHardwareKeyStore` (descriptive; avoids vendor trademark). |
| OQ-3 | Should `GdprInformedDefault` auto-configure minimum windows? | Resolved: NO. GDPR retention depends on processing purpose — not auto-derivable. Manual configuration required; see §5.1 and §GC.1. |
| OQ-4 | Does the Atlas surface use `ISecurityPolicyIssuer` or `IStandingOrderIssuer` directly? | Resolved: Atlas surface is read-only. Mutations flow through `ISecurityPolicyIssuer` at the application layer. |
| OQ-5 | `Sms` factor: remove or deprecate? | Resolved: keep as RESTRICTED authenticator with floor-validator constraints; see §1.1.5. |
| OQ-6 | Bootstrap exemption from multi-actor floor? | Resolved: NO exemption. Platform default is `TenantSecurityPolicy.DefaultFor`; first modification requires full multi-actor approval. See §1.1 + §3. |

---

## Council disposition

| Finding | Severity | Resolution |
|---|---|---|
| B-1 — `IAtlasProvider<T>` not in ADR 0066 | Blocking | §7.1 added; ADR 0066-A1 gate explicit in §A0 + phase gate |
| B-2 — ADR 0077 symbols as "Existing" | Blocking | §A0 rows corrected to "Phase-gated (ADR 0077 / W#46 P1)" |
| B-3/F-1 — `ILiveAnnouncer`/`IFocusTrap` wrong namespace | Blocking | Updated to `Sunfish.UICore.Primitives` throughout |
| B-4 — `IObservable<StandingOrderAppliedEvent>` / `§H8` | Blocking | Revisit trigger updated to `IStandingOrderEventStream.Subscribe` per ADR 0065-A1 |
| B-5 — Audit constants in separate class | Blocking | §6 changed to `static readonly AuditEventType` constants on `Sunfish.Kernel.Audit.AuditEventType` |
| S-1 — Enforcer composition order contradicts ADR 0077 §2 | Blocking | §4.1 rewritten: security policy is gate 7 (last, after capability-graph) |
| S-2 — Bootstrap exemption exploitable | Blocking | Bootstrap exemption removed entirely; §1.1 + §3 updated |
| S-3 — Attestation self-reported | Blocking | `IAttestationVerifier` contract added §4.3 |
| S-4 — Approval-chain spoofable | Blocking | `CapabilityProof` required in `ApproveAsync`; non-self-approval; 24h expiry |
| S-5 — Missing key rotation triggers | Blocking | 5 new `KeyRotationTrigger` values added |
| S-6 — HIPAA preset missing Configuration class | Blocking | §5.2 updated; Configuration class included; HIPAA CFR citations expanded |
| F-2 — SC 3.3.7 Redundant Entry missing | Blocking | §8.3 added |
| F-3 — SC 3.3.8 copy-paste vs alternative method | Blocking | §8.1 clarified; FloorPolicyValidator note added |
| L-1 — Jurisdiction preset enum implies legal certification | Blocking | Renamed to `HipaaInformedDefault` etc.; enum docstring rewritten |
| L-2 — "HIPAA floor enforced" language | Blocking | §5.2 language rewritten |
| L-3 — GDPR Art 17(3)(b) analysis overstated | Blocking | §5.1 rewritten to remove legal conclusions |
| L-4 — NIST "deprecated" inaccurate | Blocking | §1.1.5 corrected to "RESTRICTED authenticator" |
| NM-5/SSC-1 — `Instant` vs `DateTimeOffset` | NM/SC | `DateTimeOffset` throughout |
| NM-6/SSC-2 — `StandingOrderId.BootstrapSentinel` | NM/SC | Changed to `StandingOrderId? LastUpdatedBy = null` |
| SNM-2 — `RecoveryFlowExemptsFromMfa` default | NM | Default changed to `false` |
| SNM-3 — Captain single-factor default | NM | Captain default now 2 factors |
| SSC-5 — OOD/EOOW in MFA default | SC | Removed; §1.1.2 explains |
| SSC-4 — `SecurityPolicyDefaultApplied` vs `SecurityPolicyBootstrapped` | SC | Consistent: `SecurityPolicyBootstrapped` |
| SC-5 — `LiveRegionPoliteness.Critical` §A1 citation | SC | Corrected to §6.1 |
| FNM-1 — TOTP timer 30s → 10s/5s | NM | §8.2 corrected |
| FNM-3 — `PolicyCheckResult` nullability | NM | Factory methods added |
| FNM-4 — EN 301 549 mapping absent | NM | §8.10 added |
| FNM-5,6,7 — WCAG 2.2 new SC rows missing | NM | §8.7, 8.8, 8.9 added |
| LNM-2 — SOC 2 "7 years" attribution | NM | §1.3 comment corrected |
| LNM-4 — EU AI Act missing from council posture | NM | Added to cover-line |
| LNM-6 — `StrongBox`/`SecureEnclave` trademark | NM | Renamed to `AndroidHardwareKeyStore`/`AppleSecureElement` |
| LNM-7 — Captain-vacancy semantics unspecified | NM | §3.2 added |
| SC-9,10 — Internal cross-reference typos in §5.1 | SC | Fixed |
| SC-11 — OQ-1 `MfaFactor.DivisionOfficer` | SC | Fixed to `ShipRole.DivisionOfficer` |
| LSC-2 — HIPAA CFR citations narrow | SC | Expanded to three subsections |
| LSC-4 — Normative "cannot operate without" | SC | Rewritten as "typically need to meet their own compliance obligations" |

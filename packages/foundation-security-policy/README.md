# Sunfish.Foundation.SecurityPolicy

Tenant-configurable security-policy substrate per
[ADR 0068 — Tenant-Configurable Security Policy + Atlas Surface](../../docs/adrs/0068-tenant-security-policy.md).
Ships the policy data model (MFA enrollment, device attestation, audit
retention, key rotation, recovery contact) consumed by the validation
pipeline (PR 2), the Standing-Order enforcer (PR 3), and the Atlas UI
surface (Phase 2).

---

## §GC.1 General-counsel note (reproduced verbatim from ADR 0068)

> This ADR specifies enforcement behavior (MFA requirement, device
> attestation, audit retention windows, key rotation cadence) that
> intersects HIPAA, PCI-DSS, SOC 2, GDPR, and the EU AI Act. The author
> has flagged required-factor defaults and retention presets as design
> choices, NOT as legal advice. Before the Stage 02 Architecture gate
> is declared "passed," the CO MUST confirm that a qualified attorney
> has reviewed §§ 3, 4, 5, and 6 of this ADR. This note travels with
> every amendment. Every public type defined in this ADR MUST carry an
> XML `<remarks>` block referencing §GC.1. The
> `foundation-security-policy/README.md` MUST reproduce §GC.1 verbatim
> above the API surface description.

The defaults in this package — recovery-contact `MinimumContactCount`,
audit-retention windows, MFA-factor requirements per `ShipRole`, key-
rotation cadences, the `*InformedDefault` retention presets — are
informed guidance, NOT legal advice. Deployers MUST obtain qualified
legal counsel before configuring enforcement behavior for production
use against any specific regulatory regime.

---

## API surface — PR 1 (data model only)

### Models

| Type | Purpose | Spec |
|---|---|---|
| `TenantSecurityPolicy` | Top-level immutable policy projection | ADR 0068 §1 |
| `MfaEnrollmentPolicy` | Per-`ShipRole` MFA factor requirements + grace + recovery-MFA exemption | §1.1 |
| `MfaFactor` | enum: TOTP / WebAuthnPasskey / HardwareKey / Email / SMS (NIST 800-63B RESTRICTED) | §1.1 |
| `DeviceAttestationPolicy` | Per-action-class accepted attestation tiers; watch-transfer requirement | §1.2 |
| `AttestationTier` | enum: None / SoftwareSandbox / AndroidHardwareKeyStore / Tpm2 / AppleSecureElement / Fido2HardwareToken | §1.2 |
| `AttestationEvidence` | Opaque platform-attestation evidence (verified by `IAttestationVerifier`, future PR) | §1.2 |
| `AuditRetentionPolicy` | Min/max retention windows per `AuditEventClass`; jurisdiction preset | §1.3 |
| `AuditEventClass` | enum: Security / Financial / Identity / Configuration / System | §1.3 |
| `RetentionJurisdictionPreset` | enum: Custom / Hipaa / PciDss / Soc2 / Gdpr / EuAiAct *InformedDefault* | §1.3 |
| `KeyRotationPolicy` | Default + per-role rotation cadences; grace period; emergency-rotation flag; auto-triggers | §1.4 |
| `KeyRotationTrigger` | enum: CadenceExpired / CompromiseIndicatorFlagged / ... / EmergencyOverride | §1.4 |
| `RecoveryContactPolicy` | Minimum/preferred contact count; verification cadence | §1.5 |

### Out of scope (PR 1)

Validation pipeline (`ISecurityPolicyValidator` + `FloorPolicyValidator`
with NIST/HIPAA/PCI-DSS rule enforcement), Standing-Order enforcer,
audit-event types in `kernel-audit`, Roslyn analyzer
(`SUNFISH_RETENTION_FLOOR_001`), and the Atlas UI surface (Phase 2)
all ship in subsequent PRs. This PR establishes the immutable data
model + §GC.1 docs surface; nothing executable yet beyond `Default`
factories.

---

## Dependency graph

```
foundation
  ↓
foundation-ship-common (ShipRole, IPermissionResolver, ShipAction)
  ↓
foundation-security-policy  ← THIS PACKAGE
  ↓
(future) foundation-security-policy validation pipeline + enforcer
(future) foundation-security-policy → kernel-audit integration
(future) ui-core/Wayfinder/Security (Phase 2 Atlas surface)
```

## See also

- [ADR 0068](../../docs/adrs/0068-tenant-security-policy.md) — substrate spec
- [ADR 0077](../../docs/adrs/0077-ship-role-taxonomy-and-permission-resolver.md) — `ShipRole` source
- [ADR 0065](../../docs/adrs/0065-standing-orders-and-wayfinder-execution.md) — Standing-Order issuance path
- [ADR 0049](../../docs/adrs/0049-audit-trail-substrate.md) — `IAuditTrail` + `AuditEventType` consumer
- [ADR 0046-A2](../../docs/adrs/0046-key-loss-recovery.md) — `EncryptedField` future integration (Phase 3)

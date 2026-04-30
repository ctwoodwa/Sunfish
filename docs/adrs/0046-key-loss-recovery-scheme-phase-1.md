# ADR 0046 — Key-loss recovery scheme for Business MVP Phase 1

**Status:** Accepted (2026-04-26; package-placement section added 2026-04-29; **A2 + A3 + A4 + A5 amendments landed 2026-04-30** — see §"Amendments (post-acceptance, 2026-04-30)")
**Date:** 2026-04-26 (Accepted) / 2026-04-29 (package-placement section) / 2026-04-30 (A2 amendment / A3 mechanical fixes / A4 substantive resolution of council F2/F4/F5 / A5 mechanical fixes from A4 council review)
**Resolves:** Open Question Q5 from `icm/01_discovery/output/business-mvp-phase-1-discovery-interim-2026-04-26.md` (key-loss recovery implementation reference for primitive #48). **A2 also resolves** ADR 0058 amendment A1 halt-condition (Stage 06 build for W#18 Vendors gated on `EncryptedField` + `IFieldDecryptor` substrate types existing).

## Context

The Sunfish Business MVP Plan (`C:/Projects/the-inverted-stack/docs/business-mvp/mvp-plan.md` §10 Phase 1, deliverable #5) calls for *"Backup/restore + multi-sig social recovery (#48 key-loss recovery)."* The book's `design-decisions.md` §5 Volume 1 extensions defines primitive #48 with **6 sub-patterns** (48a-48f), but the plan does not specify which combination Sunfish should ship for Phase 1.

P7 (ownership) is the most user-visible Kleppmann property and the most common real-world failure mode in local-first deployments — *"user loses their key → loses all data."* Sub-patterns 48a-48f differ substantially in cryptographic complexity, UX burden, and post-MVP extensibility. Phase 1 needs a defined posture so the implementation can land without re-litigating the choice mid-build.

## Decision drivers

- **P7 ownership is non-negotiable.** Without recovery, a forgotten password = bankrupt customer's books are gone. Sunfish loses on Day 1.
- **MVP audience is SMB owners**, not crypto-natives. Recovery UX must work without trustees being technical, without paper-key handling expertise, without smartphone biometric adoption assumptions.
- **Primitive #48 was added to the catalog in v1.1** specifically because earlier Kleppmann property analysis ASSUMED keys are never lost. Sunfish cannot afford the same assumption.
- **Failed-conditions clause for #48** (per `concept-index.yaml` P7 kill-trigger): "Key-loss recovery path inadequate (no multi-sig OR custodian OR paper-key fallback)." MVP must satisfy at least one of those three.
- **Existing kernel-security primitives** (Ed25519, X25519, SqlCipher key derivation, root seed provider, role keys, team subkeys) already cover the cryptographic substrate. The recovery scheme is a layer ON TOP of these, not a replacement.
- **Post-MVP extensibility** matters — institutional custodians (48b) and biometric-derived keys (48d) are real customer asks for v1.x but require significant per-platform work. They should not gate Phase 1.

## Considered options

### Option A — 48a only (multi-sig social recovery, 3-of-5 friends)

- **Pro:** simplest cryptographic implementation; well-understood Argent / Vitalik-essay pattern; works for any user with 5 contacts.
- **Con:** no offline fallback (lost laptop AND lost trustees = no recovery); no audit trail; no time-locked grace period (race with potential attackers).
- **Verdict:** insufficient on its own. P7 kill-trigger requires "multi-sig OR custodian OR paper-key" — single-leg recovery is brittle.

### Option B — 48a + 48c (social + paper-key fallback)

- **Pro:** social recovery covers the common case; paper-key (printed seed phrase stored offline) is the disaster-recovery fallback. Two independent legs.
- **Con:** still no audit trail; still no time-locked grace period; no defense against trustee-collusion attacks.
- **Verdict:** acceptable but missing key safety patterns from the catalog.

### Option C — 48a + 48e + 48f + 48c (social + grace period + audit trail + paper fallback) **[RECOMMENDED]**

- **48a multi-sig social recovery** — 3-of-5 trustees can attest to a recovery request
- **48e timed-recovery with grace period** — once 3 trustees attest, original holder gets 7-day window to dispute (pushes attacker timeline outside reasonable response window)
- **48f cryptographically-signed audit trail** — recovery event recorded as signed log entry; future devices can replay and verify
- **48c paper-key fallback** — printed recovery phrase stored offline (bank safe deposit box, fire-safe at home); independent of all 5 trustees being available
- **Pro:** covers the catalog's full failed-conditions list; defense in depth; matches Argent + Apple iCloud Keychain + crypto-wallet patterns; survives most realistic threat scenarios
- **Con:** 4 sub-patterns to implement vs 1 or 2; UX flow has 2 entry points (social + paper); initial Trustee Setup wizard has more steps
- **Verdict:** the right Phase 1 posture. The complexity is unavoidable for P7 to actually hold.

### Option D — 48a + 48b + 48c + 48d + 48e + 48f (all six)

- **Pro:** maximum coverage; matches Apple iCloud Keychain comprehensiveness
- **Con:** 48b (institutional custodian) requires legal partnerships, attestation infrastructure, and per-jurisdiction compliance — months of out-of-band work; 48d (biometric-derived) requires per-platform native API integration (Win Hello / Touch ID / fingerprint reader / Linux PAM) — broad platform surface; both are post-MVP per plan §13
- **Verdict:** out of Phase 1 scope; deferred to post-MVP roadmap

## Decision

**Adopt Option C.** Phase 1 ships:

1. **48a multi-sig social recovery** — owner designates 3-of-5 trustees; each receives an Ed25519 signing key during onboarding; quorum of 3 attests to recovery request via signed message
2. **48e timed-recovery with grace period** — once quorum attested, original holder has 7 days (configurable per deployment, 7-30 day range) to dispute via any device still holding original keys; only after window expires does new key issue
3. **48f cryptographically-signed audit trail** — every recovery event written to per-tenant audit log (encrypted, signed by attesting trustees, timestamped); replicated via the same sync protocol as business data; visible in tenant audit-log UI
4. **48c paper-key fallback** — at first-run, owner can optionally print/save a 24-word recovery phrase (BIP-39 wordlist for industry-standard compatibility); recovery phrase derives the same root seed as device-key, bypassing trustee quorum

48b (institutional custodian) and 48d (biometric-derived) are explicitly **deferred to post-MVP**. The Phase 1 architecture must not preclude adding them later (the recovery surface is an extensible enum, not hard-coded to the 4 ship-now flows).

## Package placement (added 2026-04-29)

The Phase-1 implementation is split across two packages per the paper's §5 tier model:

- **`packages/kernel-security/`** — kernel-tier crypto primitives that recovery depends on:
  - `Crypto/Ed25519Signer.cs`, `Crypto/X25519KeyAgreement.cs` (signature primitives)
  - `Keys/SqlCipherKeyDerivation.cs`, `Keys/KeystoreRootSeedProvider.cs` (key derivation primitives)
  - `Keys/RotateKeyAsync` SQLCipher rekey primitive
  - `AddSunfishKernelSecurity` + `AddSunfishRootSeedProvider` DI extensions

- **`packages/foundation-recovery/`** — foundation-tier orchestration over those primitives:
  - `IRecoveryCoordinator` + `RecoveryCoordinator` (pure orchestration; sub-patterns #48a / #48e / #48f)
  - `RecoveryRequest`, `TrusteeAttestation`, `RecoveryDispute`, `RecoveryEvent` (signed message envelopes)
  - `TrusteeDesignation`, `RecoveryCoordinatorOptions`, `RecoveryCoordinatorState`, `RecoveryStatus`
  - `IRecoveryStateStore` + `InMemoryRecoveryStateStore`
  - `IRecoveryClock` + `SystemRecoveryClock`
  - `IDisputerValidator` + `FixedDisputerValidator`
  - `PaperKeyDerivation` (#48c — BIP-39 wrapper over kernel-security PRFs)
  - BIP-39 English wordlist (`bip39-english.txt` embedded; LogicalName `Sunfish.Foundation.Recovery.bip39-english.txt`)
  - `IAuditTrail` integration per ADR 0049 (event emission target)
  - `AddSunfishRecoveryCoordinator` DI extension (registers all the above)

This split was identified during the 2026-04-28 ADR audit (audit finding C-2 in
`icm/07_review/output/adr-audits/CONSOLIDATED-HUMAN-REVIEW.md`). The original ADR
0046 text referenced `Sunfish.Foundation.Recovery` for the entire scheme — that
was a planning-time guess made before the kernel-security substrate was complete.
The shipped reality (split-by-concern) is the de-facto correct decision; this
section ratifies it. Phase 1 inventory + research-session sign-off lives at
`icm/_state/handoffs/adr-0046-recovery-package-split-INVENTORY.md`.

## Consequences

### Positive

- P7 ownership demonstrably works; user can recover from forgotten password OR lost laptop OR lost trustees (any one of those failure modes survivable as long as the OTHER one isn't simultaneously broken)
- Audit trail provides legally-defensible record of recovery events (matters for accountant-of-record liability, for divorce/dispute scenarios, for regulator audits)
- Paper-key offers fully-offline disaster recovery (works even if Sunfish, the trustees, the company, and the internet are all unavailable)
- Argent / Apple-Keychain pattern alignment makes the UX familiar for users who've already done crypto wallet setup or Apple-Account recovery
- Architecture is forward-compatible with 48b (custodian) and 48d (biometric) when those are added post-MVP

### Negative

- Trustee Setup wizard adds 5-10 min to first-run onboarding (mitigation: defer trustee designation; allow setup with paper-key only first, prompt for trustees after first week of use)
- Paper-key UX requires user discipline (print + secure offline storage); historically poor adoption in crypto wallets; need clear messaging that this IS the disaster recovery
- 7-day grace period means actual recovery is not instant; users locked out for a week if they don't have paper-key
- Trustees must keep their Ed25519 signing keys safe; trustee key loss reduces effective quorum; recommend N+M trustees for resilience (e.g., name 5, but allow 3 to attest — rebuilds when any trustee replaces their key)

## Revisit triggers

- A real customer scenario hits the trustee-coordination problem (e.g., 5 trustees but 3 forgot they were trustees) — may need UX or process adjustment
- Apple ships a new Secure Enclave recovery primitive that meaningfully changes 48d's complexity calculus
- Regulated SMB segment (healthcare, finance) requires institutional-custodian-of-record (48b) to satisfy compliance — promote 48b from post-MVP to a v1.x deliverable
- A real recovery incident produces a post-mortem that surfaces a missing pattern
- Cryptocurrency seed-phrase UX research advances meaningfully (e.g., Apple-Account recovery improvements; new hardware-wallet patterns)

## References

- Spec source: `C:/Projects/the-inverted-stack/docs/business-mvp/mvp-plan.md` §10 Phase 1 deliverable #5
- Primitive #48 catalog entry: `C:/Projects/the-inverted-stack/docs/reference-implementation/design-decisions.md` §5 Volume 1 extensions item 48
- Failed-conditions for P7: `C:/Projects/the-inverted-stack/docs/reference-implementation/concept-index.yaml` P7 kill-triggers (key-loss recovery path inadequate)
- Existing kernel-security substrate: `packages/kernel-security/Crypto/Ed25519Signer.cs`, `Crypto/X25519KeyAgreement.cs`, `Keys/SqlCipherKeyDerivation.cs`, `Keys/KeystoreRootSeedProvider.cs`
- Phase 1 intake: `icm/00_intake/output/business-mvp-phase-1-foundation-intake-2026-04-26.md`
- Sister ADR 0044: Anchor ships Windows-only for Phase 1 (D1 outcome)
- External pattern references (per primitive #48): Argent wallet social recovery; Vitalik Buterin "Why we need wide adoption of social recovery wallets" (2021); Apple iCloud Keychain recovery; BIP-39 wordlist for paper-key

## Amendments (post-acceptance, 2026-04-30)

### A2 (REQUIRED) — `EncryptedField` + `IFieldDecryptor` field-level encryption substrate

**Driver:** ADR 0058 (Vendor Onboarding Posture) amendment A1 halt-condition. ADR 0058 takes a hard dependency on `Sunfish.Foundation.Recovery.EncryptedField` (value type wrapping ciphertext + nonce + key-version) and `Sunfish.Foundation.Recovery.IFieldDecryptor` (capability-checked, audit-emitting decrypt-on-read interface) for sensitive vendor PII (W-9 TIN/SSN/EIN). Verified 2026-04-30 via `git grep`: zero matches in `packages/`. ADR 0058 A1 explicitly halts W#18 Stage 06 build until these types ship as Accepted.

This amendment introduces the substrate as net-new types in `Sunfish.Foundation.Recovery`. Compounding consumer set beyond W#18: W#22 Leasing Pipeline (FCRA tenant SSN, banking detail records); W#23 iOS Field-Capture App (offline-stored sensitive PII before Bridge sync); ADR 0051 Payments (potentially — payment-method / card-on-file shape). Building it as substrate (not embedded in W#18 hand-off Phase 0) compounds value across these consumers and keeps separation-of-concerns clean.

**Rationale for placing in `foundation-recovery` (not a new package):** the per-tenant DEK is derived from the same root seed that drives `KeystoreRootSeedProvider` + `PaperKeyDerivation`. Placing the field-encryption substrate in `foundation-recovery` puts all root-seed-derived primitives in one package boundary and reuses the existing `kernel-security` reference. Alternative considered (new `foundation-field-encryption` package): rejected — the package would be three types deep and would duplicate `foundation-recovery`'s kernel-security reference.

#### A2.1 — `EncryptedField` value type

```csharp
namespace Sunfish.Foundation.Recovery;

/// <summary>
/// Opaque value type wrapping ciphertext encrypted with a per-tenant DEK
/// (derived from the keystore root seed). Compile-time impossible to access
/// the plaintext without going through <see cref="IFieldDecryptor"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a record struct (not a class):</b> the type is a pure
/// value envelope — equality is structural; no identity beyond the bytes
/// it carries. Compatible with EFCore <c>OwnsOne</c> + JSON serialization.
/// </para>
/// <para>
/// <b>Why no <c>Plaintext</c> property:</b> the only path from
/// <c>EncryptedField</c> to plaintext is <see cref="IFieldDecryptor.DecryptAsync"/>,
/// which requires a valid <see cref="IDecryptCapability"/> and emits an
/// <c>AuditEventType.FieldDecrypted</c> record on every successful decrypt.
/// Direct property access would bypass the audit emission AND the capability
/// check; both are load-bearing for ADR 0058's "structurally inaccessible"
/// claim about TINs.
/// </para>
/// <para>
/// <b>Crypto:</b> AES-256-GCM (per industry-best-practice defaults table —
/// AEAD; ciphertext + 16-byte authentication tag are concatenated in
/// <see cref="Ciphertext"/>; nonce is the 12-byte GCM IV). Key-version
/// is the version of the per-tenant DEK that produced the ciphertext;
/// rotation bumps the version + re-encrypts on access (lazy rotation).
/// </para>
/// </remarks>
public readonly record struct EncryptedField(
    ReadOnlyMemory<byte> Ciphertext,    // AES-GCM ciphertext + 16-byte tag (concatenated)
    ReadOnlyMemory<byte> Nonce,         // 12-byte GCM IV
    int KeyVersion);                    // per-tenant DEK version that encrypted this field

internal sealed class EncryptedFieldJsonConverter : JsonConverter<EncryptedField>
{
    // Serializes as: { "ct": "<base64url>", "nonce": "<base64url>", "kv": <int> }
    // Compact form chosen to keep JSON-payload audit logs readable;
    // base64url avoids `+` / `/` URL-encoding hazards.
}
```

**Storage shape:** consumers MAY store as a single binary blob (`Ciphertext + Nonce + KeyVersion` packed) OR as three separate columns. EFCore `OwnsOne<EncryptedField>` mapping is supported; XO recommends three columns (`*_ciphertext`, `*_nonce`, `*_key_version`) for queryability of key-version (for rotation sweeps) without DEK access.

#### A2.2 — `IFieldDecryptor` capability-checked decrypt-on-read interface

```csharp
namespace Sunfish.Foundation.Recovery;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

/// <summary>
/// Capability-checked, audit-emitting decryption gateway for
/// <see cref="EncryptedField"/>. Every decrypt attempt — successful or
/// denied — emits an audit record to <see cref="IAuditTrail"/>.
/// </summary>
public interface IFieldDecryptor
{
    /// <summary>
    /// Decrypts <paramref name="field"/> if <paramref name="capability"/>
    /// authorizes the actor + scope. Emits
    /// <c>AuditEventType.FieldDecrypted</c> on success and
    /// <c>AuditEventType.FieldDecryptionDenied</c> on rejection.
    /// </summary>
    /// <exception cref="FieldDecryptionDeniedException">
    /// Capability invalid, expired, out-of-scope, or revoked.
    /// </exception>
    Task<ReadOnlyMemory<byte>> DecryptAsync(
        EncryptedField field,
        IDecryptCapability capability,
        TenantId tenant,
        CancellationToken ct);
}

/// <summary>
/// Issuer-side companion to <see cref="IFieldDecryptor"/>. Encrypts plaintext
/// for a tenant; no capability required to encrypt (only to decrypt).
/// </summary>
public interface IFieldEncryptor
{
    Task<EncryptedField> EncryptAsync(
        ReadOnlyMemory<byte> plaintext,
        TenantId tenant,
        CancellationToken ct);
}

/// <summary>
/// Capability envelope for decrypt operations. Per ADR 0032 capability
/// delegation pattern (macaroon-bound). Phase 1 ships a
/// <c>FixedDecryptCapability</c> reference implementation; production
/// will swap in macaroon-derived capabilities.
/// </summary>
public interface IDecryptCapability
{
    /// <summary>Identifier for audit emission (capability_id key).</summary>
    string CapabilityId { get; }

    /// <summary>Actor whose capability this is (audit emission decrypted_by key).</summary>
    ActorId Actor { get; }

    /// <summary>Tenant the capability is scoped to.</summary>
    TenantId Tenant { get; }

    /// <summary>Validity check at decrypt time. Returns null if valid; rejection reason if not.</summary>
    string? ValidateForDecrypt(DateTimeOffset now);
}

public sealed class FieldDecryptionDeniedException : Exception
{
    public FieldDecryptionDeniedException(string capabilityId, string reason)
        : base($"Decrypt denied for capability {capabilityId}: {reason}") { }
}
```

**Reference implementations** (ship in this amendment):

- `RecoveryRootSeedFieldEncryptor : IFieldEncryptor` — derives per-tenant DEK from the keystore root seed via HKDF-SHA256 (info string `"sunfish-encrypted-field-v1"` + tenant ID); encrypts via AES-GCM (`System.Security.Cryptography.AesGcm`); embeds the current key-version
- `RecoveryRootSeedFieldDecryptor : IFieldDecryptor` — derives per-tenant DEK via HKDF (matching the encryptor); validates capability; emits audit; decrypts; returns plaintext
- `FixedDecryptCapability : IDecryptCapability` — Phase 1 reference impl; constructed from `(actor, tenant, validUntil)`; intended for tests + bootstrap scenarios; production callers issue macaroon-bound capabilities

#### A2.3 — Per-tenant DEK derivation

```
master_root_seed = KeystoreRootSeedProvider.GetSeed()    // 32-byte; existing primitive
per_tenant_dek_kV = HKDF-SHA256(
    ikm = master_root_seed,
    salt = TenantId.ToByteArray(),
    info = "sunfish-encrypted-field-v1|version=" + keyVersion,
    output_length = 32)
```

`keyVersion` is the current per-tenant DEK version, stored in `KeystoreRootSeedProvider` (existing `Foundation.Recovery` persistence) — not in `EncryptedField` records (those carry the version they were encrypted under). Rotation increments the per-tenant version; existing fields decrypt with their stored version; new encrypts use the latest version. Lazy re-encrypt on access.

**Rotation primitive:** `IFieldEncryptionKeyRotator.RotateAsync(TenantId tenant)` — bumps the per-tenant key version; emits `AuditEventType.FieldEncryptionKeyRotated`. Re-encrypt sweep is consumer-driven (not eager); a periodic job can scan rows where `key_version < current_version` and re-encrypt via `IFieldDecryptor.DecryptAsync` + `IFieldEncryptor.EncryptAsync`.

#### A2.4 — Audit emission shape

3 new `AuditEventType` constants in `packages/kernel-audit/AuditEventType.cs`:

```csharp
// Field-level encryption (Foundation.Recovery A2 per ADR 0046)
public static readonly AuditEventType FieldDecrypted = new("FieldDecrypted");
public static readonly AuditEventType FieldDecryptionDenied = new("FieldDecryptionDenied");
public static readonly AuditEventType FieldEncryptionKeyRotated = new("FieldEncryptionKeyRotated");
```

Payload-body schemas (per ADR 0049 + matching W#31 / W#19 / W#27 conventions):

```csharp
// FieldDecrypted (success)
new Dictionary<string, object?>
{
    ["capability_id"] = capability.CapabilityId,
    ["decrypted_by"] = capability.Actor.Value,
    ["tenant_id"] = tenant.Value,
    ["key_version"] = field.KeyVersion,
    // NO field_id key — caller emits a domain-specific audit if it wants
    // record-level attribution (e.g., the W#18 vendor row's TIN-decrypt
    // emits a separate VendorTinAccessed audit with the vendor_id);
    // this audit is ONLY about the cryptographic decrypt operation.
}

// FieldDecryptionDenied (rejection)
new Dictionary<string, object?>
{
    ["capability_id"] = capability?.CapabilityId ?? "<null-capability>",
    ["denied_by"] = capability?.Actor.Value ?? "<unknown-actor>",
    ["tenant_id"] = tenant.Value,
    ["denial_reason"] = reason,    // from IDecryptCapability.ValidateForDecrypt or "capability null"
}

// FieldEncryptionKeyRotated
new Dictionary<string, object?>
{
    ["tenant_id"] = tenant.Value,
    ["from_version"] = oldVersion,
    ["to_version"] = newVersion,
    ["rotated_by"] = actor.Value,
}
```

Factory methods land in `packages/foundation-recovery/Audit/FieldEncryptionAuditPayloads.cs` matching the established `TaxonomyAuditPayloads` pattern from W#31 (PR #263).

#### A2.5 — DI registration

Extends existing `AddSunfishRecoveryCoordinator()` (no new top-level DI extension):

```csharp
public static IServiceCollection AddSunfishRecoveryCoordinator(this IServiceCollection services)
{
    // ... existing recovery-coordinator registrations ...

    // Field-encryption substrate (A2)
    services.AddSingleton<IFieldEncryptor, RecoveryRootSeedFieldEncryptor>();
    services.AddSingleton<IFieldDecryptor, RecoveryRootSeedFieldDecryptor>();
    services.AddSingleton<IFieldEncryptionKeyRotator, RecoveryRootSeedFieldEncryptionKeyRotator>();

    return services;
}
```

#### A2.6 — Acceptance criteria for A2 implementation PR

- [ ] `EncryptedField` record struct + JSON converter in `packages/foundation-recovery/EncryptedField.cs`
- [ ] `IFieldEncryptor` + `IFieldDecryptor` + `IDecryptCapability` interfaces in `packages/foundation-recovery/Crypto/`
- [ ] `RecoveryRootSeedFieldEncryptor` + `RecoveryRootSeedFieldDecryptor` implementations
- [ ] `FixedDecryptCapability` reference impl
- [ ] `IFieldEncryptionKeyRotator` + `RecoveryRootSeedFieldEncryptionKeyRotator`
- [ ] `FieldDecryptionDeniedException`
- [ ] 3 `AuditEventType` constants added to `kernel-audit/AuditEventType.cs`
- [ ] `FieldEncryptionAuditPayloads` factory class with 3 methods
- [ ] DI registration extended on `AddSunfishRecoveryCoordinator()`
- [ ] Tests (target ~20-25):
  - [ ] Round-trip: encrypt + decrypt yields original plaintext
  - [ ] Capability invalid/expired/wrong-tenant: throws + emits denied-audit
  - [ ] Different tenants get different ciphertext for same plaintext (DEK is tenant-scoped)
  - [ ] Different key-versions decrypt correctly (rotation backward-compat)
  - [ ] Audit emission shape: keys + values match A2.4 schema
  - [ ] JSON serialization round-trip
  - [ ] EFCore OwnsOne mapping smoke test (in-memory provider)

#### A2.7 — Cited-symbol verification (Decision Discipline Rule 6)

Per the verify-symbols-before-asserting-AP21-clean rule, every `Sunfish.*` symbol referenced in this amendment is one of:

- **Existing** (verified 2026-04-30 via `git grep`): `Sunfish.Foundation.Assets.Common.ActorId` (packages/foundation/Assets/Common/ActorId.cs); `Sunfish.Foundation.MultiTenancy.TenantId` (packages/foundation-multitenancy/); `Sunfish.Kernel.Audit.AuditEventType` + `AuditPayload` + `AuditRecord` + `IAuditTrail` (packages/kernel-audit/); `KeystoreRootSeedProvider` (packages/kernel-security/Keys/); `PaperKeyDerivation` (packages/foundation-recovery/)
- **Introduced by this amendment**: `EncryptedField`, `IFieldEncryptor`, `IFieldDecryptor`, `IDecryptCapability`, `FieldDecryptionDeniedException`, `FixedDecryptCapability`, `RecoveryRootSeedFieldEncryptor`, `RecoveryRootSeedFieldDecryptor`, `IFieldEncryptionKeyRotator`, `RecoveryRootSeedFieldEncryptionKeyRotator`, `FieldEncryptionAuditPayloads`, `AuditEventType.FieldDecrypted`, `AuditEventType.FieldDecryptionDenied`, `AuditEventType.FieldEncryptionKeyRotated`

#### A2.8 — Threat model (security & privacy)

**In scope:**
- Decrypt without capability → throws + audited
- Plaintext leakage via direct property access on `EncryptedField` → impossible (no Plaintext property; no public byte-array exposure of the DEK)
- Cross-tenant decrypt → impossible (DEK is tenant-scoped via HKDF salt)
- Stolen ciphertext at rest without root seed → undecryptable (AES-256-GCM authenticated encryption)
- DEK rotation while ciphertexts still reference old key-version → handled (lazy re-encrypt; old version derivable from same root seed + version field)

**Out of scope (deferred to post-MVP per ADR 0046's general post-MVP frame):**
- Hardware-backed DEK storage (Secure Enclave / TPM) — Phase 1 root seed lives in `KeystoreRootSeedProvider`'s existing storage; A2 inherits its security profile
- Per-record DEK (vs per-tenant) — would require keying material persisted per row; Phase 1 chooses per-tenant for storage simplicity
- Forward secrecy across rotations — Phase 1 keeps old DEK derivable from root seed + version; a future rotation primitive could destroy old DEKs after re-encrypt sweep completes (out of A2 scope)
- Macaroon-derived capabilities — `FixedDecryptCapability` is Phase 1; full macaroon integration is ADR 0032 work

#### A2.9 — W#18 unblock

Once this A2 implementation lands, ADR 0058 amendment A1's halt-condition is satisfied:

> Stage 06 build does not start until the substrate types ship as Accepted (in either ADR 0046-A2 or Stage 06 Phase 0).

XO can then author the W#18 Vendor Onboarding Posture Stage 06 hand-off referencing the now-shipped `EncryptedField` + `IFieldDecryptor` types. W#18 row in `active-workstreams.md` flips from `design-in-flight (ADR Accepted; hand-off pending)` to `ready-to-build` on hand-off authoring.

#### A2.10 — Open questions

- **OQ-A2.1:** should `EncryptedField` carry a `Sunfish.Foundation.Taxonomy.TaxonomyClassification` for "what kind of PII this is" (TIN / SSN / EIN / DL / etc.)? Rationale: enables jurisdiction-policy enforcement on decrypt (e.g., "only decrypt SSN if capability has `decrypt:ssn` scope"). **XO recommendation: defer.** Phase 1 ships scope-agnostic decrypt; capability scope is the only gate. ADR 0057 leasing-pipeline can add a typed wrapper if FCRA enforcement requires it.
- **OQ-A2.2:** does `IFieldDecryptor` need an async-streaming variant for large fields (e.g., file attachments)? **XO recommendation: defer.** Phase 1 targets short-string fields (TIN, SSN, account numbers). Large encrypted blobs are a separate substrate; not in A2 scope.
- **OQ-A2.3:** per-record DEKs (rotate one record's DEK without rotating tenant DEK)? **XO recommendation: defer to post-MVP.** Phase 1 per-tenant DEK is sufficient for the named consumer set.

#### A2.11 — Compatibility & migration

Net-new types; zero existing callers. No migration needed. Existing recovery-coordinator + paper-key + trustee surface is untouched. The amendment adds DI registrations + new audit constants but does not modify any existing API.

### A3 (REQUIRED, mechanical) — A2 council-review fixes (cited-symbol drift; capability scope)

**Driver:** Stage 1.5 council review of A2 (`icm/07_review/output/adr-audits/0046-A2-council-review-2026-04-30.md`, dated 2026-04-30) found 2 Critical + 3 Major + 2 Minor + 1 Encouraged. This A3 amendment applies only the mechanical fixes (rename / fix-citation / scope-tightening per Decision Discipline Rule 3). Two business-judgment items (relationship to existing `ITenantKeyProvider`; rotation-store persistence shape) are flagged for CO escalation and remain open.

#### A3.1 — `TenantId` namespace correction (council F1)

A2.2 imported `using Sunfish.Foundation.MultiTenancy;` for `TenantId`; A2.7 cited it at the same namespace. Corrected: `TenantId` lives at `packages/foundation/Assets/Common/TenantId.cs` in namespace `Sunfish.Foundation.Assets.Common` (same namespace as `ActorId`). The `foundation-multitenancy` package consumes it via `using Sunfish.Foundation.Assets.Common;` (see `ITenantScoped.cs`); it does not redefine it.

A2.2 `using` block (canonical):

```csharp
namespace Sunfish.Foundation.Recovery;

using Sunfish.Foundation.Assets.Common;       // TenantId, ActorId
using Sunfish.Foundation.MultiTenancy;        // (only if ITenantScoped is referenced; not needed for A2 surface)
```

A2.7 "Existing (verified)" entry corrected: `Sunfish.Foundation.Assets.Common.TenantId (packages/foundation/Assets/Common/TenantId.cs)`.

#### A3.2 — `IRootSeedProvider` API signature correction (council F3)

A2.3 pseudo-code line `master_root_seed = KeystoreRootSeedProvider.GetSeed()` corrected:

```text
master_root_seed = await rootSeedProvider.GetRootSeedAsync(ct)    // 32-byte; existing primitive
                                                                  // IRootSeedProvider — kernel-security
```

Consumers inject `Sunfish.Kernel.Security.Keys.IRootSeedProvider` (interface) — `KeystoreRootSeedProvider` is the production implementation but the substrate codes against the interface for testability. Hosts must register `AddSunfishKernelSecurity()` before `AddSunfishRecoveryCoordinator()` (already implied by the existing `AddSunfishRecoveryCoordinator` XML doc remarks).

#### A3.3 — `IDecryptCapability.ValidateForDecrypt` scope-check (council F7)

A2.2's `IDecryptCapability.ValidateForDecrypt(DateTimeOffset now)` did not take the target tenant; cross-tenant prevention fell to a belt-and-suspenders check inside the decryptor. To make the cross-tenant guarantee structural (mechanically enforced by the capability, not by decryptor discipline), the validate signature takes the target tenant:

```csharp
public interface IDecryptCapability
{
    string CapabilityId { get; }
    ActorId Actor { get; }
    TenantId Tenant { get; }

    /// <summary>
    /// Validity check at decrypt time. Returns null if the capability authorizes
    /// the decrypt; rejection reason ("expired", "wrong-tenant", "revoked", ...)
    /// otherwise. <paramref name="targetTenant"/> is the tenant the caller is
    /// attempting to decrypt for; the capability rejects if it doesn't match
    /// <see cref="Tenant"/>.
    /// </summary>
    string? ValidateForDecrypt(TenantId targetTenant, DateTimeOffset now);
}
```

`FixedDecryptCapability.ValidateForDecrypt(targetTenant, now)` returns `"wrong-tenant"` if `targetTenant != Tenant`, `"expired"` if `now > validUntil`, else null.

A2.6 acceptance criteria gains: `[ ] Cross-tenant decrypt rejected by capability validate (NOT by decryptor field check)`.

#### A3.4 — Storage-shape recommendation: drop `OwnsOne` framing (council F6)

A2.1 asserted EFCore `OwnsOne<EncryptedField>` mapping is supported. EFCore's owned-type tracking does not trivially compose with `readonly record struct` containing `ReadOnlyMemory<byte>` properties (no settable members; ReadOnlyMemory<byte> requires a value converter). Corrected guidance:

> **Storage shape:** consumers store as three columns (`*_ciphertext`, `*_nonce`, `*_key_version`) for queryability of `key_version` (rotation sweeps) without DEK access. EFCore mapping uses three property mappings on the owning entity, NOT `OwnsOne` (the `readonly record struct` + `ReadOnlyMemory<byte>` shape doesn't trivially compose with EFCore's owned-type tracking). If a single-blob storage shape is needed (e.g., for a JSONB column), use the `EncryptedFieldJsonConverter` directly.

A2.6 acceptance criteria — replace `[ ] EFCore OwnsOne mapping smoke test (in-memory provider)` with `[ ] EFCore three-column property mapping round-trips a record carrying EncryptedField fields (in-memory provider)`.

#### A3.5 — Test count band + timing-attack threat-model note (council F8 + F9)

A2.6 test target raised from `~20-25` to `~30-40` to match the 11-net-new-types surface area. The 7 named test categories should each yield 3-5 cases (round-trip; capability rejection paths × 4; cross-tenant; multi-version × 3; audit-emission shape × 3; JSON round-trip × 3; EFCore three-column round-trip; key-version-store concurrency × 2; HMAC-tag-tampering rejection; nonce-uniqueness sanity).

A2.8 threat model "Out of scope" gains:

> - Constant-time validation of `IDecryptCapability.ValidateForDecrypt` denial paths — Phase 1 accepts that `denial_reason` distinguishes failure modes in audit logs and that timing differences between tenant-mismatch and AES-GCM tag-failure are observable to an attacker with sub-millisecond timing access. Audit-log access is itself privileged; mitigation is layered authorization, not constant-time crypto.

#### A3.6 — Items deferred for CO escalation (NOT applied in this A3)

The following council findings are business-judgment, not mechanical, and remain open pending CO ruling:

- **F2 — `ITenantKeyProvider` relationship.** The seam already exists at `packages/foundation-recovery/TenantKey/ITenantKeyProvider.cs` with the exact `DeriveKeyAsync(TenantId, string purpose, CancellationToken) → Task<ReadOnlyMemory<byte>>` signature; its XML docs cite `encrypted-field-aes` as a sample purpose label. A2 silently duplicates the per-tenant key derivation logic. CO must rule: consume the existing seam (purpose-label-encoded versioning), deprecate it, or document a rejection rationale.
- **F4 — Audit emission `SignedOperation<AuditPayload>` envelope.** `IAuditTrail.AppendAsync` algorithmically verifies the payload's Ed25519 envelope at the kernel boundary; A2.4's raw `Dictionary<string, object?>` body listing is incomplete. The decryptor needs an issuer-side signing key injected via DI; the issuer-vs-actor signing semantics need design. CO ruling: ship a substrate-issuer signer (new injection) or co-opt an existing signer (e.g., the recovery-coordinator's signer).
- **F5 — Rotation-store persistence.** A2.3 says the per-tenant key version is stored in `KeystoreRootSeedProvider` — incorrect (different package, different storage primitive). The Stage 06 hand-off introduces an `InMemoryFieldEncryptionKeyVersionStore` that loses state on restart, breaking previously-encrypted ciphertexts. CO ruling: durable store backed by SQLCipher, monotonic-from-ciphertext-only design, or explicit Phase 1.x deferral with halt-condition.

W#32 Stage 06 build remains paused until F2 + F4 + F5 are resolved.

### A4 (REQUIRED, substantive) — Resolve council F2/F4/F5

**Driver:** A3 §A3.6 deferred 3 non-mechanical findings from the A2 council review (PR #329). XO authors A4 per Decision Discipline Rule 3 (verdict was Accept-with-amendments; mechanical fixes auto-accept; non-mechanical findings need substantive resolution). A4 closes the W#32 `held` ledger state and unblocks Stage 06 build.

#### A4.1 — F2 resolution: consume existing `ITenantKeyProvider` seam

**Decision:** **Consume.** A2's `RecoveryRootSeedFieldEncryptor` and `RecoveryRootSeedFieldDecryptor` reference impls are revised to delegate per-tenant key derivation to the existing `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider` (W#20 Phase 0 stub at `packages/foundation-recovery/TenantKey/ITenantKeyProvider.cs`). The interface XML docs already cite `encrypted-field-aes` as the canonical purpose label — A4 makes this real.

**Rationale.** The existing seam exists for exactly this reason; duplicating the HKDF logic in A2 would have created two parallel paths to derive the same per-tenant key material, with the silent risk that one path mutated and the other didn't. Per `feedback_decision_discipline` Rule 7 (industry-best-practice defaults — "use existing Sunfish primitives before introducing new ones"), the consume-existing path is canonical.

**Revised reference impls** (replaces A2.2's `RecoveryRootSeedField*` types):

```csharp
namespace Sunfish.Foundation.Recovery.Crypto;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery.TenantKey;

/// <summary>
/// Tenant-key-provider-backed field encryptor. Per ADR 0046-A4.
/// Defers per-tenant key derivation to <see cref="ITenantKeyProvider"/>;
/// substrate is purpose-label-encoded versioning, NOT key-version-store.
/// </summary>
public sealed class TenantKeyProviderFieldEncryptor(ITenantKeyProvider tenantKeys) : IFieldEncryptor
{
    public async Task<EncryptedField> EncryptAsync(
        ReadOnlyMemory<byte> plaintext,
        TenantId tenant,
        CancellationToken ct)
    {
        // Phase 1: fixed key-version 1; rotation deferred per A4.3.
        const int keyVersion = 1;
        var purpose = $"encrypted-field-aes-v{keyVersion}";
        var dek = await tenantKeys.DeriveKeyAsync(tenant, purpose, ct).ConfigureAwait(false);

        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(dek.Span, tagSizeInBytes: 16);
        aes.Encrypt(nonce, plaintext.Span, ciphertext, tag);

        // Concatenate ciphertext || tag for storage; matches the A2.1 EncryptedField.Ciphertext shape.
        var packed = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, packed, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, packed, ciphertext.Length, tag.Length);

        return new EncryptedField(packed, nonce, keyVersion);
    }
}

public sealed class TenantKeyProviderFieldDecryptor(
    ITenantKeyProvider tenantKeys,
    IAuditTrail? auditTrail = null,
    IOperationSigner? signer = null,
    IRecoveryClock? clock = null) : IFieldDecryptor
{
    public async Task<ReadOnlyMemory<byte>> DecryptAsync(
        EncryptedField field,
        IDecryptCapability capability,
        TenantId tenant,
        CancellationToken ct)
    {
        var now = (clock ?? new SystemRecoveryClock()).UtcNow;

        // Capability check FIRST — never derive a key for a denied capability.
        var rejection = capability.ValidateForDecrypt(tenant, now);
        if (rejection is not null)
        {
            await EmitAuditAsync(
                AuditEventType.FieldDecryptionDenied,
                FieldEncryptionAuditPayloadFactory.DecryptionDenied(capability, tenant, rejection),
                tenant,
                ct).ConfigureAwait(false);
            throw new FieldDecryptionDeniedException(capability.CapabilityId, rejection);
        }

        var purpose = $"encrypted-field-aes-v{field.KeyVersion}";
        var dek = await tenantKeys.DeriveKeyAsync(tenant, purpose, ct).ConfigureAwait(false);

        var packed = field.Ciphertext.Span;
        if (packed.Length < 16)
            throw new FieldDecryptionDeniedException(capability.CapabilityId, "ciphertext too short");

        var ciphertextLen = packed.Length - 16;
        var ciphertext = packed[..ciphertextLen];
        var tag = packed[ciphertextLen..];
        var plaintext = new byte[ciphertextLen];

        using var aes = new AesGcm(dek.Span, tagSizeInBytes: 16);
        try
        {
            aes.Decrypt(field.Nonce.Span, ciphertext, tag, plaintext);
        }
        catch (CryptographicException ex)
        {
            await EmitAuditAsync(
                AuditEventType.FieldDecryptionDenied,
                FieldEncryptionAuditPayloadFactory.DecryptionDenied(capability, tenant, $"crypto: {ex.Message}"),
                tenant,
                ct).ConfigureAwait(false);
            throw new FieldDecryptionDeniedException(capability.CapabilityId, $"AES-GCM tag verification failed");
        }

        await EmitAuditAsync(
            AuditEventType.FieldDecrypted,
            FieldEncryptionAuditPayloadFactory.Decrypted(capability, tenant, field.KeyVersion),
            tenant,
            ct).ConfigureAwait(false);

        return plaintext;
    }

    private async Task EmitAuditAsync(
        AuditEventType eventType,
        AuditPayload payload,
        TenantId tenant,
        CancellationToken ct)
    {
        // Audit emission optional — both null = audit disabled (test/bootstrap).
        if (_auditTrail is null || _signer is null) return;
        var occurredAt = clock?.UtcNow ?? DateTimeOffset.UtcNow;
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenant,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }
}
```

**Deletes:** `RecoveryRootSeedFieldEncryptor`, `RecoveryRootSeedFieldDecryptor`, `RecoveryRootSeedFieldEncryptionKeyRotator`, `IFieldEncryptionKeyVersionStore`, `InMemoryFieldEncryptionKeyVersionStore`, the manual HKDF derivation block in A2.3.

**Adds:** `TenantKeyProviderFieldEncryptor`, `TenantKeyProviderFieldDecryptor`, `FieldEncryptionAuditPayloadFactory` (replaces the static dictionary factories from A2.4 — see A4.2). DI registration in `AddSunfishRecoveryCoordinator()` is updated to register these implementations against `IFieldEncryptor` + `IFieldDecryptor`.

**A2.3 §"Per-tenant DEK derivation" supersedes:** delete the HKDF pseudo-code block; replace with one paragraph: "DEK derivation is delegated to `ITenantKeyProvider.DeriveKeyAsync(tenant, $"encrypted-field-aes-v{keyVersion}", ct)`. The implementing `ITenantKeyProvider` is responsible for deriving 32-byte tenant-scoped keys; reference implementations may use HKDF-SHA256 from the keystore root seed (`IRootSeedProvider.GetRootSeedAsync`) but A2/A4 do NOT prescribe the derivation function — substitution-friendly KMS impls are explicitly supported."

#### A4.2 — F4 resolution: audit emission via `IOperationSigner` + `AuditPayload`

**Decision:** Audit emission uses the canonical kernel-audit pattern proven in W#31 / W#19 / W#27 — `IOperationSigner.SignAsync(payload, occurredAt, Guid, ct)` produces the `SignedOperation<AuditPayload>` envelope; `IAuditTrail.AppendAsync(AuditRecord, ct)` accepts the wrapped record.

**Refactor of A2.4 audit factories:** the static `Dictionary<string, object?>`-returning methods are replaced with `AuditPayload`-returning methods on `FieldEncryptionAuditPayloadFactory`, mirroring `TaxonomyAuditPayloadFactory` (`packages/foundation-taxonomy/Audit/`):

```csharp
namespace Sunfish.Foundation.Recovery.Audit;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.Kernel.Audit;

internal static class FieldEncryptionAuditPayloadFactory
{
    public static AuditPayload Decrypted(IDecryptCapability capability, TenantId tenant, int keyVersion) =>
        new(new Dictionary<string, object?>
        {
            ["capability_id"] = capability.CapabilityId,
            ["decrypted_by"] = capability.Actor.Value,
            ["tenant_id"] = tenant.Value,
            ["key_version"] = keyVersion,
        });

    public static AuditPayload DecryptionDenied(IDecryptCapability? capability, TenantId tenant, string reason) =>
        new(new Dictionary<string, object?>
        {
            ["capability_id"] = capability?.CapabilityId ?? "<null-capability>",
            ["denied_by"] = capability?.Actor.Value ?? "<unknown-actor>",
            ["tenant_id"] = tenant.Value,
            ["denial_reason"] = reason,
        });

    // KeyRotated factory deferred per A4.3.
}
```

**DI shape:** `TenantKeyProviderFieldDecryptor` constructor takes `IAuditTrail?` + `IOperationSigner?` + `IRecoveryClock?` — all nullable (audit-emission-disabled is valid for tests + bootstrap). When both `IAuditTrail` AND `IOperationSigner` are null, audit emission silently no-ops. When one is null and the other is not, that's a configuration error caught at registration time (DI extension throws in this case). Matches `InMemoryTaxonomyRegistry`'s established pattern.

**Issuer key.** The `IOperationSigner` registered for the substrate is the host's substrate-issuer signer — typically the same `IOperationSigner` consumed by `RecoveryCoordinator` for trustee-attestation signing. Phase 1 reference impl (`Ed25519Signer`) takes a `KeyPair` constructor argument; production hosts inject a key derived from the keystore root seed. Cross-tenant: a single substrate-issuer key signs all field-decryption audit records (NOT per-tenant) — the audit `tenant_id` payload key carries tenant scope; the signature attests "this substrate emitted this record."

#### A4.3 — F5 resolution: defer rotation primitive; Phase 1 = fixed key-version 1

**Decision:** Phase 1 ships with **fixed key-version 1**. No rotation primitive in Phase 1. The `EncryptedField.KeyVersion` field is preserved (always 1 in Phase 1 ciphertext; the field exists so Phase 2 rotation can ship without breaking ciphertext shape).

**Rationale.** The `IFieldEncryptionKeyVersionStore` + `RecoveryRootSeedFieldEncryptionKeyRotator` from A2 introduce a durability problem A4 cannot solve in Phase 1 without significant scope creep (durable storage abstraction; SQLCipher integration; cross-process coordination). Council F5 is correct — losing version state breaks ciphertexts. Phase 1 punts the problem cleanly: there's only one key version, so version state is implicit.

**Deletes:** `IFieldEncryptionKeyRotator`, `RecoveryRootSeedFieldEncryptionKeyRotator`, `IFieldEncryptionKeyVersionStore`, `InMemoryFieldEncryptionKeyVersionStore`, `AuditEventType.FieldEncryptionKeyRotated`.

**Adds:** halt-condition in the W#32 hand-off: "If a Phase 1 caller needs to rotate keys (e.g., trustee-driven security event invalidates current DEK), HALT and beacon to research-inbox. A future ADR amendment will introduce the rotation primitive backed by durable storage."

**Note on `EncryptedField.KeyVersion`:** Field is retained at `int` (always 1 in Phase 1). When Phase 2 rotation amendment ships, decrypt path will accept the version from the ciphertext + look up the appropriate DEK. Forward-compatible.

#### A4.4 — Updated A2.6 acceptance criteria

Replaces A2.6 entirely:

- [ ] `EncryptedField` record struct + JSON converter (unchanged from A2.1)
- [ ] `IFieldEncryptor` + `IFieldDecryptor` + `IDecryptCapability` interfaces (A3.3-revised `ValidateForDecrypt` signature)
- [ ] `TenantKeyProviderFieldEncryptor` + `TenantKeyProviderFieldDecryptor` reference impls (A4.1)
- [ ] `FixedDecryptCapability` Phase 1 reference impl (unchanged from A2.2)
- [ ] `FieldDecryptionDeniedException` (unchanged from A2.2)
- [ ] 2 `AuditEventType` constants in `kernel-audit/AuditEventType.cs`: `FieldDecrypted`, `FieldDecryptionDenied` (NOT `FieldEncryptionKeyRotated` — deferred per A4.3)
- [ ] `FieldEncryptionAuditPayloadFactory` static class with 2 methods (A4.2)
- [ ] DI registration extended on `AddSunfishRecoveryCoordinator()` — registers `IFieldEncryptor` → `TenantKeyProviderFieldEncryptor` + `IFieldDecryptor` → `TenantKeyProviderFieldDecryptor`; consumes existing `ITenantKeyProvider` registration; throws at startup if `IAuditTrail` is registered without `IOperationSigner` (or vice versa)
- [ ] **NO `IFieldEncryptionKeyRotator` / `IFieldEncryptionKeyVersionStore` / `InMemoryFieldEncryptionKeyVersionStore`** — deferred per A4.3
- [ ] Tests (target ~30-40):
  - [ ] Round-trip: encrypt + decrypt yields original plaintext (with `IDecryptCapability` valid for tenant + actor)
  - [ ] Capability rejection paths: expired, wrong-tenant, null capability — all throw `FieldDecryptionDeniedException` AND emit `FieldDecryptionDenied` audit
  - [ ] Different tenants get different ciphertext for same plaintext (verifies `ITenantKeyProvider` is tenant-scoped)
  - [ ] HMAC-tag-tampering: corrupting ciphertext or nonce throws + emits denied audit
  - [ ] AES-GCM nonce uniqueness sanity (random per encrypt)
  - [ ] Audit emission shape: `FieldDecrypted` payload-body keys match A4.2 schema (snapshot test on alphabetized keys)
  - [ ] Audit emission can be disabled: passing `auditTrail: null, signer: null` to decryptor → no audit emission, no exception
  - [ ] Audit emission misconfiguration: passing exactly one of `(auditTrail, signer)` → DI extension throws at startup
  - [ ] JSON serialization round-trip on `EncryptedField`
  - [ ] EFCore three-column property mapping (per A3.4) round-trips a record carrying EncryptedField fields (in-memory provider)

#### A4.5 — Threat model amendments

**Updated A2.8 "In scope":**
- Decrypt without capability → throws + audited (unchanged)
- Plaintext leakage via direct `EncryptedField` property access → impossible (unchanged)
- Cross-tenant decrypt → impossible — `IDecryptCapability.ValidateForDecrypt(targetTenant, now)` rejects mismatched tenants STRUCTURALLY (per A3.3); per-tenant DEK from `ITenantKeyProvider` provides defense-in-depth
- Stolen ciphertext at rest without root seed → undecryptable (unchanged)
- DEK rotation with stale ciphertexts → **N/A in Phase 1** (fixed key-version 1; rotation deferred per A4.3)

**Updated A2.8 "Out of scope":**
- Hardware-backed DEK storage (unchanged)
- Per-record DEK (unchanged)
- Forward secrecy across rotations (unchanged; deferred with rotation per A4.3)
- Macaroon-derived capabilities (unchanged)
- Constant-time `ValidateForDecrypt` (unchanged from A3.5)
- **NEW:** key-rotation primitive — Phase 1 has none. Phase 2 amendment ships rotation backed by durable storage; halt-condition in W#32 hand-off names this explicitly.

#### A4.6 — Updated DI extension

`AddSunfishRecoveryCoordinator()` registers:

```csharp
// Existing registrations unchanged ...

// Field-encryption substrate (A2 + A3 + A4)
services.AddSingleton<IFieldEncryptor, TenantKeyProviderFieldEncryptor>();
services.AddSingleton<IFieldDecryptor, TenantKeyProviderFieldDecryptor>();

// Validate audit-emission DI consistency at startup.
// (Equivalent to InMemoryTaxonomyRegistry's pattern — both auditTrail + signer
// or neither; mid-state is a misconfiguration.)
services.AddOptions<RecoveryCoordinatorOptions>()
    .Validate(_ =>
    {
        var hasAudit = services.Any(d => d.ServiceType == typeof(IAuditTrail));
        var hasSigner = services.Any(d => d.ServiceType == typeof(IOperationSigner));
        return hasAudit == hasSigner;
    }, "Field-encryption substrate requires both IAuditTrail and IOperationSigner registered together, or neither.");
```

#### A4.7 — Cited-symbol verification (Decision Discipline Rule 6 — re-run for A4)

All `Sunfish.*` symbols cited in A4 are one of:

- **Existing on origin/main** (verified 2026-04-30 via `git grep`):
  - `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider` (`packages/foundation-recovery/TenantKey/ITenantKeyProvider.cs`)
  - `Sunfish.Foundation.Crypto.IOperationSigner` (`packages/foundation/Crypto/IOperationSigner.cs`)
  - `Sunfish.Foundation.Crypto.Ed25519Signer` (`packages/foundation/Crypto/Ed25519Signer.cs`)
  - `Sunfish.Foundation.Crypto.SignedOperation<T>` (`packages/foundation/Crypto/SignedOperation.cs`)
  - `Sunfish.Foundation.Assets.Common.TenantId` + `ActorId` (verified A3.1)
  - `Sunfish.Kernel.Audit.AuditPayload`, `AuditEventType`, `AuditRecord`, `IAuditTrail`, `AttestingSignature`
  - `Sunfish.Foundation.Recovery.IRecoveryClock`, `SystemRecoveryClock` (existing in `packages/foundation-recovery/`)
  - `IRootSeedProvider.GetRootSeedAsync` (verified A3.2)
- **Introduced by A2 or A3 (still introduced; not yet shipped to packages):** `EncryptedField`, `IFieldEncryptor`, `IFieldDecryptor`, `IDecryptCapability`, `FixedDecryptCapability`, `FieldDecryptionDeniedException`, `FieldEncryptionAuditPayloadFactory`, `AuditEventType.FieldDecrypted`, `AuditEventType.FieldDecryptionDenied`
- **Introduced by A4 (this amendment):** `TenantKeyProviderFieldEncryptor`, `TenantKeyProviderFieldDecryptor`
- **Removed by A4** (deleted from A2 spec): `RecoveryRootSeedFieldEncryptor`, `RecoveryRootSeedFieldDecryptor`, `RecoveryRootSeedFieldEncryptionKeyRotator`, `IFieldEncryptionKeyRotator`, `IFieldEncryptionKeyVersionStore`, `InMemoryFieldEncryptionKeyVersionStore`, `AuditEventType.FieldEncryptionKeyRotated`

Per the cohort lesson reaffirmed in `feedback_decision_discipline` Rule 6: A4 should run a Stage 1.5 council review BEFORE merge (not after) — the cohort pattern is now 6-of-6 substrate ADRs needing post-acceptance amendments, and A2 was the merge-without-review case study. XO will dispatch a council subagent on A4 before flipping W#32 row from `held` back to `ready-to-build`.

#### A4.8 — W#32 hand-off impact

The existing W#32 hand-off (`icm/_state/handoffs/adr-0046-a2-encrypted-field-stage06-handoff.md`) requires updating to reflect A4:

- Phase 2 file list: replace `RecoveryRootSeedField*` with `TenantKeyProviderField*`
- Phase 3 file list: drop `IFieldEncryptionKeyRotator` / `IFieldEncryptionKeyVersionStore` / rotation tests
- Phase 4 acceptance criteria: per A4.4
- Halt conditions: add "Phase 1 caller needs key rotation → halt + beacon"
- Phase total drops from ~3-5h to ~2-3h (rotation work removed)

XO authors a hand-off addendum after A4 council review + merge, then flips W#32 row from `held` to `ready-to-build`.

#### A4.9 — Open questions (deferred)

- **OQ-A4.1:** when Phase 2 rotation amendment ships, what's the durable-storage substrate for per-tenant key versions? Likely SQLCipher-backed (matches existing `IRecoveryStateStore` pattern). Out of scope for A4.
- **OQ-A4.2:** should the substrate-issuer `IOperationSigner` for audit signing be tenant-scoped or substrate-scoped? A4 specifies substrate-scoped (single key signs all tenants' field-decrypt audits). Tenant-scoped could be a Phase 2 option if compliance requires per-tenant audit attestation.
- **OQ-A4.3:** should `TenantKeyProviderFieldDecryptor` accept a custom `IAuditPayloadEnricher` for callers that want to add domain-specific keys (e.g., `vendor_id` for W#18 audits)? A4 says no — domain-specific audits emit a SEPARATE `VendorTinAccessed` (etc.) record per the W#18 contract. Field-encryption audits are crypto-only.

### A5 (REQUIRED, mechanical) — A4 council-review fixes

**Driver:** Stage 1.5 council review of A4 (`icm/07_review/output/adr-audits/0046-A4-council-review-2026-04-30.md`, dated 2026-04-30; PR #335) ran pre-merge per the cohort lesson — auto-merge on PR #333 was intentionally disabled to allow council to review A4 before it lands. Council found 3 Major + 1 Minor + 3 Encouraged. All 4 required + 3 encouraged are mechanical (per Decision Discipline Rule 3); A5 applies them and the cohort batting average updates to 7-of-7 substrate-amendment council reviews finding mechanical fixes.

#### A5.1 — F1 fix: explicit two-overload constructor (drop primary-constructor syntax)

A4.1's `TenantKeyProviderFieldDecryptor` mixed primary-constructor parameters with underscored-field references (`_auditTrail`, `_signer`), which doesn't compile (CS0103). Replaced with the canonical two-overload pattern proven in `InMemoryTaxonomyRegistry`:

```csharp
public sealed class TenantKeyProviderFieldDecryptor : IFieldDecryptor
{
    private readonly ITenantKeyProvider _tenantKeys;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly IRecoveryClock _clock;

    /// <summary>Audit-disabled overload (test/bootstrap).</summary>
    public TenantKeyProviderFieldDecryptor(ITenantKeyProvider tenantKeys, IRecoveryClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(tenantKeys);
        _tenantKeys = tenantKeys;
        _clock = clock ?? new SystemRecoveryClock();
    }

    /// <summary>Audit-enabled overload — both deps required (mid-state forbidden per A5.3).</summary>
    public TenantKeyProviderFieldDecryptor(
        ITenantKeyProvider tenantKeys,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        IRecoveryClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(tenantKeys);
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        _tenantKeys = tenantKeys;
        _auditTrail = auditTrail;
        _signer = signer;
        _clock = clock ?? new SystemRecoveryClock();
    }
    // ... DecryptAsync + EmitAuditAsync as A4.1, but using _tenantKeys / _auditTrail / _signer / _clock fields
}
```

`TenantKeyProviderFieldEncryptor` follows the same pattern (single constructor; no audit deps; just `ITenantKeyProvider` injection):

```csharp
public sealed class TenantKeyProviderFieldEncryptor : IFieldEncryptor
{
    private readonly ITenantKeyProvider _tenantKeys;

    public TenantKeyProviderFieldEncryptor(ITenantKeyProvider tenantKeys)
    {
        ArgumentNullException.ThrowIfNull(tenantKeys);
        _tenantKeys = tenantKeys;
    }
    // ... EncryptAsync as A4.1
}
```

The both-or-neither invariant is now structural at the type boundary — there's no overload for "audit-trail without signer" or vice versa. F3's DI-validation block (A4.6) is no longer needed; deleted in A5.3.

#### A5.2 — F2 fix: `IRecoveryClock.UtcNow` is a method

A4.1 called `clock?.UtcNow` (twice; property syntax). The actual interface (`packages/foundation-recovery/IRecoveryClock.cs`) declares `DateTimeOffset UtcNow()` — method. Fixed:

```csharp
// before:
var now = (clock ?? new SystemRecoveryClock()).UtcNow;
var occurredAt = clock?.UtcNow ?? DateTimeOffset.UtcNow;

// after (combined with A5.1 field promotion to _clock):
var now = _clock.UtcNow();
var occurredAt = _clock.UtcNow();
```

`_clock` is non-null after A5.1 (defaulted to `SystemRecoveryClock`), so the null-coalescing is no longer needed.

#### A5.3 — F3 fix: drop A4.6's DI Validate; rely on constructor-guard

A4.6's `services.AddOptions<RecoveryCoordinatorOptions>().Validate(...)` block runs at the wrong time (closure captures mutable `services`) and is semantically misplaced (registration-shape check rather than instantiation-time check). Per A5.1, the constructor itself enforces both-or-neither via overload selection + `ArgumentNullException.ThrowIfNull`. The `Validate` block is removed entirely.

Updated DI extension (replaces A4.6):

```csharp
public static IServiceCollection AddSunfishRecoveryCoordinator(this IServiceCollection services)
{
    // ... existing recovery-coordinator registrations ...

    // Field-encryption substrate (A2 + A3 + A4 + A5)
    services.TryAddSingleton<IFieldEncryptor, TenantKeyProviderFieldEncryptor>();
    services.TryAddSingleton<IFieldDecryptor>(sp =>
    {
        var tenantKeys = sp.GetRequiredService<ITenantKeyProvider>();
        var clock = sp.GetService<IRecoveryClock>();
        var auditTrail = sp.GetService<IAuditTrail>();
        var signer = sp.GetService<IOperationSigner>();

        // Both-or-neither invariant: structural via overload selection.
        // Mid-state (exactly one of audit/signer registered) is detected here
        // at first resolution — throws a clear configuration error rather than
        // letting an audit-disabled decryptor silently swallow audits.
        return (auditTrail, signer) switch
        {
            (null, null) => new TenantKeyProviderFieldDecryptor(tenantKeys, clock),
            (not null, not null) => new TenantKeyProviderFieldDecryptor(tenantKeys, auditTrail, signer, clock),
            _ => throw new InvalidOperationException(
                "Field-encryption decryptor requires both IAuditTrail and IOperationSigner registered, or neither. " +
                "Mid-state misconfiguration: " +
                $"IAuditTrail={(auditTrail is null ? "null" : "registered")}, " +
                $"IOperationSigner={(signer is null ? "null" : "registered")}.")
        };
    });

    return services;
}
```

The factory delegate runs at first `IFieldDecryptor` resolution against the built `IServiceProvider`, so it sees the actual registration shape and not a registration-time snapshot. Mid-state throws a clear `InvalidOperationException` at that point — per the canonical .NET DI pattern for "validate cross-service consistency at resolution."

#### A5.4 — F4 fix (Minor): drop `-v{n}` suffix in Phase 1 purpose label

A4.1 used `$"encrypted-field-aes-v{keyVersion}"`. Phase 1 always passes `keyVersion = 1`, so the actual purpose label is always `encrypted-field-aes-v1` — diverges from the `ITenantKeyProvider.DeriveKeyAsync` xmldoc example (`encrypted-field-aes`).

Phase 1 simplifies to the bare label:

```csharp
// In TenantKeyProviderFieldEncryptor.EncryptAsync:
const string purpose = "encrypted-field-aes";   // matches existing ITenantKeyProvider xmldoc
var dek = await _tenantKeys.DeriveKeyAsync(tenant, purpose, ct).ConfigureAwait(false);

// In TenantKeyProviderFieldDecryptor.DecryptAsync:
// field.KeyVersion is always 1 in Phase 1 ciphertext; A5.5 requires KeyVersion == 1 for decrypt.
const string purpose = "encrypted-field-aes";
var dek = await _tenantKeys.DeriveKeyAsync(tenant, purpose, ct).ConfigureAwait(false);
```

Phase 2 rotation amendment will introduce `$"encrypted-field-aes-v{keyVersion}"` only when there's a real `keyVersion >= 2`. Phase 1 ciphertexts (`KeyVersion = 1`) decrypt under the no-suffix label; Phase 2 ciphertexts (`KeyVersion >= 2`) decrypt under the `-v{n}` label. Forward-compatible without churn.

#### A5.5 — F5 fix (Encouraged): make Phase 1 encrypt/decrypt invariants explicit

Add to A4.3 (after "always 1 in Phase 1 ciphertext"):

> **Phase 1 invariants (explicit):**
> - **Encrypt:** `TenantKeyProviderFieldEncryptor.EncryptAsync` MUST write `KeyVersion = 1`. Phase 1 has no other supported version. The Phase 2 rotation amendment relaxes this to allow `KeyVersion = current_tenant_version`.
> - **Decrypt:** `TenantKeyProviderFieldDecryptor.DecryptAsync` accepts `KeyVersion >= 1` and selects purpose label per A5.4 (`KeyVersion = 1` → `encrypted-field-aes`; `KeyVersion >= 2` → `encrypted-field-aes-v{n}` post-Phase-2). Decrypting `KeyVersion = 0` or negative values throws `FieldDecryptionDeniedException` with reason `"unsupported key version"`.
> - **Rationale:** the asymmetry preserves forward compatibility — Phase 1 ciphertexts remain decryptable after Phase 2 rotation lands; Phase 2 ciphertexts cannot accidentally appear in a Phase 1 deployment because Phase 1 encrypt refuses to write them.

#### A5.6 — F6 fix (Encouraged): substrate-issuer key compromise blast radius in threat model

Add to A4.5 "Out of scope" (after "Macaroon-derived capabilities"):

> - **Substrate-issuer signing-key compromise** — Phase 1 signs all field-decryption audit records with a single substrate-issuer Ed25519 key. Compromise of that key permits forgery of audit records (`FieldDecrypted` / `FieldDecryptionDenied`) across all tenants this substrate serves; an attacker can forge legitimate-looking decrypts to mask actual access OR forge denials to suggest legitimate access was prevented. The blast radius is "all tenants this substrate serves" — same class as any foundation-tier signing-key compromise. Mitigation derives from the platform keystore protection of the underlying Ed25519 key + ADR 0049's planned algorithm-agility refactor (audit format `v0` → `v1`); per-tenant audit signing is rejected as a design option (key-management complexity exceeds the marginal isolation benefit). If forensic-grade per-tenant attestation is required for a future compliance regime, that triggers a follow-up ADR amendment, not a Phase 1 redesign.

#### A5.7 — F7 fix (Encouraged): A4.4 acceptance-criteria wording aligned with A5.3

A4.4's misconfiguration-test bullet read "Audit emission misconfiguration: passing exactly one of (auditTrail, signer) → DI extension throws at startup." Post-A5.3 the throw point is `IServiceProvider.GetRequiredService<IFieldDecryptor>()` (first resolution), not registration. Updated wording:

```text
- [ ] Audit emission misconfiguration: registering exactly one of (IAuditTrail, IOperationSigner) without the other → first resolution of IFieldDecryptor throws InvalidOperationException with a "mid-state misconfiguration" message naming which dep is missing
- [ ] Constructor guard: directly constructing TenantKeyProviderFieldDecryptor with a non-null IAuditTrail + null IOperationSigner (or vice versa) is impossible (overload selection prevents it; the parameter list doesn't admit the mid-state)
```

Both tests verify the same invariant from different angles (DI-resolution vs direct-construction). Both pass once A5.1 + A5.3 land.

#### A5.8 — Cited-symbol re-verification (mechanical)

A4.7's audit table is reaffirmed; the only drift the council flagged was `IRecoveryClock.UtcNow` (property vs method, F2 — fixed in A5.2). All other cited symbols verified-existing or properly classified introduced/removed. A5 introduces no new `Sunfish.*` symbol citations beyond what A4 already covered; the explicit-constructor pattern reuses the existing `ArgumentNullException.ThrowIfNull` convention from `InMemoryTaxonomyRegistry`.

#### A5.9 — Cohort batting average

Substrate-amendment cohort: **7-of-7 needing post-acceptance amendments after council review.** A4 ran council pre-merge per the lesson learned from A2; the pre-merge discipline caught all 4 required amendments before they shipped. **Cost of A4's pre-merge council:** zero `held` ledger states; zero W#32 build pauses for council. **Cost of A2's post-merge council (the case study):** PR #331 `held` ledger state for ~24h. Pre-merge council is now canonical for substrate amendments going forward — XO MUST disable auto-merge on substrate ADR amendments + dispatch council before flipping any downstream ledger row.

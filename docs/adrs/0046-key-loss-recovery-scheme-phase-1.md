# ADR 0046 — Key-loss recovery scheme for Business MVP Phase 1

**Status:** Accepted (2026-04-26; package-placement section added 2026-04-29; **A2 amendment landed 2026-04-30** — see §"Amendments (post-acceptance, 2026-04-30)")
**Date:** 2026-04-26 (Accepted) / 2026-04-29 (package-placement section) / 2026-04-30 (A2 amendment)
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

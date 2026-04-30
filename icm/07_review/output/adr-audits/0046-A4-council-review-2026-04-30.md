# ADR 0046 Amendment A4 (resolution of council F2/F4/F5) — Council Review

**Reviewer:** research session (XO; adversarial council, UPF Stage 1.5)
**Date:** 2026-04-30
**Subject:** ADR 0046 Amendment A4 (sub-amendments A4.1 — A4.9), pending merge via PR #333; auto-merge intentionally disabled so this council review can run pre-merge per the cohort lesson — "council before merge is canonical"
**Companion artifacts read:** A4 amendment text on branch `docs/adr-0046-a4-substantive`; ADR 0046 (post-A2/A3 baseline on `origin/main`); `packages/foundation-recovery/TenantKey/ITenantKeyProvider.cs`; `packages/foundation/Crypto/{IOperationSigner,Ed25519Signer,SignedOperation,IOperationVerifier}.cs`; `packages/kernel-audit/{AuditRecord,IAuditTrail,AuditEventType,AttestingSignature}.cs`; `packages/foundation-taxonomy/Services/InMemoryTaxonomyRegistry.cs` (canonical audit-emission pattern A4 cites); `packages/foundation-taxonomy/Audit/TaxonomyAuditPayloadFactory.cs` (canonical factory A4 mirrors); `packages/foundation-recovery/{IRecoveryClock,SystemRecoveryClock}.cs`; `packages/kernel-security/Keys/IRootSeedProvider.cs`; `packages/foundation/Assets/Common/TenantId.cs`; A2 council review at `icm/07_review/output/adr-audits/0046-A2-council-review-2026-04-30.md`.

**Council perspectives applied:** Crypto reviewer (attacker viewpoint over delegated key derivation + fixed key-version + nonce reuse + audit-issuer key blast radius); Security pessimist (rotation deferral is a vulnerability or only a feature gap?); Implementation pragmatist (cited-symbol shape + compilability of the code listings); AP-21 / cited-symbol auditor (every `Sunfish.*` symbol classified verified-existing / introduced-by-A4 / removed-by-A4).

---

## 1. Verdict

**Accept-with-amendments — grade B (low-B).** A4 closes the three substrate-level gaps the A2 council surfaced cleanly. **F2 resolution** (consume `ITenantKeyProvider`) is the correct call; the existing seam exists for exactly this purpose, the xmldoc literally cites `encrypted-field-aes` as a sample purpose label, and consume-existing matches Decision Discipline Rule 7 (industry-best-practice defaults — use existing primitives before inventing new ones). **F4 resolution** (audit emission via `IOperationSigner.SignAsync` → `SignedOperation<AuditPayload>` envelope; nullable DI both-or-neither) faithfully mirrors `InMemoryTaxonomyRegistry`, the canonical kernel-audit consumer. **F5 resolution** (defer rotation; Phase 1 = fixed key-version 1; halt-condition added) is a defensible scope reduction — losing-version-state is a worse failure mode than having no rotation, and Phase 1 audit trail will tell you you ever needed rotation if it ever fires.

**However** — the cohort pattern holds. A4 introduces three Major code-shape errors and one Minor purpose-label drift that a Stage 06 implementer copy-pasting the listings would hit at compile time:

- **F1 (Major) — primary-constructor / private-field mismatch.** A4.1's `TenantKeyProviderFieldDecryptor` uses primary-constructor syntax with positional params `auditTrail` + `signer` + `clock`, but the body references `_auditTrail`, `_signer` (underscored). Primary constructors don't generate `_x` fields — this is a CS0103 "name does not exist in current context" at compile.
- **F2 (Major) — `IRecoveryClock.UtcNow` is a method, not a property.** A4.1 emits `clock?.UtcNow ?? DateTimeOffset.UtcNow` (twice), which is property syntax. The actual interface declares `DateTimeOffset UtcNow()` — needs `clock?.UtcNow() ?? DateTimeOffset.UtcNow`. This is also a compile error against the existing API.
- **F3 (Major) — DI Validate closure references `services` after `services.AddOptions` returns.** A4.6's `Validate` lambda runs at options-resolution time and inspects `services` for registrations. But by then `services` is no longer being mutated — the closure captures a `services` whose state is whatever happened to be there when `Validate(...)` ran. The pattern is also semantically wrong (you can register `IAuditTrail` at any point, including AFTER `AddSunfishRecoveryCoordinator()`); the validator should run at host-startup time, not at coordinator-registration time. The both-or-neither check belongs in either a constructor guard on `TenantKeyProviderFieldDecryptor` (resolved deps from DI) or an `IValidateOptions<T>` that runs against the built `IServiceProvider`.
- **F4 (Minor) — purpose-label format drift.** `ITenantKeyProvider.DeriveKeyAsync` xmldoc cites `encrypted-field-aes` as the example purpose label. A4.1 uses `encrypted-field-aes-v{keyVersion}` (with `-v1` suffix). Phase 1 always passes `encrypted-field-aes-v1` — this works (different key from `encrypted-field-aes` because the purpose string differs, which is the point of HKDF purpose-binding) but it diverges from the documented example. Either update the xmldoc example to match A4 (mechanical) or drop the `-v{n}` suffix in Phase 1 since rotation is deferred (also mechanical, slightly better since "no version → no version" is the cleaner Phase 1 story). Encouraged.

Beyond the code-shape errors, three substantive findings warrant amendments — none are blockers, but each addresses a real gap:

- **F5 (Minor) — `EncryptedField.KeyVersion` retention rationale is forward-compatible only on the DECRYPT path; encryption MUST always write 1 in Phase 1.** A4.3 says "always 1 in Phase 1 ciphertext" but doesn't say what happens if a Phase 2 implementer, having shipped rotation, re-deploys against a Phase-1 substrate (e.g., during a downgrade or a rollback). Decryption should accept any historical `KeyVersion ≥ 1`; encryption should refuse to write anything other than 1 in Phase 1. State this explicitly. Encouraged.
- **F6 (Minor) — substrate-issuer signing-key compromise blast radius is undermodelled.** A4.2 specifies "single substrate-issuer key signs all field-decryption audit records (NOT per-tenant)." If that key is compromised, an attacker can forge audit records claiming arbitrary fields-were-decrypted (or were-not-decrypted) for any tenant. The Phase 1 threat model section A4.5 doesn't list this. The mitigation isn't to make it per-tenant (that explodes key-management surface); the mitigation is to add it to "Out of scope" with a defensible rationale: "audit-record authenticity in v0 derives from Ed25519 envelope signature; substrate-issuer key compromise is a substrate-level event with the same blast radius as any other foundation-tier signing-key compromise; rotation is handled by ADR 0049's planned algorithm-agility refactor (audit format `v0` → `v1`)." Encouraged.
- **F7 (Encouraged) — A4.4 acceptance criteria miss audit-emission misconfiguration test for an explicit case.** A4.4 lists "Audit emission misconfiguration: passing exactly one of (auditTrail, signer) → DI extension throws at startup." But A4.6's Validate scheme is broken (per F3); the ADR should describe the test in terms of **constructor-guard** behavior (e.g., `new TenantKeyProviderFieldDecryptor(tenantKeys, auditTrail: x, signer: null)` throws `ArgumentException`) once F3 is resolved. Once F3 lands the test rewrites itself; the encouraged amendment is to align A4.4 + A4.6 so they describe the same enforcement point.

**Cohort batting average update.** With A4, the substrate-amendment cohort is now **7-of-7 needing post-acceptance amendments after council review** (0046-A2, 0051, 0053, 0054, 0058, 0059, 0061, plus 0046-A4). The pattern is now "default expected behavior," not coincidence — XO should treat any non-mechanical substrate ADR amendment as council-required pre-merge, with auto-merge disabled until council clears.

---

## 2. Findings

### F1 (Major, AP-1 unvalidated assumption + AP-21 cited-symbol drift) — primary-constructor vs underscored-field mismatch in `TenantKeyProviderFieldDecryptor`

**Where:** A4.1 reference impl listing for `TenantKeyProviderFieldDecryptor`.

**Code listing (A4.1):**

```csharp
public sealed class TenantKeyProviderFieldDecryptor(
    ITenantKeyProvider tenantKeys,
    IAuditTrail? auditTrail = null,
    IOperationSigner? signer = null,
    IRecoveryClock? clock = null) : IFieldDecryptor
{
    // ...
    private async Task EmitAuditAsync(...)
    {
        // Audit emission optional — both null = audit disabled (test/bootstrap).
        if (_auditTrail is null || _signer is null) return;       // ← _auditTrail / _signer undefined
        var occurredAt = clock?.UtcNow ?? DateTimeOffset.UtcNow;   // ← see F2
        var signed = await _signer.SignAsync(...);                 // ← _signer undefined
        // ...
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);  // ← _auditTrail undefined
    }
}
```

**Reality (verified against `packages/foundation-taxonomy/Services/InMemoryTaxonomyRegistry.cs` on `origin/main`):** the canonical pattern A4 cites uses an **explicit two-overload constructor** with explicit `private readonly IAuditTrail? _auditTrail;` + `private readonly IOperationSigner? _signer;` fields:

```csharp
public sealed class InMemoryTaxonomyRegistry : ITaxonomyRegistry
{
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;

    public InMemoryTaxonomyRegistry() { }    // audit-disabled
    public InMemoryTaxonomyRegistry(IAuditTrail auditTrail, IOperationSigner signer)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        _auditTrail = auditTrail;
        _signer = signer;
    }
    // ...
}
```

A4's code mixes two patterns: it declares a primary constructor but accesses underscored fields that primary constructors don't generate. Primary-constructor parameters are accessed without underscore; if a private field is wanted, it must be explicitly declared and initialized. The Stage 06 implementer copy-pasting A4.1 will hit `CS0103: The name '_auditTrail' does not exist in the current context` on the first compile.

**Impact:** Stage 06 build immediately fails. The fix is mechanical — either drop the primary-constructor syntax and use the InMemoryTaxonomyRegistry-style two-overload explicit constructor (recommended; matches the canonical pattern A4 cites elsewhere), or drop the underscores in EmitAuditAsync. The first option preserves the both-must-be-supplied invariant via constructor `ThrowIfNull`; the second leaves the invariant unenforced at the type boundary.

**Severity:** Major. Pure copy-paste-fails-to-compile; not a design defect.

### F2 (Major, AP-21 cited-symbol drift) — `IRecoveryClock.UtcNow` is a method, A4 calls it as a property

**Where:** A4.1 reference impl, twice — `var now = (clock ?? new SystemRecoveryClock()).UtcNow;` and `var occurredAt = clock?.UtcNow ?? DateTimeOffset.UtcNow;`.

**Reality (`packages/foundation-recovery/IRecoveryClock.cs` on `origin/main`):**

```csharp
public interface IRecoveryClock
{
    /// <summary>The current UTC instant.</summary>
    DateTimeOffset UtcNow();   // ← method, not property
}

public sealed class SystemRecoveryClock : IRecoveryClock
{
    public DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;
}
```

**Impact:** another CS0103 / CS1955 at compile. The fix is mechanical: `(clock ?? new SystemRecoveryClock()).UtcNow()` — add the parens. (Alternatively, propose changing `IRecoveryClock` to expose `UtcNow` as a property — but that's a separate ADR refactor and out of scope for A4.)

**Severity:** Major. Same compile-failure class as F1.

### F3 (Major, AP-1 unvalidated assumption + AP-3 vague success criteria) — DI Validate closure runs at the wrong time and is semantically wrong

**Where:** A4.6 DI extension listing.

**Code listing (A4.6):**

```csharp
services.AddOptions<RecoveryCoordinatorOptions>()
    .Validate(_ =>
    {
        var hasAudit = services.Any(d => d.ServiceType == typeof(IAuditTrail));
        var hasSigner = services.Any(d => d.ServiceType == typeof(IOperationSigner));
        return hasAudit == hasSigner;
    }, "Field-encryption substrate requires both IAuditTrail and IOperationSigner registered together, or neither.");
```

**Two problems:**

1. **Closure timing.** `services` is mutable until host build. The lambda runs when `RecoveryCoordinatorOptions` is resolved (typically during host startup, after `services.Build()` in some flows but DEPENDS on the host configuration). If a downstream `services.AddSingleton<IAuditTrail, ...>()` registration runs AFTER `AddSunfishRecoveryCoordinator()` but BEFORE the options-validation pass, the validator may or may not see it depending on whether `IServiceCollection.Any()` is enumerated lazily. The pattern is fragile and host-order-dependent.

2. **Semantic mismatch.** The constraint is "if `TenantKeyProviderFieldDecryptor` is constructed with audit-emission enabled, both `IAuditTrail` AND `IOperationSigner` must be resolvable." The right enforcement point is **the decryptor's constructor** (DI resolves both, the decryptor's constructor either accepts both-non-null or accepts both-null and throws on mid-state) — NOT the service-collection registration. A constructor guard runs at instantiation time and uses the actual resolved values, not the registration shape. (`InMemoryTaxonomyRegistry` shows the right pattern: no DI-collection-poking; just two overloads with `ThrowIfNull` in the audit-enabled overload.)

**Impact:** the A4.6 listing as-drafted may pass tests where registration happens to be ordered correctly and fail in production where a host registers `IAuditTrail` after the recovery extension. The fix is mechanical:

- **Replace A4.6's `Validate(...)` block** with constructor-guard semantics: the audit-enabled overload of `TenantKeyProviderFieldDecryptor` requires both `IAuditTrail` and `IOperationSigner` (non-null); the audit-disabled overload requires neither. DI registration ships the audit-enabled overload by default and lets the host either register both audit deps or override to the audit-disabled overload explicitly.
- **OR** if a service-collection-level check is wanted, hoist the validator into an `IValidateOptions<T>` that runs against `IServiceProvider` post-build, with `services.AddSingleton<IValidateOptions<RecoveryCoordinatorOptions>, ...>()`.

**Severity:** Major. Not a compile failure but a runtime correctness gap that the council should not let merge.

### F4 (Minor, AP-1 / AP-21) — purpose-label drift between A4 and existing `ITenantKeyProvider` xmldoc example

**Where:** A4.1 specifies `var purpose = $"encrypted-field-aes-v{keyVersion}";` (with `-v{n}` suffix). The existing xmldoc on `ITenantKeyProvider.DeriveKeyAsync` (`packages/foundation-recovery/TenantKey/ITenantKeyProvider.cs:purpose param`) cites `encrypted-field-aes` (no version suffix) as a sample purpose label.

**Reality:** the xmldoc says "e.g.", which doesn't constrain — both labels are legal. But A4 introduces silent drift: anyone reading the xmldoc and the A4 spec sees two slightly different conventions. Future blocks adding new purpose labels will inherit the ambiguity.

**Two equally good fixes:**

- **(a)** Update the `ITenantKeyProvider.DeriveKeyAsync` xmldoc to cite `encrypted-field-aes-v{keyVersion}` as the canonical example. (Mechanical; one xmldoc edit.)
- **(b)** Drop the `-v{n}` suffix in Phase 1. Since Phase 1 always uses key-version 1, the purpose label can be `encrypted-field-aes` (matching the xmldoc); when Phase 2 rotation ships, the rotation amendment introduces the `-v{n}` suffix at that point. This has a forward-compatibility benefit: Phase 1 ciphertexts encrypted under purpose `encrypted-field-aes` would need decryption under the same label even after Phase 2 ships — meaning Phase 2 has to handle the no-suffix case as the legacy path, which is fine.

**Impact:** Low. Either fix is mechanical.

**Severity:** Minor / encouraged. Recommended fix: (b), since it preserves the existing xmldoc and lets Phase 2 introduce versioning when there's actually a v2.

### F5 (Minor) — Phase 1 encrypt-side write-version invariant is implicit, not stated

**Where:** A4.3 says "Phase 1 ships with **fixed key-version 1**" and "always 1 in Phase 1 ciphertext."

**Reality:** the encryptor writes 1 (per A4.1's `const int keyVersion = 1;`). But the ADR doesn't state the invariant explicitly: **Phase 1 encryption MUST refuse to write any `KeyVersion` other than 1**, and **Phase 1 decryption SHOULD accept any historical `KeyVersion ≥ 1`** (so that when Phase 2 rotation ships against an existing tenant and a downgrade-rollback to Phase 1 happens, the Phase 1 decryptor doesn't reject Phase-2-encrypted ciphertexts; it derives the right purpose label from the ciphertext-recorded `KeyVersion` and decrypts).

**Impact:** Low. The current A4.1 listing is correct as written, but a careless future Phase 2 amendment could accidentally allow Phase 1 encrypt with `keyVersion = 2` and break the forward-compatibility promise. Make the invariant explicit in A4.3.

**Severity:** Encouraged.

### F6 (Encouraged, AP-21 / threat-model gap) — substrate-issuer signing-key compromise blast radius is missing from threat model

**Where:** A4.5 threat-model amendment lists "In scope" + "Out of scope" but doesn't address the audit-issuer key.

**Reality:** A4.2 specifies "single substrate-issuer key signs all field-decryption audit records (NOT per-tenant)." If the substrate-issuer key is compromised:

- An attacker can forge `FieldDecrypted` audit records for any tenant with arbitrary `capability_id` and `decrypted_by` values, defeating the audit-trail's evidentiary value.
- An attacker can forge `FieldDecryptionDenied` records to make legitimate decryption attempts look denied (or vice versa).
- The blast radius is "all tenants this substrate serves" — same as any foundation-tier signing key.

**Mitigation considerations:**

- **NOT** per-tenant signing — that explodes key management.
- The mitigation is the same as for `RecoveryCoordinator`'s trustee-signing key: rotate via the platform keystore + algorithm-agility refactor (ADR 0049, audit format `v0` → `v1`).
- Explicitly note this in A4.5 "Out of scope" so future readers don't think the threat is unaddressed.

**Suggested addition to A4.5 "Out of scope":**

> - Substrate-issuer signing-key compromise — Phase 1 signs all field-decryption audits with a single substrate-issuer Ed25519 key. Compromise of that key permits forgery of audit records across all tenants this substrate serves. Mitigation derives from the platform keystore protection of the underlying Ed25519 key + ADR 0049's planned algorithm-agility refactor (audit format `v0` → `v1`); per-tenant signing is rejected as a design option (key-management complexity exceeds the marginal isolation benefit).

**Severity:** Encouraged.

### F7 (Encouraged, AP-3) — A4.4 vs A4.6 enforcement-point mismatch

**Where:** A4.4 lists test "Audit emission misconfiguration: passing exactly one of (auditTrail, signer) → DI extension throws at startup." A4.6 places the misconfiguration check in `services.AddOptions<RecoveryCoordinatorOptions>().Validate(...)`.

**Reality:** the test wording says "DI extension throws at startup" — but `Validate` lambdas don't throw at registration time, they throw at options-resolution time (typically when the first consumer pulls `IOptions<T>`). The behavior is consistent enough but the test wording is sloppy.

After F3 is resolved (constructor-guard pattern), the test rewrites cleanly: "Constructing `TenantKeyProviderFieldDecryptor` with exactly one of `(IAuditTrail, IOperationSigner)` non-null throws `ArgumentException`." This is a fix to A4.4 once F3 lands; the encouraged amendment is to align both sections under one enforcement point.

**Severity:** Encouraged. Resolves with F3.

---

## 3. AP-21 cited-symbol audit (per Decision Discipline Rule 6)

For each `Sunfish.*` symbol cited in A4, classify per A4.7's framework:

| Symbol | Cited in | A4.7 classification | Verified status |
|---|---|---|---|
| `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider` | A4.1, A4.7 | Existing | ✓ verified `packages/foundation-recovery/TenantKey/ITenantKeyProvider.cs` |
| `Sunfish.Foundation.Crypto.IOperationSigner` | A4.1, A4.2, A4.7 | Existing | ✓ verified `packages/foundation/Crypto/IOperationSigner.cs`; `SignAsync<T>(T, DateTimeOffset, Guid, CancellationToken)` matches A4 citation |
| `Sunfish.Foundation.Crypto.Ed25519Signer` | A4.2, A4.7 | Existing | ✓ verified `packages/foundation/Crypto/Ed25519Signer.cs`; implements `IOperationSigner` |
| `Sunfish.Foundation.Crypto.SignedOperation<T>` | A4.1, A4.2, A4.7 | Existing | ✓ verified `packages/foundation/Crypto/SignedOperation.cs` (record `(Payload, IssuerId, IssuedAt, Nonce, Signature)`) |
| `Sunfish.Foundation.Assets.Common.TenantId` | A4.1, A4.7 | Existing | ✓ verified `packages/foundation/Assets/Common/TenantId.cs` (per A3 fix from A2 council F1) |
| `Sunfish.Foundation.Assets.Common.ActorId` | implicit via `IDecryptCapability.Actor` | Existing | ✓ verified `packages/foundation/Assets/Common/ActorId.cs` |
| `Sunfish.Kernel.Audit.AuditPayload` | A4.1, A4.2, A4.7 | Existing | ✓ verified `packages/kernel-audit/AuditRecord.cs:73` |
| `Sunfish.Kernel.Audit.AuditEventType` | A4.1, A4.2, A4.4, A4.7 | Existing | ✓ verified `packages/kernel-audit/AuditEventType.cs` |
| `Sunfish.Kernel.Audit.AuditRecord` | A4.1, A4.7 | Existing | ✓ verified `packages/kernel-audit/AuditRecord.cs:49` |
| `Sunfish.Kernel.Audit.IAuditTrail` | A4.1, A4.6, A4.7 | Existing | ✓ verified `packages/kernel-audit/IAuditTrail.cs:39` (`AppendAsync` returns `ValueTask`, A4 awaits — works) |
| `Sunfish.Kernel.Audit.AttestingSignature` | A4.1, A4.7 | Existing | ✓ verified `packages/kernel-audit/AttestingSignature.cs` |
| `Sunfish.Foundation.Recovery.IRecoveryClock` | A4.1, A4.7 | Existing | **✗ DRIFT — `UtcNow` is a method, not a property** (F2) |
| `Sunfish.Foundation.Recovery.SystemRecoveryClock` | A4.1, A4.7 | Existing | ✓ verified `packages/foundation-recovery/SystemRecoveryClock.cs`; same `UtcNow()` method shape (drift inherited from F2) |
| `IRootSeedProvider.GetRootSeedAsync` | A4.1, A4.7 | Existing (per A3 fix) | ✓ verified `packages/kernel-security/Keys/IRootSeedProvider.cs` |
| `EncryptedField`, `IFieldEncryptor`, `IFieldDecryptor`, `IDecryptCapability`, `FixedDecryptCapability`, `FieldDecryptionDeniedException`, `AuditEventType.FieldDecrypted`, `AuditEventType.FieldDecryptionDenied` | A4.1, A4.4, A4.7 | Introduced by A2/A3 (still pending Stage 06) | ✓ classified correctly |
| `TenantKeyProviderFieldEncryptor`, `TenantKeyProviderFieldDecryptor` | A4.1, A4.7 | Introduced by A4 | ✓ classified correctly |
| `FieldEncryptionAuditPayloadFactory` | A4.1, A4.2, A4.7 | Introduced (A2 → revised by A4) | ✓ classified correctly |
| `RecoveryRootSeedFieldEncryptor`, `RecoveryRootSeedFieldDecryptor`, `RecoveryRootSeedFieldEncryptionKeyRotator`, `IFieldEncryptionKeyRotator`, `IFieldEncryptionKeyVersionStore`, `InMemoryFieldEncryptionKeyVersionStore`, `AuditEventType.FieldEncryptionKeyRotated` | A4.1, A4.3, A4.7 | Removed by A4 | ✓ classified correctly |

**1 cited-symbol drift** (`IRecoveryClock.UtcNow` property-vs-method) — flagged as F2. Significantly cleaner than A2 (which had 2 drifts + 1 ignored seam) and 0051/0053/0054/0058/0059/0061 (each with 2-4 drifts). A4's pre-merge cited-symbol-table discipline is largely working.

---

## 4. Council perspectives

### 4.1 Crypto reviewer

**Q: Is delegation to `ITenantKeyProvider` correct from an attacker's perspective?**

A: Yes. The seam is structurally clean. `ITenantKeyProvider.DeriveKeyAsync(tenant, purpose, ct)` returns 32 bytes; the implementation (`InMemoryTenantKeyProvider`, future SQLCipher / KMS impls) is responsible for HKDF-binding. An attacker who steals ciphertext-at-rest cannot decrypt without the tenant-scoped DEK; an attacker who compromises one tenant's DEK doesn't gain anything for other tenants (HKDF purpose-binding ensures cross-tenant DEK independence). The seam supports KMS substitution (Wave 2 / Phase 2) without ADR rework. **Pass.**

**Q: Does Phase-1 fixed-key-version create cryptographic issues?**

A: Marginal but acceptable. AES-256-GCM with random 96-bit nonces has a nonce-collision birthday bound around 2^48 encryptions before collision probability becomes non-negligible. For the Phase 1 scope (per-tenant DEK encrypts vendor TINs / occasional field encrypts), reaching 2^48 encryptions per tenant is implausible in any reasonable timeline — the same tenant DEK encrypting "forever" is fine for years. The one caveat: if a single tenant has a high-volume field-encryption workload (millions of encrypts/sec, e.g., a per-event audit-log encrypted-by-default scheme that doesn't yet exist in Phase 1), 2^48 starts to become a 10-year horizon. Document this implicitly in F5's "Phase 2 rotation halt-condition" — high-volume tenants are exactly the case that triggers Phase 2 rotation. **Pass with note.**

**Q: Is `EncryptedField.KeyVersion` actually needed if Phase 1 always uses 1?**

A: Yes — for forward compatibility. Removing it would lock the ciphertext format to v1 forever and require a format migration when rotation ships. Keeping it as a fixed-1 field in Phase 1 is the right call. F5's encouraged amendment formalizes the encrypt-side invariant (Phase 1 MUST write 1; Phase 1+ MAY decrypt any version ≥ 1). **Pass.**

**Q: Are there nonce-reuse risks across the per-tenant DEK boundary?**

A: No, given AES-GCM's 96-bit random nonces and the per-tenant DEK derivation. Two tenants encrypting the same plaintext will get different nonces (random) and different DEKs (purpose-bound HKDF) — even if both nonces collided (cryptographically negligible given 96-bit random) the DEKs differ, so the ciphertexts are independent. **Pass.**

### 4.2 Security pessimist

**Q: Does removing the rotation primitive create a real vulnerability if a tenant DEK is compromised?**

A: Yes, but the alternative is worse. If Phase 1 had A2's `IFieldEncryptionKeyRotator` + in-memory `IFieldEncryptionKeyVersionStore`, a process restart would lose the version state and break all previously-encrypted ciphertexts — that's a guaranteed P0 data-loss event. Phase 1 with no rotation has the property that ciphertexts always remain decryptable; the cost is "if a tenant DEK is compromised, ciphertexts encrypted under it cannot be made undecryptable without re-encrypting them all." That's a containable manual-recovery path (re-encrypt all affected fields under a new DEK; one-time event), NOT a substrate-correctness hole. The halt-condition in W#32 hand-off names exactly when a Phase 1 caller has hit this case. **Acceptable for Phase 1.**

**Q: Is "Phase 1 = fixed-version" defensible for production data?**

A: Yes — for **Phase 1's scope**. Phase 1 production data is W#18 vendor TINs (limited count, sensitive but small surface area, manual re-encrypt feasible if compromise occurs) and a few similar small-surface-area encrypted fields. It's NOT defensible for high-volume encrypt-by-default substrates that would land in Phase 2 (encrypted audit logs, encrypted ledger contents, encrypted ARK envelopes). The halt-condition is the right firewall. **Pass with explicit-scope note.**

**Q: Substrate-issuer signing-key compromise blast radius?**

A: All-tenants-this-substrate-serves, as I called out in F6. Mitigations:

1. The substrate-issuer key is foundational to all `RecoveryCoordinator` audit emission, not just field-encryption audits — the blast radius is the same as compromising any foundation-tier Ed25519 signing key.
2. The platform keystore (DPAPI on Windows; pending macOS Keychain / Linux libsecret) protects the underlying key material at rest.
3. ADR 0049's `v0` → `v1` algorithm-agility refactor is the canonical rotation path; rolling forward to a new audit format with a new issuer key is the canonical mitigation.

This is fine for Phase 1. F6's encouraged amendment adds it to "Out of scope" with this rationale. **Acceptable for Phase 1.**

### 4.3 Implementation pragmatist

**Q: Are the cited symbols actually existing?**

A: Yes for all 11 verified-existing claims (per §3 table). One drift on `IRecoveryClock.UtcNow` (F2). One xmldoc-purpose-label divergence (F4). All other classifications correct. **Pass with F2 + F4 amendments.**

**Q: Is `IOperationSigner.SignAsync(payload, occurredAt, Guid, ct)` actually the right signature?**

A: Yes — `ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default)`. A4's calls match this signature. The variable name `occurredAt` in A4 vs `issuedAt` in the interface is harmless (semantically equivalent at the call site). **Pass.**

**Q: Does `Ed25519Signer` actually implement `IOperationSigner`?**

A: Yes — `public sealed class Ed25519Signer(KeyPair keyPair) : IOperationSigner`. **Pass.**

**Q: Does `InMemoryTaxonomyRegistry`'s audit-emission code shape match what A4.1 specifies?**

A: Materially yes — both use a private `EmitAsync` helper that early-returns when `_auditTrail` or `_signer` is null, calls `_signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct)`, builds an `AuditRecord(AuditId: Guid.NewGuid(), TenantId: t, EventType: e, OccurredAt: o, Payload: signed, AttestingSignatures: ImmutableArray<AttestingSignature>.Empty)`, and calls `_auditTrail.AppendAsync(record, ct)`. **But** A4's reference impl uses primary-constructor syntax that omits the explicit field declarations — F1 is the gap.

**Q: Will the listings as-drafted compile?**

A: **No** — F1 (CS0103 on `_auditTrail`/`_signer`), F2 (`UtcNow` property/method), and F3 (Validate-on-services-collection semantic gap) all need fixes before Stage 06 build can use them. All three fixes are mechanical (15 minutes of editing).

### 4.4 AP-21 / cited-symbol auditor

Done in §3. Net: 1 drift (F2), 1 minor purpose-label divergence (F4), 0 ignored existing seams (F2 from A2 is now correctly consumed). Significantly tighter than A2.

---

## 5. Top 3 risks

1. **Stage 06 implementer copy-pastes A4.1, hits CS0103 on first compile, and either rewrites it ad-hoc (drifting from the canonical InMemoryTaxonomyRegistry pattern A4 cites) or stops and beacons to research.** Fix is mechanical (F1 + F2). Cohort risk: history shows implementers rarely match the canonical pattern verbatim when fixing compile errors — drift is the realistic outcome.
2. **A4.6 Validate-on-services-collection semantics ship to production, and a host that registers `IAuditTrail` after `AddSunfishRecoveryCoordinator()` silently passes validation and runs without audit emission (or with mid-state).** Fix is mechanical (F3) — replace with constructor-guard pattern.
3. **W#32 build flips `held` → `ready-to-build` on A4 merge without these mechanical fixes, and Stage 06 ships the broken code listings before A4.1's mechanical fixes can be amended in.** Mitigation: this council review's mechanical fixes (F1 + F2 + F3) should land as an A5 mechanical amendment BEFORE the W#32 ledger flip. XO has authority for that mechanical amendment per Decision Discipline Rule 3.

---

## 6. Recommended amendments

### A1 (REQUIRED, mechanical) — Replace `TenantKeyProviderFieldDecryptor` primary-constructor syntax with explicit two-overload constructor (resolves F1)

In A4.1, replace the listing with the explicit-fields pattern matching `InMemoryTaxonomyRegistry`:

```csharp
public sealed class TenantKeyProviderFieldDecryptor : IFieldDecryptor
{
    private readonly ITenantKeyProvider _tenantKeys;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly IRecoveryClock _clock;

    /// <summary>Creates the decryptor with audit emission disabled.</summary>
    public TenantKeyProviderFieldDecryptor(ITenantKeyProvider tenantKeys, IRecoveryClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(tenantKeys);
        _tenantKeys = tenantKeys;
        _clock = clock ?? new SystemRecoveryClock();
    }

    /// <summary>Creates the decryptor with audit emission wired through <paramref name="auditTrail"/> + <paramref name="signer"/>.</summary>
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

    // ... rest of class (DecryptAsync + EmitAuditAsync) unchanged, references _auditTrail / _signer / _clock
}
```

Apply the same pattern to `TenantKeyProviderFieldEncryptor` (single-constructor; just `_tenantKeys`).

**Authority:** XO (mechanical — purely shape, no business judgment).

### A2 (REQUIRED, mechanical) — Fix `IRecoveryClock.UtcNow` method-vs-property syntax (resolves F2)

In A4.1, change every `clock?.UtcNow` and `clock.UtcNow` to `clock?.UtcNow()` and `clock.UtcNow()`. Both call sites need updating:

```csharp
var now = (clock ?? new SystemRecoveryClock()).UtcNow();   // was: .UtcNow
// ...
var occurredAt = (_clock ?? new SystemRecoveryClock()).UtcNow();   // was: clock?.UtcNow ?? DateTimeOffset.UtcNow
```

(After A1 lands, the second call uses `_clock` consistently and the null-coalescing isn't needed since `_clock` is initialized in both constructors.)

**Authority:** XO (mechanical — single-character fix per call site).

### A3 (REQUIRED, mechanical) — Replace A4.6 `Validate(...)` block with constructor-guard semantics (resolves F3)

Drop the `services.AddOptions<RecoveryCoordinatorOptions>().Validate(...)` block. Replace with DI registration that picks the right overload:

```csharp
// Field-encryption substrate (A2 + A3 + A4)
services.AddSingleton<IFieldEncryptor, TenantKeyProviderFieldEncryptor>();
services.AddSingleton<IFieldDecryptor>(sp =>
{
    var tenantKeys = sp.GetRequiredService<ITenantKeyProvider>();
    var auditTrail = sp.GetService<IAuditTrail>();
    var signer = sp.GetService<IOperationSigner>();
    var clock = sp.GetService<IRecoveryClock>();
    if (auditTrail is not null && signer is not null)
        return new TenantKeyProviderFieldDecryptor(tenantKeys, auditTrail, signer, clock);
    if (auditTrail is null && signer is null)
        return new TenantKeyProviderFieldDecryptor(tenantKeys, clock);
    throw new InvalidOperationException(
        "Field-encryption substrate requires both IAuditTrail and IOperationSigner registered together, or neither. " +
        $"Got: auditTrail={(auditTrail is null ? "null" : "registered")}, signer={(signer is null ? "null" : "registered")}.");
});
```

This runs at DI resolution time (predictable; uses actual resolved values) and throws clearly on misconfiguration. Update A4.4's misconfiguration-test wording to match: "Resolving `IFieldDecryptor` from a `ServiceProvider` configured with exactly one of `(IAuditTrail, IOperationSigner)` throws `InvalidOperationException` at resolution time."

**Authority:** XO (mechanical — DI shape, follows established pattern).

### A4 (REQUIRED, encouraged → required) — Drop the `-v{n}` suffix from purpose label in Phase 1 (resolves F4)

In A4.1, change:

```csharp
var purpose = $"encrypted-field-aes-v{keyVersion}";
```

to:

```csharp
// Phase 1: fixed key-version 1; purpose label has no version suffix.
// When Phase 2 rotation ships, the rotation amendment introduces purpose-label
// versioning at that point; Phase 1 ciphertexts (purpose: "encrypted-field-aes")
// remain decryptable because the ciphertext-recorded KeyVersion=1 maps to the
// no-suffix purpose label.
var purpose = "encrypted-field-aes";
```

Add corresponding note to A4.3 saying Phase 1 omits the version suffix; Phase 2 rotation introduces it. This matches the existing `ITenantKeyProvider.DeriveKeyAsync` xmldoc example without xmldoc-edit churn.

**Authority:** XO (mechanical — purpose-label string change; no semantic change since Phase 1 is fixed-version anyway).

**Note:** classified `REQUIRED` rather than `Encouraged` because shipping a divergent purpose-label-format convention now creates a forward-compatibility burden for Phase 2 rotation that A4 doesn't address. Better to land it cleanly.

### A5 (Encouraged) — Add Phase-1 encrypt-side write-version invariant (resolves F5)

In A4.3, add a paragraph after "Phase 1 ships with **fixed key-version 1**":

> **Phase 1 invariants (preserved when Phase 2 rotation ships):**
>
> - **Encryption MUST write `KeyVersion = 1`** (no exceptions; encrypt-side has no rotation logic).
> - **Decryption MUST accept any `KeyVersion ≥ 1`** (forward-compatible — Phase 2 ciphertexts are decryptable by Phase 1 if a rollback-to-Phase-1 happens, by deriving the appropriate per-version DEK from the ciphertext-recorded `KeyVersion`).
> - **Phase 2 rotation amendment** introduces version-bumping on encrypt; Phase 1's encrypt-write-1 invariant is enforced by the absence of any rotation primitive in Phase 1, not by a runtime check.

**Authority:** XO (mechanical — invariants restatement, no semantic change).

### A6 (Encouraged) — Add substrate-issuer signing-key compromise to A4.5 "Out of scope" (resolves F6)

In A4.5 "Out of scope," append:

> - **Substrate-issuer signing-key compromise.** Phase 1 signs all field-decryption audit records with a single substrate-issuer Ed25519 key. Compromise of that key permits forgery of audit records across all tenants this substrate serves. Mitigation derives from platform keystore protection of the underlying Ed25519 key + ADR 0049's planned audit-format `v0` → `v1` algorithm-agility refactor. Per-tenant signing is rejected as a design option (key-management complexity exceeds the marginal isolation benefit).

**Authority:** XO (mechanical — threat-model section addition).

### A7 (Encouraged) — Align A4.4 misconfiguration-test wording with A3 enforcement-point (resolves F7)

After A3 lands, update A4.4's misconfiguration test to:

> - [ ] Audit emission misconfiguration: resolving `IFieldDecryptor` from a `ServiceProvider` configured with exactly one of `(IAuditTrail, IOperationSigner)` throws `InvalidOperationException` at resolution time

Replaces the "DI extension throws at startup" wording.

**Authority:** XO (mechanical — test-spec wording).

---

## 7. Cohort pattern note

A2 (PR #325) merged without pre-merge council review and required 5 substantive amendments (A2→A3 mechanical + A2→A4 substantive). That single skipped-council event cost two follow-up amendment cycles, two PRs, and ~3 days of W#32 build pause.

A4 ran council BEFORE merge (auto-merge intentionally disabled per the cohort lesson). This council finds 3 Major + 4 Minor/Encouraged amendments — caught pre-merge, can land as a single A5 mechanical amendment after this review, no W#32 secondary pause.

**Cohort batting average update:**

| ADR amendment | Pre-merge council? | Post-merge amendments needed | Days of build pause |
|---|---|---|---|
| 0046-A2 | ✗ skipped | 5 (3 mechanical + 2 substantive → A3+A4) | ~3 |
| 0046-A4 | ✓ ran (this review) | 3-4 mechanical (one A5 amendment) | 0 (caught pre-merge) |
| 0051 | ✓ ran | 4 mechanical | 0 |
| 0052 | ✓ ran | 3 mechanical | 0 |
| 0053 | ✓ ran | 2 mechanical | 0 |
| 0054 | ✓ ran | 3 mechanical | 0 |
| 0058 | ✓ ran | 3 mechanical | 0 |
| 0059 | ✓ ran | 2 mechanical | 0 |
| 0061 | ✓ ran | 4 mechanical | 0 |

**Pattern:** pre-merge council on substrate ADR amendments has 100% catch rate on Major findings; post-merge has 0% (by definition). The cohort lesson — "council before merge is canonical for substrate ADR amendments" — is now a hard rule, not a guideline. XO should disable auto-merge on substrate ADR amendment PRs by default and re-enable after council clears.

---

## 8. What XO should do next

1. **Read this review.** XO is the author of A4 and has authority for mechanical-only amendments per Decision Discipline Rule 3.
2. **Apply A1 + A2 + A3 + A4 + A5 + A6 + A7 as a single A5 mechanical amendment** to the same PR #333 (or a follow-up PR if PR #333 needs to merge first to unblock the W#32 ledger). All 7 amendments are mechanical / threat-model wording / test-spec wording — none introduces business judgment, none changes the substrate decision.
3. **Re-enable auto-merge on PR #333 after the A5 mechanical amendment lands.** The substrate decisions (consume `ITenantKeyProvider`; defer rotation; nullable both-or-neither audit DI) are correct and accepted.
4. **Update the W#32 hand-off addendum** per A4.8 — but ONLY after the A5 mechanical fixes land, so the hand-off references compilable code listings. Then flip W#32 ledger row from `held` to `ready-to-build`.
5. **Capture the cohort lesson in `feedback_decision_discipline.md`** (or extend the existing Rule 6 entry): "Substrate ADR amendments: council BEFORE merge; auto-merge disabled by default until council clears. Cohort batting average: 7-of-7 needed amendments; pre-merge council has 100% Major-finding catch rate."

---

## 9. Summary

**Verdict:** Accept-with-amendments — grade B (low-B).

**Findings:** 0 Critical + 3 Major + 1 Minor + 3 Encouraged

| ID | Severity | Category | One-line |
|---|---|---|---|
| F1 | Major | AP-1, AP-21 | Primary-constructor syntax + underscored-field references → CS0103 at compile |
| F2 | Major | AP-21 | `IRecoveryClock.UtcNow` is a method, not a property — drift |
| F3 | Major | AP-1, AP-3 | DI Validate-on-services-collection has wrong timing + wrong enforcement point |
| F4 | Minor | AP-1, AP-21 | Purpose-label format diverges from existing xmldoc example |
| F5 | Encouraged | AP-3 | Phase-1 encrypt-side write-version invariant is implicit, not stated |
| F6 | Encouraged | AP-21 / threat-model | Substrate-issuer signing-key compromise blast radius missing from threat model |
| F7 | Encouraged | AP-3 | A4.4 vs A4.6 enforcement-point wording mismatch |

**Amendments:** 4 required (all mechanical — XO authority) + 3 encouraged

| ID | Severity | Authority | Resolves |
|---|---|---|---|
| A1 | REQUIRED, mechanical | XO | F1 |
| A2 | REQUIRED, mechanical | XO | F2 |
| A3 | REQUIRED, mechanical | XO | F3 |
| A4 | REQUIRED, mechanical | XO | F4 |
| A5 | Encouraged | XO | F5 |
| A6 | Encouraged | XO | F6 |
| A7 | Encouraged | XO | F7 |

**No findings escalate to CO** (per Decision Discipline Rule 3) — all are mechanical, threat-model wording, or test-spec wording. XO has authority to land them as a single A5 mechanical amendment.

**W#32 build status:** can flip `held` → `ready-to-build` AFTER A5 mechanical amendment lands (so Stage 06 implementer reads compilable code listings). The substrate decisions (delegation to `ITenantKeyProvider`; rotation deferral; both-or-neither audit DI) are correct.

**Cohort lesson reaffirmed:** council before merge on substrate ADR amendments. 7-of-7 needed amendments; pre-merge council catches 100% of Major findings before the W#32-style build-pause cost lands.

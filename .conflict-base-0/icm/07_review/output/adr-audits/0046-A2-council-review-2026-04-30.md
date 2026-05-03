# ADR 0046 Amendment A2 (EncryptedField + IFieldDecryptor) — Council Review

**Reviewer:** research session (XO; adversarial council, UPF Stage 1.5)
**Date:** 2026-04-30
**Subject:** ADR 0046 Amendment A2 (sub-amendments A2.1 — A2.11), merged 2026-04-30 via PR #325 without prior council review
**Companion artifacts read:** ADR 0046 (post-amendment); `packages/foundation-recovery/` source tree; `packages/kernel-security/Keys/IRootSeedProvider.cs` + `KeystoreRootSeedProvider.cs`; `packages/kernel-audit/AuditEventType.cs` + `AuditRecord.cs` + `IAuditTrail.cs`; `packages/foundation/Assets/Common/{TenantId,ActorId}.cs`; `packages/foundation-multitenancy/ITenantScoped.cs`; `packages/foundation-recovery/TenantKey/{ITenantKeyProvider,InMemoryTenantKeyProvider}.cs`; `packages/foundation-recovery/PaperKeyDerivation.cs`; W#32 Stage 06 hand-off; sister ADR 0058 council review (the trigger that forced A2 to ship).

---

## 1. Verdict

**Accept-with-amendments — grade B (borderline B/C).** The architectural intent is right: package the field-encryption substrate inside `foundation-recovery` (root-seed-derived), use AES-256-GCM (AEAD), per-tenant DEK via HKDF-SHA256, capability-checked decrypt-on-read with audit emission, EFCore-friendly `record struct` shape. The threat model and acceptance-criteria checklist are well-formed. **But three Major and two Critical findings show the same pre-merge-without-review failure mode that fired on 0051/0053/0054/0058/0059/0061**: cited-symbol drift (Critical), an unreferenced existing seam (`ITenantKeyProvider`) that the amendment duplicates (Critical), an unmodelled audit-emission envelope requirement (Major), a wrong root-seed API signature (Major), and an unspecified rotation-store persistence shape (Major). Six required + two encouraged amendments below close the gaps. Most are mechanical (rename, re-cite, add halt-condition); two are business-judgment (relationship to `ITenantKeyProvider`; rotation-store persistence) and must escalate to CO for a substrate-shape ruling.

The five-substrate-ADR cohort batting average is now **6-of-6 needing post-acceptance amendments after council review**. The pattern is structural, not coincidental. See §7.

---

## 2. Findings

### F1 (Critical, AP-19/21 cited-symbol drift) — `TenantId` namespace is wrong

**Where:** A2.2 imports `using Sunfish.Foundation.MultiTenancy;` for `TenantId`. A2.7 cited-symbol verification claims `Sunfish.Foundation.MultiTenancy.TenantId (packages/foundation-multitenancy/)`.

**Reality (verified `git grep` on origin/main):** `TenantId` is defined at `packages/foundation/Assets/Common/TenantId.cs` in namespace `Sunfish.Foundation.Assets.Common` — same namespace as `ActorId`, NOT `Sunfish.Foundation.MultiTenancy`. The `foundation-multitenancy` package consumes `TenantId` via `using Sunfish.Foundation.Assets.Common;` (see `ITenantScoped.cs`); it does not redefine it.

**Impact:** the A2.2 code listing's `using` statement will fail to compile; A2.7's "Existing (verified)" assertion is false. Stage 06 implementer copy-pasting the listing hits a CS0234 namespace error.

### F2 (Critical, AP-1 unvalidated assumption + AP-10 first-idea) — `ITenantKeyProvider` already exists with `encrypted-field-aes` purpose label; the amendment ignores it

**Where:** A2.3 derives the per-tenant DEK inline inside `RecoveryRootSeedFieldEncryptor` / `RecoveryRootSeedFieldDecryptor` via direct calls to `IRootSeedProvider` + HKDF.

**Reality:** `packages/foundation-recovery/TenantKey/ITenantKeyProvider.cs` already exists with the exact signature `Task<ReadOnlyMemory<byte>> DeriveKeyAsync(TenantId tenant, string purpose, CancellationToken ct)`. Its XML docs literally cite `encrypted-field-aes` as a sample purpose label. `InMemoryTenantKeyProvider` ships an HKDF-SHA256 implementation. The amendment does not mention this seam.

**Impact:** the substrate is being built twice. Either the amendment should consume `ITenantKeyProvider.DeriveKeyAsync(tenant, "encrypted-field-aes-v" + keyVersion, ct)` (much cleaner; key-versioning falls out of the purpose label) OR the amendment should explicitly reject `ITenantKeyProvider` with a documented rationale (e.g., "the rotation-version semantics don't fit purpose-label encoding"). Without addressing this, the cluster ships two parallel per-tenant key derivation paths in the same package.

**This is business-judgment, not mechanical** — XO must escalate to CO for a substrate-shape ruling before A3 mechanical amendments can resolve.

### F3 (Major, AP-1 + AP-21 cited-symbol drift) — `KeystoreRootSeedProvider.GetSeed()` API does not exist

**Where:** A2.3 pseudo-code: `master_root_seed = KeystoreRootSeedProvider.GetSeed()    // 32-byte; existing primitive`.

**Reality:** the canonical interface is `IRootSeedProvider.GetRootSeedAsync(CancellationToken ct)` returning `ValueTask<ReadOnlyMemory<byte>>` (`packages/kernel-security/Keys/IRootSeedProvider.cs`). There is no synchronous `GetSeed()` method. `KeystoreRootSeedProvider` is the implementation class; consumers code against the interface.

**Impact:** A2.3 pseudo-code will not compile against the actual API. The Stage 06 hand-off Phase 2 already substitutes the correct call; the amendment text is the drift point.

### F4 (Major, AP-1 unvalidated assumption) — Audit emission shape ignores the `SignedOperation<AuditPayload>` envelope requirement

**Where:** A2.4 specifies audit emission as a raw `Dictionary<string, object?>` body. The implementation note says "Factory methods land in `FieldEncryptionAuditPayloads` matching the established `TaxonomyAuditPayloads` pattern from W#31."

**Reality:** `IAuditTrail.AppendAsync(AuditRecord record, ...)` REQUIRES the record's `Payload` field to be a `Sunfish.Foundation.Crypto.SignedOperation<AuditPayload>` envelope. The kernel boundary algorithmically verifies the envelope's Ed25519 signature on append (`AuditSignatureException` on failure). A bare `AuditPayload(IReadOnlyDictionary<string, object?> Body)` is not enough; the caller must wrap it in `SignedOperation<AuditPayload>` with a real Ed25519 signature.

**Impact:** the A2.4 code listing will be rejected by `IAuditTrail` at runtime. The decryptor needs an `Ed25519Signer` (or equivalent) injected to produce the signed envelope; this dependency is missing from A2.5 DI registration. **This breaks the "audit emission on every decrypt" claim** — denied-decrypt audits would also need a signed envelope, but the actor in a denied case may not hold a valid signing key. The pattern needs an issuer-signed envelope (the substrate's own key, not the actor's), which is not specified.

`TaxonomyAuditPayloads` from W#31 may have the same gap; XO should re-audit that pattern as a follow-on.

### F5 (Major, AP-1 + AP-18 unverifiable gate) — Rotation-store persistence shape is unspecified

**Where:** A2.3 says `keyVersion` is "stored in `KeystoreRootSeedProvider` (existing `Foundation.Recovery` persistence)". The Stage 06 hand-off introduces a new `IFieldEncryptionKeyVersionStore` with an `InMemoryFieldEncryptionKeyVersionStore` (`ConcurrentDictionary<TenantId, int>`).

**Reality:** `KeystoreRootSeedProvider` lives in `packages/kernel-security/Keys/` — different package, different storage primitive (platform keystore via DPAPI / Keychain / libsecret). It does NOT store per-tenant key-version integers. The hand-off implicitly creates a new in-memory store with no durable backing — meaning **per-tenant DEK version resets to zero on process restart, and every previously-encrypted ciphertext becomes undecryptable** (its `KeyVersion` field references a version the in-memory store no longer knows about).

**Impact:** the rotation primitive is non-functional in any non-trivial deployment. This is a P0 bug waiting to ship in Stage 06 if not closed at the ADR layer. The amendment must specify either (a) durable persistence (SQLCipher row in `foundation-recovery`'s state store) OR (b) derive the version from the ciphertext's stored `KeyVersion` only and treat it as monotonically tenant-scoped without a separate store.

### F6 (Major, AP-21 cited-fact without source) — EFCore `OwnsOne<EncryptedField>` claim is unverified

**Where:** A2.1 storage shape: "EFCore `OwnsOne<EncryptedField>` mapping is supported."

**Reality:** EFCore `OwnsOne` requires the owned type to be a class or have a parameterless ctor + writable settable properties. `readonly record struct EncryptedField` with positional `ReadOnlyMemory<byte>` properties is **not** trivially `OwnsOne`-able — `ReadOnlyMemory<byte>` requires a value converter, and EFCore's owned-type tracking has known issues with structs that have no settable members. The XO recommendation to use three separate columns (`*_ciphertext`, `*_nonce`, `*_key_version`) actually side-steps `OwnsOne` entirely.

**Impact:** the A2.6 acceptance-criteria checkbox `EFCore OwnsOne mapping smoke test (in-memory provider)` may not be achievable as drafted. Either the type shape needs adjustment (mutable properties; class instead of struct; or explicit `ValueConverter<EncryptedField, byte[]>`) OR the storage-shape recommendation needs to drop the `OwnsOne` framing and lean on the three-column path it already prefers.

### F7 (Minor, AP-3 vague success criteria) — `FixedDecryptCapability.ValidateForDecrypt` strictness is unspecified

**Where:** A2.2 spec for `FixedDecryptCapability`: "constructed from `(actor, tenant, validUntil)`". Hand-off Phase 2 spec: "`ValidateForDecrypt(now)` returns null if now ≤ validUntil + actor matches the tenant scope".

**Reality:** what does "actor matches the tenant scope" mean? Does the `FixedDecryptCapability` carry a list of tenants the actor may decrypt? Is the tenant binding 1:1 (one capability per tenant) or 1:many? The interface defines `Tenant { get; }` (singular), but the validate signature does not receive the target tenant — only `now`. So **the cross-tenant decrypt prevention claim from A2.8 ("Cross-tenant decrypt → impossible") relies on `RecoveryRootSeedFieldDecryptor` independently checking `capability.Tenant == tenant`** — that check is in the Stage 06 hand-off pseudo-code but is NOT in the ADR text. If the hand-off is dropped or the implementation drifts, the structural claim becomes a policy-only claim.

**Impact:** the structural-vs-policy gap noted on ADR 0058 fires again here. Make it structural by adding `tenant` to the `ValidateForDecrypt` signature, or document that the decryptor (not the capability) is the cross-tenant gate and add it to A2.6 acceptance-criteria as an explicit checkbox.

### F8 (Minor, AP-21) — Timing-attack surface on `ValidateForDecrypt` not addressed

**Where:** A2.8 threat model lists in-scope attacks but does not address timing.

**Reality:** `IDecryptCapability.ValidateForDecrypt` is a string-returning method whose distinct denial reasons ("expired", "wrong-tenant", "revoked", "capability null") are emitted into the audit log via `denial_reason` field. An attacker with audit-log read access can distinguish denial causes; they can also potentially timing-distinguish the validation paths (constant-time comparison of `validUntil` vs `now` is straightforward, but tenant-mismatch may short-circuit before tenant-DEK derivation, while wrong-key may go through DEK derivation + AES-GCM tag verification, leaking ~ms-scale timing).

**Impact:** low for Phase 1 (audit-log access is itself privileged), but should be in the threat model under "out of scope" with a note. Encouraged amendment.

### F9 (Encouraged, AP-12 timeline fantasy) — A2.6 test count is undersized

**Where:** A2.6 acceptance criteria target `~20-25` tests across 7 named categories.

**Reality:** the amendment introduces 11 net-new types (per A2.7) + 3 audit constants. Coverage of round-trip (1) + 4 capability rejection paths (4) + cross-tenant (1) + multi-version (3 — old, current, future) + audit-emission shape (3 events) + JSON round-trip (3) + EFCore smoke (1) + key-version-store concurrency (2) + HMAC-tag-tampering rejection (1) + nonce-uniqueness sanity (1) lands closer to 35–40. 20–25 is undersized for the threat-model claims.

**Impact:** acceptance criteria will be either checked-off prematurely or expanded mid-build. Encouraged: bump the band to `~30-40`.

---

## 3. AP-21 cited-symbol audit (per Decision Discipline Rule 6)

For each `Sunfish.*` symbol in the amendment, classify per A2.7's framework:

| Symbol | Cited as | Verified status |
|---|---|---|
| `Sunfish.Foundation.Assets.Common.ActorId` | Existing | ✓ verified at `packages/foundation/Assets/Common/ActorId.cs` |
| `Sunfish.Foundation.MultiTenancy.TenantId` | Existing | **✗ DRIFT — actual location `Sunfish.Foundation.Assets.Common.TenantId`** (F1) |
| `Sunfish.Kernel.Audit.AuditEventType` | Existing | ✓ verified at `packages/kernel-audit/AuditEventType.cs` |
| `Sunfish.Kernel.Audit.AuditPayload` | Existing | ✓ verified at `packages/kernel-audit/AuditRecord.cs:73` |
| `Sunfish.Kernel.Audit.AuditRecord` | Existing | ✓ verified at `packages/kernel-audit/AuditRecord.cs:49`; **but require `SignedOperation<AuditPayload>` envelope** (F4) |
| `Sunfish.Kernel.Audit.IAuditTrail` | Existing | ✓ verified at `packages/kernel-audit/IAuditTrail.cs:39` |
| `KeystoreRootSeedProvider.GetSeed()` | Existing | **✗ DRIFT — actual API is `IRootSeedProvider.GetRootSeedAsync(CancellationToken)` → `ValueTask<ReadOnlyMemory<byte>>`** (F3) |
| `PaperKeyDerivation` | Existing | ✓ verified at `packages/foundation-recovery/PaperKeyDerivation.cs:54` |
| `EncryptedField` etc. | New (introduced by A2) | ✓ correctly classified |
| `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider` | **NOT CITED** | **✗ existing seam ignored** (F2) |

**2 cited-symbol drifts + 1 ignored existing seam.** Same failure mode as the cohort baseline.

---

## 4. Top 3 risks

1. **`ITenantKeyProvider` duplication (F2; Critical, business-judgment).** Shipping two parallel per-tenant key derivation paths in the same package is a substrate-coherence regression. CO must rule on relationship: consume the existing seam, deprecate it, or document a rejection rationale. Until then Stage 06 will guess.

2. **Audit-envelope signing omitted (F4; Major, structural).** The substrate's audit emission cannot work as drafted because `IAuditTrail.AppendAsync` rejects unsigned payloads. The decryptor needs an issuer-signing key (Ed25519) injected; this dependency is silently missing. Will fail at runtime in the first integration test.

3. **Rotation-store persistence undefined (F5; Major).** Without durable per-tenant key-version storage, rotation is non-functional and previously-encrypted ciphertexts become undecryptable on process restart. The hand-off's `InMemoryFieldEncryptionKeyVersionStore` is not a viable Phase 1 ship state without an explicit "replace with durable store before Phase 1 G6 closes" halt-condition.

---

## 5. Top 3 strengths

1. **Threat model is well-formed.** A2.8 cleanly separates in-scope from out-of-scope with concrete rationales. The "no `Plaintext` property on the type" structural claim is the right shape for the AP-21 problem ADR 0058 surfaced.

2. **Reuse-over-new-package decision is well-reasoned.** A2's preamble explicitly considers a separate `foundation-field-encryption` package and rejects it with a real rationale (root-seed-derived primitives co-located; three-types-deep package is too thin). Compounding consumer set (W#18 + W#22 + W#23 + ADR 0051) is named.

3. **Open questions are honest.** OQ-A2.1 / A2.2 / A2.3 each name a deferred decision with an XO recommendation + rationale. This is the right shape for a substrate ADR amendment — defer the optionality, ship the core.

---

## 6. Recommended amendments

Six required (mandatory before Stage 06 build); two encouraged. **Mechanical** = renames / citation fixes / added halt-conditions / scope-tightening (auto-acceptable per Decision Discipline Rule 3). **Business-judgment** = require CO escalation.

### A1 (REQUIRED, mechanical) — Fix `TenantId` namespace citation

In A2.2 `using` statement, replace `using Sunfish.Foundation.MultiTenancy;` with `using Sunfish.Foundation.Assets.Common;` (or import both, since `ITenantScoped` lives in MultiTenancy).

In A2.7 cited-symbol verification, correct: `Sunfish.Foundation.Assets.Common.TenantId (packages/foundation/Assets/Common/TenantId.cs)`.

### A2 (REQUIRED, mechanical) — Fix `IRootSeedProvider` API signature in A2.3

A2.3 pseudo-code line `master_root_seed = KeystoreRootSeedProvider.GetSeed()` becomes:

```text
master_root_seed = await rootSeedProvider.GetRootSeedAsync(ct)    // 32-byte; existing primitive
```

…where `rootSeedProvider` is an injected `IRootSeedProvider` (kernel-security). A2.5 DI registration should clarify that consumers must register `AddSunfishKernelSecurity()` first (already implied by the existing `AddSunfishRecoveryCoordinator` XML doc).

### A3 (REQUIRED, business-judgment — escalate to CO) — Reckon with `ITenantKeyProvider`

The `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider` seam already exists in the same package with the exact `DeriveKeyAsync(TenantId tenant, string purpose, CancellationToken)` signature, and its XML docs name `encrypted-field-aes` as a sample purpose. Three options:

- **α (RECOMMENDED):** consume `ITenantKeyProvider` from `RecoveryRootSeedFieldEncryptor`/`Decryptor` with `purpose = "encrypted-field-aes-v" + keyVersion`. Drops ~30 lines of inline HKDF; folds key-versioning into the purpose label naturally.
- **β:** deprecate `ITenantKeyProvider` in favor of A2's per-tenant DEK derivation; mark the existing interface for removal in Phase 2.
- **γ:** keep both; document the rejection rationale (e.g., `ITenantKeyProvider` returns 32-byte material with no algorithm/version metadata; A2 needs versioned metadata).

CO ruling required; XO cannot mechanically pick.

### A4 (REQUIRED, mechanical) — Specify `SignedOperation<AuditPayload>` envelope in A2.4

A2.4 code listing currently shows raw `Dictionary<string, object?>` bodies. Add a sentence above the Body schemas:

> Each payload Body is wrapped in a `SignedOperation<AuditPayload>` envelope signed with an issuer-side Ed25519 key. `RecoveryRootSeedFieldDecryptor` accepts an `Ed25519Signer` (or `IIssuerSigner` if a contract for substrate-issuer signing emerges) via DI. The signing key's identity is the audit-trail issuer per `IAuditTrail.AppendAsync` envelope-verification semantics.

A2.5 DI registration adds the issuer-signer dependency. A2.6 acceptance criteria adds: `[ ] Audit emission round-trips through IAuditTrail without AuditSignatureException`.

### A5 (REQUIRED, business-judgment — escalate to CO) — Specify rotation-store persistence

A2.3 currently says key-version is stored in `KeystoreRootSeedProvider` — incorrect (different package, different storage primitive). Rule on Phase 1 shape:

- **α:** durable per-tenant version row in `foundation-recovery`'s state store (`IRecoveryStateStore` extension OR new `IFieldEncryptionKeyVersionStore` with SQLCipher backing).
- **β:** version is encoded entirely on the ciphertext (`EncryptedField.KeyVersion`); no separate store; rotation increments a "next version" counter local to the encryptor's lifetime; "current version" derived as `max(KeyVersion observed)` + monotonic clock.
- **γ:** explicitly defer durable rotation to Phase 1.x; ship in-memory store with halt-condition: "Phase 1 G6 cannot close until durable rotation-store ships."

CO ruling required.

### A6 (REQUIRED, mechanical) — Strengthen `IDecryptCapability.ValidateForDecrypt` scope-check

Add tenant parameter to the validate signature:

```csharp
string? ValidateForDecrypt(TenantId targetTenant, DateTimeOffset now);
```

`FixedDecryptCapability.ValidateForDecrypt(targetTenant, now)` returns `"wrong-tenant"` if `targetTenant != Tenant`, `"expired"` if `now > validUntil`, else null.

This makes the cross-tenant prevention structural (the capability mechanically enforces it) rather than relying on the decryptor's belt-and-suspenders check. Add to A2.6 acceptance: `[ ] Cross-tenant decrypt rejected by capability validate (NOT by decryptor field check)`.

### A7 (ENCOURAGED, mechanical) — Bump test target band + add timing-attack note to threat model

A2.6 test count: change `~20-25` → `~30-40` to match the surface area introduced.

A2.8 threat model — add to "Out of scope":

> Constant-time validation of `IDecryptCapability.ValidateForDecrypt` denial paths — Phase 1 accepts that `denial_reason` distinguishes failure modes in audit logs and that timing differences between tenant-mismatch and AES-GCM tag-failure are observable to an attacker with sub-millisecond timing access. Audit-log access is itself privileged; mitigation is layered authorization, not constant-time crypto.

### A8 (ENCOURAGED, mechanical) — Drop `OwnsOne` framing in storage-shape recommendation

A2.1 currently asserts EFCore `OwnsOne<EncryptedField>` is supported. Replace the recommendation with:

> Persistence: store as three columns (`*_ciphertext`, `*_nonce`, `*_key_version`) for queryability of `key_version` (rotation sweeps). EFCore mapping uses three property mappings on the owning entity, NOT `OwnsOne` (the `readonly record struct` + `ReadOnlyMemory<byte>` shape doesn't trivially compose with EFCore's owned-type tracking; explicit value converters or per-column property mappings are preferred).

A2.6 acceptance: drop `EFCore OwnsOne mapping smoke test`; replace with `[ ] EFCore three-column property mapping round-trips a record with EncryptedField fields (in-memory provider)`.

---

## 7. Cohort pattern (6-of-6 ADRs needed post-merge amendments)

| ADR | Pre-merge confidence | Council finding tier |
|---|---|---|
| 0051 (Payments) | MEDIUM-HIGH | 2× Critical (algorithm-agility, PCI scope) |
| 0053 (Work Orders) | MEDIUM | 1× Critical (state-set shape) + 2× Major |
| 0054 (Signatures) | MEDIUM-HIGH | 1× Critical (SignatureScope shape) |
| 0058 (Vendor Posture) | MEDIUM-HIGH | 2× Critical (EncryptedField/IFieldDecryptor phantom; Vendor record api-change) |
| 0059 (next ADR in cohort) | (priors) | (priors) |
| 0061 (next ADR in cohort) | (priors) | (priors) |
| **0046-A2 (this review)** | **(merged without review)** | **2× Critical + 3× Major** |

The single durable XO learning from the cohort: **never accept a substrate ADR amendment that asserts cited-symbol verification without a council pass**. PR #325 was the first amendment to bypass review; it surfaced exactly the failure mode the council pattern was designed to catch. Future ADRs (or amendments to ADRs) introducing cross-package types should run Stage 1.5 BEFORE the merge.

---

## 8. Reviewer's bottom line

ADR 0046 amendment A2 is **architecturally on-target** but **textually under-verified**. Two cited symbols drift from origin/main reality (F1, F3); one existing seam is silently duplicated (F2); audit emission cannot work as drafted (F4); rotation persistence is unspecified (F5); EFCore mapping framing is wrong (F6); structural-vs-policy gap on capability scope-check (F7).

**Verdict: Accept with amendments — grade B/C.** A1, A2, A4, A6, A7, A8 are mechanical and can be applied via a follow-up A3 amendment PR (per Decision Discipline Rule 3). A3 (`ITenantKeyProvider` reckoning) and A5 (rotation-store shape) require CO escalation — flagged for XO.

**W#32 build status:** the W#32 Stage 06 hand-off (already authored) should be **paused** until A1+A2+A4+A6 (mechanical) land in an A3 amendment PR AND A3+A5 (business-judgment) get CO ruling. Without those resolutions, Stage 06 either ships incorrect citations + non-functional rotation OR re-discovers each finding mid-build and halts.

**Estimated rewrite cost:** ~30-45 min for the mechanical amendments (A3 PR); 1-2 hours of XO time to assemble the CO-escalation memo for A3+A5; 0 code changes required for this council-review PR itself.

**Process recommendation:** add a "council review precedes any substrate ADR merge" policy item to Decision Discipline. PR #325 is the canonical case study for why merge-without-review is a footgun on substrate amendments — even when the architectural intent is right, the citation discipline cannot be self-graded.

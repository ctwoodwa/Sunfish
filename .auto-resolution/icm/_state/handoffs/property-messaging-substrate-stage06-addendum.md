# W#20 Bidirectional Messaging — Phase 3 Prereq Addendum

**Addendum date:** 2026-04-29
**Resolves:** COB beacon `cob-question-2026-04-29T(approx)-w20-p3-tenantkey.md` (PR #291)
**Original hand-off:** [`property-messaging-substrate-stage06-handoff.md`](./property-messaging-substrate-stage06-handoff.md)
**Workstream:** #20

---

## What this addendum does

COB has shipped W#20 Phases 1+2 (PRs #273 stub-extension, #276 blocks-messaging substrate). Phase 3 (HmacThreadTokenIssuer per ADR 0052 amendment A2) halted on the explicitly-named halt condition: `Sunfish.Foundation.Recovery.ITenantKeyProvider` doesn't exist in source on origin/main — it's referenced in ADR 0052 + the hand-off but hasn't been implemented. HMAC requires per-tenant key material to mint + verify tokens.

COB recommends Option A (minimal stub mirroring W#19 Phase 0 pattern). XO concurs.

---

## Resolution — Option A: minimal `ITenantKeyProvider` stub in `foundation-recovery`

Mirror the W#19 Phase 0 Money/ThreadId stub pattern: introduce the minimal `ITenantKeyProvider` surface in `packages/foundation-recovery/` that future ADR 0046 Stage 06 work extends without redefining.

### Stub spec

Create `packages/foundation-recovery/TenantKey/ITenantKeyProvider.cs`:

```csharp
namespace Sunfish.Foundation.Recovery.TenantKey;

/// <summary>
/// Provides per-tenant key material for cryptographic operations (HMAC,
/// envelope encryption, etc.). Phase 1 stub introduced by W#20 Phase 3
/// for HmacThreadTokenIssuer; ADR 0046 Stage 06 will replace with real
/// tenant-key derivation backed by Foundation.Recovery's KEK hierarchy.
/// </summary>
public interface ITenantKeyProvider
{
    /// <summary>Derive a 32-byte key for the given tenant + purpose label.</summary>
    /// <param name="tenant">Tenant identity.</param>
    /// <param name="purpose">Purpose label — e.g., "thread-token-hmac" or "encrypted-field-aes". Different purposes derive different keys for the same tenant.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>32-byte derived key (suitable for HMAC-SHA256 + AES-256).</returns>
    Task<ReadOnlyMemory<byte>> DeriveKeyAsync(TenantId tenant, string purpose, CancellationToken ct);
}
```

Plus an in-memory implementation:

```csharp
namespace Sunfish.Foundation.Recovery.TenantKey;

/// <summary>
/// Phase 1 stub implementation — derives keys deterministically via HKDF
/// over (tenantId || purpose) with a fixed development seed. NOT secure for
/// production. ADR 0046 Stage 06 replaces with real tenant-key-hierarchy
/// derivation (per-tenant DEK from KEK; KEK from operator master key).
/// </summary>
public sealed class InMemoryTenantKeyProvider : ITenantKeyProvider
{
    // Phase 1: HKDF-SHA256(salt = ASCII bytes "sunfish-phase-1-stub", ikm = tenant.Value || purpose)
    private static readonly byte[] DevelopmentSalt = "sunfish-phase-1-stub-not-for-production"u8.ToArray();

    public Task<ReadOnlyMemory<byte>> DeriveKeyAsync(TenantId tenant, string purpose, CancellationToken ct)
    {
        var ikm = new byte[16 + System.Text.Encoding.UTF8.GetByteCount(purpose)];
        // Use TenantId.Value's binary form — assumes TenantId wraps Guid; adjust if Foundation.MultiTenancy.TenantId differs
        tenant.Value.TryWriteBytes(ikm.AsSpan(0, 16));
        System.Text.Encoding.UTF8.GetBytes(purpose, ikm.AsSpan(16));

        var key = new byte[32];
        System.Security.Cryptography.HKDF.DeriveKey(
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            ikm,
            key,
            DevelopmentSalt,
            ReadOnlySpan<byte>.Empty);

        return Task.FromResult<ReadOnlyMemory<byte>>(key);
    }
}
```

DI extension:

```csharp
namespace Sunfish.Foundation.Recovery.TenantKey;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryTenantKeyProvider(this IServiceCollection services)
    {
        services.AddSingleton<ITenantKeyProvider, InMemoryTenantKeyProvider>();
        return services;
    }
}
```

### Cross-package compatibility

Stub matches ADR 0046 Stage 06 specification:
- `ITenantKeyProvider` is the canonical contract surface (extending W#15's existing tenant-key infrastructure or replacing it depending on ADR 0046 Stage 06's path)
- `InMemoryTenantKeyProvider` is the test/development implementation; production will use a `KekBackedTenantKeyProvider` or equivalent
- Same shape; ADR 0046 Stage 06 just replaces the InMemory impl + may add provider-specific interfaces (HSM-backed, etc.)

### Cross-substrate consumers

Beyond W#20 Phase 3:
- W#21 Signatures (`kernel-signatures`) for `DeviceAttestation` signing
- ADR 0058 Vendor Onboarding (TIN encryption per `EncryptedField`)
- ADR 0046 Stage 06 (when shipped) will define the canonical implementation backed by KEK

This stub introduction unblocks all of these consumers; they share the same `ITenantKeyProvider` interface from day 1.

---

## New Phase 0 (W#20 Phase 0 — runs before Phase 3)

**Phase 0 — `ITenantKeyProvider` stub in foundation-recovery** (~0.5h)

- Create `packages/foundation-recovery/TenantKey/ITenantKeyProvider.cs` per spec above
- Create `packages/foundation-recovery/TenantKey/InMemoryTenantKeyProvider.cs` per spec above
- Create DI extension in existing `packages/foundation-recovery/DependencyInjection/ServiceCollectionExtensions.cs` (or new `TenantKey/ServiceCollectionExtensions.cs`)
- 4 unit tests: derive (success); cross-tenant key mismatch (different tenants get different keys); cross-purpose key mismatch (same tenant + different purpose = different key); idempotence (same input = same output)

**Gate:** stubs compile; `dotnet build` clean; tests pass.

**PR title:** `feat(foundation-recovery): minimal ITenantKeyProvider stub for W#20 Phase 3 (ADR 0046 Stage 06 will extend)`

---

## Updated phase summary

| Phase | Subject | Hours | Status |
|---|---|---|---|
| 1 | Foundation.Integrations.Messaging contracts | 2–3 | ✅ shipped (#273 stub-extension) |
| 2 | blocks-messaging substrate | 4–6 | ✅ shipped (#276) |
| **0 (new)** | **ITenantKeyProvider stub** | **0.5** | **NEW per this addendum** |
| 3 | HmacThreadTokenIssuer (now consumes Phase 0 stub) | 1–2 | unblocked |
| 4 | providers-postmark first email adapter | 3–4 | pending |
| 5 | Inbound 5-layer defense + Bridge route | 3–4 | pending |
| 6 | Audit emission (12 AuditEventType) | 1–2 | pending |
| 7 | Cross-package integration tests | 1 | pending |
| 8 | apps/docs | 1 | pending |
| 9 | Ledger flip | 0.5 | pending |
| **Total** | | **17–24h** | (was 17.5–24.5h; Phase 0 absorbed into existing budget) |

Phase 0 is short enough to bundle with Phase 3 in the same PR if convenient.

---

## What this addendum does NOT change

- All other §"Acceptance criteria" in the original W#20 hand-off
- The 12 `AuditEventType` constants in Phase 6 — unchanged
- The 5-layer defense scope in Phase 5 — unchanged
- The `ThreadToken` HMAC-SHA256 spec in Phase 3 — unchanged; just adds the Phase 0 dependency providing the per-tenant key

---

## How sunfish-PM should pick this up

1. **Resume W#20 with new Phase 0** (foundation-recovery stub).
2. **Archive the COB beacon** at `icm/_state/research-inbox/cob-question-2026-04-29T(approx)-w20-p3-tenantkey.md` to `_archive/` in this addendum's PR (XO is doing this).
3. **Then Phase 3** consumes the now-existing `ITenantKeyProvider` interface.
4. **Then Phase 4 → 9** per original hand-off.

Same Option-A pattern as W#19 Phase 0 (Money + ThreadId stubs). Pattern is now well-established for "introduce minimal stub matching downstream ADR's spec; downstream Stage 06 extends without refactor."

---

## References

- Original W#20 hand-off: [`property-messaging-substrate-stage06-handoff.md`](./property-messaging-substrate-stage06-handoff.md)
- W#19 Phase 0 addendum (precedent for stub pattern): [`property-work-orders-stage06-addendum.md`](./property-work-orders-stage06-addendum.md)
- W#31 addendum (precedent for COB beacon resolution shape): [`foundation-taxonomy-phase1-stage06-addendum.md`](./foundation-taxonomy-phase1-stage06-addendum.md)
- ADR 0046 (Foundation.Recovery): canonical `ITenantKeyProvider` will be specified in Stage 06
- ADR 0052 §"Amendment A2": ThreadToken specification consuming this stub

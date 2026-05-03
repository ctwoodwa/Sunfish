# Workstream #28 — Phase 5c-4 unblock addendum (Prospect-capability verifier)

**Supersedes (specific clauses of):** [`property-public-listings-stage06-handoff.md`](./property-public-listings-stage06-handoff.md) §"Phase 5 — Inbound 5-layer defense + Bridge route family" — specifically the macaroon verifier seam for the Prospect-tier `GET /listings/criteria/{token}` + `POST /listings/criteria/{token}/start-application` routes
**Effective:** 2026-04-30 (resolves `cob-question-2026-04-30T14-30Z-w28-p5c4-capability-verifier`)
**Spec source:** COB beacon proposed Options A/B/C; XO selects Option A (block-local `IProspectCapabilityVerifier`)

W#28 ledger row is already `built` (PR #369); Phase 5c-4 (Prospect-tier capability-bound routes) is the last remaining substantive surface and is currently halted on the verifier-seam question. This addendum unblocks Phase 5c-4 for COB to ship as a follow-up PR.

Per Decision Discipline Rule 3 (auto-accept mechanical amendments), this addendum is mechanical: localized verifier matches the canonical pair-issuer-with-consumer pattern from W#32 (`IFieldEncryptor` + `IFieldDecryptor`) and W#21 (issuer + verifier are ergonomic siblings of a substrate seam). No foundation churn.

---

## Decision: Option A (block-local `IProspectCapabilityVerifier`)

**Resolution:** Phase 5c-4 introduces `Sunfish.Blocks.PublicListings.Capabilities.IProspectCapabilityVerifier` (sibling to the existing `MacaroonCapabilityPromoter` issuer). The verifier owns Sunfish-specific caveat-parsing (`tenant`, `listing-allowed`, `email`, `email-verified`, `expires`) + delegates generic time/scope evaluation to the existing `Sunfish.Foundation.Macaroons.IMacaroonVerifier`. Foundation surface unchanged.

**Why Option A** (vs B / C):
- **No foundation api-change.** Option B (extend `MacaroonContext` with `TenantSlug`/`RequestedListingId`/etc.) ripples into every existing macaroon consumer (federation-pattern-c). Out of W#28 scope; risks unrelated test breakage.
- **No spec deferral.** Option C (defer `GET /listings/criteria/{token}` to a follow-up; ship POST-only) breaks ADR 0059 §"Capability promotion" which specifically scopes criteria pages to Prospect tier. The `GET criteria` page IS the capability surface — deferring it punts the Prospect-tier UX entirely.
- **Pair-issuer-with-verifier is the canonical Sunfish substrate pattern.** W#32 ships `IFieldEncryptor` + `IFieldDecryptor` as ergonomic siblings; W#21 ships issuer (`ISignatureCapture`) + verifier (`Sunfish.Kernel.Signatures` verifier path) ditto. `MacaroonCapabilityPromoter` (issuer) without a sibling verifier is the unfinished half of that pair. Option A completes it.

## Verifier shape

```csharp
// In Sunfish.Blocks.PublicListings.Capabilities (new file:
// Capabilities/IProspectCapabilityVerifier.cs)
namespace Sunfish.Blocks.PublicListings.Capabilities;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Macaroons;

/// <summary>
/// Verifies a Prospect-tier macaroon token issued by
/// <see cref="MacaroonCapabilityPromoter"/>. Returns the projected
/// capability or throws a typed denial with a specific reason
/// (matches the FieldDecryptionDeniedException shape from W#32).
/// </summary>
public interface IProspectCapabilityVerifier
{
    Task<ProspectCapability> VerifyAsync(
        string tokenBase64Url,
        TenantId requestingTenant,
        ListingId requestedListing,
        DateTimeOffset now,
        CancellationToken ct);
}

public sealed record ProspectCapability(
    Guid CapabilityId,
    TenantId Tenant,
    string Email,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<ListingId> AllowedListings);

public sealed class ProspectCapabilityDeniedException : Exception
{
    public ProspectCapabilityDeniedException(string capabilityIdOrToken, string reason)
        : base($"Prospect capability denied for {capabilityIdOrToken}: {reason}") { }
}
```

**Reference impl** (single MacaroonProspectCapabilityVerifier; pair to MacaroonCapabilityPromoter):

```csharp
public sealed class MacaroonProspectCapabilityVerifier : IProspectCapabilityVerifier
{
    private readonly IMacaroonVerifier _generic;

    public MacaroonProspectCapabilityVerifier(IMacaroonVerifier generic)
    {
        ArgumentNullException.ThrowIfNull(generic);
        _generic = generic;
    }

    public async Task<ProspectCapability> VerifyAsync(
        string tokenBase64Url,
        TenantId requestingTenant,
        ListingId requestedListing,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Decode + delegate generic evaluation (signature + standard caveats).
        Macaroon macaroon;
        try
        {
            macaroon = MacaroonCodec.DecodeBase64Url(tokenBase64Url);
        }
        catch (MacaroonCodecException ex)
        {
            throw new ProspectCapabilityDeniedException(
                tokenBase64Url[..Math.Min(8, tokenBase64Url.Length)] + "...",
                $"decode failed: {ex.Message}");
        }

        var ctx = new MacaroonContext(
            Now: now,
            SubjectUri: new Uri($"sunfish://public-listings/{requestingTenant.Value}"),
            ResourceSchema: "Sunfish.Blocks.PublicListings.Listing",
            RequestedAction: "view-criteria",
            DeviceIp: null);

        var genericResult = await _generic.VerifyAsync(macaroon, ctx, ct).ConfigureAwait(false);
        if (genericResult is not MacaroonVerificationOk ok)
            throw new ProspectCapabilityDeniedException(macaroon.CapabilityId.ToString(),
                genericResult.RejectionReason ?? "generic-verifier-rejected");

        // Parse Sunfish-specific caveats.
        var caveats = ParseCaveats(macaroon.Caveats);

        if (caveats.Tenant != requestingTenant)
            throw new ProspectCapabilityDeniedException(macaroon.CapabilityId.ToString(),
                $"wrong-tenant: caveat={caveats.Tenant.Value}, requesting={requestingTenant.Value}");

        if (!caveats.AllowedListings.Contains(requestedListing))
            throw new ProspectCapabilityDeniedException(macaroon.CapabilityId.ToString(),
                $"listing-not-in-allowed-set: requested={requestedListing}");

        if (caveats.EmailVerified != true)
            throw new ProspectCapabilityDeniedException(macaroon.CapabilityId.ToString(),
                "email-not-verified");

        if (now > caveats.ExpiresAt)
            throw new ProspectCapabilityDeniedException(macaroon.CapabilityId.ToString(),
                $"expired: caveat={caveats.ExpiresAt:o}, now={now:o}");

        return new ProspectCapability(
            CapabilityId: macaroon.CapabilityId,
            Tenant: caveats.Tenant,
            Email: caveats.Email,
            ExpiresAt: caveats.ExpiresAt,
            AllowedListings: caveats.AllowedListings);
    }

    private static ParsedCaveats ParseCaveats(IEnumerable<MacaroonCaveat> caveats)
    {
        // Parse: capability-id, tenant, email, email-verified, expires,
        // listing-allowed (multi-valued). Strict parse; throw on unknown.
        // ...
    }

    private sealed record ParsedCaveats(
        Guid CapabilityId, TenantId Tenant, string Email, bool EmailVerified,
        DateTimeOffset ExpiresAt, IReadOnlyList<ListingId> AllowedListings);
}
```

**Verify before authoring:**
- `Sunfish.Foundation.Macaroons.IMacaroonVerifier` interface signature (returns a result type or throws? what's the rejection-reason API?) — verify against `git show origin/main:packages/foundation/Macaroons/IMacaroonVerifier.cs` before authoring; the listing above assumes a result-returning shape, may need adjustment if the actual contract throws.
- `Sunfish.Foundation.Macaroons.MacaroonCodec.DecodeBase64Url` — verify exists; if not, the verifier ships a small inline decode helper.
- `Sunfish.Foundation.Macaroons.Macaroon` record's `CapabilityId` + `Caveats` shape.
- `MacaroonCapabilityPromoter`'s caveat names — Phase 4 issuer + Phase 5c-4 verifier MUST agree on the exact strings (`tenant`, `listing-allowed`, `email`, `email-verified`, `issued-from-ip`, `expires`). The verifier's `ParseCaveats` is the source of truth for the consumer side; the issuer's `MintAsync` is the source of truth for the producer side.

**Caveat-name centralization** (recommended, optional): introduce a `Sunfish.Blocks.PublicListings.Capabilities.ProspectCaveatNames` `static class` exposing each caveat name as a `const string`. Both issuer + verifier consume from the same constants. Eliminates the drift risk Option A's "Cons" entry named.

## Phase 5c-4 acceptance criteria

- [ ] `IProspectCapabilityVerifier` interface in `packages/blocks-public-listings/Capabilities/`
- [ ] `MacaroonProspectCapabilityVerifier` reference impl
- [ ] `ProspectCapability` record + `ProspectCapabilityDeniedException`
- [ ] `ProspectCaveatNames` constants (recommended for drift prevention)
- [ ] DI registration via `AddSunfishPublicListings()` (existing extension)
- [ ] Bridge routes:
  - `GET /listings/criteria/{token}` — calls `IProspectCapabilityVerifier.VerifyAsync`; on success, renders the criteria document for the listing IDs in `AllowedListings`; on denial, returns 401 with the denial reason exposed (audit-grade error message OK; not a security issue since the macaroon already has a valid signature gate)
  - `POST /listings/criteria/{token}/start-application` — verifies + invokes `ILeasingPipelineService.SubmitApplicationAsync` (existing W#22 contract per ADR 0057)
- [ ] Tests:
  - Round-trip: issue via `MacaroonCapabilityPromoter`, verify via `MacaroonProspectCapabilityVerifier` → success
  - Tenant mismatch: issue for tenant A, verify with tenant B → `wrong-tenant` denial
  - Listing-not-allowed: issue with `listing-allowed = X`, verify against listing Y → `listing-not-in-allowed-set` denial
  - Expired: time travel past `expires` → `expired` denial
  - Email-not-verified: caveat-set has `email-verified = false` → `email-not-verified` denial
  - Decode failure: malformed base64url → `decode failed` denial
  - Generic-verifier rejection (e.g., signature invalid): propagated as `generic-verifier-rejected`
  - 4 new `AuditEventType` constants emitted: `ProspectCapabilityVerified`, `ProspectCapabilityDenied`, `ProspectCriteriaViewed`, `ProspectStartedApplication`
- [ ] Integration: W#22 `ILeasingPipelineService.SubmitApplicationAsync` accepts the `ProspectCapability` projection → produces a `LeasingApplication` record bound to the `tenant` + `email` from the capability

**Effort:** ~1.5–3h sunfish-PM time per COB beacon estimate.

**PR title:** `feat(blocks-public-listings,bridge): Phase 5c-4 — Prospect capability verifier + criteria routes (W#28, ADR 0059)`

## Halt-conditions for Phase 5c-4

- **`Sunfish.Foundation.Macaroons.IMacaroonVerifier` returns/throws differently than the listing assumes**: HALT + verify the actual contract; adjust the verifier's generic-evaluation call shape; do NOT change the foundation interface.
- **`MacaroonCodec.DecodeBase64Url` doesn't exist**: HALT + ship a small inline decoder OR file a follow-up beacon for foundation surface.
- **`ILeasingPipelineService.SubmitApplicationAsync` parameter list doesn't accept the `ProspectCapability` projection cleanly** (e.g., expects different fields): HALT + propose either an adapter on the W#28 side OR a small W#22 `SubmitApplicationRequest` extension.

## Out of scope for Phase 5c-4

- Generalizing `MacaroonContext` to carry Sunfish-specific predicates (Option B) — defensible architecturally; future ADR if multiple consumers want the same extension.
- Anti-replay (capability nonce tracking) — orthogonal concern; deferred until a real replay-attack scenario surfaces.
- Refresh / re-issue flow (Prospect re-applies after expiry without re-verifying email) — out of Phase 5c-4 scope; Phase 2.2+ user-experience refinement.

## Decision-class

Session-class per `feedback_decision_discipline` Rule 1 (NOT CO-class — pure substrate completion; COB proposed Option A as default; XO ratifies). Authority: XO; addendum follows the W#19 Phase 3 / W#21 Phase 0 / W#23 hand-off / W#28 Phase 5b addendum precedents.

---

## References

- COB beacon: `icm/_state/research-inbox/cob-question-2026-04-30T14-30Z-w28-p5c4-capability-verifier.md`
- W#28 hand-off: `icm/_state/handoffs/property-public-listings-stage06-handoff.md` §"Phase 5"
- W#28 Phase 5b addendum (precedent for cross-substrate adaptation): `icm/_state/handoffs/property-public-listings-stage06-phase5b-addendum.md`
- Issuer: `packages/blocks-public-listings/Capabilities/MacaroonCapabilityPromoter.cs`
- Macaroon substrate: `packages/foundation/Macaroons/`
- W#32 paired-substrate precedent: `packages/foundation-recovery/Crypto/IFieldEncryptor.cs` + `IFieldDecryptor.cs`
- ADR 0059 §"Capability promotion"

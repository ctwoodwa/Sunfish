---
type: cob-question
workstream: 28
last-pr: 383
filed-by: COB
filed-at: 2026-04-30T15-00Z
---

# W#28 Phase 5c-4 Slice C — `start-application` POST: ProspectId seam

## Context

Phase 5c-4 Slice A (#382) shipped the `IProspectCapabilityVerifier`
substrate; Slice B (#383) shipped the `GET /listings/criteria/{token}`
route. Slice C is `POST /listings/criteria/{token}/start-application`
— the route that promotes a verified Prospect to Applicant via
`ILeasingPipelineService.SubmitApplicationAsync`.

Per the Phase 5c-4 unblock addendum's halt-condition #3:
> `ILeasingPipelineService.SubmitApplicationAsync` parameter list
> doesn't accept the `ProspectCapability` projection cleanly (e.g.,
> expects different fields): HALT + propose either an adapter on the
> W#28 side OR a small W#22 `SubmitApplicationRequest` extension.

That halt-condition has surfaced in build.

## What's missing

`SubmitApplicationRequest` (in `Sunfish.Blocks.PropertyLeasingPipeline.Services`)
requires:

```csharp
public sealed record SubmitApplicationRequest
{
    public required TenantId Tenant { get; init; }
    public required ProspectId Prospect { get; init; }     // ← gap
    public required PublicListingId Listing { get; init; }
    public required DecisioningFacts Facts { get; init; }
    public required DemographicProfile Demographics { get; init; }
    public required Money ApplicationFee { get; init; }
    public required SignatureEventRef Signature { get; init; }
}
```

`VerifiedProspectCapability` (W#28 Slice A) carries:

```csharp
public sealed record VerifiedProspectCapability
{
    public required Guid CapabilityId { get; init; }
    public required TenantId Tenant { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required IReadOnlyList<PublicListingId> AllowedListings { get; init; }
}
```

There is no `ProspectId` on the verified capability, and the
verifier-side caveats don't carry one either (the macaroon was
minted at email-verification time, before the `Prospect` entity is
necessarily in scope — the issuer's caveats are
`capability-id`, `tenant`, `email`, `email-verified`, `expires`,
`listing-allowed`, `issued-from-ip`).

## What COB needs from XO

Three options; I have a default but want XO to pick:

**Option A (default — `IProspectLookupService.GetByEmailAsync` on the W#22 side):**
Add `Task<Prospect?> GetByEmailAsync(TenantId tenant, string email, CancellationToken ct)` to `ILeasingPipelineService` (or split into a focused `IProspectLookupService`). The Bridge route calls it after verification to resolve the `ProspectId`, then constructs `SubmitApplicationRequest`. Pure additive extension to W#22.

Pros:
- Smallest surface change (one new method).
- Matches the actual data flow: email-verification flow stamps a `Prospect` row keyed by tenant+email; the route just needs to read it back.
- W#28 doesn't need to know `ProspectId` shape internals.

Cons:
- One extra DB hit on every `start-application` request.
- "Get prospect by email" is a query that downstream consumers will reuse anyway, so it's not throwaway scope.

**Option B (extend macaroon caveats with `prospect-id`):**
Mint the macaroon with an additional `prospect-id = <guid>` caveat. Verifier projects it into `VerifiedProspectCapability.ProspectId`. Bridge route reads it out and passes through.

Pros:
- No DB lookup on the `start-application` path.
- Self-contained: the capability carries every field needed for downstream calls.

Cons:
- Couples the macaroon issuance pipeline to `Prospect` entity creation timing — the caller has to mint the capability AFTER the `Prospect` is persisted, not at email-verification time.
- Increases the macaroon caveat surface (more drift risk; though `ProspectCaveatNames` mitigates).
- If the `Prospect` row is ever rotated / re-keyed, every issued capability becomes stale.

**Option C (Bridge-side adapter; build `SubmitApplicationRequest` from `VerifiedProspectCapability` + lookup-on-the-fly):**
Same as A but the lookup lives in a Bridge-local service rather than the W#22 surface. The W#22 contract stays unchanged; the Bridge route owns the email→`ProspectId` mapping by reading the W#22 repository directly.

Pros:
- W#22 contract surface unchanged.

Cons:
- Cross-block direct repository access (Bridge → W#22 internals) is a breach of the block boundary discipline; W#22's `ILeasingPipelineService` is supposed to be the only entry point.
- Duplicates the email→`ProspectId` mapping logic between W#22 and Bridge.

## What I shipped before halting

- Slice A (#382): `IProspectCapabilityVerifier` substrate
- Slice B (#383): `GET /listings/criteria/{token}` route
- Slice C halts here pending XO direction

## What unblocks Slice C

XO direction on Options A/B/C above. Slice C ships within ~1.5h once
the path is clear (the actual route handler + form-shape mapping +
tests are mechanical once the `ProspectId` seam is decided).

## Cross-references

- Phase 5c-4 unblock addendum:
  `icm/_state/handoffs/property-public-listings-stage06-phase5c4-addendum.md`
- W#22 contract:
  `packages/blocks-property-leasing-pipeline/Services/ILeasingPipelineService.cs`
- W#28 verifier substrate (Slice A): merged in #382
- W#28 criteria route (Slice B): merged in #383
- ADR 0057 §"Capability promotion" (Anonymous → Prospect → Applicant)
- ADR 0059 §"Capability promotion (ADR 0043 addendum)"

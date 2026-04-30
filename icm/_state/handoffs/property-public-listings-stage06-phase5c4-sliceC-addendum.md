# Workstream #28 — Phase 5c-4 Slice C unblock addendum (`ProspectId` seam)

**Supersedes (specific clauses of):** [`property-public-listings-stage06-phase5c4-addendum.md`](./property-public-listings-stage06-phase5c4-addendum.md) Halt-condition #3
**Effective:** 2026-04-30 (resolves `cob-question-2026-04-30T15-00Z-w28-p5c4-startapp-prospectid-seam`)
**Spec source:** COB beacon proposed Options A/B/C; XO selects Option A (additive `GetProspectByEmailAsync` on `ILeasingPipelineService`)

W#28 Phase 5c-4 Slice A (verifier) + Slice B (GET criteria route) shipped via PRs #382 + #383. Slice C (`POST /listings/criteria/{token}/start-application`) halted on the `ProspectId` seam — `VerifiedProspectCapability` carries `Email` but not `ProspectId`, while `SubmitApplicationRequest` requires `ProspectId`. This addendum unblocks Slice C.

Per Decision Discipline Rule 3 (auto-accept mechanical amendments), this addendum is mechanical: minimal additive method on the existing W#22 service contract; matches the canonical "lookup by query key" pattern.

---

## Decision: Option A (additive `GetProspectByEmailAsync` on `ILeasingPipelineService`)

**Resolution:** Add `Task<Prospect?> GetProspectByEmailAsync(TenantId tenant, string email, CancellationToken ct)` to `ILeasingPipelineService` (NOT a new `IProspectLookupService` interface — keep the surface narrow). Bridge route calls it after verification to resolve `ProspectId`, then constructs `SubmitApplicationRequest`. Pure additive extension to W#22.

**Why Option A** (vs B / C):
- **Option B (mint macaroon with `prospect-id` caveat)** couples capability issuance to `Prospect` entity creation timing. Today the email-verification flow is *what creates* the Prospect — the macaroon is minted at email-verification time, before the Prospect entity is necessarily in scope. Adding `prospect-id` to caveats means either (a) two-step issuance (mint capability without `prospect-id`, persist Prospect, re-mint with `prospect-id` — broken UX), or (b) re-architecting the email-verification flow to create the Prospect before the macaroon mint (large blast radius beyond Slice C scope). Plus: if a `Prospect` row is ever rotated / re-keyed, every issued capability becomes stale.
- **Option C (Bridge-side adapter; cross-block direct repository access)** breaches the block-boundary discipline. `ILeasingPipelineService` is W#22's public entry point; the consolidated decision-discipline memory + cluster architecture explicitly say Bridge consumes blocks via their public service interfaces, not by reading repositories directly. Option C also duplicates the email→`ProspectId` mapping logic between W#22 and Bridge (drift risk).
- **Option A is the canonical "expose query as service method" pattern.** Lookup-by-email is a recurring query that downstream consumers will reuse anyway (e.g., the resend-verification flow; admin tooling for "find prospect"). Not throwaway scope.

**Why on `ILeasingPipelineService` not a new `IProspectLookupService`:**
- Single method addition; doesn't warrant a focused interface yet.
- W#22's `ILeasingPipelineService` is already the authoritative read+write service for the Prospect/Applicant lifecycle.
- If `IProspectLookupService` makes sense later (e.g., when 4+ lookup methods accumulate), refactor then. SRP/ISP is a future-refactor consideration, not a Slice-C blocker.

## Service contract addition

```csharp
// In Sunfish.Blocks.PropertyLeasingPipeline.Services.ILeasingPipelineService
public interface ILeasingPipelineService
{
    // ... existing methods unchanged ...

    /// <summary>
    /// Looks up the Prospect entity for a tenant by email.
    /// Returns null if no Prospect exists for the (tenant, email) pair.
    /// </summary>
    /// <remarks>
    /// Used by Phase 5c-4 Slice C to resolve <c>ProspectId</c> from the
    /// <c>VerifiedProspectCapability</c>'s email field at the
    /// <c>start-application</c> POST boundary. Email is the natural
    /// lookup key because the email-verification flow stamps the
    /// Prospect row keyed by (tenant, email) before macaroon issuance.
    /// </remarks>
    Task<Prospect?> GetProspectByEmailAsync(
        TenantId tenant,
        string email,
        CancellationToken ct);
}
```

**Implementation** in the existing `LeasingPipelineService` reference impl:

```csharp
public async Task<Prospect?> GetProspectByEmailAsync(
    TenantId tenant, string email, CancellationToken ct)
{
    // Repository lookup; case-insensitive email match recommended
    // (per RFC 5321 §2.4 the local-part is case-sensitive but in
    // practice mail providers normalize to lowercase; case-insensitive
    // match here matches user expectation and aligns with the
    // email-verification flow's case-folding).
    var normalizedEmail = email.Trim().ToLowerInvariant();
    return await _prospectRepository.FindByEmailAsync(tenant, normalizedEmail, ct).ConfigureAwait(false);
}
```

(`_prospectRepository` already exists in `LeasingPipelineService`; the existing `Prospect` repository may need a `FindByEmailAsync` query added if not present — check `IProspectRepository` first; if missing, add as part of Slice C.)

## Bridge route handler shape

```csharp
// In accelerators/bridge/Sunfish.Bridge/Endpoints/PublicListingsCapabilityEndpoint.cs
// (or equivalent — verify location per Phase 5c-4 addendum's path-correction A2)
app.MapPost("/listings/criteria/{token}/start-application",
    async (
        string token,
        SubmitApplicationFormRequest formRequest,  // route-local DTO
        TenantId tenantContext,
        IProspectCapabilityVerifier capabilityVerifier,
        ILeasingPipelineService leasingPipeline,
        IClock clock,
        CancellationToken ct) =>
{
    // 1. Verify capability (Slice A)
    VerifiedProspectCapability capability;
    try
    {
        capability = await capabilityVerifier.VerifyAsync(
            token, tenantContext, /* listing isn't required for start-app; pass null or the form's listing */, clock.UtcNow, ct);
    }
    catch (ProspectCapabilityDeniedException ex)
    {
        return Results.Unauthorized();
    }

    // 2. Resolve ProspectId via the new service method
    var prospect = await leasingPipeline.GetProspectByEmailAsync(
        capability.Tenant, capability.Email, ct);
    if (prospect is null)
    {
        // Verified capability + no Prospect row = data inconsistency
        // (capability was issued post-verification but Prospect was deleted?)
        // Audit-emit + return 410 Gone
        return Results.StatusCode(StatusCodes.Status410Gone);
    }

    // 3. Construct SubmitApplicationRequest from form + capability + lookup
    var request = new SubmitApplicationRequest
    {
        Tenant = capability.Tenant,
        Prospect = prospect.Id,
        Listing = formRequest.ListingId,
        Facts = formRequest.Facts,
        Demographics = formRequest.Demographics,
        ApplicationFee = formRequest.ApplicationFee,
        Signature = formRequest.SignatureRef,
    };

    // 4. Submit
    var result = await leasingPipeline.SubmitApplicationAsync(request, ct);
    return Results.Ok(result);
});
```

## Slice C acceptance criteria

- [ ] `ILeasingPipelineService.GetProspectByEmailAsync` added with full XML doc
- [ ] `IProspectRepository.FindByEmailAsync` (or equivalent) added if not present (case-insensitive email match)
- [ ] `LeasingPipelineService` reference impl
- [ ] Bridge route `POST /listings/criteria/{token}/start-application` wired
- [ ] Route emits `ProspectStartedApplication` audit (already specified in Phase 5c-4 addendum's Slice A acceptance — reaffirmed here)
- [ ] Tests:
  - Round-trip: capability → email → ProspectId → SubmitApplicationAsync → success
  - Capability tenant mismatch (already covered in Slice A; reaffirm route propagates)
  - No Prospect row for verified email → 410 Gone + audit-emit `ProspectLookupOrphan` (new AuditEventType — add to W#22's existing constant set)
  - Wrong listing in form (not in `AllowedListings`) → 401 + audit
  - Form-validation failures (missing fields, etc.) → 400 + standard validation errors

**Effort:** ~1.5h sunfish-PM time per COB estimate.

**PR title:** `feat(blocks-property-leasing-pipeline,bridge): Slice C — start-application route + GetProspectByEmailAsync (W#28 P5c-4 Slice C)`

## Halt-conditions for Slice C

- **`IProspectRepository.FindByEmailAsync` doesn't exist AND adding it requires schema migration** (e.g., email is in a child table not currently indexed): HALT + `cob-question-*-w28-p5c4-prospect-email-index.md`. Probably mechanical (add an index migration in the same PR) but worth naming.
- **`Prospect` entity doesn't have a public `Email` field** (e.g., emails are stored in a separate `ContactRecord` join): HALT + verify Prospect's actual schema; the route may need a different lookup path.
- **Slice C surfaces a real concurrency case** (two `start-application` POSTs from the same Prospect within seconds): is the W#22 service idempotent on concurrent submissions? Out of Slice C scope to enforce idempotency at the W#28 boundary; if W#22 emits two LeasingApplications for the same Prospect under contention, that's a W#22 follow-up. Note in the PR description; don't halt on it.

## Out of scope for Slice C

- `IProspectLookupService` focused interface refactor (defer until 4+ lookup methods accumulate)
- `ProspectIdentityCookie` / session-bound capability rebinding (orthogonal; Phase 2.2+ UX work)
- Capability rotation / re-issuance for stale `Prospect` rows (orthogonal; revisit if real customer scenario surfaces)

## Decision-class

Session-class per `feedback_decision_discipline` Rule 1 (NOT CO-class — pure additive extension to existing W#22 contract; COB proposed Option A as default; XO ratifies). Authority: XO; addendum follows the W#19 Phase 3 / W#21 Phase 0 / W#23 hand-off / W#28 Phase 5b + 5c-4 (top-level) addendum precedents.

---

## References

- COB beacon: `icm/_state/research-inbox/cob-question-2026-04-30T15-00Z-w28-p5c4-startapp-prospectid-seam.md`
- Phase 5c-4 addendum (top-level): `icm/_state/handoffs/property-public-listings-stage06-phase5c4-addendum.md`
- W#28 hand-off: `icm/_state/handoffs/property-public-listings-stage06-handoff.md` §"Phase 5"
- W#22 contract: `packages/blocks-property-leasing-pipeline/Services/ILeasingPipelineService.cs`
- W#28 Slice A (verifier substrate): merged in PR #382
- W#28 Slice B (GET criteria route): merged in PR #383
- ADR 0057 §"Capability promotion" + ADR 0059 §"Capability promotion (ADR 0043 addendum)"

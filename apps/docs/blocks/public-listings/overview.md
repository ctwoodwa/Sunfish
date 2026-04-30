# Public Listings

`Sunfish.Blocks.PublicListings` is the public-listing surface for the property cluster — the block that surfaces rental listings to public browsers, with structurally enforced redaction per viewer capability tier.

It implements [ADR 0059 — Public Listing Surface](../../../docs/adrs/0059-public-listing-surface.md).

## What it gives you

| Type | Role |
|---|---|
| `PublicListing` | Tenant-scoped listing with headline + description + photos + asking rent + showing-availability + redaction policy. |
| `ListingPhotoRef` | Photo with a `MinimumTier` gate; the renderer filters per viewer tier. |
| `RedactionPolicy` | Per-listing rules for address precision + financial visibility + custom field tiers. |
| `ShowingAvailability` | Open-house slots + appointment-link override per `ShowingAvailabilityKind`. |
| `IListingRepository` | CRUD: `UpsertAsync` / `GetAsync` / `GetBySlugAsync` / `ListAsync`. |
| `IListingRenderer` | **Single chokepoint** for serving listing data — projects `RenderedListing` per `RedactionTier`. |

## Three-tier viewer model

Viewers progress through three capability tiers:

| Tier | What they see |
|---|---|
| `Anonymous` | Headline + description + neighborhood-only address + `MinimumTier=Anonymous` photos. **Asking rent is always hidden.** |
| `Prospect` | + block-number address (capped by policy) + `MinimumTier=Prospect` photos. Rent shown only when `IncludeFinancialsForProspect=true`. |
| `Applicant` | + full address (capped by policy) + all photos + asking rent. |

The `RedactionPolicy.Address` setting is the upper bound on address precision; viewer tier can be more restrictive than policy but never less.

`ICapabilityPromoter` (deferred to Phase 3) promotes Anonymous → Prospect after email verification + Prospect → Applicant after a submitted application.

## `IListingRenderer` is the chokepoint

Callers MUST go through `IListingRenderer.RenderForTierAsync(tenant, id, tier, ct)` rather than reading the raw `PublicListing` from the repository. The `RenderedListing` projection has photos filtered + address redacted + financials gated structurally — it's not possible to leak un-redacted data through this path.

```csharp
var renderer = sp.GetRequiredService<IListingRenderer>();
var rendered = await renderer.RenderForTierAsync(tenantId, listingId, RedactionTier.Anonymous, ct);
// rendered.DisplayAddress is neighborhood-only;
// rendered.Photos contains only Anonymous-tier entries;
// rendered.AskingRent is null.
```

## DI bootstrap

```csharp
services.AddInMemoryPublicListings();
// Wires IListingRepository (InMemoryListingRepository),
// IListingRenderer (DefaultListingRenderer),
// + PublicListingsEntityModule (ADR 0015) — picked up by host on bootstrap.
```

## Phase 1+2 scope (shipped)

- `PublicListing` + `ListingPhotoRef` + `RedactionPolicy` + `ShowingAvailability` + IDs + enums
- `IListingRepository` + `InMemoryListingRepository`
- `IListingRenderer` + `RenderedListing` + `DefaultListingRenderer` (tier-based redaction)
- `PublicListingsEntityModule` per ADR 0015
- DI extension

## Phase 1 deviations

- `PropertyUnitId? Unit` — replaced with `UnitRef: string?` placeholder; `blocks-properties` hasn't shipped `PropertyUnit` yet. Future phase promotes to typed FK.
- `DisplayAddress` — Phase 2 stub renders `[full address: listing X]` / `[block: listing X]` / `[neighborhood: listing X]` placeholders. The listing's raw address lives on the cross-block `Property` entity; Phase 5+ wires the cross-reference.

## Out of scope (future phases)

- Phase 3 — `Foundation.Integrations.Captcha` substrate + `ICaptchaVerifier`
- Phase 4 — `ICapabilityPromoter` + macaroon-backed `ProspectCapability` (gated on ADR 0032 macaroon substrate)
- Phase 5 — Bridge route surface + ADR 0057 inquiry-form binding
- Phase 6 — Audit emission + showing-availability publishing

## See also

- [ADR 0059](../../../docs/adrs/0059-public-listing-surface.md)
- [ADR 0015](../../../docs/adrs/0015-module-entity-registration.md) — entity-module pattern
- [W#28 hand-off](../../../icm/_state/handoffs/property-public-listings-stage06-handoff.md)

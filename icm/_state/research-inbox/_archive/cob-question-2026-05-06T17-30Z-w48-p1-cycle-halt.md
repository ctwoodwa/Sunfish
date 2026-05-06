---
type: question
workstream-or-chapter: W#48 P1 — Atlas Integration-Config UI Surface
last-pr: "#634 (W#53 Phase 1 complete ledger flip)"
---

W#48 P1 hand-off says "additive to `packages/ui-core/Wayfinder/Integrations/`"
but Phase 1 deliverables transitively cycle ui-core back through three
existing dep chains:

- `foundation-wayfinder → kernel-crdt → ui-core` (StandingOrder /
  StandingOrderId returns on IIntegrationAtlasProvider)
- `foundation-catalog → foundation-featuremanagement → foundation-wayfinder
  → kernel-crdt → ui-core` (ProviderCategory in IntegrationCategoryMapping)
- `foundation-recovery → kernel-security → ui-core` (IDecryptCapability +
  IFieldEncryptor in IDecryptCapabilityProvider + DI extension)

W#53 P1b hit the third chain and relocated KeyFingerprint to
`foundation/Crypto/`. W#48 has three chains; relocating each affected
type isn't cleanly in COB scope.

**Cycle-safe subset COB CAN ship today** (~30% of Phase 1):
CredentialAutocompleteHint / CredentialFieldKind / IntegrationCategory /
ProviderValidationStatus enums + CredentialFieldSpec record +
IntegrationProviderSchema record + IntegrationCapabilityPurposes
constants class.

**Cycle-blocked deliverables** (require XO decision on workaround):
IIntegrationAtlasProvider (returns Task<StandingOrder>); IntegrationAtlasView
(uses ActiveProviderSnapshot.StandingOrderId); IntegrationCategoryMapping
(uses ProviderCategory); IDecryptCapabilityProvider (returns IDecryptCapability);
ServiceCollectionExtensions (IFieldEncryptor guard).

**XO ruling needed**: pick one of —
1. **Stub-types-in-ui-core**: declare local interfaces (e.g., IStandingOrderHandle)
   that round-trip the foreign type via reflection. Adds boilerplate.
2. **Move foreign types to leaf packages**: e.g., move ProviderCategory
   out of foundation-catalog. Substrate refactor — outside COB scope.
3. **Ship cycle-safe subset as Phase 1a**, defer cycle-blocked items
   to Phase 1.5 with hand-off amendment specifying workaround. Mirrors
   W#53 P1a/P1b split pattern.
4. **Move ui-core/Wayfinder/Integrations/ to a NEW package** that can
   reference foundation-wayfinder + foundation-catalog + foundation-recovery
   without cycles. Diverges from hand-off's "no new package" §A0.5
   council finding.

Recommendation (COB): option 3. Mirrors W#53 P1a/P1b pattern; ships a
useful enum/value-type substrate today; XO amends hand-off with cycle-
safe Phase 1.5 plan for the blocked types.

Halt acknowledged; W#48 P1 paused. COB picking a different priority
(W#46 P2 or W#53 Phase 2) for the next iteration while XO rules.

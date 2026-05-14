---
sort_order: 50
number: 48
slug: atlas-integration-config-ui-surface
title: "**Atlas Integration-Config UI Surface** (ADR 0067; W#34 follow-on; `sunfish-feature-change` pipeline)"
status: "building"
status_cell: "`building` (Phase 1a shipped 2026-05-06 PR #640; P1.5 PR1 StandingOrderId shipped PR #641; P1.5 PR2 IDecryptCapability shipped PR #642; **Phase 1b shipped 2026-05-06 PR #660** — IIntegrationAtlasProvider + IntegrationAtlasView + ActiveProviderSnapshot + IDecryptCapabilityProvider + AddSunfishIntegrationAtlas() + 4 AuditEventType + ContractSurfaceTests; NOTE: IssueXxxAsync returns Task<StandingOrderId> (ui-core cycle constraint); **Phase 2 SHIPPED 2026-05-13 PR #829** — DefaultIntegrationAtlasProvider + InMemoryIntegrationAtlasProvider + AddSunfishIntegrationAtlasDefaults() + SUNFISH_INTEGRATION_AUDIT001 analyzer + 25 tests; security council CONDITIONAL-PASS; decrypt-fail-closed fix applied; **Phase 3a COMPLETE 2026-05-13** — providers-mesh-headscale (MeshVpn) + providers-recaptcha (Captcha) verified available on origin/main; **Phase 3b next** — HeadscaleMeshVpnSchemaProvider/Validator + RecaptchaV3SchemaProvider/IntegrationValidator in adapter packages)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/atlas-integration-config-stage06-handoff.md` + `icm/_state/handoffs/atlas-integration-config-p2-blocks-integrations-addendum.md` (XO ruling: Phase 2 impl → `blocks-integrations` package) + `docs/adrs/0067-atlas-integration-config-surface.md` (PR #539 merged)"
---

## Notes

**Phase 1a shipped 2026-05-06 PR #640.** 16 files in
`packages/ui-core/Wayfinder/Integrations/` — enums + CredentialFieldSpec +
IntegrationProviderSchema + IIntegrationAtlasContext + IIntegrationProviderValidator
+ ICustomIntegrationRenderer + IValidationStatusStore + IIntegrationSchemaProvider
+ IntegrationCapabilityPurposes + IntegrationAtlasContractTests.cs.

**Phase 1.5 COMPLETE 2026-05-06** — both cycle-break moves shipped:
PR #641 `StandingOrderId` + `AuditRecordId` → `foundation/Assets/Common/`;
PR #642 `IDecryptCapability` → `foundation/Crypto/`.

**Phase 1b SHIPPED 2026-05-06 PR #660** — `IIntegrationAtlasProvider` +
`IntegrationAtlasView` + `ActiveProviderSnapshot` + `IDecryptCapabilityProvider` +
`AddSunfishIntegrationAtlas()` + 4 `AuditEventType` constants + `ContractSurfaceTests`
on origin/main. DIVERGENCE: `IIntegrationAtlasProvider.IssueXxxAsync` methods return
`Task<StandingOrderId>` (NOT `Task<StandingOrder>`) — second cycle
`ui-core → foundation-wayfinder → kernel-crdt → ui-core` prevents returning the full
`StandingOrder` aggregate. `DefaultIntegrationAtlasProvider` in Phase 2 must extract
the `StandingOrderId` from `IStandingOrderIssuer.IssueAsync` and return it directly.
**Phase 2 NOW UNBLOCKED** — read `atlas-integration-config-p2-blocks-integrations-addendum.md`
before starting.

**Phase 2 CYCLE RESOLVED — XO ruling 2026-05-06:** `DefaultIntegrationAtlasProvider`
goes in new `packages/blocks-integrations/` package (NOT `ui-core`). Full
architectural spec at `atlas-integration-config-p2-blocks-integrations-addendum.md`.
COB MUST read addendum before starting Phase 2.

**`IAtlasProvider<TView>` is invariant** (W#53 P1a council resolution —
hand-off cited `out TView` but C# CS1961 rejects on `Task<T>` returns).
Concrete W#48 `IIntegrationAtlasProvider` derives directly from
`IAtlasProvider<IntegrationAtlasView>` without covariant downcast.

Key new types: `IIntegrationAtlasProvider` + `IIntegrationAtlasContext`
+ `IntegrationProviderSchema` + `IIntegrationSchemaProvider` +
`IIntegrationProviderValidator` + `IValidationStatusStore` +
`IDecryptCapabilityProvider` + `IntegrationCapabilityPurposes`. No new
package (additive to `packages/ui-core/Wayfinder/Integrations/`).

**Phase 1 restructured (2026-05-06 per COB question #636):** Three
dependency cycles block some Phase 1 types. New sequence:
- **Phase 1a** (ship now, cycle-safe): enums + value types + constants +
  `IIntegrationAtlasContext` + `IIntegrationProviderValidator` +
  `ICustomIntegrationRenderer` + `IValidationStatusStore`
- **Phase 1.5** (cycle-break moves): `StandingOrderId` + `AuditRecordId`
  → `foundation/Assets/Common/`; `IDecryptCapability` → `foundation/Crypto/`.
  Hand-off at `icm/_state/handoffs/atlas-integration-config-p15-cycle-break-handoff.md`.
- **Phase 1b** (after Phase 1.5 merged): `IIntegrationAtlasProvider` +
  `IntegrationAtlasView` + `ActiveProviderSnapshot` +
  `IDecryptCapabilityProvider` + `AddSunfishIntegrationAtlas()` +
  4 `AuditEventType` constants + `ContractSurfaceTests`.

5 build phases: P1a/1.5/1b → P2 reference impl + audit +
SUNFISH_INTEGRATION_AUDIT001 analyzer; P3a/3b; P4 Anchor+Bridge; P5 docs.
~26-38h / ~7-10 PRs.

**Phase 2 SHIPPED 2026-05-13 PR #829.** `packages/blocks-integrations/`:
`DefaultIntegrationAtlasProvider` + `InMemoryIntegrationAtlasProvider` +
`AddSunfishIntegrationAtlasDefaults()` DI extension + `SUNFISH_INTEGRATION_AUDIT001`
Roslyn analyzer in `foundation-wayfinder-analyzers`. 25/25 tests pass. Security
council CONDITIONAL-PASS; one High gap (decrypt-failure fail-open) applied before
merge. Medium gaps deferred to follow-ups.

**Phase 3a — Provider availability gate (2026-05-13).**
Verified providers on origin/main after PR #829:

| Provider pkg | IntegrationCategory | Available? | Notes |
|---|---|---|---|
| `providers-mesh-headscale` | `MeshVpn` | ✓ | `HeadscaleAdapter` + audit-emission tests |
| `providers-recaptcha` | `Captcha` | ✓ | `RecaptchaV3CaptchaVerifier` + config |
| `providers-stripe` | `Payments` | ✗ | W#5 commercial Phase 2 — not yet built |
| `providers-square` | `Payments` | ✗ | W#5 — not yet built |
| `providers-sendgrid` | `TransactionalEmail` | ✗ | W#22 leasing-pipeline Phase 2+ |
| `providers-postmark` | `TransactionalEmail` | ✗ | W#22 — not yet built |
| `providers-mailchimp` | `MarketingEmail` | ✗ | W#22 — not yet built |
| `providers-twilio` | `Messaging` | ✗ | W#5/W#22 — not yet built |
| `providers-hcaptcha` | `Captcha` | ✗ | Not separate pkg; hCaptcha not yet built |

**Phase 3b scope** (2 providers ready): `HeadscaleMeshVpnSchemaProvider` +
`HeadscaleMeshVpnValidator` in `providers-mesh-headscale`; `RecaptchaV3SchemaProvider`
+ `RecaptchaV3IntegrationValidator` in `providers-recaptcha`.

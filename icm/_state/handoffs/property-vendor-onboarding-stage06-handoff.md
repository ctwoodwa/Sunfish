# Workstream #18 — Vendor Onboarding Posture (EXTEND `blocks-maintenance`) — Stage 06 hand-off

**Workstream:** #18 (`Vendor` onboarding extension — W-9 capture + magic-link flow + multi-contact + performance log + capability gradient)
**Spec:** [ADR 0058](../../docs/adrs/0058-vendor-onboarding-posture.md) (Accepted 2026-04-29; A1–A8 amendments landed PR #295)
**Pipeline variant:** `sunfish-api-change` (per A2 — `Vendor` record shape change is breaking) for Phase 1; `sunfish-feature-change` for Phases 2–8
**Estimated effort:** 12–16h focused sunfish-PM time
**Decomposition:** 8 phases shipping as ~6 separate PRs
**Prerequisites:**
- ✓ ADR 0058 Accepted + A1–A8 landed (PR #295)
- ✓ ADR 0046-A2/A3/A4/A5 substrate spec landed (PR #333) — `EncryptedField` + `IFieldDecryptor` substrate
- ✓ ADR 0049 audit substrate built
- ✓ ADR 0056 (Foundation.Taxonomy) Phase 1 built (PR #263) — for `Vendor.Specialties` taxonomy reference
- ✓ ADR 0054 (Electronic Signatures) Accepted; W#21 `ready-to-build` — for `SignatureEvent` capture
- 🟡 W#32 (`EncryptedField` + `IFieldDecryptor` substrate) `ready-to-build` (PR #337) — **Phase 4 gated on W#32 build complete**
- 🟡 W#20 (Bidirectional Messaging) Phases 2.1+ shipped (PRs #273, #276, #294, #302) — **Phase 5 gated on W#20 magic-link delivery contracts**
- 🟡 W#21 (Signatures) `ready-to-build` — **Phase 5 gated on W#21 Phase 1+ for `ISignatureCapture`**

---

## Scope summary

Per ADR 0058 §"Decision" + §"Initial contract surface" + cluster reconciliation 2026-04-28 (EXTEND not NEW per cluster Rule 2 — existing `blocks-maintenance.Vendor` ships ~85% of cluster scope). This hand-off extends the existing `blocks-maintenance` package additively (with one breaking change to the `Vendor` record shape per A2).

1. **`Vendor` record extension** — additive new init-only fields (`OnboardingState` + `W9` ref + `PaymentPreference` ref + `Specialties` typed via taxonomy + `Contacts` list); positional → init-only constructor migration per A2 (api-change)
2. **`VendorContact` child entity** — multi-contact-per-vendor; per-property primary override
3. **`VendorPerformanceRecord` append-only event log** — sourced from work-order completion events (W#19 emits; W#18 consumes via projection)
4. **`W9Document` entity + `EncryptedField` TIN encryption** — consumes W#32 substrate (`IFieldEncryptor` + `IFieldDecryptor`); audit-on-decrypt per ADR 0049
5. **`VendorMagicLink` + onboarding flow** — Bridge form delivery via W#20 messaging substrate; HMAC-SHA256 token per ADR 0058 capability-gradient table; W-9 acknowledgment signature via W#21 `ISignatureCapture`
6. **`Sunfish.Vendor.Specialties@1.0.0` taxonomy charter + seed** — replaces existing `VendorSpecialty` enum per ADR 0058 cross-package wiring (Foundation.Taxonomy)
7. **Audit emission** — 7 new `AuditEventType` constants + `VendorAuditPayloadFactory` matching W#31/W#19/W#27 conventions
8. **Cross-package wiring + apps/docs + ledger flip**

**NOT in scope** (deferred to follow-up hand-offs / ADRs / phases):
- Vendor-with-portal tier (Phase 4+ per ADR 0058 capability-gradient table; OIDC-bound Bridge account)
- Vendor 1099-NEC year-end report generation (Phase 2.3 per Phase 2 commercial intake; `blocks-tax-reporting`)
- Vendor marketplace / vendor discovery (Phase 4+)
- Vendor bidding workflow / 3-quote comparison (Phase 2 commercial scope)
- Insurance certificate verification automation (manual upload + reminder only in Phase 2)

---

## Phases

### Phase 1 — `Vendor` record api-change + auxiliary IDs + `OnboardingState` (~2.5h, **api-change pipeline**)

Per ADR 0058 §"Initial contract surface" Vendor extension. Migrate `Vendor` from positional record to init-only record per A2. Add new init-only fields: `OnboardingState` (required), `W9` (nullable), `PaymentPreference` (nullable), `Specialties` (defaulted empty list of `TaxonomyClassification`), `Contacts` (defaulted empty list of `VendorContactId`).

**Breaking changes:**
- `Vendor` constructor signature changes (positional → init-only); existing callers in `blocks-maintenance` + `apps/kitchen-sink` need migration
- Existing `Vendor.Specialty` (singular `VendorSpecialty` enum) → `Vendor.Specialties` (list of `TaxonomyClassification`); enum values preserved as taxonomy nodes (Phase 6)
- `MIGRATION.md` updated documenting v0.x → v1.x for `blocks-maintenance` (matches W#19 v0.x → v1.0 precedent in PR #301)

**Files to create/modify:**
- `packages/blocks-maintenance/Models/Vendor.cs` — add new init-only fields; migrate constructor
- `packages/blocks-maintenance/Models/VendorOnboardingState.cs` — enum (Pending / W9Requested / W9Received / Active / Suspended / Retired)
- `packages/blocks-maintenance/Models/VendorContactId.cs`, `VendorPerformanceRecordId.cs`, `W9DocumentId.cs`, `VendorMagicLinkId.cs` — value-record-struct ID types
- `packages/blocks-maintenance/MIGRATION.md` — append v0.x → v1.x section
- Update existing `blocks-maintenance` callers (1-2 sites in DI extension + kitchen-sink seed)

**Gate:** PASS iff `dotnet build` clean across all consumers + 5+ tests verifying new field defaults + existing 69 tests continue passing.

**PR title:** `feat(blocks-maintenance)!: Vendor record api-change for onboarding extension (W#18 Phase 1, ADR 0058)`

### Phase 2 — `VendorContact` child entity (~1.5h)

Per ADR 0058 contract surface. Multi-contact-per-vendor with per-property primary override.

**Files:**
- `packages/blocks-maintenance/Models/VendorContact.cs` — record per ADR 0058
- `packages/blocks-maintenance/Services/IVendorContactService.cs` — `AddContactAsync`, `UpdateContactAsync`, `RemoveContactAsync`, `ListContactsAsync(VendorId)`, `GetPrimaryForPropertyAsync(VendorId, PropertyId?)`
- `packages/blocks-maintenance/Services/InMemoryVendorContactService.cs` — reference impl
- DI extension: `services.AddSingleton<IVendorContactService, InMemoryVendorContactService>()`
- Tests: contact CRUD + per-property primary override + isolated-by-tenant

**Gate:** PASS iff service tests verify CRUD + property-override semantics + 8+ tests pass.

**PR title:** `feat(blocks-maintenance): VendorContact child entity + per-property primary (W#18 Phase 2, ADR 0058)`

### Phase 3 — `VendorPerformanceRecord` append-only event log (~2h)

Per ADR 0058 contract surface. Append-only event log; sourced from work-order completion events (W#19 emits; W#18 projects).

**Files:**
- `packages/blocks-maintenance/Models/VendorPerformanceRecord.cs` — record per ADR 0058
- `packages/blocks-maintenance/Models/VendorPerformanceEvent.cs` — enum (Hired / JobCompleted / JobNoShow / JobLate / JobCancelled / RatingAdjusted / InsuranceLapse / Suspended / Retired)
- `packages/blocks-maintenance/Services/IVendorPerformanceLog.cs` — `AppendAsync`, `ListByVendorAsync(VendorId, paging)`, `ProjectFromWorkOrderAsync(WorkOrderId, VendorPerformanceEvent)`
- `packages/blocks-maintenance/Services/InMemoryVendorPerformanceLog.cs`
- DI extension registers the log
- Tests: append + projection from work-order events + paging + chronological order preservation

**Gate:** PASS iff event-log tests pass; cross-block projection from `WorkOrder.Status = Completed` audit-event correctly creates `VendorPerformanceEvent.JobCompleted`.

**PR title:** `feat(blocks-maintenance): VendorPerformanceRecord event log + work-order projection (W#18 Phase 3, ADR 0058)`

### Phase 4 — `W9Document` + `EncryptedField` TIN integration (~3h, **gated on W#32 build complete**)

Per ADR 0058 contract surface + cross-package wiring. Consumes W#32 (`IFieldEncryptor` + `IFieldDecryptor`).

**Halt-condition:** Phase 4 must NOT start until W#32 has shipped (`packages/foundation-recovery/Crypto/IFieldEncryptor.cs` + `IFieldDecryptor.cs` + `EncryptedField.cs` + `TenantKeyProviderField{En,De}cryptor.cs` exist on `origin/main`). Per W#32 hand-off addendum, the substrate is 4 phases / ~3h total — likely shipped within 1-2 COB cycles.

**Files:**
- `packages/blocks-maintenance/Models/W9Document.cs` — record per ADR 0058 (`TinEncrypted : EncryptedField`)
- `packages/blocks-maintenance/Models/W9TaxClassification.cs` — enum (Individual / LLC / SCorp / CCorp / Partnership / Trust / Other)
- `packages/blocks-maintenance/Services/IW9DocumentService.cs` — `CreateAsync(VendorId, plaintext-tin, ...)` (calls `IFieldEncryptor.EncryptAsync` internally), `GetAsync(W9DocumentId, IDecryptCapability)` (returns `W9DocumentView` with decrypted-only-on-demand semantics), `VerifyAsync(W9DocumentId, ActorId)`
- `packages/blocks-maintenance/Services/InMemoryW9DocumentService.cs` — reference impl; takes `IFieldEncryptor` + `IFieldDecryptor` via DI
- DI extension: registers W9DocumentService; `IFieldEncryptor` + `IFieldDecryptor` come from `AddSunfishRecoveryCoordinator()` (already registered by W#32 hand-off)
- Tests: encrypt+decrypt round-trip via service; capability-gating; cross-tenant isolation; audit emission via test double

**Gate:** PASS iff W9Document round-trip works + decrypt with invalid capability throws + audit emission verified.

**PR title:** `feat(blocks-maintenance): W9Document + EncryptedField TIN integration (W#18 Phase 4, ADR 0058 + ADR 0046-A2/A4/A5)`

### Phase 5 — `VendorMagicLink` + onboarding flow Bridge form (~3h, **gated on W#20 Phase 2.1 + W#21 Phase 1**)

Per ADR 0058 §"Onboarding flow" + capability-gradient table. Consumes W#20 messaging substrate (magic-link delivery via `IMessagingGateway`) + W#21 signatures (`ISignatureCapture` for vendor W-9 acknowledgment).

**Halt-conditions:**
- W#20 Phase 2.1 must be shipped (`Sunfish.Foundation.Integrations.Messaging` + `IOutboundMessageGateway`) — verified ✓ on `origin/main` (PR #273)
- W#21 Phase 1 must be shipped (`Sunfish.Kernel.Signatures.ISignatureCapture` + canonicalization per ADR 0054 A1)
- If W#21 isn't shipped at Phase 5 start: HALT + `cob-question-*-w18-signatures-prereq.md`. Phase 5's signature-acknowledgment step is core scope; cannot stub.

**Files:**
- `packages/blocks-maintenance/Models/VendorMagicLink.cs` — record per ADR 0058
- `packages/blocks-maintenance/Models/MagicLinkPurpose.cs` — enum (OnboardForm / WorkOrderResponse / ReinviteW9)
- `packages/blocks-maintenance/Services/IVendorMagicLinkService.cs` — `IssueAsync(VendorId, MagicLinkPurpose, TimeSpan? ttl)`, `ConsumeAsync(string token)` (HMAC verify + TTL + not-yet-consumed); `RevokeAsync(VendorMagicLinkId)`
- `packages/blocks-maintenance/Services/HmacVendorMagicLinkService.cs` — HMAC-SHA256 over secret + vendor_id + issued_at; consumes `ITenantKeyProvider` for HMAC secret derivation (purpose label `vendor-magic-link-hmac` — net-new label, document in interface-impl xmldoc)
- Bridge integration: new Razor pages under `apps/bridge/Pages/Vendor/Onboard/{token}.razor` (token validation + W-9 form + signature-capture-page redirect)
- W-9 form posts plaintext-TIN to a server-side controller that:
  1. Validates token via `IVendorMagicLinkService.ConsumeAsync`
  2. Captures signature via `ISignatureCapture.CaptureAsync` with `SignatureScope = Sunfish.Signature.Scopes/vendor-w9-acknowledgment` (post-W#31 + W#21)
  3. Calls `IW9DocumentService.CreateAsync` (encrypts TIN via `IFieldEncryptor`)
  4. Flips `Vendor.OnboardingState = W9Received`

**Gate:** PASS iff end-to-end onboarding test fires: magic-link issued → consumed → W9 captured + signed → state flipped to W9Received.

**PR title:** `feat(blocks-maintenance,bridge): VendorMagicLink + onboarding form (W#18 Phase 5, ADR 0058)`

### Phase 6 — `Sunfish.Vendor.Specialties@1.0.0` taxonomy charter + seed (~1.5h)

Per ADR 0056 + ADR 0058 cross-package wiring. Replaces existing `VendorSpecialty` enum; existing values become Phase-6 taxonomy nodes.

**Files:**
- `icm/00_intake/output/sunfish-vendor-specialties-v1-charter-2026-04-30.md` — taxonomy charter (vendor specialty hierarchy: HVAC, Plumbing, Electrical, Landscaping, Cleaning, Roofing, etc., with sub-categories)
- `packages/foundation-taxonomy/Seeds/SunfishVendorSpecialties.cs` — extends existing `TaxonomyCorePackages` with `VendorSpecialties => new TaxonomyCorePackage(...)`
- `packages/foundation-taxonomy/tests/SunfishVendorSpecialtiesSeedTests.cs` — verify charter ↔ seed match
- Migration of existing `VendorSpecialty` enum values into the seed (Authoritative-regime per ADR 0056)
- Update `Vendor.Specialties` field type from `IReadOnlyList<VendorSpecialty>` to `IReadOnlyList<TaxonomyClassification>` (already done in Phase 1; this phase ships the canonical taxonomy)

**Gate:** PASS iff seed loads via `RegisterCorePackageAsync` + 13+ specialty nodes resolve correctly via `ITaxonomyResolver`.

**PR title:** `feat(foundation-taxonomy): Sunfish.Vendor.Specialties@1.0.0 charter + seed (W#18 Phase 6)`

### Phase 7 — Audit emission (7 `AuditEventType` constants) (~1.5h)

Per ADR 0058 §"Onboarding flow" audit-emit calls + ADR 0049.

**Files:**
- `packages/kernel-audit/AuditEventType.cs` — add 7 new constants:
  - `VendorCreated`, `VendorMagicLinkIssued`, `VendorMagicLinkConsumed`, `VendorOnboardingStateChanged`, `W9DocumentReceived`, `W9DocumentVerified`, `VendorActivated`
  (Existing `BookkeeperAccess` + `TaxAdvisorAccess` from kernel-audit cover TIN-decrypt audit semantics — Phase 4 already emits via W#32's `FieldDecrypted`)
- `packages/blocks-maintenance/Audit/VendorAuditPayloadFactory.cs` — 7 factory methods returning `AuditPayload` matching W#31 / W#19 / W#27 conventions
- `packages/blocks-maintenance/tests/VendorAuditEmissionTests.cs` — verify emission via `IAuditTrail` test double on each onboarding-flow transition; schema snapshot tests on alphabetized payload keys

**Gate:** PASS iff 7 schema-snapshot tests pass + onboarding-flow emission verified end-to-end.

**PR title:** `feat(kernel-audit,blocks-maintenance): Vendor onboarding audit emission (W#18 Phase 7, ADR 0058)`

### Phase 8 — Cross-package wiring + apps/docs + ledger flip (~1.5h)

**Files:**
- `apps/docs/blocks/maintenance/vendor-onboarding.md` — per ADR 0058 §"Onboarding flow" diagram + capability-gradient table + magic-link UX
- `apps/kitchen-sink/Pages/Vendor/Onboarding.razor` — demo page composing Phase 1-5 pieces
- DI bootstrap update: ensures `AddSunfishRecoveryCoordinator()` runs before `AddSunfishMaintenance()` (W#32 substrate must register first)
- Update `icm/_state/active-workstreams.md` row #18 → `built`. Append last-updated entry.

**Gate:** PASS iff kitchen-sink demo page renders end-to-end + apps/docs page covers all 5 sections.

**PR title:** `chore(icm,docs,kitchen-sink): W#18 Phase 8 wiring + apps/docs + ledger flip`

---

## Total decomposition

| Phase | Subject | Hours |
|---|---|---|
| 1 | Vendor record api-change + IDs + OnboardingState | 2.5 |
| 2 | VendorContact child entity | 1.5 |
| 3 | VendorPerformanceRecord event log | 2.0 |
| 4 | W9Document + EncryptedField TIN (gated on W#32) | 3.0 |
| 5 | VendorMagicLink + onboarding flow (gated on W#20 P2.1 ✓ + W#21 P1) | 3.0 |
| 6 | Sunfish.Vendor.Specialties@1.0.0 taxonomy charter + seed | 1.5 |
| 7 | Audit emission (7 AuditEventType + factories) | 1.5 |
| 8 | Cross-package wiring + apps/docs + ledger flip | 1.5 |
| **Total** | | **~16.5 h** |

---

## Halt conditions

Per `feedback_decision_discipline` Rule 6 + Decision Discipline Rule 1 CO-class filter, name SPECIFIC scenarios that should trigger a `cob-question-*` beacon instead of in-session resolution:

- **W#32 substrate types not yet shipped at Phase 4 start** (`EncryptedField` / `IFieldEncryptor` / `IFieldDecryptor` not present on `origin/main`) → write `cob-question-*-w18-w32-prereq.md`; halt Phase 4. Phases 1-3 + 6-7 can ship in parallel.
- **W#21 Phase 1+ signatures not yet shipped at Phase 5 start** (`ISignatureCapture` not present) → write `cob-question-*-w18-w21-prereq.md`; halt Phase 5. W#21 is `ready-to-build`; XO follows up if it stalls.
- **Existing `VendorSpecialty` enum has runtime callers outside `blocks-maintenance`** (Phase 6 migration discovers cross-package consumers) → halt; XO reviews + adds an addendum or stages migration.
- **Bridge form integration requires new infrastructure** (Phase 5 needs Bridge routes that don't exist) → halt; XO confirms with W#28 Public Listings ownership boundary (W#28 owns the Bridge route family).
- **`MIGRATION.md` for `blocks-maintenance` v0.x → v1.x conflicts with W#19's existing v0.x → v1.0 migration** (Phase 1) → halt; XO reviews + sequences the version bump (likely v1.0 → v1.1 for W#18 since W#19's v1.0 already shipped).
- **Phase 5 magic-link Bridge form needs CSRF / anti-forgery integration that doesn't exist** → halt; XO addendum (CSRF is canonical .NET pattern per industry defaults; missing it would be a Bridge-side gap).

---

## Acceptance criteria (cumulative)

- [ ] All Phase 1-8 acceptance criteria pass
- [ ] `Vendor` record migrated to init-only with new fields (Phase 1)
- [ ] `VendorContact` + `VendorPerformanceRecord` + `W9Document` + `VendorMagicLink` entities exist with full XML doc + nullability + `required`
- [ ] All services have full XML doc on interface methods + parameters
- [ ] `Vendor.Specialties` consumes `Sunfish.Vendor.Specialties@1.0.0` taxonomy via `ITaxonomyResolver`
- [ ] `W9Document.TinEncrypted : EncryptedField` round-trips correctly with capability check + audit emission
- [ ] Magic-link onboarding end-to-end test passes (issue → email → consume → W-9 capture → sign → state flip)
- [ ] 7 `AuditEventType` constants emitted with correct payload-body schemas
- [ ] kitchen-sink demo + apps/docs page complete
- [ ] All tests pass; build clean; no analyzer warnings
- [ ] Ledger row #18 → `built`

---

## References

- **Spec:** [ADR 0058](../../docs/adrs/0058-vendor-onboarding-posture.md) — vendor onboarding posture (Accepted; A1–A8 landed)
- **Substrate dependencies:** [ADR 0046-A2/A3/A4/A5](../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md) — `EncryptedField` + `IFieldDecryptor`; W#32 hand-off addendum at `icm/_state/handoffs/adr-0046-a2-encrypted-field-stage06-addendum.md`
- **Companion ADRs:** [ADR 0049](../../docs/adrs/0049-foundation-audit.md) — audit pattern; [ADR 0052](../../docs/adrs/0052-bidirectional-messaging-substrate.md) — magic-link delivery; [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — vendor signature; [ADR 0056](../../docs/adrs/0056-foundation-taxonomy-substrate.md) — Vendor.Specialties taxonomy
- **Cluster reconciliation:** [`property-ops-cluster-vs-existing-reconciliation-2026-04-28.md`](../../icm/07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md) §18 — disposition reframe to EXTEND
- **Pattern references:** W#19 v0.x → v1.0 migration (PR #301); W#31 audit factory pattern (PR #263); W#19 cross-package wiring (PR #314)

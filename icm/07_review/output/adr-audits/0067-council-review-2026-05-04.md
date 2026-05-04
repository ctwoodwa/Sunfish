# ADR 0067 — Atlas Integration-Config UI Surface — Council Review

**Date:** 2026-05-04
**Reviewer:** XO research session (Opus 4.7, xhigh)
**Review type:** Canonical pre-merge Stage 1.5 council (per ADR 0069 D1 — substrate-tier)
**ADR branch under review:** `docs/adr-0067-atlas-integration-config` (PR #539)
**Worktree base:** `origin/main` at `b996bc8` (chore(icm): update W#23 ledger)
**Posture:** standard 4-perspective adversarial + WCAG/a11y subagent perspective + security-engineering perspective. Subagent dispatch deferred per CO instruction (council perspectives folded into single-session synthesis at xhigh).

---

## TL;DR

**Verdict: BLOCK pre-merge.** The ADR is structurally and conceptually well-shaped — it correctly extends ADR 0066's `IAtlasProvider<T>` contract, the schema-driven Option-B framing is the right call vs Option A's per-adapter chaos, the audit-by-construction posture and `EncryptedField` storage path are aligned with the cohort's substrate disciplines, and §A0 caught a substantial portion of the intake-stub drift. **However**, the §A0 audit missed three high-impact structural-citation failures that would compile-break Phase 1 the moment hand-off lands, and one decision-driver invariant (license-acknowledgement for SSPL/BSL) directly contradicts ADR 0061's actual posture. Plus a package-shape contradiction with ADR 0066 (PR #529), a non-trivial validation-flow gap (no `ValidateAsync` exists on the integration-egress contracts the ADR claims to call), and several substrate-citation type confusions (PrincipalId vs ActorId; AuditRecord.Payload shape; missing capability-token sourcing for IFieldDecryptor).

**Finding count by classification:**

- **Mechanical:** 8 (rename, fix-citation, scope-tightening — auto-acceptable)
- **Non-mechanical:** 5 (require author judgment; reframe contracts or invariants)
- **Structural-citation:** 6 (cited symbols don't exist or have different shape — author discipline failures the §A0 audit missed)
- **Blocking:** 4 (cannot merge as-is; must be addressed in this ADR or its hand-off cannot compile)

**Top three highest-impact findings:**

1. **`IPaymentGateway.ValidateAsync` and `IMessagingGateway.ValidateAsync` do not exist on origin/main** (BLOCKING + structural-citation). §6.2 names both as the per-category validation hook, but the actual contracts have only `AuthorizeAsync/CaptureAsync/RefundAsync` (payments) and `SendAsync/GetStatusAsync` (messaging). The validation flow as designed cannot dispatch.
2. **§A0.4 falsely claims `IMeshVpnAdapter` is uncommitted** when it is in fact present on `origin/main` at `packages/foundation-transport/IMeshVpnAdapter.cs` — and even where present, it has no `ValidateAsync` method, so §6.2's mesh-VPN dispatch path is also broken (BLOCKING + structural-citation).
3. **License-acknowledgement contract directly contradicts ADR 0061** (BLOCKING + non-mechanical). ADR 0061 explicitly *excludes* SSPL/BSL adapters with a `BannedSymbols.txt` analyzer enforcement; there is no acknowledgement opt-in path. ADR 0067 §3.3 / §5.5 invent a `StrongCopyleft` posture with admin-acknowledgement-then-activate UX that ADR 0061 does not authorize. Either ADR 0061 needs an amendment or the entire license-acknowledgement track in ADR 0067 needs to be cut.

---

## §1 — Pressure-test point dispositions

These are the five §A0-flagged points the author asked council to resolve.

### §1.1 — ADR 0066 namespace (`Sunfish.UICore.Wayfinder` or whatever ADR 0066 ships)

**Disposition: PARTIAL — namespace correct; package shape contradicted.**

ADR 0066 PR #529 (open) explicitly adds `Sunfish.UICore.Wayfinder` as **a new namespace within the existing `packages/ui-core/` package** ("additive, no new package required"). Council CONFIRMS this on the PR diff.

ADR 0067 §"Decision" + §3 + §10 Phase 1 declare a **net-new `packages/ui-core-wayfinder/` package** "alongside the package introduced by ADR 0066." This is structurally inconsistent with ADR 0066's own decision. Either:

- ADR 0067 is wrong and should declare itself additive to `packages/ui-core/Wayfinder/Integrations/` (council recommendation — preserves ADR 0066's no-new-package framing); or
- ADR 0067 is consciously deviating from ADR 0066's "no-new-package" framing and should call that out as a §"Considered options" trade-off (with rationale: separate package boundary keeps adapter-package fan-in clean).

**Recommendation:** keep the surface in `packages/ui-core/Wayfinder/Integrations/` with namespace `Sunfish.UICore.Wayfinder.Integrations`; do not introduce a separate `packages/ui-core-wayfinder/` package unless explicitly justified. Mechanical fix.

### §1.2 — `IMeshVpnAdapter.ValidateAsync` is uncommitted in W#30 — confirm method shape

**Disposition: BLOCKING — claim is doubly false.**

§A0.4 asserts: "`IMeshVpnAdapter` is uncommitted in the working tree at `packages/foundation-transport/IMeshVpnAdapter.cs` (W#30 build in flight)."

Council verification: `IMeshVpnAdapter` IS on `origin/main` at exactly that path. The full surface is:

```csharp
public interface IMeshVpnAdapter : IPeerTransport
{
    string AdapterName { get; }
    Task<MeshNodeStatus> GetMeshStatusAsync(CancellationToken ct);
    Task RegisterDeviceAsync(MeshDeviceRegistration registration, CancellationToken ct);
}
```

There is no `ValidateAsync` method. §6.2's dispatch claim ("the validator calls into the adapter's own `IMeshVpnAdapter.ValidateAsync()`") will not compile. The author's mitigation note in §A0.4 ("if `IMeshVpnAdapter` lands without a `ValidateAsync` method, the Phase 3 hand-off carries an instruction to add one") tacitly acknowledges this risk but cannot ship — adding `ValidateAsync` to `IMeshVpnAdapter` is a breaking change to a published transport contract that requires its own ADR amendment.

**Recommendation:** rework §6.2 so mesh-VPN validation is owned by `IIntegrationProviderValidator` itself, performing whatever liveness probe the validator deems appropriate against the configured control-plane URL (a TCP reachability + a control-plane handshake). Do not extend `IMeshVpnAdapter`. Non-mechanical (changes the contract surface).

### §1.3 — `AuditEventType` redaction-rule corpus test boundary

**Disposition: ACCEPTABLE — but specify the assertion mechanism.**

§8 redaction rule names the forbidden field-name patterns (`value`, `apiKey`, `secret`, `password`, `token`, `webhookSecret`, `credential.*`, `*.value`). The corpus test should be:

- A representative `AuditPayload.Body` corpus (the dictionary structure — recall `AuditRecord.Payload` is `SignedOperation<AuditPayload>` where `AuditPayload(IReadOnlyDictionary<string, object?> Body)`).
- An assertion that walks the body recursively (object values, list values, nested dictionary values) checking each key against the forbidden patterns.
- A negative-test set covering edge cases: a key like `previousProvider` with value containing literal text "secret" (must pass — only key names are forbidden), a key like `details.value` (must fail — ends-with `.value`), a key `webhook-secret` (must fail — kebab-case still hits `webhookSecret` matching when normalized).

The ADR §8 should specify the matcher's case-sensitivity (case-insensitive recommended; tenant operators sometimes paste `Secret`-cased values). Mechanical fix to §8.

### §1.4 — `IntegrationCategory` (6 values) vs `ProviderCategory` (9 values) — confirm exhaustive static mapping

**Disposition: ACCEPTABLE — counts confirmed.**

`ProviderCategory` (per `packages/foundation-catalog/Bundles/ProviderCategory.cs`) has exactly 9 values: Billing, Payments, BankingFeed, FeatureFlags, ChannelManager, Messaging, Storage, IdentityProvider, Other.

`IntegrationCategory` (proposed) has 6 values: Payments, TransactionalEmail, MarketingEmail, Sms, MeshVpn, Captcha.

The §3.4 `IntegrationCategoryMapping.ToProviderCategory(IntegrationCategory)` switch covers all 6 input values + a `_ => ProviderCategory.Other` default. Mechanically exhaustive.

**Sub-finding (mechanical):** `IntegrationCategory` is the *finer-grained* taxonomy and §1.1 of the ADR explicitly says the mapping is one-direction (`IntegrationCategory → ProviderCategory`); the reverse direction is intentionally unspecified because `Messaging → ?` is one-to-many. The §3.4 doc-comment should state this: "This mapping is one-direction. The reverse projection is undefined; do not introduce a `ToIntegrationCategory(ProviderCategory)` method."

### §1.5 — `IIntegrationAtlasProvider.IssueProviderChangeAsync` throwing `LicenseAcknowledgementRequiredException` — runtime vs compile-time enforcement

**Disposition: NON-MECHANICAL — runtime-throw is acceptable, but the issuance ordering invariant needs strengthening.**

The author's design (runtime throw → caller catches → modal recovery) is the standard pattern for an asynchronous-precondition that cannot be expressed in the type system. Council does not push for compile-time enforcement; that would over-engineer the surface (you'd need a `TenantWithAcknowledgement<TPosture>` phantom type, and the cost-benefit doesn't work for a once-per-rotation flow).

**Sub-findings:**

- **The exception itself is structurally inconsistent.** `LicenseAcknowledgementRequiredException : Exception` declares `public required string ProviderName { get; init; }` and `public required LicensePostureKind PostureKind { get; init; }`. C# does not currently support `required` on exception properties when constructed from a parameterless ctor at the throw site. The shape must use a positional constructor: `LicenseAcknowledgementRequiredException(string providerName, LicensePostureKind postureKind, string? message = null) : base(message ?? Default(providerName, postureKind))`. Mechanical fix.
- **The §3.5 "Issuance ordering invariant" sentence is correct in spirit but loose in semantics:** "MUST throw … if no `IntegrationLicenseAcknowledged` Standing Order exists for that (tenant, provider) pair." Define "exists" precisely: most-recent Standing Order at `integrations.{category}.license-acknowledged.{provider}` has `NewValue` non-null AND its `acknowledgedBy` principal is the *same* principal attempting the activation? Or any acknowledgement by any tenant principal? Council recommends *any tenant principal* — the acknowledgement is a tenant-level legal commitment, not a per-actor one. Mechanical clarification.

---

## §2 — Outside Observer perspective

The fresh-reader test: an engineer who has never seen ADR 0065 / 0066 picks up ADR 0067 and reads to §10. Can they understand:

1. What "Atlas" means in this context vs the foundation-tier `AtlasView` from ADR 0065?
2. What "Wayfinder" is vs ADR 0066's "Helm"?
3. The integration-vs-feature-vs-StandingOrder distinction?
4. How this ADR fits in the cohort?

**§2.1 finding (non-mechanical, soft-blocking):** §"Context" name-drops ADR 0065 ("Wayfinder System + Standing Order Contract"), ADR 0066 ("Helm + Identity Atlas"), and the `IAtlasProvider<T>` / `IIdentityAtlasSurface` / `IHelmWidget` contracts in a single paragraph, then proceeds. A first-time reader who does not have ADR 0065 / 0066 open in another window cannot tell:

- Whether `AtlasView` (foundation-wayfinder) is a *different* type from `IntegrationAtlasView` (proposed, ui-core-wayfinder) or the same. They are different — `AtlasView` is the projection output of `IAtlasProjector`, while `IntegrationAtlasView` is a category-specific shape consumed by `IIntegrationAtlasProvider` (which composes `IAtlasProjector` internally). The ADR should add a sentence clarifying.
- Whether "Atlas surface" and "Atlas provider" are the same concept. They are: §3.5's `IIntegrationAtlasProvider` is the implementation; "Atlas surface" is the rendered UI. The ADR uses the terms interchangeably, which is fine for cohort-internal readers but opaque to fresh ones.
- The Wayfinder-vs-Helm boundary. ADR 0066 carved this out; ADR 0067 tacitly assumes it. A one-sentence "Wayfinder is the deep-config surface; Helm is the live-state-glance surface; ADR 0067 is a Wayfinder layer" addition to §"Context" would close the gap.

**§2.2 finding (mechanical):** §3.5's headline `IIntegrationAtlasProvider` interface exposes seven methods. None are XML-doc'd in the ADR body (the implementation will obviously XML-doc them; the ADR could too, briefly — even one-line summaries would help reviewers and downstream Phase 1 implementers). Phase 1 deliverables (§10) say "XML docs required on every public type"; that's the right discipline; the ADR should preview the XML-doc summaries inline so council can review the *intent* of each method, not just the signature.

---

## §3 — Pessimistic Risk Assessor perspective

Failure modes the design either swallows or fails to specify.

**§3.1 finding (BLOCKING + non-mechanical): credential decryption requires a capability the surface doesn't source.**

§5.3 step 2: "Decrypts sensitive fields via `IFieldDecryptor`." But `IFieldDecryptor.DecryptAsync(EncryptedField field, IDecryptCapability capability, TenantId tenant, CancellationToken ct)` requires an `IDecryptCapability` — per ADR 0046 the capability is the access-control gate. Validation runs in some ambient host context; *whose* capability authorizes the decrypt? Council reads the design as "the validating principal's capability" but:

- The validation can run unattended (background re-validation; webhook-triggered re-validation). In that mode there is no human principal.
- The Bridge multi-tenant case must NOT cross capabilities between tenants; the host process holds N tenant capabilities and the wrong one is a cross-tenant credential-leak.

The ADR must specify:

- **Where the capability comes from** during user-driven validation (issuing principal's session capability).
- **Where the capability comes from** during background re-validation (a system-principal capability with explicit tenant scope; emitted as audit-logged capability-issuance).
- **Negative case:** what `ValidateProviderAsync` does when no capability is available — must NOT silently skip decryption; must fail-closed with `ProviderValidationStatus.Unknown` + a specific error code.

**§3.2 finding (non-mechanical): OAuth callback hijack — out of scope for v1, but the gap should be explicit.**

§6.3 (custom-renderer escape hatch) names "OAuth redirect dance" as the canonical example of a workflow that doesn't fit `CredentialFieldSpec`. This is correct, but defers the question. ADR 0067 v1 has no OAuth providers, so v1 ships without an OAuth callback infrastructure. **However**, the author should explicitly state: "v1 does not handle OAuth-flow providers; the first OAuth provider (e.g. a future `providers-google-workspace`) requires its own ADR addressing callback URL whitelisting, state-token CSRF, and PKCE." Otherwise an enthusiastic Phase 3 contributor adds a `providers-quickbooks` adapter and discovers the hard way that nothing prevents `state` collision across tenants. Mechanical addition to §9 (Open Questions).

**§3.3 finding (non-mechanical): webhook secret rotation race.**

§5.7 (provider rotation) explicitly preserves prior credentials. But for providers that issue webhooks back to Sunfish (Stripe, Twilio per §9.4), a rotation event creates a window where webhooks signed with the *previous* webhook-secret may arrive after the active provider has switched. The validator is responsible for verifying webhook signatures; with both old and new secrets in the Atlas projection, the validator must accept either. The ADR doesn't specify this. Mechanical addition to §5.7.

**§3.4 finding (non-mechanical): encrypted-field key-rotation handling.**

`EncryptedField` carries a `KeyVersion`; ADR 0046 envisages key rotation. When a tenant's KEK rotates from v1 → v2, the existing `integrations.payments.credentials.providers-stripe.secret-key` Standing Order contains an `EncryptedField` with `KeyVersion = 1`. The decryptor for v1 must remain available, OR a re-encrypt sweep must re-issue every credential at the new key version. Either path is fine; ADR 0067 doesn't pick one. Non-mechanical: pick a posture (council recommends "decryptor maintains v1+v2 simultaneously; sweep is async background"). Add to §9 as an Open Question.

**§3.5 finding (BLOCKING — composes with §1.2): The validation-status path is unbounded.**

§2.5 says `validation-status.{provider}` is "Mutable Standing Order updated by validation runs." But every validation run issues a *new* Standing Order (Standing Orders are append-only — see `StandingOrder` doc-comment: "Standing Orders are append-only per tenant"). Over a year of background re-validation runs every 5 minutes, that's ~100,000 Standing Orders per provider per tenant just for status. This is a load-bearing scaling failure mode for the substrate. Either:

- The substrate must support some form of compaction/superseding (out of ADR 0065's current contract).
- Or `validation-status` should NOT be a Standing Order at all — it's transient state, not a configuration intent. Move it to a separate `IValidationStatusStore` with its own append/read contract (and its own audit emission for status changes).

Council recommends the second path. **Non-mechanical (changes a substrate composition).**

---

## §4 — Skeptical Implementer perspective — full citation verification

This section follows the cohort's 3-direction discipline: positive existence, negative existence (no parallel-session pre-emption), and structural-citation correctness (the cited symbol's actual shape matches what the ADR claims).

### §4.1 — Verified PRESENT and SHAPE-CORRECT

| Symbol | Verified at | Notes |
|---|---|---|
| `EncryptedField` | `packages/foundation-recovery/EncryptedField.cs` | `readonly record struct EncryptedField(ReadOnlyMemory<byte> Ciphertext, ReadOnlyMemory<byte> Nonce, int KeyVersion)`. Author's §A0.1 cites "line 32" — actual definition starts around line 32-35. |
| `IFieldEncryptor.EncryptAsync` | `packages/foundation-recovery/Crypto/IFieldEncryptor.cs` | Signature: `Task<EncryptedField> EncryptAsync(ReadOnlyMemory<byte> plaintext, TenantId tenant, CancellationToken ct)`. Author's §6.1 reference is shape-correct. |
| `IAtlasProjector.ProjectAsync` | `packages/foundation-wayfinder/IAtlasProjector.cs` | Signature: `ValueTask<AtlasView> ProjectAsync(TenantId tenantId, StandingOrderScope? scopeFilter, CancellationToken ct)`. Shape-correct. |
| `AtlasView`, `AtlasSettingSnapshot` | `packages/foundation-wayfinder/AtlasView.cs`, `AtlasSettingSnapshot.cs` | Shapes correct. |
| `ProviderDescriptor`, `ProviderCategory` | `packages/foundation-integrations/ProviderDescriptor.cs`, `packages/foundation-catalog/Bundles/ProviderCategory.cs` | `ProviderCategory` confirmed 9 values. |
| `ICaptchaVerifier` | `packages/foundation-integrations/Captcha/ICaptchaVerifier.cs` | Confirmed. |
| `CredentialsReference` | `packages/foundation-integrations/CredentialsReference.cs` | Confirmed. |
| `IMissionEnvelopeProvider` | `packages/foundation-mission-space/Services/Contracts.cs:49` | Confirmed (note: ADR 0062 build *has* landed; this is on origin/main). |
| `AuditEventType` shape | `packages/kernel-audit/AuditEventType.cs` | `public readonly record struct AuditEventType(string Value)` with constants pattern. §8 uses this correctly. |

### §4.2 — Verified PRESENT but SHAPE-WRONG (structural-citation failures)

| Symbol | ADR claim | Origin/main reality |
|---|---|---|
| `IFieldDecryptor.DecryptAsync` | §5.3 implies it works given an `EncryptedField` | Actual signature requires `IDecryptCapability capability` + `TenantId tenant` parameters. ADR doesn't surface either dependency. **BLOCKING.** |
| `IPaymentGateway.ValidateAsync()` | §6.2: "the validator inside `StripeIntegrationValidator` calls `IPaymentGateway.ValidateAsync()`" | Actual `IPaymentGateway` exposes only `AuthorizeAsync / CaptureAsync / RefundAsync`. There is no `ValidateAsync`. **BLOCKING.** |
| `IMessagingGateway.ValidateAsync()` | §6.2: "the validator calls `IMessagingGateway.ValidateAsync()`" | Actual `IMessagingGateway` exposes only `SendAsync / GetStatusAsync`. There is no `ValidateAsync`. **BLOCKING.** |
| `IMeshVpnAdapter.ValidateAsync()` | §6.2 | Actual `IMeshVpnAdapter` (which IS on origin/main, contrary to §A0.4) has `AdapterName / GetMeshStatusAsync / RegisterDeviceAsync`. No `ValidateAsync`. **BLOCKING.** |
| `IStandingOrderIssuer.IssueAsync` shape | §3.5 `IssueProviderChangeAsync(...) -> ValueTask<StandingOrderId>` and similar issuance methods | Actual signature is `Task<StandingOrder> IssueAsync(StandingOrderDraft draft, ActorId issuedBy, IAuditTrail auditTrail, CancellationToken ct)`. Returns `StandingOrder` not `StandingOrderId`; takes `ActorId` not `PrincipalId`; requires `IAuditTrail` as a parameter (NOT a constructor-injected dep). The §3.5 wrapper methods need to compose this differently. **BLOCKING.** |
| `IssuedBy` parameter type | ADR §3.5 uses `PrincipalId issuedBy` everywhere | `StandingOrder.IssuedBy` is `ActorId`, not `PrincipalId`. ADR 0066 PR #529 §A0.4 already flagged the ADR-0065-namespace cite confusion (`Sunfish.Foundation.Identity.ActorId` vs canonical `Sunfish.Foundation.Assets.Common.ActorId`). ADR 0067 needs the same disposition: every `PrincipalId issuedBy` → `ActorId issuedBy`. **Structural-citation; non-blocking once you flip the type, but it's pervasive.** |

### §4.3 — Verified ABSENT (provider packages don't exist)

§10 Phase 3 and §"Considered options" / §1 reference these provider packages as concrete deliverables or first-wave adapters:

| Provider package | Origin/main status |
|---|---|
| `providers-stripe` | DOES NOT EXIST |
| `providers-square` | DOES NOT EXIST |
| `providers-postmark` | DOES NOT EXIST |
| `providers-sendgrid` | DOES NOT EXIST |
| `providers-mailchimp` | DOES NOT EXIST |
| `providers-twilio` | DOES NOT EXIST |
| `providers-mesh-tailscale` | DOES NOT EXIST |
| `providers-mesh-netbird` | DOES NOT EXIST |
| `providers-hcaptcha` | DOES NOT EXIST |
| `providers-mesh-headscale` | EXISTS |
| `providers-recaptcha` | EXISTS |

Of the eleven provider packages named, **nine do not exist.** The ADR's silence on this is a gap. Acceptable interpretation: each provider package will be authored by a downstream feature workstream (W#5 commercial scope brings `providers-stripe`; W#22 Leasing brings `providers-postmark`; etc.) — but the ADR should say so explicitly and the §10 Phase 3 deliverable list should be conditional on those provider packages landing. The current §10 Phase 3 wording reads as if Phase 3 of THIS ADR will *also* author those provider packages, which is a Phase 3 scope blowup of an order of magnitude.

**Structural-citation finding.** Mechanical fix to §10 Phase 3: split into "Phase 3a — first-wave provider packages exist (gated on W#5 / W#22 / W#28 build)" and "Phase 3b — schema-provider+validator additions to those packages."

### §4.4 — Verified ABSENT but EXPECTED (soft-prerequisites, correctly flagged)

| Symbol | Status |
|---|---|
| `IAtlasProvider<T>`, `IIdentityAtlasSurface`, `IHelmWidget` | Author's §A0.3 correctly flagged as on PR #529. Council confirms (PR #529 open as of council time). The contracts are in `packages/ui-core/Wayfinder/` per ADR 0066 PR. |
| `Sunfish.UICore.Wayfinder` namespace | Author's §A0.3 correctly notes net-new. **However**, the package-shape is in dispute (§1.1 above). |

---

## §5 — Devil's Advocate perspective

### §5.1 — Was Option B genuinely the right framing?

The ADR considers three options: per-provider standalone pages (A), unified `IIntegrationAtlasProvider` (B), static appsettings.json (C). Council confirms Option B is correct, but pushes harder on a fourth option the ADR did not consider:

**Option D — Defer the surface; specialize the renderer per category.**

Instead of one `IIntegrationAtlasProvider`, ship six smaller `I{Category}AtlasProvider` interfaces (`IPaymentsAtlasProvider`, `IEmailAtlasProvider`, etc.) that each specialize ADR 0066's `IAtlasProvider<T>` directly, with category-specific view models. The dynamic-schema renderer is then per-category, not global.

**Council disposition:** Option B is still the right call. The single-surface framing wins on:

- Cross-category consistency (one masking implementation, one accessible-authentication implementation).
- New-category extension (a future `IntegrationCategory.SignatureCapture` is one enum value, not a new ADR + new interface).
- Tenant-admin "what's configured?" view (one ActiveByCategory dictionary, not six probes).

But Option D's framing is worth surfacing in §"Considered options" so a future reader knows it was considered. Mechanical addition.

### §5.2 — Was extending ADR 0013 the right framing?

This is the council prompt's specific challenge: "was the ADR 0013 extension genuinely the right framing vs a separate `Sunfish.Foundation.Integrations.Atlas` package?"

**Council disposition:** ADR 0067 is NOT extending ADR 0013. It is *composing* ADR 0013 (by referencing `ProviderDescriptor` and the provider-neutrality posture) but the new contract surface lives in `ui-core-wayfinder` (or the contested `packages/ui-core/Wayfinder/Integrations/`), NOT in `foundation-integrations`. The framing "ADR 0067 extends ADR 0013" appears in the council prompt itself, not in the ADR text — the ADR's `extends: []` frontmatter is correct.

The alternative framing "separate `Sunfish.Foundation.Integrations.Atlas` package" would conflate the configuration-UX surface with the runtime-routing surface. ADR 0067 §3.7 already addresses why these must stay separate: `ProviderDescriptor` serves runtime-routing; `IIntegrationProviderSchema` serves configuration-UX. They share a `ProviderName` / `Key` identity but otherwise should not couple. Council ENDORSES the current `ui-core-wayfinder` placement (modulo the §1.1 package-name dispute).

### §5.3 — Should license-acknowledgement be deferred to a separate amendment?

Council prompt: "What about deferring license-acknowledgement to a separate amendment?"

**Council disposition: YES, defer.** This composes with the §3 finding that ADR 0061 contradicts ADR 0067's StrongCopyleft acknowledgement track. Cleanest path:

1. Cut `LicensePostureKind`, `LicenseAcknowledgementRequiredException`, `IssueLicenseAcknowledgementAsync`, `IntegrationLicenseAcknowledged` audit event, the `license-acknowledged.{provider}` Standing Order path, §5.5 entirely, and the §A1 council-checklist Pedantic-Lawyer points about license posture from this ADR.
2. File a follow-on intake (`ADR 0067-A1`) that addresses: ADR 0061's analyzer-enforcement vs the prospect of an admin-acknowledgement opt-in; the actual legal counsel posture for SSPL/BSL adapters; the license-acknowledgement modal UX.
3. ADR 0067 v1 ships with `IntegrationCategory.MeshVpn` but the license-posture column reads "(deferred to ADR 0067-A1)".

This **shrinks ADR 0067 by ~150 lines**, removes the most contested invariant, and lets ADR 0067 ship cleanly. Non-mechanical, but author-only-judgment.

---

## §6 — WCAG/a11y perspective

The ADR's §"Decision drivers" §9 commits to WCAG 2.2 AA conformance as contract; §A1 names the SCs explicitly (3.3.7, 3.3.8, 1.3.1, 4.1.3, 1.4.1, 3.3.4). Council reviews each.

### §6.1 — SC 3.3.7 (Redundant Entry)

ADR §A1 says: "no credential is asked twice in the same session."

**Risk:** the schema-driven renderer may force re-entry of an unchanged value during partial-rotation flows. E.g., admin rotates Stripe key but webhook-secret hasn't changed; if the form is rendered with the existing webhook-secret blanked-out (which is the §"Decision drivers" §4 sensible-default for sensitive fields), the admin must re-enter or the schema must allow "preserve existing." The ADR doesn't specify which.

**Recommendation:** §3.2 add a `CredentialFieldSpec.PreserveExistingOnEdit { get; init; } = true` field. Default `true` means "render the field as 'currently set; clear to replace'" rather than "render blank, demanding re-entry." Mechanical addition.

### §6.2 — SC 3.3.8 (Accessible Authentication)

§3.2's `AutocompleteHint` is correct — passes the SC 3.3.8 cognitive-function test by allowing password managers to fill credentials. **However**, §A1 says "every sensitive `CredentialFieldSpec` has an `AutocompleteHint`". The contract should *enforce* this, not aspirate to it: make `AutocompleteHint` *required* (non-nullable) on `Sensitive == true` fields. The §4.1 example violates this — `webhook-secret` has `AutocompleteHint = "off"`, which is correct (a webhook-secret is not auth-credential and should not autocomplete), but the field's *existence* is the point.

**Recommendation:** keep `AutocompleteHint` nullable; add a constructor invariant (or a Roslyn analyzer) that fails when `Sensitive == true` && `AutocompleteHint == null`. Mechanical.

### §6.3 — SC 1.3.1 (Info and Relationships)

§A1: "masking state is conveyed structurally, not just visually."

**Risk:** the show/hide toggle's state (currently masked vs currently revealed) must be programmatically determinable. ARIA `aria-pressed` on the toggle button is the canonical pattern. The ADR's §A1 phrasing is a goal; the §3 / §6 contracts don't specify the rendering invariant. Add to §7.3 component requirements: "Show/hide toggle MUST use `<button aria-pressed='{revealed}'>` per W3C button-pattern guidance; visual state must mirror programmatic state."

Mechanical addition.

### §6.4 — SC 4.1.3 (Status Messages)

§5.6 table is good but incomplete. The `Unknown (pre-validation)` row says "not announced (initial state)" — correct for first render. **However**, the `Unknown` state is also reached *after* a provider rotation when the new provider has no credentials yet (§5.7 step 4). In that case the state IS a transition the screen-reader user needs to know about. Council recommends:

- `Unknown` after a rotation MUST be announced via `aria-live="polite"`.
- `Unknown` on first render must NOT be announced.

Distinguishing "first render" from "post-rotation" requires the renderer to track previous-state. Mechanical addition to §5.6 + §7.3 component requirements.

### §6.5 — SC 1.4.1 (Use of Color)

§5.6 explicitly pairs every color with an icon shape. PASS.

### §6.6 — SC 3.3.4 (Error Prevention — Legal)

§5.5 step 3: "Click an explicit 'I acknowledge and accept the license obligations' button; checkbox-only acknowledgement is forbidden."

PASS — the explicit-action requirement satisfies SC 3.3.4. **However**, if license-acknowledgement is deferred per §5.3 of this council review, this SC moves to ADR 0067-A1.

### §6.7 — Additional SC not flagged in §A1: SC 2.4.6 (Headings and Labels)

Each `CredentialFieldSpec.DisplayLabel` becomes the form-field label. Labels must be descriptive — the ADR doesn't enforce this. A field labeled "Key" is conformant against SC 1.3.1 (info-and-relationships) but fails SC 2.4.6 (headings and labels) because "Key" is uninformative. Council recommends a minimum-length lint on `DisplayLabel` (≥3 words or contains ≥1 noun) — but that's over-engineering. **Mechanical fallback:** §A1 add SC 2.4.6 with the guidance "DisplayLabel should be descriptive and avoid bare-noun labels like 'Key'; tests should sanity-check first-wave schemas."

### §6.8 — Additional SC: SC 1.4.11 (Non-text Contrast)

Status icons (§5.6) must meet 3:1 contrast against the surface background. Renderer concern, but the contract should commit to it. Add to §A1.

### §6.9 — OAuth callback flow aria-live announcements

Council prompt notes: "OAuth callback flow needs aria-live announcements." Since OAuth is deferred (§3.2 finding, §5.2 finding), this is moot for v1. Surface in the OAuth follow-on ADR.

---

## §7 — Pedantic Lawyer perspective (Stage 1.5 sub-perspective)

### §7.1 — License acknowledgement copy

§5.5 step 3 requires the modal to "Read the posture explanation (heading + paragraph identifying the license + the obligations)." ADR does not source the copy. If this surface ships, the actual copy must be reviewed by counsel; ADR 0067 cannot ship the copy itself in markdown without becoming a legal-content artifact. **However:** this finding moves to ADR 0067-A1 if license-acknowledgement is deferred per §5.3.

### §7.2 — Audit-record retention

§A1 says: "Audit record retention satisfies the 'configuration change auditing' obligation that any tenant policy may impose." This is a goal, not a contract. Audit retention is governed by ADR 0049 + the kernel-audit substrate. ADR 0067 inherits whatever ADR 0049 provides; it does not have its own retention obligations. The §A1 phrasing should defer to ADR 0049 explicitly: "Audit retention follows ADR 0049's substrate posture; ADR 0067 does not override it."

### §7.3 — Schema-version migration must not silently drop a tenant's prior license acknowledgement

§4.2 schema migration is per-adapter; the migration path doesn't address whether a license-acknowledged Standing Order from schema v1 still satisfies the v2 schema's license posture. Council says: yes, an acknowledgement is per (tenant, provider) regardless of credential schema version — so an acknowledgement issued under schema v1 still satisfies a v2 issuance check. ADR §4.2 should explicitly say this. Moves to ADR 0067-A1 if license-acknowledgement is deferred.

---

## §8 — UPF v1.2 Stage 2 anti-pattern scan

Reviewing the 21 anti-patterns from `.claude/rules/universal-planning.md`.

| AP | Name | Status | Note |
|---|---|---|---|
| 1 | Unvalidated assumptions | PARTIAL | §A0 audit caught some drift but missed §4.2 issues. |
| 2 | Vague phases | CLEAN | §10's 5 phases are specific. |
| 3 | Vague success criteria | CLEAN | §10 deliverables are listable. |
| 4 | No rollback | PARTIAL | §10 has no explicit rollback per phase; rollback is implicit (revert the package). Acceptable for new-contract surface. |
| 5 | Plan ending at deploy | CLEAN | §10 Phase 5 includes apps/docs + kitchen-sink. |
| 6 | Missing Resume Protocol | N/A | ADR not a multi-day plan. |
| 7 | Delegation without contracts | CLEAN | Per-package deliverable is contract-shaped. |
| 8 | Blind delegation trust | CLEAN | Council pattern enforces. |
| 9 | Skipping Stage 0 | CLEAN | W#34 discovery is the Stage 0. |
| 10 | First idea unchallenged | CLEAN | Three options considered. |
| 11 | Zombie projects (no kill criteria) | N/A | ADR is contract design, not a runtime project. |
| 12 | Timeline fantasy | N/A | ADRs don't set timelines. |
| 13 | Confidence without evidence | PARTIAL | §A0 audit's "verified" table contained §4.2 errors that contradict the confidence. |
| 14 | Wrong detail distribution | PARTIAL | §3 contracts are detailed; §10 Phase 3's adapter-package work is under-detailed (see §4.3). |
| 15 | Premature precision | CLEAN | Schema versioning posture is conservative. |
| 16 | Hallucinated effort estimates | N/A | None given. |
| 17 | Delegation without context transfer | CLEAN | Hand-off includes ADR + §A0 audit. |
| 18 | Unverifiable gates | CLEAN | §10 deliverables are testable. |
| 19 | Missing tool fallbacks | N/A | Pure contract design. |
| 20 | Discovery amnesia | CLEAN | W#34 discovery cited and respected. |
| 21 | Assumed facts without sources | **FAIL** | §A0 missed §4.2 (six structural-citation failures including IPaymentGateway/IMessagingGateway/IMeshVpnAdapter ValidateAsync; provider package non-existence; PrincipalId vs ActorId; package-shape contradiction with ADR 0066). The cohort discipline says verify EVERY cited symbol on origin/main BEFORE declaring AP-21 clean. AP-21 is the ADR's most prominent gap. |

**Summary:** 1 FAIL (AP-21), 4 PARTIAL, 14 CLEAN, 2 N/A.

---

## §9 — Recommended dispositions

### §9.1 — Mechanical fixes (council pre-authorizes; author may apply directly)

1. **§3.10 exception shape:** rewrite `LicenseAcknowledgementRequiredException` to use a positional constructor instead of `required` properties — `required` on Exception subclass init properties throws at construction.
2. **§A0.4 correction:** acknowledge `IMeshVpnAdapter` IS on origin/main; remove the "uncommitted in working tree" claim.
3. **§3.4 mapping doc-comment:** add "this mapping is one-direction" note to `IntegrationCategoryMapping`.
4. **§5.7 webhook-secret-rotation note:** add a sentence to the rotation flow specifying validator must accept both old and new webhook secrets during the transition window.
5. **§8 redaction-rule case-sensitivity:** specify case-insensitive matcher for the corpus test.
6. **§3.4 issuance-ordering "exists" definition:** specify "any tenant principal" not "same principal."
7. **§9.4 webhook URL provisioning** — already correctly deferred; the §3.2 / §5.2 finding adds an OAuth provider deferral as a sibling open question.
8. **§"Considered options"**: surface Option D (per-category specialization) as considered-and-rejected.

### §9.2 — Non-mechanical (author judgment required)

1. **§3.5 issuance method shape rework.** Either (a) the ADR's wrapper methods compose `IStandingOrderIssuer.IssueAsync` correctly (taking `IAuditTrail` + `ActorId` + returning `StandingOrder`) and the `IIntegrationAtlasProvider` returns `Task<StandingOrder>`, or (b) the ADR explicitly carries a §"Layer adapter" rationale for why it's exposing a simpler `ValueTask<StandingOrderId>` shape and how the audit-trail dependency is sourced internally. Council prefers (b) — the surface should be domain-shaped, not substrate-shaped — but the rationale must be explicit + the implementation pattern must be specified.
2. **PrincipalId → ActorId rename throughout §3 / §5.** Pervasive but mechanical-once-decided. Decide whether the surface uses ActorId (canonical for StandingOrder) or PrincipalId (used by SignedOperation envelopes) or BOTH (PrincipalId for signing, ActorId for issuer attribution). ADR 0066 PR #529 §A0.4 already raised this concern as a discrepancy in ADR 0065.
3. **§6.2 ValidateAsync dispatch reframe.** Drop the per-egress-contract `ValidateAsync` claim; specify that `IIntegrationProviderValidator` performs whatever liveness probe the validator chooses (not delegating to a method that doesn't exist on the egress contract).
4. **§5.3 IFieldDecryptor capability sourcing.** Specify capability-source for user-driven validation, background re-validation, and the negative case.
5. **License-acknowledgement track: defer to ADR 0067-A1.** This composes the BLOCKING contradiction with ADR 0061 + the related Pedantic-Lawyer + WCAG SC 3.3.4 checks. Cuts ~150 lines from this ADR, leaves a clean v1, opens a single follow-on amendment intake.

### §9.3 — Structural (composes with §9.2)

1. **§1.1 package-shape resolution.** Decide: `packages/ui-core/Wayfinder/Integrations/` (council recommendation, consistent with ADR 0066) or `packages/ui-core-wayfinder/` (current ADR text, contradicts ADR 0066). Update §"Decision" / §3 / §10.
2. **§10 Phase 3 split.** `Phase 3a — provider packages exist (gated on W#5/W#22/W#28 build)`; `Phase 3b — schema-provider+validator additions to existing provider packages.`
3. **§3.5 / §2.5 validation-status NOT a Standing Order.** Move to a separate `IValidationStatusStore` to avoid append-only-log explosion under background re-validation cadence.

### §9.4 — Non-blocking (defer to author/CO discretion)

1. WCAG SC 2.4.6 / 1.4.11 surfaces in §A1.
2. `CredentialFieldSpec.PreserveExistingOnEdit` field for SC 3.3.7 partial-rotation case.
3. Sensitive-field `AutocompleteHint` enforcement (analyzer or constructor invariant).
4. EncryptedField key-rotation posture in §9 (Open Questions).

---

## §10 — Council verdict

**BLOCK pre-merge.** Four blocking findings:

1. `IPaymentGateway.ValidateAsync` / `IMessagingGateway.ValidateAsync` / `IMeshVpnAdapter.ValidateAsync` do not exist (§4.2; rework §6.2 dispatch).
2. License-acknowledgement directly contradicts ADR 0061's actual SSPL/BSL exclusion posture (§5.3, §7.1; defer to ADR 0067-A1).
3. `IFieldDecryptor` capability sourcing unspecified; ADR cannot decrypt without an `IDecryptCapability` (§3.1; specify sourcing).
4. `validation-status` Standing Order path is unbounded under background re-validation cadence (§3.5; move to `IValidationStatusStore`).

Plus 6 structural-citation failures the §A0 audit missed and 5 non-mechanical findings requiring author judgment.

**Recommended path:** author rewrites §3.5 issuance shapes against actual `IStandingOrderIssuer.IssueAsync`; rewrites §6.2 validation dispatch to not depend on non-existent egress methods; defers license-acknowledgement to ADR 0067-A1; resolves the §1.1 package-shape question; force-updates §A0 with the corrected verifications. After rework, council re-reviews with focus on the four blocking items only.

The conceptual bones are sound. The structural-citation discipline failed at §A0; that's the cohort lesson — 25-of-26 substrate amendments need council fixes, with §A0 catching 1, this council catches the other ~5–10. ADR 0067 fits the pattern.

---

## §A — Council appendix

### §A.1 — Findings table by section

| § | Finding | Class | Disposition |
|---|---|---|---|
| §1.1 | Package-shape contradicts ADR 0066 | structural | non-mechanical |
| §1.2 | `IMeshVpnAdapter` IS on origin/main; no ValidateAsync | structural-citation | BLOCKING |
| §1.3 | Redaction corpus test mechanism | mechanical | accept |
| §1.4 | `IntegrationCategory` mapping count exhaustive | (verified) | mechanical doc-clarification |
| §1.5 | LicenseAcknowledgementRequiredException shape + ordering invariant | mechanical | accept |
| §2.1 | First-time reader Atlas/Wayfinder/Helm clarity | non-mechanical | accept |
| §2.2 | XML-doc summaries inline in §3 | mechanical | accept |
| §3.1 | `IFieldDecryptor` capability sourcing missing | non-mechanical | BLOCKING |
| §3.2 | OAuth deferred but not flagged | mechanical | accept |
| §3.3 | Webhook secret rotation race | mechanical | accept |
| §3.4 | EncryptedField key-rotation posture | non-mechanical | accept (open question) |
| §3.5 | `validation-status` Standing Order unbounded | non-mechanical | BLOCKING |
| §4.2 | 6 structural-citation failures | structural-citation | BLOCKING (multiple) |
| §4.3 | 9 provider packages don't exist | structural-citation | mechanical |
| §5.1 | Option D surface in considered-options | mechanical | accept |
| §5.3 | Defer license-acknowledgement to ADR 0067-A1 | non-mechanical | accept BLOCKING-resolution |
| §6.1 | SC 3.3.7 partial-rotation `PreserveExistingOnEdit` | mechanical | accept |
| §6.2 | SC 3.3.8 enforce `AutocompleteHint` on Sensitive | mechanical | accept |
| §6.3 | SC 1.3.1 `aria-pressed` on toggle | mechanical | accept |
| §6.4 | SC 4.1.3 post-rotation `Unknown` announcement | mechanical | accept |
| §6.7 | SC 2.4.6 add to §A1 | mechanical | accept |
| §6.8 | SC 1.4.11 add to §A1 | mechanical | accept |

### §A.2 — Three-direction citation discipline scorecard

- **Positive existence verified:** 9 of 11 adapter packages absent; 6 of 6 substrate types present.
- **Negative existence verified:** no parallel-session pre-emption of `Sunfish.UICore.Wayfinder.Integrations` namespace.
- **Structural-citation correctness:** 6 failures (see §4.2). This is the dimension the ADR's §A0 audit missed; matches the cohort lesson "council catches structural-citation failures the author's pre-flight grep cannot."

### §A.3 — Cohort batting

This council finding adds to the cohort's running tally: 25-of-26 substrate amendments needed council fixes. ADR 0067 makes it 26-of-27 → 26-of-27 = 96.3% need-fixes rate. The pre-merge canonical posture continues to be the right discipline.

---

*End of council review.*

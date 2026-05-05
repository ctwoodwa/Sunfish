# ADR 0067 — Atlas Integration-Config UI Surface — Re-Council Review (Second Pass)

**Date:** 2026-05-05
**Reviewer:** XO research session (Opus 4.7, xhigh)
**Review type:** Canonical pre-merge re-council per ADR 0069 D1 — substantive rework second pass after first-pass BLOCK fix-pass
**ADR branch under review:** `docs/adr-0067-atlas-integration-config` (PR #539)
**Fix-pass commit reviewed:** `92e1618b93d713578c2c8342878fa49aefe339f5` (286 insertions / 154 deletions; 13,468 words; +2,916 vs first-pass)
**First-pass council:** `icm/07_review/output/adr-audits/0067-council-review-2026-05-04.md` (BLOCK; 4 BLOCKING + 5 NM + 8 mechanical + 6 SC = 23 findings)
**New intake stub:** `icm/00_intake/output/2026-05-05_adr-0067-a1-license-acknowledgement-intake.md` (916 words)
**Posture:** structural-citation 3-direction discipline + cohort-idiom check + crypto/security pass; council perspectives folded into single-session synthesis at xhigh.

---

## TL;DR

**Verdict: NEEDS-AMENDMENT (mechanical-tier, auto-acceptable).** All 4 BLOCKING fixes (B1 validator-owned probe, B2 capability sourcing + audit cohort, B3 `IValidationStatusStore`, B4 `IStandingOrderIssuer.IssueAsync` composition) land cleanly and structurally. All 5 non-mechanical fixes (NM1 package shape, NM2 license-track deletion, NM3 webhook-secret rotation, NM4 EncryptedField key-rotation, NM5 OAuth callback) land. The 4 author deviations (D1 `IAuditTrail` ctor + method-param, D2 reuse of existing `FieldDecrypted` events instead of net-new `CapabilityIssued`, D3 separate `IDecryptCapabilityProvider` declaration, D4 §A0.7 net-new symbol table) are sound and well-rationalized. The two NEW substrate contracts (`IValidationStatusStore`, `IDecryptCapabilityProvider`) compose cleanly with foundation-tier cohort idioms.

**However**, the rework introduced or carried through **5 mechanical-tier findings** that are auto-acceptable but worth flagging:

1. **NM2 dangling-reference partial:** four sites still narrate license-acknowledgement as if it were a v1 surface (Decision drivers #3 audit-by-construction list, Option A cons, Option B description, Option C cons). Two of those (Option A cons + Option C cons) inaccurately ascribe to ADR 0061 a posture ADR 0061 does not have ("ADR 0061 mandates an interactive acknowledgement"; "license posture is duplicated in each mesh adapter's UI"). Below the §"Halt-conditions" 5+ escalation threshold; mechanical fix.
2. **B1 sub-finding — Postmark probe URL is wrong for the cited credential.** §5.3 + §6.2 cite Postmark `/servers` for "Postmark `/servers` API call to verify the **server token** is valid." Postmark `/servers` requires the **Account-Token** (`X-Postmark-Account-Token` header), NOT the Server-Token. To validate a Server-Token, the canonical endpoint is `/server` (singular) or `/messages/outbound`. AP-21 cited-symbol drift; hand-off implementer footgun.
3. **§3.7 `ProviderDescriptor.Key` reconciliation drift.** §3.1 + §3.7 claim `ProviderName = "providers-stripe"` matches `ProviderDescriptor.Key`. On origin/main, `ProviderDescriptor.Key` is "Stable reverse-DNS-style provider key (e.g. `sunfish.providers.stripe`)" — different string shape. AP-21 carry-through (predates the fix-pass; not a regression but unresolved).
4. **§3.5 issuance composition rationale uses constructor-injected `IAuditTrail`** (council option (b), per author Deviation 1). Cohort idiom for `IStandingOrderIssuer.IssueAsync` is to take `IAuditTrail` as a method parameter EXPLICITLY (not constructor). Author rationale is sound (the wrapper is domain-shaped; substrate audit-by-construction is preserved because the wrapper still ultimately calls `IssueAsync(draft, actorId, auditTrail, ct)`). However, the rationale paragraph could be more explicit that the constructor-injected `IAuditTrail` is sourced from the same DI container as `kernel-audit`'s canonical singleton (no separate audit channel). Mechanical clarification.
5. **`IDecryptCapabilityProvider.AcquireAsync` purpose-string `"integration-validation"` is documented in §3.14 + §5.3.1 but has no `internal const string` declaration**, so it'll be a magic-string at the call site. Cohort precedent (`KeyDerivationPurposeLabels`, etc.) declares purpose strings as named constants. Recommend a `IntegrationCapabilityPurposes` static class with `public const string IntegrationValidation = "integration-validation";`. Mechanical addition (Phase 1 deliverable).

**Net-new BLOCKING findings introduced by the rework: ZERO.** No halt-condition tripped.

**Cohort batting average update:** 28-of-29 → **29-of-30 (96.7%)** substrate amendments needing council fixes. The first-pass council on ADR 0067 fits the canonical pre-merge discipline; the fix-pass produced a substantively-clean ADR with mechanical-tier residue.

---

## §1 — BLOCKING-fix audit

### §1.1 — B1 (validator-owned probe replaces non-existent `ValidateAsync` calls)

**Verdict: PASS structurally; 1 mechanical sub-finding on probe URL correctness.**

**Three-direction structural-citation:**

- **Positive existence (the contracts the validators DO compose against):** `IPaymentGateway.AuthorizeAsync/CaptureAsync/RefundAsync` confirmed at `packages/foundation-integrations/Payments/IPaymentGateway.cs`; `IMessagingGateway.SendAsync/GetStatusAsync` confirmed at `packages/foundation-integrations/Messaging/IMessagingGateway.cs`; `IMeshVpnAdapter.AdapterName/GetMeshStatusAsync/RegisterDeviceAsync` confirmed at `packages/foundation-transport/IMeshVpnAdapter.cs`.
- **Negative existence (the absent `ValidateAsync` calls):** `git grep ValidateAsync` in production text on the fix-pass branch returns ZERO results outside §A0 drift-narration (which correctly catalogs the absence). No remaining `IPaymentGateway.ValidateAsync()`, `IMessagingGateway.ValidateAsync()`, `IMeshVpnAdapter.ValidateAsync()` claims in §5.3, §6.2, or §A0.1 (which now correctly reads "AuthorizeAsync, CaptureAsync, RefundAsync only — NO ValidateAsync"). The first-pass BLOCKER is fixed.
- **Shape correctness:** §5.3 prose now reads "the validator owns its own liveness probe (HTTP API call, mesh control-plane handshake, etc.); it does NOT delegate to any method on `IPaymentGateway` / `IMessagingGateway` / `IMeshVpnAdapter`." §6.2 specifies validators are decoupled from runtime-gateway contracts. §A0.6 drift-table correctly resolves the issue.

**Probe URL spot-check (council §1 hand-off discipline — "wrong endpoint = hand-off implementer footgun"):**

| Provider | ADR-cited probe | Spot-check verdict |
|---|---|---|
| Stripe | `GET /v1/account` | **CORRECT.** Canonical Stripe API endpoint for retrieving the account associated with a secret key. Standard credential-validation idiom. |
| Postmark | `GET /servers` | **WRONG ENDPOINT FOR THE CITED CREDENTIAL.** Postmark documentation: `/servers` requires `X-Postmark-Account-Token` header (Account-Token), NOT the Server-Token. ADR §5.3 says the validator verifies "the server token is valid." To validate a Server-Token, canonical endpoint is `GET /server` (singular — fetches the current server's config using the Server-Token) or `GET /messages/outbound` (probes the Server-Token's authorization to send). **Recommendation:** mechanical fix — change to `/server` (singular) or note that `/servers` validates the Account-Token (different credential). |
| Tailscale | `GET /api/v2/tailnet/{tailnet}/keys` | **CORRECT.** Canonical Tailscale API; uses Bearer-auth-key. Reasonable liveness probe for a mesh control-plane API key. |

**Disposition:** B1's structural fix is clean. The Postmark sub-finding is a mechanical-tier AP-21 cited-endpoint drift — it makes the hand-off implementer's first probe attempt fail with HTTP 401, which is recoverable but wastes an implementer cycle. Mechanical fix recommended (auto-acceptable).

### §1.2 — B2 (capability sourcing for user-driven + background; fail-closed)

**Verdict: PASS, including author Deviation 2 (reuse of existing `FieldDecrypted` / `FieldDecryptionDenied` events).**

**Three-direction structural-citation:**

- **Positive existence:** `AuditEventType.FieldDecrypted` confirmed at `packages/kernel-audit/AuditEventType.cs:280`; `AuditEventType.FieldDecryptionDenied` confirmed at `packages/kernel-audit/AuditEventType.cs:283`. Audit emission shape verified at `packages/foundation-recovery/Crypto/TenantKeyProviderFieldDecryptor.cs:116, 125` (positive: `AuditEventType.FieldDecrypted`; negative: `AuditEventType.FieldDecryptionDenied`).
- **Negative existence:** no `CapabilityIssued` symbol on origin/main (correct — author Deviation 2 chose to reuse existing audit events rather than introduce a new one). No conflict with prior audit-event constants.
- **Shape correctness:** §5.3.1 prose accurately states "Every system-principal capability acquisition is itself audited by the recovery substrate (per ADR 0046-A4) — successful decrypts emit `FieldDecrypted` records carrying the capability id, and rejections emit `FieldDecryptionDenied`." This is consistent with ADR 0046-A4's audit-emission spec on `IFieldDecryptor.DecryptAsync`. Author Deviation 2 is sound: introducing a separate `CapabilityIssued` event would have been double-audit work without distinguishing semantics — the `FieldDecrypted` audit record's `CapabilityId` field already carries the trace.

**Fail-closed semantics check (§5.3.1 negative case):**

- ADR §5.3.1 specifies three failure modes: capability provider not registered, capability validation fails, capability TTL expired/tenant-scope mismatched. All three must yield `IntegrationValidationResult { Status = Unknown, ErrorCode = "no-decrypt-capability" }`.
- Audit emission: `IntegrationValidationFailed` (NOT silent skip). Persisted to `IValidationStatusStore` so consumers (`IMissionEnvelopeProvider`) treat the integration as not-currently-usable.
- Diagnostic emission: background-driven path additionally emits a host-level diagnostic. PASS.

**`ErrorCode` cohort idiom check:** `"no-decrypt-capability"` is kebab-case, matching cohort idiom (`"wrong-tenant"`, `"expired"` from `IDecryptCapability.ValidateForDecrypt`). Cohort idiom CLEAN.

**Cross-tenant capability-leak pass (security correctness):** §5.3.1 specifies tenant-scoped capability issuance: "the host process holds N tenant capabilities; the wrong one is rejected by `IDecryptCapability.ValidateForDecrypt`." This composes correctly with `FixedDecryptCapability.ValidateForDecrypt(targetTenant, now)` at `packages/foundation-recovery/Crypto/FixedDecryptCapability.cs:37` (`return "wrong-tenant";` on tenant mismatch). The Bridge multi-tenant credential-leak vector is structurally closed. PASS.

**Disposition:** B2's structural fix is clean. Author Deviation 2 (no new `CapabilityIssued` event) is sound and well-rationalized.

### §1.3 — B3 (`IValidationStatusStore` replaces unbounded Standing-Order path)

**Verdict: PASS structurally; cohort-idiom CLEAN with minor narration.**

**Three-direction structural-citation:**

- **Positive existence (the contract this is replacing):** §2.1's per-category `validation-status.{provider}` Standing-Order path is GONE from §2 path-namespace listing — only `active-provider`, `credentials.{provider}.{credential}`, and `routing` remain. §2.5 explicitly redirects validation-status to §3.13. CLEAN.
- **Negative existence (no parallel substrate already exists):** `git grep IValidationStatusStore` on origin/main returns nothing. Net-new symbol; no collision.
- **Shape correctness:** §3.13's three-method surface (`GetCurrentAsync`, `UpdateAsync`, `HistoryAsync`) is a sensible append/read-with-compaction shape for transient state. `ProviderValidationStatusEntry` carries `Status`, `ValidatedAt`, `ValidatedBy`, `ErrorCode`, `ErrorMessage` — appropriately rich for diagnostic surfacing.

**Cohort idiom check (foundation-tier interface conventions):**

- `IAtlasProjector` is a **read-only projector** (no audit emission); `IFieldEncryptor` is a **stateless transformer** (no audit emission); `IFieldDecryptor` is a **constructor-audit-emitter** (audit-disabled overload + audit-enabled overload, with implementation injecting `IAuditTrail` via ctor); `IStandingOrderIssuer` is a **method-parameter-audit-emitter** (audit by construction at the method signature). Cohort has BOTH idioms — constructor-audit-emitter and method-parameter-audit-emitter — depending on whether the audit emission is a per-call domain choice (issuer: yes, every call audits) or a wiring decision (decryptor: yes if wired, no for tests/bootstrap).
- `IValidationStatusStore.UpdateAsync` follows the **constructor-audit-emitter** idiom ("audit emission via the constructor-injected `IAuditTrail`"). This is the right choice: validation outcomes are wiring-driven (production: audited; tests: in-memory backstop) rather than domain-driven. Aligns with `IFieldDecryptor` precedent. CLEAN.
- One minor narrative concern: §3.13 says "the audit emission is non-optional." This contradicts the `IFieldDecryptor` two-overload-ctor pattern (audit-disabled for tests/bootstrap). Recommend §3.13 clarify: "for production wiring, audit emission is non-optional; for test fixtures (`InMemoryValidationStatusStore`), the audit-disabled construction overload is permitted, mirroring `TenantKeyProviderFieldDecryptor`." Mechanical clarification.

**Disposition:** B3's substrate fix is clean. `IValidationStatusStore` composes correctly with the cohort. Mechanical clarification recommended on the test-fixture audit-disabled overload.

### §1.4 — B4 (`IStandingOrderIssuer.IssueAsync` composition fixed)

**Verdict: PASS, with author Deviation 1 sound.**

**Three-direction structural-citation:**

- **Positive existence:** `IStandingOrderIssuer.IssueAsync` confirmed at `packages/foundation-wayfinder/IStandingOrderIssuer.cs`. Signature: `Task<StandingOrder> IssueAsync(StandingOrderDraft draft, ActorId issuedBy, IAuditTrail auditTrail, CancellationToken ct)`. ADR §3.5 issuance composition rationale (line ~427) cites this signature accurately, including the `ActorId issuedBy` and `IAuditTrail auditTrail` parameters and the `Task<StandingOrder>` return.
- **Negative existence:** `git grep PrincipalId` in the ADR body returns 2 matches, both inside drift-narration tables (frontmatter amendment summary line ~41; §A0.6 line ~1178). No load-bearing `PrincipalId issuedBy` claims in §3 / §5. CLEAN.
- **Shape correctness:** §3.5 wrapper methods all return `Task<StandingOrder>` (consistent with `IssueAsync`'s return). §3.6 `ActiveProviderSnapshot.ActivatedBy` is `ActorId` (consistent with `StandingOrder.IssuedBy` at `packages/foundation-wayfinder/StandingOrder.cs:43` which is also `ActorId`). PASS.

**Author Deviation 1 review (`IAuditTrail` constructor-injected + passed as method parameter):**

- Council first-pass §9.2 disposition #1 offered options (a) "wrapper methods compose `IStandingOrderIssuer.IssueAsync` correctly" or (b) "domain-shaped wrapper with internal sourcing." Author chose (b) — the rationale paragraph in §3.5 (lines ~427–429) explicitly cites this disposition and explains both `ActorId` is sourced from `IIntegrationAtlasContext.CurrentActorId` (§3.11) and `IAuditTrail` is constructor-injected (§6.1).
- Composition correctness: the wrapper still ultimately calls `IssueAsync(draft, actorId, auditTrail, ct)` per the actual signature. Substrate audit-by-construction is preserved — the wrapper cannot omit the audit emission because `IStandingOrderIssuer.IssueAsync` REQUIRES it as a parameter and the wrapper sources it from a constructor-required dependency. Author Deviation 1 is structurally sound.
- Minor narrative concern: the rationale paragraph could be more explicit that the constructor-injected `IAuditTrail` is the same DI container singleton (kernel-audit's canonical instance), not a separate audit channel. This would close any reviewer concern about audit-fanout. Mechanical clarification (§3.5 add one sentence: "The constructor-injected `IAuditTrail` MUST be the host's canonical kernel-audit singleton; ADR 0067 implementations MUST NOT introduce a separate audit channel.").

**Disposition:** B4's structural fix is clean. Author Deviation 1 is sound; one mechanical narrative clarification recommended.

---

## §2 — Non-mechanical-fix audit

### §2.1 — NM1 (package shape kept additive to existing `packages/ui-core/`)

**Verdict: PASS — consistent with ADR 0066 PR #529 which is still OPEN (not merged).**

**Halt-condition #4 check:** ADR 0066 PR #529 status verified via `gh pr view 529 --json state` — `state: OPEN`, `mergedAt: null`, `headRefName: docs/adr-0066-helm-and-identity-atlas`. PR #529 has not closed/merged with a different namespace shape since the first-pass council. NM1 disposition stands.

**ADR 0066 PR #529 namespace shape spot-check:** `gh pr diff 529 | grep ui-core` confirms ADR 0066 PR's package text reads "Two distinct contracts in `Sunfish.UICore.Wayfinder` (a new namespace within the existing `packages/ui-core/` package — additive, no new package required)" and "New types in `packages/ui-core/Wayfinder/`". ADR 0067's §3 placement (`packages/ui-core/Wayfinder/Integrations/` namespace `Sunfish.UICore.Wayfinder.Integrations`) is consistent with that shape — additive subnamespace under ADR 0066's ui-core/Wayfinder/ tree. CLEAN.

**§"Decision" (line 174–176), §3 (line 256), §10 Phase 1 (line 1030–1031), and §A0.5 (line 1170–1172) all consistently declare "additive to the existing `packages/ui-core/` package — no new package created."** No remaining `packages/ui-core-wayfinder/` references in the ADR body. CLEAN.

**Disposition:** NM1 lands cleanly.

### §2.2 — NM2 (license-acknowledgement track CUT entirely; deferred to ADR 0067-A1)

**Verdict: PASS substantively; 4 dangling-reference sites remain (mechanical, below halt-condition #3 threshold of 5+).**

**Symbol-level deletion sweep (`grep -nE "LicensePostureKind|LicenseAcknowledgementRequiredException|IssueLicenseAcknowledgementAsync|IntegrationLicenseAcknowledged|license-acknowledged\."`):** 12 matches; ALL are inside explicit deferral/historical narration (§2.4, §3.1 deferral note, §3.3, §3.5 deferral note, §3.9 deferral note, §3.10, §4.2 deferral, §5.1 ("no license posture"), §5.5, §8 audit-event-list comment, §9.5, §9.7, §A0.6 drift-table, §A1 Pedantic-Lawyer note). No load-bearing references remain. CLEAN.

**Broader textual sweep (`grep -niE "License|SSPL|BSL|acknowledg|StrongCopyleft|WeakCopyleft|PostureKind|Permissive"`):** 30 matches. After classification:

| Site | Disposition |
|---|---|
| Lines 44, 72, 94, 174, 192, 223, 244, 274, 325, 327, 431, 467 (NB: 467 examined separately below), 495, 497, 499, 580, 639, 645, 738, 740, 895, 907, 914, 944, 989, 991, 997–1006, 1186, 1229, 1236 | Inside explicit deferral/historical narration (§9.7, §A0.6, §A1). CLEAN. |
| Line 54 (Council posture: "+ security-engineering subagent (... license posture)") | Minor — "license posture" in v1 effectively means ADR 0061's compile-time exclusion, which security-engineering still reviews. CLEAN, fuzzy. |
| **Line 91** (Decision drivers #3): "Every provider change, every credential update, **every license acknowledgement**, every validation outcome MUST emit an `AuditRecord`" | **DANGLING.** License-acknowledgement is cut from v1; there is no acknowledgement audit event. Recommend remove "every license acknowledgement," from this list. **Mechanical fix.** |
| **Line 115** (Option A cons): "License-posture acknowledgement (ADR 0061) ends up duplicated in each mesh adapter's UI." | **DANGLING + INACCURATE.** ADR 0061 does not have an acknowledgement UX; this con-list item presupposes a reality the rest of the ADR has cut. Recommend remove. **Mechanical fix.** |
| **Line 121** (Option B description): "Adapter packages declare an `IntegrationProviderSchema` (provider name, category, credential field specs, **license posture**)." | **DANGLING.** §3.1 cut the `LicensePosture` field. Recommend remove "license posture" from the parenthetical list. **Mechanical fix.** |
| **Line 148** (Option D cons): "Cross-category concerns (credential masking, WCAG-compliant accessible authentication, audit emission, **license-posture acknowledgement**) must be re-implemented..." | **DANGLING.** Same issue as line 121. Recommend remove "license-posture acknowledgement" from the parenthetical. **Mechanical fix.** |
| **Line 165** (Option C cons): "License-posture acknowledgement (ADR 0061) for mesh VPN cannot be captured at all — ADR 0061 mandates an interactive acknowledgement." | **DANGLING + DOUBLY INACCURATE.** ADR 0061 does NOT mandate an interactive acknowledgement (the entire NM2 fix flowed from this exact misreading). Recommend rewrite as: "License-posture for mesh VPN — ADR 0061's compile-time `BannedSymbols.txt` exclusion can still be enforced, but if a future ADR 0067-A1 admin-opt-in track lands, Option C cannot capture the acknowledgement." **Mechanical fix.** |
| **Line 467** (`IIntegrationSchemaProvider` reconciliation): "carries the additional schema metadata that the Atlas surface needs (credential field specs, **license posture**, autocomplete hints)" | **DANGLING.** `IntegrationProviderSchema` no longer carries license posture in v1. Recommend remove "license posture" from the parenthetical. **Mechanical fix.** |

**Total dangling-reference sites:** 4 substantive (lines 91, 115, 121/148 [count as one — same paragraph pattern], 165, 467). Below the §"Halt-conditions" #3 threshold of 5+ sites that would suggest a rushed deletion. NOT a halt; mechanical fixes auto-acceptable.

**Council disposition: NM2 LANDS** with 4 mechanical-tier dangling-reference fixes recommended (none load-bearing; all in cons/description text where the reader can infer from §"Decision drivers" #6 and §9.7 deferral that license-acknowledgement is out of v1).

### §2.3 — NM3 (webhook-secret rotation race specification)

**Verdict: PASS.**

§5.7's "Webhook-secret rotation window" subsection (line ~779) specifies: "validators MUST accept signatures from EITHER the previous OR the new webhook-secret during the rotation grace window." §5.7's "Grace-window authority" subsection (line ~781) specifies: "grace window ends when the previous credential's `EncryptedField` is excluded from a future Atlas projection — i.e., the next rotation evicts it via the §5.7 step 6 'Clear unused credentials' admin action, OR a future rotation issues a new credential at the same `(provider, credentialKey)` path that supersedes the prior value via Standing-Order LWW semantics. There is no automatic time-bound expiry of the previous webhook secret; eviction is admin-driven (or rotation-driven via subsequent issuance), not clock-driven."

This matches ADR 0065's append-only Standing-Order log semantics (LWW resolution per `IAtlasProjector` projection); no substrate-side TTL tracking needed. PASS structurally.

**§9.9 cross-references §5.7's resolution.** Open-question correctly closed. CLEAN.

### §2.4 — NM4 (`EncryptedField.KeyVersion` rotation handling)

**Verdict: PASS.**

§4.2.1 specifies: "the v1 reference implementation maintains both v1 and v2 decryptors simultaneously: `IFieldDecryptor` resolves the per-version DEK at decrypt time (per `TenantKeyProviderFieldDecryptor` on origin/main) and the validator continues to function across the rotation. A re-encrypt sweep that rewrites prior credentials at the new key version is out of v1 scope (deferred to a future amendment per §9.4); ADR 0067 v1 relies on the multi-version decryptor pattern."

Spot-check on origin/main: `TenantKeyProviderFieldDecryptor` does support per-version DEK resolution via `EncryptedField.KeyVersion` — verified by reading the audit-emission lines at `packages/foundation-recovery/Crypto/TenantKeyProviderFieldDecryptor.cs:116, 125`. The substrate already supports the multi-version posture; ADR's claim is structurally correct.

**§9.8 captures the deferred re-encrypt sweep as `IIntegrationCredentialReencryptSweep` (open question).** Reasonable deferral. CLEAN.

### §2.5 — NM5 (OAuth callback hijack scope specification)

**Verdict: PASS.**

§9.6 explicitly enumerates the four OAuth-flow concerns that v1 does NOT address: "(a) callback URL whitelisting and per-tenant callback uniqueness; (b) CSRF-resistant `state` token generation and cross-tenant collision prevention; (c) PKCE challenge/verifier flow; (d) `aria-live` announcements for the popup/redirect lifecycle." The disposition reads: "Adapter authors MUST NOT add an OAuth-backed `IIntegrationSchemaProvider` to v1 without a companion ADR that addresses these requirements — doing so would leave CSRF and cross-tenant `state` collision undefined at the surface level. Disposition: OAuth-flow providers are explicitly OUT of v1 scope; the first OAuth provider triggers a follow-up ADR."

This is the correct discipline: explicit out-of-scope flag + explicit blocker on adapter authors who might naively add an OAuth provider without the companion ADR. PASS.

---

## §3 — Author-deviation review

### §3.1 — Deviation 1: §3.5 wrapper kept ctor-injected `IAuditTrail`; passes it as method parameter

**Verdict: SOUND. Council option (b) chosen, well-rationalized.**

Per first-pass §9.2 disposition #1: council preferred option (b) — "the surface should be domain-shaped, not substrate-shaped — but the rationale must be explicit + the implementation pattern must be specified." The fix-pass §3.5 issuance composition rationale paragraph (lines ~427–429) explicitly:

1. Cites `IStandingOrderIssuer.IssueAsync(StandingOrderDraft draft, ActorId issuedBy, IAuditTrail auditTrail, CancellationToken ct)` as the actual signature on origin/main.
2. Explains how the `ActorId issuedBy` is sourced (`IIntegrationAtlasContext.CurrentActorId` at §3.11).
3. Explains how `IAuditTrail auditTrail` is sourced (constructor-injected dependency at §6.1).
4. Justifies the domain-shaped wrapper choice: "callers pass category-specific arguments rather than constructing a `StandingOrderDraft` themselves."
5. Cross-references that this is "the canonical 'domain-shaped wrapper over substrate API' idiom that ADR 0066's `IIdentityAtlasSurface` also follows."

The rationale composes structurally: the wrapper's domain-shaped surface preserves substrate audit-by-construction because the wrapper's implementation MUST call `IssueAsync(draft, actorId, auditTrail, ct)` — there is no path to issue a Standing Order without the audit-emission parameter. Author Deviation 1 is sound.

**Minor mechanical-tier clarification:** the rationale could be more explicit that the constructor-injected `IAuditTrail` is the host's canonical kernel-audit singleton (no separate audit channel). One-sentence add to §3.5 closes the reviewer concern about audit-fanout. Recommended.

### §3.2 — Deviation 2: No new `CapabilityIssued` audit event; reuses existing `FieldDecrypted` / `FieldDecryptionDenied`

**Verdict: SOUND.**

**Three-direction structural-citation:** verified `AuditEventType.FieldDecrypted` and `AuditEventType.FieldDecryptionDenied` exist on origin/main at `packages/kernel-audit/AuditEventType.cs:280, 283`. Verified `TenantKeyProviderFieldDecryptor` emits both events at `packages/foundation-recovery/Crypto/TenantKeyProviderFieldDecryptor.cs:116, 125`. Verified ADR 0046-A4 spec (embedded in `0046-key-loss-recovery-scheme-phase-1.md` §A2.4 audit-emission shape).

**Rationale:** introducing a separate `CapabilityIssued` event would have been double-audit work without distinguishing semantics — every successful capability acquisition is followed by `FieldDecryptor.DecryptAsync` which already emits `FieldDecrypted` carrying the `CapabilityId`. The capability lifecycle is already captured by the recovery substrate's audit emission; an `IIntegrationAtlasProvider`-level `CapabilityIssued` event would be orthogonal noise. Sound deviation.

### §3.3 — Deviation 3: §3.14 added a separate declaration block for `IDecryptCapabilityProvider`

**Verdict: SOUND. Cohort idiom CLEAN.**

`IDecryptCapabilityProvider` is a NEW substrate contract introduced by ADR 0067 (per §A0.7 net-new symbol table). The interface surface:

```csharp
public interface IDecryptCapabilityProvider
{
    ValueTask<IDecryptCapability?> AcquireAsync(
        TenantId tenant,
        string purpose,
        TimeSpan ttl,
        CancellationToken ct);
}
```

**Cohort idiom check (foundation-tier `*Provider` interfaces):**

- `ITenantKeyProvider.DeriveKeyAsync(TenantId tenant, string purpose, CancellationToken ct)` — same `(TenantId, string purpose, CancellationToken)` argument shape pattern.
- `IFieldEncryptor.EncryptAsync(ReadOnlyMemory<byte> plaintext, TenantId tenant, CancellationToken ct)` — `TenantId` is a load-bearing parameter (tenant-scoping is structural).

`IDecryptCapabilityProvider` matches the cohort: `TenantId` first (canonical placement for tenant-scoped operations), `string purpose` second (matches `ITenantKeyProvider`'s purpose-label convention), `TimeSpan ttl` third (capability-specific), `CancellationToken ct` last. PASS.

**Tenant-scoping security correctness (§3.14 doc-comment):** "Implementations MUST validate the requested tenant scope before issuing — cross-tenant capability issuance is prohibited per ADR 0046-A4." This is the correct disposition for the cross-tenant credential-leak vector. The contract-level requirement (rather than implementation-only discipline) is the right way to encode this — Bridge multi-tenant hosts CANNOT issue an `IDecryptCapability` for a tenant outside the requesting principal's scope. PASS.

**Minor mechanical-tier finding:** the `purpose` string `"integration-validation"` is documented in §3.14 + §5.3.1 prose but has no `internal const string` declaration. Cohort precedent (e.g., `TenantKeyProviderFieldEncryptor.PurposeLabel`) declares purpose strings as named constants to avoid magic-string drift at the call site. Recommend Phase 1 deliverable add: `IntegrationCapabilityPurposes.IntegrationValidation = "integration-validation"` (or similar). Mechanical addition.

### §3.4 — Deviation 4: §A0.7 added a net-new symbol declaration table

**Verdict: SOUND. Completeness check PASS.**

The §A0.7 table lists every net-new symbol the ADR introduces. Cross-checked against the §3 type declarations:

| §A0.7 entry | Declared in | Present in §3? |
|---|---|---|
| `IIntegrationAtlasProvider` | §3.5 | YES |
| `IIntegrationSchemaProvider` | §3.7 | YES |
| `IIntegrationProviderValidator` | §6.2 | YES |
| `IIntegrationAtlasContext` | §3.11 | YES |
| `IValidationStatusStore` | §3.13 | YES |
| `IDecryptCapabilityProvider` | §3.14 | YES |
| `IntegrationProviderSchema` | §3.1 | YES |
| `CredentialFieldSpec` / `CredentialAutocompleteHint` / `CredentialFieldKind` | §3.2 | YES |
| `IntegrationCategory` / `IntegrationCategoryMapping` | §3.4 | YES |
| `IntegrationAtlasView` / `ActiveProviderSnapshot` | §3.6 | YES |
| `IntegrationValidationResult` / `ProviderValidationStatus` / `ProviderValidationStatusEntry` | §3.8 / §3.9 / §3.13 | YES |
| `IntegrationEmailRouting` | §3.12 | YES |
| `ICustomIntegrationRenderer` | §6.3 | YES |
| `AddSunfishIntegrationAtlas()` | §6.1 | YES |
| 4 new `AuditEventType` constants | §8 | YES |

Net-new symbols not in §A0.7 but introduced by the ADR: `DefaultIntegrationAtlasProvider` (§7.1 — implementation, not a contract; arguably not a NEW *symbol* in the cohort-canon sense); `InMemoryIntegrationAtlasProvider` (§7.2 — same); `DefaultValidationStatusStore` / `InMemoryValidationStatusStore` (§3.13 — same); `IntegrationAuditPayloads.cs` factories (§8 — Phase 2 deliverable). These are reference impls / Phase-2 derivatives; the §A0.7 table appropriately scopes itself to ADR-tier contract symbols. CLEAN.

`UnknownProviderException` (referenced in §3.5 / §3.12) and `DuplicateValidatorRegistrationException` (referenced in §6.2.1) are also net-new but not in §A0.7. Minor — these are exception types (boilerplate; cohort convention is to not enumerate exception types in symbol-introduction tables). CLEAN, fuzzy.

**Disposition:** Deviation 4 (§A0.7 net-new symbol table) lands. Author chose the right scope.

---

## §4 — Net-new findings introduced by the rework

### §4.1 — `IValidationStatusStore` cohort-idiom check

PASS. (Detailed above in §1.3.)

### §4.2 — `IDecryptCapabilityProvider` cohort-idiom check + ADR 0046/0061 conflict check

PASS. (Detailed above in §3.3.) No conflict with ADR 0046 (capability sourcing was an open hole on origin/main; ADR 0067 fills it via this provider). No conflict with ADR 0061 (which is about transport-tier license-screening; orthogonal to capability provisioning).

### §4.3 — Validator-owned probe URL spot-check

1 mechanical-tier finding: Postmark probe URL drift (§1.1 above). Stripe + Tailscale OK.

### §4.4 — License-acknowledgement deletion completeness

4 dangling-reference sites (§2.2 above). Below halt-condition threshold; mechanical-tier fixes auto-acceptable.

### §4.5 — `ProviderDescriptor.Key` reconciliation drift

**Mechanical-tier AP-21 finding (carry-through, not introduced by the rework but still unresolved).**

§3.7 says: "Both registrations share the `ProviderName` / `Key` string identity."
§3.1 says: "`ProviderName` is the canonical adapter package name (e.g. `providers-stripe`) — matches `ProviderDescriptor.Key`'s string form (per §3.7 reconciliation)."

Verified at `packages/foundation-integrations/ProviderDescriptor.cs`: `Key` is documented as "Stable reverse-DNS-style provider key (e.g. `sunfish.providers.stripe`)." That's NOT the same string as `providers-stripe` — it's `sunfish.providers.stripe` (dotted reverse-DNS).

**Disposition:** mechanical fix recommended. Either:
- (a) §3.1 / §3.7 reframe: "`ProviderName` is a stable identifier for the adapter package; first-wave adapter packages use the kebab-case `providers-stripe` form, which is distinct from `ProviderDescriptor.Key`'s reverse-DNS form (`sunfish.providers.stripe`). The two carry different purposes — `ProviderName` is the Atlas-fine identity for schema lookup; `ProviderDescriptor.Key` is the catalog-substrate routing identity. Adapter packages MAY register both forms or align them via a downstream amendment."
- (b) §3.1 / §3.7 align: choose one canonical form (council recommends the kebab-case `providers-stripe` for `IntegrationProviderSchema.ProviderName` since that matches the package-folder convention; defer reconciliation with `ProviderDescriptor.Key` to a follow-on ADR or amendment).

Either path is mechanical-tier. Below halt threshold.

---

## §5 — Cohort-idiom check summary

| Symbol | Cohort idiom | ADR conformance |
|---|---|---|
| `IValidationStatusStore` | constructor-audit-emitter (matches `IFieldDecryptor`) | CLEAN; one mechanical-tier clarification on test-fixture audit-disabled overload |
| `IDecryptCapabilityProvider` | foundation-tier `(TenantId, string purpose, ...)` (matches `ITenantKeyProvider`) | CLEAN |
| Wrapper composition over `IStandingOrderIssuer` | domain-shaped wrapper preserves substrate audit-by-construction (matches `IIdentityAtlasSurface` pattern from ADR 0066 PR #529) | CLEAN |
| Error-code strings | kebab-case (matches `"wrong-tenant"`, `"expired"`) | `"no-decrypt-capability"` matches; CLEAN |
| Audit-event constants | `AuditEventType` record-struct + `public static readonly` (matches `kernel-audit/AuditEventType.cs`) | 4 net-new constants follow the pattern; CLEAN |
| `*Audit*PayloadFactory` allowlist pattern | matches `FieldEncryptionAuditPayloadFactory` | `IntegrationAuditPayloads` (Phase 2) follows the precedent; CLEAN |
| Roslyn analyzer for audit-redaction | matches `SUNFISH_WAYFINDER001` (per ADR 0066 PR #529) | `SUNFISH_INTEGRATION_AUDIT001` follows the naming convention; CLEAN |

**No cohort-idiom violation found.** All net-new substrates compose correctly with the foundation-tier conventions.

---

## §6 — Crypto/security-correctness pass

### §6.1 — Capability sourcing fail-closed semantics (§5.3.1)

PASS. Fail-closed three-mode posture: capability provider not registered, capability validation fails (`ValidateForDecrypt` returns non-null reason), capability TTL expired or tenant-scope mismatched. All three yield `Status = Unknown`, `ErrorCode = "no-decrypt-capability"`, `IntegrationValidationFailed` audit emission, and `IValidationStatusStore` persistence. Background-driven path additionally emits a host-level diagnostic. No silent skip path.

### §6.2 — Cross-tenant capability-leak prevention (Bridge multi-tenant)

PASS. §5.3.1: "the host process holds N tenant capabilities; the wrong one is rejected by `IDecryptCapability.ValidateForDecrypt`." `IDecryptCapability.ValidateForDecrypt(targetTenant, now)` is the structural gate — confirmed at `packages/foundation-recovery/Crypto/IDecryptCapability.cs` and the `FixedDecryptCapability` reference impl returns `"wrong-tenant"` on tenant mismatch. Bridge multi-tenant credential-leak vector is structurally closed.

§3.14 reinforces at the contract level: "Implementations MUST validate the requested tenant scope before issuing — cross-tenant capability issuance is prohibited per ADR 0046-A4." Defense in depth.

### §6.3 — `IFieldDecryptor` scope isolation (§6.1.1)

PASS structurally. §6.1.1 specifies `IFieldDecryptor` MUST NOT be registered in the same DI container scope as components the rendering host can resolve. Phase 2 includes a unit test asserting this (`IFieldDecryptor` cannot be resolved from a Blazor-scoped `IServiceProvider` built via `AddSunfishIntegrationAtlas()` alone). Renderer cannot opportunistically decrypt; capability gate is structural.

### §6.4 — Plaintext lifetime constraints (§5.3.2)

PASS. §5.3.2 specifies validator MUST `CryptographicOperations.ZeroMemory` owned plaintext buffers in `finally`, NEVER log credential bytes / include in exception messages / cache decrypted credentials across calls. Phase 3 per-adapter parity tests assert these properties (positive marker test + negative provider-failure test).

### §6.5 — Audit-payload redaction enforcement (§8)

PASS. Three-tier defense:
1. **Compile-time (Phase 2):** `SUNFISH_INTEGRATION_AUDIT001` Roslyn analyzer enforces typed factory methods; free-form `JsonNode`/`Dictionary<string, object>` construction prohibited.
2. **Runtime (Phase 2):** `IntegrationAuditRedactionTests` corpus test injects marker credentials, exercises all §5 flows, asserts no `AuditRecord.Payload` JSON contains the marker.
3. **Documented contract (§8):** allowlist of forbidden field-name patterns (`value`, `apiKey`, `secret`, `password`, `token`, `webhookSecret`, `credential.*`, `*.value`); case-insensitive matcher with hyphen-normalization. Recursive walk over `AuditPayload.Body` dictionary.

Cohort idiom: matches `FieldEncryptionAuditPayloadFactory` allowlist precedent in `packages/foundation-recovery/Audit/`. CLEAN.

---

## §7 — Verdict + finding count + cohort-batting-average update

**Verdict: NEEDS-AMENDMENT (mechanical-tier; auto-acceptable per cohort precedent).**

The fix-pass resolves all 4 BLOCKING and all 5 non-mechanical first-pass findings. The 4 author deviations are sound. The 2 net-new substrates compose cleanly with foundation-tier cohort idioms. NO net-new BLOCKING finding introduced. NO halt-condition tripped.

**Finding count by classification:**

| Class | Count | Disposition |
|---|---|---|
| BLOCKING (net-new) | 0 | — |
| Non-mechanical (net-new) | 0 | — |
| Mechanical (net-new or carry-through) | 5 | auto-acceptable |
| Structural-citation (net-new or carry-through) | 1 | mechanical fix |
| **TOTAL net-new + carry-through** | **6** | all mechanical-tier |

**Per-finding mechanical-tier list (auto-acceptable):**

1. **§5.3 + §6.2 Postmark probe URL** — change `/servers` to `/server` (singular), or note that `/servers` validates the Account-Token (different credential than the cited Server-Token). AP-21 cited-endpoint drift.
2. **NM2 dangling-reference sweep** — 4 sites in cons/description text (lines 91, 115, 121, 148, 165, 467) that narrate license-acknowledgement as if it were a v1 surface. Two of those (lines 115 + 165) inaccurately ascribe to ADR 0061 a posture ADR 0061 does not have. Mechanical fix.
3. **§3.5 issuance composition rationale** — add one sentence clarifying the constructor-injected `IAuditTrail` is the host's canonical kernel-audit singleton (not a separate audit channel). Mechanical narrative add.
4. **§3.13 `IValidationStatusStore` audit-emission posture** — clarify that the test-fixture (`InMemoryValidationStatusStore`) MAY use an audit-disabled construction overload (mirroring `TenantKeyProviderFieldDecryptor`'s two-overload pattern). Mechanical narrative clarification.
5. **§3.14 `IDecryptCapabilityProvider` purpose-string declaration** — Phase 1 deliverable add: `IntegrationCapabilityPurposes.IntegrationValidation = "integration-validation"` named constant (matches cohort precedent). Mechanical addition.
6. **§3.7 / §3.1 `ProviderDescriptor.Key` reconciliation** — `ProviderName = "providers-stripe"` does not match `ProviderDescriptor.Key`'s reverse-DNS form (`sunfish.providers.stripe`). Either reframe the §3.7 reconciliation, or align identifiers, or defer the reconciliation to a follow-on ADR. Mechanical fix or scope-defer.

All six are below the §1.5 council disposition's "mechanical fix" auto-accept threshold. None is a halt-condition. The ADR is structurally and conceptually correct; the residue is narrative cleanup + one cited-URL drift + one carry-through identity-shape question.

**Recommended path:** apply mechanical fixes 1–6 inline; flip ADR status to `Accepted`; merge PR #539. The 6 mechanical fixes can ride a follow-up `chore(adrs): 0067 — re-council mechanical fixes` commit on the same PR (or a quick follow-up PR if the author prefers).

**Cohort batting average update:**

- Pre-ADR 0067: 28-of-29 substrate amendments needed council fixes (96.6%).
- ADR 0067 makes it: **29-of-30 (96.7%)** substrate amendments needing council fixes — first-pass council found 23 findings (4 BLOCKING + 5 NM + 8 mechanical + 6 SC); fix-pass resolved all substantive findings; second-pass re-council found 6 mechanical-tier residue. The pre-merge canonical posture continues to be the right discipline.
- The first-pass council was load-bearing: the 4 BLOCKING findings (especially the validator-owned-probe rework and the `IValidationStatusStore` substrate-tier separation) would have required a post-acceptance amendment cycle if council had not run pre-merge. Cohort lesson holds.

---

## Appendix — Full grep results for the dangling-reference sweep

### A.1 — Symbol-level cuts (verified all are inside deferral/historical narration; no load-bearing references remain)

```
$ grep -nE "LicensePostureKind|LicenseAcknowledgementRequiredException|IssueLicenseAcknowledgementAsync|IntegrationLicenseAcknowledged|license-acknowledged\." docs/adrs/0067-atlas-integration-config-surface.md
223: §2 "License-acknowledgement is NOT a Standing Order in v1" (deferral note)
244: §2.4 (deferred)
274: §3.1 deferral note
325: §3.3 (deferred)
327: §3.3 deferral body
431: §3.5 deferral note
497: §3.10 (deferred)
499: §3.10 deferral body
944: §8 deferred audit-event-list comment
999: §9.7 master deferral
1005: §9.7 Phase-deliverable omissions
1186: §A0.6 drift-table row
```

ALL inside deferral/historical narration. CLEAN.

### A.2 — Broader textual sweep ("License" / "SSPL" / "BSL" / "acknowledg" / "StrongCopyleft" / "WeakCopyleft" / "PostureKind" / "Permissive")

```
Lines correctly inside deferral/historical narration (CLEAN):
44, 72, 94, 174, 192, 223, 244, 274, 325, 327, 431, 495, 497, 499, 580,
639, 645, 738, 740, 895, 907, 914, 944, 989, 991, 997, 1003, 1005, 1006,
1186, 1229, 1236

Line 54 (Council posture: security-engineering subagent reviews "license posture"):
CLEAN (fuzzy — "license posture" in v1 effectively means ADR 0061's
compile-time exclusion, which security-engineering still reviews).

Line 91 (Decision drivers #3 list: "every license acknowledgement"):
DANGLING (mechanical fix — remove from list).

Line 115 (Option A cons: "License-posture acknowledgement (ADR 0061)
ends up duplicated in each mesh adapter's UI"):
DANGLING + INACCURATE (mechanical fix — remove or rewrite).

Line 121 (Option B description: "credential field specs, license posture"):
DANGLING (mechanical fix — remove "license posture" from parenthetical).

Line 148 (Option D cons: "credential masking, [...], audit emission,
license-posture acknowledgement"):
DANGLING (mechanical fix — remove "license-posture acknowledgement" from
parenthetical).

Line 165 (Option C cons: "License-posture acknowledgement (ADR 0061)
[...] ADR 0061 mandates an interactive acknowledgement"):
DANGLING + DOUBLY INACCURATE (mechanical fix — rewrite per §2.2).

Line 467 (§3.7 reconciliation: "credential field specs, license posture,
autocomplete hints"):
DANGLING (mechanical fix — remove "license posture" from parenthetical).
```

**Total dangling sites: 6.** Below halt-condition #3 threshold of 5+ that suggests rushed deletion (treating lines 121 + 148 + 467 as one stylistic-pattern cluster gives 4 substantive sites; even counting them individually gives 6, still mechanical-tier). Not a halt; mechanical fixes auto-acceptable.

### A.3 — Three-direction citation discipline scorecard

- **Positive existence verified for net-new substrates:** `IValidationStatusStore`, `IDecryptCapabilityProvider`, `IIntegrationAtlasProvider`, `IIntegrationSchemaProvider`, `IIntegrationProviderValidator`, `IIntegrationAtlasContext`, `IntegrationProviderSchema`, `CredentialFieldSpec`, `IntegrationCategory`, `IntegrationAtlasView`, `IntegrationValidationResult`, `ProviderValidationStatus`, `IntegrationEmailRouting`, `ICustomIntegrationRenderer`, 4 `AuditEventType` constants — all declared in §3 / §6 / §8 with appropriate XML-doc summaries.
- **Negative existence verified for cited absent symbols:** `IPaymentGateway.ValidateAsync`, `IMessagingGateway.ValidateAsync`, `IMeshVpnAdapter.ValidateAsync` (none exist on origin/main; ADR §6.2 correctly does not depend on them); `CapabilityIssued` audit event (does not exist; author Deviation 2 reuses existing `FieldDecrypted`/`FieldDecryptionDenied`).
- **Shape correctness verified for cited present symbols:** `IStandingOrderIssuer.IssueAsync` (matches §3.5 composition); `IFieldDecryptor.DecryptAsync` (matches §5.3 capability-acquisition); `IDecryptCapability.ValidateForDecrypt` (matches §5.3.1 fail-closed); `EncryptedField.KeyVersion` (matches §4.2.1 multi-version posture); `AuditEventType` record-struct shape (matches §8 declaration pattern); `StandingOrder.IssuedBy : ActorId` (matches §3.6 `ActiveProviderSnapshot.ActivatedBy : ActorId`); `IDecryptCapability` rejection codes `"wrong-tenant"`, `"expired"` (matches §5.3.1 cohort idiom for `"no-decrypt-capability"`); `ITenantKeyProvider.DeriveKeyAsync(TenantId, string purpose, CancellationToken)` (matches §3.14 `IDecryptCapabilityProvider.AcquireAsync` argument-shape cohort idiom); `ProviderDescriptor.Key` (DOES NOT match §3.7's reconciliation claim — see §4.5 mechanical-tier finding).

**Shape-correctness failures: 1 (`ProviderDescriptor.Key` reconciliation drift).** All others CLEAN.

### A.4 — Cohort batting

This re-council adds to the running tally: **29-of-30 (96.7%)** substrate amendments needing council fixes. ADR 0067 first-pass council was load-bearing (4 BLOCKING; would have required post-acceptance amendment cycle); fix-pass resolved all substantive findings; re-council surfaces 6 mechanical-tier residue (auto-acceptable). The pre-merge council canonical posture continues to be the right discipline.

---

*End of re-council review.*

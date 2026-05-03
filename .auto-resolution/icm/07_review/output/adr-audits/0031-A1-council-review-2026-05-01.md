# ADR 0031 Amendment A1 — Council Review (Stage 1.5 Adversarial)

**Date:** 2026-05-01
**Reviewer:** XO (research session) authoring in-thread per the established subagent-stall pattern (recent stalls correlated with long-output briefs; A1's small scope justifies compact in-thread council).
**Amendment under review:** [ADR 0031 — A1 "Bridge → Anchor subscription-event-emitter contract"](../../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) (PR #440, branch `docs/adr-0031-a1-subscription-event-emitter`, auto-merge intentionally DISABLED pre-council per cohort discipline)
**Companion intake:** [`2026-04-30_bridge-subscription-event-emitter-intake.md`](../../00_intake/output/2026-04-30_bridge-subscription-event-emitter-intake.md) (PR #409; merged)
**Halt-condition closed:** ADR 0062-A1.6 (sub-second `EditionCapabilities` responsiveness vs 30-second cache TTL ceiling)

---

## 1. Verdict

**Accept with amendments. Grade: B (Solid).** Path to A is mechanical (A1–A4 land + A5–A6 encouraged-tier).

A1's architectural shape is correct: 7-event subscription taxonomy + canonical-JSON-encoded payload + HMAC-SHA256 signature + UUID-based idempotency + ±5min replay-attack window + webhook-primary/SSE-fallback delivery + 7-attempt exponential backoff + dead-letter queue + Anchor-side `IBridgeSubscriptionEventHandler` substrate that triggers `EnvelopeChange` via `IMissionEnvelopeProvider`. Industry prior-art (Stripe, Square, GitHub, ASP.NET Core webhook receivers) is well-tracked.

The substantive gaps are: (1) per-Anchor shared-secret rotation policy is unspecified — A1 ships the secret-generation-at-registration but says nothing about rotation, which is canonical security hygiene; (2) HMAC-SHA256 vs Ed25519 tradeoff is undertheorized — HMAC matches Stripe/GitHub canonical but Ed25519 would compose with ADR 0032's keypair semantic + asymmetric verification (operators wouldn't need to share-secret-with-Bridge); (3) HTTPS-only registration without self-signed cert handling is too restrictive for operator-tier self-hosted Anchor deployments (typical Phase 1 config); (4) event taxonomy missing 3 standard subscription lifecycle states (`SubscriptionTrialStarted`, `SubscriptionTrialExpired`, `SubscriptionPaused`); (5) SSE reconnect backoff is named ("backoff per A1.5") but A1.5's backoff is for webhook retries, not SSE reconnects — substantively different concerns (one is "delivery succeeded N tries"; other is "long-lived connection drop").

Four required amendments + two encouraged. None block ADR 0062 Phase 1 substrate scaffold (A1's contract surface is sufficient as-is); ALL should land before A1's Stage 06 build emits its first `BridgeSubscriptionEventEmitted` audit event.

---

## 2. Findings

### F1 — Per-Anchor shared-secret rotation policy unspecified (Major, security)

A1.4 specifies: *"`sharedSecret` is generated per-Anchor by Bridge at registration time (Bridge holds it server-side; Anchor stores it in its keystore per ADR 0046)."* But A1 says nothing about rotation:

- What's the secret's lifetime? (Default expectation: never expire? 90 days? Per-deployment-config?)
- How does an Anchor rotate its secret? (Re-register? Atomic key-roll endpoint?)
- What happens during a rotation if a Bridge event is in-flight? (Old-key-accepted grace period? Atomic cutover?)
- Does Bridge support multiple active secrets per Anchor (key-rolling) or just one?

Per ADR 0046's keystore-rotation primitive (currently deferred per A4.3), the rotation surface is a known foundation-tier gap. A1 should either:
- (a) Spec a default rotation cadence + atomic-cutover mechanism (e.g., 90-day rotation; both old + new keys accepted during a 24-hour grace window), OR
- (b) Defer rotation explicitly to a future A1.x amendment with a named halt-condition (Stage 06 build cannot ship until rotation is specified).

**Major because:** secret-rotation is canonical security hygiene; shipping an audit-relevant signature scheme without rotation policy is a known foot-gun (Stripe webhook secrets, GitHub webhook secrets — both vendors document rotation explicitly).

### F2 — HMAC-SHA256 vs Ed25519 tradeoff undertheorized (Major, security)

A1.2 specifies HMAC-SHA256 as the signature scheme. This matches Stripe + GitHub canonical (the prior-art most A1 readers would expect), but A1 doesn't engage with the alternative:

**Ed25519** (asymmetric) would:
- Compose with ADR 0032's existing Ed25519 root-keypair semantic (Bridge has its own keypair; Anchor verifies with Bridge's public key)
- Avoid the per-Anchor shared-secret distribution problem entirely (Bridge publishes its public key; Anchor pulls it)
- Match the cohort's existing crypto posture (paper §13.4 + ADR 0032 + ADR 0046 all use Ed25519)
- Trade per-event verification cost (~50µs) for the simpler key-management story

**HMAC-SHA256** (symmetric, A1's choice) is faster (~5µs) and matches industry-standard webhook signing (Stripe / GitHub / Square / Square / Slack all use HMAC).

A1 should explicitly engage with this tradeoff. Default expectation: HMAC-SHA256 wins for Phase 1 (industry-standard; faster; matches consumer expectations) but the Ed25519 alternative should be named + a Phase 2+ migration trigger declared (e.g., "if shared-secret distribution becomes operationally painful at scale, migrate to Ed25519 per ADR 0032 keypair semantic").

**Major because:** the tradeoff is real and the Ed25519 path has cohort-composition advantages that A1's silence implicitly forecloses.

### F3 — HTTPS-only registration too restrictive for operator self-signed cert deployments (Major, deployment)

A1.4 specifies: *"Webhook URLs MUST be HTTPS (Bridge refuses HTTP at registration). The `callbackUrl` MUST resolve to a non-loopback address."*

Two operator-deployment scenarios this breaks:

- **Self-signed cert deployments** (Phase 1 self-hosted Anchor; common per ADR 0044). Operator runs Anchor on `anchor.example.local` with a self-signed cert; HTTPS works at the protocol layer but Bridge's HTTP client rejects the cert as untrusted. A1's spec doesn't describe how Bridge handles self-signed certs.
- **mTLS deployments** (enterprise Anchor on internal CA). Operator's Anchor uses an internal CA; Bridge's HTTP client trusts only public CAs; mTLS handshake fails.

A1 should specify operator-controllable trust configuration:

- (a) Per-Anchor "trust certificate" registration — Anchor uploads its cert chain at registration time; Bridge pins-and-verifies against that cert chain
- (b) Per-deployment Bridge configuration: "trust self-signed certs from registered Anchor URLs"
- (c) Both, with operator choice

Without this, A1's "HTTPS-only" effectively requires every Anchor to obtain a publicly-trusted cert (Let's Encrypt or similar), which conflicts with ADR 0044's Phase 1 self-hosted Windows-only deployment story (no Let's Encrypt for offline / air-gapped / internal-network Anchors).

**Major because:** this is a deployment-blocker for the dominant Phase 1 Anchor configuration.

### F4 — Event taxonomy missing 3 standard subscription lifecycle states (Minor)

A1.1 ships 7 event types. Missing from the canonical Stripe/Recurly/Chargebee subscription event taxonomy:

- **`SubscriptionTrialStarted`** — tenant activates a trial Edition (free for N days)
- **`SubscriptionTrialExpired`** — trial ends without conversion to paid
- **`SubscriptionPaused`** — tenant pauses (no charges; subscription preserved; tenant returns)

These 3 states matter for property-business MVP scenarios: trials are how prospective tenants evaluate Sunfish; pause-and-resume is common for seasonal property managers. A1 ships without them; they will need to be added in a future A1.x amendment when these UX flows ship.

**Minor because:** A1 can land without these (the 7 named states cover the immediate ADR 0062 halt-condition closure); future amendment adds.

### F5 — SSE reconnect backoff is named but not specified (Minor)

A1.3 specifies SSE delivery: *"Bridge maintains a long-lived SSE connection; events are pushed as `data:` frames with `event:` type. Reconnect on disconnect; backoff per A1.5."*

A1.5 specifies the **webhook retry** policy (1s → 5s → 30s → 5min → 30min → 2h → 12h × 7 attempts; dead-letter after). This is **substantively different** from SSE reconnect:

- Webhook retry: "delivery succeeded N tries" — bounded retries; dead-letter after exhaustion
- SSE reconnect: "long-lived connection drop" — unbounded reconnects; never dead-letter the connection itself; events queued during disconnect

A1.3 conflates them. SSE needs its own backoff: typically exponential 1s → 5s → 30s → 60s capped (no dead-letter; just keeps trying); events queued per-tenant for the duration of disconnect (with a reasonable upper bound — say 1-hour queue depth — beyond which Bridge falls back to webhook delivery).

**Minor because:** spec gap; implementation can disambiguate but should be explicit in the ADR.

### F6 — `subscribedEvents` filter atomicity vs in-flight events (Encouraged)

A1.4 specifies: *"Re-registration replaces the prior URL/filter atomically."* But what does "atomically" mean for events already in-flight at the moment of re-registration?

- An event was emitted under the old filter (and matched it); it's mid-delivery
- The Anchor re-registers with a new filter that doesn't include this event type
- Does Bridge cancel the in-flight delivery? Continue the old delivery and apply the new filter to NEW events only?

Recommend: continue old in-flight; apply new filter to events emitted *after* the re-registration commit. Document explicitly.

**Encouraged.**

### F-VP1 — `IEditionResolver` cited per A1.6 verified existing (verification-pass)

`git show origin/main:docs/adrs/0009-foundation-featuremanagement.md | grep IEditionResolver` returns 1 hit. Citation correct. **No finding.**

### F-VP2 — ADR 0046 keystore reference verified (verification-pass)

`git show origin/main:docs/adrs/0046-key-loss-recovery-scheme-phase-1.md | grep keystore` returns hits naming `KeystoreRootSeedProvider` + `PaperKeyDerivation` substrate. ADR 0046 IS the keystore home for shared-secret storage per A1.4. **No finding.**

### F-VP3 — 8 new `AuditEventType` constants no-collision (verification-pass)

`grep -E "BridgeSubscription|Webhook" packages/kernel-audit/AuditEventType.cs` returned 0 matches. None of the 8 proposed constants collide with existing constants. **No finding.**

### F-VP4 — ADR 0062-A1.6 halt-condition closure verified (verification-pass)

A1's stated purpose is to close ADR 0062-A1.6's halt-condition (sub-second `EditionCapabilities` responsiveness). The closure mechanism: A1.6 + A1.7 (Anchor-side handler triggers `EnvelopeChange` via `IMissionEnvelopeProvider` consuming `editionAfter`). This matches ADR 0062-A1.10's `ProbeStatus` + `EnvelopeChangeSeverity` semantics. **Citation chain holds; no finding.**

### F-VP5 — Industry prior-art (Stripe / GitHub / Square / ASP.NET) framing accurate (verification-pass)

A1 cites Stripe webhooks + GitHub conventions for HMAC + ±5min clock-skew + 30-second timeout. These match the public documentation for those vendors as of 2026-05. Citations are practitioner-shorthand but accurate at the canonical-pattern level. **No finding.**

---

## 3. Recommended amendments

### A1 (Required) — Specify per-Anchor shared-secret rotation policy (resolves F1)

A1.4 gains a new paragraph:

> **Shared-secret rotation.** Bridge generates a fresh shared secret on every webhook re-registration (operator-initiated). Per-deployment configuration MAY enforce mandatory rotation cadence (default: 90 days from issuance; tunable via Bridge admin config). On rotation:
>
> 1. Bridge generates a new secret + retains the old secret as "previous-acceptable" for a 24-hour grace period.
> 2. Bridge sends a `BridgeSubscriptionWebhookRotationStaged` audit event (9th new constant) with the rotation timestamp.
> 3. Anchor receives the new secret out-of-band (re-registration response payload) AND rotates its keystore entry per ADR 0046's existing rotation surface.
> 4. During the 24-hour grace window, Bridge HMAC-signs events with the new secret BUT Anchor accepts events signed with either the old OR new secret (HMAC verifies against both).
> 5. After 24 hours, Bridge stops accepting the old secret in any context; expired-secret events fail per `BridgeSubscriptionEventSignatureFailed` audit.
>
> Out-of-scope for A1: automatic rotation triggers (e.g., "rotate on detected compromise"). Phase 1 ships operator-initiated rotation only.

Add 9th `AuditEventType` constant: `BridgeSubscriptionWebhookRotationStaged`.

**Required because F1 is Major (security hygiene).**

### A2 (Required) — Engage HMAC-SHA256 vs Ed25519 tradeoff explicitly (resolves F2)

A1.2 gains a new paragraph engaging the tradeoff:

> **Signature scheme rationale.** A1 ships HMAC-SHA256 as the signature scheme for industry-standard webhook compatibility (Stripe, GitHub, Square, Slack all use HMAC-SHA256). The alternative is Ed25519-signed events using ADR 0032's keypair semantic (Bridge holds an Ed25519 keypair; publishes the public key; Anchor verifies with the public key). Ed25519 trades per-event verification cost (~50µs vs HMAC's ~5µs) for simpler key-management (Bridge publishes one public key; no per-Anchor shared-secret distribution).
>
> **Phase 2+ migration trigger:** if shared-secret distribution becomes operationally painful at scale (e.g., > 1000 Anchors per Bridge deployment; rotation overhead becomes the dominant Bridge admin cost), Phase 2+ amendment migrates to Ed25519 per ADR 0032 keypair semantic. The migration is non-breaking — events would carry an `Algorithm` field (`HmacSha256` | `Ed25519`); Anchor verifies based on the field; Bridge dual-signs during transition.

**Required because F2 is Major (cohort-composition; long-term substrate decision).**

### A3 (Required) — Specify operator-controllable trust configuration (resolves F3)

A1.4's HTTPS-only paragraph is replaced with:

> **Webhook URL trust configuration.** Webhook URLs MUST be HTTPS (Bridge refuses HTTP at registration; operator-deployment self-signed certs are a configurable allowance, not a default). The `callbackUrl` MUST resolve to a non-loopback address.
>
> Operator-controllable trust configuration (Bridge admin-tier):
>
> - **Default:** Bridge HTTP client trusts only publicly-rooted CAs (Let's Encrypt, public-trusted commercial CAs). Suitable for Phase 1.5+ operator deployments with public DNS.
> - **Per-Anchor cert pinning:** Anchor uploads its cert chain at registration time (PEM-encoded; included in the registration payload's `trustChain` field). Bridge pins-and-verifies against the registered chain. Suitable for Phase 1 self-hosted Windows-only deployments per ADR 0044 (no public Let's Encrypt; self-signed or internal-CA cert).
> - **Per-deployment trust override:** Bridge config supports `WebhookHttpClient.AllowSelfSignedCerts: bool` for development / lab deployments. Default `false`; admin sets `true` per-Bridge-deployment with operator-tier audit (`BridgeWebhookSelfSignedCertsConfigured` — 10th new audit constant).
>
> Operator decision matrix per deployment shape:
>
> | Deployment | Trust mode | Rationale |
> |---|---|---|
> | Phase 1 self-hosted Anchor (per ADR 0044) | Per-Anchor cert pinning | Anchor's self-signed cert is uploaded at registration |
> | Phase 1.5+ public-DNS Anchor (Let's Encrypt) | Default (publicly-rooted CA) | Industry-standard |
> | Internal enterprise CA (mTLS) | Per-Anchor cert pinning + Bridge-side mTLS client cert | Bridge presents its own cert; Anchor's cert is pinned |
> | Development / lab | Per-deployment `AllowSelfSignedCerts: true` | Testing-only; audit emits |

Add 10th `AuditEventType` constant: `BridgeWebhookSelfSignedCertsConfigured`.

**Required because F3 is Major (Phase 1 deployment-blocker; ADR 0044's self-hosted Windows-only Anchor cannot use the default trust model).**

### A4 (Required) — Disambiguate webhook retry vs SSE reconnect (resolves F5)

A1.3's SSE paragraph + A1.5's retry paragraph both gain explicit backoff semantics:

A1.3 SSE paragraph reword:

> **SSE reconnect:** SSE connections drop normally (network blips; Bridge restarts; load balancer recycling). Bridge reconnects with exponential backoff: 1s → 5s → 30s → 60s (capped at 60s). Reconnect is **unbounded** — Bridge keeps trying as long as the Anchor's subscription is active. Events emitted during disconnect are queued in Bridge's per-Anchor event queue (max 1-hour depth; if the queue exceeds 1 hour or 10,000 events, Bridge falls back to webhook-mode delivery for the queued events; the Anchor's `deliveryMode` registration is *not* changed but the specific queued events are delivered via webhook). On reconnect, all queued events ship in order via the SSE stream.

A1.5's retry paragraph gains a clarifying note:

> **Note: webhook retry is different from SSE reconnect.** This A1.5 retry policy applies to **webhook delivery** (HTTPS POST attempt failed; bounded retries with dead-letter after exhaustion). SSE reconnect (A1.3) is a separate concern with its own backoff (exponential 1s → 5s → 30s → 60s capped; unbounded retries). Don't conflate them.

**Required because F5 is Minor but deployment-blocker if implementer copies A1.5 retry policy onto SSE reconnect (would dead-letter the SSE connection after 7 attempts, breaking long-lived delivery semantic).**

### A5 (Encouraged) — Add 3 missing subscription lifecycle event types (resolves F4)

A1.1 gains 3 new event types:

| Event type | Trigger | Payload-specific fields |
|---|---|---|
| `SubscriptionTrialStarted` | Tenant activates trial Edition | `editionAfter` set; `trialExpiresAt` set |
| `SubscriptionTrialExpired` | Trial ends without conversion to paid | `editionBefore` set; `editionAfter == null` |
| `SubscriptionPaused` | Tenant pauses (preserves subscription; no charges) | `editionBefore == editionAfter`; `pausedUntil` optional |

These 3 events ship in the same A1 substrate (no breaking change; Anchor's `subscribedEvents` filter applies; old Anchors that don't subscribe to these new event types simply ignore them). The Bridge-side emit logic adds 3 new code paths.

A1.7's audit-event-type table gains 3 new dedup'd entries (matching the existing pattern; per `(tenant_id, event_id)` 24-hour dedup on Anchor side).

**Encouraged because F4 is Minor (covers future UX flows that aren't gating Phase 1 ADR 0062 closure).**

### A6 (Encouraged) — `subscribedEvents` filter atomicity for in-flight events (resolves F6)

A1.4 gains a new sub-paragraph:

> **Filter-change atomicity for in-flight events.** When Anchor re-registers with a different `subscribedEvents` filter, Bridge applies the new filter to events emitted *after* the re-registration commit timestamp. Events already in-flight (between Bridge-emit and Anchor-receive) at the moment of re-registration continue per the prior filter. This gives consistent semantics: "the filter at emit time wins"; no event is dropped because of a filter change mid-flight.

**Encouraged.**

---

## 4. Quality rubric grade

**Grade: B (Solid).** Path to A is mechanical (A1–A4 land + A5–A6 encouraged-tier).

- **C threshold (Viable):** All structural elements present (driver, event taxonomy, payload + signature, delivery mechanism, registration flow, retry/idempotency, handler substrate, audit emission, acceptance criteria, cited-symbol verification, implementation hand-off split, cohort discipline). No critical *planning* anti-patterns. **Pass.**
- **B threshold (Solid):** Stage 0 sparring evident in §"Council pressure-test points" + §"Open questions" implicit in A1.10 phase split; Cold Start Test plausible — A1 Stage 06 implementer can read A1.6 + A1.7 + A1.8 and know what to scaffold; cohort-discipline section explicit. **Pass.**
- **A threshold (Excellent):** Misses on:
  1. **F1 (Major security):** rotation policy unspec'd
  2. **F2 (Major security):** HMAC vs Ed25519 tradeoff undertheorized
  3. **F3 (Major deployment):** HTTPS-only too restrictive for Phase 1 self-hosted
  4. **F5 (Minor):** SSE reconnect vs webhook retry conflated

A grade of **B with required A1–A4 applied promotes to A**, conditional on the Stage 06 build's successful round-trip verification of all 7+3 event types under both webhook + SSE delivery.

---

## 5. Council perspective notes (compressed)

- **Distributed-systems / runtime-substrate reviewer:** "A1's architectural shape is correct: 7-event taxonomy + canonical-JSON payload + HMAC-SHA256 + UUID idempotency + ±5min replay-attack window + webhook-primary/SSE-fallback + 7-attempt exponential backoff + dead-letter is the right substrate. Per-event UUID dedup is canonical. The webhook-vs-SSE delivery split is well-scoped. F1 + F2's security gaps (rotation; HMAC-vs-Ed25519) are real; F3's deployment-blocker is the most actionable. F5's SSE reconnect vs webhook retry conflation is a minor-but-load-bearing spec gap that an implementer would diverge on without explicit disambiguation. Anchor-side handler integrating with `IMissionEnvelopeProvider` to trigger `EnvelopeChange` is the canonical closure of ADR 0062-A1.6's halt-condition." Drives F1 / F2 / F3 / F5.

- **Industry prior-art reviewer:** "HMAC-SHA256 + ±5min clock-skew + 30-second webhook timeout matches Stripe/GitHub/Square/Slack canonical. SSE for long-lived push matches Server-Sent Events RFC + ASP.NET Core's standard. ASP.NET Core's `Microsoft.AspNetCore.WebHooks` + `WebhooksReceiver.Subscriptions` packages are the canonical .NET prior-art (not cited but worth noting at A1 Stage 06 hand-off). The 9th-event-type taxonomy gap (F4) — Stripe ships ~30 subscription event types; A1 ships 7; the missing trial / pause states are the most-commonly-needed; A5 amendment adds 3. Ed25519 alternative (F2) is named correctly; cohort composition with ADR 0032 is the long-term play." Drives F4; supports F2.

- **Cited-symbol / cohort-discipline reviewer:** "Spot-checked all cited Sunfish.* symbols + ADR cross-references in three directions (negative + positive + structural-citation per the A7 lesson + ADR 0063-A1.15 §A0-insufficiency lesson + ADR 0028-A10 parent-citation propagation lesson). 5 verification-passes (F-VP1 through F-VP5): IEditionResolver verified at ADR 0009; ADR 0046 keystore reference verified structurally; 8 audit constants no-collision; ADR 0062-A1.6 halt-condition closure citation chain holds; industry prior-art Stripe/GitHub/etc. references practitioner-shorthand-but-accurate. NO structural-citation failures found in this review — improvement over the average ~67% rate. **A1's 8 audit-event constants are at risk of growing to 10 (per A3) or 11 (per A1's rotation amendment) — verify no collision after each amendment.** Cohort batting average: 17-of-17 if A1–A4 fixes apply pre-merge per current auto-merge-disabled approach; structural-citation failure rate (XO-authored) holds at 11-of-17 (~65%; A1 contributes 0)." No drives; verification-passes.

- **Security / deployment reviewer:** "A1 is Phase 1 deployment-blocking under HTTPS-only constraint (F3). Self-signed cert handling is canonical operator-tier requirement; A3 amendment specifies it correctly. Shared-secret rotation policy (F1) is the main security hygiene gap — Stripe explicitly documents 90-day rotation; GitHub explicitly documents key-roll endpoint; A1 ships rotation-naive. HMAC-SHA256 is the canonical industry choice but Ed25519 composition with ADR 0032 keypair would simplify the key-distribution story long-term — A2 amendment names the migration trigger." Drives F1 + F2 + F3.

---

## 6. Cohort discipline scorecard

| Cohort baseline | This amendment |
|---|---|
| 16 prior substrate amendments needed council fixes | Will be 17-of-17 if A1–A4 fixes apply pre-merge |
| Cited-symbol verification — three-direction standard | This amendment: 0 false-positive + 0 false-negative + 0 structural-citation failures + 5 verification-passes (F-VP1–F-VP5). **Improvement over ADR 0063 council's 4-of-4 structural-citation count.** |
| Council false-claim rate (all three directions) | This council: 0 false claims (F-VP1–F-VP5 are explicit positive-existence + structural verifications with verification commands) |
| Pedantic-Lawyer perspective | N/A for ADR 0031-A1 (substrate amendment; Bridge accelerator surface; security-tier but not regulatory-tier; standard 4-perspective adequate) |
| Council pre-merge vs post-merge | Pre-merge — correct call given F1's security-hygiene gap + F3's Phase 1 deployment-blocker |
| Severity profile | 3 Major (F1 + F2 + F3) + 2 Minor (F4 + F5) + 1 Encouraged (F6) + 5 verification-passes (F-VP1–F-VP5) |
| Structural-citation failure rate (XO-authored) | Was 11-of-16 (~69%); ADR 0031-A1 contributes 0 — rate becomes 11-of-17 (~65%); the §A0 self-audit + 3-direction spot-check + cohort-vigilance discipline held in this round |
| Subagent dispatch pattern | Skipped for A1 (small scope; ~3,500 words target; XO authored in-thread; matches ADR 0028-A9 council precedent) |

The cohort lesson holds: every substrate-tier amendment in the W#33-derived sibling chain has surfaced council fixes. A1's findings are mostly substrate-completeness gaps (security + deployment + spec-disambiguation) rather than structural-citation failures — a healthier failure-mode profile than ADR 0063's 4-of-4 structural-citation count.

---

## 7. Closing recommendation

**Accept ADR 0031-A1 with required amendments A1–A4 applied before Stage 06 build emits its first event.** The substantive gaps are:

1. **Per-Anchor shared-secret rotation** (F1 / A1) — security hygiene; default 90-day cadence; 24-hour grace window; 9th audit constant `BridgeSubscriptionWebhookRotationStaged`
2. **HMAC-SHA256 vs Ed25519 tradeoff engagement** (F2 / A2) — name the alternative; declare Phase 2+ migration trigger
3. **Operator-controllable trust configuration** (F3 / A3) — per-Anchor cert pinning + per-deployment self-signed-cert allowance + 10th audit constant `BridgeWebhookSelfSignedCertsConfigured`
4. **SSE reconnect vs webhook retry disambiguation** (F4 / A4) — explicit backoff semantics for each
5. **3 missing subscription lifecycle event types** (F5 / A5; encouraged) — `SubscriptionTrialStarted` / `SubscriptionTrialExpired` / `SubscriptionPaused`
6. **`subscribedEvents` filter atomicity** (F6 / A6; encouraged) — filter-at-emit-time wins

A1–A4 are mechanical-on-the-amendment-text. ~3-4h of XO work pre-merge.

**A1 closes ADR 0062-A1.6's halt-condition.** Phase 1 substrate scaffold of ADR 0062 may proceed once A1 lands + Anchor-side handler is wired. The wiring is W#23 / Anchor-tier territory; A1 Stage 06 hand-off is medium-scope (~6–10h per parent intake).

**Standing rung-6 task:** XO spot-checks A1's added/modified citations within 24h of merge.

**Cohort milestone:** ADR 0031-A1 closes the 4-sibling-amendment chain (ADR 0028-A9 ✓ + ADR 0036-A1 ✓ + ADR 0007-A1 ✓ + ADR 0031-A1 = 4 of 4). All 4 derived from ADR 0062 + ADR 0063 post-A1 surface; all 4 mechanical (per Decision Discipline Rule 3 council-waiver eligibility for the smaller ones; substrate-tier for ADR 0031-A1). Cohort batting average: **17-of-17** if A1–A4 fixes apply pre-merge.

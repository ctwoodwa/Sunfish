# Hand-off — Bridge → Anchor subscription-event-emitter Phase 1 (ADR 0031-A1+A1.12)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-01
**Status:** `ready-to-build`
**Spec source:** [ADR 0031 amendment A1 + A1.12](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) (post-A1.12 council-fixed surface; landed via PR #440; council in PR #441)
**Approval:** ADR 0031-A1+A1.12 Accepted on origin/main; council batting average 17-of-17; A1.12 absorbed all 4 Required council recommendations
**Estimated cost:** ~6–10h sunfish-PM (Bridge event-emitter substrate + ~12 type signatures + webhook + SSE delivery + retry/dedup + 10 audit constants + ~30–40 tests + DI + apps/docs page)
**Pipeline:** `sunfish-api-change`
**Audit before build:** `ls /Users/christopherwood/Projects/Sunfish/packages/ | grep bridge-subscription` to confirm no collision (audit not yet run; COB confirms before Phase 1 commit)

---

## Context

Phase 1 lands the Bridge subscription-event-emitter substrate per ADR 0031-A1+A1.12. **Closes ADR 0062-A1.6's halt-condition** — Phase 1 substrate scaffold of ADR 0062 (Mission Space Negotiation Protocol) may proceed once W#36 lands + Anchor-side handler is wired (sub-second `EditionCapabilities` responsiveness vs the 30-second cache TTL ceiling).

**Substrate scope:** Bridge-side event-emission queue + HMAC-SHA256 signing + canonical-JSON encoding + webhook-primary delivery + SSE-fallback delivery + retry/dedup + 10 new `AuditEventType` constants + Anchor-side `IBridgeSubscriptionEventHandler` substrate + DI extension + apps/docs page. Substrate-only; consumer wiring (ADR 0062 Phase 1 substrate scaffold's `EditionCapabilities` integration) is a separate W#37 hand-off when COB capacity opens.

This hand-off scope mirrors the W#34 + W#35 substrate-only patterns COB has executed successfully (W#34 Foundation.Versioning shipped 5/5 phases / 59 tests / 0 halt-conditions tripped; W#35 Foundation.Migration in flight P1+P2 already landed).

---

## Files to create

### Package scaffold

```
packages/bridge-subscription/
├── Sunfish.Bridge.Subscription.csproj
├── README.md
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs        (AddInMemoryBridgeSubscription; mirrors W#34 P5 + W#35 P5 shape)
├── Models/
│   ├── BridgeSubscriptionEvent.cs            (record per A1.6)
│   ├── BridgeSubscriptionEventType.cs        (enum; 7 values per A1.1)
│   ├── DeliveryMode.cs                       (enum: Webhook / Sse)
│   ├── SignatureAlgorithm.cs                 (enum: HmacSha256; Ed25519 reserved per A1.12.2)
│   ├── WebhookRegistration.cs                (record; per A1.4 registration shape)
│   └── SubscribedEventFilter.cs              (record; per A1.4 opt-in filter)
├── Services/
│   ├── IBridgeSubscriptionEventHandler.cs    (per A1.6 Anchor-side contract)
│   ├── InMemoryBridgeSubscriptionEventHandler.cs  (reference impl)
│   ├── IBridgeSubscriptionEventEmitter.cs    (Bridge-side emit contract)
│   ├── InMemoryBridgeSubscriptionEventEmitter.cs  (reference impl with in-process queue)
│   ├── IWebhookDeliveryService.cs            (HTTPS POST delivery)
│   ├── DefaultWebhookDeliveryService.cs      (with retry policy per A1.5)
│   ├── ISseDeliveryService.cs                (long-lived connection delivery)
│   └── DefaultSseDeliveryService.cs          (with reconnect policy per A1.12.4)
├── Crypto/
│   ├── IEventSigner.cs                       (HMAC-SHA256 + Ed25519 future-compat per A1.12.2)
│   ├── HmacSha256EventSigner.cs              (default; per A1.2)
│   └── PerAnchorSecretStore.cs               (per A1.4 + A1.12.1 rotation)
├── Audit/
│   └── BridgeSubscriptionAuditPayloads.cs    (factory; 10 event types per A1.7 + A1.12)
├── Trust/
│   ├── ITrustChainResolver.cs                (per A1.12.3 cert-pinning)
│   └── DefaultTrustChainResolver.cs          (default + per-Anchor pin + self-signed override)
└── tests/
    └── Sunfish.Bridge.Subscription.Tests.csproj
        ├── BridgeSubscriptionEventTests.cs   (7-event-type round-trip per A1.1 + A1.2)
        ├── HmacSignatureTests.cs              (HMAC verify success + failure; ±5min clock-skew)
        ├── IdempotencyTests.cs                (per-tenant LRU dedup; 24-hour retention)
        ├── ReplayAttackTests.cs               (effectiveAt > 5min stale → BridgeSubscriptionEventStale audit)
        ├── WebhookDeliveryTests.cs            (HTTPS POST + 30s timeout; retry policy 1s/5s/30s/5min/30min/2h/12h × 7 attempts; dead-letter)
        ├── SseDeliveryTests.cs                (long-lived connection; reconnect 1s/5s/30s/60s capped; queue during disconnect; 1-hour/10000-event fallback to webhook)
        ├── WebhookRegistrationTests.cs        (POST /api/v1/tenant/webhook; HTTPS-only; per-Anchor shared secret)
        ├── SecretRotationTests.cs             (90-day default; 24-hour grace window; 9th audit event)
        ├── TrustConfigurationTests.cs         (per-Anchor cert pinning + self-signed allowance + 10th audit event)
        ├── AuditEmissionTests.cs              (10 AuditEventType constants emit on right triggers + dedup)
        └── DiExtensionTests.cs                (audit-disabled / audit-enabled overloads; both-or-neither at registration boundary)
```

### Type definitions (post-A1.12 surface; implement exactly per ADR 0031-A1)

Use the `IBridgeSubscriptionEventHandler` + `BridgeSubscriptionEvent` exactly as A1.6 spec'd. Use the `algorithm: SignatureAlgorithm` field per A1.12.2 (defaults to `HmacSha256`; `Ed25519` reserved for Phase 2+ migration).

### Audit constants (10 per A1.7 + A1.12)

`AuditEventType` MUST gain 10 new constants in `packages/kernel-audit/AuditEventType.cs`:

```csharp
// Bridge-side (5 — including the 9th + 10th constants from A1.12.1 + A1.12.3)
public static readonly AuditEventType BridgeSubscriptionEventEmitted          = new("BridgeSubscriptionEventEmitted");
public static readonly AuditEventType BridgeSubscriptionEventDelivered        = new("BridgeSubscriptionEventDelivered");
public static readonly AuditEventType BridgeSubscriptionEventDeliveryFailed   = new("BridgeSubscriptionEventDeliveryFailed");
public static readonly AuditEventType BridgeSubscriptionEventDeliveryFailedTerminal = new("BridgeSubscriptionEventDeliveryFailedTerminal");
public static readonly AuditEventType BridgeSubscriptionWebhookRegistered     = new("BridgeSubscriptionWebhookRegistered");
public static readonly AuditEventType BridgeSubscriptionWebhookRotationStaged = new("BridgeSubscriptionWebhookRotationStaged");  // A1.12.1
public static readonly AuditEventType BridgeWebhookSelfSignedCertsConfigured  = new("BridgeWebhookSelfSignedCertsConfigured");   // A1.12.3

// Anchor-side (3)
public static readonly AuditEventType BridgeSubscriptionEventReceived         = new("BridgeSubscriptionEventReceived");
public static readonly AuditEventType BridgeSubscriptionEventSignatureFailed  = new("BridgeSubscriptionEventSignatureFailed");
public static readonly AuditEventType BridgeSubscriptionEventStale            = new("BridgeSubscriptionEventStale");
```

Total: 10 constants. Per A1.12.5: collision check completed in council review (no collisions in `packages/kernel-audit/AuditEventType.cs`).

`BridgeSubscriptionAuditPayloads` factory mirrors `LeaseAuditPayloadFactory` shape (alphabetized keys; canonical-JSON-serialized; per ADR 0049 emission contract).

---

## Phase breakdown (~5 PRs, ~6–10h total)

### Phase 1 — Substrate scaffold + core types (~1–2h, 1 PR)

- Package created at `packages/bridge-subscription/` with foundation-tier-adjacent csproj
- All Models per the spec block above
- 10 new `AuditEventType` constants in `packages/kernel-audit/AuditEventType.cs`
- `BridgeSubscriptionEvent` round-trip via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` test (camelCase shape per ADR 0028-A7.8)
- `JsonStringEnumConverter` for all enum types
- README.md per the standard package-README pattern
- ~6–10 unit tests on Models alone

**Acceptance:**
- [ ] All 7 BridgeSubscriptionEventType enum values per A1.1
- [ ] BridgeSubscriptionEvent record with all A1.2 fields (tenantId / eventType / editionBefore / editionAfter / effectiveAt / eventId / deliveryAttempt / signature)
- [ ] CanonicalJson round-trip stable; camelCase canonical shape
- [ ] PR description names which post-A1.12 sub-amendments are wired

### Phase 2 — HMAC signing + canonical-JSON encoding + idempotency (~1–2h, 1 PR)

- `IEventSigner` + `HmacSha256EventSigner` per A1.2
- `BridgeSubscriptionAuditPayloads` factory (alphabetized keys per ADR 0049)
- Per-tenant LRU dedup cache for Anchor-side idempotency (24-hour retention per A1.5)
- ±5min clock-skew tolerance for replay-attack hardening per A1.2

**Acceptance:**
- [ ] HMAC-SHA256 signature round-trip (sign + verify)
- [ ] Replay-attack test: effectiveAt > 5min in past → BridgeSubscriptionEventStale audit
- [ ] Idempotency test: same eventId delivered twice → second is no-op
- [ ] Audit dedup tests for 4 dedup'd event types per A1.7 + A1.12.5

### Phase 3 — Webhook delivery + retry policy + dead-letter (~2–3h, 1 PR)

- `IWebhookDeliveryService` + `DefaultWebhookDeliveryService` per A1.3 + A1.5
- HTTPS POST with 30-second timeout
- Retry policy: 1s → 5s → 30s → 5min → 30min → 2h → 12h × 7 attempts
- Dead-letter queue + `BridgeSubscriptionEventDeliveryFailedTerminal` audit emission
- Trust configuration per A1.12.3:
  - Default: publicly-rooted CA verification
  - Per-Anchor cert pinning (PEM upload at registration; pin-and-verify)
  - Per-deployment self-signed cert allowance (with `BridgeWebhookSelfSignedCertsConfigured` audit)

**Acceptance:**
- [ ] HTTPS POST 30s timeout per A1.3
- [ ] Retry timing test: 7 attempts; verify backoff intervals
- [ ] Dead-letter test: 8th attempt → `BridgeSubscriptionEventDeliveryFailedTerminal` emitted; event in DLQ
- [ ] Trust configuration tests: publicly-rooted (default) + per-Anchor pin + self-signed override

### Phase 4 — SSE delivery + webhook URL registration endpoint (~1–2h, 1 PR)

- `ISseDeliveryService` + `DefaultSseDeliveryService` per A1.3 + A1.12.4
- SSE reconnect with exponential backoff: 1s → 5s → 30s → 60s capped; **unbounded** per A1.12.4 (do NOT inherit webhook retry's 7-attempt bound)
- Per-tenant event queue during disconnect (max 1-hour depth or 10,000 events; falls back to webhook delivery if exceeded)
- `POST /api/v1/tenant/webhook` registration endpoint per A1.4 (HTTPS-only; per-Anchor shared secret)
- Secret rotation per A1.12.1: 90-day default cadence; 24-hour grace window with old+new secret both accepted; `BridgeSubscriptionWebhookRotationStaged` audit

**Acceptance:**
- [ ] SSE long-lived connection establishment
- [ ] SSE reconnect timing test: 1s/5s/30s/60s capped; unbounded (do NOT dead-letter the connection)
- [ ] SSE queue-during-disconnect: 100 events queued during disconnect → all ship in order on reconnect
- [ ] SSE queue-overflow fallback: 10,001-event queue → events 1-10000 delivered via webhook fallback; events 10001+ via SSE on reconnect
- [ ] Webhook registration endpoint tests: HTTPS-only; non-loopback; PEM cert pinning
- [ ] Secret rotation tests: 90-day cadence; 24-hour grace window; both old + new secrets accepted during grace

### Phase 5 — Anchor-side handler + IMissionEnvelopeProvider integration + DI + apps/docs + ledger flip (~1–2h, 1 PR)

- `IBridgeSubscriptionEventHandler` Anchor-side default impl per A1.6:
  1. Verify HMAC signature
  2. Check eventId in per-tenant LRU cache (idempotency)
  3. Update local `EditionCapabilities` cache via `IEditionResolver`
  4. Emit `BridgeSubscriptionEventReceived` audit
  5. Trigger `EnvelopeChange` event via ADR 0062's `IMissionEnvelopeProvider` (consumes `editionAfter`)
  6. Return 200
- `AddInMemoryBridgeSubscription()` DI extension (audit-disabled + audit-enabled overloads; both-or-neither at registration; mirrors W#34 P5 + W#35 P5)
- `apps/docs/bridge/subscription-events/overview.md` walkthrough page (cite ADR 0031-A1 + post-A1.12 surface explicitly)
- Active-workstreams.md row 36 flipped from `building` → `built` with PR list

**Acceptance:**
- [ ] AddInMemoryBridgeSubscription() registers all 6 substrate interfaces + 10 audit-event constants
- [ ] Anchor-side handler integration test: receives event → triggers EnvelopeChange via IMissionEnvelopeProvider; EditionCapabilities cache updated
- [ ] apps/docs page renders cleanly + cites ADR 0031-A1 + post-A1.12 cohort lesson
- [ ] All N tests passing (~30–40 across phases)
- [ ] **ADR 0062-A1.6 halt-condition closure verified:** ADR 0062's Phase 1 substrate scaffold can now proceed; sub-second EditionCapabilities responsiveness path is end-to-end functional

---

## Halt-conditions (cob-question if any of these surface)

1. **HMAC-SHA256 vs Ed25519 confusion.** Phase 2 ships HMAC-SHA256 only; the `algorithm` field defaults to `HmacSha256`. **Do NOT implement Ed25519** in Phase 1 — it's a Phase 2+ migration trigger per A1.12.2. If `Ed25519` shows up as a live code path, file `cob-question-*` beacon.

2. **SSE reconnect inheriting webhook retry policy.** Per A1.12.4, SSE reconnect (1s/5s/30s/60s capped; unbounded) is DIFFERENT from webhook retry (1s/5s/30s/5min/30min/2h/12h × 7 attempts; dead-letter). If the implementation conflates them — specifically if SSE reconnect dead-letters after 7 attempts — file `cob-question-*` beacon.

3. **Per-Anchor shared secret persistence.** A1.12.1 specifies Bridge holds the secret server-side; Anchor stores it in its keystore per ADR 0046 substrate. Phase 4 needs to hook ADR 0046's `IFieldEncryptor` for at-rest encryption of the Anchor-side stored secret. If this requires reaching into ADR 0046 substrate boundaries unexpectedly, file `cob-question-*` beacon.

4. **Trust configuration default.** Phase 3 defaults to publicly-rooted CA verification. **Do NOT default to `AllowSelfSignedCerts: true`** even for development convenience — A1.12.3 specifies the default is `false`; admin opt-in only with audit emission. If a test scenario tempts an "always-allow-self-signed" shortcut, file `cob-question-*` beacon.

5. **CP-record quorum integration.** A1's Anchor-side handler triggers `EnvelopeChange` via `IMissionEnvelopeProvider`; this composes with ADR 0062-A1.10's ProbeStatus + EnvelopeChangeSeverity semantics. If the wiring requires reaching into W#23 / W#35 / future-form-factor surfaces unexpectedly, file `cob-question-*` beacon.

6. **`AddInMemoryBridgeSubscription` two-overload pattern.** Per W#34 + W#35 cohort lesson: audit-disabled + audit-enabled DI overloads; both-or-neither at registration boundary. If the pattern feels awkward for this substrate, file `cob-question-*` beacon (likely the substrate is right; document the lesson if you find an exception).

7. **30-second webhook timeout in operator-deployment Anchor.** Phase 3's HTTPS POST 30-second timeout may produce false-failures if the Anchor is on a slow link (residential rural broadband). The retry policy absorbs this (1s, 5s, 30s, 5min...) but the operator UX may want a timeout signal earlier. If real-world testing exposes this gap, file `cob-question-*` beacon — the timeout may need to be configurable per-deployment.

---

## Cited-symbol verification (per cohort discipline)

**Existing on origin/main (verified before hand-off authored):**

- ADR 0031-A1 + A1.12 (PR #440 merged) — substrate spec source
- ADR 0062-A1.6 (PR #406 merged post-A1) — halt-condition source ✓
- ADR 0046 keystore + IFieldEncryptor (PR #371 merged W#32 P2+P3) — for shared-secret at-rest storage ✓
- ADR 0009 IEditionResolver (Accepted) — for EditionCapabilities cache integration ✓
- ADR 0049 audit substrate ✓
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` ✓
- `Sunfish.Foundation.MissionSpace.IMissionEnvelopeProvider` (post-ADR 0062-A1.2 IFeatureGate rename; W#36 consumes the post-A1.2 surface)

**Introduced by this hand-off** (ship in Phase 1):

- New package: `Sunfish.Bridge.Subscription`
- New types: `BridgeSubscriptionEvent`, `BridgeSubscriptionEventType` (enum 7 values), `DeliveryMode`, `SignatureAlgorithm` (enum HmacSha256 + Ed25519-reserved), `WebhookRegistration`, `SubscribedEventFilter`
- New service contracts: `IBridgeSubscriptionEventHandler` + `IBridgeSubscriptionEventEmitter` + `IWebhookDeliveryService` + `ISseDeliveryService` + `IEventSigner` + `ITrustChainResolver`
- 10 new `AuditEventType` constants per A1.7 + A1.12.5
- `BridgeSubscriptionAuditPayloads` factory class

**Cohort lesson reminder (per ADR 0028-A10 + ADR 0063-A1.15):** §A0 self-audit pattern is necessary but NOT sufficient. COB should structurally verify each Sunfish.* symbol exists (read actual cited file's schema; don't grep alone) before declaring AP-21 clean.

---

## Cohort discipline

This hand-off is **not** a substrate ADR amendment; it's a Stage 06 hand-off implementing post-A1.12-fixed surface. The cohort discipline applies to ADR amendments, not to this hand-off.

- Pre-merge council on this hand-off is NOT required.
- COB's standard pre-build checklist applies.
- **W#34 + W#35 cohort lessons incorporated:** ConcurrentDictionary dedup pattern; two-overload constructor (audit-disabled / audit-enabled both-or-neither); JsonStringEnumConverter for all enum types; AddInMemoryX() DI extension naming; apps/docs/{tier}/X/overview.md page convention. Mirror these exactly.

---

## Beacon protocol

If COB hits a halt-condition (per the 7 named above) or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w36-{slug}.md` in `icm/_state/research-inbox/`
- Halt the workstream + add a note in active-workstreams.md row 36 ("paused on cob-question-XXX")
- ScheduleWakeup 1800s

If COB completes Phase 5 + drops to fallback:

- Drop `cob-idle-2026-05-XXTHH-MMZ-{slug}.md` to research-inbox
- Continue with rung-1 dependabot + rung-2 build-hygiene per CLAUDE.md fallback work order

---

## Cross-references

- Spec source: ADR 0031-A1+A1.12 (PR #440 merged 2026-05-01)
- Council that drove A1.12: PR #441 (merged); council file at `icm/07_review/output/adr-audits/0031-A1-council-review-2026-05-01.md`
- Sibling workstreams in flight / queued: W#23 iOS Field-Capture (`ready-to-build`); W#35 Foundation.Migration (`building` per COB's W#35 P1+P2 PRs #439/#442); W#34 Foundation.Versioning (`built` 5/5 phases / 59 tests)
- Halt-condition source: ADR 0062-A1.6 (PR #406 merged post-A1)
- Sibling-amendment chain: ADR 0028-A9 ✓ + ADR 0036-A1 ✓ + ADR 0007-A1 ✓ + ADR 0031-A1 ✓ (4 of 4 W#33-derived sibling intakes have authored amendments)

# Workstream #20 — Bidirectional Messaging Substrate — Stage 06 hand-off

**Workstream:** #20 (Bidirectional Messaging Substrate — cluster #4 spine)
**Spec:** [ADR 0052](../../docs/adrs/0052-bidirectional-messaging-substrate.md) (Accepted 2026-04-29; amendments A1–A5 + Minor landed)
**Pipeline variant:** `sunfish-feature-change` (new substrate; no breaking changes)
**Estimated effort:** 15–19 hours focused sunfish-PM time
**Decomposition:** 9 phases shipping as ~6–7 separate PRs
**Prerequisites:** W#31 Foundation.Taxonomy ✓ (PR #263); ADR 0052 amendments ✓ (PR #259); ADR 0013 provider-neutrality enforcement gate ✓ (workstream #14, PR #196)

**Phase 2.1 scope** — first provider adapter is Postmark (email). SendGrid (email parity test target) and Twilio (SMS) are deferred to follow-up hand-offs after this Phase 2.1 lands.

---

## Scope summary

Build the Phase 2.1 messaging substrate end-to-end:

1. **Contracts** in `Sunfish.Foundation.Integrations.Messaging` — `IMessagingGateway`, `IThreadStore`, `IThreadTokenIssuer`, `IInboundMessageScorer`, `IUnroutedTriageQueue`, plus all envelope/event/config types
2. **`blocks-messaging`** — `Thread`, `Message`, `MessageVisibility` (3-value enum per Minor amendment), `Participant` entities + `InMemoryThreadStore` + `InMemoryMessagingGateway` + DI extension
3. **ThreadToken HMAC implementation** per ADR 0052 amendment A2 (HMAC-SHA256 + per-tenant key from `Foundation.Recovery` + 90-day TTL + revocation log)
4. **`providers-postmark`** — first email adapter (CC the ADR's Postmark Inbound webhook surface for ingress); SendGrid/Twilio deferred
5. **Inbound webhook handler** with 5-layer defense per ADR 0052 amendment A1 (provider sig verify + sender allow-list + rate limit + scoring hook + triage queue)
6. **Audit emission** — new `AuditEventType` constants per ADR 0049 pattern (mirrors W#31 + W#19 patterns)
7. **Cross-package wiring point**: W#19 Work Orders Phase 6 will consume `IThreadStore.SplitAsync(...)`; ensure the interface is shipped here

**NOT in scope (deferred to follow-up hand-offs):** SendGrid adapter, Twilio SMS adapter, parity-test framework against multiple providers, public-listings inquiry surface (consumes this substrate via `IInboundMessageScorer` plug-in but is its own workstream), Phase 2.3 deliverability isolation (per-tenant subdomain DKIM/SPF/DMARC).

---

## Phases (binary PASS/FAIL gates)

### Phase 1 — Contracts in `foundation-integrations/Messaging/` (~2–3h)

Add namespace `Sunfish.Foundation.Integrations.Messaging` to the existing `packages/foundation-integrations/` package.

Types to ship (full XML doc + nullability + `required`):

```csharp
// Identifiers
public readonly record struct ThreadId(Guid Value);
public readonly record struct MessageId(Guid Value);
public readonly record struct ParticipantId(Guid Value);
public readonly record struct ThreadToken(string Value); // 34-char base32 + "." + epoch per A2

// Enums
public enum MessageDirection { Inbound, Outbound }
public enum MessageChannel { Email, Sms, ProviderInternal }
public enum MessageVisibility { Public, PartyPair, OperatorOnly } // 3 values per Minor amendment
public enum SenderIsolationMode { SharedDomain, PerTenantStream, PerTenantSubdomain } // per A3
public enum SmsThreadTokenStrategy { OmitToken, InlineToken } // per A4

// Records
public sealed record Participant { /* ... */ }
public sealed record OutboundMessageRequest { /* ... */ }
public sealed record OutboundMessageResult { /* ... */ }
public sealed record InboundMessageEnvelope { /* ... */ }
public sealed record MessagingProviderConfig { /* ... */ }

// Interfaces
public interface IMessagingGateway        // SendAsync, GetStatusAsync
public interface IThreadStore             // CreateAsync, GetAsync, SplitAsync, AppendMessageAsync
public interface IThreadTokenIssuer       // Mint, Verify, RevokeAsync (per A2)
public interface IInboundMessageScorer    // ScoreAsync — public-listings + abuse-defense plug-in surface (per A1)
public interface IUnroutedTriageQueue     // EnqueueAsync, ListPendingAsync, ResolveAsync
```

Reference ADR 0052 §"Initial contract surface" + amendment A2 for the `ThreadToken` + `IThreadTokenIssuer` shapes.

**Gate:** `dotnet build` clean; new namespace compiles; XML doc on every public member; no implementations in this PR (contracts only).

**PR title:** `feat(foundation-integrations): Sunfish.Foundation.Integrations.Messaging contracts (ADR 0052 Phase 2.1)`

### Phase 2 — `blocks-messaging` package + InMemory implementations (~4–6h)

New package `packages/blocks-messaging/` (audit existing `packages/blocks-*` first to confirm no collision — `feedback_audit_existing_blocks_before_handoff`).

- `Models/Thread.cs` — entity (`ThreadId`, `TenantId`, `IReadOnlyList<Participant>`, `MessageVisibility`, `CreatedAt`, `UpdatedAt`, `IReadOnlyList<MessageId>`)
- `Models/Message.cs` — entity (`MessageId`, `ThreadId`, `MessageDirection`, `MessageChannel`, `Participant Sender`, `string SubjectLine`, `string Body`, `DateTimeOffset SentOrReceivedAt`, `IReadOnlyDictionary<string,string> ProviderMetadata`)
- `InMemoryThreadStore.cs` — implements `IThreadStore`; `SplitAsync` creates a new thread with a different participant set, copies forward any message references the caller specifies (per Minor amendment "private aside" use case)
- `InMemoryMessagingGateway.cs` — `IMessagingGateway` no-op test stub (returns `Sent` immediately; emits no audit unless wired)
- `MessagingEntityModule.cs` — implements `ISunfishEntityModule` per ADR 0015
- `DependencyInjection/ServiceCollectionExtensions.cs` — `AddInMemoryMessaging()`
- Tests covering: thread create/get/split, message append, participant-set membership enforcement (3-value visibility per Minor amendment), entity-module registration

**Gate:** package builds; entity-module registers; tests cover 3-value `MessageVisibility` (Public, PartyPair, OperatorOnly); `IThreadStore.SplitAsync` works (W#19 Phase 6 will consume this).

**PR title:** `feat(blocks-messaging): Phase 2.1 substrate scaffold (Thread + Message + InMemory + ADR 0015)`

### Phase 3 — `IThreadTokenIssuer` HMAC implementation (~1–2h)

Implement in `packages/foundation-integrations/Messaging/HmacThreadTokenIssuer.cs` per ADR 0052 amendment A2:

- HMAC-SHA256 over `{tenantId}:{threadId}:{notBeforeUtc:O}` with per-tenant key sourced from `Sunfish.Foundation.Recovery.ITenantKeyProvider`
- Token format: `base32(HMAC) + "." + base32(notBeforeUtcEpoch)` — 34 chars
- 90-day default TTL; overridable via `MessagingProviderConfig.ThreadTokenTtl`
- `Verify` checks: HMAC validity + TTL + revocation log
- Revocation log: `IRevokedTokenStore` interface; `InMemoryRevokedTokenStore` ships in this PR; persistent store deferred
- `RevokeAsync` emits `ThreadTokenRevoked` audit record (Phase 7 wires the `AuditEventType`)
- Tenant-key rotation cascade: on rotation, all extant tokens get `expires_at = now + grace_period` (default 7 days)

Tests: mint + verify round-trip; cross-tenant rejection; expired token rejection; revocation rejection; rotation grace window.

**Gate:** all 5 cryptographic-property tests pass; HMAC verification stays under 5ms p95 in unit benchmarks (per A5 success criterion).

**PR title:** `feat(foundation-integrations): HmacThreadTokenIssuer per ADR 0052 A2`

### Phase 4 — `providers-postmark` adapter (~3–4h)

New package `packages/providers-postmark/` per ADR 0013 provider-neutrality enforcement gate (workstream #14 — `SUNFISH_PROVNEUT_001` analyzer auto-attaches; vendor SDK use is allowed inside `providers-*` only).

- `PostmarkMessagingGateway.cs` — implements `IMessagingGateway`; calls Postmark API via `Postmark.Net` SDK
- `PostmarkInboundParser.cs` — parses Postmark Inbound webhook payloads into `InboundMessageEnvelope`
- `PostmarkSignatureVerifier.cs` — verifies provider signature (X-Postmark-* header HMAC)
- `DependencyInjection/ServiceCollectionExtensions.cs` — `AddPostmarkMessagingProvider(...)` taking config (API token + inbound webhook secret)
- Tests against Postmark's documented webhook payload shape (no live API calls; recorded fixtures)
- `BannedSymbols.txt` allow-list for Postmark types (provider-neutrality analyzer expects this exclusion file in the providers-* package)

**Gate:** Postmark gateway sends + receives; signature verify works against fixture payloads; provider-neutrality analyzer passes (i.e., no `using Postmark` outside this package).

**PR title:** `feat(providers-postmark): Phase 2.1 first email adapter (ADR 0052 + ADR 0013)`

### Phase 5 — Inbound webhook handler + 5-layer defense (~3–4h)

Implements ADR 0052 amendment A1 in a new `MessagingInboundController.cs` (or equivalent host integration) — likely in `accelerators/bridge/` or as a `blocks-messaging` extension.

5 layers in order:

1. **Provider signature verify** — `PostmarkSignatureVerifier` from Phase 4; reject 401 on failure
2. **Sender allow-list** — `MessagingProviderConfig.AllowedSenderDomains` + `AllowedFromAddresses` per tenant; Phase 2.1 default = empty (accept all but score)
3. **Rate limit** — sliding window (default 30/hr per sender, 300/hr per tenant); exceeded → emit `MessageRateLimitExceeded` audit + soft-reject (200 OK to provider; held in unrouted-triage)
4. **`IInboundMessageScorer`** — default `NullScorer` (always 0); pluggable for spam classifiers
5. **Manual `IUnroutedTriageQueue`** — catch-all when 1–4 reject ambiguously

After all 5 pass: route to thread via `ThreadToken` lookup OR fuzzy sender-recency matching (per A4: 14-day window; token tiebreaker).

**Gate:** integration test covers all 5 layers in success + each-layer-rejection scenarios; A4 fuzzy matching works for SMS-style ingress.

**PR title:** `feat(blocks-messaging): inbound 5-layer defense + thread routing (ADR 0052 A1 + A4)`

### Phase 6 — Audit emission (~1–2h)

Add `AuditEventType` constants to `packages/kernel-audit/AuditEventType.cs` under `===== ADR 0052 — Bidirectional Messaging =====` divider:

```csharp
public static readonly AuditEventType MessageSent = new("MessageSent");
public static readonly AuditEventType MessageDelivered = new("MessageDelivered");
public static readonly AuditEventType MessageReceived = new("MessageReceived");
public static readonly AuditEventType MessageRouted = new("MessageRouted");
public static readonly AuditEventType MessageRoutingAmbiguous = new("MessageRoutingAmbiguous"); // SMS multiple-thread match
public static readonly AuditEventType MessageRateLimitExceeded = new("MessageRateLimitExceeded");
public static readonly AuditEventType InboundSignatureVerifyFailed = new("InboundSignatureVerifyFailed");
public static readonly AuditEventType InboundSenderRejected = new("InboundSenderRejected");
public static readonly AuditEventType ThreadTokenRevoked = new("ThreadTokenRevoked");
public static readonly AuditEventType ThreadCreated = new("ThreadCreated");
public static readonly AuditEventType ThreadSplit = new("ThreadSplit"); // per Minor amendment
public static readonly AuditEventType ThreadClosed = new("ThreadClosed");
```

12 new event types. Author `MessagingAuditPayloadFactory` mirroring W#31 + W#19 patterns.

**Gate:** 12 event types ship; factory works; audit emission verified for each via unit test.

**PR title:** `feat(blocks-messaging): audit emission — 12 AuditEventType + factory (ADR 0049)`

### Phase 7 — Cross-package wiring + integration tests (~1h)

- Verify `IThreadStore.SplitAsync` is callable from W#19 Work Orders Phase 6 (cross-package smoke test)
- Verify `IInboundMessageScorer` plug-in surface is consumable from a future `blocks-public-listings` package (interface stability test)
- End-to-end integration test: outbound email via Postmark → provider webhook → inbound parse → 5-layer defense → thread routing → audit emission

**Gate:** all 3 wiring scenarios pass.

**PR title:** `test(blocks-messaging): Phase 2.1 cross-package integration suite`

### Phase 8 — Tests + apps/docs (~1h)

- `apps/docs/blocks/messaging/overview.md` — substrate overview, 3-tier visibility model, ThreadToken usage, provider config
- `apps/docs/foundation-integrations/messaging.md` — contract surface reference
- Kitchen-sink seed page demonstrating thread + message lifecycle (deferred per Properties/Equipment first-slice precedent — flag as TODO if Yeoman-style block-into-kitchen-sink wiring isn't ready)

**Gate:** `apps/docs` builds; no broken cross-references.

**PR title:** `docs(blocks-messaging): Phase 2.1 substrate apps/docs`

### Phase 9 — Ledger flip (~0.5h)

Update `icm/_state/active-workstreams.md` row #20 from `ready-to-build` → `built` with the merged PR list. Append entry to `## Last updated` footer.

**PR title:** `chore(icm): flip W#20 ledger row → built`

---

## Total decomposition

| Phase | Subject | Hours | PR |
|---|---|---|---|
| 1 | Foundation.Integrations.Messaging contracts | 2–3 | `feat(foundation-integrations): messaging contracts` |
| 2 | `blocks-messaging` InMemory implementations | 4–6 | `feat(blocks-messaging): Phase 2.1 substrate` |
| 3 | `HmacThreadTokenIssuer` per A2 | 1–2 | `feat(foundation-integrations): HmacThreadTokenIssuer` |
| 4 | `providers-postmark` first adapter | 3–4 | `feat(providers-postmark): Phase 2.1 email adapter` |
| 5 | Inbound 5-layer defense (A1 + A4) | 3–4 | `feat(blocks-messaging): inbound defense + routing` |
| 6 | Audit emission (12 AuditEventType) | 1–2 | `feat(blocks-messaging): audit emission` |
| 7 | Cross-package integration tests | 1 | `test(blocks-messaging): integration suite` |
| 8 | apps/docs | 1 | `docs(blocks-messaging): Phase 2.1` |
| 9 | Ledger flip | 0.5 | `chore(icm): W#20 → built` |
| **Total** | | **17.5–24.5h** | **~7 PRs** |

---

## Halt conditions

Per the inbox protocol, halt + write `cob-question-*` beacon if:

- **`Sunfish.Foundation.Recovery.ITenantKeyProvider`** doesn't exist or doesn't expose per-tenant HMAC keys at Phase 3 → `cob-question-*` naming the gap; Phase 3 stalls
- **Postmark.Net SDK API surface diverges** from what ADR 0052's spec assumes (e.g., signature verification API changed) → `cob-question-*` naming the API drift; XO updates ADR if needed
- **`IThreadStore.SplitAsync` semantics ambiguity** when W#19 Phase 6 needs to call it (e.g., does Split copy forward all messages or none?) → `cob-question-*`; XO clarifies
- **Phase 5 webhook host integration** — should the controller live in `accelerators/bridge/`, in `blocks-messaging/`, or as a separate `blocks-messaging-host` package? `cob-question-*` if not obvious from the existing accelerator structure
- **Provider-neutrality analyzer** flags a false positive in `providers-postmark` → `cob-question-*` with the specific symbol

---

## Acceptance criteria (cumulative)

- [ ] `Sunfish.Foundation.Integrations.Messaging` namespace ships with full XML doc + nullability + `required`
- [ ] All 5 contract interfaces compile + are consumable from `blocks-messaging`
- [ ] `MessageVisibility` enum has exactly 3 values (Public, PartyPair, OperatorOnly) per Minor amendment
- [ ] `IThreadStore.SplitAsync` works for W#19 Phase 6 use
- [ ] `HmacThreadTokenIssuer` round-trip verify < 5ms p95 (per A5)
- [ ] `providers-postmark` passes `SUNFISH_PROVNEUT_001` analyzer (no Postmark imports outside this package)
- [ ] Inbound 5-layer defense covers all 5 reject scenarios per ADR 0052 A1
- [ ] SMS thread-resolution: fuzzy sender-recency primary; ThreadToken tiebreaker (per A4)
- [ ] Default `SmsThreadTokenStrategy = OmitToken` for Phase 2.1 (per A4)
- [ ] 12 new `AuditEventType` constants in kernel-audit
- [ ] `MessagingAuditPayloadFactory` ships with one factory method per event type
- [ ] `apps/docs/blocks/messaging/` page + `apps/docs/foundation-integrations/messaging.md` page exist
- [ ] All tests pass; `dotnet build` clean
- [ ] Ledger row #20 → `built`

---

## References

- [ADR 0052](../../docs/adrs/0052-bidirectional-messaging-substrate.md) — full substrate spec + amendments A1–A5 + Minor
- [Council review](../07_review/output/adr-audits/0052-council-review-2026-04-29.md) — surfaced the 5 amendments
- [Cluster intake](../00_intake/output/property-messaging-substrate-intake-2026-04-28.md) — original scope
- [W#19 Work Orders hand-off](./property-work-orders-stage06-handoff.md) — Phase 6 will consume `IThreadStore.SplitAsync` from this hand-off's Phase 2
- [W#31 Foundation.Taxonomy](./foundation-taxonomy-phase1-stage06-handoff.md) — established the AuditEventType + payload-factory cardinality pattern this hand-off mirrors
- ADR 0008 (multi-tenancy), 0013 (provider-neutrality), 0015 (entity-module), 0043 (T2 ingress threat tier per A1), 0046 (Foundation.Recovery for per-tenant keys per A2), 0049 (audit substrate)

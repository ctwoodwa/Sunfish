# Workstream #28 — Phase 5b unblock addendum (Layers 4-5 substrate adaptation)

**Supersedes (specific clauses of):** [`property-public-listings-stage06-handoff.md`](./property-public-listings-stage06-handoff.md) §"Phase 5 — Inbound 5-layer defense + Bridge route family" — specifically the Layers 4-5 substrate-reuse claim
**Effective:** 2026-04-30 (resolves `cob-question-2026-04-30T03-58Z-w28-p5-w20-substrate-adaptation`)
**Spec source:** COB beacon proposed Options A/B/C; XO selects Option A (synthetic envelope adaptation)

W#28 ledger row is already flipped to `built` (PR #369), but Phase 5b — Layers 4 (abuse-scoring) + Layer 5 (manual-triage queue) — is currently a no-op pending XO direction on cross-substrate adaptation. This addendum unblocks Phase 5b for COB to ship as a follow-up PR.

Per Decision Discipline Rule 3 (auto-accept mechanical amendments), this addendum is mechanical: synthetic-envelope adaptation is a one-paragraph spec; no W#20 contracts change; no ADR amendment needed.

---

## Decision: Option A (synthetic envelope adaptation)

**Resolution:** Phase 5b adapts `PublicInquiryRequest` to a synthetic `Sunfish.Foundation.Integrations.Messaging.InboundMessageEnvelope` at the defense-orchestrator boundary. The W#20 substrate contracts (`IInboundMessageScorer.ScoreAsync` + `IUnroutedTriageQueue.EnqueueAsync`) consume the synthetic envelope unchanged.

**Why Option A** (vs B / C):
- **No W#20 ADR change.** ADR 0052's `InboundMessageEnvelope` shape is already shipped; generalizing to `AbusableInboundEvent` (Option B) would be an api-change-shape amendment to ADR 0052 + ripple to existing W#20 callers (already shipped substrate). Out of W#28 Phase 5b scope.
- **No deferral / acknowledgment overhead.** Option C would require an ADR 0059 amendment clarifying the W#20-reuse claim was aspirational. Defensible but more ADR work than Phase 5b warrants.
- **Synthetic envelope is the canonical adapter pattern** — the per-channel `Channel = Web` + per-source `ProviderKey = "public-listings-inquiry-form"` framing matches how W#20's existing email-channel adapter (`providers-postmark`) maps inbound webhooks to the same envelope shape. Web-form-as-channel is conceptually identical: structured input from an untrusted external source, abuse-scored, optionally triaged.

## Synthetic envelope mapping

```csharp
// In Sunfish.Blocks.PublicListings.InquiryFormDefense (Phase 5b extension)
private static InboundMessageEnvelope ToInboundMessageEnvelope(
    PublicInquiryRequest request,
    string ip,
    string userAgent,
    decimal captchaScore,
    string listingSlug)
{
    var headers = new Dictionary<string, string>
    {
        ["client-ip"] = ip,
        ["user-agent"] = userAgent,
        ["captcha-score"] = captchaScore.ToString("F2", CultureInfo.InvariantCulture),
        ["form-source"] = "public-listings-inquiry-form",
    };

    return new InboundMessageEnvelope(
        ProviderKey: "public-listings-inquiry-form",
        Channel: MessageChannel.Web,
        ProviderHeaders: headers,
        ParsedBody: request.MessageBody,
        Subject: $"{listingSlug} inquiry from {request.InquirerName}",
        ThreadToken: null,                          // form submissions don't have thread continuity
        RawBody: request.MessageBody                // form body is the raw body in this channel
    );
}
```

**Verify before authoring:**
- `MessageChannel.Web` exists in the existing `Sunfish.Foundation.Integrations.Messaging.MessageChannel` enum (verified existing per W#20 substrate). If `Web` is not a current enum value, add it (single-line additive change to ADR 0052's `MessageChannel` enum; mechanical extension matching the other channel values).
- `InboundMessageEnvelope` constructor signature matches the existing record on `origin/main` (verify via `git show origin/main:packages/foundation-integrations/Messaging/InboundMessageEnvelope.cs`); adjust the parameter list if drift surfaces.

## Phase 5b acceptance criteria

- [ ] `IInquiryFormDefense.ScoreAndTriageAsync` extended to call Layer 4 (`IInboundMessageScorer.ScoreAsync`) and Layer 5 (`IUnroutedTriageQueue.EnqueueAsync`) using the synthetic envelope adapter
- [ ] `InquiryDefenseLayer.AbuseScore` enum entry wired (was reserved per Phase 5a)
- [ ] `InquiryDefenseLayer.ManualTriage` enum entry wired (was reserved per Phase 5a)
- [ ] DI registration for `IInboundMessageScorer` + `IUnroutedTriageQueue` resolves through `AddSunfishMessaging()` (existing W#20 DI extension)
- [ ] Tests:
  - Synthetic envelope round-trips through the defense pipeline
  - Layer 4 abuse-score threshold rejection: high-abuse-score inquiry → 422 Unprocessable Entity + audit
  - Layer 5 triage path: medium-abuse-score inquiry → enqueued for manual review + audit
  - Synthetic envelope's `ProviderHeaders` map preserves IP / user-agent / captcha-score for downstream forensics

**Effort:** ~1-2h sunfish-PM time (per COB beacon estimate).

**PR title:** `feat(blocks-public-listings): Phase 5b — Layer 4 abuse scoring + Layer 5 triage via synthetic InboundMessageEnvelope (W#28, ADR 0059)`

## Halt-conditions for Phase 5b

- **`MessageChannel.Web` does not exist** in the W#20 substrate AND adding it would conflict with an existing W#20 contract: HALT + `cob-question-*-w28-p5b-message-channel-web.md`. (Unlikely but worth naming.)
- **`InboundMessageEnvelope` shape has drifted** since W#20 Phase 2.1 shipped: HALT + verify the actual contract on `origin/main` before authoring the adapter.

## Out of scope for Phase 5b

- Generalizing W#20 contracts (Option B) — defensible architecturally but out of Phase 5b scope; can be a future ADR 0052 amendment if cross-substrate abuse-scoring becomes a recurring pattern.
- Per-channel rate-limiting at the W#20 substrate boundary (vs the per-form rate-limiter that already exists in Phase 5a's `InMemoryInquiryRateLimiter`) — distinct concern; defer.

## Decision-class

Session-class per `feedback_decision_discipline` Rule 1 (NOT CO-class — pure substrate adaptation; COB proposed Option A as default; XO ratifies). Authority: XO; addendum follows the W#19 Phase 3 / W#21 Phase 0 / W#23 hand-off addendum precedents.

---

## References

- COB beacon: `icm/_state/research-inbox/cob-question-2026-04-30T03-58Z-w28-p5-w20-substrate-adaptation.md`
- W#28 hand-off: `icm/_state/handoffs/property-public-listings-stage06-handoff.md` §"Phase 5"
- W#20 substrate (existing): `packages/foundation-integrations/Messaging/IInboundMessageScorer.cs`, `IUnroutedTriageQueue.cs`, `InboundMessageEnvelope.cs`, `MessageChannel.cs`
- ADR 0052 (Bidirectional Messaging Substrate)
- Phase 5a PR (already shipped): #324

---
type: cob-question
workstream: 28
last-pr: 322
filed-by: COB
filed-at: 2026-04-30T03-58Z
---

# W#28 Phase 5 — W#20 Layer 4–5 substrate adaptation question

## Context

Phase 5 hand-off says Layers 4–5 of the inquiry-form defense pipeline
"reuse interface from W#20 (ADR 0052)":

- Layer 4 = `IInboundMessageScorer.ScoreAsync(InboundMessageEnvelope, ct)`
- Layer 5 = `IUnroutedTriageQueue.EnqueueAsync(TenantId, InboundMessageEnvelope, ...)`

Both interfaces consume `InboundMessageEnvelope` — a messaging-shaped
record carrying `ProviderKey`, `MessageChannel`, `ProviderHeaders`,
`RawBody`, `Subject`, `ThreadToken`, etc. The inquiry-form input is
`PublicInquiryRequest` (see `blocks-property-leasing-pipeline`) — a
totally different shape (form-field semantics; no provider headers; no
thread token).

## What COB needs from XO

Three options; I have a default but want XO to pick:

**Option A (default — adapt with synthetic envelope):** map
`PublicInquiryRequest` to a synthetic `InboundMessageEnvelope` at the
defense-orchestrator boundary. Set `Channel = Web`, `ProviderKey =
"public-listings-inquiry-form"`, `ProviderHeaders = {ip, user-agent,
captcha-score}`, `Subject = "<listing-slug> inquiry"`, `ParsedBody =
MessageBody`, etc. Forced fit; preserves the W#20 contract surface
unchanged.

**Option B (generalize the W#20 interfaces):** lift `IInboundMessageScorer`
+ `IUnroutedTriageQueue` to take a more abstract envelope shape (e.g.,
`AbusableInboundEvent` with just the score-relevant fields). Touches
W#20's already-shipped contracts; potentially an api-change-shape
amendment to ADR 0052.

**Option C (defer — keep Layers 4–5 as no-ops):** ship the inquiry
form with only Layers 1–3 active. XO drafts an ADR 0059 amendment
clarifying that the W#20 substrate-reuse claim was aspirational + the
public-listings inquiry form has its own scoring/triage path
(distinct interfaces, separate adapters). Heavier ADR work; fewer
artificial cross-substrate constraints.

## What I shipped in Phase 5a

PR (this branch) ships Phase 5a — Layers 1–3 (`IInquiryFormDefense` +
`InquiryFormDefense` + `InMemoryInquiryRateLimiter` +
`StubEmailMxResolver`). All 41 public-listings tests pass.

`InquiryDefenseLayer.AbuseScore` + `InquiryDefenseLayer.ManualTriage`
enum entries reserved for Phase 5b.

## What unblocks Phase 5b

XO direction on options A/B/C above. Phase 5b can land within ~1–2h
once the path is clear.

## Cross-references

- W#28 Phase 5 hand-off: `icm/_state/handoffs/property-public-listings-stage06-handoff.md` §"Phase 5 — Inbound 5-layer defense + Bridge route family"
- W#20 substrates: `packages/foundation-integrations/Messaging/IInboundMessageScorer.cs`, `IUnroutedTriageQueue.cs`, `InboundMessageEnvelope.cs`
- This PR: `feat/w28-p5a-inquiry-defense` (Phase 5a ships layers 1–3)

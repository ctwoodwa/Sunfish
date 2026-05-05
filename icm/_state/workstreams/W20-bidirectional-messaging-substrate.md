---
sort_order: 19
number: 20
slug: bidirectional-messaging-substrate
title: "Bidirectional Messaging Substrate (cluster #4 spine)"
status: "building"
status_cell: "`building` (Phases 0/2.1/3 shipped 2026-04-29; remaining phases queued)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/00_intake/output/property-messaging-substrate-intake-2026-04-28.md` + `docs/adrs/0052-bidirectional-messaging-substrate.md` + `icm/_state/handoffs/property-messaging-substrate-stage06-handoff.md` + `icm/_state/handoffs/property-messaging-substrate-stage06-addendum.md`"
---

## Notes

**In flight; substrate scaffolded.** Phase 2.1 contracts shipped (PR #273, `Sunfish.Foundation.Integrations.Messaging`); blocks-messaging substrate scaffold (PR #276, Thread + Message + InMemory + ADR 0015 entity-module); Phase 0 ITenantKeyProvider stub addendum (PR #294); Phase 0 + Phase 3 HmacThreadTokenIssuer (PR #302). Remaining phases (providers-postmark adapter, 5-layer inbound defense, full audit emission, kitchen-sink wiring) queued in COB's pipeline. SendGrid + Twilio deferred. **Cross-substrate: W#19 Phase 6 already consumed `IThreadStore.SplitAsync` via stub** — sequence resolved.

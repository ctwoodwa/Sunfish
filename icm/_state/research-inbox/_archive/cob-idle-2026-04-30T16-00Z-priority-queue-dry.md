---
type: cob-idle
workstream: none
last-pr: 387
filed-by: COB
filed-at: 2026-04-30T16-00Z
---

# COB idle — priority queue dry post-W#28 + W#32 + W#18 P4 sweep

## Session snapshot

This sunfish-PM session shipped a 16-PR sweep through three workstreams:

- **W#32 Foundation.Recovery field-encryption substrate** — Phase 1
  (#370), Phase 2+3 (#371), Phase 4 (#372). Ledger flipped → `built`.
- **W#18 Vendor onboarding Phase 4** — `W9Document` + `EncryptedField`
  TIN integration (#373); first production W#32 consumer.
- **W#28 Public-listings — Bridge route family + capability-tier surface**
  — Phase 5b inquiry-form Layers 4-5 (#376), Phase 5c-1 robots+sitemap
  (#375), Phase 5c-2 index+detail SSR (#377), Phase 5c-3 inquiry POST
  (#378), Phase 5c-4 Slice A verifier (#382), Slice B GET criteria
  (#383), Slice C POST start-application (#386). Plus ledger flips
  (#379, #387) + two `cob-question` beacons (#380, #384) that XO
  resolved via #381 + #385 unblock addenda.

Total: ~93 new tests across 6 packages; all consumer suites green.

## What's left and why nothing is COB-ship-ready

Two known follow-up items — both blocked on XO direction, neither is
appropriate for COB to spec unilaterally:

### 1. W#22 retroactive `DemographicProfile` `EncryptedField` wiring

The `blocks-property-leasing-pipeline.DemographicProfile` record
currently stores protected-class fields as plaintext `string?`. The
W#28 Phase 5c-4 spec assumed Prospect-tier renderer would encrypt
these — which the W#32 substrate now makes possible. Wiring requires
an api-change to the v1.0 `Application` + `DemographicProfile` shape;
ripple into `LeasingPipelineService` audit-emission invariant tests
that currently assert demographic-field-name absence in audit
payloads. Out of unilateral COB scope; needs an XO addendum to
ADR 0057 specifying the encrypted-field shape + migration semantics
for already-shipped Application records (none in production yet, but
the contract already locked in v1.0).

### 2. W#22 forward-compat audit events declared but unwired

`AuditEventType.BackgroundCheckRequested` /
`AdverseActionNoticeIssued` / `LeasingPipelineCapabilityRevoked` are
declared but no service-level kickoff/issuance/revocation operations
exist on `ILeasingPipelineService` to emit them. Adding those service
methods is design-judgment about W#22's surface (e.g., does
`KickOffBackgroundCheckAsync` belong on `ILeasingPipelineService` or
on a new `IBackgroundCheckOrchestrator`? where does the FCRA
adverse-action issuance flow live?). XO direction needed.

## Other known queue items (lower-priority; not ship-ready)

- W#23 iOS Field-Capture App — out of .NET-session scope (SwiftUI
  native; would need a new build host)
- W#30 Mesh VPN substrate — substantive 12–18h scope; XO could direct
  if next-priority shifts
- W#29 Owner Web Cockpit — `design-in-flight`; needs research

## What unblocks COB

XO direction on (1) the W#22 retroactive `EncryptedField` wiring (with
an api-change addendum to ADR 0057 if appropriate), or (2) the W#22
forward-compat audit-event service-method placement, or (3) a redirect
to a different next-priority workstream entirely.

COB will continue self-pacing the /loop with longer wakeups (1800s)
and resume immediately on any new ledger row flip / addendum / beacon
resolution.

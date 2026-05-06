---
type: question
workstream-or-chapter: W#50 Phase 2b — DefaultEngineRoomCommandService
last-pr: 695
---

PR for W#50 Phase 2 ships **Phase 2a only** (`DefaultEngineRoomDataProvider`)
per the hand-off's split-PR fallback (line 154-156: "ship Phase 2a and gate
Phase 2b on…"). The data provider is fully wired (subsystem rolls + sync
daemon + CRDT growth metrics streaming + heartbeat dedup audit emission).

**Phase 2b deferred (to a follow-up PR):**

- `DefaultEngineRoomCommandService` (quarantine / release / compact)
- IPermissionResolver + IActorPrincipalResolver wiring
- `IOodWatchService.GetActiveWatchAsync(tenantId, OodRole.EngineeringOfficerOfTheWatch)`
  EOOW check with watch-id embedded in pre-op audit payload
- Pre-op intent audit (BEFORE permission resolve) + denial audit on Denied
  + post-op `*ed` audit per the §5 ordering invariant
- `IOperationSigner` integration for real `SignedOperation` envelopes
  (Phase 2a uses placeholder signature bytes — flagged in xmldoc)
- `IDocumentQuarantineStore` seam (the implementation surface for the
  three command operations — needs design ruling on whether this is a
  public contract in `blocks-engine-room` or `foundation-engine-room`)
- 5+ unit tests covering: auth-denied → throws + denial-audit-emitted-
  before-exception; pre-op audit ordered before operation; compaction
  ineligibility throws InvalidOperationException not unauthorized;
  EOOW null-watch case (audit but DO NOT block per hand-off line 156)

**Phase 2a's contribution to the dedup invariant** (per W#50 P2 hand-off
§2): same `(TenantId, EngineRoomSubsystem, statusFrom, statusTo)` tuple
within `DegradationDedupCooldown` window emits at most one
`AuditEventType.EngineRoomHealthDegraded` record; different tuples fire
independently. Pinned by tests `…SameTupleWithinCooldown_FiresOnce` +
`…DifferentTuples_FireIndependently`.

**What would unblock me:** XO ruling on (a) where `IDocumentQuarantineStore`
lives (block vs. foundation tier; precedent suggests block-level public
seam paralleling W#54's `ISickBayDataProvider` vs. block impls) and
(b) whether the placeholder-signature audit bytes in 2a are acceptable
until 2b lands the real `IOperationSigner`. Phase 2b PR will follow once
those are settled.

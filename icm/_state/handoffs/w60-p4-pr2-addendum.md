---
workstream: w60-erpnext-react-ui-phase2
phase: 4
addendum-to: blocks-financial-periods-stage06-handoff.md
authored: 2026-05-16
authored-by: COB
status: ready-to-build-in-pr3-and-pr5
---

# W#60 P4 PR 2 — Addendum: deferred council findings

Council review of W#60 P4 PR 2 (`cob/blocks-financial-periods-soft-close-and-resolver`)
returned 1 BLOCKING + 5 MAJOR + 9 minor findings. PR 2 absorbed the
BLOCKING (B1: ReopenAsync Open/Locked split) + 2 MAJOR (M1 ×2: switch
fail-closed + repo TryUpdate CAS) + 4 minor (audit-principal-id flow,
result-field doc, role-gate doc, Singleton-intent doc).

The remaining items are tracked here so PR 3 + PR 5 can pick them up
without re-litigating the council.

---

## Deferred to PR 3 (hard-close + retained-earnings)

### D1 — `FiscalPeriod` + `FiscalYear` need a `Version` field (optimistic concurrency)

**Source:** Security M2 + Architecture M4.

`PeriodCloseService.SoftCloseAsync` / `ReopenAsync` are read-mutate-write.
PR 2 mitigates intra-process races with a CAS loop in
`InMemoryFiscalPeriodRepository.UpdateAsync` (compare prior value),
but the entity itself has no version surface for cross-process
concurrency (real SQLite repo + multi-window admin races).

**Action in PR 3:**

1. Add `int Version` field to `FiscalPeriod` + `FiscalYear` records
   (initialized to 0 on `CreateOpen`).
2. Bump `Version` in every `PeriodCloseService` mutation (`with { ..., Version = period.Version + 1 }`).
3. Update `IFiscalPeriodRepository.UpdateAsync` /
   `IFiscalYearRepository.UpdateAsync` to take the expected prior
   version + return `PeriodCloseError.ConcurrentUpdate` (new enum case)
   when the stored row's version doesn't match.
4. The SQLite repo impl uses a `WHERE Id = @id AND Version = @priorVersion`
   clause on the UPDATE; row-count check decides ConcurrentUpdate vs
   success.

**Why deferred:** Schema change to PR 1 entities; cleaner to land as
part of PR 3's wider repo-contract expansion (transactions, batch update,
status-filtered queries — see D3 below) than to amend PR 1 in PR 2.

### D2 — Publish-after-commit atomicity (`IUnitOfWork` / `BeginTransactionAsync`)

**Source:** Architecture M3.

PR 2's `PeriodCloseService.SoftCloseAsync` does `UpdateAsync` →
`PublishAsync` with no transaction boundary. If publish fails (Noop
can't fail; the real bus can), the row is updated but the event is
lost — consumer-side projections (reports cluster's period-status
panel, ledger's gate cache) drift.

**Action in PR 3:**

Choose one of:

- **(a) Outbox pattern** — `IFiscalPeriodRepository.SaveEventAsync(payload, ct)`
  writes the event row in the same SQLite transaction as the period
  update; a separate dispatcher (PR 5+) drains the outbox to the bus.
- **(b) Transactional seam** — add
  `Task<IAsyncDisposable> BeginTransactionAsync(CT)` to both repos;
  service-layer wraps update+publish in a `using` block;
  on dispose: commit if no exception, rollback otherwise.

Recommend (a) — outbox is the canonical pattern at
`_shared/engineering/cross-cluster-event-bus-design.md` §5 and PR 3's
year-end close needs it anyway (N periods + 1 closing JE + 1 FY row
update + ≥3 events as one atomic action).

### D3 — `IFiscalPeriodRepository` contract gaps for PR 3

**Source:** Architecture M4.

PR 3's `closeFiscalYear` algorithm needs:

1. `GetByFiscalYearAndStatusAsync(FiscalYearId, FiscalPeriodStatus)` —
   filter "all Open periods" without client-side filtering.
2. `UpdateBatchAsync(IReadOnlyList<FiscalPeriod>, CT)` — batch-lock
   N periods in one transaction.
3. `UpsertAsync` for ERPNext importer idempotent replay (PR 4 prep).

**Action in PR 3:** add methods + InMemory impls + SQLite impls.

---

## Deferred to PR 5 (Anchor + Bridge UI surfaces)

### D4 — Role-gate enforcement at the wiring layer

**Source:** Security m4 + Architecture (implicit).

`IPeriodCloseService` deliberately does NOT consult `IUserContext`.
PR 2 added an XML-doc authorization warning. PR 5 wiring must:

1. Anchor Razor page `ClosePeriodPage.razor`: gate the
   "Close period" button on `IUserContext.HasRole("FinancialAdmin")`.
2. Bridge controller `ClosePeriodController`: `[Authorize(Roles = "FinancialAdmin")]`
   attribute on the action method.
3. Anchor + Bridge call-sites pass `IUserContext.UserId` as
   `closedByPrincipalId` to `SoftCloseAsync` so the
   `Financial.PeriodSoftClosed` event payload carries the auditor's
   identity (the field was deliberately surfaced on the service
   signature in PR 2 for this purpose).

### D5 — Reopen audit-memo persistence (parallel audit log)

**Source:** Security m2.

PR 2 flows the reopen audit memo into `Financial.PeriodOpened.Reason`
only. If the canonical event bus is a Noop publisher (PR 2's default),
the memo is silently dropped.

**Action in PR 5:**

Either (a) require a non-Noop publisher in production hosts via a
startup-time guard (`AddBlocksFinancialPeriods` throws when
`IDomainEventPublisher` is still `NoopDomainEventPublisher` at host
build), OR (b) add a dedicated `IPeriodAuditLog.RecordReopenAsync(periodId,
principalId, memo, atUtc)` seam analogous to W#67's `IAuditEventLog` +
`RecoveryRekeyPayload`.

Recommend (b) for SOX-class trails; (a) at minimum.

---

## Deferred until canonical event-bus home lands

### D6 — `IDomainEventPublisher` envelope migration

**Source:** Architecture M2 + the existing cob-question file (see
`cob-question-2026-05-16T*-domain-event-publisher-home.md` filed
2026-05-16T22Z).

The local `IDomainEventPublisher.PublishAsync<TPayload>(TPayload, CT)`
shape is intentionally minimal. When the canonical
`foundation-events` / `kernel-events` home is ratified
(`_shared/engineering/cross-cluster-event-bus-design.md` §1, Q1), the
shape needs to expand to carry the canonical envelope: `eventId /
eventType / schemaVersion / tenantId / originatingReplicaId /
occurredAt / recordedAtUtc / causationId / correlationId /
producerCluster / idempotencyKey`.

**Action when canonical home lands:**

1. Move `IDomainEventPublisher` from
   `Sunfish.Blocks.FinancialPeriods.Services` to the canonical home.
2. Widen signature to
   `PublishAsync<TPayload>(string eventType, string idempotencyKey, TPayload payload, CT)`
   (minimum), or carve a `DomainEvent` value-type bundling those.
3. Update `PeriodCloseService` call-sites to pass
   `"Financial.PeriodSoftClosed"` + canonical idempotency key
   (`$"period-soft-closed:{periodId}"` per §3.1 catalog).

This addendum entry references the existing `cob-question` so the
canonical-home owner sees the impact list.

---

## Status when PR 2 lands

PR 2 is **APPROVED WITH ADDENDUM** per the council's recommendation:
in-PR fixes shipped; D1–D6 tracked here for PR 3 + PR 5 + the
event-bus-home decision. No work in this addendum gates PR 2 merge.

Next workstream priority (per `xo-ruling-2026-05-17T00-15Z-cob-queue-confirmed.md`)
remains: **blocks-financial-periods PR 3 (hard-close + year-end
rollover)** — which subsumes D1, D2, D3 above.

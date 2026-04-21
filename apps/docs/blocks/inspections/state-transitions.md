---
uid: block-inspections-state-transitions
title: Inspections — State Transitions
description: Allowed lifecycle transitions for Inspection and Deficiency entities, and the service methods that perform them.
keywords:
  - sunfish
  - inspections
  - state-machine
  - inspection-phase
  - deficiency-status
  - lifecycle
---

# Inspections — State Transitions

## Overview

Two enums carry lifecycle state in the inspections block: `InspectionPhase` and
`DeficiencyStatus`. Transitions on `InspectionPhase` are enforced by `IInspectionsService`
and throw `InvalidOperationException` on invalid targets. Transitions on
`DeficiencyStatus` are not yet exposed through service methods — deficiencies are passive
records in this pass.

## Inspection phase

```
           ┌─────────────────┐
           │    Scheduled    │
           └────────┬────────┘
                    │ StartAsync
                    ▼
           ┌─────────────────┐    RecordResponseAsync
  (self) ← │   InProgress    │ ←───────────┐
           └────────┬────────┘             │
                    │ CompleteAsync        │
                    ▼                      │
           ┌─────────────────┐             │
           │    Completed    │             │
           └─────────────────┘             │
                                           │
           (Cancelled — reserved, no       │
            transition method yet) ────────┘
```

### Allowed transitions

| From         | To           | Method                          | Notes |
|--------------|--------------|----------------------------------|-------|
| `Scheduled`  | `InProgress` | `StartAsync`                     | Records `StartedAtUtc`. |
| `InProgress` | `Completed`  | `CompleteAsync`                  | Records `CompletedAtUtc`. |
| `InProgress` | `InProgress` | `RecordResponseAsync`            | Appends to `Responses`; no phase change. |
| `Scheduled` or `InProgress` | `Cancelled` | *(deferred)* | No method yet; enum value reserved. |

Any other transition throws `InvalidOperationException` — for example, calling
`StartAsync` on a `Completed` inspection, or `RecordResponseAsync` on a `Scheduled` one.

### Terminal phases

`Completed` and `Cancelled` are terminal. There is no method to re-open a completed
inspection; create a new one if re-inspection is required.

## Deficiency status

Deficiencies are passive in this pass — there is no service method that transitions
`DeficiencyStatus`. The enum exists so that future passes (and consumer code that writes
directly to a persistence-backed store) have a canonical value set.

| Value            | Meaning |
|------------------|---------|
| `Open`           | Recorded but no action taken yet. |
| `Acknowledged`   | Reviewed by responsible staff. |
| `Resolved`       | Remediated and closed. |
| `Deferred`       | Intentionally deferred to a later date. |

The intended future path: completing a work order in `blocks-maintenance` will transition
the linked deficiency to `Resolved`. That rollup is explicitly deferred to the second pass
of `blocks-maintenance`.

## Response recording is idempotent on phase

`RecordResponseAsync` appends to `Inspection.Responses` but does not change the phase —
the inspection stays in `InProgress` throughout. Complete only when the inspector has
captured every response they plan to record.

## Report generation does not transition state

`GenerateReportAsync` is a read-through operation. It does not change the inspection's
phase, does not mutate deficiency status, and can be called repeatedly — each call yields
a new `InspectionReport` snapshot that reflects the state at generation time.

## Worked example — an illegal transition

Pinned by `StartAsync_WhenNotScheduled_ThrowsInvalidOperationException`:

```csharp
var (svc, template) = await MakeServiceWithTemplate();
var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
await svc.StartAsync(inspection.Id);

// Inspection is now InProgress — starting again throws.
await Assert.ThrowsAsync<InvalidOperationException>(
    () => svc.StartAsync(inspection.Id).AsTask());
```

Similarly, `CompleteAsync_WhenNotInProgress_ThrowsInvalidOperationException` asserts that
completing a `Scheduled` inspection throws. The service never enters a half-transitioned
state — either the transition succeeds atomically and the returned record reflects the new
phase, or it throws and the stored record is untouched.

## Test-surface cross-reference

| Test | What it pins |
|---|---|
| `ScheduleAsync_CreatesInspection_InScheduledPhase` | `ScheduleAsync` always produces `Scheduled`. |
| `StartAsync_Scheduled_TransitionsToInProgress_AndSetsStartedAtUtc` | `Scheduled → InProgress` with `StartedAtUtc` stamped. |
| `CompleteAsync_InProgress_TransitionsToCompleted_AndSetsCompletedAtUtc` | `InProgress → Completed` with `CompletedAtUtc` stamped. |
| `RecordResponseAsync_AppendsToResponses` | Response append is idempotent on phase. |
| `ConcurrentRecordResponseAsync_OnSameInspection_AreSerializedNoLostResponses` | Per-inspection lock prevents lost responses. |

## Deferred transitions

The `Cancelled` value is reserved but no service method drives it today. When a future pass
adds a `CancelAsync`, the allowed transitions are expected to be:
`Scheduled → Cancelled` and `InProgress → Cancelled`; `Completed → Cancelled` must remain
forbidden.

## Related pages

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- [Service Contract](service-contract.md)

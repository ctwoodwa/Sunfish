# Sunfish.Blocks.Scheduling

Schedule-view orchestration block — wraps `SunfishScheduler`, `SunfishAllocationScheduler`, and `SunfishCalendar` into a single view-switcher.

## What this ships

### Models

- **`ScheduleBlockView`** — view-mode discriminator (Calendar / TimelineDay / TimelineWeek / TimelineMonth / AllocationGrid).
- **`SlotReservation`** — reserved time-slot record (start, end, owner, status).
- **`ReservationOutcome`** — projection of a reservation's state (Pending / Confirmed / Rejected / Cancelled).

### Services

- **`ISchedulingService`** + `InMemorySchedulingService` — slot reservation + view query + conflict detection.

### UI

- Razor components composing `SunfishScheduler` / `SunfishAllocationScheduler` / `SunfishCalendar` into a unified view-switcher.

## DI

```csharp
services.AddInMemoryScheduling();
```

## Cluster role

Horizontal scheduling primitive. Used by `blocks-public-listings.ShowingAvailability`, `blocks-maintenance.WorkOrderAppointment`, and any future block needing time-slot reservation.

## See also

- [apps/docs Overview](../../apps/docs/blocks/scheduling/overview.md)

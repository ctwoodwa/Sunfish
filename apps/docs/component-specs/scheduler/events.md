---
title: Events
page_title: Scheduler - Events
description: Events in the Scheduler for Blazor.
slug: scheduler-events
tags: sunfish,blazor,scheduler,events
published: true
position: 100
components: ["scheduler"]
---
# Scheduler Events

> **Spec version:** v1.0 -- updated to reflect implemented events in source.

This article explains the events available in the Sunfish Scheduler for Blazor:

* [CRUD Events](#crud-events) -- `OnAppointmentCreate`, `OnUpdate`, `OnDelete`
* [OnAppointmentClick](#onappointmentclick)
* [OnDateClick](#ondateclick)
* [CurrentDateChanged](#currentdatechanged)
* [ViewChanged](#viewchanged)

## CRUD Events

To enable editing, set `Editable="true"` on the Scheduler. The component fires the following events to let you propagate changes to your data store:

| Event | Type | Trigger |
|-------|------|---------|
| `OnAppointmentCreate` | `EventCallback<SchedulerAppointment>` | Fires when a new appointment is created via drag-to-create in Day/Week views. The event args contain the new appointment with `Start`/`End` set from the drag range. |
| `OnUpdate` | `EventCallback<SchedulerAppointment>` | Fires when the user saves changes in the edit popup or drag-reschedules an appointment. The event args contain the updated appointment. |
| `OnDelete` | `EventCallback<SchedulerAppointment>` | Fires when the user deletes an appointment via the edit popup Delete button. |

@[template](/_contentTemplates/common/onmodelinit.md#onmodelinit-info)
The Scheduler is fully controlled -- it does not mutate its own `Appointments` collection. Update your data source in the event handler for visual changes to appear.

>caption CRUD event handling

````RAZOR
<SunfishScheduler Appointments="@_appointments"
                 CurrentDate="@(new DateTime(2026, 4, 13))"
                 View="SchedulerView.Month"
                 Editable="true"
                 OnAppointmentCreate="@HandleCreate"
                 OnUpdate="@HandleUpdate"
                 OnDelete="@HandleDelete"
                 Height="500px" />

@code {
    private List<SchedulerAppointment> _appointments = new() { /* ... */ };

    private void HandleCreate(SchedulerAppointment appt)
    {
        _appointments = new(_appointments) { appt };
    }

    private void HandleUpdate(SchedulerAppointment updated)
    {
        var idx = _appointments.FindIndex(a => a.Id == updated.Id);
        if (idx >= 0) _appointments[idx] = updated;
    }

    private void HandleDelete(SchedulerAppointment deleted)
    {
        _appointments.RemoveAll(a => a.Id == deleted.Id);
    }
}
````

## OnAppointmentClick

Fires when the user clicks an appointment in any view.

| Parameter | Type |
|-----------|------|
| `OnAppointmentClick` | `EventCallback<SchedulerAppointment>` |

````RAZOR
<SunfishScheduler Appointments="@_appointments"
                 CurrentDate="@(new DateTime(2026, 4, 13))"
                 OnAppointmentClick="@HandleClick"
                 Height="500px" />

@code {
    private void HandleClick(SchedulerAppointment appt)
    {
        Console.WriteLine($"Clicked: {appt.Title}");
    }
}
````

## OnDateClick

Fires when the user clicks a date cell in Month view. The `DateTime` argument contains the clicked date.

| Parameter | Type |
|-----------|------|
| `OnDateClick` | `EventCallback<DateTime>` |

````RAZOR
<SunfishScheduler Appointments="@_appointments"
                 CurrentDate="@(new DateTime(2026, 4, 13))"
                 View="SchedulerView.Month"
                 OnDateClick="@HandleDateClick"
                 Height="500px" />

@code {
    private void HandleDateClick(DateTime date)
    {
        Console.WriteLine($"Clicked date: {date:yyyy-MM-dd}");
    }
}
````

## CurrentDateChanged

Fires when the visible date changes via the Previous/Next navigation buttons. Use with two-way binding (`@bind-CurrentDate`).

| Parameter | Type |
|-----------|------|
| `CurrentDateChanged` | `EventCallback<DateTime>` |

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)
@[template](/_contentTemplates/common/general-info.md#event-callback-can-be-async)
Navigation increments by view:
- **Month**: +/- 1 month
- **Week**: +/- 7 days
- **Day**: +/- 1 day
- **MultiDay**: +/- NumberOfDays
- **Timeline**: +/- 1 day
- **Agenda**: +/- NumberOfDays

## ViewChanged

Fires when the user switches views via the toolbar buttons. Use with two-way binding (`@bind-View`).

| Parameter | Type |
|-----------|------|
| `ViewChanged` | `EventCallback<SchedulerView>` |


## Unimplemented Events (Gap)

The following events are documented in the spec but **not yet implemented** in source. They are tracked in the scheduler-gap-analysis workspace:

@[template](/_contentTemplates/common/general-info.md#event-callback-can-be-async)
- `OnModelInit` -- factory callback for appointment creation
- `OnItemDoubleClick` -- separate from the edit-popup behavior
- `OnItemContextMenu` -- right-click context menu
- `ItemRender` / `OnItemRender` -- per-item render callback
- `OnCellRender` -- per-cell render callback
- `OnEdit` / `OnCancel` -- edit lifecycle callbacks
- `AllowCreate` / `AllowUpdate` / `AllowDelete` -- granular permission flags (current implementation uses single `Editable` flag)


## See Also

* [Scheduler Overview](slug:scheduler-overview)
* [Scheduler Editing](slug:scheduler-appointments-edit)
* [Live Demo: Scheduler](https://demos.sunfish.dev/blazor-ui/scheduler/overview)

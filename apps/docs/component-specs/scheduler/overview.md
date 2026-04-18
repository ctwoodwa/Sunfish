---
title: Overview
page_title: Scheduler Overview
description: Overview of the Scheduler for Blazor.
slug: scheduler-overview
tags: sunfish,blazor,scheduler,overview
published: True
position: 0
components: ["scheduler"]
---
# Blazor Scheduler Overview

> **Spec version:** v1.0 — full parameter table reflects source. Source implements 20 parameters on `SunfishScheduler` plus composable child view components (`SchedulerDayView`, `SchedulerWeekView`, `SchedulerMonthView`, `SchedulerMultiDayView`, `SchedulerTimelineView`, `SchedulerAgendaView`).

The Blazor Scheduler component lets users see, edit and add appointments, so they can plan their agenda. The Scheduler offers different views, control over the workday start and end, resource grouping, drag-and-drop, templates and various other features and settings.


## Creating Blazor Scheduler

1. Use the `<SunfishScheduler>` tag.
1. Set its `Appointments` parameter to `IEnumerable<SchedulerAppointment>` to provide the collection of appointments. The `SchedulerAppointment` model lives in `Sunfish.Core.Models` and has properties `Title`, `Description`, `Start`, `End`, `IsAllDay`, `Color`, `RecurrenceRule`, and `Id`.
1. (optional) Define the available views by placing child view components (`SchedulerDayView`, `SchedulerWeekView`, `SchedulerMonthView`, `SchedulerMultiDayView`, `SchedulerTimelineView`, `SchedulerAgendaView`) inside `ChildContent`. Only registered views appear in the toolbar. When no child views are registered, the toolbar defaults to Day, Week, and Month.
1. (optional) Set `CurrentDate` and `View` parameters. By default, users will see today's date and the Month view. Both parameters support two-way binding via `CurrentDateChanged` and `ViewChanged`.

>caption Basic Scheduler

````RAZOR
<SunfishScheduler Appointments="@_appointments"
                 @bind-CurrentDate="@_startDate"
                 @bind-View="@_currentView"
                 Height="600px">
    <SchedulerDayView StartTime="TimeSpan.FromHours(6)" EndTime="TimeSpan.FromHours(19)" />
    <SchedulerWeekView StartTime="TimeSpan.FromHours(6)" EndTime="TimeSpan.FromHours(19)" />
    <SchedulerMonthView />
    <SchedulerTimelineView StartTime="TimeSpan.FromHours(6)" EndTime="TimeSpan.FromHours(19)" />
</SunfishScheduler>

@code {
    private DateTime _startDate = new DateTime(2026, 4, 13);
    private SchedulerView _currentView = SchedulerView.Week;

    private List<SchedulerAppointment> _appointments = new()
    {
        new SchedulerAppointment
        {
            Title = "Planning meeting",
            Start = new DateTime(2026, 4, 13, 9, 30, 0),
            End = new DateTime(2026, 4, 13, 12, 45, 0)
        },
        new SchedulerAppointment
        {
            Title = "Vet visit",
            Start = new DateTime(2026, 4, 14, 7, 0, 0),
            End = new DateTime(2026, 4, 14, 7, 30, 0)
        },
        new SchedulerAppointment
        {
            Title = "Trip to Hawaii",
            IsAllDay = true,
            Start = new DateTime(2026, 4, 15),
            End = new DateTime(2026, 4, 20)
        }
    };
}
````


## Data Binding

The Scheduler works with `IEnumerable<SchedulerAppointment>`. See [Data Binding](slug:scheduler-appointments-databinding) for details on the `SchedulerAppointment` model and how to map custom property names.


## Views

The [Scheduler offers different views](slug:scheduler-views-overview) that are suitable for different user needs:

* **Day view** -- single day with hourly slots
* **Week view** -- 7 day columns with hourly slots
* **Month view** -- calendar grid
* **MultiDay view** -- configurable N-day columns with hourly slots
* **Timeline view** -- horizontal time axis with day rows
* **Agenda view** -- flat chronological list grouped by date


## Navigation

The [Scheduler features built-in navigation](slug:scheduler-navigation) via Previous/Next toolbar buttons. Navigation increments depend on the active view (month, week, day, N-days, or agenda range). Both `CurrentDate` and `View` support two-way binding for programmatic control.


## Editing

Set `Editable="true"` to enable CRUD operations. Double-click an appointment to open the built-in edit popup. Handle `OnUpdate`, `OnDelete`, and `OnAppointmentCreate` events to propagate changes. Drag-to-create and drag-to-reschedule are supported in Day and Week views. See [Editing](slug:scheduler-appointments-edit) for details.


## Recurrence

The `SchedulerAppointment.RecurrenceRule` property stores iCalendar RRULE strings. See [Recurrence](slug:scheduler-recurrence) for details.


## Resources and Grouping

[Scheduler resources](slug:scheduler-resources) let you associate appointments with shared resources (rooms, people, equipment) and [group appointments](slug:scheduler-resource-grouping) by resource. Set `Resources`, `ResourceIdField`, and `GroupByResource` parameters. Grouping is supported in Day, Week, and Month views.


## Templates

Use the `AppointmentTemplate` render fragment to customize appointment appearance. The template receives the `SchedulerAppointment` as context and applies across all views. See [Templates](slug:scheduler-templates-appointment).


## Events

The [Scheduler fires events](slug:scheduler-events) for CRUD operations, appointment clicks, date clicks, and navigation. Key events: `OnAppointmentClick`, `OnDateClick`, `OnAppointmentCreate`, `OnUpdate`, `OnDelete`, `CurrentDateChanged`, `ViewChanged`.


## Scheduler Parameters -- Complete Reference

The following table lists **every** parameter on `SunfishScheduler` as implemented in source.

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)
### SunfishScheduler Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `CurrentDate` | `DateTime` | `DateTime.Today` | The currently visible date. Supports two-way binding via `CurrentDateChanged`. |
| `CurrentDateChanged` | `EventCallback<DateTime>` | -- | Fires when the visible date changes (navigation or programmatic). |
| `View` | `SchedulerView` | `SchedulerView.Month` | The active view. Supports two-way binding via `ViewChanged`. |
| `ViewChanged` | `EventCallback<SchedulerView>` | -- | Fires when the user switches views. |
| `Appointments` | `IEnumerable<SchedulerAppointment>` | `Empty` | The collection of appointments to display. |
| `StartHour` | `int` | `8` | Fallback start hour for Day/Week/MultiDay/Timeline views when no child view component is registered. |
| `EndHour` | `int` | `18` | Fallback end hour for Day/Week/MultiDay/Timeline views when no child view component is registered. |
| `Editable` | `bool` | `false` | Enables double-click editing popup, drag-to-create, and drag-to-reschedule. |
| `OnAppointmentClick` | `EventCallback<SchedulerAppointment>` | -- | Fires when the user clicks an appointment. |
| `OnDateClick` | `EventCallback<DateTime>` | -- | Fires when the user clicks a date cell (Month view). |
| `OnAppointmentCreate` | `EventCallback<SchedulerAppointment>` | -- | Fires when a new appointment is created (drag-to-create or edit form). |
| `OnUpdate` | `EventCallback<SchedulerAppointment>` | -- | Fires when an appointment is updated (edit form save or drag-to-reschedule). |
| `OnDelete` | `EventCallback<SchedulerAppointment>` | -- | Fires when an appointment is deleted via the edit form. |
| `AppointmentTemplate` | `RenderFragment<SchedulerAppointment>?` | `null` | Custom template for rendering appointments across all views. |
| `ChildContent` | `RenderFragment?` | `null` | Slot for child view configuration components (`SchedulerDayView`, etc.). |
| `Height` | `string?` | `null` | CSS height value applied as an inline style. |
| `Width` | `string?` | `null` | CSS width value applied as an inline style. |
| `Resources` | `IEnumerable<SchedulerResource>?` | `null` | Collection of resources for grouping. Each has `Id` (object), `Text` (string), `Color` (string?). |
| `ResourceIdField` | `string?` | `null` | Property name on the appointment model that holds the resource ID. Resolved via reflection. |
| `GroupByResource` | `bool` | `false` | When `true` and `Resources` is set, views group appointments by resource. |
| `Class` | `string?` | `null` | *(Inherited from SunfishComponentBase)* Additional CSS class(es) for the root element. |
| `Style` | `string?` | `null` | *(Inherited from SunfishComponentBase)* Additional inline styles for the root element. |

### SchedulerView Enum

| Value | Description |
|-------|-------------|
| `Day` | Single day with hourly slots. |
| `Week` | 7-day columns with hourly slots. |
| `Month` | Calendar grid. |
| `MultiDay` | Configurable N-day columns. |
| `Timeline` | Horizontal time axis. |
| `Agenda` | Flat chronological list. |

### SchedulerAppointment Model

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | `Guid.NewGuid().ToString("N")` | Unique identifier. |
| `Title` | `string` | `""` | Display title. |
| `Description` | `string?` | `null` | Optional description. |
| `Start` | `DateTime` | -- | Start date/time. |
| `End` | `DateTime` | -- | End date/time. |
| `IsAllDay` | `bool` | `false` | Whether the appointment spans full days. |
| `Color` | `string?` | `null` | CSS color for the appointment background. |
| `RecurrenceRule` | `string?` | `null` | iCalendar RRULE string. |

### Child View Component Parameters

All child views inherit from `SchedulerViewBase` and share `Label` (`string?`, toolbar button text).

#### SchedulerDayView

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `StartTime` | `TimeSpan` | `08:00` | Visible range start. |
| `EndTime` | `TimeSpan` | `18:00` | Visible range end. |
| `SlotDuration` | `TimeSpan` | `60 min` | Duration of each time slot. |

#### SchedulerWeekView

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `StartTime` | `TimeSpan` | `08:00` | Visible range start. |
| `EndTime` | `TimeSpan` | `18:00` | Visible range end. |
| `SlotDuration` | `TimeSpan` | `60 min` | Duration of each time slot. |
| `FirstDayOfWeek` | `DayOfWeek` | `Sunday` | Which day starts the week. |

#### SchedulerMonthView

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `FirstDayOfWeek` | `DayOfWeek` | `Sunday` | Which day starts the week. |

#### SchedulerMultiDayView

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `NumberOfDays` | `int` | `3` | Number of day columns to show. |
| `StartTime` | `TimeSpan` | `08:00` | Visible range start. |
| `EndTime` | `TimeSpan` | `18:00` | Visible range end. |
| `SlotDuration` | `TimeSpan` | `60 min` | Duration of each time slot. |

#### SchedulerTimelineView

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `SlotDuration` | `TimeSpan` | `30 min` | Duration of each time slot column. |
| `StartTime` | `TimeSpan` | `08:00` | Visible range start. |
| `EndTime` | `TimeSpan` | `18:00` | Visible range end. |
| `NumberOfDays` | `int` | `1` | Number of day rows. |

#### SchedulerAgendaView

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `NumberOfDays` | `int` | `7` | Number of days shown in the agenda list. |


## Next Steps

* [Bind the Scheduler to data](slug:scheduler-appointments-databinding)
* [Configure Scheduler views](slug:scheduler-views-overview)
* [Enable Scheduler editing](slug:scheduler-appointments-edit)
* [Set up resources and grouping](slug:scheduler-resources)


## See Also

* [Live Demo: Scheduler](https://demos.sunfish.dev/blazor-ui/scheduler/overview)
* [Scheduler API Reference](slug:Sunfish.Blazor.Components.SunfishScheduler-1)

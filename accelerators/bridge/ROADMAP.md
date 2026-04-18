# Bridge Accelerator — Roadmap

> **Migration note:** This document was preserved from `Marilo.PmDemo/SETTINGS_STATUS.md`
> during the Phase 9 Sunfish migration. Sections 1–2 (canonical notification pipeline,
> shell footer) and Section 3 (MainLayout) are **DONE**. Sections 4–12 (Account pages,
> shared settings components, services, data model, build order) document the original
> roadmap and are **NOT completed** as part of the migration. These remain as the
> forward work plan for the Bridge accelerator.
>
> The canonical notification pipeline has been promoted to `packages/foundation/Notifications/`
> (see Phase 9, Task 9-5). References to `IUserNotificationService` in this doc now
> resolve to `Sunfish.Foundation.Notifications.IUserNotificationService`.
>
> Demo-only auth seams (`DemoTenantContext`, `MockOktaService`) remain in place with
> explicit annotations and startup warnings; replace before production deployment.

---

# Bridge — Settings Area & Account Pages Status

> Generated from the current design session. Tracks what exists, what's planned, and what the desired end result looks like.

---

## 1. Canonical Notification Pipeline (DONE)

The unified notification architecture is implemented and wired.

### What exists now

| File | Purpose |
|---|---|
| `Sunfish.Bridge.Client/Notifications/UserNotification.cs` | Canonical record + enums (Source, Category, Importance, Delivery) |
| `Sunfish.Bridge.Client/Notifications/IUserNotificationService.cs` | Service interface + `IUserNotificationToastForwarder` seam |
| `Sunfish.Bridge.Client/Notifications/InMemoryUserNotificationService.cs` | In-memory impl with dedupe, seed data (8 PM events), Changed event |
| `Sunfish.Bridge.Client/Notifications/NotificationProjections.cs` | `NotificationFeedProjection` (canonical → bell `NotificationItem`) + `SunfishToastUserNotificationForwarder` (canonical → `NotificationModel` toast) |
| `tests/Sunfish.Tests.Unit/Bridge/UserNotificationServiceTests.cs` | 8 focused tests: create, dedupe, mark read, mark all read, delete all read, toast forwarding, feed projection, critical severity mapping |

### Architecture

```
                ┌──────────────────────────────┐
   any source → │  IUserNotificationService    │ ← single source of truth
                │  (InMemoryUserNotification…) │   • CRUD + read state
                │                              │   • dedupe via CorrelationKey
                │                              │   • Changed event
                └──────────────┬───────────────┘
                               │
                ┌──────────────┴───────────────┐
                ▼                              ▼
  NotificationFeedProjection      IUserNotificationToastForwarder
  (→ NotificationItem for bell)   (→ NotificationModel for toast)
                │                              │
                ▼                              ▼
       SunfishNotificationBell          ISunfishNotificationService
       (sidebar bell / inbox)          (existing toast/snackbar host)
```

- `UserNotification` has no toast-only properties (no CloseAfterMs, Closeable, ThemeColor).
- `NotificationModel` (Sunfish.Core) remains a presentation-only concern, untouched.
- `NotificationItem` (Sunfish.Components.Blazor.Shell) remains the bell's view DTO, populated only via the feed projection mapper.
- The bell component is strictly presentational — it never mutates state, only fires event callbacks.
- `SunfishSnackbarHost` is mounted once in `MainLayout.razor`.

### Seed data sources

Task assigned, due date changed, comment added, risk severity escalated, budget threshold crossed, milestone planning completed, teammate mention, file uploaded.

---

## 2. Shell Footer (DONE — styling iteration may continue)

### What exists now

`SunfishAppShell.razor` parameters added this session:

| Parameter | Type | Purpose |
|---|---|---|
| `UserName` | `string?` | Display name in footer |
| `UserAvatarUrl` | `string?` | Optional avatar image URL |
| `UserEmail` | `string?` | Email shown below name in identity stack |
| `UserBadge` | `string?` | Badge label overlaid on avatar (e.g. "Pro") |
| `NotificationCount` | `int` | Badge count on bell |

Footer default rendering: bordered button group — left side has avatar + identity stack (name + email), right side has bell with divider. Collapsed sidebar shows avatar only.

### SunfishNotificationBell enhancements (DONE)

- "More options" button (···) with tooltip "More options" in the panel header.
- Dropdown menu with "Settings" and "Delete all read" actions.
- `OnSettingsClick` and `OnDeleteAllRead` EventCallbacks added.
- Panel repositioned: `bottom: 0; left: calc(100% + 12px)` — pops out to the right of the sidebar.

---

## 3. MainLayout.razor Current State (DONE)

- Injects `NavigationManager` + `IUserNotificationService`.
- Implements `IDisposable`; unsubscribes from `Changed` in `Dispose()`.
- No local `_notifs` field — all reads go through `UserNotifications.All` → projected to `_feedItems` via `NotificationFeedProjection.ToFeedItem`.
- Badge count reads `UserNotifications.UnreadCount`.
- Bell wired: `OnItemClick` → `MarkReadAsync`, `OnMarkAllRead` → `MarkAllReadAsync`, `OnDeleteAllRead` → `DeleteAllReadAsync`, `OnSettingsClick` → `Nav.NavigateTo("/account/details")`.
- `<SunfishSnackbarHost />` mounted in `ChildContent`.
- User menu items: Profile, Settings (⌘,), Theme (expandable), Help & docs, Keyboard shortcuts (?), Sign out.

---

## 4. Account Pages — Current vs Desired

### What exists now

| Route | File | Status |
|---|---|---|
| `/account/details` | `Pages/AccountDetails.razor` | Stub — inline styles, hardcoded data, no service binding |

No other account pages exist. No `SettingsLayout`, `SettingsNav`, or shared settings components exist.

### Desired end result — pages

| Route | Page | Purpose | Priority |
|---|---|---|---|
| `/account/details` | AccountDetails | Profile, email, title, timezone, locale, plan, sessions, sign-out, delete | MVP |
| `/account/preferences` | PreferencesPage | Theme (System/Light/Dark), density, default home, default project view, date format, week start, accessibility | MVP |
| `/account/personalization` | PersonalizationPage | Work profile, department, focus hours, preferred projects/teams, followed items, AI context bio | Later |
| `/account/assistant` | AssistantPage | AI tone, response length, task breakdown depth, recap cadence, risk sensitivity, context sources, live preview | Later |
| `/account/shortcuts` | ShortcutsPage | Keyboard shortcuts table, key recorder, conflict detection, quick actions, reset defaults | Later |
| `/account/notifications` | NotificationsPage | Channel × event matrix, quiet hours, digest cadence, per-project overrides | MVP |
| `/account/connectors` | ConnectorsPage | Slack, Teams, GitHub, Jira, Calendar, webhook — status, auth, scopes, last sync | Later |
| `/account/security` | SecurityPage | Sessions, 2FA, audit log | Stretch |
| `/account/billing` | BillingPage | Plan, seats, invoices | Stretch |
| `/account/workspace` | WorkspacePage | Org-level admin settings | Stretch |

### Desired end result — layout

`SettingsLayout.razor` renders only the inner settings frame (not SunfishAppShell). Declares `@layout MainLayout` so the outer shell renders once. Contains:

- `SettingsNav` — left rail (~240px) with NavLink items grouped under "You" / "Workspace" / "Plan" headings.
- Content panel to the right for `@Body`.
- Each account page declares `@layout SettingsLayout`.

---

## 5. Shared Settings Components — Desired

### Settings chrome (net-new, demo-level)

| Component | Purpose | Based on |
|---|---|---|
| `SettingsCard` | Titled card with description, content slot, optional footer/actions | Thin wrapper around existing `SunfishCard` + `SunfishCardHeader` + `SunfishCardBody` |
| `SettingsHeader` | Page-level title + description + optional actions slot | Simple component |
| `DangerZone` | Red-bordered card for destructive actions (deactivate, delete) | `SettingsCard` variant |

### Input components — existing Sunfish library (USE, don't rebuild)

| Need | Existing component | Path |
|---|---|---|
| Text input | `SunfishTextBox` | `Forms/Inputs/SunfishTextBox.razor` |
| Text area | `SunfishTextArea` | `Forms/Inputs/SunfishTextArea.razor` |
| Select/dropdown | `SunfishSelect` | `Forms/Inputs/SunfishSelect.razor` |
| Toggle switch | `SunfishSwitch` | `Forms/Inputs/SunfishSwitch.razor` |
| Checkbox | `SunfishCheckbox` | `Forms/Inputs/SunfishCheckbox.razor` |
| Slider | `SunfishSlider` | `Forms/Inputs/SunfishSlider.razor` |
| Range slider | `SunfishRangeSlider` | `Forms/Inputs/SunfishRangeSlider.razor` |
| Segmented toggle | `SunfishSegmentedControl` | `Buttons/SunfishSegmentedControl.razor` |
| Chip multi-select | `SunfishChipSet` + `SunfishChip` | `Buttons/SunfishChipSet.razor` |
| Time range | `SunfishTimeRangeSelector` | `Navigation/SunfishTimeRangeSelector.razor` (verify fit for quiet-hours) |

### Form containers — existing Sunfish library (USE)

| Component | Purpose |
|---|---|
| `SunfishField` | Wraps input + label + validation state |
| `SunfishLabel` | Standalone label |
| `SunfishForm` | EditContext wrapper |
| `SunfishValidation` / `SunfishValidationMessage` / `SunfishValidationSummary` | Validation display |

### Toast/dialog — existing Sunfish library (USE)

| Component | Purpose |
|---|---|
| `SunfishSnackbarHost` | Toast host (already mounted in MainLayout) |
| `ISunfishNotificationService.ShowToast(...)` | Trigger toasts from settings save actions |
| `SunfishDialog` / `SunfishConfirmDialog` | Confirm destructive actions (deactivate account, delete data) |
| `SunfishDrawer` | Slide-over for connector config |

### Net-new upstream components

| Component | Purpose | Priority |
|---|---|---|
| `SunfishKeyRecorder` | Captures keyboard chord for shortcut rebinding | When Shortcuts page is built |

---

## 6. Services — Current vs Desired

### Already registered (server Program.cs)

| Service | Registration | Source |
|---|---|---|
| `IUserNotificationService` → `InMemoryUserNotificationService` | Scoped | This session |
| `IUserNotificationToastForwarder` → `SunfishToastUserNotificationForwarder` | Scoped | This session |
| `ISunfishThemeService` → `ThemeService` | Scoped | `AddSunfishCoreServices()` via `UseFluentUI()` |
| `ISunfishNotificationService` → `SunfishNotificationService` | Scoped | `AddSunfishCoreServices()` via `UseFluentUI()` |
| `ISunfishCssProvider` → `FluentUICssProvider` | Scoped | `UseFluentUI()` |

### Needed (not yet created)

| Service | Purpose | When |
|---|---|---|
| `ICurrentUserContext` / `DemoCurrentUserContext` | Mock user identity, plan tier, admin flag. All plan/admin gating routes through this. Annotated "DEMO ONLY". | Step 2 (before any settings page touches permissions) |
| `IAccountService` | Profile CRUD, sessions, account deletion | AccountDetails page |
| `IPreferencesService` | Theme mode, density, defaults, a11y flags; live-applies via `ISunfishThemeService` | PreferencesPage |
| `INotificationPreferencesService` | Channel × event matrix, quiet hours, digest, per-project overrides | NotificationsPage |
| `IPersonalizationService` | Work profile, focus hours, followed items, dashboard widgets | PersonalizationPage |
| `IAssistantSettingsService` | Tone, length, recap, risk sensitivity, context sources | AssistantPage |
| `IShortcutsService` | Keybinding CRUD, conflict detection, Ctrl+, reservation | ShortcutsPage |
| `IConnectorService` | Connector catalog, auth lifecycle, sync status | ConnectorsPage |

All will be backed by in-memory mock implementations for the demo, structured so a DAB/GraphQL-backed implementation can slot in via the same interface.

---

## 7. Data Model — Desired

### Transport contracts (DTOs, for eventual DAB/GraphQL parity)

To live in `Models/Settings/Dtos/`. Dedicated files per domain area.

### Page-facing view models

To live in `Models/Settings/ViewModels/`. Carry dirty-tracking, validation attributes, UI-only fields. Mapped from DTOs via static mappers in `Services/Settings/Mapping/SettingsMappers.cs`.

### DAB schema entities needed

| Table | Scope | When |
|---|---|---|
| `user_preferences` | User | PreferencesPage build |
| `notification_preferences` | User | NotificationsPage build |
| `notification_project_overrides` | User × Project | NotificationsPage build |
| `user_personalization` | User | PersonalizationPage build |
| `user_assistant_settings` | User | AssistantPage build |
| `user_shortcuts` | User | ShortcutsPage build |
| `connectors_personal` / `connectors_workspace` | User / Org | ConnectorsPage build |

---

## 8. Implementation Guardrails (Agreed)

1. **Nested layouts**: `MainLayout` is the only component that renders `SunfishAppShell`. `SettingsLayout` renders only its inner frame.
2. **Shared state via services**: Theme and notification state live in scoped services. Consumers implement `IDisposable` and unsubscribe in `Dispose`.
3. **Demo auth seams**: `ICurrentUserContext` annotated "DEMO ONLY" with explicit replacement guidance.
4. **DTO vs VM separation**: Transport DTOs in dedicated files, mapped to page VMs. UI never binds to DAB/GraphQL types directly.
5. **Sunfish-native inputs only**: If a form control is missing, create a new Sunfish component upstream rather than pulling in another library.
6. **Ctrl+, / Cmd+,**: Reserved globally for opening settings.
7. **Serilog + OTEL + DAB**: Confirmed as the intended stack. No drift.

---

## 9. Build Order

| Step | Scope | Status |
|---|---|---|
| 1. Upstream component audit | Confirmed 7 of 9 inputs exist; only SunfishKeyRecorder is net-new; SunfishTimeRangeSelector needs quiet-hours fit check | **DONE** (audit) |
| 2. Shared infra | `ICurrentUserContext`, `INotificationFeedService` → replaced by canonical `IUserNotificationService`, toast host mount, MainLayout refactor | **DONE** (canonical pipeline) |
| 3. DAB migrations | `user_preferences`, `notification_preferences` tables + DAB entity config | Pending |
| 4. Settings shell | `SettingsLayout`, `SettingsNav`, `SettingsCard`, `SettingsHeader`, `DangerZone` | Pending |
| 5. Account page | `/account/details` — profile, locale, plan, sessions, danger zone | Pending (stub exists) |
| 6. Preferences page | `/account/preferences` — live theme broadcast via `ISunfishThemeService` | Pending |
| 7. Notifications page | `/account/notifications` — matrix, quiet hours; shares `IUserNotificationService` with bell | Pending |
| 8. Connectors page | Card grid + SunfishDrawer config | Pending |
| 9. Assistant page | Live preview pane | Pending |
| 10. Personalization page | Followed items, focus hours, dashboard widgets | Pending |
| 11. Shortcuts page | SunfishKeyRecorder + conflict detection | Pending |
| 12. Stretch | Security, Billing, Workspace admin | Pending |

---

## 10. Remaining UI using temporary projection vs canonical model

| Surface | Model used | Source of truth | Status |
|---|---|---|---|
| Sidebar bell (SunfishNotificationBell) | `NotificationItem` (Shell view DTO) | Projected from `IUserNotificationService.All` via `NotificationFeedProjection.ToFeedItem` | Clean — no local state |
| Bell badge count | `int` from `IUserNotificationService.UnreadCount` | Canonical service | Clean |
| SunfishAppShell `NotificationCount` param | `int` passed from MainLayout | Reads `UserNotifications.UnreadCount` | Clean |
| Toast display | `NotificationModel` (Sunfish.Core) | Forwarded from canonical via `SunfishToastUserNotificationForwarder` | Clean — presentation only |
| `/account/details` stub page | Hardcoded inline HTML | No service binding | **Temporary** — will be replaced when Account page is fully built (step 5) |
| User menu items | `List<PopupMenuItem>` in MainLayout | Private field, no service | **Temporary** — menu item click handlers (Settings, Profile, Sign out) need wiring to nav/auth in later steps |

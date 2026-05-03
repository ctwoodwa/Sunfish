# Feedback Components - Gap Analysis (Part 1)

> Generated: 2026-03-30

## 1. SunfishAlert.razor vs Notification Spec

The spec describes a `SunfishNotification` component with imperative `Show()`/`Hide()`/`HideAll()` methods and a `NotificationModel` class. `SunfishAlert` is a declarative inline alert -- a fundamentally different pattern. This is intentional (alert vs toast notification), but there are gaps within what SunfishAlert does implement.

| Gap | Severity | Detail |
|-----|----------|--------|
| No animation support | **[Medium]** | Spec defines `AnimationType` (Fade) and `AnimationDuration` (300ms). Alert has neither. |
| No auto-close / `CloseAfter` | **[Low]** | Spec's `NotificationModel.CloseAfter` (5000ms default). Not relevant for inline alerts but could be useful. |
| No `ThemeColor` parameter | **[Medium]** | Spec uses `ThemeColor` string. Alert uses `AlertSeverity` enum instead -- different API surface. |
| No icon support (`ShowIcon`/`Icon`) | **[Medium]** | Spec supports icon rendering per notification. Alert has no icon slot. |
| Missing `role` on dismiss button | **[Low]** | Dismiss button has no aria-label for accessibility. |
| No stacking support | **[Low]** | Spec supports stacked notifications. Alert is single-instance. |

**Implemented:** `Severity`, `ChildContent`, `IsDismissible`, `OnDismiss`, `role="alert"`, base class params.

---

## 2. SunfishAlertStrip.razor (No Spec)

Custom component -- no spec equivalent. Renders a list of `AlertItem` objects with Title/Detail/Module.

**Implemented parameters:**
- `Alerts` (`IReadOnlyList<AlertItem>`) -- conditionally renders when non-empty
- Uses `CssProvider.AlertStripClass()` and `CssProvider.AlertClass(severity)`
- Per-item rendering of Title (strong), Detail (span), Module (span with dedicated class)

**Observations:**
| Item | Severity | Detail |
|------|----------|--------|
| No dismiss/close per item | **[Medium]** | Individual alerts cannot be dismissed. |
| No `role="alert"` on container or items | **[Medium]** | Missing ARIA semantics for accessibility. |
| No animation for enter/exit | **[Low]** | Items appear/disappear without transition. |

---

## 3. SunfishCallout.razor (No Spec)

Custom informational callout with icon and content. No spec to compare against.

**Implemented parameters:**
- `Type` (`CalloutType` enum: Info, Warning, Danger, Success, Note)
- `Title` (string?)
- `ChildContent` (RenderFragment?)
- Auto-selects icon via `IconProvider.GetIcon()` based on type

**Observations:**
| Item | Severity | Detail |
|------|----------|--------|
| No dismissible option | **[Low]** | Callouts are static; no close/dismiss support. |
| No `role` attribute | **[Medium]** | Should have `role="note"` or `role="alert"` depending on type. |
| No custom icon override | **[Low]** | Icon is always derived from `Type`; no parameter to override. |

---

## 4. SunfishConfirmDialog.razor vs Dialog Spec

Specialized confirm dialog. Compared to the full Dialog spec:

| Gap | Severity | Detail |
|-----|----------|--------|
| No `Visible`/two-way binding | **[High]** | Spec uses `Visible` with `@bind-Visible`. Implementation uses `IsOpen` (one-way only). |
| No `DialogContent` RenderFragment | **[Medium]** | Spec uses `DialogContent` slot. Implementation uses a plain `Message` string -- no rich content. |
| No `DialogButtons` RenderFragment | **[Medium]** | Spec allows custom button composition. Implementation hardcodes Confirm/Cancel. |
| No `ShowCloseButton` | **[Low]** | Spec has titlebar close button toggle. Not present. |
| No `CloseOnOverlayClick` param | **[Medium]** | Overlay click always cancels; no way to disable this behavior. |
| No `ButtonsLayout` | **[Low]** | Spec supports `Stretch` layout option for buttons. |
| No `ThemeColor` | **[Low]** | Spec supports themed titlebars. |
| No `Width`/`Height` | **[Medium]** | Spec supports dimension parameters. Not exposed. |
| No `FocusedElementSelector` | **[Low]** | Spec supports initial focus control. |
| No `Refresh()` method | **[Low]** | Spec provides imperative refresh. |

**Implemented:** `IsOpen`, `Title`, `Message`, `ConfirmText`, `CancelText`, `IsDangerous`, `OnConfirm`, `OnCancel`, overlay with stopPropagation.

---

## 5. SunfishDataBanner.razor (No Spec)

Domain-specific banner for pending data changes. No spec equivalent.

**Implemented parameters:**
- `PendingChanges` (`IReadOnlyList<DataChangeInfo>`)
- `OnRefreshRequested` (EventCallback)
- `OnDismiss` (EventCallback)
- Displays count of pending changes with Refresh and Dismiss buttons

**Observations:**
| Item | Severity | Detail |
|------|----------|--------|
| No details expansion | **[Low]** | Shows count only; no way to inspect individual changes. |
| Hardcoded button text | **[Low]** | "Refresh" and "Dismiss" are not customizable. |
| No severity/type styling | **[Low]** | Uses a single static class; no visual urgency levels. |
| No `role` or ARIA attributes | **[Medium]** | Should have `role="status"` or `role="alert"`. |

---

## 6. SunfishDataToast.razor (No Spec)

Domain-specific toast list for data change notifications. No spec equivalent.

**Implemented parameters:**
- `Changes` (`IReadOnlyList<DataChangeInfo>`)
- `MaxVisible` (int, default 5)
- `OnDismiss` (`EventCallback<DataChangeInfo>`)
- Renders up to `MaxVisible` items, each with summary text and dismiss button

**Observations:**
| Item | Severity | Detail |
|------|----------|--------|
| No auto-dismiss / timeout | **[Medium]** | Toasts persist until manually dismissed. |
| No enter/exit animation | **[Medium]** | Items appear/disappear abruptly. |
| No positioning control | **[Low]** | Always renders inline; no top/bottom/left/right positioning. |
| No ARIA live region | **[Medium]** | Should use `aria-live="polite"` for screen readers. |

---

## 7. SunfishDialog.razor vs Dialog Spec

The closest match to the spec. Covers more ground than SunfishConfirmDialog.

| Gap | Severity | Detail |
|-----|----------|--------|
| No `Visible`/two-way binding | **[High]** | Spec uses `@bind-Visible`. Implementation uses `IsOpen` (one-way). |
| No `DialogContent` named RenderFragment | **[Medium]** | Spec uses `DialogContent` slot. Implementation uses generic `ChildContent`. |
| No `DialogButtons` RenderFragment | **[Medium]** | Spec allows fully custom buttons via RenderFragment. Implementation uses `DialogButtons` enum with predefined sets only. |
| No `ShowCloseButton` | **[Medium]** | Spec has titlebar close button. Not rendered. |
| No `CloseOnOverlayClick` param | **[Medium]** | Overlay click always closes when `Modal=true`; not configurable. |
| No `ThemeColor` | **[Low]** | Spec supports themed titlebars. |
| No `ButtonsLayout` | **[Low]** | Spec supports button layout (Stretch). |
| No `FocusedElementSelector` | **[Low]** | No initial focus management. |
| No `Refresh()` method | **[Low]** | Spec exposes imperative Refresh for content updates. |
| `Draggable` not in spec | **[Low]** | Implementation adds `Draggable` param; spec says "use Window for drag/resize." Intentional divergence. |

**Implemented:** `IsOpen`, `Title`, `ChildContent`, `OnClose`, `Width`, `Height`, `Draggable`, `Modal`, `Buttons` (enum: None/Ok/OkCancel/YesNo/YesNoCancel/RetryCancel), `OnButtonClick` (DialogResult), overlay with stopPropagation.

---

## Summary

| Component | Spec Match | Critical Gaps |
|-----------|-----------|---------------|
| SunfishAlert | Partial (different pattern) | No icons, no animation, no ThemeColor |
| SunfishAlertStrip | No spec | Missing ARIA, no dismiss per item |
| SunfishCallout | No spec | Missing ARIA roles |
| SunfishConfirmDialog | Weak | No two-way `Visible` binding, string-only content, no dimensions |
| SunfishDataBanner | No spec | Missing ARIA, hardcoded text |
| SunfishDataToast | No spec | No auto-dismiss, no animation, missing ARIA live region |
| SunfishDialog | Moderate | No two-way `Visible` binding, no close button, no custom button RenderFragment |

**Top priority fixes (High severity):**
1. Both `SunfishDialog` and `SunfishConfirmDialog` should adopt `Visible` with two-way binding (`@bind-Visible`) per the Dialog spec, replacing the one-way `IsOpen` parameter.

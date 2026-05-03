# Gap Analysis Part 2: Feedback Components

Base class (`SunfishComponentBase`) provides: `Class` (string?), `Style` (string?), `AdditionalAttributes`.

---

## 1. SunfishProgressBar.razor vs `progressbar/overview.md`

| Area | Spec | Implementation | Severity |
|------|------|----------------|----------|
| `Max` parameter | `double`, default `100` | **Missing** -- value is hardcoded as percentage | **[High]** |
| `Orientation` parameter | `ProgressBarOrientation` enum (`Horizontal`/`Vertical`) | **Missing** | **[Medium]** |
| Label support | Spec describes label showing progress value with `%`, plus label template/position customization | **Missing** -- no label rendered at all | **[High]** |
| `Value` parameter | `double` | Implemented | OK |
| Indeterminate state | Supported via slug reference | Implemented via `IsIndeterminate` (spec uses indeterminate slug, not a direct parameter) | OK |
| ARIA attributes | Not specified but expected | **Missing** -- no `role="progressbar"`, no `aria-valuenow/min/max` | **[High]** |

---

## 2. SunfishProgressCircle.razor vs `loader/overview.md`

The spec describes a generic `SunfishLoader` component; the implementation is a circular progress indicator. These are architecturally different components.

| Area | Spec | Implementation | Severity |
|------|------|----------------|----------|
| Component name | `SunfishLoader` | `SunfishProgressCircle` -- different concept | **[Low]** (intentional split) |
| `Visible` parameter | `bool`, default `true` | **Missing** -- always renders | **[Medium]** |
| `Type` parameter | `LoaderType` enum (Pulsing, ConvergingSpinner, etc.) | **Missing** -- only circular SVG | **[Low]** (different scope) |
| `ThemeColor` parameter | `string`, default `"primary"` | **Missing** | **[Medium]** |
| `Size` parameter | `string` (`"md"`) | Implemented as `int` (pixel value, default 48) -- different API shape | **[Low]** |
| `StrokeWidth` parameter | Not in spec | Implemented (extra feature) | OK |
| `IsIndeterminate` parameter | Not in spec | Implemented | OK |
| ARIA attributes | Not specified | Implemented (`role="progressbar"`, `aria-valuenow/min/max`) | OK |

---

## 3. SunfishSkeleton.razor vs `skeleton/overview.md`

| Area | Spec | Implementation | Severity |
|------|------|----------------|----------|
| `ShapeType` parameter | `SkeletonShapeType` enum, default `Text` | Implemented as `Variant` (`SkeletonVariant`), default `Text` -- **name mismatch** | **[Medium]** |
| `AnimationType` parameter | `SkeletonAnimationType` enum, default `Pulse` | **Missing** | **[High]** |
| `Visible` parameter | `bool`, default `true` | **Missing** -- always renders | **[Medium]** |
| `Width` parameter | `string` | Implemented | OK |
| `Height` parameter | `string` | Implemented | OK |

---

## 4. SunfishSnackbar.razor -- NO SPEC

Implemented features (standalone snackbar):
- `Message` (string) -- text content
- `IsOpen` (bool) -- visibility toggle
- `ActionText` (string?) -- optional action button
- `OnAction` (EventCallback) -- action button callback
- `OnDismiss` (EventCallback) -- dismiss callback
- `ClosedCallback` (EventCallback) -- closed callback (redundant with OnDismiss?)
- `ContentTemplate` (RenderFragment?) -- custom content
- `DurationMs` (int, default 3000) -- auto-dismiss timer
- Auto-dismiss via `System.Timers.Timer`
- ARIA: `role="status"`, `aria-live="polite"`
- Always shows dismiss button (no `Closable` toggle)

**Observations:**
- **[Low]** `ClosedCallback` and `OnDismiss` both fire on dismiss -- potential API confusion
- **[Low]** No severity/theme color parameter

---

## 5. SunfishSnackbarHost.razor -- NO SPEC

Implemented features (notification host/container):
- Injects `ISunfishNotificationService` for centralized notification management
- `VerticalPosition` (NotificationVerticalPosition, default Bottom)
- `HorizontalPosition` (NotificationHorizontalPosition, default Right)
- `MaxCount` (int, default 5) -- max visible notifications
- `AnimationType` (NotificationAnimation: Fade, SlideIn, None)
- `AnimationDurationMs` (int, default 300)
- Auto-dismiss per notification via `CloseAfterMs`
- Closeable dismiss button per notification
- Proper cleanup of timers and event subscriptions

**Observations:**
- **[Low]** Uses both `CssProvider.SnackbarClass()` and `CssProvider.ToastClass()` -- mixed naming
- Component is well-structured with proper `Dispose` pattern

---

## 6. SunfishSpinner.razor vs `loader/overview.md`

The spec describes `SunfishLoader`; this is a simpler spinner variant.

| Area | Spec | Implementation | Severity |
|------|------|----------------|----------|
| `Visible` parameter | `bool`, default `true` | **Missing** -- always renders | **[Medium]** |
| `Size` parameter | `string` (`"md"`) | Implemented as `SpinnerSize` enum (default `Medium`) -- different type but equivalent | OK |
| `ThemeColor` parameter | `string`, default `"primary"` | **Missing** | **[Medium]** |
| `Type` parameter | `LoaderType` enum | **Missing** (single animation type only) | **[Low]** |
| ARIA attributes | Not specified | **Missing** -- no `role` or `aria-label` | **[Medium]** |

---

## 7. SunfishToast.razor vs `notification/overview.md`

The spec describes `SunfishNotification` with a service-oriented `Show/Hide` API; the implementation is a simple presentational component.

| Area | Spec | Implementation | Severity |
|------|------|----------------|----------|
| Architecture | Imperative API via `@ref` + `Show()`/`Hide()`/`HideAll()` methods | Declarative component with parameters -- **fundamentally different pattern** | **[High]** |
| `NotificationModel` | Rich model: `ThemeColor`, `Closable`, `CloseAfter`, `ShowIcon`, `Icon`, `Text` | Only `Severity` enum and `Message` string | **[High]** |
| `AnimationType` parameter | `AnimationType` enum, default `Fade` | **Missing** | **[Medium]** |
| `AnimationDuration` parameter | `int`, default `300` | **Missing** | **[Low]** |
| `VerticalPosition` parameter | `NotificationVerticalPosition`, default `Bottom` | **Missing** (handled by SnackbarHost instead) | **[Medium]** |
| `HorizontalPosition` parameter | `NotificationHorizontalPosition`, default `Right` | **Missing** (handled by SnackbarHost instead) | **[Medium]** |
| Stacking support | Multiple notifications via `Show()` | **Missing** -- single toast only | **[High]** |
| Icon support | `ShowIcon` + `Icon` properties | **Missing** | **[Medium]** |
| Close button | `Closable` property, default `true` | Always shows close button, no toggle | **[Low]** |
| Auto-close | `CloseAfter`, default `5000ms` | **Missing** -- requires manual dismiss | **[Medium]** |
| ARIA attributes | Not specified | **Missing** -- no `role` or `aria-live` | **[Medium]** |

**Note:** The spec's notification functionality is partially covered by `SunfishSnackbarHost` (stacking, positioning, auto-close, service integration) rather than `SunfishToast`. The Toast component appears to be a low-level building block.

---

## Summary

| Component | High | Medium | Low | Status |
|-----------|------|--------|-----|--------|
| SunfishProgressBar | 3 | 1 | 0 | Significant gaps (Max, Label, ARIA) |
| SunfishProgressCircle | 0 | 2 | 3 | Intentionally different from Loader spec |
| SunfishSkeleton | 1 | 2 | 0 | Missing AnimationType and Visible |
| SunfishSnackbar | 0 | 0 | 2 | No spec (functional) |
| SunfishSnackbarHost | 0 | 0 | 1 | No spec (well-implemented) |
| SunfishSpinner | 0 | 3 | 1 | Missing Visible, ThemeColor, ARIA |
| SunfishToast | 3 | 4 | 1 | Major architectural divergence from spec |

**Top priorities:** SunfishProgressBar (add `Max`, label, ARIA) and SunfishToast (decide whether spec's imperative API is needed or if SnackbarHost covers it).

# Feedback family — spec verification (ADR 0022 Tier 1)

**Date:** 2026-04-21
**Scope:** Blazor feedback components vs `apps/docs/component-specs/*` where `category == "feedback"` and `status ∈ {implemented, partial}`.
**Audited entries:** `loader` (implemented), `loadercontainer` (partial), `notification` (implemented), `progressbar` (implemented), `chunkprogressbar` (partial), `skeleton` (implemented) — 6 components.
**Excluded:** `dialog` and `alert`/`snackbar` are not `category="feedback"` in `component-mapping.json` (dialog=`layout`; no alert/snackbar entries at all). Skipped per scope.

---

## Summary

| Spec dir           | Sunfish impl file                                       | Demo present                        | Gaps (miss/bug/incomplete) | Verdict                  |
|--------------------|----------------------------------------------------------|-------------------------------------|----------------------------|--------------------------|
| loader             | `SunfishLoader.razor`                                    | Overview only                       | 1/0/3                      | needs-work               |
| loadercontainer    | `SunfishLoaderContainer.razor`                           | Overview only                       | 3/1/4                      | needs-work (stay partial)|
| notification       | `SunfishSnackbarHost.razor` (+ `ISunfishNotificationService`) | Placeholder stub only          | 2/0/5                      | downgrade-to-partial     |
| progressbar        | `SunfishProgressBar.razor`                               | Overview + Accessibility + Appearance| 2/0/2                      | needs-work               |
| chunkprogressbar   | `SunfishChunkProgressBar.razor`                          | Overview only                       | 0/1/2                      | needs-work (stay partial)|
| skeleton           | `SunfishSkeleton.razor`                                  | Overview + Appearance               | 2/0/1                      | needs-work               |

Verdicts tally: **verified 0 · needs-work 5 · downgrade-to-partial 1**.

---

## loader

**Verdict:** needs-work.
**Impl:** `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Feedback\Loader\SunfishLoader.razor`
**Demos:** `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Feedback\Loader\Overview\Demo.razor` (Overview only). Demo links `/loader/types-and-sizes` and `/loader/appearance` but those pages do not exist.
**Spec files reviewed:** `loader/overview.md`, `loader/appearance.md`, `loader/toc.yml`.

| Gap | Severity | Detail |
|-----|----------|--------|
| `Type` parameter declared but not rendered | bug | `SunfishLoader.razor` exposes `LoaderType Type` but the template always delegates to `SunfishSpinner`, which has no `Type`. The three spec values (`Pulsing`, `InfiniteSpinner`, `ConvergingSpinner`) are not visually distinguishable. |
| `Size` type mismatch with spec | incomplete | Spec: `Size` is `string` keyed off `ThemeConstants.Loader.Size.{Small,Medium,Large}`. Impl: `Size` is `SpinnerSize` enum. Functionally equivalent tier but not spec-shaped; no `ThemeConstants.Loader.Size` static exists. |
| `ThemeColor` parameter shape | incomplete | Spec: string driving a `k-loader-<value>` class for Primary/Secondary/Tertiary/Success/Info/Warning/Error/Dark/Light/Inverse. Impl: accepts a raw colour string and emits it as inline `style="color: ..."` on the text only; the indicator colour does not follow `ThemeColor`, and no `ThemeConstants.Loader.ThemeColor` is exposed. |
| `ChildContent` overlay behaviour | incomplete | Spec does not describe `ChildContent`; impl adds an overlay mode which is not documented (not a bug, but drift worth reconciling — the LoaderContainer is the documented overlay surface). |
| Appearance + Types demo pages | missing | Spec has an Appearance article; demo tree has only `Overview`. Narrative already advertises `/types-and-sizes` and `/appearance` routes that 404. |

---

## loadercontainer

**Verdict:** needs-work (retain `partial` status).
**Impl:** `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Feedback\Loader\SunfishLoaderContainer.razor`
**Demos:** `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Feedback\LoaderContainer\Overview\Demo.razor` (Overview only). Demo links `/loadercontainer/panel` and `/loadercontainer/events` that do not exist.
**Spec files reviewed:** `loadercontainer/overview.md`, `loadercontainer/appearance.md`, `loadercontainer/template.md`, `loadercontainer/toc.yml`.

| Gap | Severity | Detail |
|-----|----------|--------|
| `Visible` parameter name | bug | Spec parameter is `Visible` (`bool`, default `true`). Impl uses `Loading` (no default). This is a direct API-name mismatch against the spec. |
| `LoaderPosition` parameter | missing | Spec: `LoaderPosition` enum (`Top` default / `Start` / `End`). Impl: no such parameter; the template hard-codes vertical stacking. |
| `OverlayThemeColor` | missing | Spec: string (`"dark"` / `"light"` / null). Impl: no parameter — CSS only. |
| `ThemeColor` parameter | missing | Spec: text + animation colour. Impl: no parameter. |
| `LoaderType` passthrough | incomplete | Spec exposes `LoaderType` on the container for the non-template branch. Impl always delegates to `SunfishSpinner` with no type choice. |
| Template parameter name | incomplete | Spec: `<Template>` render fragment. Impl: `LoaderTemplate` render fragment — name drift. Spec also requires that the default panel be suppressed when a template is provided; impl achieves this implicitly but no test covers it. |
| Appearance + Template demo pages | missing | Spec has Appearance and Template articles; demo has only Overview. Narrative advertises `/panel` and `/events` routes that do not exist. |
| `Size` type mismatch | incomplete | Same shape mismatch as Loader (string vs `SpinnerSize` enum). |

---

## notification

**Verdict:** downgrade-to-partial (currently listed `implemented`).
**Impl:** Mapping says `SunfishToast`, but the spec describes a reference-per-instance component with `@ref.Show(...)`. The actual stack that covers the spec is `SunfishSnackbarHost.razor` + `ISunfishNotificationService` (`C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Feedback\Snackbar\SunfishSnackbarHost.razor`, plus `SunfishToast.razor`/`SunfishSnackbar.razor`). No component named `SunfishNotification` exists.
**Demos:** `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Feedback\Notification\Overview\Demo.razor` is a placeholder ("overview demo coming soon"); no working example.
**Spec files reviewed:** `notification/overview.md`, `notification/appearance.md`, `notification/open-close-hide.md`, `notification/stacked-notifications.md`, `notification/templates.md`, `notification/accessibility/wai-aria-support.md`.

| Gap | Severity | Detail |
|-----|----------|--------|
| `SunfishNotification` component + `Show/Hide/HideAll` ref API | missing | Spec-documented usage (`@ref.Show(new NotificationModel { ... })`) has no corresponding public component. The service-based host does not match the spec's per-instance reference model, and the mapping's `SunfishToast` is a stateless presentational card, not a dispatcher. |
| `ThemeColor`/severity parity | incomplete | Spec names Primary/Secondary/Tertiary/Success/Info/Warning/Error/Dark/Light/Inverse. Impl uses `ToastSeverity` enum on `SunfishToast` and an arbitrary string `ThemeColor` on `NotificationModel` rendered as inline `background-color` — not the `k-notification-<ThemeColor>` class the spec advertises. |
| `AnimationType` (13 options) | incomplete | Spec accepts 13 `AnimationType` values. Host exposes `NotificationAnimation { None, Fade, SlideIn }` only. |
| `VerticalPosition` / `HorizontalPosition` | covered | `SunfishSnackbarHost` exposes both with matching enum names (verified against the spec table). |
| `Closable`, `CloseAfter`, `ShowIcon`, `Icon` on model | incomplete | Spec's `NotificationModel` includes `Closable`, `CloseAfter`, `ShowIcon`, `Icon`. Host renders `Closeable` and `CloseAfterMs`; icon fields are not wired into the rendered DOM. |
| Stacked notifications from different refs | incomplete | Spec: multiple refs produce independent stacks. Impl: single service, single host, shared stack. |
| Templates (`<Template>` with `context` = `NotificationModel`) | missing | No templating surface on the host; the host hard-codes message + dismiss button. |
| `role="alert"` + `aria-label`/`aria-labelledby` | bug | Spec requires `role="alert"` on the notification element and an accessible name via `aria-label`/`aria-labelledby`. Host emits `role="status"` (weaker urgency) and sets no accessible name. Accessibility drift. |
| Working demo | missing | Only a placeholder demo exists; no example demonstrates calling `Show/Hide/HideAll` or the model. |

---

## progressbar

**Verdict:** needs-work.
**Impl:** `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Feedback\Progress\SunfishProgressBar.razor`
**Demos:** `Overview/Demo.razor`, `Accessibility/Demo.razor`, `Appearance/Demo.razor` under `apps/kitchen-sink/Pages/Components/Feedback/ProgressBar/`.
**Spec files reviewed:** `progressbar/overview.md`, `progressbar/label.md`, `progressbar/indeterminate.md`, `progressbar/accessibility/wai-aria-support.md`, `progressbar/accessibility/toc.yml`.

| Gap | Severity | Detail |
|-----|----------|--------|
| `Indeterminate` parameter name | incomplete | Spec: `Indeterminate` (`bool`). Impl: `IsIndeterminate`. Minor naming drift vs the spec contract. |
| `Orientation` parameter | missing | Spec: `ProgressBarOrientation` enum (`Horizontal` default, `Vertical`). Impl: no orientation parameter; vertical bars are not supported. |
| `<ProgressBarLabel>` child component with `Visible`/`Position`/`<Template>` | missing | Spec defines a nested `<ProgressBarLabel Position="..."><Template>@context.Value</Template></ProgressBarLabel>` API. Impl exposes only `ShowLabel` + `Label` string; no positional control, no template, no `Value` context. `ProgressBarLabelPosition` enum also missing. |
| ARIA surface | covered | `role="progressbar"`, `aria-valuemin=0`, `aria-valuemax=Max`, `aria-valuenow` (omitted when indeterminate), `aria-label=Label` all present — matches spec WAI-ARIA table. |
| `Label` demo page | missing | Narrative advertises `/progressbar/label` and `/progressbar/events` that are not in the demo tree. |

---

## chunkprogressbar

**Verdict:** needs-work (retain `partial`).
**Impl:** `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Feedback\Progress\SunfishChunkProgressBar.razor`
**Demos:** Overview only.
**Spec files reviewed:** `chunkprogressbar/overview.md`, `chunkprogressbar/indeterminate.md`, `chunkprogressbar/accessibility/wai-aria-support.md`.

| Gap | Severity | Detail |
|-----|----------|--------|
| `Indeterminate` default | bug | Spec parameter table: `Indeterminate` default `true`. Impl: default `false`. (Spec table appears to itself be wrong but it is the canonical text; either update the spec or the default.) |
| `Orientation` enum type | incomplete | Spec: `ProgressBarOrientation`. Impl: `SliderOrientation`. Wrong enum; no `ProgressBarOrientation` type exists. |
| `ChunkCount` unsigned constraint | incomplete | Spec: `unsigned int`, default `5`. Impl: signed `int`, default `5`; no guard against negative values. |
| ARIA surface | covered | `role="progressbar"`, valuemin/max/now, label — matches. |
| `Events` demo page | missing | Narrative advertises `/chunkprogressbar/events`; no such route. |

---

## skeleton

**Verdict:** needs-work.
**Impl:** `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Feedback\SunfishSkeleton.razor`
**Demos:** `Overview/Demo.razor`, `Appearance/Demo.razor` under `apps/kitchen-sink/Pages/Components/Feedback/Skeleton/`.
**Spec files reviewed:** `skeleton/overview.md`, `skeleton/appearance.md`, `skeleton/accessibility/wai-aria-support.md`.

| Gap | Severity | Detail |
|-----|----------|--------|
| `ShapeType` vs `Variant` name + enum values | incomplete | Spec: `ShapeType` parameter, `SkeletonShapeType` enum with `Text`/`Rectangle`/`Circle`. Impl: `Variant` parameter, `SkeletonVariant` enum with `Text`/`Rectangular`/`Circular`/`Rounded`. Added `Rounded` is fine; but none of the spec-declared names match. |
| `AnimationType` parameter | missing | Spec: `SkeletonAnimationType` (`Pulse` default / `Wave` / `None`) with dedicated section. Impl: no parameter, no enum — animation is hard-coded in CSS. The Appearance demo narrative even omits animation; the spec's central appearance feature is unimplemented. |
| `aria-hidden="true"` default | covered | Matches spec guidance that the skeleton be invisible to AT and paired with an external `aria-busy`/`role="alert"`. |
| Templates demo page | missing | Narrative advertises `/skeleton/templates` and `/skeleton/shapes`; neither route exists. |

---

## Top-level next actions

1. **Notification family (highest-impact, downgrade risk)** — build a first-class `SunfishNotification` component with `@ref` + `Show/Hide/HideAll` and a spec-shaped `NotificationModel` (icon fields, `ThemeColor` as CSS class, all 13 animations). Correct accessibility to `role="alert"` + `aria-label`. Replace the placeholder demo with a working example. Downgrade `notification` mapping status to `partial` until delivered.
2. **Loader: fix `Type` bug** — either wire `LoaderType` through to distinct visuals or remove the parameter and reflect that in the mapping/spec.
3. **LoaderContainer: rename `Loading`→`Visible`**, and add `LoaderPosition`, `OverlayThemeColor`, `ThemeColor`, and `LoaderType`. Rename `LoaderTemplate`→`Template` to match spec.
4. **ProgressBar: add `<ProgressBarLabel>`** (Position/Template/context.Value) and `Orientation`. Rename `IsIndeterminate`→`Indeterminate` for parity.
5. **Skeleton: add `AnimationType` (None/Pulse/Wave)**; align `ShapeType` naming and add a `ShapeType` alias or update the spec to `Variant`.
6. **ChunkProgressBar: fix `Indeterminate` default and switch enum** to a new `ProgressBarOrientation`.
7. **Demo tree coverage** — fix the dangling route advertisements (`loader/types-and-sizes`, `loader/appearance`, `loadercontainer/panel`, `loadercontainer/events`, `progressbar/label`, `progressbar/events`, `chunkprogressbar/events`, `skeleton/templates`, `skeleton/shapes`) by either adding pages or removing the links.

### Tier 2 priority order (highest drift → lowest)

1. **notification** — downgrade + rebuild (missing ref API, a11y bug, 7 gaps).
2. **loadercontainer** — 8 gaps, 3 missing primary parameters.
3. **loader** — API-shape bug (`Type` not wired) + appearance gaps.
4. **skeleton** — missing `AnimationType` enum + naming drift.
5. **progressbar** — missing `<ProgressBarLabel>` template surface + orientation.
6. **chunkprogressbar** — smallest delta; mostly defaults/enum fixes.

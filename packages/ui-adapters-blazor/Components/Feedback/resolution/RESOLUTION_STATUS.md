---
component: SunfishAlert, SunfishAlertStrip, SunfishCallout, SunfishConfirmDialog, SunfishDataBanner, SunfishDataToast, SunfishDialog, SunfishProgressBar, SunfishProgressCircle, SunfishSkeleton, SunfishSnackbar, SunfishSnackbarHost, SunfishSpinner, SunfishToast
phase: 2
status: not-started
complexity: mixed
priority: high
owner: ""
last-updated: 2026-03-31
depends-on: [SunfishThemeProvider]
external-resources:
  - name: ""
    url: ""
    license: ""
    approved: false
---

# Resolution Status: Feedback

## Current Phase
Phase 2: Dialog and ConfirmDialog are Phase 2; Alert, ProgressBar, Skeleton, Toast are Phase 3; remaining components are Phase 4

## Gap Summary
Dialog has 9 gaps (no two-way Visible binding), ConfirmDialog 8 gaps, Toast 8 gaps (architectural divergence from target), ProgressBar 4 gaps, Skeleton 3 gaps, Alert 5 gaps. Other components have minor gaps.

## Resolution Progress

### Completed
- [x] **SunfishDialog** — IMPLEMENTED (9/9 gaps resolved): Added two-way `@bind-Visible`, `DialogContent`/`DialogActions` RenderFragments, `ShowCloseButton`, `CloseOnOverlayClick`, `Refresh()` method, `role="dialog"` and `aria-modal`. Updated all samples and tests.
- [x] **SunfishConfirmDialog** — IMPLEMENTED (8/8 gaps resolved): Added two-way `@bind-Visible`, `DialogContent` RenderFragment, `Width`/`Height`, `ShowCloseButton`, `CloseOnOverlayClick`, `role="alertdialog"`. Updated all samples.

### Not Started
- [ ] SunfishAlert — 5 gaps
- [ ] SunfishProgressBar — 4 gaps
- [ ] SunfishSkeleton — 3 gaps
- [ ] SunfishToast — 8 gaps
- [ ] Minor components (AlertStrip, Callout, DataBanner, DataToast, etc.)

## Blockers
- None

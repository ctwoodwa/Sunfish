---
component: SunfishAccordion, SunfishAccordionItem, SunfishAppBar, SunfishColumn, SunfishContainer, SunfishDivider, SunfishDrawer, SunfishGridLayout, SunfishPanel, SunfishRow, SunfishSplitter, SunfishStack, SunfishStep, SunfishStepper, SunfishTabStrip, TabStripTab
note: "SunfishGrid renamed to SunfishGridLayout (2026-04-03). Historical references to SunfishGrid in this file refer to the layout component now named SunfishGridLayout."
phase: 1
status: in-progress
complexity: multi-pass
priority: critical
owner: ""
last-updated: 2026-03-31
depends-on: [SunfishThemeProvider]
external-resources:
  - name: ""
    url: ""
    license: ""
    approved: false
---

# Resolution Status: Layout Components

## Current Phase
Phase 1: Grid, Stack, Container, Row, Column, Divider
Phase 2: Accordion, Drawer, Splitter, Panel, Stepper
Phase 3: AccordionItem, AppBar, TabStrip, Step
Phase 4: TabStripTab

## Gap Summary
SunfishGrid has 7 gaps (needs GridLayoutColumn/Row/Item children). SunfishStack has 5 gaps (missing Spacing/Width/Height). SunfishDrawer has 10 gaps (no Mode/MiniMode/data binding). SunfishAccordion has 9 gaps (no data binding/hierarchy). SunfishSplitter has 8 gaps (2 panes only, no resize). SunfishPanel has 7 gaps (placeholder div). SunfishStepper has 6 gaps (no orientation/linear flow). Rest are minor.

## Resolution Progress

### Phase 1 Components
- [x] **SunfishStack** — IMPLEMENTED (5/5 gaps resolved): Added `Orientation`, `Spacing`, `Width`, `Height`, `HorizontalAlign`, `VerticalAlign`. Simplified `ISunfishCssProvider.StackClass` interface. Updated both providers and sample pages.
- [x] **SunfishContainer** — COMPLETE (0 gaps)
- [x] **SunfishRow** — COMPLETE (0 gaps)
- [x] **SunfishColumn** — COMPLETE (0 gaps)
- [x] **SunfishDivider** — COMPLETE (0 gaps)
- [x] **SunfishGrid** — IMPLEMENTED (7/7 gaps resolved): Added CSS Grid Layout mode with `Columns`, `Rows`, `ColumnSpacing`, `RowSpacing`, `Width`, `HorizontalAlign`, `VerticalAlign`. Created `SunfishGridLayoutColumn`, `SunfishGridLayoutRow`, `SunfishGridLayoutItem` child components. Backward-compatible with existing flex container mode.

### Phase 2-4 Components
- [ ] SunfishAccordion — NOT STARTED
- [ ] SunfishDrawer — NOT STARTED
- [ ] SunfishSplitter — NOT STARTED
- [ ] SunfishPanel — NOT STARTED
- [ ] SunfishStepper — NOT STARTED
- [ ] SunfishAccordionItem — NOT STARTED
- [ ] SunfishAppBar — NOT STARTED
- [ ] SunfishTabStrip — NOT STARTED
- [ ] SunfishStep — NOT STARTED
- [ ] TabStripTab — NOT STARTED

## Blockers
- None

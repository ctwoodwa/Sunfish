# A11y audit — non-Forms/DataDisplay — 2026-04-21

All 36 in-scope `/Accessibility/Demo.razor` pages were read and assessed against the "demonstrates real a11y features" bar (ARIA roles/attributes, keyboard map, focus restoration, live regions, non-colour cues, screen-reader output, WCAG guidance). Every demo already meets or exceeds that bar — no rewrites were needed.

## Summary by family

### Buttons
| Component | Before | After | Changes |
|---|---|---|---|
| Button | good | good | none — keyboard table, icon-only `aria-label`, `aria-describedby`, disabled state, `aria-haspopup`/`aria-expanded` menu trigger |
| ButtonGroup | good | good | none — `role="group"`, `aria-labelledby`, full roving-tabindex toolbar pattern |
| Chip | good | good | none — listbox + options with `aria-selected`, removable dismiss button with `aria-label` |
| Fab | good | good | none — icon-only name, extended, `aria-describedby`, disabled |
| SegmentedControl | good | good | none — radiogroup semantics, `aria-labelledby`, `aria-describedby` |
| ToggleButton | good | good | none — `aria-pressed`, switch-vs-toggle guidance, icon-only label |

### Charts
| Component | Before | After | Changes |
|---|---|---|---|
| Chart | good | good | none — `graphics-document` / `graphics-object` / `graphics-symbol`, `aria-roledescription`, legend as list, tabbable data points |

### Editors
| Component | Before | After | Changes |
|---|---|---|---|
| Autocomplete | good | good | none — combobox + `aria-autocomplete="list"`, `aria-activedescendant`, grouped suggestions |
| Checkbox | good | good | none — tri-state `aria-checked`, required/describedby, `aria-invalid` + error alert |
| ColorPicker | good | good | none — `aria-haspopup="dialog"`, HSV arrow stepping, grid/gridcell palette |
| ComboBox | good | good | none — combobox listbox pattern, `aria-expanded`, `aria-selected` |
| DatePicker | good | good | none — `role="dialog"`, per-day `aria-label` with full date, `aria-selected`, disabled days |
| DropDownList | good | good | none — listbox trigger, filterable popup, `aria-disabled` |
| MultiSelect | good | good | none — `aria-multiselectable`, backspace-to-remove, Ctrl+A |
| Rating | good | good | none — `role="radiogroup"`, "N of Max stars" labels, read-only span semantics |
| Slider | good | good | none — native range `role="slider"`, `aria-valuetext`, `aria-describedby` |
| Switch | good | good | none — `role="switch"`, labelledby, describedby hint |
| TextArea | good | good | none — `aria-invalid` + `role="alert"`, required, described-by |
| TextBox | good | good | none — labelled via labelledby, required, invalid state |
| Upload | good | good | none — landmark region, progressbar with `aria-valuenow`, per-file button labels, polite live region |

### Feedback
| Component | Before | After | Changes |
|---|---|---|---|
| Alert | good | good | none — `role="alert"` live announcement, dismiss button, non-colour severity cues |
| ConfirmDialog | good | good | none — `role="alertdialog"`, destructive verb, focus restoration to trigger |
| Dialog | good | good | none — `role="dialog"` vs alertdialog guidance, locked dialog, focus restoration |
| ProgressBar | good | good | none — determinate + indeterminate, `aria-valuenow` omitted when indeterminate, reduced-motion note |
| SignalRStatus | good | good | none — live `aria-label` aggregate, textual health labels, keyboard map |
| SnackbarHost | good | good | none — `role="status"` polite region, severity prefixes, WCAG 2.2.1 persistent option |
| Tooltip | good | good | none — `role="tooltip"`, hover+focus parity (WCAG 1.4.13), icon-only naming |
| Window | good | good | none — `role="dialog"` vs alertdialog, `aria-modal`, Esc behaviour, focus management |

### Layout
| Component | Before | After | Changes |
|---|---|---|---|
| Accordion | good | good | none — `aria-expanded`, `aria-controls`, `role="region"` panels, disabled header |
| Container | good | good | none — skip-link, `role="main"`, `aria-labelledby` region, complementary landmark |
| Drawer | good | good | none — `role="navigation"` landmark, modal `role="dialog"`, MiniMode with hidden labels |
| Grid | good | good | none — "no `role="grid"`" rationale, HTML5 landmarks in columns, source-order reading |
| Panel | good | good | none — collapsible button with `aria-expanded`, region pattern via labelledby |
| Stack | good | good | none — toolbar/radiogroup/nav patterns, flex-order vs DOM-order reminder |

### Navigation
| Component | Before | After | Changes |
|---|---|---|---|
| Breadcrumb | good | good | none — `<nav aria-label>`, `<ol>`/`<li>`, `aria-current="page"`, expected SR output |
| Menu | good | good | none — `role="menuitem"`, separator, `aria-haspopup`, arrow/Home/End/Esc keyboard |
| Pagination | good | good | none — `aria-current="page"`, prev/next labelled, auto-disabled ends, page-size combobox |
| Stepper | good | good | none — nav landmark + live step label, error step + `role="alert"` |
| TabStrip | good | good | none — tablist/tab/tabpanel, `aria-orientation`, closeable close-button label, PersistTabContent |
| Toolbar | good | good | none — `role="toolbar"` + group + separator, `aria-pressed` toggles, disabled skip |

### Overlays
No `/Accessibility/Demo.razor` files exist in this family (only `Popup/Overview`). Nothing to audit.

### Scheduling
| Component | Before | After | Changes |
|---|---|---|---|
| AllocationScheduler | good | good | none — `role="grid"` with columnheader/gridcell/separator/toolbar, polite live region wired to OnSelectionChanged |

### Utility
Family folder does not exist in the kitchen-sink tree. Skipped per instructions.

## Notes
- All 36 demos render `SunfishExamplePanel` with required `Title`, `Breadcrumb`, `GitHubUrl`, `Sources="@GeneratedSources"`, `<Narrative>`, `<Example>` shape. No structural fixes required.
- No trivial/empty demos were found. No files were rewritten; none needed engineering follow-up.
- Build verification (`dotnet build --nologo -v quiet`): **0 errors, 1 pre-existing unrelated CS0162 warning**. Clean.

## Outcome
- Demos audited: **36**
- Rewritten: **0**
- Already good: **36**
- Flagged for engineering: **0**
- Biggest improvement: n/a — all demos were already depth-complete, covering ARIA roles, keyboard maps, live regions, non-colour cues, and screen-reader output samples.

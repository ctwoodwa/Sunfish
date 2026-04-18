# Component API Specifications

This directory contains the authoritative API specifications for all Sunfish components.
Each subdirectory corresponds to a component (or component group) and contains markdown
files describing parameters, events, templates, accessibility requirements, and usage patterns.

These specs drive component development — every Sunfish component should implement the API
surface described in its corresponding spec directory.

## How to Use

- **Implementing a component**: Read the spec's `overview.md` first, then review
  `events.md`, `accessibility/`, and any feature-specific files.
- **Reviewing a PR**: Compare the component's parameters and behavior against its spec.
- **Adding a new component**: Create the spec directory first (or find the existing one),
  then implement to match.

## Component Index

> **Status legend**: Implemented | Partial | Planned | N/A (not applicable to Sunfish)

### Layout

| Spec Directory | Sunfish Component | Status |
|---|---|---|
| [appbar](appbar/overview.md) | SunfishAppBar | Implemented |
| [dialog](dialog/overview.md) | SunfishDialog | Implemented |
| [drawer](drawer/overview.md) | SunfishDrawer | Implemented |
| [gridlayout](gridlayout/overview.md) | SunfishGrid / SunfishRow / SunfishColumn | Implemented |
| [panelbar](panelbar/overview.md) | SunfishAccordion / SunfishAccordionItem | Implemented |
| [splitter](splitter/overview.md) | SunfishSplitter | Implemented |
| [stacklayout](stacklayout/overview.md) | SunfishStack | Implemented |
| [tabstrip](tabstrip/overview.md) | SunfishTabs / SunfishTabPanel | Implemented |
| [tilelayout](tilelayout/overview.md) | — | Planned |
| [window](window/overview.md) | SunfishDialog | Partial |
| [card](card/overview.md) | SunfishCard | Implemented |
| [dockmanager](dockmanager/overview.md) | — | Planned |

### Navigation

| Spec Directory | Sunfish Component | Status |
|---|---|---|
| [breadcrumb](breadcrumb/overview.md) | SunfishBreadcrumb | Implemented |
| [menu](menu/overview.md) | SunfishMenu / SunfishMenuItem | Implemented |
| [contextmenu](contextmenu/overview.md) | SunfishContextMenu | Implemented |
| [pager](pager/overview.md) | SunfishPagination | Implemented |
| [stepper](stepper/overview.md) | SunfishStepper / SunfishStep | Implemented |
| [toolbar](toolbar/overview.md) | SunfishToolbar | Implemented |
| [treeview](treeview/overview.md) | SunfishTreeView / SunfishTreeItem | Implemented |
| [wizard](wizard/overview.md) | — | Planned |

### Buttons

| Spec Directory | Sunfish Component | Status |
|---|---|---|
| [button](button/overview.md) | SunfishButton | Implemented |
| [buttongroup](buttongroup/overview.md) | SunfishButtonGroup | Implemented |
| [dropdownbutton](dropdownbutton/overview.md) | SunfishSplitButton | Partial |
| [floatingactionbutton](floatingactionbutton/overview.md) | SunfishFab | Implemented |
| [splitbutton](splitbutton/overview.md) | SunfishSplitButton | Implemented |
| [togglebutton](togglebutton/overview.md) | SunfishToggleButton | Implemented |
| [chip](chip/overview.md) | SunfishChip | Implemented |
| [chiplist](chiplist/overview.md) | SunfishChipSet | Implemented |

### Forms & Inputs

| Spec Directory | Sunfish Component | Status |
|---|---|---|
| [autocomplete](autocomplete/overview.md) | SunfishAutocomplete | Implemented |
| [checkbox](checkbox/overview.md) | SunfishCheckbox | Implemented |
| [colorpicker](colorpicker/overview.md) | SunfishColorPicker | Implemented |
| [colorgradient](colorgradient/overview.md) | SunfishColorPicker | Partial |
| [colorpalette](colorpalette/overview.md) | SunfishColorPicker | Partial |
| [flatcolorpicker](flatcolorpicker/overview.md) | SunfishColorPicker | Partial |
| [combobox](combobox/overview.md) | SunfishSelect | Partial |
| [dateinput](dateinput/overview.md) | SunfishDatePicker | Partial |
| [datepicker](datepicker/overview.md) | SunfishDatePicker | Implemented |
| [daterangepicker](daterangepicker/overview.md) | SunfishDateRangePicker | Implemented |
| [datetimepicker](datetimepicker/overview.md) | SunfishDateTimePicker | Implemented |
| [dropdownlist](dropdownlist/overview.md) | SunfishSelect | Implemented |
| [dropdowntree](dropdowntree/overview.md) | — | Planned |
| [editor](editor/overview.md) | — | Planned |
| [fileselect](fileselect/overview.md) | SunfishFileUpload | Partial |
| [filter](filter/overview.md) | — | Planned |
| [floatinglabel](floatinglabel/overview.md) | SunfishLabel | Partial |
| [form](form/overview.md) | SunfishForm / SunfishField | Implemented |
| [listbox](listbox/overview.md) | SunfishList | Partial |
| [maskedtextbox](maskedtextbox/overview.md) | SunfishMaskedInput | Implemented |
| [multicolumncombobox](multicolumncombobox/overview.md) | — | Planned |
| [multiselect](multiselect/overview.md) | SunfishSelect | Partial |
| [numerictextbox](numerictextbox/overview.md) | SunfishNumericInput | Implemented |
| [radiogroup](radiogroup/overview.md) | SunfishRadio | Implemented |
| [rangeslider](rangeslider/overview.md) | SunfishSlider | Partial |
| [rating](rating/overview.md) | SunfishRating | Implemented |
| [signature](signature/overview.md) | — | Planned |
| [slider](slider/overview.md) | SunfishSlider | Implemented |
| [switch](switch/overview.md) | SunfishSwitch | Implemented |
| [textarea](textarea/overview.md) | SunfishTextArea | Implemented |
| [textbox](textbox/overview.md) | SunfishTextField | Implemented |
| [timepicker](timepicker/overview.md) | SunfishTimePicker | Implemented |
| [upload](upload/overview.md) | SunfishFileUpload | Implemented |
| [validation](validation/overview.md) | SunfishValidation | Implemented |

### Data Display

| Spec Directory | Sunfish Component | Status |
|---|---|---|
| [avatar](avatar/overview.md) | SunfishAvatar | Implemented |
| [badge](badge/overview.md) | SunfishBadge | Implemented |
| [grid](grid/overview.md) | SunfishDataGrid | Implemented |
| [listview](listview/overview.md) | SunfishList / SunfishListItem | Implemented |
| [pivotgrid](pivotgrid/overview.md) | — | Planned |
| [spreadsheet](spreadsheet/overview.md) | — | Planned |
| [treelist](treelist/overview.md) | SunfishTreeView | Partial |
| [tooltip](tooltip/overview.md) | SunfishTooltip | Implemented |
| [popover](popover/overview.md) | SunfishPopover | Implemented |

### Feedback & Notifications

| Spec Directory | Sunfish Component | Status |
|---|---|---|
| [notification](notification/overview.md) | SunfishToast / SunfishSnackbar | Implemented |
| [progressbar](progressbar/overview.md) | SunfishProgressBar | Implemented |
| [chunkprogressbar](chunkprogressbar/overview.md) | SunfishProgressBar | Partial |
| [loader](loader/overview.md) | SunfishSpinner | Implemented |
| [loadercontainer](loadercontainer/overview.md) | SunfishSpinner | Partial |
| [skeleton](skeleton/overview.md) | SunfishSkeleton | Implemented |

### Charts & Gauges

| Spec Directory | Sunfish Component | Status |
|---|---|---|
| [chart](chart/overview.md) | — | Planned |
| [stockchart](stockchart/overview.md) | — | Planned |
| [gauges](gauges/overview.md) | — | Planned |
| [sankey](sankey/overview.md) | — | Planned |

### Scheduling

| Spec Directory | Sunfish Component | Status |
|---|---|---|
| [calendar](calendar/overview.md) | SunfishDatePicker | Partial |
| [gantt](gantt/overview.md) | — | Planned |
| [scheduler](scheduler/overview.md) | — | Planned |

### Barcodes & Media

| Spec Directory | Sunfish Component | Status |
|---|---|---|
| [barcodes](barcodes/barcode/overview.md) | — | Planned |
| [carousel](carousel/overview.md) | SunfishCarousel | Implemented |
| [map](map/overview.md) | — | Planned |
| [pdfviewer](pdfviewer/overview.md) | — | Planned |

### AI Components

| Spec Directory | Sunfish Component | Status |
|---|---|---|
| [aiprompt](aiprompt/overview.md) | — | Planned |
| [chat](chat/overview.md) | — | Planned |
| [inlineaiprompt](inlineaiprompt/overview.md) | — | Planned |
| [promptbox](promptbox/overview.md) | — | Planned |
| [smartpastebutton](smartpastebutton/overview.md) | — | Planned |
| [speechtotextbutton](speechtotextbutton/overview.md) | — | Planned |

### Utility & Infrastructure

| Spec Directory | Sunfish Component | Status |
|---|---|---|
| [animationcontainer](animationcontainer/overview.md) | — | N/A |
| [diagram](diagram/overview.md) | — | Planned |
| [dropzone](dropzone/overview.md) | — | Planned |
| [mediaquery](mediaquery/overview.md) | — | N/A |
| [popup](popup/overview.md) | SunfishPopover | Partial |
| [rootcomponent](rootcomponent/overview.md) | SunfishThemeProvider | Partial |

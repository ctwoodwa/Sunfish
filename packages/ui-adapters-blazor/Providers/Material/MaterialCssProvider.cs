using Sunfish.Foundation.Base;
using Sunfish.UICore.Contracts;
using Sunfish.Foundation.Enums;

namespace Sunfish.Providers.Material;

public class MaterialCssProvider : ISunfishCssProvider
{
    // Layout
    public string ContainerClass(string? size = null) =>
        new CssClassBuilder()
            .AddClass("sf-container")
            .AddClass($"sf-container--{size}", size != null)
            .Build();

    public string GridClass() => "sf-grid";

    public string RowClass() => "sf-row";

    public string ColumnClass(int? span = null, int? offset = null) =>
        new CssClassBuilder()
            .AddClass("sf-col")
            .AddClass($"sf-col--{span}", span.HasValue)
            .AddClass($"sf-col--offset-{offset}", offset.HasValue)
            .Build();

    public string StackClass(StackDirection orientation) =>
        new CssClassBuilder()
            .AddClass("sf-stack")
            .AddClass($"sf-stack--{orientation.ToString().ToLower()}")
            .Build();

    public string DividerClass(bool vertical) =>
        new CssClassBuilder()
            .AddClass("sf-divider")
            .AddClass("sf-divider--vertical", vertical)
            .Build();

    public string PanelClass() => "sf-panel";

    public string DrawerClass(DrawerPosition position, bool isOpen) =>
        new CssClassBuilder()
            .AddClass("sf-drawer")
            .AddClass($"sf-drawer--{position.ToString().ToLower()}")
            .AddClass("sf-drawer--open", isOpen)
            .Build();

    public string AppBarClass(AppBarPosition position) =>
        new CssClassBuilder()
            .AddClass("sf-appbar")
            .AddClass($"sf-appbar--{position.ToString().ToLower()}")
            .Build();

    public string AccordionClass() => "sf-accordion";

    public string AccordionItemClass(bool isExpanded) =>
        new CssClassBuilder()
            .AddClass("sf-accordion-item")
            .AddClass("sf-accordion-item--expanded", isExpanded)
            .Build();

    public string TabsClass(TabPosition position, TabAlignment alignment, TabSize size) =>
        new CssClassBuilder()
            .AddClass("sf-tabs")
            .AddClass($"sf-tabs--{position.ToString().ToLower()}", position != TabPosition.Top)
            .AddClass($"sf-tabs--align-{alignment.ToString().ToLower()}", alignment != TabAlignment.Start)
            .AddClass($"sf-tabs--{size.ToString().ToLower()}", size != TabSize.Medium)
            .Build();

    public string TabClass(bool isActive, bool isDisabled) =>
        new CssClassBuilder()
            .AddClass("sf-tab")
            .AddClass("sf-tab--active", isActive)
            .AddClass("sf-tab--disabled", isDisabled)
            .Build();

    public string TabPanelClass(bool isActive, bool persistContent) =>
        new CssClassBuilder()
            .AddClass("sf-tab-panel")
            .AddClass("sf-tab-panel--active", isActive)
            .AddClass("sf-tab-panel--hidden", persistContent && !isActive)
            .Build();

    public string StepperClass() => "sf-stepper";

    public string StepClass(StepStatus status) =>
        new CssClassBuilder()
            .AddClass("sf-step")
            .AddClass($"sf-step--{status.ToString().ToLower()}")
            .Build();

    public string SplitterClass(SplitterOrientation orientation) =>
        new CssClassBuilder()
            .AddClass("sf-splitter")
            .AddClass($"sf-splitter--{orientation.ToString().ToLower()}")
            .Build();

    public string DrawerOverlayClass() => "sf-drawer-overlay";

    public string ContextMenuClass() => "sf-context-menu";

    // Navigation
    public string NavBarClass() => "sf-navbar";

    public string NavMenuClass() => "sf-navmenu";

    public string NavItemClass(bool isActive) =>
        new CssClassBuilder()
            .AddClass("sf-navitem")
            .AddClass("sf-navitem--active", isActive)
            .Build();

    public string BreadcrumbClass() => "sf-breadcrumb";

    public string BreadcrumbItemClass() => "sf-breadcrumb-item";

    public string MenuClass() => "sf-menu";

    public string MenuItemClass(bool isDisabled) =>
        new CssClassBuilder()
            .AddClass("sf-menu-item")
            .AddClass("sf-menu-item--disabled", isDisabled)
            .Build();

    public string MenuDividerClass() => "sf-menu-divider";

    public string PaginationClass() => "sf-pagination";

    public string PaginationItemClass(bool isActive) =>
        new CssClassBuilder()
            .AddClass("sf-pagination-item")
            .AddClass("sf-pagination-item--active", isActive)
            .Build();

    public string TreeViewClass() => "sf-treeview";

    public string TreeItemClass(bool isExpanded, bool isSelected) =>
        new CssClassBuilder()
            .AddClass("sf-tree-item")
            .AddClass("sf-tree-item--expanded", isExpanded)
            .AddClass("sf-tree-item--selected", isSelected)
            .Build();

    public string ToolbarClass() => "sf-toolbar";

    public string EnvironmentBadgeClass(string env) =>
        new CssClassBuilder()
            .AddClass("sf-env-badge")
            .AddClass($"sf-env-badge--{env.ToLower()}")
            .Build();

    public string TimeRangeSelectorClass() => "sf-time-range-selector";

    public string ToolbarButtonClass(bool isDisabled = false) =>
        new CssClassBuilder()
            .AddClass("sf-toolbar-btn")
            .AddClass("sf-toolbar-btn--disabled", isDisabled)
            .Build();

    public string ToolbarToggleButtonClass(bool isActive, bool isDisabled = false) =>
        new CssClassBuilder()
            .AddClass("sf-toolbar-btn")
            .AddClass("sf-toolbar-btn--active", isActive)
            .AddClass("sf-toolbar-btn--disabled", isDisabled)
            .Build();

    public string ToolbarSeparatorClass() => "sf-toolbar-sep";

    public string ToolbarGroupClass() => "sf-toolbar-group";

    public string LinkClass() => "sf-link";

    // Buttons
    // ADR 0024 — Subtle / Transparent / Light / Dark emit dedicated `sf-btn-*`
    // classes that are token-backed in Providers/Material/Styles/components/_button.scss.
    // Subtle / Transparent → M3 "text" button family; Light → M3 outlined;
    // Dark → M3 filled on inverse-surface.
    private static string MaterialVariantAdditionalClass(ButtonVariant variant) =>
        variant switch
        {
            ButtonVariant.Subtle => "sf-btn-subtle",
            ButtonVariant.Transparent => "sf-btn-transparent",
            ButtonVariant.Light => "sf-btn-light",
            ButtonVariant.Dark => "sf-btn-dark",
            _ => string.Empty
        };

    public string ButtonClass(ButtonVariant variant, ButtonSize size, bool isOutline, bool isDisabled) =>
        new CssClassBuilder()
            .AddClass("sf-button")
            .AddClass($"sf-button--{variant.ToString().ToLower()}")
            .AddClass(MaterialVariantAdditionalClass(variant), !string.IsNullOrEmpty(MaterialVariantAdditionalClass(variant)))
            .AddClass($"sf-button--{size.ToString().ToLower()}")
            .AddClass("sf-button--outline", isOutline)
            .AddClass("sf-button--disabled", isDisabled)
            .Build();

    public string ButtonClass(ButtonVariant variant, ButtonSize size, FillMode fillMode, RoundedMode rounded, bool isDisabled) =>
        new CssClassBuilder()
            .AddClass("sf-button")
            .AddClass($"sf-button--{variant.ToString().ToLower()}")
            .AddClass(MaterialVariantAdditionalClass(variant), !string.IsNullOrEmpty(MaterialVariantAdditionalClass(variant)))
            .AddClass($"sf-button--{size.ToString().ToLower()}")
            .AddClass($"sf-button--fill-{fillMode.ToString().ToLower()}", fillMode != FillMode.Solid)
            .AddClass($"sf-button--rounded-{rounded.ToString().ToLower()}", rounded != RoundedMode.Medium)
            .AddClass("sf-button--disabled", isDisabled)
            .Build();

    public string IconButtonClass(ButtonSize size) =>
        new CssClassBuilder()
            .AddClass("sf-icon-button")
            .AddClass($"sf-icon-button--{size.ToString().ToLower()}")
            .Build();

    public string ButtonGroupClass() => "sf-button-group";

    public string ToggleButtonClass(bool selected) =>
        new CssClassBuilder()
            .AddClass("sf-toggle-button")
            .AddClass("sf-toggle-button--active", selected)
            .Build();

    public string SplitButtonClass() => "sf-split-button";

    public string ChipClass(ChipVariant variant, bool isSelected) =>
        new CssClassBuilder()
            .AddClass("sf-chip")
            .AddClass($"sf-chip--{variant.ToString().ToLower()}")
            .AddClass("sf-chip--selected", isSelected)
            .Build();

    public string ChipSetClass() => "sf-chip-set";

    public string FabClass(FabSize size) =>
        new CssClassBuilder()
            .AddClass("sf-fab")
            .AddClass($"sf-fab--{size.ToString().ToLower()}")
            .Build();

    // Forms - Inputs
    public string TextBoxClass(bool isInvalid, bool isDisabled) =>
        new CssClassBuilder()
            .AddClass("sf-textbox")
            .AddClass("sf-textbox--invalid", isInvalid)
            .AddClass("sf-textbox--disabled", isDisabled)
            .Build();

    public string TextAreaClass(bool isInvalid) =>
        new CssClassBuilder()
            .AddClass("sf-textarea")
            .AddClass("sf-textarea--invalid", isInvalid)
            .Build();

    public string NumericInputClass() => "sf-numeric-input";

    public string SearchBoxClass() => "sf-search-box";

    public string AutocompleteClass() => "sf-autocomplete";

    public string AutocompleteClass(bool isOpen, bool isDisabled, bool isInvalid) =>
        new CssClassBuilder()
            .AddClass("sf-autocomplete")
            .AddClass("sf-autocomplete--open", isOpen)
            .AddClass("sf-autocomplete--disabled", isDisabled)
            .AddClass("sf-autocomplete--invalid", isInvalid)
            .Build();

    public string AutocompleteItemClass(bool isHighlighted, bool isSelected) =>
        new CssClassBuilder()
            .AddClass("sf-autocomplete-item")
            .AddClass("sf-autocomplete-item--highlighted", isHighlighted)
            .AddClass("sf-autocomplete-item--selected", isSelected)
            .Build();

    public string SelectClass(bool isInvalid) =>
        new CssClassBuilder()
            .AddClass("sf-select")
            .AddClass("sf-select--invalid", isInvalid)
            .Build();

    public string CheckboxClass(bool isChecked) =>
        new CssClassBuilder()
            .AddClass("sf-checkbox")
            .AddClass("sf-checkbox--checked", isChecked)
            .Build();

    public string RadioClass(bool isSelected) =>
        new CssClassBuilder()
            .AddClass("sf-radio")
            .AddClass("sf-radio--selected", isSelected)
            .Build();

    public string RadioGroupClass() => "sf-radio-group";

    public string SwitchClass(bool isOn) =>
        new CssClassBuilder()
            .AddClass("sf-switch")
            .AddClass("sf-switch--on", isOn)
            .Build();

    public string SliderClass() => "sf-slider";

    public string SliderClass(SliderOrientation orientation) =>
        new CssClassBuilder()
            .AddClass("sf-slider")
            .AddClass($"sf-slider--{orientation.ToString().ToLower()}")
            .Build();

    public string RatingClass() => "sf-rating";

    public string ColorPickerClass() => "sf-color-picker";
    public string ColorPickerPopupClass() => "sf-color-picker__popup";
    public string ColorGradientClass() => "fui-colorgradient";
    public string ColorPaletteClass() => "fui-colorpalette";
    public string FlatColorPickerClass() => "fui-flatcolorpicker";

    public string DatePickerClass() => "sf-datepicker";

    public string TimePickerClass() => "sf-timepicker";
    public string TimePickerPopupClass() => "sf-timepicker__popup";

    public string DateRangePickerClass() => "sf-date-range-picker";

    public string DateRangePickerPopupClass() => "sf-date-range-picker__popup";

    public string DateTimePickerClass() => "sf-datetime-picker";

    public string DateTimePickerPopupClass() => "sf-datetime-picker__popup";

    public string FileUploadClass() => "sf-file-upload";

    public string FileUploadFileListClass() => "sf-file-upload-file-list";

    public string FileUploadDropZoneClass(bool isDragOver, bool isDisabled) =>
        new CssClassBuilder()
            .AddClass("sf-file-upload__zone")
            .AddClass("sf-file-upload__zone--dragover", isDragOver)
            .AddClass("sf-file-upload__zone--disabled", isDisabled)
            .Build();

    // Selection / Dropdowns
    public string DropDownListClass(bool isOpen, bool isDisabled, bool isInvalid) =>
        new CssClassBuilder()
            .AddClass("sf-dropdownlist")
            .AddClass("sf-dropdownlist--open", isOpen)
            .AddClass("sf-dropdownlist--disabled", isDisabled)
            .AddClass("sf-dropdownlist--invalid", isInvalid)
            .Build();

    public string DropDownListPopupClass() => "sf-dropdownlist-popup";

    public string DropDownListItemClass(bool isHighlighted, bool isSelected) =>
        new CssClassBuilder()
            .AddClass("sf-dropdownlist-item")
            .AddClass("sf-dropdownlist-item--highlighted", isHighlighted)
            .AddClass("sf-dropdownlist-item--selected", isSelected)
            .Build();

    public string ComboBoxClass(bool isOpen, bool isDisabled, bool isInvalid) =>
        new CssClassBuilder()
            .AddClass("sf-combobox")
            .AddClass("sf-combobox--open", isOpen)
            .AddClass("sf-combobox--disabled", isDisabled)
            .AddClass("sf-combobox--invalid", isInvalid)
            .Build();

    public string ComboBoxPopupClass() => "sf-combobox-popup";

    public string ComboBoxItemClass(bool isHighlighted, bool isSelected) =>
        new CssClassBuilder()
            .AddClass("sf-combobox-item")
            .AddClass("sf-combobox-item--highlighted", isHighlighted)
            .AddClass("sf-combobox-item--selected", isSelected)
            .Build();

    public string MultiSelectClass(bool isOpen, bool isDisabled, bool isInvalid) =>
        new CssClassBuilder()
            .AddClass("sf-multiselect")
            .AddClass("sf-multiselect--open", isOpen)
            .AddClass("sf-multiselect--disabled", isDisabled)
            .AddClass("sf-multiselect--invalid", isInvalid)
            .Build();

    public string MultiSelectPopupClass() => "sf-multiselect-popup";

    public string MultiSelectItemClass(bool isHighlighted, bool isSelected) =>
        new CssClassBuilder()
            .AddClass("sf-multiselect-item")
            .AddClass("sf-multiselect-item--highlighted", isHighlighted)
            .AddClass("sf-multiselect-item--selected", isSelected)
            .Build();

    public string MultiSelectTagClass() => "sf-multiselect-tag";

    public string DropdownPopupClass() => "sf-dropdown-popup";

    // Forms - Containers
    public string FormClass() => "sf-form";

    public string FieldClass() => "sf-field";

    public string LabelClass() => "sf-label";

    public string InputGroupClass() => "sf-input-group";

    public string ValidationMessageClass(ValidationSeverity severity) =>
        new CssClassBuilder()
            .AddClass("sf-validation-message")
            .AddClass($"sf-validation-message--{severity.ToString().ToLower()}")
            .Build();

    // Data Display
    public string CardClass() => "sf-card";

    public string CardHeaderClass() => "sf-card-header";

    public string CardBodyClass() => "sf-card-body";

    public string CardActionsClass() => "sf-card-actions";

    public string CardFooterClass() => "sf-card-footer";

    public string CardImageClass() => "sf-card-image";

    public string ListClass() => "sf-list";

    public string ListItemClass() => "sf-list-item";

    public string TableClass() => "sf-table";

    public string AvatarClass(AvatarSize size) =>
        new CssClassBuilder()
            .AddClass("sf-avatar")
            .AddClass($"sf-avatar--{size.ToString().ToLower()}")
            .Build();

    public string BadgeClass(BadgeVariant variant) =>
        new CssClassBuilder()
            .AddClass("sf-badge")
            .AddClass($"sf-badge--{variant.ToString().ToLower()}")
            .Build();

    public string TooltipClass(TooltipPosition position) =>
        new CssClassBuilder()
            .AddClass("sf-tooltip")
            .AddClass($"sf-tooltip--{position.ToString().ToLower()}")
            .Build();

    public string TooltipClass(TooltipPosition position, TooltipShowOn showOn) =>
        new CssClassBuilder()
            .AddClass("sf-tooltip")
            .AddClass($"sf-tooltip--{position.ToString().ToLower()}")
            .AddClass($"sf-tooltip--show-{showOn.ToString().ToLower()}", showOn != TooltipShowOn.Hover)
            .Build();

    public string PopoverClass() => "sf-popover";

    public string TimelineClass() => "sf-timeline";

    public string TimelineItemClass() => "sf-timeline-item";

    public string CarouselClass() => "sf-carousel";

    public string TypographyClass(TypographyVariant variant) =>
        new CssClassBuilder()
            .AddClass("sf-typography")
            .AddClass($"sf-typography--{variant.ToString().ToLower()}")
            .Build();

    // Feedback
    public string AlertClass(AlertSeverity severity) =>
        new CssClassBuilder()
            .AddClass("sf-alert")
            .AddClass($"sf-alert--{severity.ToString().ToLower()}")
            .Build();

    public string AlertStripClass() => "sf-alert-strip";

    public string ToastClass(ToastSeverity severity) =>
        new CssClassBuilder()
            .AddClass("sf-toast")
            .AddClass($"sf-toast--{severity.ToString().ToLower()}")
            .Build();

    public string SnackbarClass() => "sf-snackbar";

    public string SnackbarClass(NotificationVerticalPosition vertical, NotificationHorizontalPosition horizontal) =>
        new CssClassBuilder()
            .AddClass("sf-snackbar")
            .AddClass($"sf-snackbar--{vertical.ToString().ToLower()}")
            .AddClass($"sf-snackbar--{horizontal.ToString().ToLower()}")
            .Build();

    public string SnackbarHostClass() => "sf-snackbar-host";

    public string DialogClass() => "sf-dialog";

    public string DialogClass(bool isDraggable) =>
        new CssClassBuilder()
            .AddClass("sf-dialog")
            .AddClass("sf-dialog--draggable", isDraggable)
            .Build();

    public string DialogOverlayClass() => "sf-dialog-overlay";

    // Theme 9 (Phase 2B) — Material Symbols "close" glyph. `aria-hidden` because
    // the button itself carries `aria-label="Close"` (set in SunfishDialog.razor).
    public string DialogCloseMarkup() =>
        "<span class=\"material-symbols-outlined\" aria-hidden=\"true\">close</span>";
    public string DialogCloseButtonClass() => "sf-m3-dialog-close";

    public string ProgressBarClass() => "sf-progress-bar";

    public string ProgressCircleClass() => "sf-progress-circle";

    public string SpinnerClass(SpinnerSize size) =>
        new CssClassBuilder()
            .AddClass("sf-spinner")
            .AddClass($"sf-spinner--{size.ToString().ToLower()}")
            .Build();

    public string SkeletonClass(SkeletonVariant variant) =>
        new CssClassBuilder()
            .AddClass("sf-skeleton")
            .AddClass($"sf-skeleton--{variant.ToString().ToLower()}")
            .Build();

    public string CalloutClass(CalloutType type) =>
        new CssClassBuilder()
            .AddClass("sf-callout")
            .AddClass($"sf-callout--{type.ToString().ToLower()}")
            .Build();

    // DataGrid
    public string DataGridClass() => "sf-datagrid";

    // Material doesn't have a native HTML-table base class; the data grid surface
    // is fully styled via sf-datagrid-* BEM. Return empty so the Razor <table>
    // carries only the Sunfish classes.
    public string DataGridTableClass() => string.Empty;

    public string DataGridHeaderClass() => "sf-datagrid-header";

    public string DataGridHeaderCellClass(bool isSortable, bool isSorted) =>
        new CssClassBuilder()
            .AddClass("sf-datagrid-header-cell")
            .AddClass("sf-datagrid-header-cell--sortable", isSortable)
            .AddClass("sf-datagrid-header-cell--sorted", isSorted)
            .Build();

    public string DataGridRowClass(bool isSelected, bool isStriped) =>
        new CssClassBuilder()
            .AddClass("sf-datagrid-row")
            .AddClass("sf-datagrid-row--selected", isSelected)
            .AddClass("sf-datagrid-row--striped", isStriped)
            .Build();

    public string DataGridCellClass() => "sf-datagrid-cell";

    // Material uses its M3 text-button icon-button density for command column actions.
    // Keep sf-datagrid__cmd-btn as the existing hook that sunfish-material.css styles.
    public string DataGridCommandButtonClass() => "sf-datagrid__cmd-btn";

    public string DataGridPagerClass() => "sf-datagrid-pager";

    public string DataGridToolbarClass() => "sf-datagrid-toolbar";

    public string DataGridFilterRowClass() => "sf-datagrid-filter-row";

    public string DataGridFilterCellClass() => "sf-datagrid-filter-cell";

    public string DataGridGroupHeaderClass() => "sf-datagrid-group-header";

    // DataSheet
    public string DataSheetClass(bool isLoading) =>
        new CssClassBuilder()
            .AddClass("sf-datasheet")
            .AddClass("sf-datasheet--loading", isLoading)
            .Build();

    public string DataSheetCellClass(CellState state, bool isActive, bool isEditable) =>
        new CssClassBuilder()
            .AddClass("sf-datasheet__cell")
            .AddClass("sf-datasheet__cell--active", isActive)
            .AddClass("sf-datasheet__cell--readonly", !isEditable)
            .AddClass("sf-datasheet__cell--dirty", state == CellState.Dirty)
            .AddClass("sf-datasheet__cell--invalid", state == CellState.Invalid)
            .AddClass("sf-datasheet__cell--saving", state == CellState.Saving)
            .AddClass("sf-datasheet__cell--saved", state == CellState.Saved)
            .Build();

    public string DataSheetHeaderCellClass(bool isSortable) =>
        new CssClassBuilder()
            .AddClass("sf-datasheet__header-cell")
            .AddClass("sf-datasheet__header-cell--sortable", isSortable)
            .Build();

    public string DataSheetRowClass(bool isDirty, bool isSelected, bool isDeleted) =>
        new CssClassBuilder()
            .AddClass("sf-datasheet__row")
            .AddClass("sf-datasheet__row--dirty", isDirty)
            .AddClass("sf-datasheet__row--selected", isSelected)
            .AddClass("sf-datasheet__row--deleted", isDeleted)
            .Build();

    public string DataSheetToolbarClass() => "sf-datasheet__toolbar";

    public string DataSheetBulkBarClass(bool isVisible) =>
        new CssClassBuilder()
            .AddClass("sf-datasheet__bulk-bar")
            .AddClass("sf-datasheet__bulk-bar--visible", isVisible)
            .Build();

    public string DataSheetSaveFooterClass(int dirtyCount) =>
        new CssClassBuilder()
            .AddClass("sf-datasheet__save-footer")
            .AddClass("sf-datasheet__save-footer--has-changes", dirtyCount > 0)
            .Build();

    public string DataSheetAddButtonClass() => "sf-datasheet__add-btn";
    public string DataSheetSaveButtonClass() => "sf-datasheet__save-btn";
    public string DataSheetResetButtonClass() => "sf-datasheet__reset-btn";
    public string DataSheetSpinnerClass() => "sf-datasheet__spinner";
    public string DataSheetDirtyBadgeClass() => "sf-datasheet__dirty-badge";
    public string DataSheetSkeletonClass() => "sf-datasheet__skeleton";
    public string DataSheetSkeletonRowClass() => "sf-datasheet__skeleton-row";
    public string DataSheetSkeletonCellClass() => "sf-datasheet__skeleton-cell";
    public string DataSheetLoadingTextClass() => "sf-datasheet__loading-text";
    public string DataSheetEmptyClass() => "sf-datasheet__empty";
    public string DataSheetSelectHeaderClass() => "sf-datasheet__select-header";
    public string DataSheetActionsHeaderClass() => "sf-datasheet__actions-header";
    public string DataSheetAriaLiveClass() => "sf-datasheet__aria-live";
    public string DataSheetSelectCellClass() => "sf-datasheet__select-cell";
    public string DataSheetActionsCellClass() => "sf-datasheet__actions-cell";
    public string DataSheetDeleteButtonClass() => "sf-datasheet__delete-btn";
    public string DataSheetCellTextClass() => "sf-datasheet__cell-text";
    public string DataSheetEditorInputClass() => "sf-datasheet__editor-input";
    public string DataSheetEditorSelectClass() => "sf-datasheet__editor-select";
    public string DataSheetContentClass() => "sf-datasheet__content";
    public string DataSheetScreenReaderOnlyClass() => "sf-datasheet__sr-only";

    // ListView
    public string ListViewClass() => "sf-listview";

    public string ListViewItemClass(bool isSelected) =>
        new CssClassBuilder()
            .AddClass("sf-listview-item")
            .AddClass("sf-listview-item--selected", isSelected)
            .Build();

    // Window
    public string WindowClass(bool isModal) =>
        new CssClassBuilder()
            .AddClass("sf-window")
            .AddClass("sf-window--modal", isModal)
            .Build();

    public string WindowTitleBarClass() => "sf-window-titlebar";

    public string WindowContentClass() => "sf-window-content";

    public string WindowActionsClass() => "sf-window-actions";

    public string WindowOverlayClass() => "sf-window-overlay";

    public string WindowFooterClass() => "sf-window-footer";

    // Editor
    public string EditorClass() => "sf-editor";

    public string EditorToolbarClass() => "sf-editor-toolbar";

    public string EditorContentClass() => "sf-editor-content";

    // Upload
    public string UploadClass() => "sf-upload";

    public string UploadFileListClass() => "sf-upload-file-list";

    public string UploadFileItemClass() => "sf-upload-file-item";

    public string UploadDropZoneClass(bool isActive) =>
        new CssClassBuilder()
            .AddClass("sf-upload-dropzone")
            .AddClass("sf-upload-dropzone--active", isActive)
            .Build();

    // Charts
    public string ChartContainerClass() => "sf-chart-container";

    public string GaugeClass() => "sf-gauge";

    // Scheduling
    public string CalendarClass() => "sf-calendar";

    public string SchedulerClass() => "sf-scheduler";

    // AllocationScheduler
    public string AllocationSchedulerClass() => "sf-allocation-scheduler";

    public string AllocationSchedulerToolbarClass() => "sf-allocation-scheduler__toolbar";

    public string AllocationSchedulerResourceColumnClass(bool isPinned) =>
        new CssClassBuilder()
            .AddClass("sf-allocation-scheduler__resource-col")
            .AddClass("sf-allocation-scheduler__resource-col--pinned", isPinned)
            .Build();

    public string AllocationSchedulerTimeHeaderClass(TimeGranularity grain) =>
        new CssClassBuilder()
            .AddClass("sf-allocation-scheduler__time-header")
            .AddClass($"sf-allocation-scheduler__time-header--{grain.ToString().ToLower()}")
            .Build();

    public string AllocationSchedulerRowClass(bool isSelected, bool isOverAllocated, bool isStriped = false) =>
        new CssClassBuilder()
            .AddClass("sf-allocation-scheduler__row")
            .AddClass("sf-allocation-scheduler__row--selected", isSelected)
            .AddClass("sf-allocation-scheduler__row--over-allocated", isOverAllocated)
            .AddClass("sf-allocation-scheduler__row--striped", isStriped)
            .Build();

    public string AllocationSchedulerCellClass(bool isEditable, bool isSelected, bool isConflict, bool isDisabled, bool isDragTarget) =>
        new CssClassBuilder()
            .AddClass("sf-allocation-scheduler__cell")
            .AddClass("sf-allocation-scheduler__cell--editable", isEditable)
            .AddClass("sf-allocation-scheduler__cell--selected", isSelected)
            .AddClass("sf-allocation-scheduler__cell--conflict", isConflict)
            .AddClass("sf-allocation-scheduler__cell--disabled", isDisabled)
            .AddClass("sf-allocation-scheduler__cell--drag-target", isDragTarget)
            .Build();

    public string AllocationSchedulerCellValueClass(AllocationValueMode mode) =>
        new CssClassBuilder()
            .AddClass("sf-allocation-scheduler__cell-value")
            .AddClass($"sf-allocation-scheduler__cell-value--{mode.ToString().ToLower()}")
            .Build();

    public string AllocationSchedulerDeltaClass(DeltaDisplayMode mode, bool isOver, bool isUnder) =>
        new CssClassBuilder()
            .AddClass("sf-allocation-scheduler__delta")
            .AddClass($"sf-allocation-scheduler__delta--{mode.ToString().ToLower()}")
            .AddClass("sf-allocation-scheduler__delta--over", isOver)
            .AddClass("sf-allocation-scheduler__delta--under", isUnder)
            .Build();

    public string AllocationSchedulerScenarioStripClass() => "sf-allocation-scheduler__scenario-strip";

    public string AllocationSchedulerScenarioChipClass(bool isActive, bool isLocked) =>
        new CssClassBuilder()
            .AddClass("sf-allocation-scheduler__scenario-chip")
            .AddClass("sf-allocation-scheduler__scenario-chip--active", isActive)
            .AddClass("sf-allocation-scheduler__scenario-chip--locked", isLocked)
            .Build();

    public string AllocationSchedulerGhostBarClass() => "sf-allocation-scheduler__ghost-bar";

    public string AllocationSchedulerContextMenuClass() => "sf-allocation-scheduler__context-menu";

    public string AllocationSchedulerEmptyClass() => "sf-allocation-scheduler__empty";

    public string AllocationSchedulerLoaderClass() => "sf-allocation-scheduler__loader";

    public string AllocationSchedulerSplitterClass(bool isDragging, bool isFocused) =>
        new CssClassBuilder()
            .AddClass("sf-allocation-scheduler__splitter")
            .AddClass("sf-allocation-scheduler__splitter--dragging", isDragging)
            .AddClass("sf-allocation-scheduler__splitter--focused", isFocused)
            .Build();

    public string AllocationSchedulerSplitterRestoreClass(SplitterSide collapsedSide) =>
        new CssClassBuilder()
            .AddClass("sf-allocation-scheduler__splitter-restore")
            .AddClass($"sf-allocation-scheduler__splitter-restore--{collapsedSide.ToString().ToLower()}")
            .Build();

    // Overlays
    public string ModalClass(ModalSize size) =>
        new CssClassBuilder()
            .AddClass("sf-modal")
            .AddClass($"sf-modal--{size.ToString().ToLower()}")
            .Build();

    public string ModalOverlayClass() => "sf-modal-overlay";

    // ResizableContainer
    public string ResizableContainerClass(bool isResizing, bool isDisabled) =>
        new CssClassBuilder()
            .AddClass("sf-resizable-container")
            .AddClass("sf-resizable-container--resizing", isResizing)
            .AddClass("sf-resizable-container--disabled", isDisabled)
            .Build();

    public string ResizableContainerContentClass() => "sf-resizable-container__content";

    public string ResizableContainerHandleClass(ResizeEdges edge, bool isActive, bool isFocused) =>
        new CssClassBuilder()
            .AddClass("sf-resizable-container__handle")
            .AddClass("sf-resizable-container__handle--right", edge == ResizeEdges.Right)
            .AddClass("sf-resizable-container__handle--bottom", edge == ResizeEdges.Bottom)
            .AddClass("sf-resizable-container__handle--bottom-right", edge == ResizeEdges.BottomRight)
            .AddClass("sf-resizable-container__handle--left", edge == ResizeEdges.Left)
            .AddClass("sf-resizable-container__handle--top", edge == ResizeEdges.Top)
            .AddClass("sf-resizable-container__handle--top-left", edge == ResizeEdges.TopLeft)
            .AddClass("sf-resizable-container__handle--top-right", edge == ResizeEdges.TopRight)
            .AddClass("sf-resizable-container__handle--bottom-left", edge == ResizeEdges.BottomLeft)
            .AddClass("sf-resizable-container__handle--active", isActive)
            .AddClass("sf-resizable-container__handle--focused", isFocused)
            .Build();

    // Utility
    public string IconClass(string iconName, IconSize size, IconFlip flip = IconFlip.None, IconThemeColor themeColor = IconThemeColor.Base) =>
        new CssClassBuilder()
            .AddClass("sf-icon")
            .AddClass($"sf-icon--{size.ToString().ToLower()}")
            .AddClass($"sf-icon--{iconName}")
            .AddClass($"sf-icon--flip-{flip.ToString().ToLower()}", flip != IconFlip.None)
            .AddClass($"sf-icon--{themeColor.ToString().ToLower()}", themeColor != IconThemeColor.Base)
            .Build();

    public string DragDropClass() => "sf-dragdrop";

    public string DropZoneClass(bool isActive) =>
        new CssClassBuilder()
            .AddClass("sf-dropzone")
            .AddClass("sf-dropzone--active", isActive)
            .Build();

    public string ScrollViewClass() => "sf-scrollview";

    // SignalR Status
    public string SignalRStatusClass(AggregateConnectionState state, bool isCompact) =>
        new CssClassBuilder()
            .AddClass("sf-signalr-status")
            .AddClass($"sf-signalr-status--{state.ToString().ToLower()}")
            .AddClass("sf-signalr-status--compact", isCompact)
            .Build();

    public string SignalRPopupClass() => "sf-signalr-popup";

    public string SignalRRowClass(ConnectionHealthState health) =>
        new CssClassBuilder()
            .AddClass("sf-signalr-row")
            .AddClass($"sf-signalr-row--{health.ToString().ToLower()}")
            .Build();

    public string SignalRBadgeClass(ConnectionHealthState health) =>
        new CssClassBuilder()
            .AddClass("sf-signalr-badge")
            .AddClass($"sf-signalr-badge--{health.ToString().ToLower()}")
            .Build();
}

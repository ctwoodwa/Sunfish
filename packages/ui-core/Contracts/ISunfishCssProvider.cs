using Sunfish.Foundation.Enums;

namespace Sunfish.UICore.Contracts;

/// <summary>
/// Defines the contract for a design-system CSS provider. Each implementation
/// (e.g., Fluent UI, Material, Bootstrap) maps Sunfish component states to its
/// own CSS class names, keeping component logic design-system-agnostic.
/// </summary>
public interface ISunfishCssProvider
{
    // TODO(phase-2-followup): split by category into ISunfishButtonCssProvider, ISunfishFormCssProvider, etc.
    // ── Layout ──────────────────────────────────────────────────────────
    string ContainerClass(string? size = null);
    string GridClass();
    string RowClass();
    string ColumnClass(int? span = null, int? offset = null);
    string StackClass(StackDirection orientation);
    string DividerClass(bool vertical);
    string PanelClass();
    string DrawerClass(DrawerPosition position, bool isOpen);
    string AppBarClass(AppBarPosition position);
    string AccordionClass();
    string AccordionItemClass(bool isExpanded);
    string TabsClass(TabPosition position, TabAlignment alignment, TabSize size);
    string TabClass(bool isActive, bool isDisabled);
    string TabPanelClass(bool isActive, bool persistContent);
    string StepperClass();
    string StepClass(StepStatus status);
    string SplitterClass(SplitterOrientation orientation);
    string DrawerOverlayClass();
    string ContextMenuClass();

    // ── Navigation ──────────────────────────────────────────────────────
    string NavBarClass();
    string NavMenuClass();
    string NavItemClass(bool isActive);
    string BreadcrumbClass();
    string BreadcrumbItemClass();
    string MenuClass();
    string MenuItemClass(bool isDisabled);
    string MenuDividerClass();
    string PaginationClass();
    string PaginationItemClass(bool isActive);
    string TreeViewClass();
    string TreeItemClass(bool isExpanded, bool isSelected);
    string ToolbarClass();
    string EnvironmentBadgeClass(string env);
    string TimeRangeSelectorClass();
    string ToolbarButtonClass(bool isDisabled = false);
    string ToolbarToggleButtonClass(bool isActive, bool isDisabled = false);
    string ToolbarSeparatorClass();
    string ToolbarGroupClass();
    string LinkClass();

    // ── Buttons ─────────────────────────────────────────────────────────
    string ButtonClass(ButtonVariant variant, ButtonSize size, bool isOutline, bool isDisabled);
    string ButtonClass(ButtonVariant variant, ButtonSize size, FillMode fillMode, RoundedMode rounded, bool isDisabled);
    string IconButtonClass(ButtonSize size);
    string ButtonGroupClass();
    string ToggleButtonClass(bool selected);
    string SplitButtonClass();
    string ChipClass(ChipVariant variant, bool isSelected);
    string ChipSetClass();
    string FabClass(FabSize size);

    // ── Forms — Inputs ──────────────────────────────────────────────────
    string TextBoxClass(bool isInvalid, bool isDisabled);
    string TextAreaClass(bool isInvalid);
    string NumericInputClass();
    string SearchBoxClass();
    string AutocompleteClass();
    string AutocompleteClass(bool isOpen, bool isDisabled, bool isInvalid);
    string AutocompleteItemClass(bool isHighlighted, bool isSelected);
    string SelectClass(bool isInvalid);
    string CheckboxClass(bool isChecked);
    string RadioClass(bool isSelected);
    string RadioGroupClass();
    string SwitchClass(bool isOn);
    string SliderClass();
    string SliderClass(SliderOrientation orientation);
    string RatingClass();
    string ColorPickerClass();
    string ColorPickerPopupClass();
    string ColorGradientClass();
    string ColorPaletteClass();
    string FlatColorPickerClass();
    string DatePickerClass();
    string TimePickerClass();
    string TimePickerPopupClass();
    string DateRangePickerClass();
    string DateRangePickerPopupClass();
    string DateTimePickerClass();
    string DateTimePickerPopupClass();
    string FileUploadClass();
    string FileUploadFileListClass();
    string FileUploadDropZoneClass(bool isDragOver, bool isDisabled);

    // ── Selection / Dropdowns ───────────────────────────────────────────
    string DropDownListClass(bool isOpen, bool isDisabled, bool isInvalid);
    string DropDownListPopupClass();
    string DropDownListItemClass(bool isHighlighted, bool isSelected);
    string ComboBoxClass(bool isOpen, bool isDisabled, bool isInvalid);
    string ComboBoxPopupClass();
    string ComboBoxItemClass(bool isHighlighted, bool isSelected);
    string MultiSelectClass(bool isOpen, bool isDisabled, bool isInvalid);
    string MultiSelectPopupClass();
    string MultiSelectItemClass(bool isHighlighted, bool isSelected);
    string MultiSelectTagClass();
    string DropdownPopupClass();

    // ── Forms — Containers ──────────────────────────────────────────────
    string FormClass();
    string FieldClass();
    string LabelClass();
    string InputGroupClass();
    string ValidationMessageClass(ValidationSeverity severity);

    // ── Data Display ────────────────────────────────────────────────────
    string CardClass();
    string CardHeaderClass();
    string CardBodyClass();
    string CardActionsClass();
    string CardFooterClass();
    string CardImageClass();
    string ListClass();
    string ListItemClass();
    string TableClass();
    string AvatarClass(AvatarSize size);
    string BadgeClass(BadgeVariant variant);
    string TooltipClass(TooltipPosition position);
    string TooltipClass(TooltipPosition position, TooltipShowOn showOn);
    string PopoverClass();
    string TimelineClass();
    string TimelineItemClass();
    string CarouselClass();
    string TypographyClass(TypographyVariant variant);

    // ── Feedback ────────────────────────────────────────────────────────
    string AlertClass(AlertSeverity severity);
    string AlertStripClass();
    string ToastClass(ToastSeverity severity);
    string SnackbarClass();
    string SnackbarClass(NotificationVerticalPosition vertical, NotificationHorizontalPosition horizontal);
    string SnackbarHostClass();
    string DialogClass();
    string DialogClass(bool isDraggable);
    string DialogOverlayClass();
    /// <summary>
    /// Returns the HTML markup fragment rendered inside the dialog close button
    /// (e.g., an inline SVG or icon-font span). Each provider chooses its framework's
    /// idiomatic glyph. Bootstrap returns string.Empty because `.btn-close` renders
    /// its own SVG via CSS background.
    /// </summary>
    string DialogCloseMarkup();
    /// <summary>
    /// Returns the CSS class(es) for the dialog close button. Bootstrap returns
    /// "btn-close"; Fluent and Material return their skin-scoped icon-button class.
    /// </summary>
    string DialogCloseButtonClass();
    string ProgressBarClass();
    string ProgressCircleClass();
    string SpinnerClass(SpinnerSize size);
    string SkeletonClass(SkeletonVariant variant);
    string CalloutClass(CalloutType type);

    // ── DataGrid ────────────────────────────────────────────────────────
    string DataGridClass();
    string DataGridTableClass();
    string DataGridHeaderClass();
    string DataGridHeaderCellClass(bool isSortable, bool isSorted);
    string DataGridRowClass(bool isSelected, bool isStriped);
    string DataGridCellClass();
    string DataGridCommandButtonClass();
    string DataGridPagerClass();
    string DataGridToolbarClass();
    string DataGridFilterRowClass();
    string DataGridFilterCellClass();
    string DataGridGroupHeaderClass();

    // ── DataSheet ───────────────────────────────────────────────
    string DataSheetClass(bool isLoading);
    string DataSheetCellClass(CellState state, bool isActive, bool isEditable);
    string DataSheetHeaderCellClass(bool isSortable);
    string DataSheetRowClass(bool isDirty, bool isSelected, bool isDeleted);
    string DataSheetToolbarClass();
    string DataSheetBulkBarClass(bool isVisible);
    string DataSheetSaveFooterClass(int dirtyCount);
    string DataSheetAddButtonClass();
    string DataSheetSaveButtonClass();
    string DataSheetResetButtonClass();
    string DataSheetSpinnerClass();
    string DataSheetDirtyBadgeClass();
    string DataSheetSkeletonClass();
    string DataSheetSkeletonRowClass();
    string DataSheetSkeletonCellClass();
    string DataSheetLoadingTextClass();
    string DataSheetEmptyClass();
    string DataSheetSelectHeaderClass();
    string DataSheetActionsHeaderClass();
    string DataSheetAriaLiveClass();
    string DataSheetSelectCellClass();
    string DataSheetActionsCellClass();
    string DataSheetDeleteButtonClass();
    string DataSheetCellTextClass();
    string DataSheetEditorInputClass();
    string DataSheetEditorSelectClass();
    string DataSheetContentClass();
    string DataSheetScreenReaderOnlyClass();

    // ── ListView ────────────────────────────────────────────────────────
    string ListViewClass();
    string ListViewItemClass(bool isSelected);

    // ── Window ──────────────────────────────────────────────────────────
    string WindowClass(bool isModal);
    string WindowTitleBarClass();
    string WindowContentClass();
    string WindowActionsClass();
    string WindowOverlayClass();
    string WindowFooterClass();

    // ── Editor ──────────────────────────────────────────────────────────
    string EditorClass();
    string EditorToolbarClass();
    string EditorContentClass();

    // ── Upload ──────────────────────────────────────────────────────────
    string UploadClass();
    string UploadFileListClass();
    string UploadFileItemClass();
    string UploadDropZoneClass(bool isActive);

    // ── Charts ──────────────────────────────────────────────────────────
    string ChartContainerClass();
    string GaugeClass();

    // ── Scheduling ──────────────────────────────────────────────────────
    string CalendarClass();
    string SchedulerClass();

    // ── AllocationScheduler ─────────────────────────────────────────────
    string AllocationSchedulerClass();
    string AllocationSchedulerToolbarClass();
    string AllocationSchedulerResourceColumnClass(bool isPinned);
    string AllocationSchedulerTimeHeaderClass(TimeGranularity grain);
    string AllocationSchedulerRowClass(bool isSelected, bool isOverAllocated, bool isStriped = false);
    string AllocationSchedulerCellClass(bool isEditable, bool isSelected, bool isConflict, bool isDisabled, bool isDragTarget);
    string AllocationSchedulerCellValueClass(AllocationValueMode mode);
    string AllocationSchedulerDeltaClass(DeltaDisplayMode mode, bool isOver, bool isUnder);
    string AllocationSchedulerScenarioStripClass();
    string AllocationSchedulerScenarioChipClass(bool isActive, bool isLocked);
    string AllocationSchedulerGhostBarClass();
    string AllocationSchedulerContextMenuClass();
    string AllocationSchedulerEmptyClass();
    string AllocationSchedulerLoaderClass();
    string AllocationSchedulerSplitterClass(bool isDragging, bool isFocused);
    string AllocationSchedulerSplitterRestoreClass(SplitterSide collapsedSide);

    // ── Overlays ────────────────────────────────────────────────────────
    string ModalClass(ModalSize size);
    string ModalOverlayClass();

    // ── SignalR Status ─────────────────────────────────────────────────
    string SignalRStatusClass(AggregateConnectionState state, bool isCompact);
    string SignalRPopupClass();
    string SignalRRowClass(ConnectionHealthState health);
    string SignalRBadgeClass(ConnectionHealthState health);

    // ── ResizableContainer ────────────────────────────────────────────
    string ResizableContainerClass(bool isResizing, bool isDisabled);
    string ResizableContainerContentClass();
    string ResizableContainerHandleClass(ResizeEdges edge, bool isActive, bool isFocused);

    // ── Utility ─────────────────────────────────────────────────────────
    string IconClass(string iconName, IconSize size, IconFlip flip = IconFlip.None, IconThemeColor themeColor = IconThemeColor.Base);
    string DragDropClass();
    string DropZoneClass(bool isActive);
    string ScrollViewClass();
}

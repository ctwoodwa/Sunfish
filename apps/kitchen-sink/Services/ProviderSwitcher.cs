using Sunfish.UICore.Contracts;
using Sunfish.Foundation.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.KitchenSink.Services;

public enum DesignProvider { FluentUI, Bootstrap, Material }

public class ProviderSwitcher : ISunfishCssProvider, ISunfishIconProvider, ISunfishJsInterop
{
    private readonly ISunfishCssProvider _fluentCss;
    private readonly ISunfishIconProvider _fluentIcons;
    private readonly ISunfishJsInterop _fluentJs;
#if BOOTSTRAP_PROVIDER
    private readonly ISunfishCssProvider _bootstrapCss;
    private readonly ISunfishIconProvider _bootstrapIcons;
    private readonly ISunfishJsInterop _bootstrapJs;
#endif
#if MATERIAL_PROVIDER
    private readonly ISunfishCssProvider _materialCss;
    private readonly ISunfishIconProvider _materialIcons;
    private readonly ISunfishJsInterop _materialJs;
#endif

    public DesignProvider ActiveProvider { get; private set; } = DesignProvider.FluentUI;

    public event Action? OnProviderChanged;

    public ProviderSwitcher(
        Sunfish.Providers.FluentUI.FluentUICssProvider fluentCss,
        Sunfish.Providers.FluentUI.FluentUIIconProvider fluentIcons,
        Sunfish.Providers.FluentUI.FluentUIJsInterop fluentJs
#if BOOTSTRAP_PROVIDER
        , Sunfish.Providers.Bootstrap.BootstrapCssProvider bootstrapCss
        , Sunfish.Providers.Bootstrap.BootstrapIconProvider bootstrapIcons
        , Sunfish.Providers.Bootstrap.BootstrapJsInterop bootstrapJs
#endif
#if MATERIAL_PROVIDER
        , Sunfish.Providers.Material.MaterialCssProvider materialCss
        , Sunfish.Providers.Material.MaterialIconProvider materialIcons
        , Sunfish.Providers.Material.MaterialJsInterop materialJs
#endif
    )
    {
        _fluentCss = fluentCss;
        _fluentIcons = fluentIcons;
        _fluentJs = fluentJs;
#if BOOTSTRAP_PROVIDER
        _bootstrapCss = bootstrapCss;
        _bootstrapIcons = bootstrapIcons;
        _bootstrapJs = bootstrapJs;
#endif
#if MATERIAL_PROVIDER
        _materialCss = materialCss;
        _materialIcons = materialIcons;
        _materialJs = materialJs;
#endif
    }

    private ISunfishCssProvider Css => ActiveProvider switch
    {
#if BOOTSTRAP_PROVIDER
        DesignProvider.Bootstrap => _bootstrapCss,
#endif
#if MATERIAL_PROVIDER
        DesignProvider.Material => _materialCss,
#endif
        _ => _fluentCss,
    };
    private ISunfishIconProvider Icons => ActiveProvider switch
    {
#if BOOTSTRAP_PROVIDER
        DesignProvider.Bootstrap => _bootstrapIcons,
#endif
#if MATERIAL_PROVIDER
        DesignProvider.Material => _materialIcons,
#endif
        _ => _fluentIcons,
    };
    private ISunfishJsInterop JsInterop => ActiveProvider switch
    {
#if BOOTSTRAP_PROVIDER
        DesignProvider.Bootstrap => _bootstrapJs,
#endif
#if MATERIAL_PROVIDER
        DesignProvider.Material => _materialJs,
#endif
        _ => _fluentJs,
    };

    public void SetProvider(DesignProvider provider)
    {
        if (ActiveProvider == provider) return;
        ActiveProvider = provider;
        OnProviderChanged?.Invoke();
    }

    // ── ISunfishIconProvider ──
    public string GetIcon(string name, IconSize size = IconSize.Medium) => Icons.GetIcon(name, size);
    public string GetIconSpriteUrl() => Icons.GetIconSpriteUrl();
    public IconRenderMode RenderMode => Icons.RenderMode;
    public string LibraryName => Icons.LibraryName;

    // ── ISunfishJsInterop ──
    public ValueTask InitializeAsync() => JsInterop.InitializeAsync();
    public ValueTask<bool> ShowModalAsync(string modalId) => JsInterop.ShowModalAsync(modalId);
    public ValueTask HideModalAsync(string modalId) => JsInterop.HideModalAsync(modalId);
    public ValueTask<BoundingBox> GetElementBoundsAsync(string elementId) => JsInterop.GetElementBoundsAsync(elementId);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── ISunfishCssProvider — Layout ──
    public string ContainerClass(string? size = null) => Css.ContainerClass(size);
    public string GridClass() => Css.GridClass();
    public string RowClass() => Css.RowClass();
    public string ColumnClass(int? span = null, int? offset = null) => Css.ColumnClass(span, offset);
    public string StackClass(StackDirection orientation) => Css.StackClass(orientation);
    public string DividerClass(bool vertical) => Css.DividerClass(vertical);
    public string PanelClass() => Css.PanelClass();
    public string DrawerClass(DrawerPosition position, bool isOpen) => Css.DrawerClass(position, isOpen);
    public string AppBarClass(AppBarPosition position) => Css.AppBarClass(position);
    public string AccordionClass() => Css.AccordionClass();
    public string AccordionItemClass(bool isExpanded) => Css.AccordionItemClass(isExpanded);
    public string TabsClass(TabPosition position, TabAlignment alignment, TabSize size) => Css.TabsClass(position, alignment, size);
    public string TabClass(bool isActive, bool isDisabled) => Css.TabClass(isActive, isDisabled);
    public string TabPanelClass(bool isActive, bool persistContent) => Css.TabPanelClass(isActive, persistContent);
    public string StepperClass() => Css.StepperClass();
    public string StepClass(StepStatus status) => Css.StepClass(status);
    public string SplitterClass(SplitterOrientation orientation) => Css.SplitterClass(orientation);
    public string DrawerOverlayClass() => Css.DrawerOverlayClass();
    public string ContextMenuClass() => Css.ContextMenuClass();

    // ── Navigation ──
    public string NavBarClass() => Css.NavBarClass();
    public string NavMenuClass() => Css.NavMenuClass();
    public string NavItemClass(bool isActive) => Css.NavItemClass(isActive);
    public string BreadcrumbClass() => Css.BreadcrumbClass();
    public string BreadcrumbItemClass() => Css.BreadcrumbItemClass();
    public string MenuClass() => Css.MenuClass();
    public string MenuItemClass(bool isDisabled) => Css.MenuItemClass(isDisabled);
    public string MenuDividerClass() => Css.MenuDividerClass();
    public string PaginationClass() => Css.PaginationClass();
    public string PaginationItemClass(bool isActive) => Css.PaginationItemClass(isActive);
    public string TreeViewClass() => Css.TreeViewClass();
    public string TreeItemClass(bool isExpanded, bool isSelected) => Css.TreeItemClass(isExpanded, isSelected);
    public string ToolbarClass() => Css.ToolbarClass();
    public string EnvironmentBadgeClass(string env) => Css.EnvironmentBadgeClass(env);
    public string TimeRangeSelectorClass() => Css.TimeRangeSelectorClass();
    public string ToolbarButtonClass(bool isDisabled = false) => Css.ToolbarButtonClass(isDisabled);
    public string ToolbarToggleButtonClass(bool isActive, bool isDisabled = false) => Css.ToolbarToggleButtonClass(isActive, isDisabled);
    public string ToolbarSeparatorClass() => Css.ToolbarSeparatorClass();
    public string ToolbarGroupClass() => Css.ToolbarGroupClass();
    public string LinkClass() => Css.LinkClass();

    // ── Buttons ──
    public string ButtonClass(ButtonVariant variant, ButtonSize size, bool isOutline, bool isDisabled) => Css.ButtonClass(variant, size, isOutline, isDisabled);
    public string ButtonClass(ButtonVariant variant, ButtonSize size, FillMode fillMode, RoundedMode rounded, bool isDisabled) => Css.ButtonClass(variant, size, fillMode, rounded, isDisabled);
    public string IconButtonClass(ButtonSize size) => Css.IconButtonClass(size);
    public string ButtonGroupClass() => Css.ButtonGroupClass();
    public string ToggleButtonClass(bool selected) => Css.ToggleButtonClass(selected);
    public string SplitButtonClass() => Css.SplitButtonClass();
    public string ChipClass(ChipVariant variant, bool isSelected) => Css.ChipClass(variant, isSelected);
    public string ChipSetClass() => Css.ChipSetClass();
    public string FabClass(FabSize size) => Css.FabClass(size);

    // ── Forms — Inputs ──
    public string TextBoxClass(bool isInvalid, bool isDisabled) => Css.TextBoxClass(isInvalid, isDisabled);
    public string TextAreaClass(bool isInvalid) => Css.TextAreaClass(isInvalid);
    public string NumericInputClass() => Css.NumericInputClass();
    public string SearchBoxClass() => Css.SearchBoxClass();
    public string AutocompleteClass() => Css.AutocompleteClass();
    public string AutocompleteClass(bool isOpen, bool isDisabled, bool isInvalid) => Css.AutocompleteClass(isOpen, isDisabled, isInvalid);
    public string AutocompleteItemClass(bool isHighlighted, bool isSelected) => Css.AutocompleteItemClass(isHighlighted, isSelected);
    public string SelectClass(bool isInvalid) => Css.SelectClass(isInvalid);
    public string CheckboxClass(bool isChecked) => Css.CheckboxClass(isChecked);
    public string RadioClass(bool isSelected) => Css.RadioClass(isSelected);
    public string RadioGroupClass() => Css.RadioGroupClass();
    public string SwitchClass(bool isOn) => Css.SwitchClass(isOn);
    public string SliderClass() => Css.SliderClass();
    public string SliderClass(SliderOrientation orientation) => Css.SliderClass(orientation);
    public string RatingClass() => Css.RatingClass();
    public string ColorPickerClass() => Css.ColorPickerClass();
    public string ColorPickerPopupClass() => Css.ColorPickerPopupClass();
    public string ColorGradientClass() => Css.ColorGradientClass();
    public string ColorPaletteClass() => Css.ColorPaletteClass();
    public string FlatColorPickerClass() => Css.FlatColorPickerClass();
    public string DatePickerClass() => Css.DatePickerClass();
    public string TimePickerClass() => Css.TimePickerClass();
    public string TimePickerPopupClass() => Css.TimePickerPopupClass();
    public string FileUploadClass() => Css.FileUploadClass();
    public string FileUploadFileListClass() => Css.FileUploadFileListClass();
    public string FileUploadDropZoneClass(bool isDragOver, bool isDisabled) => Css.FileUploadDropZoneClass(isDragOver, isDisabled);

    // ── Selection / Dropdowns ──
    public string DropDownListClass(bool isOpen, bool isDisabled, bool isInvalid) => Css.DropDownListClass(isOpen, isDisabled, isInvalid);
    public string DropDownListPopupClass() => Css.DropDownListPopupClass();
    public string DropDownListItemClass(bool isHighlighted, bool isSelected) => Css.DropDownListItemClass(isHighlighted, isSelected);
    public string ComboBoxClass(bool isOpen, bool isDisabled, bool isInvalid) => Css.ComboBoxClass(isOpen, isDisabled, isInvalid);
    public string ComboBoxPopupClass() => Css.ComboBoxPopupClass();
    public string ComboBoxItemClass(bool isHighlighted, bool isSelected) => Css.ComboBoxItemClass(isHighlighted, isSelected);
    public string MultiSelectClass(bool isOpen, bool isDisabled, bool isInvalid) => Css.MultiSelectClass(isOpen, isDisabled, isInvalid);
    public string MultiSelectPopupClass() => Css.MultiSelectPopupClass();
    public string MultiSelectItemClass(bool isHighlighted, bool isSelected) => Css.MultiSelectItemClass(isHighlighted, isSelected);
    public string MultiSelectTagClass() => Css.MultiSelectTagClass();
    public string DropdownPopupClass() => Css.DropdownPopupClass();

    // ── Forms — Containers ──
    public string FormClass() => Css.FormClass();
    public string FieldClass() => Css.FieldClass();
    public string LabelClass() => Css.LabelClass();
    public string InputGroupClass() => Css.InputGroupClass();
    public string ValidationMessageClass(ValidationSeverity severity) => Css.ValidationMessageClass(severity);

    // ── Data Display ──
    public string CardClass() => Css.CardClass();
    public string CardHeaderClass() => Css.CardHeaderClass();
    public string CardBodyClass() => Css.CardBodyClass();
    public string CardActionsClass() => Css.CardActionsClass();
    public string CardFooterClass() => Css.CardFooterClass();
    public string CardImageClass() => Css.CardImageClass();
    public string ListClass() => Css.ListClass();
    public string ListItemClass() => Css.ListItemClass();
    public string TableClass() => Css.TableClass();
    public string AvatarClass(AvatarSize size) => Css.AvatarClass(size);
    public string BadgeClass(BadgeVariant variant) => Css.BadgeClass(variant);
    public string TooltipClass(TooltipPosition position) => Css.TooltipClass(position);
    public string TooltipClass(TooltipPosition position, TooltipShowOn showOn) => Css.TooltipClass(position, showOn);
    public string PopoverClass() => Css.PopoverClass();
    public string TimelineClass() => Css.TimelineClass();
    public string TimelineItemClass() => Css.TimelineItemClass();
    public string CarouselClass() => Css.CarouselClass();
    public string TypographyClass(TypographyVariant variant) => Css.TypographyClass(variant);

    // ── Feedback ──
    public string AlertClass(AlertSeverity severity) => Css.AlertClass(severity);
    public string AlertStripClass() => Css.AlertStripClass();
    public string ToastClass(ToastSeverity severity) => Css.ToastClass(severity);
    public string SnackbarClass() => Css.SnackbarClass();
    public string SnackbarClass(NotificationVerticalPosition vertical, NotificationHorizontalPosition horizontal) => Css.SnackbarClass(vertical, horizontal);
    public string SnackbarHostClass() => Css.SnackbarHostClass();
    public string DialogClass() => Css.DialogClass();
    public string DialogClass(bool isDraggable) => Css.DialogClass(isDraggable);
    public string DialogOverlayClass() => Css.DialogOverlayClass();
    public string ProgressBarClass() => Css.ProgressBarClass();
    public string ProgressCircleClass() => Css.ProgressCircleClass();
    public string SpinnerClass(SpinnerSize size) => Css.SpinnerClass(size);
    public string SkeletonClass(SkeletonVariant variant) => Css.SkeletonClass(variant);
    public string CalloutClass(CalloutType type) => Css.CalloutClass(type);

    // ── DataGrid ──
    public string DataGridClass() => Css.DataGridClass();
    public string DataGridHeaderClass() => Css.DataGridHeaderClass();
    public string DataGridHeaderCellClass(bool isSortable, bool isSorted) => Css.DataGridHeaderCellClass(isSortable, isSorted);
    public string DataGridRowClass(bool isSelected, bool isStriped) => Css.DataGridRowClass(isSelected, isStriped);
    public string DataGridCellClass() => Css.DataGridCellClass();
    public string DataGridPagerClass() => Css.DataGridPagerClass();
    public string DataGridToolbarClass() => Css.DataGridToolbarClass();
    public string DataGridFilterRowClass() => Css.DataGridFilterRowClass();
    public string DataGridFilterCellClass() => Css.DataGridFilterCellClass();
    public string DataGridGroupHeaderClass() => Css.DataGridGroupHeaderClass();

    // ── DataSheet ──
    public string DataSheetClass(bool isLoading) => Css.DataSheetClass(isLoading);
    public string DataSheetCellClass(CellState state, bool isActive, bool isEditable) => Css.DataSheetCellClass(state, isActive, isEditable);
    public string DataSheetHeaderCellClass(bool isSortable) => Css.DataSheetHeaderCellClass(isSortable);
    public string DataSheetRowClass(bool isDirty, bool isSelected, bool isDeleted) => Css.DataSheetRowClass(isDirty, isSelected, isDeleted);
    public string DataSheetToolbarClass() => Css.DataSheetToolbarClass();
    public string DataSheetBulkBarClass(bool isVisible) => Css.DataSheetBulkBarClass(isVisible);
    public string DataSheetSaveFooterClass(int dirtyCount) => Css.DataSheetSaveFooterClass(dirtyCount);
    public string DataSheetAddButtonClass() => Css.DataSheetAddButtonClass();
    public string DataSheetSaveButtonClass() => Css.DataSheetSaveButtonClass();
    public string DataSheetResetButtonClass() => Css.DataSheetResetButtonClass();
    public string DataSheetSpinnerClass() => Css.DataSheetSpinnerClass();
    public string DataSheetDirtyBadgeClass() => Css.DataSheetDirtyBadgeClass();
    public string DataSheetSkeletonClass() => Css.DataSheetSkeletonClass();
    public string DataSheetSkeletonRowClass() => Css.DataSheetSkeletonRowClass();
    public string DataSheetSkeletonCellClass() => Css.DataSheetSkeletonCellClass();
    public string DataSheetLoadingTextClass() => Css.DataSheetLoadingTextClass();
    public string DataSheetEmptyClass() => Css.DataSheetEmptyClass();
    public string DataSheetSelectHeaderClass() => Css.DataSheetSelectHeaderClass();
    public string DataSheetActionsHeaderClass() => Css.DataSheetActionsHeaderClass();
    public string DataSheetAriaLiveClass() => Css.DataSheetAriaLiveClass();
    public string DataSheetSelectCellClass() => Css.DataSheetSelectCellClass();
    public string DataSheetActionsCellClass() => Css.DataSheetActionsCellClass();
    public string DataSheetDeleteButtonClass() => Css.DataSheetDeleteButtonClass();
    public string DataSheetCellTextClass() => Css.DataSheetCellTextClass();
    public string DataSheetEditorInputClass() => Css.DataSheetEditorInputClass();
    public string DataSheetEditorSelectClass() => Css.DataSheetEditorSelectClass();
    public string DataSheetContentClass() => Css.DataSheetContentClass();
    public string DataSheetScreenReaderOnlyClass() => Css.DataSheetScreenReaderOnlyClass();

    // ── ListView ──
    public string ListViewClass() => Css.ListViewClass();
    public string ListViewItemClass(bool isSelected) => Css.ListViewItemClass(isSelected);

    // ── Window ──
    public string WindowClass(bool isModal) => Css.WindowClass(isModal);
    public string WindowTitleBarClass() => Css.WindowTitleBarClass();
    public string WindowContentClass() => Css.WindowContentClass();
    public string WindowActionsClass() => Css.WindowActionsClass();
    public string WindowOverlayClass() => Css.WindowOverlayClass();
    public string WindowFooterClass() => Css.WindowFooterClass();

    // ── Editor ──
    public string EditorClass() => Css.EditorClass();
    public string EditorToolbarClass() => Css.EditorToolbarClass();
    public string EditorContentClass() => Css.EditorContentClass();

    // ── Upload ──
    public string UploadClass() => Css.UploadClass();
    public string UploadFileListClass() => Css.UploadFileListClass();
    public string UploadFileItemClass() => Css.UploadFileItemClass();
    public string UploadDropZoneClass(bool isActive) => Css.UploadDropZoneClass(isActive);

    // ── Charts ──
    public string ChartContainerClass() => Css.ChartContainerClass();
    public string GaugeClass() => Css.GaugeClass();

    // ── Scheduling ──
    public string CalendarClass() => Css.CalendarClass();
    public string SchedulerClass() => Css.SchedulerClass();

    // ── DateRangePicker / DateTimePicker ──
    public string DateRangePickerClass() => Css.DateRangePickerClass();
    public string DateRangePickerPopupClass() => Css.DateRangePickerPopupClass();
    public string DateTimePickerClass() => Css.DateTimePickerClass();
    public string DateTimePickerPopupClass() => Css.DateTimePickerPopupClass();

    // ── AllocationScheduler ──
    public string AllocationSchedulerClass() => Css.AllocationSchedulerClass();
    public string AllocationSchedulerToolbarClass() => Css.AllocationSchedulerToolbarClass();
    public string AllocationSchedulerResourceColumnClass(bool isPinned) => Css.AllocationSchedulerResourceColumnClass(isPinned);
    public string AllocationSchedulerTimeHeaderClass(TimeGranularity grain) => Css.AllocationSchedulerTimeHeaderClass(grain);
    public string AllocationSchedulerRowClass(bool isSelected, bool isOverAllocated, bool isStriped = false) => Css.AllocationSchedulerRowClass(isSelected, isOverAllocated, isStriped);
    public string AllocationSchedulerCellClass(bool isEditable, bool isSelected, bool isConflict, bool isDisabled, bool isDragTarget) => Css.AllocationSchedulerCellClass(isEditable, isSelected, isConflict, isDisabled, isDragTarget);
    public string AllocationSchedulerCellValueClass(AllocationValueMode mode) => Css.AllocationSchedulerCellValueClass(mode);
    public string AllocationSchedulerDeltaClass(DeltaDisplayMode mode, bool isOver, bool isUnder) => Css.AllocationSchedulerDeltaClass(mode, isOver, isUnder);
    public string AllocationSchedulerScenarioStripClass() => Css.AllocationSchedulerScenarioStripClass();
    public string AllocationSchedulerScenarioChipClass(bool isActive, bool isLocked) => Css.AllocationSchedulerScenarioChipClass(isActive, isLocked);
    public string AllocationSchedulerGhostBarClass() => Css.AllocationSchedulerGhostBarClass();
    public string AllocationSchedulerContextMenuClass() => Css.AllocationSchedulerContextMenuClass();
    public string AllocationSchedulerEmptyClass() => Css.AllocationSchedulerEmptyClass();
    public string AllocationSchedulerLoaderClass() => Css.AllocationSchedulerLoaderClass();
    public string AllocationSchedulerSplitterClass(bool isDragging, bool isFocused) => Css.AllocationSchedulerSplitterClass(isDragging, isFocused);
    public string AllocationSchedulerSplitterRestoreClass(SplitterSide collapsedSide) => Css.AllocationSchedulerSplitterRestoreClass(collapsedSide);

    // ── Overlays ──
    public string ModalClass(ModalSize size) => Css.ModalClass(size);
    public string ModalOverlayClass() => Css.ModalOverlayClass();

    // ── SignalR Status ──
    public string SignalRStatusClass(AggregateConnectionState state, bool isCompact) => Css.SignalRStatusClass(state, isCompact);
    public string SignalRPopupClass() => Css.SignalRPopupClass();
    public string SignalRRowClass(ConnectionHealthState health) => Css.SignalRRowClass(health);
    public string SignalRBadgeClass(ConnectionHealthState health) => Css.SignalRBadgeClass(health);

    // ── ResizableContainer ──
    public string ResizableContainerClass(bool isResizing, bool isDisabled) => Css.ResizableContainerClass(isResizing, isDisabled);
    public string ResizableContainerContentClass() => Css.ResizableContainerContentClass();
    public string ResizableContainerHandleClass(ResizeEdges edge, bool isActive, bool isFocused) => Css.ResizableContainerHandleClass(edge, isActive, isFocused);

    // ── Utility ──
    public string IconClass(string iconName, IconSize size, IconFlip flip = IconFlip.None, IconThemeColor themeColor = IconThemeColor.Base) => Css.IconClass(iconName, size, flip, themeColor);
    public string DragDropClass() => Css.DragDropClass();
    public string DropZoneClass(bool isActive) => Css.DropZoneClass(isActive);
    public string ScrollViewClass() => Css.ScrollViewClass();
}

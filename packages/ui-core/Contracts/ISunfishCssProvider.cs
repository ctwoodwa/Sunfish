using Sunfish.Foundation.Enums;

namespace Sunfish.UICore.Contracts;

/// <summary>
/// Defines the contract for a design-system CSS provider. Each implementation
/// (e.g., Fluent UI, Material, Bootstrap) maps Sunfish component states to its
/// own CSS class names, keeping component logic design-system-agnostic.
/// </summary>
/// <remarks>
/// <para>
/// Methods on this contract return raw class-name strings (one or more
/// space-separated tokens) suitable for direct inclusion in an element's
/// <c>class</c> attribute. They must never return <c>null</c>; providers that
/// have nothing to contribute for a given slot return <see cref="string.Empty"/>.
/// </para>
/// <para>
/// The contract is intentionally large: each Sunfish component has dedicated
/// hooks so providers can target idiomatic vocabulary (e.g., Bootstrap's
/// <c>modal-*</c> classes vs. Fluent's <c>fui-Dialog*</c> classes) without
/// component code branching on the active design system.
/// </para>
/// </remarks>
public interface ISunfishCssProvider
{
    // TODO(phase-2-followup): split by category into ISunfishButtonCssProvider, ISunfishFormCssProvider, etc.
    // ── Layout ──────────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a layout container.</summary>
    /// <param name="size">Optional size token (e.g., <c>"sm"</c>, <c>"lg"</c>, <c>"fluid"</c>) used by some skins to switch container width.</param>
    string ContainerClass(string? size = null);

    /// <summary>Returns the CSS class(es) for a grid root.</summary>
    string GridClass();

    /// <summary>Returns the CSS class(es) for a grid row.</summary>
    string RowClass();

    /// <summary>Returns the CSS class(es) for a grid column.</summary>
    /// <param name="span">Optional column span (1-12 in 12-column grids).</param>
    /// <param name="offset">Optional column offset.</param>
    string ColumnClass(int? span = null, int? offset = null);

    /// <summary>Returns the CSS class(es) for a stack layout.</summary>
    /// <param name="orientation">Whether the stack flows horizontally or vertically.</param>
    string StackClass(StackDirection orientation);

    /// <summary>Returns the CSS class(es) for a divider element.</summary>
    /// <param name="vertical"><c>true</c> for a vertical divider; <c>false</c> for horizontal.</param>
    string DividerClass(bool vertical);

    /// <summary>Returns the CSS class(es) for a panel (sectioned surface).</summary>
    string PanelClass();

    /// <summary>Returns the CSS class(es) for a drawer (slide-in panel).</summary>
    /// <param name="position">Edge of the viewport the drawer is anchored to.</param>
    /// <param name="isOpen"><c>true</c> when the drawer is in its open state.</param>
    string DrawerClass(DrawerPosition position, bool isOpen);

    /// <summary>Returns the CSS class(es) for an app bar.</summary>
    /// <param name="position">Whether the app bar is anchored to the top or bottom.</param>
    string AppBarClass(AppBarPosition position);

    /// <summary>Returns the CSS class(es) for an accordion root.</summary>
    string AccordionClass();

    /// <summary>Returns the CSS class(es) for a single accordion item.</summary>
    /// <param name="isExpanded"><c>true</c> when the item is currently expanded.</param>
    string AccordionItemClass(bool isExpanded);

    /// <summary>Returns the CSS class(es) for a tab strip root.</summary>
    /// <param name="position">Edge the tab strip is anchored to (top, bottom, left, right).</param>
    /// <param name="alignment">Alignment of tabs within the strip.</param>
    /// <param name="size">Tab size token.</param>
    string TabsClass(TabPosition position, TabAlignment alignment, TabSize size);

    /// <summary>Returns the CSS class(es) for a single tab header.</summary>
    /// <param name="isActive"><c>true</c> when this tab is the selected one.</param>
    /// <param name="isDisabled"><c>true</c> when the tab is disabled.</param>
    string TabClass(bool isActive, bool isDisabled);

    /// <summary>Returns the CSS class(es) for a tab panel (the body shown for a tab).</summary>
    /// <param name="isActive"><c>true</c> when this panel is the visible one.</param>
    /// <param name="persistContent"><c>true</c> when inactive panels remain in the DOM (hidden via CSS) instead of being removed.</param>
    string TabPanelClass(bool isActive, bool persistContent);

    /// <summary>Returns the CSS class(es) for a stepper root (multi-step wizard).</summary>
    string StepperClass();

    /// <summary>Returns the CSS class(es) for a single step in a stepper.</summary>
    /// <param name="status">The step's progress status (pending, active, completed, error).</param>
    string StepClass(StepStatus status);

    /// <summary>Returns the CSS class(es) for a splitter (resizable pane separator).</summary>
    /// <param name="orientation">Whether the splitter divides horizontally or vertically.</param>
    string SplitterClass(SplitterOrientation orientation);

    /// <summary>Returns the CSS class(es) for the modal overlay rendered behind a drawer.</summary>
    string DrawerOverlayClass();

    /// <summary>Returns the CSS class(es) for a context menu (right-click / long-press menu).</summary>
    string ContextMenuClass();

    // ── Navigation ──────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a top-level navigation bar.</summary>
    string NavBarClass();

    /// <summary>Returns the CSS class(es) for a navigation menu (typically the side rail).</summary>
    string NavMenuClass();

    /// <summary>Returns the CSS class(es) for a single navigation item.</summary>
    /// <param name="isActive"><c>true</c> when the item represents the current route.</param>
    string NavItemClass(bool isActive);

    /// <summary>Returns the CSS class(es) for a breadcrumb trail root.</summary>
    string BreadcrumbClass();

    /// <summary>Returns the CSS class(es) for a single breadcrumb item.</summary>
    string BreadcrumbItemClass();

    /// <summary>Returns the CSS class(es) for a generic menu container.</summary>
    string MenuClass();

    /// <summary>Returns the CSS class(es) for a single menu item.</summary>
    /// <param name="isDisabled"><c>true</c> when the menu item is disabled.</param>
    string MenuItemClass(bool isDisabled);

    /// <summary>Returns the CSS class(es) for a menu divider (separator line).</summary>
    string MenuDividerClass();

    /// <summary>Returns the CSS class(es) for a pagination control root.</summary>
    string PaginationClass();

    /// <summary>Returns the CSS class(es) for a single pagination item.</summary>
    /// <param name="isActive"><c>true</c> when the item is the currently selected page.</param>
    string PaginationItemClass(bool isActive);

    /// <summary>Returns the CSS class(es) for a tree view root.</summary>
    string TreeViewClass();

    /// <summary>Returns the CSS class(es) for a single tree item.</summary>
    /// <param name="isExpanded"><c>true</c> when the item's children are visible.</param>
    /// <param name="isSelected"><c>true</c> when the item is selected.</param>
    string TreeItemClass(bool isExpanded, bool isSelected);

    /// <summary>Returns the CSS class(es) for a toolbar container.</summary>
    string ToolbarClass();

    /// <summary>Returns the CSS class(es) for an environment badge (e.g., "DEV", "STAGING").</summary>
    /// <param name="env">The environment label, used by some skins to vary color (e.g., red for prod).</param>
    string EnvironmentBadgeClass(string env);

    /// <summary>Returns the CSS class(es) for a time-range selector control.</summary>
    string TimeRangeSelectorClass();

    /// <summary>Returns the CSS class(es) for a button hosted inside a toolbar.</summary>
    /// <param name="isDisabled"><c>true</c> when the button is disabled.</param>
    string ToolbarButtonClass(bool isDisabled = false);

    /// <summary>Returns the CSS class(es) for a toggle button hosted inside a toolbar.</summary>
    /// <param name="isActive"><c>true</c> when the toggle is in the on/pressed state.</param>
    /// <param name="isDisabled"><c>true</c> when the toggle is disabled.</param>
    string ToolbarToggleButtonClass(bool isActive, bool isDisabled = false);

    /// <summary>Returns the CSS class(es) for a toolbar separator (vertical rule).</summary>
    string ToolbarSeparatorClass();

    /// <summary>Returns the CSS class(es) for a toolbar group wrapper (groups related buttons).</summary>
    string ToolbarGroupClass();

    /// <summary>Returns the CSS class(es) for an inline hyperlink.</summary>
    string LinkClass();

    // ── Buttons ─────────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a button (legacy outline overload).</summary>
    /// <param name="variant">Semantic variant (primary, secondary, danger, etc.).</param>
    /// <param name="size">Button size token.</param>
    /// <param name="isOutline"><c>true</c> for an outline button; <c>false</c> for a filled button.</param>
    /// <param name="isDisabled"><c>true</c> when the button is disabled.</param>
    /// <remarks>Prefer the <see cref="ButtonClass(ButtonVariant, ButtonSize, FillMode, RoundedMode, bool)"/> overload — it carries the richer fill / rounded vocabulary used by post-Telerik-parity skins.</remarks>
    string ButtonClass(ButtonVariant variant, ButtonSize size, bool isOutline, bool isDisabled);

    /// <summary>Returns the CSS class(es) for a button.</summary>
    /// <param name="variant">Semantic variant (primary, secondary, danger, etc.).</param>
    /// <param name="size">Button size token.</param>
    /// <param name="fillMode">Fill style (solid, outline, flat, link).</param>
    /// <param name="rounded">Corner-radius mode.</param>
    /// <param name="isDisabled"><c>true</c> when the button is disabled.</param>
    string ButtonClass(ButtonVariant variant, ButtonSize size, FillMode fillMode, RoundedMode rounded, bool isDisabled);

    /// <summary>Returns the CSS class(es) for an icon-only button.</summary>
    /// <param name="size">Button size token.</param>
    string IconButtonClass(ButtonSize size);

    /// <summary>Returns the CSS class(es) for a button group (segmented bar of related buttons).</summary>
    string ButtonGroupClass();

    /// <summary>Returns the CSS class(es) for a toggle button.</summary>
    /// <param name="selected"><c>true</c> when the toggle is in its pressed/on state.</param>
    string ToggleButtonClass(bool selected);

    /// <summary>Returns the CSS class(es) for a split button (primary action plus a dropdown caret).</summary>
    string SplitButtonClass();

    /// <summary>Returns the CSS class(es) for a chip (compact entity / filter pill).</summary>
    /// <param name="variant">Visual variant of the chip.</param>
    /// <param name="isSelected"><c>true</c> when the chip is selected.</param>
    string ChipClass(ChipVariant variant, bool isSelected);

    /// <summary>Returns the CSS class(es) for a chip set (group of chips).</summary>
    string ChipSetClass();

    /// <summary>Returns the CSS class(es) for a floating action button.</summary>
    /// <param name="size">FAB size token.</param>
    string FabClass(FabSize size);

    // ── Forms — Inputs ──────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a single-line text input.</summary>
    /// <param name="isInvalid"><c>true</c> when the input is in a validation-error state.</param>
    /// <param name="isDisabled"><c>true</c> when the input is disabled.</param>
    string TextBoxClass(bool isInvalid, bool isDisabled);

    /// <summary>Returns the CSS class(es) for a multi-line text area.</summary>
    /// <param name="isInvalid"><c>true</c> when the input is in a validation-error state.</param>
    string TextAreaClass(bool isInvalid);

    /// <summary>Returns the CSS class(es) for a numeric input (with steppers).</summary>
    string NumericInputClass();

    /// <summary>Returns the CSS class(es) for a search box (text input with search affordance).</summary>
    string SearchBoxClass();

    /// <summary>Returns the CSS class(es) for an autocomplete input root (no state).</summary>
    string AutocompleteClass();

    /// <summary>Returns the CSS class(es) for an autocomplete input root with state.</summary>
    /// <param name="isOpen"><c>true</c> when the suggestion popup is shown.</param>
    /// <param name="isDisabled"><c>true</c> when the input is disabled.</param>
    /// <param name="isInvalid"><c>true</c> when the input is in a validation-error state.</param>
    string AutocompleteClass(bool isOpen, bool isDisabled, bool isInvalid);

    /// <summary>Returns the CSS class(es) for a single suggestion row in an autocomplete popup.</summary>
    /// <param name="isHighlighted"><c>true</c> when the row is keyboard-focused.</param>
    /// <param name="isSelected"><c>true</c> when the row is the chosen value.</param>
    string AutocompleteItemClass(bool isHighlighted, bool isSelected);

    /// <summary>Returns the CSS class(es) for a native-style select element.</summary>
    /// <param name="isInvalid"><c>true</c> when the input is in a validation-error state.</param>
    string SelectClass(bool isInvalid);

    /// <summary>Returns the CSS class(es) for a checkbox.</summary>
    /// <param name="isChecked"><c>true</c> when the checkbox is checked.</param>
    string CheckboxClass(bool isChecked);

    /// <summary>Returns the CSS class(es) for a single radio button.</summary>
    /// <param name="isSelected"><c>true</c> when the radio is the chosen option.</param>
    string RadioClass(bool isSelected);

    /// <summary>Returns the CSS class(es) for a radio group container.</summary>
    string RadioGroupClass();

    /// <summary>Returns the CSS class(es) for a switch (binary on/off toggle).</summary>
    /// <param name="isOn"><c>true</c> when the switch is in its on state.</param>
    string SwitchClass(bool isOn);

    /// <summary>Returns the CSS class(es) for a slider (range input) — default orientation.</summary>
    string SliderClass();

    /// <summary>Returns the CSS class(es) for a slider with explicit orientation.</summary>
    /// <param name="orientation">Whether the slider lays out horizontally or vertically.</param>
    string SliderClass(SliderOrientation orientation);

    /// <summary>Returns the CSS class(es) for a star/rating control.</summary>
    string RatingClass();

    /// <summary>Returns the CSS class(es) for a color picker root.</summary>
    string ColorPickerClass();

    /// <summary>Returns the CSS class(es) for the popup surface of a color picker.</summary>
    string ColorPickerPopupClass();

    /// <summary>Returns the CSS class(es) for a 2-D color gradient surface (saturation / value plane).</summary>
    string ColorGradientClass();

    /// <summary>Returns the CSS class(es) for a fixed color palette (preset swatches).</summary>
    string ColorPaletteClass();

    /// <summary>Returns the CSS class(es) for a flat color picker (palette + sliders, no popup).</summary>
    string FlatColorPickerClass();

    /// <summary>Returns the CSS class(es) for a date picker root.</summary>
    string DatePickerClass();

    /// <summary>Returns the CSS class(es) for a time picker root.</summary>
    string TimePickerClass();

    /// <summary>Returns the CSS class(es) for the popup surface of a time picker.</summary>
    string TimePickerPopupClass();

    /// <summary>Returns the CSS class(es) for a date-range picker root.</summary>
    string DateRangePickerClass();

    /// <summary>Returns the CSS class(es) for the popup surface of a date-range picker.</summary>
    string DateRangePickerPopupClass();

    /// <summary>Returns the CSS class(es) for a date-time picker root.</summary>
    string DateTimePickerClass();

    /// <summary>Returns the CSS class(es) for the popup surface of a date-time picker.</summary>
    string DateTimePickerPopupClass();

    /// <summary>Returns the CSS class(es) for a file upload root.</summary>
    string FileUploadClass();

    /// <summary>Returns the CSS class(es) for the file list shown beneath a file upload.</summary>
    string FileUploadFileListClass();

    /// <summary>Returns the CSS class(es) for the drop-zone region of a file upload.</summary>
    /// <param name="isDragOver"><c>true</c> while a drag operation is hovering the zone.</param>
    /// <param name="isDisabled"><c>true</c> when uploads are disabled.</param>
    string FileUploadDropZoneClass(bool isDragOver, bool isDisabled);

    // ── Selection / Dropdowns ───────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a drop-down list root.</summary>
    /// <param name="isOpen"><c>true</c> when the popup is shown.</param>
    /// <param name="isDisabled"><c>true</c> when the control is disabled.</param>
    /// <param name="isInvalid"><c>true</c> when the control is in a validation-error state.</param>
    string DropDownListClass(bool isOpen, bool isDisabled, bool isInvalid);

    /// <summary>Returns the CSS class(es) for the popup surface of a drop-down list.</summary>
    string DropDownListPopupClass();

    /// <summary>Returns the CSS class(es) for a single item in a drop-down list popup.</summary>
    /// <param name="isHighlighted"><c>true</c> when the item is keyboard-focused.</param>
    /// <param name="isSelected"><c>true</c> when the item is the chosen value.</param>
    string DropDownListItemClass(bool isHighlighted, bool isSelected);

    /// <summary>Returns the CSS class(es) for a combo box root.</summary>
    /// <param name="isOpen"><c>true</c> when the popup is shown.</param>
    /// <param name="isDisabled"><c>true</c> when the control is disabled.</param>
    /// <param name="isInvalid"><c>true</c> when the control is in a validation-error state.</param>
    string ComboBoxClass(bool isOpen, bool isDisabled, bool isInvalid);

    /// <summary>Returns the CSS class(es) for the popup surface of a combo box.</summary>
    string ComboBoxPopupClass();

    /// <summary>Returns the CSS class(es) for a single item in a combo-box popup.</summary>
    /// <param name="isHighlighted"><c>true</c> when the item is keyboard-focused.</param>
    /// <param name="isSelected"><c>true</c> when the item is the chosen value.</param>
    string ComboBoxItemClass(bool isHighlighted, bool isSelected);

    /// <summary>Returns the CSS class(es) for a multi-select root.</summary>
    /// <param name="isOpen"><c>true</c> when the popup is shown.</param>
    /// <param name="isDisabled"><c>true</c> when the control is disabled.</param>
    /// <param name="isInvalid"><c>true</c> when the control is in a validation-error state.</param>
    string MultiSelectClass(bool isOpen, bool isDisabled, bool isInvalid);

    /// <summary>Returns the CSS class(es) for the popup surface of a multi-select.</summary>
    string MultiSelectPopupClass();

    /// <summary>Returns the CSS class(es) for a single item in a multi-select popup.</summary>
    /// <param name="isHighlighted"><c>true</c> when the item is keyboard-focused.</param>
    /// <param name="isSelected"><c>true</c> when the item is one of the chosen values.</param>
    string MultiSelectItemClass(bool isHighlighted, bool isSelected);

    /// <summary>Returns the CSS class(es) for a removable tag pill in a multi-select.</summary>
    string MultiSelectTagClass();

    /// <summary>Returns the CSS class(es) for a generic dropdown popup wrapper (used by ad-hoc menus).</summary>
    string DropdownPopupClass();

    // ── Forms — Containers ──────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a form root element.</summary>
    string FormClass();

    /// <summary>Returns the CSS class(es) for a form field (label + input + validation message wrapper).</summary>
    string FieldClass();

    /// <summary>Returns the CSS class(es) for a form label element.</summary>
    string LabelClass();

    /// <summary>Returns the CSS class(es) for an input group (input plus addons / prefixes / suffixes).</summary>
    string InputGroupClass();

    /// <summary>Returns the CSS class(es) for a per-field validation message.</summary>
    /// <param name="severity">Severity tier (info, warning, error).</param>
    string ValidationMessageClass(ValidationSeverity severity);

    // ── Data Display ────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a card root (elevated surface).</summary>
    string CardClass();

    /// <summary>Returns the CSS class(es) for a card header slot.</summary>
    string CardHeaderClass();

    /// <summary>Returns the CSS class(es) for a card body slot.</summary>
    string CardBodyClass();

    /// <summary>Returns the CSS class(es) for the action row inside a card (primary/secondary buttons).</summary>
    string CardActionsClass();

    /// <summary>Returns the CSS class(es) for a card footer slot.</summary>
    string CardFooterClass();

    /// <summary>Returns the CSS class(es) for an image hosted inside a card.</summary>
    string CardImageClass();

    /// <summary>Returns the CSS class(es) for a list root (vertical list of items).</summary>
    string ListClass();

    /// <summary>Returns the CSS class(es) for a single list item.</summary>
    string ListItemClass();

    /// <summary>Returns the CSS class(es) for a generic table element.</summary>
    string TableClass();

    /// <summary>Returns the CSS class(es) for an avatar (user/entity image circle).</summary>
    /// <param name="size">Avatar size token.</param>
    string AvatarClass(AvatarSize size);

    /// <summary>Returns the CSS class(es) for a badge (small status / count pill).</summary>
    /// <param name="variant">Visual variant of the badge.</param>
    string BadgeClass(BadgeVariant variant);

    /// <summary>Returns the CSS class(es) for a tooltip (hover-only).</summary>
    /// <param name="position">Edge of the trigger the tooltip is anchored to.</param>
    string TooltipClass(TooltipPosition position);

    /// <summary>Returns the CSS class(es) for a tooltip with explicit show-on trigger.</summary>
    /// <param name="position">Edge of the trigger the tooltip is anchored to.</param>
    /// <param name="showOn">Trigger that causes the tooltip to appear (hover, focus, click).</param>
    string TooltipClass(TooltipPosition position, TooltipShowOn showOn);

    /// <summary>Returns the CSS class(es) for a popover (rich, click-anchored bubble).</summary>
    string PopoverClass();

    /// <summary>Returns the CSS class(es) for a timeline root (vertical sequence of events).</summary>
    string TimelineClass();

    /// <summary>Returns the CSS class(es) for a single timeline item.</summary>
    string TimelineItemClass();

    /// <summary>Returns the CSS class(es) for a carousel root (rotating slide deck).</summary>
    string CarouselClass();

    /// <summary>Returns the CSS class(es) for a typography element (h1, body, caption, etc.).</summary>
    /// <param name="variant">The typography variant to style.</param>
    string TypographyClass(TypographyVariant variant);

    // ── Feedback ────────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for an alert (inline banner).</summary>
    /// <param name="severity">Severity tier (info, success, warning, error).</param>
    string AlertClass(AlertSeverity severity);

    /// <summary>Returns the CSS class(es) for an alert strip (full-width banner across a section).</summary>
    string AlertStripClass();

    /// <summary>Returns the CSS class(es) for a toast (transient floating notification).</summary>
    /// <param name="severity">Severity tier (info, success, warning, error).</param>
    string ToastClass(ToastSeverity severity);

    /// <summary>Returns the CSS class(es) for a snackbar (transient notification with optional action).</summary>
    string SnackbarClass();

    /// <summary>Returns the CSS class(es) for a snackbar with explicit positioning.</summary>
    /// <param name="vertical">Vertical anchor (top/bottom).</param>
    /// <param name="horizontal">Horizontal anchor (start/center/end).</param>
    string SnackbarClass(NotificationVerticalPosition vertical, NotificationHorizontalPosition horizontal);

    /// <summary>Returns the CSS class(es) for the snackbar host container (holds the active snackbar stack).</summary>
    string SnackbarHostClass();

    /// <summary>Returns the CSS class(es) for a generic dialog root.</summary>
    string DialogClass();

    // ── Dialog slot classes (ADR 0023) ─────────────────────────────────
    // Per-slot classes let each first-party skin target the idiomatic
    // structural vocabulary of its framework (BS5 `.modal-dialog/content/
    // header/title/body/footer`, Fluent v9 surface/title/content/actions,
    // M3 headline/supporting-text/actions). Required on every provider —
    // these are abstract, not default-implemented, per the ADR 0023
    // pre-release strengthening ("third-party providers silently render
    // unstyled slots" risk removed).

    /// <summary>
    /// Returns the CSS class(es) for the dialog container wrapper.
    /// Bootstrap returns <c>"modal-dialog"</c>; Fluent/Material return the
    /// outer dialog wrapper class their skin CSS targets (e.g., <c>"sf-dialog"</c>).
    /// Size / draggable modifiers are composed onto this class in Razor.
    /// </summary>
    string DialogDialogClass();

    /// <summary>
    /// Returns the CSS class(es) for the dialog inner content surface.
    /// Bootstrap returns <c>"modal-content"</c>; Fluent/Material return the
    /// inner surface class (e.g., <c>"sf-dialog__content"</c>) that hosts the
    /// elevation / surface-tint styles for the skin.
    /// </summary>
    string DialogContentClass();

    /// <summary>
    /// Returns the CSS class(es) for the dialog header slot (the row that
    /// contains the title and optional close button). Bootstrap returns
    /// <c>"modal-header"</c>; Fluent returns the title-row class; Material
    /// returns the headline-row class.
    /// </summary>
    string DialogHeaderClass();

    /// <summary>
    /// Returns the CSS class(es) for the dialog title element.
    /// Bootstrap returns <c>"modal-title"</c>; Fluent returns the
    /// <c>fui-DialogTitle</c>-equivalent class; Material returns the
    /// headline-small typography class.
    /// </summary>
    string DialogTitleClass();

    /// <summary>
    /// Returns the CSS class(es) for the dialog body / main content slot.
    /// Bootstrap returns <c>"modal-body"</c>; Fluent returns the
    /// <c>fui-DialogContent</c>-equivalent class; Material returns the
    /// supporting-text typography class.
    /// </summary>
    string DialogBodyClass();

    /// <summary>
    /// Returns the CSS class(es) for the dialog footer / actions row slot.
    /// Bootstrap returns <c>"modal-footer"</c>; Fluent returns the
    /// <c>fui-DialogActions</c>-equivalent class; Material returns the
    /// label-large actions row class.
    /// </summary>
    string DialogFooterClass();

    /// <summary>Returns the CSS class(es) for the dialog backdrop overlay.</summary>
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

    /// <summary>Returns the CSS class(es) for a determinate progress bar.</summary>
    string ProgressBarClass();

    /// <summary>Returns the CSS class(es) for a circular progress indicator.</summary>
    string ProgressCircleClass();

    /// <summary>Returns the CSS class(es) for an indeterminate spinner.</summary>
    /// <param name="size">Spinner size token.</param>
    string SpinnerClass(SpinnerSize size);

    /// <summary>Returns the CSS class(es) for a skeleton placeholder shown while content loads.</summary>
    /// <param name="variant">Skeleton shape (text, circle, rect, etc.).</param>
    string SkeletonClass(SkeletonVariant variant);

    /// <summary>Returns the CSS class(es) for a callout (small attention-grabbing balloon).</summary>
    /// <param name="type">Callout severity / kind.</param>
    string CalloutClass(CalloutType type);

    // ── DataGrid ────────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a data grid root.</summary>
    string DataGridClass();

    /// <summary>Returns the CSS class(es) for the inner table element of a data grid.</summary>
    string DataGridTableClass();

    /// <summary>Returns the CSS class(es) for the header row of a data grid.</summary>
    string DataGridHeaderClass();

    /// <summary>Returns the CSS class(es) for a single header cell.</summary>
    /// <param name="isSortable"><c>true</c> when the column is sortable.</param>
    /// <param name="isSorted"><c>true</c> when the column is the active sort.</param>
    string DataGridHeaderCellClass(bool isSortable, bool isSorted);

    /// <summary>Returns the CSS class(es) for a single body row.</summary>
    /// <param name="isSelected"><c>true</c> when the row is selected.</param>
    /// <param name="isStriped"><c>true</c> when the row should receive zebra striping.</param>
    string DataGridRowClass(bool isSelected, bool isStriped);

    /// <summary>Returns the CSS class(es) for a single body cell.</summary>
    string DataGridCellClass();

    /// <summary>Returns the CSS class(es) for an inline command button rendered in a grid cell.</summary>
    string DataGridCommandButtonClass();

    /// <summary>Returns the CSS class(es) for the pager footer of a data grid.</summary>
    string DataGridPagerClass();

    /// <summary>Returns the CSS class(es) for the toolbar above a data grid.</summary>
    string DataGridToolbarClass();

    /// <summary>Returns the CSS class(es) for the filter row of a data grid.</summary>
    string DataGridFilterRowClass();

    /// <summary>Returns the CSS class(es) for a single filter cell in the filter row.</summary>
    string DataGridFilterCellClass();

    /// <summary>Returns the CSS class(es) for a group header row (when grouping is enabled).</summary>
    string DataGridGroupHeaderClass();

    // ── DataSheet ───────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a data sheet root.</summary>
    /// <param name="isLoading"><c>true</c> when the sheet is fetching its initial data.</param>
    string DataSheetClass(bool isLoading);

    /// <summary>Returns the CSS class(es) for a data sheet cell.</summary>
    /// <param name="state">Validation / dirty state of the cell.</param>
    /// <param name="isActive"><c>true</c> when the cell is the active (focused) cell.</param>
    /// <param name="isEditable"><c>true</c> when the cell can be edited inline.</param>
    string DataSheetCellClass(CellState state, bool isActive, bool isEditable);

    /// <summary>Returns the CSS class(es) for a data sheet header cell.</summary>
    /// <param name="isSortable"><c>true</c> when the column is sortable.</param>
    string DataSheetHeaderCellClass(bool isSortable);

    /// <summary>Returns the CSS class(es) for a data sheet body row.</summary>
    /// <param name="isDirty"><c>true</c> when the row has unsaved changes.</param>
    /// <param name="isSelected"><c>true</c> when the row is selected.</param>
    /// <param name="isDeleted"><c>true</c> when the row is marked for deletion.</param>
    string DataSheetRowClass(bool isDirty, bool isSelected, bool isDeleted);

    /// <summary>Returns the CSS class(es) for the toolbar above a data sheet.</summary>
    string DataSheetToolbarClass();

    /// <summary>Returns the CSS class(es) for the bulk-action bar shown when multiple rows are selected.</summary>
    /// <param name="isVisible"><c>true</c> when the bar is shown.</param>
    string DataSheetBulkBarClass(bool isVisible);

    /// <summary>Returns the CSS class(es) for the save footer shown when the sheet has unsaved edits.</summary>
    /// <param name="dirtyCount">Number of dirty rows; some skins use this to size or color the footer.</param>
    string DataSheetSaveFooterClass(int dirtyCount);

    /// <summary>Returns the CSS class(es) for the "add row" button in the data sheet toolbar.</summary>
    string DataSheetAddButtonClass();

    /// <summary>Returns the CSS class(es) for the "save" button in the data sheet save footer.</summary>
    string DataSheetSaveButtonClass();

    /// <summary>Returns the CSS class(es) for the "reset" button in the data sheet save footer.</summary>
    string DataSheetResetButtonClass();

    /// <summary>Returns the CSS class(es) for the loading spinner shown over a data sheet.</summary>
    string DataSheetSpinnerClass();

    /// <summary>Returns the CSS class(es) for the dirty-count badge in the data sheet save footer.</summary>
    string DataSheetDirtyBadgeClass();

    /// <summary>Returns the CSS class(es) for the skeleton placeholder root of a loading data sheet.</summary>
    string DataSheetSkeletonClass();

    /// <summary>Returns the CSS class(es) for a single skeleton row in a loading data sheet.</summary>
    string DataSheetSkeletonRowClass();

    /// <summary>Returns the CSS class(es) for a single skeleton cell in a loading data sheet.</summary>
    string DataSheetSkeletonCellClass();

    /// <summary>Returns the CSS class(es) for the loading-text label shown beneath the spinner.</summary>
    string DataSheetLoadingTextClass();

    /// <summary>Returns the CSS class(es) for the empty-state placeholder shown when the sheet has no rows.</summary>
    string DataSheetEmptyClass();

    /// <summary>Returns the CSS class(es) for the select-all checkbox header cell.</summary>
    string DataSheetSelectHeaderClass();

    /// <summary>Returns the CSS class(es) for the row-actions header cell.</summary>
    string DataSheetActionsHeaderClass();

    /// <summary>Returns the CSS class(es) for the ARIA live region used for screen-reader announcements.</summary>
    string DataSheetAriaLiveClass();

    /// <summary>Returns the CSS class(es) for a row-select checkbox cell.</summary>
    string DataSheetSelectCellClass();

    /// <summary>Returns the CSS class(es) for a row-actions cell.</summary>
    string DataSheetActionsCellClass();

    /// <summary>Returns the CSS class(es) for the per-row delete button.</summary>
    string DataSheetDeleteButtonClass();

    /// <summary>Returns the CSS class(es) for the read-only text rendered inside a non-editing cell.</summary>
    string DataSheetCellTextClass();

    /// <summary>Returns the CSS class(es) for the inline editor input shown when a cell enters edit mode.</summary>
    string DataSheetEditorInputClass();

    /// <summary>Returns the CSS class(es) for the inline editor select shown when a lookup cell enters edit mode.</summary>
    string DataSheetEditorSelectClass();

    /// <summary>Returns the CSS class(es) for the scrollable content region of a data sheet.</summary>
    string DataSheetContentClass();

    /// <summary>Returns the CSS class(es) for visually-hidden text intended for screen readers only.</summary>
    string DataSheetScreenReaderOnlyClass();

    // ── ListView ────────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a list view root.</summary>
    string ListViewClass();

    /// <summary>Returns the CSS class(es) for a single list view item.</summary>
    /// <param name="isSelected"><c>true</c> when the item is selected.</param>
    string ListViewItemClass(bool isSelected);

    // ── Window ──────────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a window root (floating draggable surface).</summary>
    /// <param name="isModal"><c>true</c> when the window is shown modally with a backdrop.</param>
    string WindowClass(bool isModal);

    /// <summary>Returns the CSS class(es) for the title bar slot of a window.</summary>
    string WindowTitleBarClass();

    /// <summary>Returns the CSS class(es) for the content slot of a window.</summary>
    string WindowContentClass();

    /// <summary>Returns the CSS class(es) for the action row inside a window's title bar.</summary>
    string WindowActionsClass();

    /// <summary>Returns the CSS class(es) for the modal backdrop behind a window.</summary>
    string WindowOverlayClass();

    /// <summary>Returns the CSS class(es) for the footer slot of a window.</summary>
    string WindowFooterClass();

    // ── Editor ──────────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a rich-text editor root.</summary>
    string EditorClass();

    /// <summary>Returns the CSS class(es) for the toolbar of a rich-text editor.</summary>
    string EditorToolbarClass();

    /// <summary>Returns the CSS class(es) for the editable content surface of a rich-text editor.</summary>
    string EditorContentClass();

    // ── Upload ──────────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for an upload control root.</summary>
    string UploadClass();

    /// <summary>Returns the CSS class(es) for the file list shown beneath an upload control.</summary>
    string UploadFileListClass();

    /// <summary>Returns the CSS class(es) for a single file row in an upload file list.</summary>
    string UploadFileItemClass();

    /// <summary>Returns the CSS class(es) for the drop-zone region of an upload control.</summary>
    /// <param name="isActive"><c>true</c> while a drag operation is hovering the zone.</param>
    string UploadDropZoneClass(bool isActive);

    // ── Charts ──────────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a chart container (responsive box around any chart).</summary>
    string ChartContainerClass();

    /// <summary>Returns the CSS class(es) for a gauge chart root.</summary>
    string GaugeClass();

    // ── Scheduling ──────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a calendar root (month / week / day view).</summary>
    string CalendarClass();

    /// <summary>Returns the CSS class(es) for a scheduler root (timeline of events).</summary>
    string SchedulerClass();

    // ── AllocationScheduler ─────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for an allocation scheduler root (resource × time matrix).</summary>
    string AllocationSchedulerClass();

    /// <summary>Returns the CSS class(es) for the toolbar of an allocation scheduler.</summary>
    string AllocationSchedulerToolbarClass();

    /// <summary>Returns the CSS class(es) for the (left-most) resource column of an allocation scheduler.</summary>
    /// <param name="isPinned"><c>true</c> when the column is sticky-pinned during horizontal scroll.</param>
    string AllocationSchedulerResourceColumnClass(bool isPinned);

    /// <summary>Returns the CSS class(es) for the time-axis header row.</summary>
    /// <param name="grain">Time bucket granularity (hour, day, week, etc.).</param>
    string AllocationSchedulerTimeHeaderClass(TimeGranularity grain);

    /// <summary>Returns the CSS class(es) for a single resource row.</summary>
    /// <param name="isSelected"><c>true</c> when the row is selected.</param>
    /// <param name="isOverAllocated"><c>true</c> when the row's totals exceed capacity.</param>
    /// <param name="isStriped"><c>true</c> when the row should receive zebra striping.</param>
    string AllocationSchedulerRowClass(bool isSelected, bool isOverAllocated, bool isStriped = false);

    /// <summary>Returns the CSS class(es) for a single allocation cell (one resource × one time bucket).</summary>
    /// <param name="isEditable"><c>true</c> when the cell is editable.</param>
    /// <param name="isSelected"><c>true</c> when the cell is part of the current selection.</param>
    /// <param name="isConflict"><c>true</c> when the cell value violates a constraint.</param>
    /// <param name="isDisabled"><c>true</c> when the cell is disabled.</param>
    /// <param name="isDragTarget"><c>true</c> when the cell is the target of an in-flight drag.</param>
    string AllocationSchedulerCellClass(bool isEditable, bool isSelected, bool isConflict, bool isDisabled, bool isDragTarget);

    /// <summary>Returns the CSS class(es) for the value text inside an allocation cell.</summary>
    /// <param name="mode">How the value is being displayed (hours, percent, FTE, etc.).</param>
    string AllocationSchedulerCellValueClass(AllocationValueMode mode);

    /// <summary>Returns the CSS class(es) for the delta indicator (difference vs. baseline / target).</summary>
    /// <param name="mode">Display mode for the delta (absolute, percent).</param>
    /// <param name="isOver"><c>true</c> when actual exceeds target.</param>
    /// <param name="isUnder"><c>true</c> when actual is below target.</param>
    string AllocationSchedulerDeltaClass(DeltaDisplayMode mode, bool isOver, bool isUnder);

    /// <summary>Returns the CSS class(es) for the scenario strip (row of scenario chips above the matrix).</summary>
    string AllocationSchedulerScenarioStripClass();

    /// <summary>Returns the CSS class(es) for a single scenario chip in the scenario strip.</summary>
    /// <param name="isActive"><c>true</c> when the chip represents the active scenario.</param>
    /// <param name="isLocked"><c>true</c> when the scenario is locked from edits.</param>
    string AllocationSchedulerScenarioChipClass(bool isActive, bool isLocked);

    /// <summary>Returns the CSS class(es) for the ghost bar shown during drag operations.</summary>
    string AllocationSchedulerGhostBarClass();

    /// <summary>Returns the CSS class(es) for the right-click context menu over the matrix.</summary>
    string AllocationSchedulerContextMenuClass();

    /// <summary>Returns the CSS class(es) for the empty-state placeholder shown when no resources are loaded.</summary>
    string AllocationSchedulerEmptyClass();

    /// <summary>Returns the CSS class(es) for the loading spinner shown over the matrix.</summary>
    string AllocationSchedulerLoaderClass();

    /// <summary>Returns the CSS class(es) for the splitter between the resource column and the time matrix.</summary>
    /// <param name="isDragging"><c>true</c> while the splitter is being dragged.</param>
    /// <param name="isFocused"><c>true</c> when the splitter has keyboard focus.</param>
    string AllocationSchedulerSplitterClass(bool isDragging, bool isFocused);

    /// <summary>Returns the CSS class(es) for the splitter restore handle shown when one side is collapsed.</summary>
    /// <param name="collapsedSide">Which side is currently collapsed (left/right).</param>
    string AllocationSchedulerSplitterRestoreClass(SplitterSide collapsedSide);

    // ── Overlays ────────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a generic modal root.</summary>
    /// <param name="size">Modal size token.</param>
    string ModalClass(ModalSize size);

    /// <summary>Returns the CSS class(es) for the backdrop overlay behind a modal.</summary>
    string ModalOverlayClass();

    // ── SignalR Status ─────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for the SignalR aggregate-status indicator.</summary>
    /// <param name="state">Aggregate state across all hubs.</param>
    /// <param name="isCompact"><c>true</c> for the icon-only compact form.</param>
    string SignalRStatusClass(AggregateConnectionState state, bool isCompact);

    /// <summary>Returns the CSS class(es) for the popup that lists per-hub connection details.</summary>
    string SignalRPopupClass();

    /// <summary>Returns the CSS class(es) for a single per-hub row in the SignalR popup.</summary>
    /// <param name="health">Health tier for that hub.</param>
    string SignalRRowClass(ConnectionHealthState health);

    /// <summary>Returns the CSS class(es) for a per-hub health badge.</summary>
    /// <param name="health">Health tier the badge represents.</param>
    string SignalRBadgeClass(ConnectionHealthState health);

    // ── ResizableContainer ────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for a resizable container root.</summary>
    /// <param name="isResizing"><c>true</c> while an active resize gesture is in flight.</param>
    /// <param name="isDisabled"><c>true</c> when resizing is disabled.</param>
    string ResizableContainerClass(bool isResizing, bool isDisabled);

    /// <summary>Returns the CSS class(es) for the content slot inside a resizable container.</summary>
    string ResizableContainerContentClass();

    /// <summary>Returns the CSS class(es) for a single resize handle on a resizable container.</summary>
    /// <param name="edge">Which edge or corner this handle controls.</param>
    /// <param name="isActive"><c>true</c> while this handle is being dragged.</param>
    /// <param name="isFocused"><c>true</c> when the handle has keyboard focus.</param>
    string ResizableContainerHandleClass(ResizeEdges edge, bool isActive, bool isFocused);

    // ── Utility ─────────────────────────────────────────────────────────

    /// <summary>Returns the CSS class(es) for an icon element.</summary>
    /// <param name="iconName">The logical icon name (resolved through the active <see cref="ISunfishIconProvider"/>).</param>
    /// <param name="size">Icon size token.</param>
    /// <param name="flip">Optional flip transform.</param>
    /// <param name="themeColor">Optional theme color tier.</param>
    string IconClass(string iconName, IconSize size, IconFlip flip = IconFlip.None, IconThemeColor themeColor = IconThemeColor.Base);

    /// <summary>Returns the CSS class(es) for a draggable element.</summary>
    string DragDropClass();

    /// <summary>Returns the CSS class(es) for a generic drop zone (drag-and-drop target).</summary>
    /// <param name="isActive"><c>true</c> while a drag operation is hovering the zone.</param>
    string DropZoneClass(bool isActive);

    /// <summary>Returns the CSS class(es) for a scroll view (scrollable content container).</summary>
    string ScrollViewClass();
}

namespace Sunfish.Core.Enums;

/// <summary>
/// Specifies the layout alignment of action buttons in dialogs, popovers, and similar containers.
/// </summary>
public enum ActionsLayout
{
    /// <summary>Actions are aligned to the start (left in LTR).</summary>
    Start,

    /// <summary>Actions are centered.</summary>
    Center,

    /// <summary>Actions are aligned to the end (right in LTR).</summary>
    End,

    /// <summary>Actions are stretched to fill the available width.</summary>
    Stretch
}

/// <summary>
/// Specifies the visual style variant of a chip.
/// </summary>
public enum ChipVariant
{
    /// <summary>Default chip styling.</summary>
    Default,

    /// <summary>Primary-colored chip.</summary>
    Primary,

    /// <summary>Secondary-colored chip.</summary>
    Secondary,

    /// <summary>Danger-colored chip for destructive context.</summary>
    Danger,

    /// <summary>Warning-colored chip for cautionary context.</summary>
    Warning,

    /// <summary>Info-colored chip for informational context.</summary>
    Info,

    /// <summary>Success-colored chip for positive context.</summary>
    Success
}

/// <summary>
/// Specifies the size of a floating action button (FAB).
/// </summary>
public enum FabSize
{
    /// <summary>A compact FAB.</summary>
    Small,

    /// <summary>The default FAB size.</summary>
    Medium,

    /// <summary>A large, prominent FAB.</summary>
    Large
}

/// <summary>
/// Specifies the severity of a form validation message.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Informational validation hint.</summary>
    Info,

    /// <summary>Non-blocking validation warning.</summary>
    Warning,

    /// <summary>Blocking validation error that prevents submission.</summary>
    Error
}

/// <summary>
/// Specifies the display size of an avatar.
/// </summary>
public enum AvatarSize
{
    /// <summary>Extra-small avatar (e.g., inline mentions).</summary>
    ExtraSmall,

    /// <summary>Small avatar.</summary>
    Small,

    /// <summary>Default avatar size.</summary>
    Medium,

    /// <summary>Large avatar.</summary>
    Large,

    /// <summary>Extra-large avatar (e.g., profile headers).</summary>
    ExtraLarge
}

/// <summary>
/// Specifies the visual style variant of a badge.
/// </summary>
public enum BadgeVariant
{
    /// <summary>Default badge styling.</summary>
    Default,

    /// <summary>Primary-colored badge.</summary>
    Primary,

    /// <summary>Secondary-colored badge.</summary>
    Secondary,

    /// <summary>Danger-colored badge.</summary>
    Danger,

    /// <summary>Warning-colored badge.</summary>
    Warning,

    /// <summary>Info-colored badge.</summary>
    Info,

    /// <summary>Success-colored badge.</summary>
    Success
}

/// <summary>
/// Specifies the preferred position of a tooltip relative to its anchor element.
/// </summary>
public enum TooltipPosition
{
    /// <summary>Tooltip appears above the anchor.</summary>
    Top,

    /// <summary>Tooltip appears below the anchor.</summary>
    Bottom,

    /// <summary>Tooltip appears to the left of the anchor.</summary>
    Left,

    /// <summary>Tooltip appears to the right of the anchor.</summary>
    Right
}

/// <summary>
/// Specifies the trigger that shows a popover or tooltip.
/// </summary>
public enum PopoverShowOn
{
    /// <summary>Show on click (toggle).</summary>
    Click,

    /// <summary>Show on mouse enter, hide on mouse leave.</summary>
    MouseEnter
}

/// <summary>
/// Specifies the collision behavior when a popover would overflow the viewport.
/// </summary>
public enum PopoverCollision
{
    /// <summary>No collision detection; popover may overflow viewport.</summary>
    None,

    /// <summary>Flip to the opposite position if the popover overflows.</summary>
    Flip,

    /// <summary>Shift the popover along the axis to stay within the viewport.</summary>
    Fit
}

/// <summary>
/// Specifies the position of the toolbar in a list component.
/// </summary>
public enum ListToolbarPosition
{
    /// <summary>Toolbar on the right side.</summary>
    Right,

    /// <summary>Toolbar on the left side.</summary>
    Left,

    /// <summary>Toolbar above the list.</summary>
    Top,

    /// <summary>Toolbar below the list.</summary>
    Bottom
}

/// <summary>
/// Specifies the animation type for popover show/hide transitions.
/// </summary>
public enum PopoverAnimationType
{
    /// <summary>No animation.</summary>
    None,

    /// <summary>Fade in/out.</summary>
    Fade,

    /// <summary>Push up from bottom.</summary>
    PushUp,

    /// <summary>Push down from top.</summary>
    PushDown,

    /// <summary>Push left from right.</summary>
    PushLeft,

    /// <summary>Push right from left.</summary>
    PushRight,

    /// <summary>Reveal vertically from center.</summary>
    RevealVertical,

    /// <summary>Slide up into view.</summary>
    SlideUp,

    /// <summary>Slide inward.</summary>
    SlideIn,

    /// <summary>Slide down into view (default).</summary>
    SlideDown,

    /// <summary>Slide right into view.</summary>
    SlideRight,

    /// <summary>Slide left into view.</summary>
    SlideLeft,

    /// <summary>Zoom in from smaller scale.</summary>
    ZoomIn,

    /// <summary>Zoom out from larger scale.</summary>
    ZoomOut
}

/// <summary>
/// Specifies the semantic typography variant for text rendering.
/// </summary>
public enum TypographyVariant
{
    /// <summary>Heading level 1.</summary>
    H1,

    /// <summary>Heading level 2.</summary>
    H2,

    /// <summary>Heading level 3.</summary>
    H3,

    /// <summary>Heading level 4.</summary>
    H4,

    /// <summary>Heading level 5.</summary>
    H5,

    /// <summary>Heading level 6.</summary>
    H6,

    /// <summary>Subtitle level 1 (larger).</summary>
    Subtitle1,

    /// <summary>Subtitle level 2 (smaller).</summary>
    Subtitle2,

    /// <summary>Body text level 1 (default).</summary>
    Body1,

    /// <summary>Body text level 2 (smaller).</summary>
    Body2,

    /// <summary>Small caption text.</summary>
    Caption,

    /// <summary>All-caps overline text.</summary>
    Overline
}

/// <summary>
/// Specifies the size of a loading spinner.
/// </summary>
public enum SpinnerSize
{
    /// <summary>A compact spinner for inline use.</summary>
    Small,

    /// <summary>The default spinner size.</summary>
    Medium,

    /// <summary>A large spinner for full-page loading states.</summary>
    Large
}

/// <summary>
/// Specifies the shape of a skeleton placeholder element.
/// </summary>
public enum SkeletonVariant
{
    /// <summary>A text-line-shaped skeleton.</summary>
    Text,

    /// <summary>A rectangular skeleton block.</summary>
    Rectangular,

    /// <summary>A circular skeleton (e.g., avatar placeholder).</summary>
    Circular,

    /// <summary>A rectangle with rounded corners.</summary>
    Rounded
}

/// <summary>
/// Specifies the semantic type of a callout message.
/// </summary>
public enum CalloutType
{
    /// <summary>Informational callout.</summary>
    Info,

    /// <summary>Warning callout.</summary>
    Warning,

    /// <summary>Danger callout for critical information.</summary>
    Danger,

    /// <summary>Success callout for positive information.</summary>
    Success,

    /// <summary>Neutral note callout.</summary>
    Note
}

/// <summary>
/// Specifies the size of a modal dialog.
/// </summary>
public enum ModalSize
{
    /// <summary>A compact modal for simple confirmations.</summary>
    Small,

    /// <summary>The default modal size.</summary>
    Medium,

    /// <summary>A wide modal for complex content.</summary>
    Large,

    /// <summary>A modal that fills the entire viewport.</summary>
    FullScreen
}

/// <summary>
/// Specifies how an icon provider renders icon markup.
/// </summary>
public enum IconRenderMode
{
    /// <summary>SVG sprite reference: &lt;svg&gt;&lt;use href="sprite.svg#name"&gt;&lt;/use&gt;&lt;/svg&gt;.</summary>
    SvgSprite,

    /// <summary>Raw &lt;svg&gt; markup injected directly into the DOM.</summary>
    InlineSvg,

    /// <summary>CSS class-based rendering: &lt;i class="{prefix} {prefix}-{name}"&gt;.</summary>
    CssClass
}

/// <summary>
/// Specifies the display size of an icon.
/// </summary>
public enum IconSize
{
    /// <summary>Small icon (e.g., inline with text).</summary>
    Small,

    /// <summary>Default icon size.</summary>
    Medium,

    /// <summary>Large icon.</summary>
    Large,

    /// <summary>Extra-large icon for hero or feature sections.</summary>
    ExtraLarge
}

/// <summary>
/// Specifies the flip transformation applied to an icon.
/// </summary>
public enum IconFlip
{
    /// <summary>No flip applied.</summary>
    None,

    /// <summary>Flipped along the horizontal axis.</summary>
    Horizontal,

    /// <summary>Flipped along the vertical axis.</summary>
    Vertical,

    /// <summary>Flipped along both axes.</summary>
    Both
}

/// <summary>
/// Specifies a theme-aware color applied to an icon.
/// </summary>
public enum IconThemeColor
{
    /// <summary>The default base color.</summary>
    Base,

    /// <summary>Primary theme color.</summary>
    Primary,

    /// <summary>Secondary theme color.</summary>
    Secondary,

    /// <summary>Success/positive color.</summary>
    Success,

    /// <summary>Warning/cautionary color.</summary>
    Warning,

    /// <summary>Danger/error color.</summary>
    Danger,

    /// <summary>Informational color.</summary>
    Info,

    /// <summary>Inherits color from the parent element.</summary>
    Inherit
}

/// <summary>
/// Specifies a responsive breakpoint tier for layout decisions.
/// </summary>
public enum Breakpoint
{
    /// <summary>Extra-small viewport (mobile portrait).</summary>
    ExtraSmall,

    /// <summary>Small viewport (mobile landscape).</summary>
    Small,

    /// <summary>Medium viewport (tablet).</summary>
    Medium,

    /// <summary>Large viewport (desktop).</summary>
    Large,

    /// <summary>Extra-large viewport (wide desktop).</summary>
    ExtraLarge,

    /// <summary>Extra-extra-large viewport (ultra-wide).</summary>
    ExtraExtraLarge
}

/// <summary>
/// Specifies the checkbox mode for a tree view component.
/// </summary>
public enum CheckBoxMode
{
    /// <summary>No checkboxes shown.</summary>
    None,

    /// <summary>Single checkbox selection.</summary>
    Single,

    /// <summary>Multiple checkbox selection.</summary>
    Multiple
}

/// <summary>
/// Specifies the selection mode for a tree view component.
/// </summary>
public enum TreeSelectionMode
{
    /// <summary>No selection allowed.</summary>
    None,

    /// <summary>Only one item can be selected at a time.</summary>
    Single,

    /// <summary>Multiple items can be selected simultaneously.</summary>
    Multiple
}

/// <summary>
/// Specifies the adaptive rendering mode for autocomplete on mobile devices.
/// </summary>
public enum AutoCompleteAdaptiveMode
{
    /// <summary>Standard dropdown rendering.</summary>
    None,

    /// <summary>
    /// Automatically switches to a full-screen action sheet on small viewports.
    /// CSS class mar-autocomplete--adaptive is applied and a media query
    /// triggers full-screen overlay rendering.
    /// </summary>
    Auto
}

/// <summary>
/// Specifies the adaptive rendering mode for popup-based components on mobile devices.
/// When Auto, the component renders as a full-screen action sheet on small screens.
/// </summary>
public enum AdaptiveMode
{
    /// <summary>Standard popup rendering on all screen sizes.</summary>
    None,

    /// <summary>Automatically switch to action sheet on small screens.</summary>
    Auto
}

/// <summary>
/// Specifies the layout orientation of the two calendars in a DateRangePicker popup.
/// </summary>
public enum CalendarOrientation
{
    /// <summary>The two calendars are placed side by side (left and right).</summary>
    Horizontal,

    /// <summary>The two calendars are stacked vertically (top and bottom).</summary>
    Vertical
}

/// <summary>Determines how the menu is triggered.</summary>
public enum MenuShowOn
{
    /// <summary>Menu opens on click (default).</summary>
    Click,

    /// <summary>Menu opens on mouse hover.</summary>
    MouseEnter
}

/// <summary>
/// Specifies the calendar view level shown in a date picker popup.
/// </summary>
public enum CalendarView
{
    /// <summary>Month view — individual days are shown (default).</summary>
    Month,

    /// <summary>Year view — months of a year are shown.</summary>
    Year,

    /// <summary>Decade view — years within a decade are shown.</summary>
    Decade,

    /// <summary>Century view — decades within a century are shown.</summary>
    Century
}

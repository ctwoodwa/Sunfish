using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sunfish.Foundation.Enums;
using Sunfish.UIAdapters.Blazor.Base;

namespace Sunfish.UIAdapters.Blazor.Components.Buttons;

/// <summary>
/// Non-selectable child of <see cref="SunfishButtonGroup"/>. Behaves like a regular
/// <see cref="SunfishButton"/> but participates in the group's visual and disabled-cascade
/// semantics. Use <see cref="ButtonGroupToggleButton"/> for selectable entries.
/// </summary>
public partial class ButtonGroupButton : SunfishComponentBase
{
    [CascadingParameter] internal SunfishButtonGroup? ParentGroup { get; set; }

    /// <summary>The visual-variant of the button (primary, secondary, etc.).</summary>
    [Parameter] public ButtonVariant Variant { get; set; } = ButtonVariant.Secondary;

    /// <summary>The button size.</summary>
    [Parameter] public ButtonSize Size { get; set; } = ButtonSize.Medium;

    /// <summary>The fill mode (solid, outline, flat).</summary>
    [Parameter] public FillMode FillMode { get; set; } = FillMode.Solid;

    /// <summary>Rounded-corner mode.</summary>
    [Parameter] public RoundedMode Rounded { get; set; } = RoundedMode.Medium;

    /// <summary>Whether this child is enabled. Falls back to the parent group's <c>Enabled</c> state.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether this child renders at all. <c>false</c> suppresses render without affecting
    /// the indexes of sibling toggle-button selection state (per spec).
    /// </summary>
    [Parameter] public bool Visible { get; set; } = true;

    /// <summary>Optional <c>title</c> attribute (HTML tooltip).</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Optional icon render-fragment rendered before the child content.</summary>
    [Parameter] public RenderFragment? Icon { get; set; }

    /// <summary>Short-hand text content. Ignored when <see cref="ChildContent"/> is supplied.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>Rich child-content of the button. Takes priority over <see cref="Text"/>.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Fires when the user clicks the button.</summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    internal bool EffectiveEnabled => Enabled && (ParentGroup?.GroupEnabled ?? true);

    private async Task HandleClick(MouseEventArgs args)
    {
        if (!EffectiveEnabled) return;
        if (OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(args);
        }
    }
}

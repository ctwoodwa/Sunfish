using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sunfish.Foundation.Enums;
using Sunfish.UIAdapters.Blazor.Base;

namespace Sunfish.UIAdapters.Blazor.Components.Buttons;

/// <summary>
/// Toggleable child of <see cref="SunfishButtonGroup"/>. Exposes per-child two-way
/// <c>Selected</c> binding and participates in container-level selection coordination
/// (single vs. multiple) via the <see cref="SunfishButtonGroup.SelectionMode"/> cascade.
/// </summary>
public partial class ButtonGroupToggleButton : SunfishComponentBase, IDisposable
{
    private bool _registered;

    [CascadingParameter] internal SunfishButtonGroup? ParentGroup { get; set; }

    /// <summary>Button size.</summary>
    [Parameter] public ButtonSize Size { get; set; } = ButtonSize.Medium;

    /// <summary>
    /// Whether the toggle button is currently in its "on" state. Supports
    /// <c>@bind-Selected</c>. Container-level <see cref="SunfishButtonGroup.SelectionMode"/>
    /// can mutate this on peer clicks (Single mode).
    /// </summary>
    [Parameter] public bool Selected { get; set; }

    /// <summary>Two-way-binding callback for <see cref="Selected"/>.</summary>
    [Parameter] public EventCallback<bool> SelectedChanged { get; set; }

    /// <summary>Whether this child is enabled. Falls back to the parent group's <c>Enabled</c>.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>Whether this child renders at all (spec: toggle without affecting indexes).</summary>
    [Parameter] public bool Visible { get; set; } = true;

    /// <summary>Optional <c>title</c> attribute.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Optional icon render-fragment rendered before the child content.</summary>
    [Parameter] public RenderFragment? Icon { get; set; }

    /// <summary>Short-hand text content. Ignored when <see cref="ChildContent"/> is supplied.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>Rich child-content of the toggle button.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Fires whenever the user clicks the button (before <see cref="SelectedChanged"/>).
    /// </summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    internal bool EffectiveEnabled => Enabled && (ParentGroup?.GroupEnabled ?? true);

    protected override void OnInitialized()
    {
        if (ParentGroup != null && !_registered)
        {
            ParentGroup.RegisterToggle(this);
            _registered = true;
        }
    }

    private async Task HandleClick(MouseEventArgs args)
    {
        if (!EffectiveEnabled) return;

        // Spec: OnClick always fires before Selected changes.
        if (OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(args);
        }

        if (ParentGroup != null)
        {
            // Parent coordinates peer state and publishes container-level events.
            await ParentGroup.NotifyToggleActivatedAsync(this);
        }
        else
        {
            // Stand-alone fallback — behave like SunfishToggleButton.
            await SetSelectedInternalAsync(!Selected);
        }
    }

    /// <summary>
    /// Parent-driven update of the toggle state. Publishes <see cref="SelectedChanged"/>
    /// so callers using <c>@bind-Selected</c> stay in sync.
    /// </summary>
    internal async Task SetSelectedInternalAsync(bool value)
    {
        if (Selected == value) return;
        Selected = value;
        if (SelectedChanged.HasDelegate)
        {
            await SelectedChanged.InvokeAsync(Selected);
        }
        StateHasChanged();
    }

    public new void Dispose()
    {
        if (_registered && ParentGroup != null)
        {
            ParentGroup.UnregisterToggle(this);
            _registered = false;
        }
        GC.SuppressFinalize(this);
    }
}

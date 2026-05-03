using Microsoft.AspNetCore.Components;
using Sunfish.Foundation.Enums;
using Sunfish.UIAdapters.Blazor.Base;

namespace Sunfish.UIAdapters.Blazor.Components.Buttons;

/// <summary>
/// Visual container for related <see cref="ButtonGroupButton"/> and
/// <see cref="ButtonGroupToggleButton"/> children. Coordinates selection across toggle
/// children based on <see cref="SelectionMode"/> and publishes selection-changed events
/// both at the container (index-based) and per-child (<c>bool</c>) level.
/// </summary>
public partial class SunfishButtonGroup : SunfishComponentBase
{
    private readonly List<ButtonGroupToggleButton> _toggleButtons = new();

    /// <summary>
    /// Selection mode for <see cref="ButtonGroupToggleButton"/> children. Defaults to
    /// <see cref="ButtonGroupSelectionMode.Single"/> (per spec).
    /// </summary>
    [Parameter]
    public ButtonGroupSelectionMode SelectionMode { get; set; } = ButtonGroupSelectionMode.Single;

    /// <summary>Whether the group as a whole is enabled. When <c>false</c>, each child button is also disabled.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>Width of the button-group container (any valid CSS length).</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>
    /// The zero-based index of the currently selected toggle button, or <c>-1</c> when none is
    /// selected. Only meaningful when <see cref="SelectionMode"/> is
    /// <see cref="ButtonGroupSelectionMode.Single"/>.
    /// </summary>
    [Parameter] public int SelectedIndex { get; set; } = -1;

    /// <summary>Two-way-binding callback for <see cref="SelectedIndex"/>.</summary>
    [Parameter] public EventCallback<int> SelectedIndexChanged { get; set; }

    /// <summary>
    /// The zero-based indexes of every currently selected toggle button. Only meaningful when
    /// <see cref="SelectionMode"/> is <see cref="ButtonGroupSelectionMode.Multiple"/>.
    /// </summary>
    [Parameter] public int[] SelectedIndexes { get; set; } = Array.Empty<int>();

    /// <summary>Two-way-binding callback for <see cref="SelectedIndexes"/>.</summary>
    [Parameter] public EventCallback<int[]> SelectedIndexesChanged { get; set; }

    /// <summary>
    /// Container-level callback fired when any toggle child's <c>Selected</c> state changes,
    /// carrying the zero-based index of the toggled child. Retained for backward compatibility
    /// with the pre-child-tag API surface.
    /// </summary>
    [Parameter] public EventCallback<int> SelectedChanged { get; set; }

    /// <summary>
    /// Markup of the group (<see cref="ButtonGroupButton"/> / <see cref="ButtonGroupToggleButton"/>
    /// children, plus any free-form content).
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string? WidthStyle => Width != null ? $"width:{Width};" : null;

    internal bool GroupEnabled => Enabled;

    internal void RegisterToggle(ButtonGroupToggleButton toggle)
    {
        if (!_toggleButtons.Contains(toggle))
        {
            _toggleButtons.Add(toggle);
        }
    }

    internal void UnregisterToggle(ButtonGroupToggleButton toggle)
    {
        _toggleButtons.Remove(toggle);
    }

    /// <summary>
    /// Called by a <see cref="ButtonGroupToggleButton"/> child after a user click. Coordinates
    /// peer deselection (Single mode) and publishes container-level events.
    /// </summary>
    internal async Task NotifyToggleActivatedAsync(ButtonGroupToggleButton source)
    {
        var index = _toggleButtons.IndexOf(source);
        if (index < 0) return;

        switch (SelectionMode)
        {
            case ButtonGroupSelectionMode.Single:
            {
                // Radio behavior: source becomes selected, every peer is cleared.
                for (int i = 0; i < _toggleButtons.Count; i++)
                {
                    var peer = _toggleButtons[i];
                    var shouldBeSelected = ReferenceEquals(peer, source);
                    if (peer.Selected != shouldBeSelected)
                    {
                        await peer.SetSelectedInternalAsync(shouldBeSelected);
                    }
                }

                if (SelectedIndex != index)
                {
                    SelectedIndex = index;
                    if (SelectedIndexChanged.HasDelegate)
                    {
                        await SelectedIndexChanged.InvokeAsync(index);
                    }
                }
                break;
            }
            case ButtonGroupSelectionMode.Multiple:
            {
                // Checkbox behavior: only the source flips.
                await source.SetSelectedInternalAsync(!source.Selected);

                var selected = new List<int>();
                for (int i = 0; i < _toggleButtons.Count; i++)
                {
                    if (_toggleButtons[i].Selected) selected.Add(i);
                }

                SelectedIndexes = selected.ToArray();
                if (SelectedIndexesChanged.HasDelegate)
                {
                    await SelectedIndexesChanged.InvokeAsync(SelectedIndexes);
                }
                break;
            }
            case ButtonGroupSelectionMode.None:
            default:
                // No coordinated state — the toggle child manages itself.
                await source.SetSelectedInternalAsync(!source.Selected);
                break;
        }

        if (SelectedChanged.HasDelegate)
        {
            await SelectedChanged.InvokeAsync(index);
        }

        StateHasChanged();
    }
}

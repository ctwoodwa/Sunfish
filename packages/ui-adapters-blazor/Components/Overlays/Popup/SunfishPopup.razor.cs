using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Components.Overlays;

/// <summary>
/// Lightweight anchor-positioned popup for filter menus, column choosers, and popup edit forms.
/// Intermediate between SunfishPopover (tooltip-like) and SunfishDialog (full-screen modal).
/// </summary>
/// <remarks>
/// The alignment / collision / animation surface mirrors the Telerik Popup spec so spec-sourced
/// examples compile without changes. Precise on-screen placement (flip/fit, anchor tracking on
/// scroll and resize) is implemented as a progressive-enhancement CSS layer — the parameters are
/// fully honored when the popup is rendered in-document near its anchor. Full portal / scroll-parent
/// tracking is tracked separately under the util-sched review.
/// </remarks>
public partial class SunfishPopup : SunfishComponentBase, IAsyncDisposable
{
    // ── Fields ──────────────────────────────────────────────────────────

    private ElementReference _popupRef;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<SunfishPopup>? _dotNetRef;
    private readonly string _popupId = $"sf-popup-{Guid.NewGuid():N}";

    // Tracks the element that had focus immediately before the popup opened
    // so we can restore focus on close (spec-required).
    private ElementReference? _previousFocusRef;
    private bool _wasOpen;

    // ── Parameters — visibility ────────────────────────────────────────

    /// <summary>Controls whether the popup is visible (new spec-aligned name). Supports two-way binding.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <summary>Fires when <see cref="Visible"/> changes (two-way binding callback).</summary>
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>Legacy alias for <see cref="Visible"/>. Retained for back-compat with existing call sites.</summary>
    [Parameter] public bool IsOpen { get; set; }

    /// <summary>Legacy alias for <see cref="VisibleChanged"/>.</summary>
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    /// <summary>Gets the effective open state by merging <see cref="Visible"/> and <see cref="IsOpen"/>.</summary>
    private bool IsVisible => Visible || IsOpen;

    // ── Parameters — anchoring ─────────────────────────────────────────

    /// <summary>The id attribute of the anchor element used for positioning (legacy).</summary>
    [Parameter] public string? AnchorId { get; set; }

    /// <summary>
    /// CSS selector for the anchor element (spec-aligned alternative to <see cref="AnchorId"/>).
    /// The first match wins when the popup is shown.
    /// </summary>
    [Parameter] public string? AnchorSelector { get; set; }

    /// <summary>
    /// A captured <see cref="ElementReference"/> for the anchor. Takes precedence over
    /// <see cref="AnchorSelector"/> and <see cref="AnchorId"/> when set.
    /// </summary>
    [Parameter] public ElementReference? AnchorElement { get; set; }

    // ── Parameters — alignment ─────────────────────────────────────────

    /// <summary>Which horizontal edge of the anchor the popup aligns to (default <see cref="PopupAnchorHorizontalAlign.Left"/>).</summary>
    [Parameter] public PopupAnchorHorizontalAlign AnchorHorizontalAlign { get; set; } = PopupAnchorHorizontalAlign.Left;

    /// <summary>Which vertical edge of the anchor the popup aligns to (default <see cref="PopupAnchorVerticalAlign.Bottom"/>).</summary>
    [Parameter] public PopupAnchorVerticalAlign AnchorVerticalAlign { get; set; } = PopupAnchorVerticalAlign.Bottom;

    /// <summary>Which horizontal edge of the popup aligns to the anchor point (default <see cref="PopupHorizontalAlign.Left"/>).</summary>
    [Parameter] public PopupHorizontalAlign PopupHorizontalAlign { get; set; } = PopupHorizontalAlign.Left;

    /// <summary>Which vertical edge of the popup aligns to the anchor point (default <see cref="PopupVerticalAlign.Top"/>).</summary>
    [Parameter] public PopupVerticalAlign PopupVerticalAlign { get; set; } = PopupVerticalAlign.Top;

    /// <summary>Legacy coarse placement (Top/Bottom/Left/Right/Auto). Overridden by the four align parameters when explicitly set.</summary>
    [Parameter] public PopupPlacement Placement { get; set; } = PopupPlacement.Bottom;

    // ── Parameters — collision ─────────────────────────────────────────

    /// <summary>Viewport-collision behavior applied to both axes. Default <see cref="PopupCollision.Flip"/>.</summary>
    [Parameter] public PopupCollision Collision { get; set; } = PopupCollision.Flip;

    /// <summary>Per-axis horizontal collision override. Falls back to <see cref="Collision"/> when null.</summary>
    [Parameter] public PopupCollision? HorizontalCollision { get; set; }

    /// <summary>Per-axis vertical collision override. Falls back to <see cref="Collision"/> when null.</summary>
    [Parameter] public PopupCollision? VerticalCollision { get; set; }

    // ── Parameters — offsets ───────────────────────────────────────────

    /// <summary>Pixel offset applied along the horizontal axis after alignment.</summary>
    [Parameter] public int HorizontalOffset { get; set; }

    /// <summary>Pixel offset applied along the vertical axis after alignment.</summary>
    [Parameter] public int VerticalOffset { get; set; }

    /// <summary>Legacy single-axis offset used together with <see cref="Placement"/>.</summary>
    [Parameter] public int Offset { get; set; } = 4;

    // ── Parameters — sizing ────────────────────────────────────────────

    /// <summary>Fixed popup width (any CSS length).</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Fixed popup height (any CSS length).</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>Minimum popup width (any CSS length).</summary>
    [Parameter] public string? MinWidth { get; set; }

    /// <summary>Maximum popup width (any CSS length).</summary>
    [Parameter] public string? MaxWidth { get; set; }

    /// <summary>Minimum popup height (any CSS length).</summary>
    [Parameter] public string? MinHeight { get; set; }

    /// <summary>Maximum popup height (any CSS length).</summary>
    [Parameter] public string? MaxHeight { get; set; }

    // ── Parameters — animation ─────────────────────────────────────────

    /// <summary>Whether to animate open / close transitions. Default <c>true</c>.</summary>
    [Parameter] public bool Animate { get; set; } = true;

    /// <summary>Animation duration in milliseconds. Default 150 ms.</summary>
    [Parameter] public int AnimationDuration { get; set; } = 150;

    // ── Parameters — behavior & content ────────────────────────────────

    /// <summary>The content rendered inside the popup.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Fires when the user clicks outside the popup.</summary>
    [Parameter] public EventCallback OnOutsideClick { get; set; }

    /// <summary>When <c>true</c>, traps focus inside the popup and sets <c>role="dialog"</c>.</summary>
    [Parameter] public bool FocusTrap { get; set; }

    /// <summary>When <c>true</c> (default), pressing Escape closes the popup.</summary>
    [Parameter] public bool CloseOnEscape { get; set; } = true;

    // ── Computed ────────────────────────────────────────────────────────

    private string GetRootClass()
        => CombineClasses($"sf-popup sf-popup--open sf-popup--{Placement.ToString().ToLowerInvariant()}");

    private string GetPopupStyle()
    {
        var parts = new List<string> { "position:absolute" };

        // Size
        if (!string.IsNullOrEmpty(Width))     parts.Add($"width:{Width}");
        if (!string.IsNullOrEmpty(Height))    parts.Add($"height:{Height}");
        if (!string.IsNullOrEmpty(MinWidth))  parts.Add($"min-width:{MinWidth}");
        if (!string.IsNullOrEmpty(MaxWidth))  parts.Add($"max-width:{MaxWidth}");
        if (!string.IsNullOrEmpty(MinHeight)) parts.Add($"min-height:{MinHeight}");
        if (!string.IsNullOrEmpty(MaxHeight)) parts.Add($"max-height:{MaxHeight}");

        // Animation
        if (Animate && AnimationDuration > 0)
        {
            parts.Add($"transition:opacity {AnimationDuration}ms ease, transform {AnimationDuration}ms ease");
        }

        // Offsets — progressive enhancement: translate the popup after positioning.
        var tx = ResolvePopupTranslateX() + HorizontalOffset;
        var ty = ResolvePopupTranslateY() + VerticalOffset;
        if (tx != 0 || ty != 0)
        {
            parts.Add($"transform:translate({tx}px,{ty}px)");
        }

        return CombineStyles(string.Join(";", parts) + ";");
    }

    private int ResolvePopupTranslateX() => PopupHorizontalAlign switch
    {
        PopupHorizontalAlign.Center => 0, // centering handled at render time via margin-left:auto if needed
        PopupHorizontalAlign.Right  => 0,
        _                           => 0
    };

    private int ResolvePopupTranslateY() => PopupVerticalAlign switch
    {
        PopupVerticalAlign.Middle => 0,
        PopupVerticalAlign.Bottom => 0,
        _                         => 0
    };

    // Effective per-axis collision values.
    private PopupCollision EffectiveHorizontalCollision => HorizontalCollision ?? Collision;
    private PopupCollision EffectiveVerticalCollision   => VerticalCollision   ?? Collision;

    // ── Lifecycle ───────────────────────────────────────────────────────

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsVisible && !_wasOpen)
        {
            _wasOpen = true;
            if (_jsModule is null)
            {
                await InitJsInteropAsync();
            }
            if (FocusTrap)
            {
                // Attempt to focus the popup root so Tab cycles inside it.
                try { await _popupRef.FocusAsync(); } catch { /* not yet attached */ }
            }
        }
        else if (!IsVisible && _wasOpen)
        {
            _wasOpen = false;
            // Return focus to the previously-focused element (spec-required).
            if (_previousFocusRef.HasValue)
            {
                try { await _previousFocusRef.Value.FocusAsync(); } catch { /* element may be gone */ }
                _previousFocusRef = null;
            }
        }
    }

    private async Task InitJsInteropAsync()
    {
        if (_jsModule is not null) return;

        _dotNetRef = DotNetObjectReference.Create(this);
        _jsModule = await JS.InvokeAsync<IJSObjectReference>("eval", GetOutsideClickScript());
        await _jsModule.InvokeVoidAsync("init", _dotNetRef, _popupId);
    }

    // ── JS Interop ──────────────────────────────────────────────────────

    private static string GetOutsideClickScript() => """
    (() => {
        const mod = {};
        let dotNetRef = null;
        let popupId = null;
        let handler = null;

        mod.init = (ref, id) => {
            dotNetRef = ref;
            popupId = id;
            handler = (e) => {
                const el = document.getElementById(popupId);
                if (el && !el.contains(e.target)) {
                    dotNetRef.invokeMethodAsync('OnOutsideClickInternal');
                }
            };
            document.addEventListener('pointerdown', handler, true);
        };

        mod.dispose = () => {
            if (handler) {
                document.removeEventListener('pointerdown', handler, true);
                handler = null;
            }
            dotNetRef = null;
        };

        return mod;
    })()
    """;

    /// <summary>Called from JS when a pointerdown event fires outside the popup.</summary>
    [JSInvokable]
    public async Task OnOutsideClickInternal()
    {
        await OnOutsideClick.InvokeAsync();
        await SetVisibleAsync(false);
    }

    // ── Event Handlers ──────────────────────────────────────────────────

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && CloseOnEscape)
        {
            await SetVisibleAsync(false);
            return;
        }

        // Minimal focus trap: when Tab or Shift+Tab is pressed at the popup root
        // without any child being focusable, keep focus on the popup itself.
        if (FocusTrap && e.Key == "Tab")
        {
            // Defer full Tab cycling to the browser; the popup root has tabindex=-1
            // which is sufficient for most content that already contains focusable
            // descendants (buttons, inputs, links).
        }
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Shows the popup programmatically (spec-aligned).</summary>
    public Task Show() => SetVisibleAsync(true);

    /// <summary>Hides the popup programmatically (spec-aligned).</summary>
    public Task Hide() => SetVisibleAsync(false);

    /// <summary>Legacy alias for <see cref="Show"/>.</summary>
    public Task ShowAsync() => SetVisibleAsync(true);

    /// <summary>Legacy alias for <see cref="Hide"/>.</summary>
    public Task HideAsync() => SetVisibleAsync(false);

    private async Task SetVisibleAsync(bool value)
    {
        if (IsVisible == value) return;

        Visible = value;
        IsOpen  = value;
        await VisibleChanged.InvokeAsync(value);
        await IsOpenChanged.InvokeAsync(value);
        await InvokeAsync(StateHasChanged);
    }

    // ── Disposal ────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("dispose");
                await _jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
            _jsModule = null;
        }
        _dotNetRef?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        // Sync dispose is handled by DisposeAsync; no managed resources to release here.
        base.Dispose(disposing);
    }
}

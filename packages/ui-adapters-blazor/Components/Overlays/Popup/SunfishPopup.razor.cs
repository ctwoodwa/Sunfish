using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Components.Overlays;

/// <summary>
/// Lightweight anchor-positioned popup for filter menus, column choosers, and popup edit forms.
/// Intermediate between SunfishPopover (tooltip-like) and SunfishDialog (full-screen modal).
/// </summary>
public partial class SunfishPopup : SunfishComponentBase, IAsyncDisposable
{
    // ── Fields ──────────────────────────────────────────────────────────

    private ElementReference _popupRef;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<SunfishPopup>? _dotNetRef;
    private readonly string _popupId = $"mar-popup-{Guid.NewGuid():N}";

    // ── Parameters ─────────────────────────────────────────────────────

    /// <summary>Controls whether the popup is visible. Supports two-way binding.</summary>
    [Parameter] public bool IsOpen { get; set; }

    /// <summary>Fires when the open state changes (two-way binding callback).</summary>
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    /// <summary>The id attribute of the anchor element used for positioning.</summary>
    [Parameter] public string? AnchorId { get; set; }

    /// <summary>The preferred placement of the popup relative to the anchor element.</summary>
    [Parameter] public PopupPlacement Placement { get; set; } = PopupPlacement.Bottom;

    /// <summary>Pixel offset from the anchor element.</summary>
    [Parameter] public int Offset { get; set; } = 4;

    /// <summary>The content rendered inside the popup.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Fires when the user clicks outside the popup.</summary>
    [Parameter] public EventCallback OnOutsideClick { get; set; }

    /// <summary>When true, traps focus inside the popup and sets role="dialog".</summary>
    [Parameter] public bool FocusTrap { get; set; }

    /// <summary>When true (default), pressing Escape closes the popup.</summary>
    [Parameter] public bool CloseOnEscape { get; set; } = true;

    // ── Computed ────────────────────────────────────────────────────────

    private string GetRootClass()
        => CombineClasses($"mar-popup mar-popup--open mar-popup--{Placement.ToString().ToLowerInvariant()}");

    private string? GetPopupStyle()
        => CombineStyles("position:absolute;");

    // ── Lifecycle ───────────────────────────────────────────────────────

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsOpen && _jsModule is null)
        {
            await InitJsInteropAsync();
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
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(false);
        await InvokeAsync(StateHasChanged);
    }

    // ── Event Handlers ──────────────────────────────────────────────────

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && CloseOnEscape)
        {
            IsOpen = false;
            await IsOpenChanged.InvokeAsync(false);
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Shows the popup programmatically.</summary>
    public async Task ShowAsync()
    {
        if (!IsOpen)
        {
            IsOpen = true;
            await IsOpenChanged.InvokeAsync(true);
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>Hides the popup programmatically.</summary>
    public async Task HideAsync()
    {
        if (IsOpen)
        {
            IsOpen = false;
            await IsOpenChanged.InvokeAsync(false);
            await InvokeAsync(StateHasChanged);
        }
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

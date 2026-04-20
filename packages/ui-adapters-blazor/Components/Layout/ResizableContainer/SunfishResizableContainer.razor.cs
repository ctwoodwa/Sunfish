using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Components.Layout.ResizableContainer;

/// <summary>
/// A wrapper component that allows end users to resize its child content via drag handles.
/// Supports configurable edges/corners, min/max constraints, keyboard resizing,
/// ResizeObserver integration, and optional size persistence.
/// </summary>
public partial class SunfishResizableContainer : SunfishComponentBase, IAsyncDisposable
{
    // ── Parameters: Content ────────────────────────────────────────────

    /// <summary>Content to render inside the resizable container.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    // ── Parameters: Sizing ─────────────────────────────────────────────

    /// <summary>Current width as a CSS value (e.g., "100%", "400px", "calc(100vw - 2rem)").</summary>
    [Parameter] public string Width { get; set; } = "100%";

    /// <summary>Current height as a CSS value.</summary>
    [Parameter] public string Height { get; set; } = "320px";

    /// <summary>Minimum width constraint (CSS value).</summary>
    [Parameter] public string? MinWidth { get; set; }

    /// <summary>Minimum height constraint (CSS value).</summary>
    [Parameter] public string? MinHeight { get; set; }

    /// <summary>Maximum width constraint (CSS value).</summary>
    [Parameter] public string? MaxWidth { get; set; }

    /// <summary>Maximum height constraint (CSS value).</summary>
    [Parameter] public string? MaxHeight { get; set; }

    // ── Parameters: Behavior ───────────────────────────────────────────

    /// <summary>Whether resizing is enabled.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>Whether to render the resize handle(s).</summary>
    [Parameter] public bool ShowHandle { get; set; } = true;

    /// <summary>Which edges/corners have resize handles.</summary>
    [Parameter] public ResizeEdges ResizeEdges { get; set; } = ResizeEdges.BottomRight;

    /// <summary>Whether to use ResizeObserver to detect size changes.</summary>
    [Parameter] public bool ObserveSizeChanges { get; set; } = true;

    /// <summary>Whether to persist resized dimensions in browser storage.</summary>
    [Parameter] public bool PersistSize { get; set; }

    /// <summary>Storage key for persisted dimensions.</summary>
    [Parameter] public string? PersistKey { get; set; }

    /// <summary>Show ghost outline during drag instead of live resize.</summary>
    [Parameter] public bool UseGhostOutline { get; set; }

    /// <summary>Constrain resize within parent element bounds.</summary>
    [Parameter] public bool ClampToParent { get; set; }

    /// <summary>Disable text selection while dragging.</summary>
    [Parameter] public bool DisableTextSelection { get; set; } = true;

    /// <summary>Allow keyboard arrow-key resizing when handle is focused.</summary>
    [Parameter] public bool KeyboardResizeEnabled { get; set; } = true;

    // ── Parameters: Handle / UX ────────────────────────────────────────

    /// <summary>Accessible label for the resize handle (defaults to "Resize").</summary>
    [Parameter] public string? HandleAriaLabel { get; set; }

    /// <summary>Additional CSS class for the handle element.</summary>
    [Parameter] public string? HandleClass { get; set; }

    /// <summary>Additional inline style for the handle element.</summary>
    [Parameter] public string? HandleStyle { get; set; }

    // ── Events ─────────────────────────────────────────────────────────

    /// <summary>Fires when a drag resize begins.</summary>
    [Parameter] public EventCallback<SunfishResizeEventArgs> OnResizeStart { get; set; }

    /// <summary>Fires on each frame during a drag resize.</summary>
    [Parameter] public EventCallback<SunfishResizeEventArgs> OnResizing { get; set; }

    /// <summary>Fires when a drag resize ends.</summary>
    [Parameter] public EventCallback<SunfishResizeEventArgs> OnResizeEnd { get; set; }

    /// <summary>Fires when ResizeObserver detects a size change (any cause).</summary>
    [Parameter] public EventCallback<SunfishObservedSizeChangedEventArgs> OnObservedSizeChanged { get; set; }

    /// <summary>Two-way binding callback for Width.</summary>
    [Parameter] public EventCallback<string> WidthChanged { get; set; }

    /// <summary>Two-way binding callback for Height.</summary>
    [Parameter] public EventCallback<string> HeightChanged { get; set; }

    // ── Private State ──────────────────────────────────────────────────

    private ElementReference _containerRef;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<SunfishResizableContainer>? _dotNetRef;
    private bool _isDragging;
    private bool _handleFocused;
    private string _currentWidth = "100%";
    private string _currentHeight = "320px";
    private double _ghostWidth;
    private double _ghostHeight;
    private string _initialWidth = "100%";
    private string _initialHeight = "320px";

    private string _containerId = $"mar-rc-{Guid.NewGuid():N}";

    // ── Computed Properties ────────────────────────────────────────────

    private string _containerClass => CombineClasses(
        CssProvider.ResizableContainerClass(_isDragging, !Enabled));

    private string _contentClass => CssProvider.ResizableContainerContentClass();

    private string _containerStyle
    {
        get
        {
            var sb = StyleBuilder.Clear()
                .AddStyle($"width:{_currentWidth}")
                .AddStyle($"height:{_currentHeight}");

            if (MinWidth != null) sb.AddStyle($"min-width:{MinWidth}");
            if (MinHeight != null) sb.AddStyle($"min-height:{MinHeight}");
            if (MaxWidth != null) sb.AddStyle($"max-width:{MaxWidth}");
            if (MaxHeight != null) sb.AddStyle($"max-height:{MaxHeight}");

            return CombineStyles(sb.Build());
        }
    }

    private string? _ghostStyle => UseGhostOutline && _isDragging
        ? $"width:{_ghostWidth}px;height:{_ghostHeight}px"
        : null;

    private List<ResizeEdges> _activeHandles
    {
        get
        {
            var handles = new List<ResizeEdges>();
            if (ResizeEdges == ResizeEdges.None) return handles;

            // Check individual edges and composed corners
            if (ResizeEdges.HasFlag(ResizeEdges.Top) && ResizeEdges.HasFlag(ResizeEdges.Left))
                handles.Add(ResizeEdges.TopLeft);
            else
            {
                if (ResizeEdges.HasFlag(ResizeEdges.Top)) handles.Add(ResizeEdges.Top);
                if (ResizeEdges.HasFlag(ResizeEdges.Left)) handles.Add(ResizeEdges.Left);
            }

            if (ResizeEdges.HasFlag(ResizeEdges.Top) && ResizeEdges.HasFlag(ResizeEdges.Right))
                handles.Add(ResizeEdges.TopRight);
            else if (ResizeEdges.HasFlag(ResizeEdges.Right) && !handles.Contains(ResizeEdges.TopRight))
                handles.Add(ResizeEdges.Right);

            if (ResizeEdges.HasFlag(ResizeEdges.Bottom) && ResizeEdges.HasFlag(ResizeEdges.Left))
                handles.Add(ResizeEdges.BottomLeft);

            if (ResizeEdges.HasFlag(ResizeEdges.Bottom) && ResizeEdges.HasFlag(ResizeEdges.Right))
                handles.Add(ResizeEdges.BottomRight);
            else if (ResizeEdges.HasFlag(ResizeEdges.Bottom) && !handles.Contains(ResizeEdges.BottomLeft))
                handles.Add(ResizeEdges.Bottom);

            return handles;
        }
    }

    private string GetHandleClass(ResizeEdges edge)
    {
        var providerClass = CssProvider.ResizableContainerHandleClass(edge, _isDragging, _handleFocused);
        return HandleClass != null ? $"{providerClass} {HandleClass}" : providerClass;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        _currentWidth = Width;
        _currentHeight = Height;
        _initialWidth = Width;
        _initialHeight = Height;
    }

    protected override void OnParametersSet()
    {
        if (!_isDragging)
        {
            _currentWidth = Width;
            _currentHeight = Height;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _jsModule = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Sunfish.Components/js/resizable-container.js");

            await _jsModule.InvokeVoidAsync("init", _containerRef, _dotNetRef, new
            {
                containerId = _containerId,
                enabled = Enabled,
                resizeEdges = (int)ResizeEdges,
                observeSizeChanges = ObserveSizeChanges,
                persistSize = PersistSize,
                persistKey = PersistKey,
                useGhostOutline = UseGhostOutline,
                clampToParent = ClampToParent,
                disableTextSelection = DisableTextSelection,
                keyboardResizeEnabled = KeyboardResizeEnabled,
                minWidth = MinWidth,
                minHeight = MinHeight,
                maxWidth = MaxWidth,
                maxHeight = MaxHeight
            });
        }
    }

    // ── JSInvokable Callbacks ──────────────────────────────────────────

    [JSInvokable]
    public async Task OnResizeStartFromJs(double widthPx, double heightPx, int activeEdge)
    {
        _isDragging = true;
        var args = CreateResizeArgs(widthPx, heightPx, (ResizeEdges)activeEdge, true);
        await OnResizeStart.InvokeAsync(args);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnResizingFromJs(double widthPx, double heightPx, int activeEdge)
    {
        if (UseGhostOutline)
        {
            _ghostWidth = widthPx;
            _ghostHeight = heightPx;
        }
        else
        {
            _currentWidth = $"{widthPx}px";
            _currentHeight = $"{heightPx}px";
        }

        var args = CreateResizeArgs(widthPx, heightPx, (ResizeEdges)activeEdge, true);
        await OnResizing.InvokeAsync(args);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnResizeEndFromJs(double widthPx, double heightPx, int activeEdge)
    {
        _isDragging = false;
        _currentWidth = $"{widthPx}px";
        _currentHeight = $"{heightPx}px";

        var args = CreateResizeArgs(widthPx, heightPx, (ResizeEdges)activeEdge, true);
        await OnResizeEnd.InvokeAsync(args);
        await WidthChanged.InvokeAsync(_currentWidth);
        await HeightChanged.InvokeAsync(_currentHeight);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnObservedSizeChangedFromJs(double widthPx, double heightPx)
    {
        var args = new SunfishObservedSizeChangedEventArgs
        {
            Width = $"{widthPx}px",
            Height = $"{heightPx}px",
            WidthPixels = widthPx,
            HeightPixels = heightPx
        };
        await OnObservedSizeChanged.InvokeAsync(args);
    }

    [JSInvokable]
    public async Task OnPersistedSizeRestoredFromJs(double widthPx, double heightPx)
    {
        _currentWidth = $"{widthPx}px";
        _currentHeight = $"{heightPx}px";
        await WidthChanged.InvokeAsync(_currentWidth);
        await HeightChanged.InvokeAsync(_currentHeight);
        await InvokeAsync(StateHasChanged);
    }

    // ── Public Methods ─────────────────────────────────────────────────

    /// <summary>Programmatically set container dimensions.</summary>
    public async Task SetSizeAsync(string width, string height)
    {
        _currentWidth = width;
        _currentHeight = height;
        await WidthChanged.InvokeAsync(_currentWidth);
        await HeightChanged.InvokeAsync(_currentHeight);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Reset to initial Width/Height values.</summary>
    public async Task ResetSizeAsync()
    {
        _currentWidth = _initialWidth;
        _currentHeight = _initialHeight;
        await WidthChanged.InvokeAsync(_currentWidth);
        await HeightChanged.InvokeAsync(_currentHeight);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Focus the primary resize handle.</summary>
    public async Task FocusHandleAsync()
    {
        if (_jsModule != null)
        {
            await _jsModule.InvokeVoidAsync("focusHandle");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static SunfishResizeEventArgs CreateResizeArgs(
        double widthPx, double heightPx, ResizeEdges activeEdge, bool isUserInitiated)
    {
        return new SunfishResizeEventArgs
        {
            Width = $"{widthPx}px",
            Height = $"{heightPx}px",
            WidthPixels = widthPx,
            HeightPixels = heightPx,
            ActiveEdge = activeEdge,
            IsUserInitiated = isUserInitiated
        };
    }

    // ── Disposal ────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_jsModule != null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("dispose");
                await _jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }
        _dotNetRef?.Dispose();
    }
}

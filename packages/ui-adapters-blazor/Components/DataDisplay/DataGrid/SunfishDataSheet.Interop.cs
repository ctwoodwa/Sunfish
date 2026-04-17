using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// JS interop for clipboard, focus management, scroll-to-row, and keyboard handler registration.
/// </summary>
public partial class SunfishDataSheet<TItem> : IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<SunfishDataSheet<TItem>>? _dotNetRef;
    private string _gridId = $"mar-datasheet-{Guid.NewGuid():N}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            try
            {
                _jsModule = await JS.InvokeAsync<IJSObjectReference>(
                    "import",
                    "./_content/Sunfish.Components/js/marilo-datasheet.js");

                await _jsModule.InvokeVoidAsync("registerKeydownHandler", _gridId, _dotNetRef);
            }
            catch (JSException)
            {
                // Module may not be available in test environments
            }
        }
    }

    /// <summary>Scrolls the row with the given key into view.</summary>
    public async Task ScrollToRowAsync(object key)
    {
        if (_jsModule != null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("scrollToRow", _gridId, key.ToString());
            }
            catch (JSDisconnectedException) { }
        }
    }

    internal async Task CopyToClipboardAsync(string text)
    {
        if (_jsModule != null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("copyToClipboard", text);
            }
            catch (JSDisconnectedException) { }
        }
    }

    internal async Task FocusCellAsync(object rowKey, string field)
    {
        if (_jsModule != null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("focusCell", _gridId, rowKey.ToString(), field);
            }
            catch (JSDisconnectedException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsModule != null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("unregisterKeydownHandler", _gridId);
                await _jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }
        _dotNetRef?.Dispose();
    }
}

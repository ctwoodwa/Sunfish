using Sunfish.UICore.Contracts;
using Microsoft.JSInterop;

namespace Sunfish.Providers.Bootstrap;

public class BootstrapJsInterop : ISunfishJsInterop
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public BootstrapJsInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask InitializeAsync()
    {
        _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sunfish.Providers.Bootstrap/js/sunfish-bootstrap.js");
    }

    public async ValueTask<bool> ShowModalAsync(string modalId)
    {
        if (_module is null) await InitializeAsync();
        return await _module!.InvokeAsync<bool>("showModal", modalId);
    }

    public async ValueTask HideModalAsync(string modalId)
    {
        if (_module is null) await InitializeAsync();
        await _module!.InvokeVoidAsync("hideModal", modalId);
    }

    public async ValueTask<BoundingBox> GetElementBoundsAsync(string elementId)
    {
        if (_module is null) await InitializeAsync();
        return await _module!.InvokeAsync<BoundingBox>("getElementBounds", elementId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}

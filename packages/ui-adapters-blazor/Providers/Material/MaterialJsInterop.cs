using Sunfish.UICore.Contracts;
using Microsoft.JSInterop;

namespace Sunfish.Providers.Material;

public class MaterialJsInterop : ISunfishJsInterop
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public MaterialJsInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask InitializeAsync()
    {
        _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Sunfish.Providers.Material/js/sunfish-material.js");
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

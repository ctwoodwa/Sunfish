using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Sunfish.UICore.Primitives;

namespace Sunfish.UIAdapters.Blazor.A11y;

/// <summary>
/// Blazor adapter for <see cref="ILiveAnnouncer"/> per ADR 0077 §4 + §6.
/// Bridges to the browser <c>aria-live</c> region via <c>sunfish-a11y.js</c>.
/// </summary>
public sealed class BlazorLiveAnnouncer : ILiveAnnouncer, IAsyncDisposable
{
    private const string ModuleUri =
        "./_content/Sunfish.UIAdapters.Blazor/js/sunfish-a11y.js";

    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public BlazorLiveAnnouncer(IJSRuntime js) => _js = js;

    /// <inheritdoc/>
    /// <remarks>
    /// Fire-and-forget per the side-effect-free contract on
    /// <see cref="ILiveAnnouncer"/>. JS errors are swallowed; the
    /// renderer must not throw on announcement failure.
    /// </remarks>
    public void Announce(string message, LiveRegionPoliteness politeness)
        => _ = AnnounceInternalAsync(message, politeness);

    /// <summary>Awaitable path used by tests to avoid fire-and-forget timing.</summary>
    internal Task AnnounceAsync(string message, LiveRegionPoliteness politeness)
        => AnnounceInternalAsync(message, politeness);

    private async Task AnnounceInternalAsync(string message, LiveRegionPoliteness politeness)
    {
        try
        {
            var module = await EnsureModuleAsync().ConfigureAwait(false);
            await module.InvokeVoidAsync(
                "announce",
                CancellationToken.None,
                message,
                politeness.ToString().ToLowerInvariant()).ConfigureAwait(false);
        }
        catch (JSDisconnectedException) { /* component unmounted mid-flight */ }
        catch (TaskCanceledException)   { /* navigation or disposal */ }
    }

    private async ValueTask<IJSObjectReference> EnsureModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModuleUri)
                              .ConfigureAwait(false);
        return _module;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.DisposeAsync().ConfigureAwait(false); }
            catch (JSDisconnectedException) { /* circuit already gone */ }
        }
    }
}

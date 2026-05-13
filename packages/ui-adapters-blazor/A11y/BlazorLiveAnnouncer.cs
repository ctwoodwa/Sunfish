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
    // Task<T> field set on first load; subsequent callers await the same task.
    // Safe in Blazor's single-threaded execution model (one circuit per instance).
    private Task<IJSObjectReference>? _moduleTask;

    public BlazorLiveAnnouncer(IJSRuntime js) => _js = js;

    /// <inheritdoc/>
    /// <remarks>
    /// Fire-and-forget per the side-effect-free contract on
    /// <see cref="ILiveAnnouncer"/>. JS errors are swallowed; the
    /// renderer must not throw on announcement failure.
    ///
    /// TRUST BOUNDARY: <paramref name="message"/> is passed to the browser via
    /// JS <c>textContent</c> (no HTML parse — XSS-safe), but callers must not
    /// pass untrusted content without length bounds. Unbounded strings can cause
    /// screen-reader DoS. Validate at the application boundary before calling.
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
        catch (JSException)             { /* module load failure (404) or JS error */ }
        catch (TaskCanceledException)   { /* navigation or disposal */ }
    }

    private ValueTask<IJSObjectReference> EnsureModuleAsync()
    {
        _moduleTask ??= _js.InvokeAsync<IJSObjectReference>("import", ModuleUri).AsTask();
        return new ValueTask<IJSObjectReference>(_moduleTask);
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask is not null)
        {
            try
            {
                var module = await _moduleTask.ConfigureAwait(false);
                await module.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException) { /* circuit already gone */ }
            catch (JSException)             { /* module never loaded */  }
        }
    }
}

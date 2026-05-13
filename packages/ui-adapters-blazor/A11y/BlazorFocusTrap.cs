using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Sunfish.UICore.Primitives;

namespace Sunfish.UIAdapters.Blazor.A11y;

/// <summary>
/// Blazor adapter for <see cref="IFocusTrap"/> per ADR 0077 §4 + WCAG 2.2
/// SC 2.4.3 (Focus Order) + SC 2.1.2 (No Keyboard Trap).
/// Traps focus within a container element via <c>sunfish-a11y.js</c>.
/// The container must have id=<c>ContainerId</c> (or <c>data-focustrap-id</c>)
/// in the rendered DOM before <see cref="EnterAsync"/> is called.
/// </summary>
public sealed class BlazorFocusTrap : IFocusTrap, IAsyncDisposable
{
    private const string ModuleUri =
        "./_content/Sunfish.UIAdapters.Blazor/js/sunfish-a11y.js";

    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private string? _activeContainerId;

    public BlazorFocusTrap(IJSRuntime js) => _js = js;

    /// <summary>
    /// The DOM id of the container to trap focus within. Set before calling
    /// <see cref="EnterAsync"/>. Defaults to a fresh GUID each enter cycle.
    /// </summary>
    public string ContainerId { get; set; } = Guid.NewGuid().ToString("N");

    /// <inheritdoc/>
    public async ValueTask EnterAsync(CancellationToken ct = default)
    {
        // Re-entry without exit: log-and-ignore per IFocusTrap contract.
        if (_activeContainerId is not null) return;

        var module = await EnsureModuleAsync().ConfigureAwait(false);
        _activeContainerId = ContainerId;
        await module.InvokeVoidAsync("trapFocus", ct, _activeContainerId).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ExitAsync(CancellationToken ct = default)
    {
        if (_activeContainerId is null) return;
        var module = await EnsureModuleAsync().ConfigureAwait(false);
        var id = _activeContainerId;
        _activeContainerId = null;
        await module.InvokeVoidAsync("releaseFocus", ct, id).ConfigureAwait(false);
    }

    private async ValueTask<IJSObjectReference> EnsureModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModuleUri)
                              .ConfigureAwait(false);
        return _module;
    }

    public async ValueTask DisposeAsync()
    {
        if (_activeContainerId is not null)
        {
            try { await ExitAsync().ConfigureAwait(false); }
            catch (JSDisconnectedException) { /* circuit gone */ }
        }

        if (_module is not null)
        {
            try { await _module.DisposeAsync().ConfigureAwait(false); }
            catch (JSDisconnectedException) { /* circuit already gone */ }
        }
    }
}

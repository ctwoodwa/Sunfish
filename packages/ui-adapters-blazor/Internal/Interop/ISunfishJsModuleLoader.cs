using Microsoft.JSInterop;

namespace Sunfish.Components.Blazor.Internal.Interop;

/// <summary>
/// Manages lazy loading, caching, and disposal of JS ES modules.
/// Components should not import modules directly; instead, use this service
/// to ensure consistent module paths and lifecycle management.
/// </summary>
internal interface ISunfishJsModuleLoader : IAsyncDisposable
{
    /// <summary>
    /// Imports a JS module from the package static content path.
    /// The module is cached after first import. Subsequent calls return the cached reference.
    /// </summary>
    /// <param name="modulePath">
    /// Path relative to the package wwwroot, e.g., "js/marilo-measurement.js".
    /// The loader prepends the correct static content prefix.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the import operation.</param>
    ValueTask<IJSObjectReference> ImportAsync(string modulePath, CancellationToken cancellationToken = default);
}

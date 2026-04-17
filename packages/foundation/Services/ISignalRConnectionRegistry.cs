using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models;

namespace Sunfish.Foundation.Services;

/// <summary>
/// Manages registration, lifecycle tracking, and health reporting for
/// application-level SignalR hub connections. The UI component binds to
/// <see cref="GetSnapshot"/> and <see cref="Changed"/> to render state.
/// </summary>
public interface ISignalRConnectionRegistry : IAsyncDisposable
{
    /// <summary>Returns a point-in-time snapshot of all registered hub connection statuses.</summary>
    IReadOnlyList<HubConnectionStatusItem> GetSnapshot();

    /// <summary>Computed aggregate state across all critical hubs.</summary>
    AggregateConnectionState AggregateState { get; }

    /// <summary>Fires on every lifecycle transition, retry count change, or health recalculation.</summary>
    event Action? Changed;

    /// <summary>
    /// Registers a hub connection. The factory is invoked immediately, and lifecycle
    /// handlers (Reconnecting, Reconnected, Closed) are attached before any start call.
    /// </summary>
    Task RegisterAsync(SignalRHubRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>Starts all registered hub connections with initial-connect retry logic.</summary>
    Task StartAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Gracefully stops all registered hub connections.</summary>
    Task StopAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Manually retries a single hub by name (for disconnected hubs).</summary>
    Task RetryAsync(string name, CancellationToken cancellationToken = default);
}

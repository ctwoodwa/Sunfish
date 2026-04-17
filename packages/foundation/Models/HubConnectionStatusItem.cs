using Sunfish.Foundation.Enums;
using Microsoft.AspNetCore.SignalR.Client;

namespace Sunfish.Foundation.Models;

/// <summary>
/// Read-only snapshot of a single registered SignalR hub connection,
/// suitable for UI binding in the connection status popup.
/// </summary>
/// <param name="Name">Display name for the hub (e.g. "Notifications", "Presence").</param>
/// <param name="Endpoint">Hub endpoint URI or logical key.</param>
/// <param name="IsCritical">Whether this hub is required for the app to be considered online.</param>
/// <param name="State">Raw SignalR connection state.</param>
/// <param name="Health">Application-level health derived from state plus heartbeat freshness.</param>
/// <param name="LastConnectedAt">Timestamp of the last successful connection, or null if never connected.</param>
/// <param name="LastStateChangedAt">Timestamp of the most recent state transition.</param>
/// <param name="LastError">Most recent exception message, truncated for display. Null if no error.</param>
/// <param name="RetryCount">Current reconnect attempt number, or null if not reconnecting.</param>
/// <param name="ConnectionId">Current SignalR connection ID, or null if disconnected.</param>
public sealed record HubConnectionStatusItem(
    string Name,
    string Endpoint,
    bool IsCritical,
    HubConnectionState State,
    ConnectionHealthState Health,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset LastStateChangedAt,
    string? LastError,
    int? RetryCount,
    string? ConnectionId);

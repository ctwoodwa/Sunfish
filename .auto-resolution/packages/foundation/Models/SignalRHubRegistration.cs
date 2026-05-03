using Microsoft.AspNetCore.SignalR.Client;

namespace Sunfish.Foundation.Models;

/// <summary>
/// Registration descriptor for a SignalR hub connection. The factory delegate
/// allows auth tokens, headers, and environment-specific URLs to be injected cleanly.
/// </summary>
/// <param name="Name">Display name for the hub (e.g. "Notifications").</param>
/// <param name="Endpoint">Hub endpoint URI.</param>
/// <param name="IsCritical">Whether this hub is required for the app to be considered online.</param>
/// <param name="Factory">
/// Factory that builds a configured <see cref="HubConnection"/>.
/// Called once during registration. Attach <c>WithAutomaticReconnect</c> inside the factory
/// because the registry will attach lifecycle handlers before calling <c>StartAsync</c>.
/// </param>
public sealed record SignalRHubRegistration(
    string Name,
    string Endpoint,
    bool IsCritical,
    Func<IServiceProvider, HubConnection> Factory);

using Sunfish.Foundation.Services;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Sunfish.Foundation.Services;

/// <summary>
/// Default implementation of <see cref="ISignalRConnectionRegistry"/>.
/// Owns all tracked <see cref="HubConnection"/> instances, subscribes to
/// SignalR lifecycle events, and exposes a read-only snapshot for UI binding.
/// </summary>
public class SignalRConnectionRegistry : ISignalRConnectionRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SignalRConnectionRegistry> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly List<TrackedHub> _hubs = [];
    private readonly object _lock = new();

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public AggregateConnectionState AggregateState
    {
        get
        {
            lock (_lock)
            {
                return ComputeAggregateState();
            }
        }
    }

    public SignalRConnectionRegistry(
        IServiceProvider serviceProvider,
        ILogger<SignalRConnectionRegistry> logger,
        TimeProvider? timeProvider = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task RegisterAsync(SignalRHubRegistration registration, CancellationToken cancellationToken = default)
    {
        var connection = registration.Factory(_serviceProvider);
        var tracked = new TrackedHub(registration, connection, _timeProvider.GetUtcNow());

        // Attach lifecycle handlers before StartAsync per Microsoft docs
        connection.Reconnecting += error =>
        {
            UpdateState(tracked, HubConnectionState.Reconnecting, error?.Message);
            return Task.CompletedTask;
        };

        connection.Reconnected += connectionId =>
        {
            lock (_lock)
            {
                tracked.ConnectionId = connectionId;
                tracked.LastConnectedAt = _timeProvider.GetUtcNow();
                tracked.RetryCount = null;
                tracked.LastError = null;
            }
            UpdateState(tracked, HubConnectionState.Connected, null);
            return Task.CompletedTask;
        };

        connection.Closed += error =>
        {
            UpdateState(tracked, HubConnectionState.Disconnected, error?.Message);
            return Task.CompletedTask;
        };

        lock (_lock)
        {
            _hubs.Add(tracked);
        }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        List<TrackedHub> snapshot;
        lock (_lock)
        {
            snapshot = [.. _hubs];
        }

        var tasks = snapshot.Select(h => StartWithRetryAsync(h, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        List<TrackedHub> snapshot;
        lock (_lock)
        {
            snapshot = [.. _hubs];
        }

        foreach (var hub in snapshot)
        {
            try
            {
                await hub.Connection.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping hub {Name}", hub.Registration.Name);
            }
        }
    }

    /// <inheritdoc />
    public async Task RetryAsync(string name, CancellationToken cancellationToken = default)
    {
        TrackedHub? hub;
        lock (_lock)
        {
            hub = _hubs.Find(h => h.Registration.Name == name);
        }

        if (hub is null)
            throw new InvalidOperationException($"Hub '{name}' is not registered.");

        await StartWithRetryAsync(hub, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<HubConnectionStatusItem> GetSnapshot()
    {
        lock (_lock)
        {
            return _hubs.Select(h => new HubConnectionStatusItem(
                Name: h.Registration.Name,
                Endpoint: h.Registration.Endpoint,
                IsCritical: h.Registration.IsCritical,
                State: h.Connection.State,
                Health: MapHealth(h),
                LastConnectedAt: h.LastConnectedAt,
                LastStateChangedAt: h.LastStateChangedAt,
                LastError: h.LastError,
                RetryCount: h.RetryCount,
                ConnectionId: h.ConnectionId
            )).ToList().AsReadOnly();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        List<TrackedHub> snapshot;
        lock (_lock)
        {
            snapshot = [.. _hubs];
            _hubs.Clear();
        }

        foreach (var hub in snapshot)
        {
            try
            {
                await hub.Connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing hub {Name}", hub.Registration.Name);
            }
        }

        GC.SuppressFinalize(this);
    }

    // ── Private helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Initial StartAsync is NOT covered by WithAutomaticReconnect.
    /// This method implements explicit retry for first connect.
    /// </summary>
    private async Task StartWithRetryAsync(TrackedHub hub, CancellationToken cancellationToken)
    {
        const int maxRetries = 5;
        TimeSpan[] delays = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

        UpdateState(hub, HubConnectionState.Connecting, null);

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    lock (_lock)
                    {
                        hub.RetryCount = attempt;
                    }
                    Changed?.Invoke();
                    await Task.Delay(delays[attempt], cancellationToken);
                }

                await hub.Connection.StartAsync(cancellationToken);

                lock (_lock)
                {
                    hub.ConnectionId = hub.Connection.ConnectionId;
                    hub.LastConnectedAt = _timeProvider.GetUtcNow();
                    hub.RetryCount = null;
                    hub.LastError = null;
                }
                UpdateState(hub, HubConnectionState.Connected, null);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hub {Name} start attempt {Attempt} failed", hub.Registration.Name, attempt + 1);
                lock (_lock)
                {
                    hub.LastError = Truncate(ex.Message, 200);
                }
            }
        }

        // All retries exhausted
        UpdateState(hub, HubConnectionState.Disconnected, hub.LastError);
    }

    private void UpdateState(TrackedHub hub, HubConnectionState state, string? error)
    {
        lock (_lock)
        {
            hub.LastStateChangedAt = _timeProvider.GetUtcNow();
            if (error is not null)
                hub.LastError = Truncate(error, 200);
        }
        Changed?.Invoke();
    }

    private static ConnectionHealthState MapHealth(TrackedHub hub)
    {
        return hub.Connection.State switch
        {
            HubConnectionState.Connected when hub.LastError is null => ConnectionHealthState.Healthy,
            HubConnectionState.Connected => ConnectionHealthState.Degraded,
            HubConnectionState.Connecting => ConnectionHealthState.Connecting,
            HubConnectionState.Reconnecting => ConnectionHealthState.Recovering,
            HubConnectionState.Disconnected => ConnectionHealthState.Offline,
            _ => ConnectionHealthState.Offline
        };
    }

    private AggregateConnectionState ComputeAggregateState()
    {
        if (_hubs.Count == 0) return AggregateConnectionState.Healthy;

        var critical = _hubs.Where(h => h.Registration.IsCritical).ToList();
        var nonCritical = _hubs.Where(h => !h.Registration.IsCritical).ToList();

        var anyCriticalDisconnected = critical.Any(h => h.Connection.State == HubConnectionState.Disconnected);
        var anyCriticalRecovering = critical.Any(h =>
            h.Connection.State is HubConnectionState.Reconnecting or HubConnectionState.Connecting);
        var anyNonCriticalUnhealthy = nonCritical.Any(h => h.Connection.State != HubConnectionState.Connected);

        if (anyCriticalDisconnected) return AggregateConnectionState.Offline;
        if (anyCriticalRecovering) return AggregateConnectionState.Degraded;
        if (anyNonCriticalUnhealthy) return AggregateConnectionState.Partial;
        return AggregateConnectionState.Healthy;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 3), "...");

    // ── Inner tracking class ────────────────────────────────────────────

    private sealed class TrackedHub(SignalRHubRegistration registration, HubConnection connection, DateTimeOffset createdAt)
    {
        public SignalRHubRegistration Registration { get; } = registration;
        public HubConnection Connection { get; } = connection;
        public DateTimeOffset LastStateChangedAt { get; set; } = createdAt;
        public DateTimeOffset? LastConnectedAt { get; set; }
        public string? LastError { get; set; }
        public int? RetryCount { get; set; }
        public string? ConnectionId { get; set; }
    }
}

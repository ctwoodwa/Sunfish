using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Integrations;

/// <summary>Live health classification of a provider adapter.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProviderHealthStatus
{
    /// <summary>Health has not been determined yet.</summary>
    Unknown = 0,

    /// <summary>Adapter is operating normally.</summary>
    Healthy = 1,

    /// <summary>Adapter is degraded (slow, rate-limited, partial availability).</summary>
    Degraded = 2,

    /// <summary>Adapter cannot serve requests.</summary>
    Unhealthy = 3,
}

/// <summary>
/// Reported by provider adapters so Bridge admin and ops dashboards can
/// surface live integration status.
/// </summary>
public interface IProviderHealthCheck
{
    /// <summary>Provider this check reports on.</summary>
    string ProviderKey { get; }

    /// <summary>Runs the health check and returns a status + optional detail.</summary>
    ValueTask<ProviderHealthReport> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>Health report for one provider adapter.</summary>
public sealed record ProviderHealthReport
{
    /// <summary>Provider key.</summary>
    public required string ProviderKey { get; init; }

    /// <summary>Current status.</summary>
    public required ProviderHealthStatus Status { get; init; }

    /// <summary>Observation timestamp (UTC).</summary>
    public DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Optional human-readable detail (latency, error code).</summary>
    public string? Detail { get; init; }
}

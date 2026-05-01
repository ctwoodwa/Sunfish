using System;

namespace Sunfish.Providers.Mesh.Headscale;

/// <summary>
/// Configuration for <see cref="HeadscaleMeshAdapter"/>.
/// </summary>
public sealed class HeadscaleOptions
{
    /// <summary>
    /// Headscale control-plane base URL (e.g.,
    /// <c>https://headscale.internal.example/</c>). The adapter
    /// appends <c>/api/v1/...</c> + <c>/health</c>.
    /// </summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>
    /// Headscale-issued API key. Sent as
    /// <c>Authorization: Bearer &lt;ApiKey&gt;</c> on every request.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Optional Headscale "user" (org/tenant) namespace under which
    /// registered devices land. When null, the API key's default user
    /// is used.
    /// </summary>
    public string? User { get; init; }

    /// <summary>
    /// How long to cache an <see cref="HeadscaleMeshAdapter.IsAvailable"/>
    /// probe result before re-checking the control plane. Default 5s
    /// keeps the selector fast without flooding the control plane.
    /// </summary>
    public TimeSpan AvailabilityCacheDuration { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Per-request timeout sent on the underlying <see cref="System.Net.Http.HttpClient"/>.
    /// Default 3s — well inside the Tier-2 5s budget per ADR 0061 A4.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(3);
}

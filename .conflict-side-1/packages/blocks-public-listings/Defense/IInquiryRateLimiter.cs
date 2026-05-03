using System.Net;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PublicListings.Defense;

/// <summary>
/// Sliding-window rate limiter for inquiry-form submissions per ADR 0059
/// §"Inquiry-form abuse posture" Layer 2: 5/hr per source IP, 50/hr per
/// tenant. The limiter consults its own state; it is not stateless.
/// </summary>
public interface IInquiryRateLimiter
{
    /// <summary>
    /// Records the current submission and returns whether it fits
    /// within both windows. Implementations may compose their own
    /// per-IP + per-tenant budgets per ADR 0059 defaults.
    /// </summary>
    Task<RateLimitVerdict> CheckAsync(IPAddress clientIp, TenantId tenant, DateTimeOffset now, CancellationToken ct);
}

/// <summary>The verdict for a rate-limit consultation.</summary>
public sealed record RateLimitVerdict
{
    /// <summary>Whether the current submission fits within both windows.</summary>
    public required bool Allowed { get; init; }

    /// <summary>Which window rejected when <see cref="Allowed"/> is false.</summary>
    public RateLimitWindow? ExceededWindow { get; init; }

    /// <summary>The accept verdict.</summary>
    public static RateLimitVerdict Allow { get; } = new() { Allowed = true };

    /// <summary>Builds a deny verdict for a specific exceeded window.</summary>
    public static RateLimitVerdict Deny(RateLimitWindow window) =>
        new() { Allowed = false, ExceededWindow = window };
}

/// <summary>The two rate-limit windows the inquiry form enforces.</summary>
public enum RateLimitWindow
{
    /// <summary>Per-IP sliding window (5/hr default per ADR 0059).</summary>
    PerIp,

    /// <summary>Per-tenant sliding window (50/hr default per ADR 0059).</summary>
    PerTenant,
}

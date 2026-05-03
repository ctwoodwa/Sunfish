using System.Collections.Concurrent;
using System.Net;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PublicListings.Defense;

/// <summary>
/// In-memory <see cref="IInquiryRateLimiter"/>. Keeps per-IP +
/// per-tenant timestamp buffers and consults them with a sliding
/// 1-hour window. Defaults per ADR 0059: 5 submissions / IP / hour
/// + 50 submissions / tenant / hour. The check method records the
/// submission ONLY when both windows allow it; rejected submissions
/// do NOT consume budget (denial preserves room for legitimate retries).
/// </summary>
public sealed class InMemoryInquiryRateLimiter : IInquiryRateLimiter
{
    /// <summary>Default per-IP submissions per <see cref="DefaultWindow"/>.</summary>
    public const int DefaultPerIpLimit = 5;

    /// <summary>Default per-tenant submissions per <see cref="DefaultWindow"/>.</summary>
    public const int DefaultPerTenantLimit = 50;

    /// <summary>Default sliding window length.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<IPAddress, List<DateTimeOffset>> _perIp = new();
    private readonly ConcurrentDictionary<TenantId, List<DateTimeOffset>> _perTenant = new();

    private readonly int _perIpLimit;
    private readonly int _perTenantLimit;
    private readonly TimeSpan _window;

    /// <summary>Creates a limiter with ADR 0059 defaults.</summary>
    public InMemoryInquiryRateLimiter() : this(DefaultPerIpLimit, DefaultPerTenantLimit, DefaultWindow) { }

    /// <summary>Creates a limiter with custom thresholds.</summary>
    public InMemoryInquiryRateLimiter(int perIpLimit, int perTenantLimit, TimeSpan window)
    {
        if (perIpLimit < 1) throw new ArgumentOutOfRangeException(nameof(perIpLimit), "Must be >= 1.");
        if (perTenantLimit < 1) throw new ArgumentOutOfRangeException(nameof(perTenantLimit), "Must be >= 1.");
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window), "Must be > 0.");
        _perIpLimit = perIpLimit;
        _perTenantLimit = perTenantLimit;
        _window = window;
    }

    /// <inheritdoc />
    public Task<RateLimitVerdict> CheckAsync(IPAddress clientIp, TenantId tenant, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clientIp);
        ct.ThrowIfCancellationRequested();

        var ipBucket = _perIp.GetOrAdd(clientIp, _ => new List<DateTimeOffset>());
        var tenantBucket = _perTenant.GetOrAdd(tenant, _ => new List<DateTimeOffset>());

        lock (ipBucket)
        {
            ipBucket.RemoveAll(t => now - t >= _window);
            if (ipBucket.Count >= _perIpLimit)
            {
                return Task.FromResult(RateLimitVerdict.Deny(RateLimitWindow.PerIp));
            }
        }

        lock (tenantBucket)
        {
            tenantBucket.RemoveAll(t => now - t >= _window);
            if (tenantBucket.Count >= _perTenantLimit)
            {
                return Task.FromResult(RateLimitVerdict.Deny(RateLimitWindow.PerTenant));
            }
        }

        // Both buckets allow — record the submission in both.
        lock (ipBucket) { ipBucket.Add(now); }
        lock (tenantBucket) { tenantBucket.Add(now); }

        return Task.FromResult(RateLimitVerdict.Allow);
    }
}

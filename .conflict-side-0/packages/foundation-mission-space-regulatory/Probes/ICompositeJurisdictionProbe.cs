using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Per ADR 0064-A1.5 + A1.15 — composite probe that aggregates 3 jurisdiction
/// signals (IP-geo, user-declaration, tenant-config) and produces a single
/// <see cref="JurisdictionProbe"/> with a confidence band.
/// </summary>
/// <remarks>
/// <para>
/// Tie-breaker per A1.15: when signals disagree on jurisdiction, the
/// resolved code follows <c>user-declaration &gt; tenant-config &gt; IP-geo</c>.
/// Confidence drops to <see cref="Confidence.Low"/> in any disagreement case.
/// </para>
/// <para>
/// Confidence ladder when signals agree: 3-of-3 → <see cref="Confidence.High"/>;
/// 2-of-3 → <see cref="Confidence.Medium"/>; 1-of-3 → <see cref="Confidence.Low"/>;
/// 0-of-3 → no probe (caller surfaces the no-signal case).
/// </para>
/// </remarks>
public interface ICompositeJurisdictionProbe
{
    ValueTask<JurisdictionProbe?> ProbeAsync(CompositeJurisdictionSignals signals, CancellationToken ct = default);
}

/// <summary>Per A1.5 — input signals to the composite probe.</summary>
public sealed record CompositeJurisdictionSignals
{
    /// <summary>Jurisdiction code derived from IP geolocation (lowest priority on tie).</summary>
    public string? IpGeoCode { get; init; }

    /// <summary>Jurisdiction code from tenant configuration.</summary>
    public string? TenantConfigCode { get; init; }

    /// <summary>Jurisdiction code declared by the user (highest priority on tie per A1.15).</summary>
    public string? UserDeclaredCode { get; init; }
}

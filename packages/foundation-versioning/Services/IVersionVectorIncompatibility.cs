using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Versioning;

/// <summary>
/// Records federation-handshake rejections and legacy-device reconnects
/// for audit + UX-surface emission (ADR 0028-A6.4 + A7.4). Implementations
/// MUST honor the A7.4 dedup windows so a misconfigured-peer-retry storm
/// does not flood the audit trail.
/// </summary>
public interface IVersionVectorIncompatibility
{
    /// <summary>
    /// Records a rejection for audit + UX surface emission. Per A7.4,
    /// dedup is 1-per-(<paramref name="remoteNodeId"/>,
    /// <see cref="VersionVectorVerdict.FailedRule"/>,
    /// <see cref="VersionVectorVerdict.FailedRuleDetail"/>) tuple per
    /// 1-hour rolling window.
    /// </summary>
    ValueTask RecordRejectionAsync(
        string remoteNodeId,
        VersionVectorVerdict verdict,
        CancellationToken ct = default);

    /// <summary>
    /// Records a legacy-device reconnect (one-sided receive-only mode per
    /// A6.5). Per A7.4, dedup is 1-per-(<paramref name="remoteNodeId"/>,
    /// <paramref name="kernelMinorLag"/>) tuple per 24-hour rolling
    /// window.
    /// </summary>
    ValueTask RecordLegacyReconnectAsync(
        string remoteNodeId,
        string remoteKernel,
        int kernelMinorLag,
        CancellationToken ct = default);
}

using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Versioning;

/// <summary>
/// Handshake-time version-vector exchange surface (ADR 0028-A6.3 / A7.1).
/// Each peer in a federation handshake calls
/// <see cref="EvaluateAsync"/> independently against its local view + the
/// peer-supplied wire-format vector; the protocol caller is responsible
/// for exchanging the resulting verdicts. Per A7.1.3c, federation
/// proceeds iff BOTH peers' verdicts are
/// <see cref="VerdictKind.Compatible"/>.
/// </summary>
/// <remarks>
/// <para>
/// The exchange is stateless and side-effect-free at this layer; audit
/// emission lives on the rejection-handler interface introduced in
/// Phase 4 (post-A6.4). A consumer that wants the receive-only-mode behaviour
/// (A6.5; e.g., legacy device with kernel-minor-lag &gt;
/// <see cref="DefaultCompatibilityRelation.DefaultMaxKernelMinorLag"/>)
/// inspects the returned verdict's
/// <see cref="VersionVectorVerdict.FailedRule"/> + detail string and
/// decides per its own policy whether to allow one-sided receive.
/// </para>
/// </remarks>
public interface IVersionVectorExchange
{
    /// <summary>
    /// Evaluates the local node's version vector against a peer's vector
    /// using the configured <see cref="ICompatibilityRelation"/>.
    /// </summary>
    ValueTask<VersionVectorVerdict> EvaluateAsync(
        VersionVector localVector,
        VersionVector peerVector,
        CancellationToken ct = default);
}

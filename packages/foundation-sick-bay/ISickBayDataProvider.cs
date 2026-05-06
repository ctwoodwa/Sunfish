using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Read-side data provider for the Sick Bay aggregation surface per
/// ADR 0082 §2. Implementations live in <c>blocks-sick-bay</c> (Phase 2);
/// this contract is the foundation-tier seam.
/// </summary>
/// <remarks>
/// <para>
/// <b>CALLER CONTRACT (per ADR 0082 §2):</b> callers MUST verify
/// <see cref="ShipAction.ViewSickBay"/> via <see cref="IPermissionResolver"/>
/// before invoking any method on this interface. Until W#54 Phase 2
/// lands the analyzer enforcement, this is a comment-only contract —
/// pre-merge code review is the enforcement substitute.
/// </para>
/// <para>
/// <b>FORBIDDEN: <c>Sunfish.Foundation.Recovery.IFieldDecryptor</c>.</b>
/// Per ADR 0046-A2 §4 + ADR 0082 §Trust impact: implementations MUST
/// NOT call <c>IFieldDecryptor</c> anywhere in the implementation chain.
/// The pharmacy browse pane carries the k=3-anonymized
/// <see cref="PharmacyRecordCount"/> only — never decrypted record
/// values. Decryption only happens on the per-document detail surface
/// (a separate authority cell), never in the Sick Bay browse pane.
/// </para>
/// <para>
/// <b>Latency posture:</b> recommended completion under 2s per
/// <see cref="GetSnapshotAsync"/>; partial-snapshot-on-timeout posture
/// is acceptable (return what you have rather than fault).
/// </para>
/// </remarks>
public interface ISickBayDataProvider
{
    /// <summary>
    /// Materialize the current Sick Bay snapshot for the supplied
    /// tenant. Pre-condition: caller has verified
    /// <see cref="ShipAction.ViewSickBay"/>.
    /// </summary>
    Task<SickBaySnapshot> GetSnapshotAsync(TenantId tenant, CancellationToken ct = default);

    /// <summary>
    /// Subscribe to incremental Sick Bay snapshot updates. Implementations
    /// emit one snapshot immediately on subscribe, then on each
    /// state change, then every <see cref="SickBayOptions.FallbackPollingInterval"/>
    /// when no push transport is available.
    /// </summary>
    IAsyncEnumerable<SickBaySnapshot> SubscribeSnapshotAsync(
        TenantId tenant,
        CancellationToken ct);
}

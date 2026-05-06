using System.Collections.Generic;
using System.Threading;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Plug-in contract for any subsystem (Tactical lookout, Engine Room
/// damage-control, Mission-Envelope guard, third-party block) that
/// emits alerts onto the Quarterdeck ticker per ADR 0080 §2.3 rule 7.
/// </summary>
/// <remarks>
/// <para>
/// <b>SourceName uniqueness (§5.3):</b> implementations MUST register
/// a stable <see cref="SourceName"/>; the
/// <c>AddSunfishQuarterdeck()</c> startup hook validates uniqueness
/// across all registered sources and rejects duplicate names. The
/// prefix <c>"sunfish.*"</c> is reserved for first-party sources;
/// third-party alert sources MUST use a different prefix (e.g.,
/// <c>"acme.tactical.lookout"</c>) to prevent first-party-source
/// impersonation per ADR 0080 §5.3 anti-spoofing. The reservation
/// applies symmetrically across <see cref="IQuarterdeckAlertSource"/>
/// and <see cref="IDepartmentKpiSource"/> — the SourceName
/// namespace is shared.
/// </para>
/// <para>
/// <b>Visibility policy:</b> sources stamp each emitted alert with
/// <see cref="QuarterdeckAlert.VisibilityPolicy"/>; the data provider
/// applies the per-alert policy at aggregation time per §2.3 rule 5
/// to decide whether to omit the alert for actors who cannot reach
/// the source. Default policy is
/// <see cref="AlertVisibilityPolicy.OmitForDeniedActors"/>; sources
/// emitting ship-wide broadcasts (mass-notification, Mission-Envelope-
/// failed banners) set <see cref="AlertVisibilityPolicy.ShowAll"/> on
/// the alert.
/// </para>
/// </remarks>
public interface IQuarterdeckAlertSource
{
    /// <summary>
    /// Stable, registered name for this source per §5.3. Used for
    /// startup uniqueness validation + per-alert source attribution.
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Stream alerts the source currently has for the supplied actor.
    /// Implementations MUST be idempotent — the data provider may
    /// invoke <see cref="GetAlertsAsync"/> on every snapshot emit, and
    /// alerts MUST keep their <see cref="QuarterdeckAlert.AlertId"/>
    /// stable across emits.
    /// </summary>
    IAsyncEnumerable<QuarterdeckAlert> GetAlertsAsync(
        TenantId tenantId,
        ActorId actor,
        CancellationToken ct = default);
}

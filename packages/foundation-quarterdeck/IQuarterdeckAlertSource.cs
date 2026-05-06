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
/// prefix <c>"sunfish.*"</c> is reserved for first-party sources.
/// </para>
/// <para>
/// <b>Visibility policy:</b> sources MAY embed an
/// <see cref="AlertVisibilityPolicy"/> in their alerts indirectly by
/// returning an empty enumerable for actors they do not surface to —
/// the data provider applies the configured visibility policy at
/// aggregation time per §2.3 rule 5.
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

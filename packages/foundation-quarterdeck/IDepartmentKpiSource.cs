using System.Collections.Generic;
using System.Threading;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Plug-in contract for any department surface that emits KPI cards
/// onto the Quarterdeck per ADR 0080 §2.3 rule 9. Engine Room, Sick
/// Bay, Tactical, Ship's Office, Supply Office, and any third-party
/// department block register a <see cref="IDepartmentKpiSource"/> to
/// surface a single-line "department health" projection.
/// </summary>
/// <remarks>
/// <b>SourceName uniqueness (§5.3):</b> the
/// <c>AddSunfishQuarterdeck()</c> startup hook validates uniqueness
/// across all registered KPI sources and rejects duplicate names.
/// The prefix <c>"sunfish.*"</c> is reserved for first-party sources;
/// third-party KPI sources MUST use a different prefix (e.g.,
/// <c>"acme.medbay.census"</c>) to prevent first-party-source
/// impersonation per ADR 0080 §5.3 anti-spoofing. The reservation
/// applies symmetrically across <see cref="IDepartmentKpiSource"/>
/// and <see cref="IQuarterdeckAlertSource"/> — the SourceName
/// namespace is shared.
/// </remarks>
public interface IDepartmentKpiSource
{
    /// <summary>
    /// Stable, registered name for this source per §5.3.
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Stream the source's current KPI cards. Implementations MUST
    /// supply a neutral <see cref="DepartmentKpi.Value"/> when the
    /// actor cannot see real values — the data provider does not
    /// rewrite values, it stamps the access decision through unchanged.
    /// </summary>
    IAsyncEnumerable<DepartmentKpi> GetKpisAsync(
        TenantId tenantId,
        CancellationToken ct = default);
}

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
/// <b>SourceName uniqueness (§5.3):</b> mirrors
/// <see cref="IQuarterdeckAlertSource.SourceName"/>; the
/// <c>AddSunfishQuarterdeck()</c> startup hook validates uniqueness
/// across all registered KPI sources and rejects duplicate names.
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

using System.Collections;
using System.Threading;

namespace Sunfish.Compat.Telerik;

/// <summary>
/// Telerik-shaped server-side data-read event arguments. Mirrors
/// <c>Telerik.Blazor.Components.GridReadEventArgs</c>.
///
/// <para><b>Status:</b> This type exists so consumer handler signatures compile after a
/// using-swap. <c>TelerikGrid.OnRead</c> still <b>throws</b> in Phase 6 (see
/// <c>TelerikGrid.razor</c>'s <c>OnParametersSet</c> guard and
/// <c>docs/compat-telerik-mapping.md</c>). The functional wiring of
/// <c>GridReadEventArgs → SunfishDataGrid.OnRead</c> is tracked as a future gap-closure PR.</para>
///
/// <para><b>Divergence:</b> Sunfish's server-side read uses
/// <see cref="Sunfish.UIAdapters.Blazor.Components.DataDisplay.GridReadEventArgs{TItem}"/>
/// which is strongly typed on the row item and exposes a structured <c>GridState</c> request.
/// The Telerik shape is weakly typed (<c>Data</c> is <see cref="IEnumerable"/>) — translation
/// happens at the delegation boundary when the feature ships.</para>
/// </summary>
public class GridReadEventArgs
{
    /// <summary>The data-request state (page, sort, filter, group). Weakly-typed per Telerik.</summary>
    public object? Request { get; init; }

    /// <summary>Consumer sets this to the current page/view's items.</summary>
    public IEnumerable? Data { get; set; }

    /// <summary>Consumer sets this to the total row count before paging.</summary>
    public int Total { get; set; }

    /// <summary>Cancellation token for the in-flight read (Telerik exposes this on the args).</summary>
    public CancellationToken CancellationToken { get; init; }
}

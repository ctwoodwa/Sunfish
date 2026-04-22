namespace Sunfish.Compat.Infragistics;

/// <summary>
/// Ignite-UI-shaped sorting event arguments for <c>IgbGrid</c>. Mirrors
/// <c>IgniteUI.Blazor.Controls.IgbGridSortingEventArgs</c>.
///
/// <para><b>Status:</b> Type shipped for consumer-signature compatibility. Full wiring
/// into <c>IgbGrid&lt;TItem&gt;</c> is future-gated.</para>
/// </summary>
public class IgbGridSortingEventArgs
{
    /// <summary>The field the sort is applied to.</summary>
    public string? Field { get; init; }

    /// <summary>The sort direction: <c>"asc"</c>, <c>"desc"</c>, or <c>"none"</c> per Ignite UI.</summary>
    public string? Direction { get; init; }

    /// <summary>When set to <c>true</c>, cancels the sort (if the host supports cancellation).</summary>
    public bool Cancel { get; set; }
}

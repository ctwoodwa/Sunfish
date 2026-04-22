using System.Collections.Generic;

namespace Sunfish.Compat.Infragistics;

/// <summary>
/// Ignite-UI-shaped selection-change event arguments for <c>IgbGrid</c>. Mirrors
/// <c>IgniteUI.Blazor.Controls.IgbGridSelectionEventArgs</c>.
///
/// <para><b>Divergence:</b> <see cref="NewSelection"/> is typed as <see cref="IEnumerable{T}"/>
/// of <c>object</c> per Ignite UI's erased-row-type shape. Consumers cast as needed.</para>
/// </summary>
public class IgbGridSelectionEventArgs
{
    /// <summary>The new selection set after the change.</summary>
    public IEnumerable<object>? NewSelection { get; init; }

    /// <summary>The old selection set before the change.</summary>
    public IEnumerable<object>? OldSelection { get; init; }

    /// <summary>When set to <c>true</c>, cancels the selection change (if the host supports cancellation).</summary>
    public bool Cancel { get; set; }
}

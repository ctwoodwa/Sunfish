namespace Sunfish.Compat.Telerik;

/// <summary>
/// Telerik-shaped grid command event arguments. Mirrors
/// <c>Telerik.Blazor.Components.GridCommandEventArgs</c>. Fires for built-in edit/save/cancel/
/// delete commands and any consumer-defined custom command.
///
/// <para><b>Divergence:</b> Sunfish's equivalent is
/// <see cref="Sunfish.UIAdapters.Blazor.Components.DataDisplay.GridCommandEventArgs{TItem}"/>
/// which is strongly typed on the row item. The Telerik shape erases the row type to
/// <c>object</c> — consumers cast as needed. See <c>docs/compat-telerik-mapping.md</c>.</para>
///
/// <para><b>Out-of-scope:</b> the <c>Field</c>, <c>IsCancelled</c>, and command-name routing
/// wiring is not re-plumbed through <c>TelerikGrid</c> in this gap-closure — those arrive when
/// the grid wrapper gains command-column shims in a future PR. The type is shipped now so
/// consumer signatures compile and can be wired up incrementally.</para>
/// </summary>
public class GridCommandEventArgs
{
    /// <summary>The row the command targets. Consumer casts to the concrete row type.</summary>
    public object? Item { get; init; }

    /// <summary>The Telerik command name (e.g. <c>"Edit"</c>, <c>"Save"</c>, <c>"Delete"</c>).</summary>
    public string? Command { get; init; }

    /// <summary>The field name associated with the command, if any.</summary>
    public string? Field { get; init; }

    /// <summary>When set to <c>true</c>, cancels the command's default handling.</summary>
    public bool IsCancelled { get; set; }

    /// <summary>Whether the command targets a newly created (not yet saved) item.</summary>
    public bool IsNew { get; init; }
}

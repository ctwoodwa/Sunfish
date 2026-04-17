using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Components.Editors;

/// <summary>
/// Defines a custom tool for the SunfishEditor toolbar.
/// </summary>
public class EditorCustomTool
{
    /// <summary>Display name of the tool.</summary>
    public string Name { get; set; } = "";

    /// <summary>CSS class for the tool icon.</summary>
    public string? Icon { get; set; }

    /// <summary>Tooltip text shown on hover.</summary>
    public string? Tooltip { get; set; }

    /// <summary>Callback invoked when the tool button is clicked.</summary>
    public Func<Task>? OnClick { get; set; }

    /// <summary>Optional custom render template. If set, replaces the default button.</summary>
    public RenderFragment? Template { get; set; }
}

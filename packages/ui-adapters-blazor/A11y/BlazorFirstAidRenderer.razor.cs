using System;
using Microsoft.AspNetCore.Components;
using Sunfish.UICore.FirstAid;

namespace Sunfish.UIAdapters.Blazor.A11y;

/// <summary>
/// Code-behind for <c>BlazorFirstAidRenderer.razor</c>.
/// Accepts an <see cref="IFirstAidContract"/> plus pre-localized override
/// strings; falls back to <see cref="IFirstAidContract.HelpKey"/> as-is
/// when no override is supplied (consumers are expected to localize before
/// passing or use an <c>IStringLocalizer</c> at the call site).
/// </summary>
public partial class BlazorFirstAidRenderer : ComponentBase
{
    /// <summary>The First-Aid contract describing this surface's a11y requirements.</summary>
    [Parameter, EditorRequired] public IFirstAidContract Contract { get; set; } = default!;

    /// <summary>
    /// Pre-localized help text. When null, <see cref="IFirstAidContract.HelpKey"/>
    /// is rendered verbatim (the consuming app should localize).
    /// </summary>
    [Parameter] public string? HelpText { get; set; }

    /// <summary>
    /// Pre-localized next-action hint. When null,
    /// <see cref="IFirstAidContract.NextActionHintKey"/> is rendered verbatim.
    /// Omitted from output when both this and the contract key are null/empty.
    /// </summary>
    [Parameter] public string? NextActionHintText { get; set; }

    /// <summary>Content wrapped by this First-Aid region.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    // Resolved values exposed to the .razor template.
    internal string HelpTextResolved =>
        !string.IsNullOrWhiteSpace(HelpText) ? HelpText : Contract?.HelpKey ?? string.Empty;

    internal string? NextActionHintResolved =>
        !string.IsNullOrWhiteSpace(NextActionHintText)
            ? NextActionHintText
            : Contract?.NextActionHintKey;

    // Stable per-render id wired to aria-describedby.
    private readonly string _helpId = $"sf-help-{Guid.NewGuid():N}";
}

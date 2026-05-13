using Microsoft.AspNetCore.Components;
using Sunfish.UICore.FirstAid;

namespace Sunfish.UIAdapters.Blazor.Maui;

/// <summary>
/// MAUI First-Aid renderer per ADR 0077 §4.
/// Anchor is a MAUI Blazor Hybrid app — its interactive surfaces render
/// inside a <c>BlazorWebView</c>; all First-Aid rendering is therefore
/// delegated to <see cref="A11y.BlazorFirstAidRenderer"/>, which runs
/// inside the embedded Blazor component tree.
/// This class is a DI-visible marker / factory for the MAUI host so
/// callers can resolve <c>IFirstAidContract</c>-aware renderers without
/// taking a direct dependency on the Blazor adapter namespace.
/// </summary>
public sealed class MauiFirstAidRenderer
{
    /// <summary>
    /// Returns a <see cref="RenderFragment"/> wrapping <paramref name="childContent"/>
    /// in a <see cref="A11y.BlazorFirstAidRenderer"/> with the given contract.
    /// Intended for use inside a <c>BlazorWebView</c> Razor tree.
    /// </summary>
    public static RenderFragment Render(
        IFirstAidContract contract,
        RenderFragment? childContent = null,
        string? helpText = null,
        string? nextActionHintText = null)
    {
        return builder =>
        {
            builder.OpenComponent<A11y.BlazorFirstAidRenderer>(0);
            builder.AddComponentParameter(1, nameof(A11y.BlazorFirstAidRenderer.Contract), contract);
            if (helpText is not null)
                builder.AddComponentParameter(2, nameof(A11y.BlazorFirstAidRenderer.HelpText), helpText);
            if (nextActionHintText is not null)
                builder.AddComponentParameter(3, nameof(A11y.BlazorFirstAidRenderer.NextActionHintText), nextActionHintText);
            if (childContent is not null)
                builder.AddComponentParameter(4, nameof(A11y.BlazorFirstAidRenderer.ChildContent), childContent);
            builder.CloseComponent();
        };
    }
}

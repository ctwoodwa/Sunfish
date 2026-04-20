namespace Sunfish.UICore.Contracts;

/// <summary>
/// Pluggable rendering backend for Sunfish components. Sibling to
/// <see cref="ISunfishCssProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Per spec §4.5 Phase 2.5 + Appendix E.3 (L1). Derived from Iced's <c>Renderer</c>
/// type parameter. The default implementation (<c>BlazorDomRenderer</c> in
/// <c>Sunfish.UIAdapters.Blazor</c>) produces a <c>RenderFragment</c> output for
/// browser DOM. A future <c>MauiNativeRenderer</c> or <c>AvaloniaRenderer</c> could
/// emit native-widget tree output for Phase 2.5 multi-platform hosts without
/// touching component code.
/// </para>
/// <para>
/// <see cref="Render"/> returns <see cref="object"/> rather than a typed payload in
/// order to preserve the <c>HasNoBlazorDependency</c> invariant on
/// <c>Sunfish.UICore</c>. Consumers cast to the renderer's expected payload type
/// (e.g., <c>RenderFragment</c> for <c>blazor-dom</c>). A typed
/// <c>SunfishRenderOutput&lt;TPayload&gt;</c> variant will ship alongside the first
/// non-Blazor renderer (see G6 in the gap analysis).
/// </para>
/// </remarks>
public interface ISunfishRenderer
{
    /// <summary>
    /// Produces a platform-specific render payload for a widget described by
    /// <paramref name="descriptor"/>. Renderers may transform the descriptor into
    /// native controls, HTML elements, or any other output format their host
    /// expects.
    /// </summary>
    /// <param name="descriptor">The framework-agnostic description of the widget.</param>
    /// <returns>
    /// An opaque payload whose concrete type is defined by the renderer (consult
    /// <see cref="Platform"/> for a hint). For <c>blazor-dom</c>, this is a
    /// <c>Microsoft.AspNetCore.Components.RenderFragment</c>.
    /// </returns>
    object Render(SunfishWidgetDescriptor descriptor);

    /// <summary>
    /// The renderer's platform identifier (e.g., <c>"blazor-dom"</c>,
    /// <c>"maui-native"</c>, <c>"avalonia"</c>). Callers inspect this to decide how
    /// to consume the <see cref="Render"/> payload.
    /// </summary>
    string Platform { get; }
}

/// <summary>
/// Framework-agnostic description of a widget. Carries enough information for any
/// <see cref="ISunfishRenderer"/> to emit its platform-specific representation.
/// </summary>
/// <param name="WidgetKind">
/// The logical widget kind (e.g., <c>"button"</c>, <c>"div"</c>,
/// <c>"sunfish:card"</c>). Renderers map this to the appropriate concrete control.
/// </param>
/// <param name="Parameters">
/// Opaque named parameters passed to the renderer (attributes, properties, data
/// bindings). Keys and values are renderer-defined; the contract places no
/// constraints on them.
/// </param>
/// <param name="Children">
/// Child widgets to render inside this widget. Empty if the widget is a leaf.
/// </param>
public sealed record SunfishWidgetDescriptor(
    string WidgetKind,
    IReadOnlyDictionary<string, object?> Parameters,
    IReadOnlyList<SunfishWidgetDescriptor> Children);

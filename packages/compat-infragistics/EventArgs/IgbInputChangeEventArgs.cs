namespace Sunfish.Compat.Infragistics;

/// <summary>
/// Ignite-UI-shaped change event arguments for <c>IgbInput</c> / <c>IgbCheckbox</c> /
/// <c>IgbSelect</c>. Mirrors the WC-bridged <c>CustomEventArgs</c> shape that Ignite UI
/// exposes on Blazor-side change events.
///
/// <para><b>Status:</b> Type shipped so consumer handler signatures compile. Functional
/// wiring from the wrapped Sunfish change events to this shape is handled at each
/// wrapper's delegation boundary; see <c>docs/compat-infragistics-mapping.md</c>.</para>
///
/// <para><b>Divergence:</b> Ignite UI's underlying WC <c>change</c> event payload is JS-side
/// and carries a <c>detail</c> object. In Blazor-land it surfaces as <see cref="Value"/> —
/// the new value after the change. The shim preserves the shape so consumer handler
/// signatures keep compiling.</para>
/// </summary>
public class IgbInputChangeEventArgs
{
    /// <summary>The new value after the change. Consumer casts to the concrete type.</summary>
    public object? Value { get; init; }

    /// <summary>The name of the source input, if set via the <c>Name</c> parameter.</summary>
    public string? Name { get; init; }
}

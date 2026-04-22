namespace Sunfish.Compat.Infragistics;

/// <summary>
/// Buffered option record used by <see cref="IgbSelect{TValue}"/> to bridge child-based
/// Ignite UI option declarations to Sunfish's Data-bound dropdown model. Internal to the
/// wrapper: not a consumer-facing type but exposed as <c>public</c> so the generic
/// delegation wiring compiles across the razor/code boundary.
/// </summary>
public class IgbSelectItemModel<TValue>
{
    public TValue? Value { get; init; }
    public string Text { get; init; } = string.Empty;
    public bool Selected { get; init; }
    public bool Disabled { get; init; }
}

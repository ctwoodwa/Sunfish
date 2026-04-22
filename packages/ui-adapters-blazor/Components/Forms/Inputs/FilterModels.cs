using Sunfish.Foundation.Enums;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Data-type hint used by <see cref="FilterField"/> to pick the correct value-input
/// (text, number, date, checkbox, enum dropdown) in <c>SunfishFilter</c>.
/// </summary>
public enum FilterFieldType
{
    /// <summary>A free-text string value (renders a text input).</summary>
    Text,

    /// <summary>A numeric value (renders a number input).</summary>
    Number,

    /// <summary>A date/time value (renders a date input).</summary>
    Date,

    /// <summary>A boolean value (renders a checkbox).</summary>
    Boolean,

    /// <summary>A bounded set of values (renders a dropdown seeded from <see cref="FilterField.EnumValues"/>).</summary>
    Enum
}

/// <summary>
/// Describes a field offered to <c>SunfishFilter</c>'s field dropdown along with the
/// operators that are meaningful for its type.
/// </summary>
public class FilterField
{
    /// <summary>The member path used as <see cref="FilterLeafDescriptor.Field"/> (e.g. <c>"LastName"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable label shown in the field dropdown. Defaults to <see cref="Name"/> when empty.</summary>
    public string? Label { get; set; }

    /// <summary>Data-type hint for the value-input selector.</summary>
    public FilterFieldType Type { get; set; } = FilterFieldType.Text;

    /// <summary>
    /// Operators offered for this field. When empty, the component falls back to a
    /// sensible default per <see cref="Type"/>.
    /// </summary>
    public IList<FilterOperator> Operators { get; set; } = new List<FilterOperator>();

    /// <summary>Enum values rendered in the value dropdown when <see cref="Type"/> is <see cref="FilterFieldType.Enum"/>.</summary>
    public IList<string> EnumValues { get; set; } = new List<string>();
}

/// <summary>
/// Root abstraction for the <c>SunfishFilter</c> model. Either a
/// <see cref="FilterLeafDescriptor"/> (single expression) or a
/// <see cref="FilterCompositeDescriptor"/> (AND/OR group of children).
/// </summary>
public abstract class FilterDescriptor
{
}

/// <summary>
/// A single filter expression: <c>Field OPERATOR Value</c>.
/// </summary>
public class FilterLeafDescriptor : FilterDescriptor
{
    /// <summary>The field path being filtered (matches <see cref="FilterField.Name"/>).</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>The comparison operator.</summary>
    public FilterOperator Operator { get; set; } = FilterOperator.Equals;

    /// <summary>The compared value. Type is determined by the corresponding field's <see cref="FilterFieldType"/>.</summary>
    public object? Value { get; set; }
}

/// <summary>
/// A group of child filter descriptors combined with <see cref="Logic"/>.
/// Groups can nest to form arbitrary boolean expressions.
/// </summary>
public class FilterCompositeDescriptor : FilterDescriptor
{
    /// <summary>The logical operator combining the children.</summary>
    public FilterLogic Logic { get; set; } = FilterLogic.And;

    /// <summary>The child descriptors (leaves or nested composites).</summary>
    public IList<FilterDescriptor> Filters { get; set; } = new List<FilterDescriptor>();
}

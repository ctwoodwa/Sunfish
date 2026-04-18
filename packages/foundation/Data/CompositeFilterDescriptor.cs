using Sunfish.Foundation.Enums;

namespace Sunfish.Foundation.Data;

/// <summary>
/// Groups multiple filter descriptors with a logical operator (AND/OR).
/// </summary>
public class CompositeFilterDescriptor
{
    /// <summary>The logical operator to combine the filters.</summary>
    public FilterCompositionOperator LogicalOperator { get; set; } = FilterCompositionOperator.And;

    /// <summary>The individual filter conditions in this group.</summary>
    public List<FilterDescriptor> Filters { get; set; } = [];
}

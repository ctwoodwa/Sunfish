namespace Sunfish.Foundation.Models;

public class FilterDescriptor
{
    public string Field { get; set; } = "";
    public FilterOperator Operator { get; set; }
    public object? Value { get; set; }
}

public class CompositeFilterDescriptor
{
    public FilterCompositionLogicalOperator LogicalOperator { get; set; }
    public List<FilterDescriptor> FilterDescriptors { get; set; } = new();
}

public enum FilterOperator
{
    IsEqualTo,
    IsNotEqualTo,
    Contains,
    DoesNotContain,
    StartsWith,
    EndsWith,
    IsGreaterThan,
    IsLessThan,
    IsGreaterThanOrEqualTo,
    IsLessThanOrEqualTo,
    IsNull,
    IsNotNull
}

public enum FilterCompositionLogicalOperator
{
    And,
    Or
}

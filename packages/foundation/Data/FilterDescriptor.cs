using Sunfish.Foundation.Enums;

namespace Sunfish.Foundation.Data;

public class FilterDescriptor
{
    public string Field { get; set; } = string.Empty;
    public FilterOperator Operator { get; set; }
    public object? Value { get; set; }
}

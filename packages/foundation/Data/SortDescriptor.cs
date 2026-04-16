using Sunfish.Foundation.Enums;

namespace Sunfish.Foundation.Data;

public class SortDescriptor
{
    public string Field { get; set; } = string.Empty;
    public SortDirection Direction { get; set; }
}

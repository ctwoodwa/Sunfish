using Sunfish.Foundation.Enums;

namespace Sunfish.Foundation.Data;

/// <summary>
/// Describes a grouping applied to a data grid column.
/// </summary>
public class GroupDescriptor
{
    /// <summary>The property name to group by.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Sort direction within the group. Defaults to Ascending.</summary>
    public SortDirection Direction { get; set; } = SortDirection.Ascending;
}

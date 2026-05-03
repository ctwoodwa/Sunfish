namespace Sunfish.Foundation.Data;

public class DataRequest
{
    public List<FilterDescriptor> Filters { get; set; } = [];
    public List<SortDescriptor> Sorting { get; set; } = [];
    public List<GroupDescriptor> Grouping { get; set; } = [];
    public PageState Paging { get; set; } = new();
}

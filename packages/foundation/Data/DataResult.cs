namespace Sunfish.Foundation.Data;

public class DataResult<T>
{
    public IEnumerable<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public Dictionary<string, object>? Aggregates { get; set; }
}

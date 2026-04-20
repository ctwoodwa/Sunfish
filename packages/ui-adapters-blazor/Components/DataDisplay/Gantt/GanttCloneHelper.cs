using System.Text.Json;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

internal static class GanttCloneHelper
{
    /// <summary>
    /// Deep clones a TItem. Uses IGanttCloneable if available, otherwise JSON roundtrip.
    /// </summary>
    internal static TItem? DeepClone<TItem>(TItem? item) where TItem : class
    {
        if (item is null) return null;
        if (item is IGanttCloneable<TItem> cloneable)
            return cloneable.Clone();
        // JSON roundtrip fallback
        var json = JsonSerializer.Serialize(item, item.GetType());
        return JsonSerializer.Deserialize<TItem>(json);
    }
}

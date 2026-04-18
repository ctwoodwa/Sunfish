namespace Sunfish.KitchenSink.Components;

public class PageSectionTracker
{
    public record SubSection(string Id, string Title, int Order);

    public class Section(string id, string title, int order)
    {
        public string Id { get; } = id;
        public string Title { get; } = title;
        public int Order { get; } = order;
        public List<SubSection> SubSections { get; } = [];
    }

    private readonly List<Section> _sections = [];
    private int _nextOrder;

    public IReadOnlyList<Section> Sections => _sections.OrderBy(s => s.Order).ToList();

    public event Action? OnChanged;

    public int NextOrder() => _nextOrder++;

    public void RegisterSection(string id, string title, int order)
    {
        var existing = _sections.FirstOrDefault(s => s.Id == id);
        if (existing is null)
        {
            _sections.Add(new Section(id, title, order));
            OnChanged?.Invoke();
        }
    }

    public void RegisterSubSection(string sectionId, string subId, string subTitle, int order)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId);
        if (section is not null && section.SubSections.All(ss => ss.Id != subId))
        {
            section.SubSections.Add(new SubSection(subId, subTitle, order));
            OnChanged?.Invoke();
        }
    }

    public void Clear()
    {
        _sections.Clear();
        _nextOrder = 0;
    }

    public static string ToAnchorId(string title) =>
        title.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace("(", "")
            .Replace(")", "")
            .Replace("&", "")
            .Replace("'", "");
}

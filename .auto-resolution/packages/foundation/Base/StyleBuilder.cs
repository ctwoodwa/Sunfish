namespace Sunfish.Foundation.Base;

public class StyleBuilder
{
    private readonly List<string> _styles = [];

    public StyleBuilder AddStyle(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _styles.Add(value.TrimEnd(';'));
        return this;
    }

    public StyleBuilder AddStyle(string property, string value, bool when = true)
    {
        if (when && !string.IsNullOrWhiteSpace(value))
            _styles.Add($"{property}: {value}");
        return this;
    }

    public StyleBuilder Clear()
    {
        _styles.Clear();
        return this;
    }

    public string Build() => _styles.Count > 0 ? string.Join("; ", _styles) + ";" : string.Empty;

    public override string ToString() => Build();
}

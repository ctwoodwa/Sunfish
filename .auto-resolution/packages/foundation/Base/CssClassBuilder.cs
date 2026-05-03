namespace Sunfish.Foundation.Base;

public class CssClassBuilder
{
    private readonly List<string> _classes = [];

    public CssClassBuilder AddClass(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _classes.Add(value);
        return this;
    }

    public CssClassBuilder AddClass(string value, bool when)
    {
        if (when) AddClass(value);
        return this;
    }

    public CssClassBuilder AddClass(string value, Func<bool> when)
    {
        if (when()) AddClass(value);
        return this;
    }

    public CssClassBuilder Clear()
    {
        _classes.Clear();
        return this;
    }

    public string Build() => string.Join(" ", _classes);

    public override string ToString() => Build();
}

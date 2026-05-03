namespace Sunfish.Foundation.BusinessLogic.Rules;

/// <summary>
/// Marks a property required when a condition is true.
/// Works for any nullable reference type or value type whose default
/// equals the "empty" sentinel (e.g., <c>null</c>, <c>0</c>, empty string).
/// </summary>
/// <typeparam name="TObject">The business-object type.</typeparam>
/// <typeparam name="TValue">The property value type.</typeparam>
public sealed class RequiredWhenRule<TObject, TValue> : IBusinessRule
    where TObject : class
{
    private readonly PropertyInfo<TValue> _property;
    private readonly Func<TObject, bool> _condition;
    private readonly string _message;

    /// <inheritdoc/>
    public string? PropertyName => _property.Name;

    /// <param name="property">The property that must have a value.</param>
    /// <param name="condition">Returns <c>true</c> when the property is required.</param>
    /// <param name="message">The broken-rule message. Defaults to "{Name} is required."</param>
    public RequiredWhenRule(
        PropertyInfo<TValue> property,
        Func<TObject, bool> condition,
        string? message = null)
    {
        _property = property;
        _condition = condition;
        _message = message ?? $"{property.Name} is required.";
    }

    /// <inheritdoc/>
    public string? Validate(object businessObject)
    {
        if (businessObject is not TObject typed) return null;
        if (!_condition(typed)) return null;

        var value = (typed as dynamic).Fields.Read(_property);
        bool isEmpty = value is null
            || (value is string s && string.IsNullOrWhiteSpace(s))
            || EqualityComparer<TValue>.Default.Equals(value, _property.DefaultValue);

        return isEmpty ? _message : null;
    }
}

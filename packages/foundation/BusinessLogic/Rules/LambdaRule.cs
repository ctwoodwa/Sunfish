namespace Sunfish.Foundation.BusinessLogic.Rules;

/// <summary>
/// A single-property validation rule expressed as a lambda — no custom class required.
/// </summary>
/// <typeparam name="TObject">The business-object type that owns the property.</typeparam>
/// <typeparam name="TValue">The property value type.</typeparam>
public sealed class LambdaRule<TObject, TValue> : IBusinessRule
    where TObject : class
{
    private readonly PropertyInfo<TValue> _property;
    private readonly Func<TObject, TValue, string?> _validate;

    /// <inheritdoc/>
    public string? PropertyName => _property.Name;

    /// <param name="property">The property token this rule targets.</param>
    /// <param name="validate">
    /// Returns a broken-rule message, or <c>null</c> when the value is valid.
    /// </param>
    public LambdaRule(PropertyInfo<TValue> property, Func<TObject, TValue, string?> validate)
    {
        _property = property;
        _validate = validate;
    }

    /// <inheritdoc/>
    public string? Validate(object businessObject)
    {
        if (businessObject is not TObject typed) return null;
        var value = (typed as dynamic).Fields.Read(_property); // resolved via FieldManager
        return _validate(typed, value);
    }
}

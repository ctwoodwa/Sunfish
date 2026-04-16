namespace Sunfish.Foundation.BusinessLogic.Rules;

/// <summary>
/// An object-level rule that validates relationships between two or more
/// properties — expressed as a lambda.
/// </summary>
/// <typeparam name="TObject">The business-object type.</typeparam>
public sealed class CrossFieldLambdaRule<TObject> : IBusinessRule
    where TObject : class
{
    private readonly Func<TObject, string?> _validate;

    /// <inheritdoc/>
    /// <remarks>Always <c>null</c> — cross-field rules are not scoped to a single property.</remarks>
    public string? PropertyName => null;

    /// <param name="validate">
    /// Returns a broken-rule message, or <c>null</c> when the object is valid.
    /// </param>
    public CrossFieldLambdaRule(Func<TObject, string?> validate)
        => _validate = validate;

    /// <inheritdoc/>
    public string? Validate(object businessObject)
        => businessObject is TObject typed ? _validate(typed) : null;
}

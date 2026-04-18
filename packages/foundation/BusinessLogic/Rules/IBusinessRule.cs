namespace Sunfish.Foundation.BusinessLogic.Rules;

/// <summary>
/// A single validation rule evaluated against a business object.
/// Implement this interface for custom per-property or cross-field rules.
/// </summary>
public interface IBusinessRule
{
    /// <summary>
    /// The property name this rule is primarily associated with, or <c>null</c>
    /// for object-level (cross-field) rules.
    /// </summary>
    string? PropertyName { get; }

    /// <summary>Runs the rule. Returns a non-null message when the rule is broken.</summary>
    string? Validate(object businessObject);
}

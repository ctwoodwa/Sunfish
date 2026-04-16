using Sunfish.Foundation.BusinessLogic.Rules;

namespace Sunfish.Foundation.BusinessLogic;

/// <summary>
/// Maintains a registry of <see cref="IBusinessRule"/> instances and evaluates
/// them on demand. Attach one engine per <see cref="BusinessObjectBase{T}"/> instance.
/// </summary>
public sealed class BusinessRuleEngine
{
    private readonly List<IBusinessRule> _rules = new();

    // ── Registration ────────────────────────────────────────────────────

    /// <summary>Registers a rule. Call from the owning object's constructor.</summary>
    public void AddRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
    }

    /// <summary>Convenience overload — registers a lambda rule directly.</summary>
    public void AddRule<TObject, TValue>(
        PropertyInfo<TValue> property,
        Func<TObject, TValue, string?> validate)
        where TObject : class
        => AddRule(new LambdaRule<TObject, TValue>(property, validate));

    // ── Evaluation ──────────────────────────────────────────────────────

    /// <summary>
    /// Runs all registered rules against <paramref name="businessObject"/>.
    /// Returns every broken rule found.
    /// </summary>
    public IReadOnlyList<BrokenRule> CheckRules(object businessObject)
    {
        var broken = new List<BrokenRule>();
        foreach (var rule in _rules)
        {
            var message = rule.Validate(businessObject);
            if (message is not null)
                broken.Add(new BrokenRule(rule.PropertyName, message));
        }
        return broken;
    }

    /// <summary>
    /// Runs only the rules registered for <paramref name="propertyName"/>
    /// (plus any object-level rules whose <c>PropertyName</c> is null).
    /// </summary>
    public IReadOnlyList<BrokenRule> CheckRulesFor(object businessObject, string propertyName)
    {
        var broken = new List<BrokenRule>();
        foreach (var rule in _rules)
        {
            if (rule.PropertyName != null && rule.PropertyName != propertyName) continue;
            var message = rule.Validate(businessObject);
            if (message is not null)
                broken.Add(new BrokenRule(rule.PropertyName, message));
        }
        return broken;
    }

    /// <summary>True when <see cref="CheckRules"/> would return zero broken rules.</summary>
    public bool IsValid(object businessObject) => CheckRules(businessObject).Count == 0;
}

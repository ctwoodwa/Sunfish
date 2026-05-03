using Sunfish.Foundation.BusinessLogic.Enums;

namespace Sunfish.Foundation.BusinessLogic.Authorization;

/// <summary>
/// Composes multiple <see cref="IAuthorizationRule"/> instances using a
/// most-restrictive-wins strategy. Called by every GetProperty / SetProperty
/// invocation in <see cref="BusinessObjectBase{T}"/>.
/// </summary>
public sealed class AuthorizationEngine
{
    private readonly List<IAuthorizationRule> _rules = new();

    // ── Registration ────────────────────────────────────────────────────

    public void AddRule(IAuthorizationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
    }

    /// <summary>Adds a lambda-based rule without a custom class.</summary>
    public void AddRule(Func<string, AuthorizationAction, object?, AccessMode?> rule)
        => AddRule(new LambdaAuthorizationRule(rule));

    // ── Evaluation ──────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates all rules and returns the most restrictive <see cref="AccessMode"/>.
    /// When no rule votes, returns <see cref="AccessMode.ReadWrite"/>.
    /// </summary>
    public AccessMode GetAccess(string propertyName, AuthorizationAction action, object? principal)
    {
        var result = AccessMode.ReadWrite;
        foreach (var rule in _rules)
        {
            var vote = rule.GetAccess(propertyName, action, principal);
            if (vote is null) continue;
            // Most-restrictive wins: None > ReadOnly > ReadWrite
            if (vote < result) result = vote.Value;
            if (result == AccessMode.None) break; // can't get more restrictive
        }
        return result;
    }

    public bool CanRead(string propertyName, object? principal)
        => GetAccess(propertyName, AuthorizationAction.Read, principal) >= AccessMode.ReadOnly;

    public bool CanWrite(string propertyName, object? principal)
        => GetAccess(propertyName, AuthorizationAction.Write, principal) == AccessMode.ReadWrite;

    // ── Inner lambda adapter ────────────────────────────────────────────

    private sealed class LambdaAuthorizationRule(Func<string, AuthorizationAction, object?, AccessMode?> fn)
        : IAuthorizationRule
    {
        public AccessMode? GetAccess(string propertyName, AuthorizationAction action, object? principal)
            => fn(propertyName, action, principal);
    }
}

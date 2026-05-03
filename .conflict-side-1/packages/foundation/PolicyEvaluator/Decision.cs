namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// Tri-state verdict rendered by <see cref="IPermissionEvaluator"/>.
/// </summary>
public enum DecisionKind
{
    /// <summary>The request is permitted (subject to any attached obligations).</summary>
    Permit,
    /// <summary>The request is explicitly denied.</summary>
    Deny,
    /// <summary>The evaluator cannot render a verdict (e.g. the resource type is unknown).</summary>
    Indeterminate,
}

/// <summary>
/// The structured output of a permission evaluation. Carries the verdict, a human-readable reason,
/// the names of matched policy relations (for audit), and any obligations the caller must satisfy.
/// </summary>
public sealed record Decision(
    DecisionKind Kind,
    string? Reason,
    IReadOnlyList<string> MatchedPolicies,
    IReadOnlyList<Obligation> Obligations)
{
    /// <summary>Creates a Permit decision with no obligations and the given matched-policy trail.</summary>
    public static Decision Permit(string reason, params string[] matched) =>
        new(DecisionKind.Permit, reason, matched, Array.Empty<Obligation>());

    /// <summary>Creates a Deny decision.</summary>
    public static Decision Deny(string reason) =>
        new(DecisionKind.Deny, reason, Array.Empty<string>(), Array.Empty<Obligation>());

    /// <summary>Creates an Indeterminate decision — the evaluator lacks schema to decide.</summary>
    public static Decision Indeterminate(string reason) =>
        new(DecisionKind.Indeterminate, reason, Array.Empty<string>(), Array.Empty<Obligation>());
}

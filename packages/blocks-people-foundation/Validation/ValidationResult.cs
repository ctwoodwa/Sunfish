namespace Sunfish.Blocks.People.Foundation.Validation;

/// <summary>
/// Outcome of running an entity through one of the package's validators.
/// Carries either success (no errors) or a non-empty list of human-readable
/// failure reasons. Returning a result rather than throwing keeps the call
/// sites in PR 3's write services deterministic — callers can branch on
/// <see cref="IsValid"/> instead of catching.
/// </summary>
public sealed record ValidationResult(IReadOnlyList<string> Errors)
{
    /// <summary>True when there are no errors.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Shared success singleton.</summary>
    public static readonly ValidationResult Success = new(Array.Empty<string>());

    /// <summary>Build a failure result from one or more error strings.</summary>
    public static ValidationResult Fail(params string[] errors) => new(errors);
}

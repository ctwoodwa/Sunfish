namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// Outcome of <see cref="GLAccount.Validate"/>. Surfaces specific
/// rule failures rather than throwing — callers decide whether to
/// reject or surface to UI.
/// </summary>
/// <param name="IsValid"><c>true</c> when no rule failed.</param>
/// <param name="Errors">Empty when <see cref="IsValid"/>; one entry per failed rule otherwise.</param>
public sealed record AccountValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    /// <summary>An always-valid result.</summary>
    public static AccountValidationResult Ok { get; } = new(true, Array.Empty<string>());

    /// <summary>Build a failure result with a single error message.</summary>
    public static AccountValidationResult Fail(string error)
        => new(false, new[] { error });

    /// <summary>Build a failure result with multiple error messages.</summary>
    public static AccountValidationResult Fail(IReadOnlyList<string> errors)
        => new(false, errors);
}

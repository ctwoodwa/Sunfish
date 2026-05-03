namespace Sunfish.Ingestion.Core.Middleware;

/// <summary>
/// Modality-supplied validator for <typeparamref name="TInput"/>. Used by
/// <see cref="ValidationMiddleware{TInput}"/>. When no validator is registered, validation is
/// skipped and the pipeline falls through to the next middleware.
/// </summary>
/// <typeparam name="TInput">The modality-specific input type.</typeparam>
public interface IValidator<TInput>
{
    /// <summary>Validates <paramref name="input"/> and returns a <see cref="ValidationResult"/>.</summary>
    ValueTask<ValidationResult> ValidateAsync(TInput input, CancellationToken ct);
}

/// <summary>
/// Result of validating an ingestion input — either <see cref="Pass"/> or a failure carrying one
/// or more error messages.
/// </summary>
/// <param name="IsValid"><c>true</c> iff validation passed.</param>
/// <param name="Errors">Zero-or-more human-readable error messages.</param>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    /// <summary>Shared instance representing successful validation with no errors.</summary>
    public static ValidationResult Pass { get; } = new(true, Array.Empty<string>());

    /// <summary>Builds a failed <see cref="ValidationResult"/> from the given error messages.</summary>
    public static ValidationResult Fail(params string[] errors) => new(false, errors);
}

/// <summary>
/// Middleware that runs an <see cref="IValidator{TInput}"/> (if provided) before continuing the
/// chain. On failure it short-circuits with
/// <see cref="IngestOutcome.ValidationFailed"/>.
/// </summary>
/// <typeparam name="TInput">The modality-specific input type.</typeparam>
public sealed class ValidationMiddleware<TInput>(IValidator<TInput>? validator) : IIngestionMiddleware<TInput>
{
    /// <inheritdoc />
    public async ValueTask<IngestionResult<IngestedEntity>> InvokeAsync(
        TInput input, IngestionContext context, IngestionDelegate<TInput> next, CancellationToken ct)
    {
        if (validator is null) return await next(input, context, ct);
        var result = await validator.ValidateAsync(input, ct);
        if (!result.IsValid)
            return IngestionResult<IngestedEntity>.Fail(IngestOutcome.ValidationFailed, "Input failed validation.", result.Errors);
        return await next(input, context, ct);
    }
}

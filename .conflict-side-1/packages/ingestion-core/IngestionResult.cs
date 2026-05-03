namespace Sunfish.Ingestion.Core;

/// <summary>
/// Discriminated result of a single ingestion call — either a success carrying a value of type
/// <typeparamref name="T"/>, or a failure carrying a structured <see cref="IngestionFailure"/>.
/// </summary>
/// <typeparam name="T">The success payload type (usually <see cref="IngestedEntity"/>).</typeparam>
/// <param name="Outcome">The outcome category.</param>
/// <param name="Value">The success payload, or <c>default</c> when the call failed.</param>
/// <param name="Failure">The structured failure, or <c>null</c> when the call succeeded.</param>
public sealed record IngestionResult<T>(
    IngestOutcome Outcome,
    T? Value,
    IngestionFailure? Failure)
{
    /// <summary>True when <see cref="Outcome"/> is <see cref="IngestOutcome.Success"/>.</summary>
    public bool IsSuccess => Outcome == IngestOutcome.Success;

    /// <summary>Creates a success result carrying the given <paramref name="value"/>.</summary>
    public static IngestionResult<T> Success(T value) => new(IngestOutcome.Success, value, null);

    /// <summary>
    /// Creates a failure result with the given outcome, message, and optional detail list.
    /// </summary>
    /// <param name="outcome">The failure category (must not be <see cref="IngestOutcome.Success"/> in practice).</param>
    /// <param name="message">Human-readable summary of the failure.</param>
    /// <param name="details">Optional additional details (e.g. validator error list).</param>
    public static IngestionResult<T> Fail(IngestOutcome outcome, string message, IReadOnlyList<string>? details = null) =>
        new(outcome, default, new IngestionFailure(outcome, message, details ?? Array.Empty<string>()));
}

/// <summary>
/// Structured description of an ingestion failure. Present on <see cref="IngestionResult{T}.Failure"/>
/// whenever <see cref="IngestionResult{T}.IsSuccess"/> is <c>false</c>.
/// </summary>
/// <param name="Outcome">The failure category (mirrors <see cref="IngestionResult{T}.Outcome"/>).</param>
/// <param name="Message">Human-readable summary of the failure.</param>
/// <param name="Details">Zero-or-more supplementary detail lines.</param>
public sealed record IngestionFailure(IngestOutcome Outcome, string Message, IReadOnlyList<string> Details);

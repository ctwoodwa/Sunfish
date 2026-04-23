namespace Sunfish.Kernel.Ledger.Exceptions;

/// <summary>
/// Thrown when a caller explicitly demands a unique-insert semantic for an
/// idempotency key that the ledger has already committed. Paper §12.2.
/// </summary>
/// <remarks>
/// The default <c>PostAsync</c> path does NOT throw this — it silently returns
/// the prior <see cref="Sunfish.Kernel.Ledger.PostingResult"/>. The exception is
/// reserved for advanced callers that want a failure rather than a dedupe on
/// replay.
/// </remarks>
public sealed class DuplicateIdempotencyKeyException : InvalidOperationException
{
    /// <summary>The idempotency key that was already committed.</summary>
    public string IdempotencyKey { get; }

    /// <summary>The previously-committed transaction id for the key.</summary>
    public Guid ExistingTransactionId { get; }

    /// <summary>Constructs a new exception.</summary>
    public DuplicateIdempotencyKeyException(string idempotencyKey, Guid existingTransactionId)
        : base($"Idempotency key '{idempotencyKey}' already committed as transaction {existingTransactionId}.")
    {
        IdempotencyKey = idempotencyKey;
        ExistingTransactionId = existingTransactionId;
    }
}

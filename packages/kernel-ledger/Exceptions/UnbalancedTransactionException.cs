namespace Sunfish.Kernel.Ledger.Exceptions;

/// <summary>
/// Thrown when a <see cref="Transaction"/> whose postings do not sum to zero is
/// passed to an API that requires a balanced transaction. Paper §12.1 invariant.
/// </summary>
public sealed class UnbalancedTransactionException : InvalidOperationException
{
    /// <summary>The sum of the offending transaction's postings.</summary>
    public decimal Sum { get; }

    /// <summary>The transaction id that failed balancing.</summary>
    public Guid TransactionId { get; }

    /// <summary>Constructs a new exception.</summary>
    public UnbalancedTransactionException(Guid transactionId, decimal sum)
        : base($"Transaction {transactionId} is unbalanced: posting sum = {sum} (expected 0).")
    {
        TransactionId = transactionId;
        Sum = sum;
    }
}

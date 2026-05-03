namespace Sunfish.Kernel.Ledger.Exceptions;

/// <summary>
/// Thrown by <see cref="Sunfish.Kernel.Ledger.Periods.IPeriodCloser"/> when a
/// caller attempts to close a period that ends at or before the most recent
/// closed period. Paper §12.4: periods close monotonically forward.
/// </summary>
public sealed class ClosedPeriodException : InvalidOperationException
{
    /// <summary>The period end the caller attempted to close.</summary>
    public DateTimeOffset AttemptedPeriodEnd { get; }

    /// <summary>The most recently closed period end.</summary>
    public DateTimeOffset LastClosedPeriodEnd { get; }

    /// <summary>Constructs a new exception.</summary>
    public ClosedPeriodException(DateTimeOffset attemptedPeriodEnd, DateTimeOffset lastClosedPeriodEnd)
        : base($"Cannot close period ending {attemptedPeriodEnd:O}: last closed period ends {lastClosedPeriodEnd:O}.")
    {
        AttemptedPeriodEnd = attemptedPeriodEnd;
        LastClosedPeriodEnd = lastClosedPeriodEnd;
    }
}

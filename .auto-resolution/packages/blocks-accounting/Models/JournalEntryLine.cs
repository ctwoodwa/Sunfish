namespace Sunfish.Blocks.Accounting.Models;

/// <summary>
/// A single debit or credit line within a <see cref="JournalEntry"/>.
/// </summary>
/// <remarks>
/// Invariants (enforced at construction):
/// <list type="bullet">
///   <item>Exactly one of <see cref="Debit"/> or <see cref="Credit"/> must be non-zero.</item>
///   <item>Both <see cref="Debit"/> and <see cref="Credit"/> must be non-negative.</item>
/// </list>
/// Use the constructor <c>JournalEntryLine(accountId, debit, credit, notes)</c> to create instances.
/// </remarks>
public sealed record JournalEntryLine
{
    /// <summary>Reference to the <see cref="GLAccount"/> being debited or credited.</summary>
    public GLAccountId AccountId { get; }

    /// <summary>
    /// Amount debited to <see cref="AccountId"/>.  Zero on a credit line.
    /// </summary>
    public decimal Debit { get; }

    /// <summary>
    /// Amount credited to <see cref="AccountId"/>.  Zero on a debit line.
    /// </summary>
    public decimal Credit { get; }

    /// <summary>Optional free-form annotation for this line.</summary>
    public string? Notes { get; }

    /// <summary>Constructs and validates a journal entry line.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when both <paramref name="debit"/> and <paramref name="credit"/> are non-zero,
    /// both are zero, or either is negative.
    /// </exception>
    public JournalEntryLine(GLAccountId accountId, decimal debit, decimal credit, string? notes = null)
    {
        if (debit < 0m)
            throw new ArgumentException($"Debit must be non-negative (got {debit}).", nameof(debit));
        if (credit < 0m)
            throw new ArgumentException($"Credit must be non-negative (got {credit}).", nameof(credit));
        if (debit != 0m && credit != 0m)
            throw new ArgumentException(
                $"A journal entry line cannot have both a non-zero Debit ({debit}) and a non-zero Credit ({credit}). " +
                "Each line must be exclusively a debit or a credit.",
                nameof(debit));
        if (debit == 0m && credit == 0m)
            throw new ArgumentException(
                "A journal entry line must have either a non-zero Debit or a non-zero Credit; both are zero.",
                nameof(debit));

        AccountId = accountId;
        Debit = debit;
        Credit = credit;
        Notes = notes;
    }
}

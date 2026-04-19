namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>
/// A bank account associated with a landlord or tenant.
/// Stores display-safe metadata only — no raw account or routing numbers.
/// ACH / Plaid integration is deferred to a follow-up.
/// </summary>
/// <param name="Id">Unique account identifier.</param>
/// <param name="DisplayName">Human-readable label, e.g. "Operating Checking".</param>
/// <param name="AccountHolderName">Name of the account holder as it appears on the account.</param>
/// <param name="MaskedAccountNumber">
/// Last four digits of the account number in display-safe form, e.g. <c>"****1234"</c>.
/// </param>
/// <param name="Kind">Whether this is a checking, savings, or credit account.</param>
public sealed record BankAccount(
    BankAccountId Id,
    string DisplayName,
    string AccountHolderName,
    string MaskedAccountNumber,
    BankAccountKind Kind);

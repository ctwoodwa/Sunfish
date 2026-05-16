namespace Sunfish.Blocks.FinancialLedger.Migration;

/// <summary>
/// ERPNext "Journal Entry" doctype as exported via REST.
/// </summary>
public sealed record ErpnextJournalEntrySource(
    string Name,
    string Modified,
    DateOnly PostingDate,
    string Memo,
    string VoucherType,
    bool IsOpening,
    int DocStatus,
    IReadOnlyList<ErpnextJournalEntryLineSource> Lines);

/// <summary>
/// One row in an ERPNext journal entry's <c>accounts</c> child table.
/// </summary>
public sealed record ErpnextJournalEntryLineSource(
    string AccountName,
    decimal DebitInAccountCurrency,
    decimal CreditInAccountCurrency,
    string? CostCenter,
    string? UserRemark);

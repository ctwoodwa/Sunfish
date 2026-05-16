namespace Sunfish.Blocks.FinancialLedger.Migration;

/// <summary>
/// ERPNext "Account" doctype as exported via REST. Field shapes
/// match the ERPNext API (snake_case mapped to PascalCase here).
/// </summary>
/// <param name="Name">ERPNext stable id (the "name" field; also the
/// natural key used for idempotency via <c>GLAccount.ExternalRef</c>).</param>
/// <param name="Modified">
/// ERPNext "modified" timestamp string — opaque version key. Compared
/// string-wise; ERPNext emits ISO-8601 so lexicographic order matches
/// temporal order.
/// </param>
/// <param name="AccountName">Display name (maps to <c>GLAccount.Name</c>).</param>
/// <param name="AccountNumber">Optional account code (maps to <c>GLAccount.Code</c>).</param>
/// <param name="ParentAccountName">Optional parent (by ERPNext name).</param>
/// <param name="AccountType">Raw ERPNext account_type string (Bank, Receivable, Income Account, etc.).</param>
/// <param name="IsGroup">If true → maps to <c>GLAccount.IsPostable = false</c>.</param>
/// <param name="Disabled">If true → maps to <c>GLAccount.IsActive = false</c>.</param>
public sealed record ErpnextAccountSource(
    string Name,
    string Modified,
    string AccountName,
    string? AccountNumber,
    string? ParentAccountName,
    string? AccountType,
    bool IsGroup,
    bool Disabled);

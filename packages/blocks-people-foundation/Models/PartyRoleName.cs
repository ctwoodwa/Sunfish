namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>
/// Canonical role codes that a <see cref="Party"/> can be tagged with. Role
/// codes are stable string identifiers — not enums — because future hand-offs
/// add codes ("lead", "applicant", "owner", "user", "contact") without
/// renaming existing ones (CRDT §5: open-set, additive-only).
///
/// <para>
/// <b>Format discipline:</b> lowercase, kebab-case, no spaces, no underscores,
/// no leading/trailing dash, ≤ 64 characters. <c>PartyRoleValidator</c>
/// (in <see cref="Sunfish.Blocks.People.Foundation.Validation"/>) enforces
/// the shape on write; unknown codes pass shape validation (the registry's
/// <see cref="IsKnown"/> is informational, not gated).
/// </para>
/// </summary>
public static class PartyRoleName
{
    /// <summary>Bill-payer / order-placer. Held by both individuals and orgs.</summary>
    public const string Customer = "customer";

    /// <summary>Lease tenant — the human or org named on a residential or commercial lease.</summary>
    public const string Tenant = "tenant";

    /// <summary>Sells goods / services to the business — counterparty on bills.</summary>
    public const string Vendor = "vendor";

    /// <summary>1099-style service provider engaged for a defined scope of work.</summary>
    public const string Contractor = "contractor";

    /// <summary>W-2-style staff member. Distinct from <see cref="Contractor"/> for payroll + tax purposes.</summary>
    public const string Employee = "employee";

    /// <summary>
    /// Closed enumeration of the canonical roles known at v1. Future hand-offs
    /// add codes additively (CRDT §5); unknown codes appear in stored data but
    /// do not appear here until ratified.
    /// </summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Customer,
        Tenant,
        Vendor,
        Contractor,
        Employee,
    };

    /// <summary>True if <paramref name="code"/> is one of the canonical v1 roles.</summary>
    /// <remarks>
    /// Informational only — the storage layer accepts unknown shape-valid codes
    /// (open-set policy). Use this to drive UI hints or analytics buckets, not
    /// to reject writes.
    /// </remarks>
    public static bool IsKnown(string? code) =>
        code is not null && All.Contains(code);
}

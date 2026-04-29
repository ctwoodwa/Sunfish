namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// RBAC-style role distinction within the tenant set on a lease (per W#27
/// Phase 4 + UPF Rule 5). Complements <c>PartyKind.Tenant</c> which only
/// answers "is this party a tenant"; <see cref="LeaseHolderRole"/> answers
/// "what role does this tenant play on this lease".
/// </summary>
public enum LeaseHolderRole
{
    /// <summary>Primary financial party; receives rent reminders + manages account.</summary>
    PrimaryLeaseholder,

    /// <summary>Shares lease responsibility; receives copies of all communications.</summary>
    CoLeaseholder,

    /// <summary>Listed on lease but not a financial party (e.g., minor child, dependent).</summary>
    Occupant,

    /// <summary>Financial backstop; not occupying. Signs a separate attestation document (deferred to follow-up phase).</summary>
    Guarantor
}

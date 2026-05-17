using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.People.Foundation.Migration;

/// <summary>
/// Upsert ERPNext <c>Customer</c> + <c>Supplier</c> records into the canonical
/// <see cref="Party"/> substrate. Idempotent on <c>(Name, Modified)</c>:
/// re-running the same import is a no-op (returns <see cref="ImportOutcomeKind.Skipped"/>);
/// a newer <c>Modified</c> key triggers <see cref="ImportOutcomeKind.Updated"/>.
///
/// <para>
/// <b>External-ref tag convention</b> (matches blocks-work-projects):
/// the importer tags every imported Party with two tag entries —
/// <c>externalRef:erpnext:customer:{Name}</c> (or <c>:supplier:{Name}</c>)
/// for the FK, and <c>erpnextModified:{Modified}</c> for the version key.
/// These live on <see cref="Party.Tags"/> rather than a dedicated
/// <c>ExternalRef</c> field so we don't lock the schema shape until the
/// migration-importer convention stabilizes across all clusters.
/// </para>
/// </summary>
public interface IErpnextPartyImporter
{
    /// <summary>
    /// Upsert an ERPNext Customer. Side-effects on success: a <see cref="Party"/>
    /// is created or updated; a <c>customer</c> <see cref="PartyRole"/> edge is
    /// attached pointing at the source <c>Name</c>; email + phone rows are
    /// added when the source carries shape-valid values (silently skipped
    /// otherwise — we don't want a malformed phone to fail the whole Party
    /// import).
    /// </summary>
    Task<ImportOutcome<Party>> UpsertCustomerAsync(
        ErpnextCustomerSource source,
        TenantId tenantId,
        PartyId actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert an ERPNext Supplier. Same shape as <see cref="UpsertCustomerAsync"/>
    /// but attaches a <c>vendor</c> role rather than <c>customer</c>.
    /// </summary>
    Task<ImportOutcome<Party>> UpsertSupplierAsync(
        ErpnextSupplierSource source,
        TenantId tenantId,
        PartyId actor,
        CancellationToken cancellationToken = default);
}

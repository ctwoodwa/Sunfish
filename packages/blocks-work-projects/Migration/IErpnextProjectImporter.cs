using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Migration;

/// <summary>
/// Idempotent upsert entry point for ERPNext <c>Project</c> doctype
/// records. Keyed on <c>(source, externalRefId)</c> via the tag
/// convention <c>externalRef:erpnext:&lt;name&gt;</c>. Returns
/// <see cref="ImportOutcomeKind.Skipped"/> when the existing project's
/// embedded modified-key tag matches the incoming source's
/// <see cref="ErpnextProjectSource.Modified"/>.
/// </summary>
public interface IErpnextProjectImporter
{
    Task<ImportOutcome<Project>> UpsertFromErpnextAsync(
        ErpnextProjectSource source,
        TenantId tenantId,
        Guid ownerPartyId,
        CancellationToken cancellationToken = default);
}

using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Kernel.Audit;

/// <summary>
/// Filter for an <see cref="IAuditTrail.QueryAsync"/> request. All filters are
/// AND-combined; an unfiltered query (all properties null) returns every record
/// for the calling tenant.
/// </summary>
/// <remarks>
/// Per ADR 0049 §"Open questions": the v0 surface targets two demonstrated
/// needs — IRS export (time-range) and security review (principal-filter).
/// Additional filter dimensions land as compliance use cases surface.
/// </remarks>
/// <param name="TenantId">Required. Audit reads are tenant-scoped — there is no cross-tenant audit query in v0 (see ADR 0049 §"Open questions" on whether <see cref="Sunfish.Foundation.MultiTenancy.IMayHaveTenant"/> ever applies to audit records).</param>
/// <param name="EventType">Optional. Match a single event type. Combine multiple queries to OR across types.</param>
/// <param name="OccurredAfter">Optional. Inclusive lower bound on <see cref="AuditRecord.OccurredAt"/>.</param>
/// <param name="OccurredBefore">Optional. Inclusive upper bound on <see cref="AuditRecord.OccurredAt"/>.</param>
/// <param name="IssuedBy">Optional. Match records whose payload signature was issued by this principal.</param>
public sealed record AuditQuery(
    TenantId TenantId,
    AuditEventType? EventType = null,
    DateTimeOffset? OccurredAfter = null,
    DateTimeOffset? OccurredBefore = null,
    PrincipalId? IssuedBy = null);

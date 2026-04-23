using System.IO;

namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Static helper for the on-disk path conventions used by Bridge's per-tenant
/// data-plane orchestration (tenant root, node data directory, graveyard).
/// Pure functions with no I/O — every method composes paths from the
/// caller-provided <c>tenantDataRoot</c> and a <see cref="System.Guid"/>;
/// none create, inspect, or mutate the filesystem.
/// </summary>
/// <remarks>
/// <para>
/// Lock-in per <c>_shared/product/wave-5.2-decomposition.md</c> §5 "Tenant
/// Data-Dir Layout — Lock-In". The string conventions — the literal
/// <c>tenants/</c> and <c>graveyard/</c> segments, the <c>"D"</c>-format GUID
/// rendering, and the <c>yyyyMMdd-HHmmss</c> timestamp shape on graveyard
/// roots — are pinned here so the supervisor (Wave 5.2.C), the registry
/// lifecycle (5.2.B), and the health monitor (5.2.D) always agree on layout.
/// </para>
/// <para>
/// Deliberately mirrors <c>Sunfish.Kernel.Runtime.Teams.TeamPaths</c> in
/// shape (static helper, not <c>ITenantDirectoryLayout</c> interface). The
/// decomposition plan declares this a static helper so MDM overrides cannot
/// smuggle a different layout in; if runtime substitution is ever needed,
/// introduce the interface then — not now.
/// </para>
/// <para>
/// <b>TenantId formatting.</b> Every path segment that interpolates the tenant
/// id uses <c>tenantId.ToString("D")</c> — the 36-character hyphenated GUID
/// form <c>"00000000-0000-0000-0000-000000000000"</c>. Callers MUST NOT
/// URL-encode, hyphen-strip, uppercase, or otherwise normalize the rendered
/// tenant id; doing so breaks correspondence with directory names created by
/// earlier calls.
/// </para>
/// <para>
/// <b>Doubly-nested <c>{TenantId:D}</c>.</b> The outer segment (here) is
/// Bridge-scope (supervisor owns); the inner <c>teams/{teamId:D}</c> segment
/// (computed inside the child by <c>TeamPaths</c>) is node-scope
/// (kernel-runtime owns). They happen to share the GUID because Wave 5.2 maps
/// tenant 1:1 to team; future cross-tenant collaboration can break the
/// symmetry without restructure.
/// </para>
/// </remarks>
public static class TenantPaths
{
    private const string TenantsSegment = "tenants";
    private const string GraveyardSegment = "graveyard";
    private const string NodeSegment = "node";
    private const string GraveyardTimestampFormat = "yyyyMMdd-HHmmss";

    /// <summary>
    /// Returns the on-disk root directory for the given tenant —
    /// <c>{tenantDataRoot}/tenants/{tenantId:D}</c>, no trailing separator.
    /// </summary>
    /// <remarks>
    /// Per decomposition plan §5 lock-in table. Callers that need a child
    /// path inside the tenant root should compose via <see cref="Path.Combine(string, string)"/>
    /// rather than string concatenation.
    /// </remarks>
    /// <param name="tenantDataRoot">Bridge-owned root (typically bound from
    /// <c>BridgeOrchestrationOptions.TenantDataRoot</c>).</param>
    /// <param name="tenantId">Tenant whose root directory to compute.</param>
    public static string TenantRoot(string tenantDataRoot, Guid tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantDataRoot);
        return Path.Combine(tenantDataRoot, TenantsSegment, tenantId.ToString("D"));
    }

    /// <summary>
    /// Returns the absolute path that becomes the per-tenant child's
    /// <c>LocalNodeOptions.DataDirectory</c> —
    /// <c>{tenantDataRoot}/tenants/{tenantId:D}/node</c>, no trailing separator.
    /// </summary>
    /// <remarks>
    /// Per decomposition plan §5 lock-in table. Kernel-runtime's
    /// <c>TeamPaths</c> then composes <c>teams/{teamId:D}/sunfish.db</c> etc.
    /// underneath this directory; the doubly-nested GUID (here, then again
    /// inside) is intentional (see type remarks).
    /// </remarks>
    public static string NodeDataDirectory(string tenantDataRoot, Guid tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantDataRoot);
        return Path.Combine(tenantDataRoot, TenantsSegment, tenantId.ToString("D"), NodeSegment);
    }

    /// <summary>
    /// Returns the directory into which the supervisor moves a tenant's disk
    /// on <c>DeleteMode.RetainCiphertext</c> — literally
    /// <c>{tenantDataRoot}/graveyard/{tenantId:D}/{cancelledAt:yyyyMMdd-HHmmss}</c>,
    /// no trailing separator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per decomposition plan §5 lock-in table. The timestamp segment lets a
    /// single tenant be cancelled-and-recreated repeatedly without collision
    /// (each cancellation lands in a fresh sub-directory keyed by its UTC
    /// wall-clock instant).
    /// </para>
    /// <para>
    /// The timestamp format is literal <c>yyyyMMdd-HHmmss</c> — no offset
    /// suffix, no seconds-fraction. Callers SHOULD pass a UTC
    /// <see cref="DateTimeOffset"/>; the format does not disambiguate time
    /// zones, so mixing local-time instants across machines will produce
    /// misordered graveyard directories.
    /// </para>
    /// </remarks>
    public static string GraveyardRoot(string tenantDataRoot, Guid tenantId, DateTimeOffset cancelledAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantDataRoot);
        return Path.Combine(
            tenantDataRoot,
            GraveyardSegment,
            tenantId.ToString("D"),
            cancelledAt.ToString(GraveyardTimestampFormat, System.Globalization.CultureInfo.InvariantCulture));
    }
}

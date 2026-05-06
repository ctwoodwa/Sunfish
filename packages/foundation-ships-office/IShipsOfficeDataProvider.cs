using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Read-side data provider for the Ship's Office surface per ADR 0083 §2.
/// Provides a snapshot, a search enumerator, and a change-subscription
/// stream. Implementations live in <c>blocks-ships-office</c> (Phase 2);
/// this contract is the foundation-tier seam consumers depend on.
/// </summary>
/// <remarks>
/// <para>
/// <b>CALLER CONTRACT (per ADR 0083 §2):</b> callers MUST verify
/// <see cref="ShipAction.ViewShipsOffice"/> via <see cref="IPermissionResolver"/>
/// before invoking any method on this interface. The data provider does
/// not re-verify role — it is the caller's (UI block's) responsibility
/// to gate access. This contract is enforced by Roslyn analyzer
/// <c>SUNFISH_SHIPSOFFICE_PERM001</c> shipping with W#55 Phase 2; until
/// Phase 2 lands, this is a comment-only contract — pre-merge code
/// review is the enforcement substitute (W#55 P1 pre-merge council
/// 2026-05-06 — Major PL-1).
/// </para>
/// <para>
/// <b>FORBIDDEN: <c>Sunfish.Foundation.Recovery.IFieldDecryptor</c>.</b>
/// Implementations MUST NOT call <c>IFieldDecryptor</c> anywhere in the
/// implementation chain. <see cref="ShipsOfficeDocumentKind.VendorW9"/>
/// documents render with the W9 TIN ALWAYS redacted in browse view per
/// §Trust impact; decryption only happens on the per-document detail
/// surface (a separate authority cell), never in the Ship's Office
/// browse pane.
/// </para>
/// <para>
/// <b>Latency posture:</b> recommended completion under 2s per
/// <see cref="GetSnapshotAsync"/>. Phase 1 ships a complete-or-fault
/// posture (implementations either materialize the full page or throw);
/// partial-snapshot-on-timeout requires a <c>WasTruncated</c> field on
/// <see cref="ShipsOfficeSnapshot"/> that lands in Phase 2.
/// </para>
/// </remarks>
public interface IShipsOfficeDataProvider
{
    /// <summary>
    /// Materialize the current Ship's Office snapshot for the supplied
    /// tenant. Pre-condition: caller has verified
    /// <see cref="ShipAction.ViewShipsOffice"/>.
    /// </summary>
    Task<ShipsOfficeSnapshot> GetSnapshotAsync(TenantId tenant, CancellationToken ct = default);

    /// <summary>
    /// Stream documents matching <paramref name="query"/>. Pre-condition:
    /// caller has verified <see cref="ShipAction.ViewShipsOffice"/>.
    /// Implementations emit
    /// <see cref="Sunfish.Kernel.Audit.AuditEventType.ShipsOfficeDocumentSearched"/>
    /// on every call returning ≥1 result; zero-result background polls
    /// SHOULD suppress the audit emission per §6 audit-noise policy.
    /// </summary>
    IAsyncEnumerable<ShipsOfficeDocumentView> SearchAsync(
        TenantId tenant,
        ShipsOfficeSearchQuery query,
        CancellationToken ct);

    /// <summary>
    /// Subscribe to incremental document-view changes for the supplied
    /// tenant. Pre-condition: caller has verified
    /// <see cref="ShipAction.ViewShipsOffice"/>.
    /// </summary>
    /// <remarks>
    /// Emits one <see cref="ShipsOfficeDocumentView"/> per changed
    /// document (incremental); does NOT replay history. Initial-state
    /// hydration is the caller's responsibility via
    /// <see cref="GetSnapshotAsync"/> before subscribing. Implementations
    /// may fall back to polling at
    /// <c>ShipsOfficeOptions.FallbackPollingInterval</c> when no push
    /// transport is available.
    /// </remarks>
    IAsyncEnumerable<ShipsOfficeDocumentView> SubscribeChangesAsync(
        TenantId tenant,
        CancellationToken ct);
}

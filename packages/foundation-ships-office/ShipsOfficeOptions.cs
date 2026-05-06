using System;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Host-configurable Ship's Office tunables per ADR 0083 §7. All defaults
/// are conservative; tenants opt up to stricter settings (e.g.,
/// <see cref="RequireSecondActorPublish"/>) as their compliance posture
/// demands.
/// </summary>
public sealed class ShipsOfficeOptions
{
    /// <summary>
    /// Polling cadence used by <see cref="IShipsOfficeDataProvider.SubscribeChangesAsync"/>
    /// implementations that fall back to polling when no push transport
    /// is available. Default 60 seconds.
    /// </summary>
    public TimeSpan FallbackPollingInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum number of <see cref="ShipsOfficeDocumentView"/> records
    /// returned by <see cref="IShipsOfficeDataProvider.GetSnapshotAsync"/>
    /// in a single page. Default 500.
    /// </summary>
    /// <remarks>
    /// Per W#55 P1 pre-merge council 2026-05-06 (Minor SE-1): tenants
    /// with very large document corpora SHOULD lower this and rely on
    /// <see cref="IShipsOfficeDataProvider.SearchAsync"/> paging instead.
    /// The snapshot endpoint is intended for typical-tenant browse
    /// hydration, not exhaustive enumeration.
    /// </remarks>
    public int SnapshotPageSize { get; set; } = 500;

    /// <summary>
    /// When true, <see cref="IShipsOfficeCommandService.PublishAsync"/>
    /// requires a second-actor co-sign before the document moves to
    /// <see cref="DocumentStatus.Published"/>. Default false. Phase 2
    /// opt-in for regulated-industry tenants per A1 amendment; Bridge
    /// (regulated-default) hosts SHOULD set this to true via DI options
    /// configuration.
    /// </summary>
    public bool RequireSecondActorPublish { get; set; } = false;
}

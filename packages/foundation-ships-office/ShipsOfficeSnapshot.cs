using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Aggregate snapshot of a tenant's Ship's Office contents at a point in
/// time per ADR 0083 §1. <see cref="IShipsOfficeDataProvider.GetSnapshotAsync"/>
/// returns this; the browse pane renders <see cref="Documents"/> directly.
/// </summary>
/// <param name="Documents">Documents in the tenant's Ship's Office (page-bounded by <c>SnapshotPageSize</c>).</param>
/// <param name="TotalCount">Total document count across the tenant; equals <see cref="Documents"/> length when fewer than one page exists.</param>
/// <param name="AsOf">Wall-clock time the snapshot was materialized.</param>
public sealed record ShipsOfficeSnapshot(
    IReadOnlyList<ShipsOfficeDocumentView> Documents,
    int TotalCount,
    DateTimeOffset AsOf);

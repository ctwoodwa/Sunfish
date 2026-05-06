using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Read-side projection of a Ship's Office document per ADR 0083 §1. The
/// view is intentionally narrow — no body, no decrypted fields — so the
/// browse pane can render without invoking <c>IFieldDecryptor</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>§Trust impact (W9 TIN redaction).</b> For
/// <see cref="ShipsOfficeDocumentKind.VendorW9"/> documents,
/// <see cref="Title"/> is the vendor display name only; the W9 TIN field
/// is excluded from this view. Per ADR 0083 council resolution: the W9
/// TIN is ALWAYS redacted in browse view regardless of caller role —
/// decryption requires the vendor-detail surface, NOT the browse pane.
/// </para>
/// <para>
/// <b>Time fields use <see cref="DateTimeOffset"/></b> per W#49 cohort
/// precedent (W#34 / W#35 / W#40 / W#41 / W#46). The hand-off cited
/// <c>NodaTime.Instant</c>; <c>NodaTime</c> is not on
/// <c>Directory.Packages.props</c>, and adding a foundation dependency
/// is outside COB scope. <see cref="DateTimeOffset"/> aligns with
/// <c>AuditRecord.OccurredAt</c> + every other built foundation
/// time-bearing record.
/// </para>
/// </remarks>
/// <param name="Id">Stable, opaque document identifier.</param>
/// <param name="Kind">Document kind discriminator.</param>
/// <param name="Title">Display title; for VendorW9 this is the vendor display name only (TIN excluded).</param>
/// <param name="Status">Lifecycle status.</param>
/// <param name="UpdatedAt">Wall-clock time of the most recent change.</param>
/// <param name="LastModifiedBy">Actor who made the most recent change.</param>
/// <param name="VersionLabel">Optional human-readable version label (e.g., <c>"v3"</c>); null when the document kind does not carry versions.</param>
public sealed record ShipsOfficeDocumentView(
    ShipsOfficeDocumentId Id,
    ShipsOfficeDocumentKind Kind,
    string Title,
    DocumentStatus Status,
    DateTimeOffset UpdatedAt,
    ActorId LastModifiedBy,
    string? VersionLabel);

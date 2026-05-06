using System;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Per-field-purpose pharmacy inventory entry per ADR 0082 §1. The
/// pharmacy panel renders one row per entry; <see cref="RecordCount"/>
/// applies the k=3 anonymity floor; the <c>PendingTriggerLabel</c>
/// field (Phase 2; gated on H3 / ADR 0068 Accepted) is intentionally
/// absent from this Phase 1 record.
/// </summary>
/// <remarks>
/// <see cref="DateTimeOffset"/> per cohort precedent (W#46 / W#49 / W#50 /
/// W#55) — hand-off cited <c>NodaTime.Instant</c> but NodaTime is not on
/// <c>Directory.Packages.props</c>; future ADR amendment will migrate
/// every cohort time-bearing record at once.
/// </remarks>
/// <param name="FieldPurpose">Wayfinder field-purpose key (e.g., <c>"recovery-key"</c>); registered in <see cref="SickBayOptions.RegisteredFieldPurposes"/>.</param>
/// <param name="FriendlyName">Localized display name for the field-purpose; rendered in the pharmacy table header.</param>
/// <param name="RecordCount">k=3-anonymity-aware count of records with this field-purpose.</param>
/// <param name="LastRotatedAt">Wall-clock time of the most recent successful rotation.</param>
/// <param name="RotationStatus">Discriminator for the rotation window's current state.</param>
/// <param name="HasCompromiseFlag">True when the field has been flagged compromised; UI surfaces this with a Critical badge.</param>
public sealed record PharmacyInventoryEntry(
    string FieldPurpose,
    string FriendlyName,
    PharmacyRecordCount RecordCount,
    DateTimeOffset LastRotatedAt,
    RotationHealth RotationStatus,
    bool HasCompromiseFlag);

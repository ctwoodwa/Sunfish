namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>Identifier for a <c>VendorContact</c> child entity (W#18 Phase 2).</summary>
public readonly record struct VendorContactId(Guid Value);

/// <summary>Identifier for a <c>VendorPerformanceRecord</c> entry in the append-only event log (W#18 Phase 3).</summary>
public readonly record struct VendorPerformanceRecordId(Guid Value);

/// <summary>Identifier for a <c>W9Document</c> entity (W#18 Phase 4).</summary>
public readonly record struct W9DocumentId(Guid Value);

/// <summary>Identifier for a <c>VendorMagicLink</c> issuance (W#18 Phase 5).</summary>
public readonly record struct VendorMagicLinkId(Guid Value);

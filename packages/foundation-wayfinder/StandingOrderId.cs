using System;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Stable identifier for a <see cref="StandingOrder"/>. Per ADR 0065 §1.
/// </summary>
public readonly record struct StandingOrderId(Guid Value);

/// <summary>
/// Stable identifier referencing a <see cref="Sunfish.Kernel.Audit.AuditRecord"/>
/// emitted at the time a Standing Order was issued, amended, rescinded, rejected,
/// or conflict-resolved. Audit-record-id round-trips with
/// <see cref="Sunfish.Kernel.Audit.AuditRecord.AuditId"/>. Per ADR 0065 §1.
/// </summary>
public readonly record struct AuditRecordId(Guid Value);

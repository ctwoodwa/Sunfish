using System;

namespace Sunfish.Foundation.Assets.Common;

/// <summary>
/// Stable identifier for a Standing Order. Per ADR 0065 §1.
/// </summary>
/// <remarks>
/// W#48 Phase 1.5 PR 1: moved from <c>Sunfish.Foundation.Wayfinder</c>
/// to <c>Sunfish.Foundation.Assets.Common</c> to break the
/// <c>ui-core → foundation-wayfinder → kernel-crdt → ui-core</c>
/// cycle. After this move, the <c>Sunfish.UICore.Wayfinder.Integrations</c>
/// records can reference <see cref="StandingOrderId"/> via
/// <c>foundation</c> (which <c>ui-core</c> already pulls in) without
/// dragging in <c>foundation-wayfinder</c>.
/// </remarks>
/// <param name="Value">Provider-internal GUID identifier.</param>
public readonly record struct StandingOrderId(Guid Value);

/// <summary>
/// Stable identifier referencing a <c>Sunfish.Kernel.Audit.AuditRecord</c>
/// emitted at the time a Standing Order was issued, amended, rescinded,
/// rejected, or conflict-resolved. Audit-record-id round-trips with
/// <c>Sunfish.Kernel.Audit.AuditRecord.AuditId</c>. Per ADR 0065 §1.
/// </summary>
/// <remarks>
/// W#48 Phase 1.5 PR 1: relocated alongside <see cref="StandingOrderId"/>.
/// </remarks>
/// <param name="Value">Provider-internal GUID identifier.</param>
public readonly record struct AuditRecordId(Guid Value);

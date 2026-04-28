namespace Sunfish.Kernel.Audit;

/// <summary>
/// A single audit record was appended. Persisted to the kernel
/// <c>IEventLog</c> as the durability hook (per ADR 0049). Direct parallel to
/// <c>Sunfish.Kernel.Ledger.PostingsAppliedEvent</c>.
/// </summary>
/// <param name="Record">The committed audit record. Signature verification has already been performed at <see cref="IAuditTrail.AppendAsync"/>; consumers may treat the record as authentic.</param>
public sealed record AuditAppendedEvent(AuditRecord Record);

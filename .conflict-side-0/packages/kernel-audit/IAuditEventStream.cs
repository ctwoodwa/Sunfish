namespace Sunfish.Kernel.Audit;

/// <summary>
/// In-process typed event stream for audit-subsystem consumers (compliance
/// projections, retention reporters, audit-log UIs). Sits alongside the kernel
/// <c>IEventLog</c> which carries untyped <c>KernelEvent</c>s — this stream
/// carries domain-typed <see cref="AuditRecord"/>s.
/// </summary>
/// <remarks>
/// Direct parallel to <c>Sunfish.Kernel.Ledger.ILedgerEventStream</c>. The
/// stream is in-process only; durability is the kernel <c>IEventLog</c>'s
/// responsibility. <see cref="ReplayAll"/> rebuilds the stream from scratch,
/// which projections use to reconstruct state without a snapshot.
/// </remarks>
public interface IAuditEventStream
{
    /// <summary>Replay every appended audit record in append order.</summary>
    IReadOnlyList<AuditRecord> ReplayAll();

    /// <summary>
    /// Subscribe a callback invoked for each newly-appended audit record.
    /// Returns an <see cref="IDisposable"/> that unsubscribes on dispose.
    /// </summary>
    IDisposable Subscribe(Action<AuditRecord> handler);
}

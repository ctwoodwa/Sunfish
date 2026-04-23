namespace Sunfish.Kernel.Ledger;

/// <summary>
/// Internal typed event stream for ledger subsystem consumers (balance and
/// statement projections, period closer). Sits alongside the kernel
/// <c>IEventLog</c> which carries untyped <c>KernelEvent</c>s — this stream
/// carries the domain-typed ledger events (<see cref="PostingsAppliedEvent"/>,
/// <see cref="CompensationAppliedEvent"/>, <see cref="PeriodClosedEvent"/>).
/// </summary>
/// <remarks>
/// <para>
/// Paper §12.3: "Projections are rebuilt from the event stream if needed."
/// <see cref="ReplayAll"/> enumerates every committed event in the order it
/// was appended, which projections can use to rebuild state from scratch.
/// </para>
/// <para>
/// The stream is in-process only. A future distributed backend would pair this
/// with a persistent event log (the kernel <c>IEventLog</c> append is the
/// durability hook) but in-process projections subscribe directly.
/// </para>
/// </remarks>
public interface ILedgerEventStream
{
    /// <summary>Replay every committed ledger event in append order.</summary>
    IReadOnlyList<object> ReplayAll();

    /// <summary>
    /// Subscribe a callback invoked for each newly-committed ledger event.
    /// Returns an <see cref="IDisposable"/> that unsubscribes on dispose.
    /// </summary>
    IDisposable Subscribe(Action<object> handler);
}

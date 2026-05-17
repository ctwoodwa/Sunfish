using Sunfish.Blocks.WorkProjects.Events;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Façade over <see cref="JournalEntryPostedHandler"/>. Exposes a
/// replay surface for rebuilding the <see cref="Models.ProjectActual"/>
/// table from the upstream event stream — per
/// <c>cross-cluster-event-bus-design.md</c> §5, the table is derived
/// state and the upstream event store is the canonical source.
/// </summary>
public interface IProjectActualProjector
{
    /// <summary>The handler delegate registered with the dispatcher.</summary>
    IEventHandler<JournalEntryPostedPayload> Handler { get; }

    /// <summary>
    /// Replay the supplied envelopes through the handler in stream
    /// order. Intended for rebuilds from a known cursor; callers
    /// pre-fetch the envelopes (typically from
    /// <c>foundation-events</c>'s event store). Idempotent — re-running
    /// over the same range produces the same final state.
    /// </summary>
    Task RebuildFromCursorAsync(
        IEnumerable<DomainEventEnvelope<JournalEntryPostedPayload>> envelopes,
        CancellationToken cancellationToken = default);
}

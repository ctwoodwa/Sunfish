using Sunfish.Blocks.WorkProjects.Events;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>Default <see cref="IProjectActualProjector"/> wrapping <see cref="JournalEntryPostedHandler"/>.</summary>
public sealed class InMemoryProjectActualProjector : IProjectActualProjector
{
    public InMemoryProjectActualProjector(JournalEntryPostedHandler handler)
    {
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <inheritdoc />
    public IEventHandler<JournalEntryPostedPayload> Handler { get; }

    /// <inheritdoc />
    public async Task RebuildFromCursorAsync(
        IEnumerable<DomainEventEnvelope<JournalEntryPostedPayload>> envelopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        foreach (var envelope in envelopes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Handler.HandleAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
    }
}

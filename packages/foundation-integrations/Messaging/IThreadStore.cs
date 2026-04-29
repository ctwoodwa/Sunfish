using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// CRUD contract for the messaging thread substrate. Implementations live in
/// <c>blocks-messaging</c>; <see cref="SplitAsync"/> is the W#19 Work Orders
/// Phase 6 cross-package wiring point per the hand-off.
/// </summary>
public interface IThreadStore
{
    /// <summary>Creates a new thread with a participant set and visibility.</summary>
    /// <param name="tenant">Tenant the thread is scoped to.</param>
    /// <param name="participants">Initial participant set; must contain at least one participant.</param>
    /// <param name="defaultVisibility">Default per-message visibility for the thread.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="ThreadId"/>.</returns>
    Task<ThreadId> CreateAsync(
        TenantId tenant,
        IReadOnlyList<Participant> participants,
        MessageVisibility defaultVisibility,
        CancellationToken ct);

    /// <summary>Reads a thread snapshot — participants + ordered list of message ids.</summary>
    /// <param name="tenant">Tenant; required for cross-tenant isolation.</param>
    /// <param name="threadId">Target thread.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The thread snapshot, or null when not present.</returns>
    Task<ThreadSnapshot?> GetAsync(TenantId tenant, ThreadId threadId, CancellationToken ct);

    /// <summary>
    /// Forks a child thread with a (possibly different) participant set,
    /// optionally copying forward a subset of message ids. Used by the Minor
    /// amendment "private aside" use case + W#19 Phase 6 work-order
    /// owner-vendor side branches.
    /// </summary>
    /// <param name="tenant">Tenant; required for cross-tenant isolation.</param>
    /// <param name="sourceThreadId">Parent thread.</param>
    /// <param name="newParticipants">Participant set for the child thread.</param>
    /// <param name="copyForwardMessageIds">Subset of message ids to copy forward; pass an empty list to start the child thread fresh.</param>
    /// <param name="newDefaultVisibility">Default visibility on the child thread.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The child <see cref="ThreadId"/>.</returns>
    Task<ThreadId> SplitAsync(
        TenantId tenant,
        ThreadId sourceThreadId,
        IReadOnlyList<Participant> newParticipants,
        IReadOnlyList<MessageId> copyForwardMessageIds,
        MessageVisibility newDefaultVisibility,
        CancellationToken ct);

    /// <summary>Appends a message id to the thread's ordered message list. The full <see cref="MessageId"/>-keyed payload lives in the thread store's message-side persistence.</summary>
    /// <param name="tenant">Tenant; required for cross-tenant isolation.</param>
    /// <param name="threadId">Target thread.</param>
    /// <param name="messageId">Id to append.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AppendMessageAsync(TenantId tenant, ThreadId threadId, MessageId messageId, CancellationToken ct);
}

/// <summary>
/// Lightweight read projection of a thread (participants + ordered message
/// ids) returned by <see cref="IThreadStore.GetAsync"/>.
/// </summary>
public sealed record ThreadSnapshot
{
    /// <summary>Thread identifier.</summary>
    public required ThreadId Id { get; init; }

    /// <summary>Owning tenant.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>Current participant set.</summary>
    public required IReadOnlyList<Participant> Participants { get; init; }

    /// <summary>Default per-message visibility for the thread.</summary>
    public required MessageVisibility DefaultVisibility { get; init; }

    /// <summary>Message ids appended to the thread, in append order.</summary>
    public required IReadOnlyList<MessageId> MessageIds { get; init; }

    /// <summary>Wall-clock time the thread was opened.</summary>
    public required DateTimeOffset OpenedAt { get; init; }

    /// <summary>Wall-clock time the thread was closed; null while open.</summary>
    public DateTimeOffset? ClosedAt { get; init; }
}

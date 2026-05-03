using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Messaging;

namespace Sunfish.Blocks.Messaging.Models;

/// <summary>
/// A messaging thread — durable, audit-logged, ordered conversation between
/// a fixed participant set under a single per-thread visibility default.
/// Per ADR 0052; this is the cluster #4 spine. (Named <c>MessageThread</c>
/// rather than <c>Thread</c> to avoid the <c>System.Threading.Thread</c>
/// collision under ImplicitUsings.)
/// </summary>
public sealed record MessageThread
{
    /// <summary>Stable thread identifier from <see cref="IThreadStore.CreateAsync"/>.</summary>
    public required ThreadId Id { get; init; }

    /// <summary>Owning tenant; required per <c>IMustHaveTenant</c>.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>Participant set at the current moment. Mutations (add/remove) are recorded as audit events but the participant list itself is the live state.</summary>
    public required IReadOnlyList<Participant> Participants { get; init; }

    /// <summary>Default per-message visibility for the thread; individual messages may narrow via <see cref="MessageVisibility"/> override.</summary>
    public required MessageVisibility DefaultVisibility { get; init; }

    /// <summary>Wall-clock time the thread was opened.</summary>
    public required DateTimeOffset OpenedAt { get; init; }

    /// <summary>Wall-clock time the thread was last updated (any participant change or message append).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Wall-clock time the thread was closed; null while open.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Ordered list of message ids appended to this thread, oldest first. The full <see cref="Message"/> bodies live in the message-side store keyed by id.</summary>
    public IReadOnlyList<MessageId> MessageIds { get; init; } = Array.Empty<MessageId>();
}

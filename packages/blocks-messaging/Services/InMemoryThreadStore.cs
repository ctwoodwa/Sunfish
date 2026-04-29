using System.Collections.Concurrent;
using Sunfish.Blocks.Messaging.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Messaging;

namespace Sunfish.Blocks.Messaging.Services;

/// <summary>
/// In-memory reference implementation of <see cref="IThreadStore"/>.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>; not
/// durable.
/// </summary>
public sealed class InMemoryThreadStore : IThreadStore
{
    private readonly ConcurrentDictionary<(TenantId Tenant, ThreadId Id), MessageThread> _threads = new();

    /// <summary>Snapshot view of every thread held in memory; used by tests for assertions.</summary>
    internal IReadOnlyDictionary<(TenantId, ThreadId), MessageThread> Snapshot => _threads;

    /// <inheritdoc />
    public Task<ThreadId> CreateAsync(TenantId tenant, IReadOnlyList<Participant> participants, MessageVisibility defaultVisibility, CancellationToken ct)
    {
        EnsureTenant(tenant);
        ArgumentNullException.ThrowIfNull(participants);
        if (participants.Count == 0)
        {
            throw new ArgumentException("At least one participant is required to open a thread.", nameof(participants));
        }

        var now = DateTimeOffset.UtcNow;
        var id = new ThreadId(Guid.NewGuid());
        var thread = new MessageThread
        {
            Id = id,
            Tenant = tenant,
            Participants = participants,
            DefaultVisibility = defaultVisibility,
            OpenedAt = now,
            UpdatedAt = now,
        };

        if (!_threads.TryAdd((tenant, id), thread))
        {
            // Vanishingly rare GUID collision; surface explicitly rather than silently overwrite.
            throw new InvalidOperationException($"Thread '{id}' already exists for tenant '{tenant}'.");
        }
        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public Task<ThreadSnapshot?> GetAsync(TenantId tenant, ThreadId threadId, CancellationToken ct)
    {
        EnsureTenant(tenant);
        if (!_threads.TryGetValue((tenant, threadId), out var thread))
        {
            return Task.FromResult<ThreadSnapshot?>(null);
        }
        return Task.FromResult<ThreadSnapshot?>(new ThreadSnapshot
        {
            Id = thread.Id,
            Tenant = thread.Tenant,
            Participants = thread.Participants,
            DefaultVisibility = thread.DefaultVisibility,
            MessageIds = thread.MessageIds,
            OpenedAt = thread.OpenedAt,
            ClosedAt = thread.ClosedAt,
        });
    }

    /// <inheritdoc />
    public Task<ThreadId> SplitAsync(TenantId tenant, ThreadId sourceThreadId, IReadOnlyList<Participant> newParticipants, IReadOnlyList<MessageId> copyForwardMessageIds, MessageVisibility newDefaultVisibility, CancellationToken ct)
    {
        EnsureTenant(tenant);
        ArgumentNullException.ThrowIfNull(newParticipants);
        ArgumentNullException.ThrowIfNull(copyForwardMessageIds);
        if (newParticipants.Count == 0)
        {
            throw new ArgumentException("At least one participant is required to open a child thread.", nameof(newParticipants));
        }
        if (!_threads.ContainsKey((tenant, sourceThreadId)))
        {
            throw new InvalidOperationException($"Cannot split thread '{sourceThreadId}' for tenant '{tenant}': source thread not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var childId = new ThreadId(Guid.NewGuid());
        var child = new MessageThread
        {
            Id = childId,
            Tenant = tenant,
            Participants = newParticipants,
            DefaultVisibility = newDefaultVisibility,
            OpenedAt = now,
            UpdatedAt = now,
            MessageIds = copyForwardMessageIds,
        };

        if (!_threads.TryAdd((tenant, childId), child))
        {
            throw new InvalidOperationException($"Child thread '{childId}' already exists for tenant '{tenant}'.");
        }
        return Task.FromResult(childId);
    }

    /// <inheritdoc />
    public Task AppendMessageAsync(TenantId tenant, ThreadId threadId, MessageId messageId, CancellationToken ct)
    {
        EnsureTenant(tenant);
        var key = (tenant, threadId);
        var success = false;
        while (!success)
        {
            if (!_threads.TryGetValue(key, out var current))
            {
                throw new InvalidOperationException($"Thread '{threadId}' not found for tenant '{tenant}'.");
            }
            var nextIds = new List<MessageId>(current.MessageIds.Count + 1);
            nextIds.AddRange(current.MessageIds);
            nextIds.Add(messageId);
            var updated = current with
            {
                MessageIds = nextIds.AsReadOnly(),
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            success = _threads.TryUpdate(key, updated, current);
        }
        return Task.CompletedTask;
    }

    private static void EnsureTenant(TenantId tenant)
    {
        if (tenant == default)
        {
            throw new ArgumentException("TenantId is required.", nameof(tenant));
        }
    }
}

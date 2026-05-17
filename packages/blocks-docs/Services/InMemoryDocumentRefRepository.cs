using System.Collections.Concurrent;
using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// In-memory <see cref="IDocumentRefRepository"/>. Mirrors the v1 shape
/// of <see cref="InMemoryAttachmentRepository"/>: a single concurrent
/// dictionary keyed by <see cref="DocumentRefId"/>, with secondary
/// queries scanning the values. Persistence-backed implementations
/// shadow this via <c>TryAddSingleton</c> in the docs DI extension.
/// </summary>
public sealed class InMemoryDocumentRefRepository : IDocumentRefRepository
{
    private readonly ConcurrentDictionary<DocumentRefId, DocumentRef> _refs = new();

    /// <inheritdoc />
    public Task UpsertAsync(DocumentRef documentRef, CancellationToken cancellationToken = default)
    {
        if (documentRef is null) throw new ArgumentNullException(nameof(documentRef));
        if (_refs.TryGetValue(documentRef.Id, out var existing) && existing.DeletedAtUtc is not null)
            throw new InvalidOperationException($"DocumentRef '{documentRef.Id.Value}' is tombstoned; further mutations are not permitted.");
        _refs[documentRef.Id] = documentRef;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<DocumentRef?> GetAsync(DocumentRefId id, CancellationToken cancellationToken = default)
    {
        _refs.TryGetValue(id, out var r);
        return Task.FromResult(r is null || r.DeletedAtUtc is not null ? null : r);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentRef>> FindByAttachmentAsync(
        TenantId tenantId,
        AttachmentId attachmentId,
        CancellationToken cancellationToken = default)
    {
        var rows = _refs.Values
            .Where(r => r.DeletedAtUtc is null
                && r.TenantId == tenantId
                && r.AttachmentId == attachmentId)
            .ToList();
        return Task.FromResult<IReadOnlyList<DocumentRef>>(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentRef>> FindByParentAsync(
        TenantId tenantId,
        string clusterCode,
        string parentEntityType,
        string parentEntityId,
        CancellationToken cancellationToken = default)
    {
        var rows = _refs.Values
            .Where(r => r.DeletedAtUtc is null
                && r.TenantId == tenantId
                && string.Equals(r.ClusterCode, clusterCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.ParentEntityType, parentEntityType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.ParentEntityId, parentEntityId, StringComparison.Ordinal))
            .ToList();
        return Task.FromResult<IReadOnlyList<DocumentRef>>(rows);
    }

    /// <inheritdoc />
    public Task<bool> SoftDeleteAsync(
        DocumentRefId id,
        string actor,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!_refs.TryGetValue(id, out var r)) return Task.FromResult(false);
        if (r.DeletedAtUtc is not null) return Task.FromResult(false);

        var now = Instant.Now;
        _refs[id] = r with
        {
            DeletedAtUtc = now,
            DeletedBy = actor,
            DeletedReason = reason,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = r.Version + 1,
        };
        return Task.FromResult(true);
    }
}

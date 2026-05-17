using System.Collections.Concurrent;
using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// In-memory <see cref="IAttachmentRepository"/>. State lives in a
/// single <c>ConcurrentDictionary</c> keyed by <see cref="AttachmentId"/>;
/// secondary queries (by content-hash, by tenant) scan the values — fine
/// for the in-memory v1 path. A SQLite-backed implementation lands in
/// the follow-on substrate hand-off and shadows this binding.
/// </summary>
public sealed class InMemoryAttachmentRepository : IAttachmentRepository
{
    private readonly ConcurrentDictionary<AttachmentId, Attachment> _attachments = new();

    /// <inheritdoc />
    public Task UpsertAsync(Attachment attachment, CancellationToken cancellationToken = default)
    {
        if (attachment is null) throw new ArgumentNullException(nameof(attachment));
        if (_attachments.TryGetValue(attachment.Id, out var existing) && existing.DeletedAtUtc is not null)
            throw new InvalidOperationException($"Attachment '{attachment.Id.Value}' is tombstoned; further mutations are not permitted.");
        _attachments[attachment.Id] = attachment;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Attachment?> GetAsync(AttachmentId id, CancellationToken cancellationToken = default)
    {
        _attachments.TryGetValue(id, out var a);
        return Task.FromResult(a is null || a.DeletedAtUtc is not null ? null : a);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Attachment>> FindByContentHashAsync(
        TenantId tenantId,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        var rows = _attachments.Values
            .Where(a => a.DeletedAtUtc is null
                && a.TenantId == tenantId
                && string.Equals(a.ContentHash, contentHash, StringComparison.Ordinal))
            .ToList();
        return Task.FromResult<IReadOnlyList<Attachment>>(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Attachment>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var rows = _attachments.Values
            .Where(a => a.DeletedAtUtc is null && a.TenantId == tenantId)
            .ToList();
        return Task.FromResult<IReadOnlyList<Attachment>>(rows);
    }

    /// <inheritdoc />
    public Task<long> GetTenantTotalSizeBytesAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        // Sum only Active rows — Superseded/Tombstoned don't count toward a
        // tenant's current footprint per PR 3's quota gate.
        var total = _attachments.Values
            .Where(a => a.DeletedAtUtc is null
                && a.TenantId == tenantId
                && a.Status == AttachmentStatus.Active)
            .Sum(a => a.SizeBytes);
        return Task.FromResult(total);
    }

    /// <inheritdoc />
    public Task<bool> SoftDeleteAsync(AttachmentId id, string actor, string? reason, CancellationToken cancellationToken = default)
    {
        if (!_attachments.TryGetValue(id, out var a)) return Task.FromResult(false);
        if (a.DeletedAtUtc is not null) return Task.FromResult(true);

        var now = Instant.Now;
        _attachments[id] = a with
        {
            Status = AttachmentStatus.Tombstoned,
            DeletedAtUtc = now,
            DeletedBy = actor,
            DeletedReason = reason,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = a.Version + 1,
        };
        return Task.FromResult(true);
    }
}

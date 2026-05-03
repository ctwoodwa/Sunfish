using Sunfish.Blocks.Leases.Models;

namespace Sunfish.Blocks.Leases.Services;

/// <summary>
/// Append-only log of <see cref="LeaseDocumentVersion"/> entries per
/// W#27 Phase 2 + ADR 0054 amendment A1. Implementations MUST refuse
/// edits to existing entries — every revision is a new entry. Per-lease
/// version numbers are monotonically increasing (first append = 1).
/// </summary>
public interface ILeaseDocumentVersionLog
{
    /// <summary>
    /// Appends a new revision; the implementation assigns a stable id +
    /// the next per-lease version number, returns the persisted entry.
    /// Caller supplies the document hash + blob ref + change summary;
    /// caller does NOT supply <see cref="LeaseDocumentVersion.VersionNumber"/>
    /// or <see cref="LeaseDocumentVersion.Id"/> (overwritten on append).
    /// </summary>
    Task<LeaseDocumentVersion> AppendAsync(LeaseDocumentVersion entry, CancellationToken ct);

    /// <summary>Streams every revision for the supplied lease in version-number order (oldest first).</summary>
    IAsyncEnumerable<LeaseDocumentVersion> ListAsync(LeaseId lease, CancellationToken ct);

    /// <summary>Returns the latest revision for the supplied lease, or null when no revision has been appended.</summary>
    Task<LeaseDocumentVersion?> GetLatestAsync(LeaseId lease, CancellationToken ct);

    /// <summary>Returns the revision with the supplied id, or null when unknown.</summary>
    Task<LeaseDocumentVersion?> GetAsync(LeaseDocumentVersionId id, CancellationToken ct);
}

using System.Collections.Concurrent;

namespace Sunfish.Kernel.SchemaRegistry.Compaction;

/// <summary>
/// Lightweight metadata marking a source log as archived — i.e. superseded by a compacted
/// target. Paper §7.2: <i>"the old stream is archived for deep audit."</i>
/// </summary>
/// <param name="SourceLogName">Archived log name.</param>
/// <param name="TargetLogName">The compacted target that superseded it.</param>
/// <param name="ArchivedAt">When the archive record was written.</param>
/// <param name="TargetSchemaVersion">Schema version the target stream was stamped to.</param>
/// <param name="EventsMigrated">Number of events migrated from source to target at archive time.</param>
public sealed record StreamArchiveRecord(
    string SourceLogName,
    string TargetLogName,
    DateTimeOffset ArchivedAt,
    string TargetSchemaVersion,
    ulong EventsMigrated);

/// <summary>
/// Metadata store that records which source logs have been archived following a
/// compaction run. This is deliberately only <i>metadata</i> — it does not enforce
/// read-only status on the underlying log; that's the storage backend's job. The store
/// exists so auditors can ask "which streams were archived, when, and into what?"
/// without scanning every log.
/// </summary>
public interface IStreamArchive
{
    /// <summary>Record that the log named <paramref name="record"/>.<see cref="StreamArchiveRecord.SourceLogName"/> has been archived into <paramref name="record"/>.<see cref="StreamArchiveRecord.TargetLogName"/>.</summary>
    Task ArchiveAsync(StreamArchiveRecord record, CancellationToken ct);

    /// <summary>Is the named log recorded as archived?</summary>
    Task<bool> IsArchivedAsync(string logName, CancellationToken ct);

    /// <summary>Fetch the archive record for the named log, or <see langword="null"/> if it is not archived.</summary>
    Task<StreamArchiveRecord?> GetArchiveRecordAsync(string logName, CancellationToken ct);

    /// <summary>All archive records, in the order they were added.</summary>
    IReadOnlyList<StreamArchiveRecord> AllArchives { get; }
}

/// <summary>
/// In-memory <see cref="IStreamArchive"/>. Persistence is a follow-up; this implementation
/// is sufficient for local-node tests and the single-process scheduler path.
/// </summary>
public sealed class StreamArchive : IStreamArchive
{
    private readonly ConcurrentDictionary<string, StreamArchiveRecord> _byLogName = new(StringComparer.Ordinal);
    private readonly object _orderGate = new();
    private readonly List<StreamArchiveRecord> _order = new();

    /// <inheritdoc />
    public Task ArchiveAsync(StreamArchiveRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrEmpty(record.SourceLogName);
        ct.ThrowIfCancellationRequested();

        if (_byLogName.TryAdd(record.SourceLogName, record))
        {
            lock (_orderGate)
            {
                _order.Add(record);
            }
        }
        else
        {
            // Overwrite semantics for re-archival of the same log — keep the newest record
            // on the key lookup, but preserve the insertion order of the original record.
            _byLogName[record.SourceLogName] = record;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> IsArchivedAsync(string logName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(logName);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_byLogName.ContainsKey(logName));
    }

    /// <inheritdoc />
    public Task<StreamArchiveRecord?> GetArchiveRecordAsync(string logName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(logName);
        ct.ThrowIfCancellationRequested();
        _byLogName.TryGetValue(logName, out var record);
        return Task.FromResult<StreamArchiveRecord?>(record);
    }

    /// <inheritdoc />
    public IReadOnlyList<StreamArchiveRecord> AllArchives
    {
        get
        {
            lock (_orderGate)
            {
                return _order.ToArray();
            }
        }
    }
}

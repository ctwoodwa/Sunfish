using System.Buffers.Binary;
using System.Formats.Cbor;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Kernel.Events;

/// <summary>
/// Persistent, append-only <see cref="IEventLog"/> backed by the filesystem. Events are framed as
/// a 4-byte big-endian length prefix followed by a CBOR payload; each append is fsync'd for
/// durability (configurable via <see cref="EventLogOptions.FlushIntervalMilliseconds"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>File layout:</b> events are written to <c>events-{EpochId}.log</c> (part 0) with rollover to
/// <c>events-{EpochId}-{part}.log</c> (parts 1..N) once <see cref="EventLogOptions.MaxFileSizeBytes"/>
/// is exceeded. Sequence numbers are monotonic across all parts within an epoch. Snapshots live
/// alongside in <c>snapshot-{aggregateId}-{epochId}-{schemaVersion}-{ticks}.cbor</c>, with the
/// ticks suffix making multiple snapshots for the same tuple non-colliding; <c>ReadLatestSnapshot</c>
/// picks the one with the largest <c>CreatedAt</c>.
/// </para>
/// <para>
/// <b>Framing:</b> each record = <c>[u32-BE length][CBOR bytes]</c>. Length excludes the prefix
/// itself. The CBOR payload is a fixed-shape map — see <see cref="WriteCborEvent"/> /
/// <see cref="ReadCborEvent"/>. A partial tail (length-prefix says N bytes but fewer are present,
/// or the prefix itself is short) is detected on open and truncated to the last known-good
/// boundary; this is the paper §2.5 corruption-resistance guarantee.
/// </para>
/// <para>
/// <b>Concurrency:</b> a single writer <see cref="SemaphoreSlim"/> serializes the append path.
/// Readers open their own <see cref="FileStream"/> in read+share mode per call, so reads never
/// block writes and vice versa (the log is append-only — previously-written bytes are never mutated,
/// so concurrent reads of older data are safe).
/// </para>
/// </remarks>
public sealed class FileBackedEventLog : IEventLog, IDisposable, IAsyncDisposable
{
    private const int LengthPrefixBytes = 4;

    // CBOR shape keys. Short strings — deliberately compact so the log stays small. The payload
    // map is deterministic-sized (see WriteCborEvent).
    private const string KeyEventId = "id";
    private const string KeyEntityId = "e";
    private const string KeyKind = "k";
    private const string KeyOccurredAt = "t";
    private const string KeyPayload = "p";

    private const string SnapshotKeyAggregate = "agg";
    private const string SnapshotKeyEpoch = "epoch";
    private const string SnapshotKeySchema = "schema";
    private const string SnapshotKeySeq = "seq";
    private const string SnapshotKeyPayload = "payload";
    private const string SnapshotKeyCreatedAt = "createdAt";

    private readonly EventLogOptions _options;
    private readonly ILogger<FileBackedEventLog> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // The currently-active write file. Opened lazily on the first Append and on rollover.
    private FileStream? _writeStream;
    private long _writeStreamLength;
    private int _currentPart;
    private ulong _currentSequence;
    private bool _disposed;

    // Periodic flush machinery — only enabled when FlushIntervalMilliseconds > 0.
    private readonly CancellationTokenSource? _flushCts;
    private readonly Task? _flushLoop;
    private volatile bool _dirty;

    /// <summary>Creates a new file-backed event log using the supplied options.</summary>
    public FileBackedEventLog(IOptions<EventLogOptions> options, ILogger<FileBackedEventLog>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<FileBackedEventLog>.Instance;

        Directory.CreateDirectory(_options.Directory);
        RecoverOnOpen();

        if (_options.FlushIntervalMilliseconds > 0)
        {
            _flushCts = new CancellationTokenSource();
            _flushLoop = Task.Run(() => FlushLoopAsync(_flushCts.Token));
        }
    }

    /// <inheritdoc />
    public ulong CurrentSequence => Volatile.Read(ref _currentSequence);

    /// <inheritdoc />
    public async Task<ulong> AppendAsync(KernelEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ct.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureWriteStream();

            // Rollover if this append would exceed the cap. We evaluate the projected size after
            // encoding so the rollover decision uses real bytes, not an estimate.
            var payload = WriteCborEvent(evt);
            var total = LengthPrefixBytes + payload.Length;
            if (_writeStreamLength > 0 && _writeStreamLength + total > _options.MaxFileSizeBytes)
            {
                RollOver();
                EnsureWriteStream();
            }

            Span<byte> prefix = stackalloc byte[LengthPrefixBytes];
            BinaryPrimitives.WriteUInt32BigEndian(prefix, (uint)payload.Length);

            await _writeStream!.WriteAsync(prefix.ToArray().AsMemory(), ct).ConfigureAwait(false);
            await _writeStream.WriteAsync(payload, ct).ConfigureAwait(false);
            _writeStreamLength += total;

            var nextSeq = _currentSequence + 1;
            Volatile.Write(ref _currentSequence, nextSeq);

            if (_options.FlushIntervalMilliseconds == 0)
            {
                // Synchronous durability: push all the way to disk. FlushAsync(flushToDisk:true)
                // maps to fsync on POSIX and FlushFileBuffers on Windows.
                await _writeStream.FlushAsync(ct).ConfigureAwait(false);
                _writeStream.Flush(flushToDisk: true);
            }
            else
            {
                _dirty = true;
            }

            return nextSeq;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<LogEntry> ReadAfterAsync(ulong afterSeq, CancellationToken ct)
        => ReadFilteredAsync(entry => entry.Sequence > afterSeq, _ => false, ct);

    /// <inheritdoc />
    public IAsyncEnumerable<LogEntry> ReadRangeAsync(ulong fromSeq, ulong toSeqInclusive, CancellationToken ct)
        => ReadFilteredAsync(entry => entry.Sequence >= fromSeq && entry.Sequence <= toSeqInclusive,
                             entry => entry.Sequence > toSeqInclusive, ct);

    private async IAsyncEnumerable<LogEntry> ReadFilteredAsync(
        Func<LogEntry, bool> include,
        Func<LogEntry, bool> stop,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (var file in EnumerateLogFilesInOrder())
        {
            // Open a fresh read handle per file. FileShare.ReadWrite lets the writer continue
            // appending while we stream through the file contents.
            using var fs = new FileStream(
                file,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.ReadWrite | FileShare.Delete,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                });

            ulong seq = FileStartSequence(file);

            var prefixBuffer = new byte[LengthPrefixBytes];
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var prefixRead = await ReadExactAsync(fs, prefixBuffer, ct).ConfigureAwait(false);
                if (prefixRead == 0)
                {
                    break; // clean EOF
                }
                if (prefixRead < LengthPrefixBytes)
                {
                    // Torn prefix at tail — recovery would truncate. On the read path we stop
                    // cleanly; a subsequent Append will fix the file via RecoverOnOpen on the next
                    // process start.
                    break;
                }

                var payloadLen = BinaryPrimitives.ReadUInt32BigEndian(prefixBuffer);
                if (payloadLen == 0 || payloadLen > _options.MaxFileSizeBytes)
                {
                    break;
                }

                var payload = new byte[payloadLen];
                var read = await ReadExactAsync(fs, payload, ct).ConfigureAwait(false);
                if (read < payloadLen)
                {
                    break; // torn payload
                }

                KernelEvent evt;
                try
                {
                    evt = ReadCborEvent(payload);
                }
                catch (CborContentException)
                {
                    break;
                }

                var entry = new LogEntry(seq, evt);
                seq++;
                if (include(entry))
                {
                    yield return entry;
                }
                if (stop(entry))
                {
                    yield break;
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task WriteSnapshotAsync(Snapshot snapshot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ct.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var bytes = WriteCborSnapshot(snapshot);
        var filename = SnapshotFileName(snapshot.AggregateId, snapshot.EpochId, snapshot.SchemaVersion, snapshot.CreatedAt);
        var path = Path.Combine(_options.Directory, filename);

        // Write to a temp and rename so readers never observe a partial snapshot.
        var tempPath = path + ".tmp";
        await File.WriteAllBytesAsync(tempPath, bytes, ct).ConfigureAwait(false);

        // If a file with the exact same name already exists (same ticks — rare) replace it.
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        File.Move(tempPath, path);
    }

    /// <inheritdoc />
    public Task<Snapshot?> ReadLatestSnapshotAsync(string aggregateId, string epochId, string schemaVersion, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(aggregateId);
        ArgumentException.ThrowIfNullOrEmpty(epochId);
        ArgumentException.ThrowIfNullOrEmpty(schemaVersion);
        ct.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var prefix = SnapshotFilePrefix(aggregateId, epochId, schemaVersion);
        string[] candidates;
        try
        {
            candidates = Directory.GetFiles(_options.Directory, prefix + "*.cbor");
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<Snapshot?>(null);
        }

        Snapshot? latest = null;
        foreach (var file in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var bytes = File.ReadAllBytes(file);
                var snap = ReadCborSnapshot(bytes);
                if (latest is null || snap.CreatedAt > latest.CreatedAt)
                {
                    latest = snap;
                }
            }
            catch (Exception ex) when (ex is IOException or CborContentException)
            {
                _logger.LogWarning(ex, "Skipping corrupted snapshot file {File}.", file);
            }
        }
        return Task.FromResult(latest);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeCoreAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => DisposeCoreAsync();

    private async ValueTask DisposeCoreAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_flushCts is not null)
        {
            _flushCts.Cancel();
            try
            {
                if (_flushLoop is not null) await _flushLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            _flushCts.Dispose();
        }

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_writeStream is not null)
            {
                await _writeStream.FlushAsync().ConfigureAwait(false);
                _writeStream.Flush(flushToDisk: true);
                await _writeStream.DisposeAsync().ConfigureAwait(false);
                _writeStream = null;
            }
        }
        finally
        {
            _writeLock.Release();
        }
        _writeLock.Dispose();
    }

    // ---- internals ----

    private async Task FlushLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.FlushIntervalMilliseconds, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }

            if (!_dirty) continue;
            await _writeLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_writeStream is not null && _dirty)
                {
                    await _writeStream.FlushAsync(ct).ConfigureAwait(false);
                    _writeStream.Flush(flushToDisk: true);
                    _dirty = false;
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }

    private void EnsureWriteStream()
    {
        if (_writeStream is not null) return;

        var path = Path.Combine(_options.Directory, LogFileName(_currentPart));
        _writeStream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.Write,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });
        _writeStream.Seek(0, SeekOrigin.End);
        _writeStreamLength = _writeStream.Length;
    }

    private void RollOver()
    {
        if (_writeStream is not null)
        {
            _writeStream.Flush(flushToDisk: true);
            _writeStream.Dispose();
            _writeStream = null;
        }
        _currentPart++;
        _writeStreamLength = 0;
    }

    /// <summary>
    /// Scans every log file in the epoch, truncating any torn tail, and advances
    /// <see cref="_currentSequence"/> / <see cref="_currentPart"/> to the resume position.
    /// </summary>
    private void RecoverOnOpen()
    {
        var files = EnumerateLogFilesInOrder().ToArray();
        ulong seq = 0;
        int lastPart = 0;
        long lastGoodLength = 0;

        foreach (var file in files)
        {
            lastPart = FilePartFromPath(file);
            using var fs = new FileStream(
                file,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.None,
                });

            var prefixBuffer = new byte[LengthPrefixBytes];
            long goodUpTo = 0;
            while (true)
            {
                var posBefore = fs.Position;
                var prefixRead = ReadExactSync(fs, prefixBuffer);
                if (prefixRead == 0)
                {
                    break; // clean EOF
                }
                if (prefixRead < LengthPrefixBytes)
                {
                    // Torn prefix. Truncate back to the last good boundary.
                    fs.Position = posBefore;
                    break;
                }

                var payloadLen = BinaryPrimitives.ReadUInt32BigEndian(prefixBuffer);
                if (payloadLen == 0 || payloadLen > _options.MaxFileSizeBytes)
                {
                    fs.Position = posBefore;
                    break;
                }

                var payload = new byte[payloadLen];
                var read = ReadExactSync(fs, payload);
                if (read < payloadLen)
                {
                    fs.Position = posBefore;
                    break;
                }

                try
                {
                    _ = ReadCborEvent(payload);
                }
                catch (CborContentException)
                {
                    fs.Position = posBefore;
                    break;
                }

                goodUpTo = fs.Position;
                seq++;
            }

            lastGoodLength = goodUpTo;

            // Truncate if there was a torn tail. Capture length *before* closing the file handle —
            // FileStream.Length throws ObjectDisposedException after Close.
            var physicalLength = fs.Length;
            if (physicalLength > goodUpTo)
            {
                fs.Close();
                using var truncate = new FileStream(
                    file, FileMode.Open, FileAccess.Write, FileShare.None);
                truncate.SetLength(goodUpTo);
                truncate.Flush(flushToDisk: true);
                _logger.LogWarning(
                    "Recovered {File}: truncated {RemovedBytes} torn bytes.", file, physicalLength - goodUpTo);
            }
        }

        _currentSequence = seq;
        _currentPart = lastPart;
        _writeStreamLength = lastGoodLength;
    }

    private IEnumerable<string> EnumerateLogFilesInOrder()
    {
        if (!Directory.Exists(_options.Directory)) yield break;
        var files = Directory.GetFiles(_options.Directory, $"events-{_options.EpochId}*.log");
        Array.Sort(files, (a, b) => FilePartFromPath(a).CompareTo(FilePartFromPath(b)));
        foreach (var f in files) yield return f;
    }

    private string LogFileName(int part)
        => part == 0 ? $"events-{_options.EpochId}.log" : $"events-{_options.EpochId}-{part}.log";

    private int FilePartFromPath(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path); // e.g. events-epoch-0 or events-epoch-0-3
        var prefix = $"events-{_options.EpochId}";
        if (!name.StartsWith(prefix, StringComparison.Ordinal)) return 0;
        var tail = name[prefix.Length..];
        if (tail.Length == 0) return 0;
        if (tail[0] != '-') return 0;
        return int.TryParse(tail[1..], out var part) ? part : 0;
    }

    /// <summary>Returns the sequence number of the first event in <paramref name="file"/>.</summary>
    private ulong FileStartSequence(string file)
    {
        // Walk prior parts in order, counting frames, to derive the first-sequence of this file.
        var targetPart = FilePartFromPath(file);
        ulong seq = 1;
        foreach (var f in EnumerateLogFilesInOrder())
        {
            var p = FilePartFromPath(f);
            if (p >= targetPart) break;
            seq += CountFramesInFile(f);
        }
        return seq;
    }

    private ulong CountFramesInFile(string file)
    {
        using var fs = new FileStream(
            file,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.SequentialScan,
            });

        ulong count = 0;
        var prefixBuffer = new byte[LengthPrefixBytes];
        while (true)
        {
            var prefixRead = ReadExactSync(fs, prefixBuffer);
            if (prefixRead < LengthPrefixBytes) break;
            var payloadLen = BinaryPrimitives.ReadUInt32BigEndian(prefixBuffer);
            if (payloadLen == 0 || payloadLen > _options.MaxFileSizeBytes) break;
            if (fs.Length - fs.Position < payloadLen) break;
            fs.Seek(payloadLen, SeekOrigin.Current);
            count++;
        }
        return count;
    }

    private static int ReadExactSync(Stream s, byte[] buf)
    {
        var total = 0;
        while (total < buf.Length)
        {
            var r = s.Read(buf, total, buf.Length - total);
            if (r <= 0) break;
            total += r;
        }
        return total;
    }

    private static async Task<int> ReadExactAsync(Stream s, byte[] buf, CancellationToken ct)
    {
        var total = 0;
        while (total < buf.Length)
        {
            var r = await s.ReadAsync(buf.AsMemory(total, buf.Length - total), ct).ConfigureAwait(false);
            if (r <= 0) break;
            total += r;
        }
        return total;
    }

    private static string SnapshotFilePrefix(string aggregateId, string epochId, string schemaVersion)
        => $"snapshot-{Sanitize(aggregateId)}-{Sanitize(epochId)}-{Sanitize(schemaVersion)}-";

    private static string SnapshotFileName(string aggregateId, string epochId, string schemaVersion, DateTimeOffset createdAt)
        => SnapshotFilePrefix(aggregateId, epochId, schemaVersion) + createdAt.UtcTicks.ToString("D20") + ".cbor";

    private static string Sanitize(string value)
    {
        // Replace filesystem-problematic characters with '_' so aggregate ids like "property:acme/1"
        // produce valid file names on both Windows and POSIX.
        Span<char> invalid = stackalloc char[]
        {
            '/', '\\', ':', '*', '?', '"', '<', '>', '|', ' ', '\t',
        };
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            foreach (var c in invalid)
            {
                if (chars[i] == c) { chars[i] = '_'; break; }
            }
        }
        return new string(chars);
    }

    // ---- CBOR encode / decode ----

    private static byte[] WriteCborEvent(KernelEvent evt)
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartMap(5);

        writer.WriteTextString(KeyEventId);
        writer.WriteTextString(evt.Id.Value.ToString("D"));

        writer.WriteTextString(KeyEntityId);
        writer.WriteTextString(evt.EntityId.ToString());

        writer.WriteTextString(KeyKind);
        writer.WriteTextString(evt.Kind);

        writer.WriteTextString(KeyOccurredAt);
        writer.WriteInt64(evt.OccurredAt.UtcTicks);

        writer.WriteTextString(KeyPayload);
        WritePayload(writer, evt.Payload);

        writer.WriteEndMap();
        return writer.Encode();
    }

    private static KernelEvent ReadCborEvent(byte[] bytes)
    {
        var reader = new CborReader(bytes, CborConformanceMode.Strict);
        var mapLen = reader.ReadStartMap();
        if (mapLen is not 5)
        {
            throw new CborContentException("Expected a 5-entry event map.");
        }

        Guid? id = null;
        EntityId? entity = null;
        string? kind = null;
        long? occurredAtTicks = null;
        Dictionary<string, object?>? payload = null;

        for (var i = 0; i < 5; i++)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case KeyEventId:
                    id = Guid.Parse(reader.ReadTextString());
                    break;
                case KeyEntityId:
                    entity = EntityId.Parse(reader.ReadTextString());
                    break;
                case KeyKind:
                    kind = reader.ReadTextString();
                    break;
                case KeyOccurredAt:
                    occurredAtTicks = reader.ReadInt64();
                    break;
                case KeyPayload:
                    payload = ReadPayloadMap(reader);
                    break;
                default:
                    throw new CborContentException($"Unknown event field '{key}'.");
            }
        }
        reader.ReadEndMap();

        if (id is null || entity is null || kind is null || occurredAtTicks is null || payload is null)
        {
            throw new CborContentException("Event missing required fields.");
        }

        var occurredAt = new DateTimeOffset(occurredAtTicks.Value, TimeSpan.Zero);
        return new KernelEvent(new EventId(id.Value), entity.Value, kind, occurredAt, payload);
    }

    private static void WritePayload(CborWriter writer, IReadOnlyDictionary<string, object?> payload)
    {
        writer.WriteStartMap(payload.Count);
        foreach (var (k, v) in payload)
        {
            writer.WriteTextString(k);
            WriteValue(writer, v);
        }
        writer.WriteEndMap();
    }

    private static Dictionary<string, object?> ReadPayloadMap(CborReader reader)
    {
        var len = reader.ReadStartMap();
        var dict = new Dictionary<string, object?>(len ?? 0);
        var count = len ?? 0;
        for (var i = 0; i < count; i++)
        {
            var k = reader.ReadTextString();
            dict[k] = ReadValue(reader);
        }
        reader.ReadEndMap();
        return dict;
    }

    private static void WriteValue(CborWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNull();
                break;
            case string s:
                writer.WriteTextString(s);
                break;
            case bool b:
                writer.WriteBoolean(b);
                break;
            case int i:
                writer.WriteInt32(i);
                break;
            case long l:
                writer.WriteInt64(l);
                break;
            case ulong ul:
                writer.WriteUInt64(ul);
                break;
            case double d:
                writer.WriteDouble(d);
                break;
            case float f:
                writer.WriteSingle(f);
                break;
            case byte[] bytes:
                writer.WriteByteString(bytes);
                break;
            case DateTimeOffset dto:
                // Encode as a tagged int64 ticks to preserve precision.
                writer.WriteTag(CborTag.DateTimeString);
                writer.WriteTextString(dto.UtcDateTime.ToString("O"));
                break;
            default:
                // Fallback: stringify. Consumers that need richer types should use a typed
                // codec; the log doesn't attempt to round-trip arbitrary CLR graphs.
                writer.WriteTextString(value.ToString() ?? "");
                break;
        }
    }

    private static object? ReadValue(CborReader reader)
    {
        var state = reader.PeekState();
        switch (state)
        {
            case CborReaderState.Null:
                reader.ReadNull();
                return null;
            case CborReaderState.TextString:
                return reader.ReadTextString();
            case CborReaderState.Boolean:
                return reader.ReadBoolean();
            case CborReaderState.UnsignedInteger:
                {
                    var v = reader.ReadUInt64();
                    if (v <= int.MaxValue) return (int)v;
                    if (v <= long.MaxValue) return (long)v;
                    return v;
                }
            case CborReaderState.NegativeInteger:
                {
                    var v = reader.ReadInt64();
                    if (v >= int.MinValue && v <= int.MaxValue) return (int)v;
                    return v;
                }
            case CborReaderState.DoublePrecisionFloat:
                return reader.ReadDouble();
            case CborReaderState.SinglePrecisionFloat:
                return reader.ReadSingle();
            case CborReaderState.ByteString:
                return reader.ReadByteString();
            case CborReaderState.Tag:
                {
                    var tag = reader.ReadTag();
                    if (tag == CborTag.DateTimeString)
                    {
                        var s = reader.ReadTextString();
                        return DateTimeOffset.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                    }
                    // Unknown tag — read the tagged value as an opaque string.
                    return ReadValue(reader);
                }
            default:
                throw new CborContentException($"Unsupported CBOR state {state} in payload.");
        }
    }

    private static byte[] WriteCborSnapshot(Snapshot snap)
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartMap(6);

        writer.WriteTextString(SnapshotKeyAggregate);
        writer.WriteTextString(snap.AggregateId);

        writer.WriteTextString(SnapshotKeyEpoch);
        writer.WriteTextString(snap.EpochId);

        writer.WriteTextString(SnapshotKeySchema);
        writer.WriteTextString(snap.SchemaVersion);

        writer.WriteTextString(SnapshotKeySeq);
        writer.WriteUInt64(snap.LastEventSeq);

        writer.WriteTextString(SnapshotKeyPayload);
        writer.WriteByteString(snap.Payload);

        writer.WriteTextString(SnapshotKeyCreatedAt);
        writer.WriteInt64(snap.CreatedAt.UtcTicks);

        writer.WriteEndMap();
        return writer.Encode();
    }

    private static Snapshot ReadCborSnapshot(byte[] bytes)
    {
        var reader = new CborReader(bytes, CborConformanceMode.Strict);
        var len = reader.ReadStartMap();
        if (len is not 6)
        {
            throw new CborContentException("Expected a 6-entry snapshot map.");
        }

        string? agg = null;
        string? epoch = null;
        string? schema = null;
        ulong? seq = null;
        byte[]? payload = null;
        long? createdTicks = null;

        for (var i = 0; i < 6; i++)
        {
            var k = reader.ReadTextString();
            switch (k)
            {
                case SnapshotKeyAggregate: agg = reader.ReadTextString(); break;
                case SnapshotKeyEpoch: epoch = reader.ReadTextString(); break;
                case SnapshotKeySchema: schema = reader.ReadTextString(); break;
                case SnapshotKeySeq: seq = reader.ReadUInt64(); break;
                case SnapshotKeyPayload: payload = reader.ReadByteString(); break;
                case SnapshotKeyCreatedAt: createdTicks = reader.ReadInt64(); break;
                default: throw new CborContentException($"Unknown snapshot field '{k}'.");
            }
        }
        reader.ReadEndMap();

        if (agg is null || epoch is null || schema is null || seq is null || payload is null || createdTicks is null)
        {
            throw new CborContentException("Snapshot missing required fields.");
        }

        return new Snapshot(agg, epoch, schema, seq.Value, payload, new DateTimeOffset(createdTicks.Value, TimeSpan.Zero));
    }
}

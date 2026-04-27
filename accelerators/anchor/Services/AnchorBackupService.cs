using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sunfish.Kernel.Crdt;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Phase 1 G5 — Anchor backup orchestration. Exports the active team's
/// CRDT state to a portable ZIP blob containing a JSON manifest and the
/// raw CRDT snapshot bytes; imports restore the snapshot into the target
/// document. Closes Kleppmann property P5 (long-now) + P7 (ownership) on
/// the data-portability surface — the user owns a self-contained file
/// they can move between devices, archive on external media, or restore
/// after a fresh install.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> Phase 1 G5 first deliverable. The plan task list also
/// names: SQLCipher <c>VACUUM INTO</c> snapshot, signed audit-log entry
/// per backup (primitive #48f), and the Razor UI page. Those land in
/// follow-ups: VACUUM INTO requires DB-layer integration the current
/// contracts don't expose; the audit-log surface is shared with G6
/// recovery-event audit; the UI page is a Stage 06 deliverable. The
/// service surface here is the foundation those follow-ups extend.
/// </para>
/// <para>
/// <b>Format.</b> ZIP archive (<c>System.IO.Compression.ZipArchive</c>,
/// no native deps). Two entries:
/// <list type="bullet">
///   <item><c>manifest.json</c> — <see cref="BackupManifest"/> JSON</item>
///   <item><c>crdt-snapshot.bin</c> — raw bytes from
///     <see cref="ICrdtDocument.ToSnapshot"/></item>
/// </list>
/// The snapshot is opaque to this service; the engine
/// (<see cref="ICrdtEngine"/>) chooses its serialization format. ADR 0028's
/// engine-swappability invariant therefore extends to backups — same
/// engine on both sides round-trips cleanly; mismatch surfaces at
/// <see cref="ICrdtDocument.ApplySnapshot"/> as an exception caught by
/// the caller (the eventual UI page reports the failure to the user).
/// </para>
/// <para>
/// <b>Import semantics.</b> <see cref="ImportAsync"/> verifies the
/// manifest version is recognized and the manifest's <see cref="BackupManifest.DocumentId"/>
/// matches the target document's <see cref="ICrdtDocument.DocumentId"/>;
/// either mismatch throws <see cref="InvalidDataException"/> before any
/// state mutation. CRDT semantics make
/// <see cref="ICrdtDocument.ApplySnapshot"/> idempotent (applying the
/// same snapshot twice is a no-op) and commutative (existing local
/// mutations merge with backed-up state), so import is safe to run
/// against a non-empty target — useful for "restore over partial state"
/// flows the recovery UX (G6) will eventually expose.
/// </para>
/// </remarks>
public sealed class AnchorBackupService
{
    private readonly ICrdtDocument _document;
    private readonly ILogger<AnchorBackupService> _logger;

    /// <summary>Construct the backup service over a single CRDT document.</summary>
    public AnchorBackupService(ICrdtDocument document, ILogger<AnchorBackupService> logger)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Export the document's current snapshot into a ZIP blob written to
    /// <paramref name="destination"/>. The caller owns the stream's
    /// position and lifetime; this method only writes from the current
    /// position onward.
    /// </summary>
    public async Task ExportAsync(Stream destination, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var snapshot = _document.ToSnapshot();
        var manifest = new BackupManifest(
            Version: BackupManifest.CurrentVersion,
            DocumentId: _document.DocumentId,
            ExportedAt: DateTimeOffset.UtcNow,
            SnapshotSize: snapshot.Length);

        // ZipArchive in Create mode flushes its central directory on Dispose,
        // so we leave the underlying stream open for the caller. The using-block
        // here scopes the archive's lifetime; leaveOpen:true keeps the caller's
        // stream usable after we return.
        using (var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry(BackupManifest.ManifestEntryName, CompressionLevel.Optimal);
            await using (var entryStream = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(entryStream, manifest, BackupJsonOptions, ct).ConfigureAwait(false);
            }

            var snapshotEntry = archive.CreateEntry(BackupManifest.SnapshotEntryName, CompressionLevel.Optimal);
            await using (var entryStream = snapshotEntry.Open())
            {
                await entryStream.WriteAsync(snapshot, ct).ConfigureAwait(false);
            }
        }

        _logger.LogInformation(
            "Anchor backup exported: documentId={DocumentId} snapshotBytes={SnapshotBytes}",
            _document.DocumentId, snapshot.Length);
    }

    /// <summary>
    /// Read a backup blob from <paramref name="source"/> and apply its
    /// CRDT snapshot to the target document. Throws
    /// <see cref="InvalidDataException"/> for malformed blobs or
    /// document-id mismatch before any state mutation.
    /// </summary>
    public async Task ImportAsync(Stream source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        var manifestEntry = archive.GetEntry(BackupManifest.ManifestEntryName)
            ?? throw new InvalidDataException(
                $"Backup blob is missing the required '{BackupManifest.ManifestEntryName}' entry.");

        BackupManifest? manifest;
        await using (var manifestStream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(
                manifestStream, BackupJsonOptions, ct).ConfigureAwait(false);
        }
        if (manifest is null)
        {
            throw new InvalidDataException("Backup manifest deserialized as null.");
        }
        if (manifest.Version != BackupManifest.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Backup manifest version '{manifest.Version}' is not supported by this Anchor build " +
                $"(expected '{BackupManifest.CurrentVersion}').");
        }
        if (!string.Equals(manifest.DocumentId, _document.DocumentId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Backup is for document '{manifest.DocumentId}' but target document is " +
                $"'{_document.DocumentId}'. Refusing to apply across document boundaries.");
        }

        var snapshotEntry = archive.GetEntry(BackupManifest.SnapshotEntryName)
            ?? throw new InvalidDataException(
                $"Backup blob is missing the required '{BackupManifest.SnapshotEntryName}' entry.");

        byte[] snapshot;
        await using (var snapshotStream = snapshotEntry.Open())
        await using (var memory = new MemoryStream(capacity: (int)Math.Max(0, manifest.SnapshotSize)))
        {
            await snapshotStream.CopyToAsync(memory, ct).ConfigureAwait(false);
            snapshot = memory.ToArray();
        }

        _document.ApplySnapshot(snapshot);

        _logger.LogInformation(
            "Anchor backup imported: documentId={DocumentId} exportedAt={ExportedAt:O} snapshotBytes={SnapshotBytes}",
            manifest.DocumentId, manifest.ExportedAt, snapshot.Length);
    }

    private static readonly JsonSerializerOptions BackupJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

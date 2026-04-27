using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

using Sunfish.Anchor.Services;
using Sunfish.Kernel.Crdt;
using Sunfish.Kernel.Crdt.Backends;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// Phase 1 G5 — coverage for <see cref="AnchorBackupService"/>. Verifies
/// the round-trip (export → fresh document → import → projection match)
/// the Stage 05 plan calls out as the G5 acceptance test, plus the
/// defensive checks that prevent cross-document and cross-version blobs
/// from corrupting local state.
/// </summary>
public sealed class AnchorBackupServiceTests
{
    [Fact]
    public async Task ExportImport_round_trip_preserves_50_text_operations()
    {
        // Acceptance criterion from icm/05_implementation-plan/output/business-mvp-phase-1-plan-2026-04-26.md
        // §G5 — write 50 CRDT operations, export, delete (use a fresh doc), import,
        // verify all 50 operations present + projections match byte-for-byte.
        var engine = new StubCrdtEngine();
        await using var sourceDoc = engine.CreateDocument("default");

        var sourceText = sourceDoc.GetText("notes");
        for (var i = 0; i < 50; i++)
        {
            sourceText.Insert(i, "x");
        }
        Assert.Equal(50, sourceText.Value.Length);

        await using var blob = new MemoryStream();
        var exporter = new AnchorBackupService(sourceDoc, NullLogger<AnchorBackupService>.Instance);
        await exporter.ExportAsync(blob);
        Assert.True(blob.Length > 0);
        blob.Position = 0;

        // "Delete the team / fresh Anchor" — same engine, fresh empty doc.
        await using var restoredDoc = engine.CreateDocument("default");
        Assert.Equal(string.Empty, restoredDoc.GetText("notes").Value);

        var importer = new AnchorBackupService(restoredDoc, NullLogger<AnchorBackupService>.Instance);
        await importer.ImportAsync(blob);

        Assert.Equal(sourceText.Value, restoredDoc.GetText("notes").Value);
        Assert.Equal(50, restoredDoc.GetText("notes").Value.Length);
    }

    [Fact]
    public async Task ExportAsync_writes_zip_with_manifest_and_snapshot_entries()
    {
        var engine = new StubCrdtEngine();
        await using var doc = engine.CreateDocument("default");
        doc.GetText("greeting").Insert(0, "hello");

        await using var blob = new MemoryStream();
        var svc = new AnchorBackupService(doc, NullLogger<AnchorBackupService>.Instance);
        await svc.ExportAsync(blob);
        blob.Position = 0;

        using var archive = new ZipArchive(blob, ZipArchiveMode.Read, leaveOpen: true);
        Assert.NotNull(archive.GetEntry(BackupManifest.ManifestEntryName));
        Assert.NotNull(archive.GetEntry(BackupManifest.SnapshotEntryName));

        var manifestEntry = archive.GetEntry(BackupManifest.ManifestEntryName)!;
        await using var manifestStream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(
            manifestStream,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.NotNull(manifest);
        Assert.Equal(BackupManifest.CurrentVersion, manifest!.Version);
        Assert.Equal("default", manifest.DocumentId);
        Assert.True(manifest.SnapshotSize > 0);
        Assert.True(manifest.ExportedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ImportAsync_throws_when_blob_is_missing_manifest()
    {
        var engine = new StubCrdtEngine();
        await using var doc = engine.CreateDocument("default");

        await using var blob = new MemoryStream();
        using (var archive = new ZipArchive(blob, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Snapshot entry only — no manifest.
            var snapshotEntry = archive.CreateEntry(BackupManifest.SnapshotEntryName);
            await using var s = snapshotEntry.Open();
            await s.WriteAsync(new byte[] { 0, 1, 2 });
        }
        blob.Position = 0;

        var svc = new AnchorBackupService(doc, NullLogger<AnchorBackupService>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => svc.ImportAsync(blob));
        Assert.Contains("manifest.json", ex.Message);
    }

    [Fact]
    public async Task ImportAsync_throws_when_manifest_version_is_unknown()
    {
        var engine = new StubCrdtEngine();
        await using var doc = engine.CreateDocument("default");

        // Build a blob with a future version manifest.
        var rogueManifest = new BackupManifest(
            Version: "99.0",
            DocumentId: "default",
            ExportedAt: DateTimeOffset.UtcNow,
            SnapshotSize: 0);

        await using var blob = new MemoryStream();
        using (var archive = new ZipArchive(blob, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry(BackupManifest.ManifestEntryName);
            await using (var s = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(s, rogueManifest,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            archive.CreateEntry(BackupManifest.SnapshotEntryName); // empty
        }
        blob.Position = 0;

        var svc = new AnchorBackupService(doc, NullLogger<AnchorBackupService>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => svc.ImportAsync(blob));
        Assert.Contains("99.0", ex.Message);
    }

    [Fact]
    public async Task ImportAsync_throws_when_manifest_documentId_does_not_match_target()
    {
        // Defense in depth: a blob exported from team A's document must not be
        // applied to team B's document silently. The service compares
        // manifest.DocumentId to ICrdtDocument.DocumentId before mutating state.
        var engine = new StubCrdtEngine();
        await using var sourceDoc = engine.CreateDocument("team-alpha-default");
        sourceDoc.GetText("notes").Insert(0, "alpha state");

        await using var blob = new MemoryStream();
        await new AnchorBackupService(sourceDoc, NullLogger<AnchorBackupService>.Instance)
            .ExportAsync(blob);
        blob.Position = 0;

        // Different document id — import must refuse.
        await using var targetDoc = engine.CreateDocument("team-beta-default");
        var svc = new AnchorBackupService(targetDoc, NullLogger<AnchorBackupService>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => svc.ImportAsync(blob));
        Assert.Contains("team-alpha-default", ex.Message);
        Assert.Contains("team-beta-default", ex.Message);

        // And the target document remains untouched.
        Assert.Equal(string.Empty, targetDoc.GetText("notes").Value);
    }

    [Fact]
    public async Task ImportAsync_into_existing_document_merges_state_idempotently()
    {
        // CRDT semantics make ApplySnapshot idempotent and commutative — the
        // snapshot bytes carry full causal history, so applying the same backup
        // twice should not double the state.
        var engine = new StubCrdtEngine();
        await using var sourceDoc = engine.CreateDocument("default");
        sourceDoc.GetText("notes").Insert(0, "abc");

        await using var blob = new MemoryStream();
        await new AnchorBackupService(sourceDoc, NullLogger<AnchorBackupService>.Instance)
            .ExportAsync(blob);

        await using var target = engine.CreateDocument("default");
        var svc = new AnchorBackupService(target, NullLogger<AnchorBackupService>.Instance);

        blob.Position = 0;
        await svc.ImportAsync(blob);

        blob.Position = 0;
        await svc.ImportAsync(blob);

        Assert.Equal("abc", target.GetText("notes").Value);
    }
}

using System.Text.Json.Serialization;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Phase 1 G5 — manifest envelope for an Anchor backup blob. Lives at the
/// root of the ZIP container as <c>manifest.json</c>; describes the format
/// version, the source document, and when the backup was taken so import
/// can verify compatibility before touching local state.
/// </summary>
/// <remarks>
/// <para>
/// Phase 1 single-document convention (see
/// <c>accelerators/anchor/Services/AnchorCrdtDeltaBridge.cs</c>): Anchor
/// ships one logical document per team with id <c>"default"</c>.
/// <see cref="DocumentId"/> records which document the backup covers; on
/// import the manifest's <see cref="DocumentId"/> must match the target
/// <c>ICrdtDocument.DocumentId</c> so a blob exported from team A doesn't
/// silently overwrite team B's state.
/// </para>
/// <para>
/// <see cref="Version"/> is the backup-format version, not the schema-epoch
/// of the inner CRDT data. Phase 1 ships <c>"1.0"</c>; future format
/// changes (added entries, alternative compression) bump this.
/// </para>
/// <para>
/// <see cref="ExportedAt"/> is informational — used by the eventual UI to
/// label backup files. <see cref="SnapshotSize"/> is the byte length of
/// the CRDT snapshot inside the ZIP, retained for sanity-checking the
/// blob during import.
/// </para>
/// </remarks>
public sealed record BackupManifest(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("documentId")] string DocumentId,
    [property: JsonPropertyName("exportedAt")] DateTimeOffset ExportedAt,
    [property: JsonPropertyName("snapshotSize")] long SnapshotSize)
{
    /// <summary>The current backup-format version emitted by
    /// <see cref="AnchorBackupService.ExportAsync"/>.</summary>
    public const string CurrentVersion = "1.0";

    /// <summary>Filename of the manifest entry inside the ZIP container.</summary>
    public const string ManifestEntryName = "manifest.json";

    /// <summary>Filename of the CRDT-snapshot entry inside the ZIP container.</summary>
    public const string SnapshotEntryName = "crdt-snapshot.bin";
}

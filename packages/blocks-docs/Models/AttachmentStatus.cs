using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Docs.Models;

/// <summary>
/// Attachment lifecycle state. Attachments are immutable post-upload —
/// a "new version" is a fresh <see cref="Attachment"/> row with
/// <see cref="Attachment.ReplacesAttachmentId"/> pointing at the prior
/// row, which transitions to <see cref="Superseded"/>. Tombstoning
/// erases the catalog row but the underlying blob may be pinned by
/// other references; PR 6's reconciler handles blob GC.
/// </summary>
[JsonConverter(typeof(AttachmentStatusJsonConverter))]
public enum AttachmentStatus
{
    /// <summary>Live attachment — fetchable, referencable.</summary>
    Active,

    /// <summary>Replaced by a newer version of the same logical document; still referenceable for audit.</summary>
    Superseded,

    /// <summary>Tombstoned — catalog row marked deleted. Blob may persist if pinned elsewhere.</summary>
    Tombstoned,
}

internal sealed class AttachmentStatusJsonConverter : JsonConverter<AttachmentStatus>
{
    public override AttachmentStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "active"      => AttachmentStatus.Active,
            "superseded"  => AttachmentStatus.Superseded,
            "tombstoned"  => AttachmentStatus.Tombstoned,
            var other     => throw new JsonException($"Unknown AttachmentStatus '{other}'."),
        };

    public override void Write(Utf8JsonWriter writer, AttachmentStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            AttachmentStatus.Active      => "active",
            AttachmentStatus.Superseded  => "superseded",
            AttachmentStatus.Tombstoned  => "tombstoned",
            _ => throw new JsonException($"Unknown AttachmentStatus '{value}'."),
        });
    }
}

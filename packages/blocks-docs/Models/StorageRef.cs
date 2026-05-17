using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Docs.Models;

/// <summary>
/// Where the blob lives. The tier indicates which backing store the
/// attachment was put to; downstream readers route fetches accordingly.
/// </summary>
[JsonConverter(typeof(StorageRefKindJsonConverter))]
public enum StorageRefKind
{
    /// <summary>Inline-bytes — the blob is tiny enough to live in the catalog row itself (≤ 8 KB).</summary>
    Inline,

    /// <summary>Foundation-blob store — content-addressed via the foundation Blobs primitive.</summary>
    FoundationBlob,

    /// <summary>External URL — the blob lives in an external object store (S3, MinIO, etc.); the URL is opaque to the catalog.</summary>
    ExternalUrl,
}

internal sealed class StorageRefKindJsonConverter : JsonConverter<StorageRefKind>
{
    public override StorageRefKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "inline"          => StorageRefKind.Inline,
            "foundationBlob"  => StorageRefKind.FoundationBlob,
            "externalUrl"     => StorageRefKind.ExternalUrl,
            var other         => throw new JsonException($"Unknown StorageRefKind '{other}'."),
        };

    public override void Write(Utf8JsonWriter writer, StorageRefKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            StorageRefKind.Inline          => "inline",
            StorageRefKind.FoundationBlob  => "foundationBlob",
            StorageRefKind.ExternalUrl     => "externalUrl",
            _ => throw new JsonException($"Unknown StorageRefKind '{value}'."),
        });
    }
}

/// <summary>
/// Discriminated union over the three storage tiers. Exactly one of the
/// per-kind fields is populated for any given <see cref="Kind"/>:
///
/// <list type="bullet">
/// <item><see cref="StorageRefKind.Inline"/> — <see cref="InlineBytes"/> set.</item>
/// <item><see cref="StorageRefKind.FoundationBlob"/> — <see cref="FoundationCid"/> set.</item>
/// <item><see cref="StorageRefKind.ExternalUrl"/> — <see cref="ExternalUrl"/> set.</item>
/// </list>
///
/// <para>
/// The Inline tier is reserved for very small payloads (signature stamps,
/// tiny thumbnails). Anything larger goes to FoundationBlob (the canonical
/// local-first path) or ExternalUrl (S3-equivalent, when the host opts in).
/// </para>
/// </summary>
public sealed record StorageRef
{
    /// <summary>Which storage tier this ref points at.</summary>
    public required StorageRefKind Kind { get; init; }

    /// <summary>Inline-bytes payload. Non-null iff <see cref="Kind"/> = <see cref="StorageRefKind.Inline"/>.</summary>
    public ReadOnlyMemory<byte>? InlineBytes { get; init; }

    /// <summary>Foundation-blob content id. Non-null iff <see cref="Kind"/> = <see cref="StorageRefKind.FoundationBlob"/>.</summary>
    public string? FoundationCid { get; init; }

    /// <summary>External URL. Non-null iff <see cref="Kind"/> = <see cref="StorageRefKind.ExternalUrl"/>.</summary>
    public string? ExternalUrl { get; init; }

    /// <summary>Constructor for the Inline tier.</summary>
    public static StorageRef ForInline(ReadOnlyMemory<byte> bytes) =>
        new() { Kind = StorageRefKind.Inline, InlineBytes = bytes };

    /// <summary>Constructor for the FoundationBlob tier.</summary>
    public static StorageRef ForFoundationBlob(string cid)
    {
        if (string.IsNullOrWhiteSpace(cid))
            throw new ArgumentException("FoundationBlob cid is required.", nameof(cid));
        return new StorageRef { Kind = StorageRefKind.FoundationBlob, FoundationCid = cid };
    }

    /// <summary>Constructor for the ExternalUrl tier.</summary>
    public static StorageRef ForExternalUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("External URL is required.", nameof(url));
        return new StorageRef { Kind = StorageRefKind.ExternalUrl, ExternalUrl = url };
    }
}

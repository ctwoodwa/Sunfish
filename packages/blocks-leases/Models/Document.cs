namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// A document associated with a lease (e.g., signed PDF, disclosure form).
/// References blob storage by URI only — <c>IBlobStore</c> integration is deferred.
/// </summary>
public sealed record Document
{
    /// <summary>Unique identifier for this document.</summary>
    public required DocumentId Id { get; init; }

    /// <summary>Human-readable document title.</summary>
    public required string Title { get; init; }

    /// <summary>MIME content-type of the document (e.g., <c>application/pdf</c>).</summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// URI pointing to the blob in storage, or <see langword="null"/> when the blob
    /// has not yet been uploaded.
    /// </summary>
    public Uri? BlobUri { get; init; }
}

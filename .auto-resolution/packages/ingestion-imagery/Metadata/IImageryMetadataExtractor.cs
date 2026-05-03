namespace Sunfish.Ingestion.Imagery.Metadata;

/// <summary>
/// Extracts structured <see cref="ImageryMetadata"/> from an image stream. Implementations MUST be
/// resilient to malformed or non-image input — a corrupt stream must return
/// <see cref="ImageryMetadata.Empty"/>, not throw.
/// </summary>
public interface IImageryMetadataExtractor
{
    /// <summary>
    /// Extracts metadata from the given image stream. Returns <see cref="ImageryMetadata.Empty"/>
    /// when no usable metadata can be parsed; never throws for recognised-but-corrupt input.
    /// </summary>
    ImageryMetadata Extract(Stream imageContent);
}
